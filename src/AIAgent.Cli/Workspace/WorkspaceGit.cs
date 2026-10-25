using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    internal async Task<GitCaptureResult> CaptureGitAsync(IGitReadAdapter adapter, GitValidationManifest? expected, CancellationToken token)
    {
        if (!_executionPolicy.Allows(ToolCapability.GitRead)) return new("mode_restricted");
        if (!_executionPolicy.WorkspaceOperations.TryAcquire(token, out var lease)) return new("unavailable");
        await using var owned = lease!;
        using var generation = _executionPolicy.Plans.BorrowGeneration();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lease!.CancellationToken, _lifetime.Token);
        _activePlanGeneration = _executionPolicy.Plans.ExecutionGeneration;
        var operation = new WorkspaceOperation(_snapshots.Generation, _activePlanGeneration) { Lease = lease };
        try
        {
            CheckSensitive(_root, ContentBoundary.ToolArguments);
            var rules = _executionPolicy.Rules!;
            var error = await rules.ValidateAsync(linked.Token, lease);
            if (error != "none") return new(error);
            var stamp = rules.Capture(); operation.RulesStamp = stamp;
            var first = await CollectGitAsync(adapter, operation, stamp, linked.Token, expected is not null);
            if (expected is not null)
                return new(expected.Scope == first.Scope && expected.Fingerprint == first.Manifest.Fingerprint ? "none" : "git_snapshot_stale");
            var second = await CollectGitAsync(adapter, operation, stamp, linked.Token, true);
            if (first.Manifest.Fingerprint != second.Manifest.Fingerprint) return new("git_snapshot_stale");
            if (!rules.IsCurrent(stamp)) return new("rules_changed");
            linked.Token.ThrowIfCancellationRequested();
            return new("none", first);
        }
        catch (GitFailure failure) { return new(failure.Code); }
        catch (WorkspaceFailure failure) { return new(WorkspaceToolProjection.Code(failure.Code)); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return new("git_failed"); }
        finally { _executionPolicy.Rules?.Settle(); }
    }

    private async Task<GitSnapshot> CollectGitAsync(IGitReadAdapter adapter, WorkspaceOperation operation, ProjectRulesStamp stamp, CancellationToken token, bool validationOnly = false)
    {
        var scan = new GitScan(validationOnly);
        void Observe(string name, WorkspaceNodeState state) => scan.Observations[name] = GitState(state);
        var root = InspectRoot(); Observe("root", root);
        var gitdir = _fileSystem.Inspect(Path.Combine(_root, ".git")); Observe("gitdir", gitdir);
        if (gitdir.Kind == WorkspaceNodeKind.Missing) throw new GitFailure("not_git_repository");
        if (gitdir.Kind != WorkspaceNodeKind.Directory) throw new GitFailure("unsupported_git_layout");
        CheckGitFixedInputs(adapter);
        foreach (var name in new[] { ".git/HEAD", ".git/config", ".git/index", ".git/objects", ".git/objects/pack", ".git/refs" })
        {
            var state = _fileSystem.Inspect(Path.Combine(_root, name)); Observe(name, state);
            if (state.Kind is WorkspaceNodeKind.Link or WorkspaceNodeKind.Other) throw new GitFailure("unsupported_git_layout");
        }
        foreach (var name in new[] { ".git/commondir", ".git/objects/info/alternates", ".git/objects/info/http-alternates", ".git/info/sparse-checkout", ".git/sharedindex" })
        {
            var state = _fileSystem.Inspect(Path.Combine(_root, name)); Observe(name, state);
            if (state.Kind != WorkspaceNodeKind.Missing) throw new GitFailure("unsupported_git_layout");
        }
        async Task<(int Exit, byte[] Bytes)> Run(GitReadRecipe recipe, string? oid = null, string? headRef = null, IEnumerable<string>? paths = null, IEnumerable<string>? ignores = null, string format = "sha1", bool absence = false)
        {
            CheckActive(operation.Generation, token); RequireRulesLease(operation.Lease!);
            var request = new GitReadRequest(stamp.Scope, oid, headRef, paths, ignores, format);
            foreach (var argument in GitProcessRecipe.Arguments(_root, recipe, request)) CheckSensitive(argument, ContentBoundary.ToolArguments);
            CheckSensitive(Encoding.UTF8.GetString(GitProcessRecipe.Input(recipe, request)), ContentBoundary.ToolArguments);
            foreach (var file in request.IgnoreReadFiles) CheckSensitive(file, ContentBoundary.ToolArguments);
            var response = await adapter.RunAsync(recipe, request, token);
            if (!response.CleanupConfirmed) throw new GitFailure("process_cleanup_failed");
            if (response.ErrorCode != "none") throw new GitFailure(response.ErrorCode);
            if (response.ExitCode is null || response.ExitCode != 0 && !absence) throw new GitFailure("git_failed");
            var bytes = response.Stdout.ToArray();
            CheckSensitive(Encoding.UTF8.GetString(bytes), ContentBoundary.ToolResult);
            return (response.ExitCode.Value, bytes);
        }
        async Task<string> Text(GitReadRecipe recipe, bool absence = false)
        {
            var value = await Run(recipe, absence: absence);
            if (absence && value.Exit == 1) return "";
            if (value.Exit != 0) throw new GitFailure("git_failed");
            try { return new UTF8Encoding(false, true).GetString(value.Bytes).TrimEnd('\n'); }
            catch (DecoderFallbackException) { throw new GitFailure("git_failed"); }
        }
        _ = await Run(GitReadRecipe.Version);
        if (await Text(GitReadRecipe.InsideWorkTree) != "true" || await Text(GitReadRecipe.BareRepository) != "false") throw new GitFailure("unsupported_git_layout");
        var format = await Text(GitReadRecipe.ObjectFormat);
        if (format is not ("sha1" or "sha256")) throw new GitFailure("git_unavailable");
        foreach (var kind in new[] { GitReadRecipe.SparseCheckoutConfig, GitReadRecipe.SparseIndexConfig, GitReadRecipe.WorktreeConfig, GitReadRecipe.BareConfig })
        {
            var value = await Text(kind, true);
            if (value.Length == 0) continue;
            value = value.TrimEnd('\0');
            if (kind == GitReadRecipe.WorktreeConfig)
            { if (Path.GetFullPath(value, Path.Combine(_root, ".git")) != _root) throw new GitFailure("unsupported_git_layout"); }
            else if (!new[] { "false", "no", "off", "0" }.Contains(value, StringComparer.OrdinalIgnoreCase)) throw new GitFailure("unsupported_git_layout");
        }
        var symbolic = await Run(GitReadRecipe.SymbolicHead, absence: true, format: format);
        if (symbolic.Exit is not (0 or 1)) throw new GitFailure("git_failed");
        var headResult = await Run(GitReadRecipe.VerifyHead, absence: true, format: format);
        string? head = null;
        if (headResult.Exit == 0)
        {
            head = Encoding.ASCII.GetString(headResult.Bytes).TrimEnd('\n');
            if (!GitManifestParser.Oid(head, format)) throw new GitFailure("git_failed");
        }
        else
        {
            if (symbolic.Exit != 0) throw new GitFailure("git_failed");
            var reference = new UTF8Encoding(false, true).GetString(symbolic.Bytes).TrimEnd('\n');
            var exists = await Run(GitReadRecipe.VerifyHeadRef, headRef: reference, format: format, absence: true);
            if (exists.Exit != 1) throw new GitFailure("git_failed");
        }
        scan.Observations["HEAD"] = head ?? "unborn";
        scan.Observations["format"] = format;
        scan.Observations["rules"] = FormattableString.Invariant($"{stamp.SessionGeneration}/{stamp.RulesGeneration}/{stamp.RulesRevision}/{stamp.Scope}");
        var headBytes = head is null ? [] : (await Run(GitReadRecipe.HeadManifest, oid: head, format: format)).Bytes;
        var indexBytes = (await Run(GitReadRecipe.IndexManifest, format: format)).Bytes;
        var oldEntries = GitManifestParser.Entries(headBytes, format, false);
        var indexEntries = GitManifestParser.Entries(indexBytes, format, true);
        scan.Scanned = checked(oldEntries.Count + indexEntries.Count);
        if (scan.Scanned > GitLimits.MaxScannedEntries) throw new GitFailure("git_limit");
        scan.Observations["tree-manifest"] = Digest(headBytes); scan.Observations["index-manifest"] = Digest(indexBytes);
        var visibleBytes = (await Run(GitReadRecipe.IntentToAddVisible, oid: head, format: format)).Bytes;
        var invisibleBytes = (await Run(GitReadRecipe.IntentToAddInvisible, oid: head, format: format)).Bytes;
        scan.Observations["ita-visible"] = Digest(visibleBytes); scan.Observations["ita-invisible"] = Digest(invisibleBytes);
        var visible = GitManifestParser.RawDiff(visibleBytes, format); var invisible = GitManifestParser.RawDiff(invisibleBytes, format);
        var ita = visible.Keys.Union(invisible.Keys, StringComparer.Ordinal).Where(p => !visible.TryGetValue(p, out var a) || !invisible.TryGetValue(p, out var b) || a != b).ToHashSet(StringComparer.Ordinal);
        var tracked = oldEntries.Concat(indexEntries).Where(x => x.Path is not null).Select(x => x.Path!).ToHashSet(StringComparer.Ordinal);
        var work = new SortedDictionary<string, WorkspaceNodeState>(StringComparer.Ordinal);
        async Task Walk(string directory, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > GitLimits.MaxDepth) throw new GitFailure("git_limit");
            var boundary = await CheckProjectRulesPathAsync(stamp.Scope, directory, true, operation.RuleBoundaries, operation.Lease!, token);
            if (boundary == WorkspaceErrorCode.RulesScopeMismatch) { scan.Exclude(null, null, "scope_boundary"); return; }
            if (boundary != WorkspaceErrorCode.None) throw new GitFailure(WorkspaceToolProjection.Code(boundary));
            var node = _fileSystem.Inspect(directory == "." ? _root : Path.Combine(_root, directory)); Observe("dir:" + directory, node);
            using var names = _fileSystem.EnumerateNames(directory == "." ? _root : Path.Combine(_root, directory)).GetEnumerator();
            var children = new List<(string Path, WorkspaceNodeState State)>();
            while (names.MoveNext())
            {
                token.ThrowIfCancellationRequested();
                if (checked(++scan.Scanned) > GitLimits.MaxScannedEntries) throw new GitFailure("git_limit");
                var name = names.Current; var path = directory == "." ? name : directory + "/" + name;
                if (path == ".git") continue;
                if (name.Contains('/') || !WorkspacePathRules.IsCanonical(path) || WorkspacePathRules.IsSensitive(path)) { scan.Exclude(null, null, "safety_filtered"); continue; }
                CheckSensitive(path, ContentBoundary.ToolResult);
                var state = _fileSystem.Inspect(Path.Combine(_root, path));
                if (state.Kind == WorkspaceNodeKind.Missing) throw new GitFailure("git_snapshot_stale");
                children.Add((path, state));
            }
            foreach (var child in children.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                if (child.State.Kind == WorkspaceNodeKind.Directory) await Walk(child.Path, depth + 1);
                else { work[child.Path] = child.State; Observe("work:" + child.Path, child.State); }
            }
        }
        await Walk(stamp.Scope, stamp.Scope == "." ? 0 : stamp.Scope.Split('/').Length);
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in oldEntries.Concat(indexEntries))
        {
            if (entry.Path is not { } path || WorkspacePathRules.IsSensitive(path)) { scan.Exclude(null, null, "safety_filtered"); continue; }
            CheckSensitive(path, ContentBoundary.ToolResult);
            if (path.Split('/').Length > GitLimits.MaxDepth + 1) { scan.Exclude(path, null, "depth_limit"); continue; }
            if (stamp.Scope != "." && !path.StartsWith(stamp.Scope + "/", StringComparison.Ordinal)) { scan.Exclude(null, null, "scope_boundary"); continue; }
            if (await GitPathAllowedAsync(path, operation, stamp, scan, token)) allowed.Add(path);
        }
        var ignored = new HashSet<string>(StringComparer.Ordinal); var ignoreAvailable = true;
        var candidates = work.Keys.Where(p => !indexEntries.Any(x => x.Path == p)).ToArray();
        var ignoreFiles = new HashSet<string>(StringComparer.Ordinal) { ".git/info/exclude" };
        foreach (var path in candidates)
        {
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
            while (true)
            {
                ignoreFiles.Add(parent.Length == 0 ? ".gitignore" : parent + "/.gitignore");
                if (parent.Length == 0) break;
                parent = Path.GetDirectoryName(parent)?.Replace('\\', '/') ?? "";
            }
        }
        var approvedIgnores = new List<string>();
        foreach (var path in ignoreFiles.Order(StringComparer.Ordinal))
        {
            var state = _fileSystem.Inspect(Path.Combine(_root, path)); Observe("ignore:" + path, state);
            if (state.Kind == WorkspaceNodeKind.Missing) continue;
            try
            {
                var file = await ReadVerifiedFileAsync(path, operation, token, expected: state, maxBytes: GitLimits.MaxSourceBytes);
                scan.AddBytes("ignore:" + path, file.Bytes.Length); scan.Observations["ignore-bytes:" + path] = Digest(file.Bytes);
                approvedIgnores.Add(Path.Combine(_root, path));
            }
            catch (WorkspaceFailure failure) when (failure.Code is not (WorkspaceErrorCode.SensitiveContent or WorkspaceErrorCode.WorkspaceUnavailable or WorkspaceErrorCode.RulesChanged)) { ignoreAvailable = false; }
        }
        if (ignoreAvailable && candidates.Length > 0)
        {
            try
            {
                var response = await Run(GitReadRecipe.CheckIgnore, paths: candidates, ignores: approvedIgnores, format: format, absence: true);
                if (response.Exit is not (0 or 1)) throw new GitFailure("ignore_unavailable");
                var fields = GitManifestParser.Fields(response.Bytes);
                if (fields.Count != candidates.Length * 4) throw new GitFailure("ignore_unavailable");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < fields.Count; i += 4)
                {
                    var path = new UTF8Encoding(false, true).GetString(fields[i + 3]);
                    if (path.StartsWith("./", StringComparison.Ordinal)) path = path[2..];
                    if (!candidates.Contains(path, StringComparer.Ordinal) || !seen.Add(path)) throw new GitFailure("ignore_unavailable");
                    var source = new UTF8Encoding(false, true).GetString(fields[i]); var pattern = new UTF8Encoding(false, true).GetString(fields[i + 2]);
                    var line = new UTF8Encoding(false, true).GetString(fields[i + 1]);
                    if (source.Length == 0 ? line.Length != 0 || pattern.Length != 0 : pattern.Length == 0 || !int.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out var lineNumber) || lineNumber <= 0)
                        throw new GitFailure("ignore_unavailable");
                    if (source.Length > 0 && !ignoreFiles.Contains(source) && !approvedIgnores.Contains(source, StringComparer.Ordinal)) throw new GitFailure("ignore_unavailable");
                    if (source.Length > 0 && pattern.Length > 0 && pattern[0] != '!') ignored.Add(path);
                }
                scan.Observations["ignored"] = string.Join('\n', ignored.Order(StringComparer.Ordinal));
            }
            catch (GitFailure failure) when (failure.Code is not ("sensitive_content" or "process_cleanup_failed" or "process_unconfirmed" or "turn_cancelled")) { ignoreAvailable = false; }
            catch (DecoderFallbackException) { ignoreAvailable = false; }
        }
        if (!ignoreAvailable) scan.Exclude(null, GitLayer.Untracked, "ignore_unavailable");
        if (ignored.Count > 0) scan.Exclude(null, GitLayer.Untracked, "ignored_by_allowed_sources");
        var changes = new List<GitChange>(); var blobCache = new Dictionary<string, GitSource>(StringComparer.Ordinal); var changedPaths = new HashSet<string>(StringComparer.Ordinal);
        async Task<GitSource> Blob(string path, GitManifestEntry? entry, GitLayer layer, GitSide side)
        {
            if (entry is null) return GitSource.Absent(path, layer, side);
            if (entry.Stage != 0 || entry.Mode is not ("100644" or "100755") || ita.Contains(path))
                return GitSource.Unsupported(path, layer, side, entry.Mode, entry.Oid, null, ita.Contains(path) ? "intent_to_add" : entry.Stage != 0 ? "unmerged" : "unsupported_metadata");
            var key = path + ":" + entry.Oid + ":" + entry.Mode;
            if (blobCache.TryGetValue(key, out var cached)) return cached.ForSide(layer, side);
            var header = (await Run(GitReadRecipe.BatchCheck, oid: entry.Oid, format: format)).Bytes;
            var info = Encoding.ASCII.GetString(header).TrimEnd('\n').Split(' ');
            if (info.Length != 3 || info[0] != entry.Oid || info[1] != "blob" || !int.TryParse(info[2], NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size < 0) return GitSource.Unsupported(path, layer, side, entry.Mode, entry.Oid, null, "git_source_unavailable");
            if (size > GitLimits.MaxSourceBytes) return GitSource.Unsupported(path, layer, side, entry.Mode, entry.Oid, size, "file_too_large");
            var raw = (await Run(GitReadRecipe.Batch, oid: entry.Oid, format: format)).Bytes;
            var end = Array.IndexOf(raw, (byte)'\n');
            if (end < 0 || !raw.AsSpan(0, end + 1).SequenceEqual(header) || raw.Length != end + 1 + size + 1 || raw[^1] != '\n') throw new GitFailure("git_failed");
            scan.Observations["blob-bytes:" + path + ":" + entry.Oid] = Digest(raw.AsSpan(end + 1, size).ToArray());
            GitSource source;
            try { source = new(path, layer, side, raw.AsSpan(end + 1, size).ToArray(), entry.Mode, entry.Oid); }
            catch (GitFailure failure) when (failure.Code is "invalid_utf8" or "binary_content") { source = GitSource.Unsupported(path, layer, side, entry.Mode, entry.Oid, size, failure.Code); }
            blobCache.Add(key, source); return source;
        }
        async Task<GitSource> Work(string path, GitLayer layer)
        {
            var state = _fileSystem.Inspect(Path.Combine(_root, path)); Observe("source-state:" + path, state);
            if (state.Kind == WorkspaceNodeKind.Missing) return GitSource.Absent(path, layer, GitSide.New);
            if (state.Kind != WorkspaceNodeKind.File) return GitSource.Unsupported(path, layer, GitSide.New, null, null, null, state.Kind == WorkspaceNodeKind.Link ? "symbolic_link" : "wrong_entry_type");
            try
            {
                var file = await ReadVerifiedFileAsync(path, operation, token, expected: state, rawGitObservation: true);
                var mode = (file.Mode & UnixFileMode.UserExecute) != 0 ? "100755" : "100644";
                scan.Observations["work-bytes:" + path] = Digest(file.Bytes);
                try { return new(path, layer, GitSide.New, file.Bytes, mode); }
                catch (GitFailure failure) when (failure.Code is "invalid_utf8" or "binary_content")
                { return GitSource.Unsupported(path, layer, GitSide.New, mode, null, file.Bytes.Length, failure.Code); }
            }
            catch (WorkspaceFailure failure) when (failure.Code is WorkspaceErrorCode.FileTooLarge or WorkspaceErrorCode.InvalidUtf8 or WorkspaceErrorCode.BinaryContent or WorkspaceErrorCode.SymbolicLink or WorkspaceErrorCode.WrongEntryType)
            { return GitSource.Unsupported(path, layer, GitSide.New, null, null, state.Length, WorkspaceToolProjection.Code(failure.Code)); }
        }
        void Add(GitChange change)
        {
            if (!change.HasChange && change.ExclusionReason is null) return;
            change = new(change.Path, change.Layer, scan.Retain(change.Old), scan.Retain(change.New), change.ExclusionReason);
            if (!validationOnly) changes.Add(change);
            if (change.ExclusionReason is { } reason) scan.Exclude(change.Path, change.Layer, reason);
            else if (!change.ContentAvailable) scan.Exclude(change.Path, change.Layer, "metadata_only");
            if (changedPaths.Add(change.Path) && changedPaths.Count > GitLimits.MaxChangedPaths) throw new GitFailure("git_limit");
        }
        foreach (var path in allowed.Order(StringComparer.Ordinal))
        {
            var old = oldEntries.FirstOrDefault(x => x.Path == path); var index = indexEntries.FirstOrDefault(x => x.Path == path);
            if (old?.Oid != index?.Oid || old?.Mode != index?.Mode || index is { Stage: not 0 } || ita.Contains(path))
                Add(new(path, GitLayer.Staged, await Blob(path, old, GitLayer.Staged, GitSide.Old), await Blob(path, index, GitLayer.Staged, GitSide.New)));
            if (index is not null) Add(new(path, GitLayer.Unstaged, await Blob(path, index, GitLayer.Unstaged, GitSide.Old), await Work(path, GitLayer.Unstaged)));
            // Only retained changes own source bytes; comparison versions must not accumulate for clean paths.
            blobCache.Clear();
        }
        if (ignoreAvailable)
            foreach (var path in candidates.Where(p => !ignored.Contains(p)))
            {
                if (!await GitPathAllowedAsync(path, operation, stamp, scan, token)) continue;
                Add(new(path, GitLayer.Untracked, GitSource.Absent(path, GitLayer.Untracked, GitSide.Old), await Work(path, GitLayer.Untracked)));
            }
        scan.Observations["executable"] = adapter.ExecutableIdentity ?? throw new GitFailure("git_unavailable");
        foreach (var value in scan.Observations) { CheckSensitive(value.Key, ContentBoundary.ToolResult); CheckSensitive(value.Value, ContentBoundary.ToolResult); }
        var rulesError = await _executionPolicy.Rules!.ValidateAsync(token, operation.Lease);
        if (rulesError != "none") throw new GitFailure(rulesError);
        if (!_executionPolicy.Rules.IsCurrent(stamp)) throw new GitFailure("rules_changed");
        if (RevalidateProjectRulesPaths(operation.RuleBoundaries, operation.Lease!) != WorkspaceErrorCode.None) throw new GitFailure("git_snapshot_stale");
        return new("", stamp.Scope, head, format, changes, scan.Exclusions, new(stamp.Scope, scan.Observations.Select(x => new GitObservation(x.Key, x.Value))), stamp);
    }

    private Task<bool> GitPathAllowedAsync(string path, WorkspaceOperation operation, ProjectRulesStamp stamp, GitScan scan, CancellationToken token)
    {
        var current = _root; var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            token.ThrowIfCancellationRequested(); current = Path.Combine(current, segments[i]);
            var state = _fileSystem.Inspect(current);
            if (state.Kind == WorkspaceNodeKind.Missing) break;
            if (state.Kind == WorkspaceNodeKind.Link || i < segments.Length - 1 && state.Kind != WorkspaceNodeKind.Directory)
            { scan.Exclude(path, null, "symbolic_link"); return Task.FromResult(false); }
            if (i >= segments.Length - 1) continue;
            var relative = string.Join('/', segments.Take(i + 1));
            if (relative == stamp.Scope || stamp.Scope.StartsWith(relative + "/", StringComparison.Ordinal)) continue;
            var rules = _fileSystem.Inspect(Path.Combine(current, "AGENTS.md"));
            scan.Observations["boundary:" + relative] = GitState(rules);
            if (rules.Kind != WorkspaceNodeKind.Missing) { scan.Exclude(null, null, "scope_boundary"); return Task.FromResult(false); }
        }
        return Task.FromResult(true);
    }
    private void CheckGitFixedInputs(IGitReadAdapter adapter)
    {
        var placeholderOid = new string('0', 40); const string placeholderRef = "refs/heads/placeholder";
        foreach (var recipe in Enum.GetValues<GitReadRecipe>())
            foreach (var argument in GitProcessRecipe.Arguments(_root, recipe, new(objectId: placeholderOid, headReference: placeholderRef)))
                if (argument != placeholderOid && argument != placeholderRef) CheckSensitive(argument, ContentBoundary.ToolArguments);
        foreach (var pair in GitProcessRecipe.Environment()) CheckSensitive(pair.Key + "=" + pair.Value, ContentBoundary.ToolArguments);
        foreach (var value in new[] { Path.Combine(AppContext.BaseDirectory, "ProcessHost", "AIAgent.ProcessHost"), "/usr/local/share/dotnet", "/opt/homebrew/share/dotnet", "DOTNET_ROOT", "DOTNET_EnableDiagnostics=0" })
            CheckSensitive(value, ContentBoundary.ToolArguments);
        if (adapter is not GitReadAdapter) return;
        foreach (var installation in GitInstallation.Discover())
            foreach (var value in installation.RuntimeFiles.Prepend(installation.Executable).Append(installation.Identity)) CheckSensitive(value, ContentBoundary.ToolArguments);
    }
    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string GitState(WorkspaceNodeState state) => state.Kind == WorkspaceNodeKind.Directory
        ? FormattableString.Invariant($"{state.Kind}:{state.Device}:{state.Identity}:{state.Mode}")
        : FormattableString.Invariant($"{state.Kind}:{state.Device}:{state.Identity}:{state.Mode}:{state.Length}:{state.ModifiedSeconds}:{state.ModifiedNanoseconds}:{state.Links}");
    private sealed class GitScan(bool validationOnly)
    {
        internal int Scanned { get; set; }
        internal Dictionary<string, string> Observations { get; } = new(StringComparer.Ordinal);
        internal List<GitExclusion> Exclusions { get; } = [];
        private readonly HashSet<string> _counted = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GitSource> _retained = new(StringComparer.Ordinal);
        private int _bytes;
        internal GitSource Retain(GitSource source)
        {
            if (source.Sha256 is null) return source;
            var key = source.Path + ":" + source.Sha256;
            if (validationOnly) { AddBytes(key, checked((int)source.ByteCount!.Value)); return source; }
            if (_retained.TryGetValue(key, out var version)) return source.ShareVersion(version);
            AddBytes(key, checked((int)source.ByteCount!.Value)); _retained.Add(key, source); return source;
        }
        internal void AddBytes(string key, int count) { if (_counted.Add(key) && checked(_bytes += count) > GitLimits.MaxSnapshotSourceBytes) throw new GitFailure("git_limit"); }
        internal void Exclude(string? path, GitLayer? layer, string reason)
        { if (!Exclusions.Any(x => x.Path == path && x.Layer == layer && x.Reason == reason)) Exclusions.Add(new(path, layer, reason)); }
    }
}