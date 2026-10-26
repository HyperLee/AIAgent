using System.Globalization;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    private async Task<CheckpointGitGuard> CaptureCheckpointGitGuardAsync(string path, WorkspaceOperation operation, CancellationToken token)
    {
        // Leave the existing native runner's five-second cleanup reserve inside the total deadline.
        using var deadline = new CancellationTokenSource(GitLimits.CaptureTimeout - TimeSpan.FromSeconds(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, operation.Lease!.CancellationToken, _lifetime.Token, deadline.Token);
        var adapter = _checkpointGitAdapter ?? new GitReadAdapter(_root);
        try
        {
            CheckActive(operation.Generation, linked.Token); RequireRulesLease(operation.Lease);
            if (ValidateArgument(path, true) != path) throw Rejected(WorkspaceErrorCode.InvalidPath);
            if (!operation.CheckpointSettling) await CheckRulesOperationAsync(path, false, operation, linked.Token);
            var first = await ObserveCheckpointGitAsync(path, operation, adapter, linked.Token);
            var second = await ObserveCheckpointGitAsync(path, operation, adapter, linked.Token);
            if (!first.SameAs(second)) throw new GitFailure("checkpoint_git_conflict");
            if (!operation.CheckpointSettling) await CheckRulesResultAsync(operation, linked.Token);
            CheckActive(operation.Generation, linked.Token); RequireRulesLease(operation.Lease);
            return first;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !token.IsCancellationRequested &&
            !operation.Lease.CancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
        { throw new GitFailure("checkpoint_manual_required"); }
        catch (GitFailure failure) when (failure.Code == "turn_cancelled")
        {
            if (deadline.IsCancellationRequested && !token.IsCancellationRequested &&
                !operation.Lease.CancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
                throw new GitFailure("checkpoint_manual_required");
            throw new OperationCanceledException(linked.Token);
        }
        catch (GitFailure failure) when (failure.Code is not ("checkpoint_git_conflict" or "sensitive_content" or "sensitive_path" or "process_cleanup_failed" or "process_unconfirmed"))
        { throw new GitFailure("checkpoint_manual_required"); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException or DecoderFallbackException or OverflowException)
        { throw new GitFailure("checkpoint_manual_required"); }
        finally
        {
            if (_checkpointGitAdapter is null)
            {
                try { await adapter.DisposeAsync(); }
                catch (Exception) { throw new GitFailure("process_cleanup_failed"); }
            }
        }
    }

    private async Task<CheckpointGitGuard> ObserveCheckpointGitAsync(string path, WorkspaceOperation operation, IGitReadAdapter adapter, CancellationToken token)
    {
        CheckActive(operation.Generation, token); RequireRulesLease(operation.Lease!);
        var root = InspectRoot();
        var gitdir = _fileSystem.Inspect(Path.Combine(_root, ".git"));
        var observations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["root-path"] = _root,
            ["root"] = GitState(root),
            ["target"] = path,
            ["gitdir"] = gitdir.Kind == WorkspaceNodeKind.Missing ? "Absent" : GitState(gitdir)
        };
        CheckSensitive(_root, ContentBoundary.ToolArguments);
        if (gitdir.Kind == WorkspaceNodeKind.Missing)
        {
            if (!operation.CheckpointSettling) await CheckRulesResultAsync(operation, token);
            if (GitState(InspectRoot()) != GitState(root) || _fileSystem.Inspect(Path.Combine(_root, ".git")).Kind != WorkspaceNodeKind.Missing)
                throw new GitFailure("checkpoint_git_conflict");
            return Complete(false);
        }
        if (gitdir.Kind != WorkspaceNodeKind.Directory) throw new GitFailure("checkpoint_manual_required");
        ValidateLayout(); CheckCheckpointGitFixedInputs(path, adapter);

        async Task<(int Exit, byte[] Bytes)> Run(GitReadRecipe recipe, string format = "sha1", string? oid = null, string? headReference = null, bool absence = false)
        {
            CheckActive(operation.Generation, token); RequireRulesLease(operation.Lease!);
            var request = new GitReadRequest(scope: path, objectId: oid, headReference: headReference, objectFormat: format);
            foreach (var argument in GitProcessRecipe.Arguments(_root, recipe, request)) CheckSensitive(argument, ContentBoundary.ToolArguments);
            CheckSensitive(Encoding.UTF8.GetString(GitProcessRecipe.Input(recipe, request)), ContentBoundary.ToolArguments);
            var result = await adapter.RunAsync(recipe, request, token);
            if (!result.CleanupConfirmed) throw new GitFailure(result.ErrorCode == "process_unconfirmed" ? "process_unconfirmed" : "process_cleanup_failed");
            if (result.ErrorCode != "none") throw new GitFailure(result.ErrorCode);
            if (result.ExitCode is null || result.ExitCode != 0 && !absence) throw new GitFailure("checkpoint_manual_required");
            var bytes = result.Stdout.ToArray();
            if (bytes.Length > GitProcessRecipe.MetadataLimit) throw new GitFailure("checkpoint_manual_required");
            var text = new UTF8Encoding(false, true).GetString(bytes);
            CheckSensitive(text, ContentBoundary.ToolResult);
            CheckActive(operation.Generation, token); RequireRulesLease(operation.Lease!);
            return (result.ExitCode.Value, bytes);
        }
        async Task<string> Text(GitReadRecipe recipe, bool absence = false)
        {
            var response = await Run(recipe, absence: absence);
            if (response.Exit == 1 && absence && response.Bytes.Length == 0) return "";
            if (response.Exit != 0) throw new GitFailure("checkpoint_manual_required");
            return new UTF8Encoding(false, true).GetString(response.Bytes).TrimEnd('\n');
        }
        _ = await Run(GitReadRecipe.Version);
        if (await Text(GitReadRecipe.InsideWorkTree) != "true" || await Text(GitReadRecipe.BareRepository) != "false")
            throw new GitFailure("checkpoint_manual_required");
        var format = await Text(GitReadRecipe.ObjectFormat);
        if (format is not ("sha1" or "sha256")) throw new GitFailure("checkpoint_manual_required");
        foreach (var recipe in new[] { GitReadRecipe.SparseCheckoutConfig, GitReadRecipe.SparseIndexConfig, GitReadRecipe.WorktreeConfig, GitReadRecipe.BareConfig })
        {
            var value = await Text(recipe, true);
            if (value.Length == 0) continue;
            value = value.TrimEnd('\0');
            if (recipe == GitReadRecipe.WorktreeConfig)
            {
                if (Path.GetFullPath(value, Path.Combine(_root, ".git")) != _root) throw new GitFailure("checkpoint_manual_required");
            }
            else if (!new[] { "false", "no", "off", "0" }.Contains(value, StringComparer.OrdinalIgnoreCase))
                throw new GitFailure("checkpoint_manual_required");
        }
        var symbolic = await Run(GitReadRecipe.SymbolicHead, format, absence: true);
        if (symbolic.Exit is not (0 or 1) || symbolic.Exit == 1 && symbolic.Bytes.Length != 0) throw new GitFailure("checkpoint_manual_required");
        var reference = symbolic.Exit == 0 ? new UTF8Encoding(false, true).GetString(symbolic.Bytes).TrimEnd('\n') : null;
        if (reference is not null && !GitProcessRecipe.ValidRequest(_root, GitReadRecipe.VerifyHeadRef, new(headReference: reference, objectFormat: format)))
            throw new GitFailure("checkpoint_manual_required");
        var resolved = await Run(GitReadRecipe.VerifyHead, format, absence: true);
        string? head = null;
        if (resolved.Exit == 0)
        {
            head = Encoding.ASCII.GetString(resolved.Bytes).TrimEnd('\n');
            if (!GitManifestParser.Oid(head, format)) throw new GitFailure("checkpoint_manual_required");
        }
        else
        {
            if (reference is null || resolved.Exit is not (1 or 128) || resolved.Bytes.Length != 0)
                throw new GitFailure("checkpoint_manual_required");
            var absent = await Run(GitReadRecipe.VerifyHeadRef, format, headReference: reference, absence: true);
            if (absent.Exit != 1 || absent.Bytes.Length != 0) throw new GitFailure("checkpoint_manual_required");
        }
        observations["symbolic-head"] = reference is null ? "Detached" : reference;
        observations["head"] = head ?? "Unborn";
        observations["object-format"] = format;
        var headBytes = head is null ? [] : (await Run(GitReadRecipe.HeadManifest, format, oid: head)).Bytes;
        var indexBytes = (await Run(GitReadRecipe.IndexManifest, format)).Bytes;
        observations["head-entry"] = Entry(headBytes, false);
        observations["index-entry"] = Entry(indexBytes, true);
        observations["ita-visible"] = Intent((await Run(GitReadRecipe.IntentToAddVisible, format, oid: head)).Bytes);
        observations["ita-invisible"] = Intent((await Run(GitReadRecipe.IntentToAddInvisible, format, oid: head)).Bytes);
        observations["executable"] = adapter.ExecutableIdentity ?? throw new GitFailure("checkpoint_manual_required");
        if (observations["executable"].Length == 0) throw new GitFailure("checkpoint_manual_required");
        ValidateLayout();
        if (GitState(InspectRoot()) != GitState(root) || GitState(_fileSystem.Inspect(Path.Combine(_root, ".git"))) != GitState(gitdir))
            throw new GitFailure("checkpoint_git_conflict");
        if (!operation.CheckpointSettling) await CheckRulesResultAsync(operation, token);
        return Complete(true);

        string Entry(byte[] bytes, bool index)
        {
            var entries = GitManifestParser.Entries(bytes, format, index);
            if (entries.Count == 0) return "Absent";
            if (entries.Count != 1 || entries[0] is not { } entry || entry.Path != path || entry.Stage != 0 || entry.Tag != 'H' || entry.Mode is not ("100644" or "100755"))
                throw new GitFailure("checkpoint_manual_required");
            if (!index && !Encoding.ASCII.GetString(bytes).StartsWith(entry.Mode + " blob ", StringComparison.Ordinal))
                throw new GitFailure("checkpoint_manual_required");
            return string.Join(':', entry.Mode, entry.Oid, entry.Stage.ToString(CultureInfo.InvariantCulture), entry.Tag.ToString());
        }
        string Intent(byte[] bytes)
        {
            var changes = GitManifestParser.RawDiff(bytes, format);
            if (changes.Count == 0 && bytes.Length == 0) return "Absent";
            if (changes.Count != 1 || !changes.TryGetValue(path, out var change)) throw new GitFailure("checkpoint_manual_required");
            var modes = change.Split(' ');
            if (modes[0][1..] is not ("000000" or "100644" or "100755") || modes[1] is not ("000000" or "100644" or "100755"))
                throw new GitFailure("checkpoint_manual_required");
            return change;
        }
        void ValidateLayout()
        {
            foreach (var name in new[] { ".git/HEAD", ".git/config", ".git/index", ".git/objects", ".git/objects/pack", ".git/objects/info", ".git/refs", ".git/info" })
            {
                var state = _fileSystem.Inspect(Path.Combine(_root, name));
                if (state.Kind is WorkspaceNodeKind.Link or WorkspaceNodeKind.Other) throw new GitFailure("checkpoint_manual_required");
            }
            foreach (var name in new[] { ".git/commondir", ".git/objects/info/alternates", ".git/objects/info/http-alternates", ".git/info/sparse-checkout", ".git/sharedindex" })
                if (_fileSystem.Inspect(Path.Combine(_root, name)).Kind != WorkspaceNodeKind.Missing) throw new GitFailure("checkpoint_manual_required");
        }
        CheckpointGitGuard Complete(bool repository)
        {
            observations["repository"] = repository ? "RootRepository" : "NotRootRepository";
            foreach (var pair in observations) { CheckSensitive(pair.Key, ContentBoundary.ToolResult); CheckSensitive(pair.Value, ContentBoundary.ToolResult); }
            return new(repository, observations);
        }
    }

    private void CheckCheckpointGitFixedInputs(string path, IGitReadAdapter adapter)
    {
        foreach (var recipe in new[] { GitReadRecipe.Version, GitReadRecipe.InsideWorkTree, GitReadRecipe.BareRepository, GitReadRecipe.ObjectFormat,
            GitReadRecipe.SymbolicHead, GitReadRecipe.VerifyHead, GitReadRecipe.VerifyHeadRef, GitReadRecipe.SparseCheckoutConfig, GitReadRecipe.SparseIndexConfig,
            GitReadRecipe.WorktreeConfig, GitReadRecipe.BareConfig, GitReadRecipe.HeadManifest, GitReadRecipe.IndexManifest, GitReadRecipe.IntentToAddVisible, GitReadRecipe.IntentToAddInvisible })
            foreach (var argument in GitProcessRecipe.Arguments(_root, recipe, new(scope: path, objectId: new string('0', 40), headReference: "refs/heads/placeholder")))
                CheckSensitive(argument, ContentBoundary.ToolArguments);
        foreach (var pair in GitProcessRecipe.Environment()) CheckSensitive(pair.Key + "=" + pair.Value, ContentBoundary.ToolArguments);
        foreach (var value in new[] { Path.Combine(AppContext.BaseDirectory, "ProcessHost", "AIAgent.ProcessHost"), "/usr/local/share/dotnet", "/opt/homebrew/share/dotnet", "DOTNET_ROOT", "DOTNET_EnableDiagnostics=0" })
            CheckSensitive(value, ContentBoundary.ToolArguments);
        if (adapter is not GitReadAdapter) return;
        foreach (var installation in GitInstallation.Discover())
            foreach (var value in installation.RuntimeFiles.Prepend(installation.Executable).Append(installation.Identity)) CheckSensitive(value, ContentBoundary.ToolArguments);
    }
}