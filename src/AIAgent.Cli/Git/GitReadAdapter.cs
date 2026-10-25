namespace AIAgent.Cli.Git;

internal interface IGitReadAdapter : IAsyncDisposable
{
    string? ExecutableIdentity { get; }
    ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken);
}

internal sealed class GitReadAdapter(string confirmedRoot, IGitProcessRunner? processRunner = null) : IGitReadAdapter
{
    private readonly IGitProcessRunner _runner = processRunner ?? new GitProcessRunner();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _root = confirmedRoot;
    private GitInstallation? _installation;
    private bool _capabilityChecked;
    private int _disposed;
    public string? ExecutableIdentity => _installation?.Identity;
    public async ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            await _gate.WaitAsync(linked.Token);
            try
            {
                if (Volatile.Read(ref _disposed) != 0 || linked.IsCancellationRequested) return new("turn_cancelled");
                if (!GitProcessRecipe.ValidRequest(_root, recipe, request)) return new("git_failed");
                if (!GitProcessRecipe.WithinInputLimit(recipe, request)) return new("git_limit");
                if (!OperatingSystem.IsMacOS() || GitInstallation.Canonical(_root) != _root) return new("capability_unavailable");
                if (!_capabilityChecked)
                {
                    var admission = await AdmitAsync(request, linked.Token);
                    if (admission is not null) return admission;
                }
                if (_installation is null || !_installation.ValidateCurrent()) return new("git_unavailable");
                return await RunCore(recipe, request, linked.Token);
            }
            finally { _gate.Release(); }
        }
        catch (OperationCanceledException) { return new("turn_cancelled"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { return new("git_failed"); }
    }
    private async Task<GitReadResult?> AdmitAsync(GitReadRequest request, CancellationToken cancellationToken)
    {
        var code = "git_unavailable";
        foreach (var candidate in GitInstallation.Discover())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!candidate.ValidateCurrent()) continue;
            _installation = candidate;
            var compatible = true;
            foreach (var capability in new[] { GitReadRecipe.Version, GitReadRecipe.IntentToAddVisible, GitReadRecipe.IntentToAddInvisible })
            {
                var check = await RunCore(capability, new GitReadRequest(scope: request.Scope, ignoreReadFiles: request.IgnoreReadFiles), cancellationToken);
                if (check.ErrorCode == "none") continue;
                if (!check.CleanupConfirmed || check.ErrorCode is not ("git_failed" or "git_unavailable" or "capability_unavailable")) return check;
                code = check.ErrorCode == "capability_unavailable" ? "capability_unavailable" : "git_unavailable";
                compatible = false; break;
            }
            if (!compatible) continue;
            _capabilityChecked = true; return null;
        }
        _installation = null;
        return new(code);
    }
    private Task<GitReadResult> RunCore(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken) =>
        _runner.RunAsync(new GitProcessDescriptor(_root, _installation!.Identity, recipe, request), cancellationToken);
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel(); await _gate.WaitAsync(); _gate.Release();
    }
    public override string ToString() => "[GitReadAdapter]";
}