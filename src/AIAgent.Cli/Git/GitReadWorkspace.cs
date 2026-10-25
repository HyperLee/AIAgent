using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Git;

internal sealed class GitReadWorkspace(WorkspaceFiles files, Func<IGitReadAdapter> createAdapter, TimeProvider? clock = null) : IGitReadWorkspace
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private Task? _active;
    private bool _disposed;
    internal GitReadWorkspace(WorkspaceFiles files, string root) : this(files, () => new GitReadAdapter(root)) { }
    public Task<GitCaptureResult> CaptureAsync(CancellationToken cancellationToken) => RunAsync(null, cancellationToken);
    public Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken cancellationToken) => ValidateAsync(snapshot.Manifest, cancellationToken);
    public async Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken cancellationToken) => (await RunAsync(manifest, cancellationToken)).ErrorCode;
    private Task<GitCaptureResult> RunAsync(GitValidationManifest? manifest, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposed || _active is { IsCompleted: false }) return Task.FromResult(new GitCaptureResult("unavailable"));
            var task = ExecuteAsync(manifest, cancellationToken); _active = task; return task;
        }
    }
    private async Task<GitCaptureResult> ExecuteAsync(GitValidationManifest? manifest, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(GitLimits.CaptureTimeout - TimeSpan.FromSeconds(5), clock ?? TimeProvider.System);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token, deadline.Token);
        try
        {
            await using var adapter = createAdapter();
            var result = await files.CaptureGitAsync(adapter, manifest, linked.Token);
            return result.ErrorCode == "turn_cancelled" && deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested
                ? new("git_timed_out") : result;
        }
        catch (OperationCanceledException) { return new(cancellationToken.IsCancellationRequested || _lifetime.IsCancellationRequested ? "turn_cancelled" : "git_timed_out"); }
        catch (GitFailure failure) { return new(failure.Code); }
        catch (Exception) { return new("git_failed"); }
    }
    public async ValueTask DisposeAsync()
    {
        Task? active;
        lock (_gate) { if (_disposed) return; _disposed = true; _lifetime.Cancel(); active = _active; }
        if (active is not null) await active;
        _lifetime.Dispose();
    }
    public override string ToString() => "[GitReadWorkspace]";
}