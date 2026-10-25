using System.Text;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles
{
    private static readonly HashSet<string> SearchIgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    { "bin", "obj", "node_modules", ".vs", ".idea", "dist", "coverage", "TestResults", "logs", ".cache" };

    public Task<WorkspaceFileSearchResult> SearchFilesAsync(string path, string pattern, CancellationToken cancellationToken) =>
        RunAsync((operation, token) => SearchFilesCoreAsync(path, pattern, operation, token), WorkspaceFileSearchResult.End, cancellationToken);
    public Task<WorkspaceTextSearchResult> SearchTextAsync(string path, string query, string filePattern, bool caseSensitive,
        int contextLines, CancellationToken cancellationToken) => RunAsync((operation, token) =>
            SearchTextCoreAsync(path, query, filePattern, caseSensitive, contextLines, operation, token), WorkspaceTextSearchResult.End, cancellationToken);
    public Task<WorkspaceRangeReadResult> ReadFileRangeAsync(string path, int startLine, int endLine, CancellationToken cancellationToken) =>
        RunAsync((operation, token) => RangeCoreAsync(path, startLine, endLine, operation, token), WorkspaceRangeReadResult.End, cancellationToken);

    private DiscoveryPattern ValidatePattern(string pattern)
    {
        if (pattern is not null) CheckSensitive(pattern, ContentBoundary.ToolArguments);
        if (!DiscoveryPattern.TryParse(pattern, out var parsed)) throw Rejected(WorkspaceErrorCode.InvalidPattern);
        return parsed!;
    }

    private async Task<WorkspaceFileSearchResult> SearchFilesCoreAsync(string rawPath, string pattern, WorkspaceOperation operation, CancellationToken token)
    {
        var path = ValidateArgument(rawPath, false);
        await CheckRulesOperationAsync(path, true, operation, token);
        BeginTaskOperation(operation, "search_files", path);
        var glob = ValidatePattern(pattern);
        var scan = new DiscoveryScan(); operation.Discovery = scan; var matches = new SortedDictionary<string, WorkspaceFileMatch>(StringComparer.Ordinal);
        await WalkDiscoveryAsync(path, glob, scan, operation, token, (relative, state) =>
        {
            ObserveDiscovery(scan, Path.Combine(_root, relative), state);
            matches[relative] = new(relative);
            if (matches.Count > WorkspaceLimits.MaxSearchResults) { matches.Remove(matches.Last().Key); scan.Truncated = true; }
            return Task.FromResult(true);
        });
        WorkspaceFileSearchResult result;
        while (true)
        {
            CheckActive(operation.Generation, token);
            result = WorkspaceFileSearchResult.Success(new(path, matches.Values.ToArray(), scan.Truncated));
            if (WorkspaceResponseWriter.TryCreateResponse(result, _policy!, out _, out var error, token)) break;
            if (error != WorkspaceErrorCode.ResultTooLarge || matches.Count == 0) throw Rejected(error);
            matches.Remove(matches.Last().Key); scan.Truncated = true;
        }
        RevalidateDiscovery(scan, operation, token);
        await CheckRulesResultAsync(operation, token);
        lock (_gate) { CheckActive(operation.Generation, token); return result; }
    }

    private async Task<WorkspaceTextSearchResult> SearchTextCoreAsync(string rawPath, string query, string pattern, bool caseSensitive,
        int contextLines, WorkspaceOperation operation, CancellationToken token)
    {
        var path = ValidateArgument(rawPath, false);
        await CheckRulesOperationAsync(path, true, operation, token);
        BeginTaskOperation(operation, "search_text", path);
        if (contextLines is < 0 or > WorkspaceLimits.MaxSearchContextLines) throw Rejected(WorkspaceErrorCode.InvalidArguments);
        if (query is not null) CheckSensitive(query, ContentBoundary.ToolArguments);
        if (!DiscoveryPattern.ValidQuery(query)) throw Rejected(WorkspaceErrorCode.InvalidQuery);
        var glob = ValidatePattern(pattern); var scan = new DiscoveryScan(); operation.Discovery = scan; var budget = new DiscoveryReadBudget();
        var matches = new SortedDictionary<(string Path, int Line), WorkspaceTextMatch>(Comparer<(string Path, int Line)>.Create((a, b) =>
        { var pathOrder = string.Compare(a.Path, b.Path, StringComparison.Ordinal); return pathOrder == 0 ? a.Line.CompareTo(b.Line) : pathOrder; }));
        var candidates = 0;
        await WalkDiscoveryAsync(path, glob, scan, operation, token, async (relative, state) =>
        {
            CheckActive(operation.Generation, token);
            if (candidates >= WorkspaceLimits.MaxSearchCandidateFiles || budget.Remaining == 0) { scan.Truncated = true; return false; }
            candidates++;
            lock (_gate) { CheckActive(operation.Generation, token); _snapshots.Remove(relative); }
            ObserveDiscovery(scan, Path.Combine(_root, relative), state);
            RevalidateDiscovery(scan, operation, token);
            VerifiedWorkspaceFile? file = null;
            try
            {
                file = await ReadVerifiedFileAsync(relative, operation, token, budget: budget, expected: state,
                    observeMode: mode => scan.Modes[Path.Combine(_root, relative)] = mode);
                scan.Modes[Path.Combine(_root, relative)] = file.Mode;
            }
            catch (DiscoveryBudgetExhausted) { scan.Truncated = true; return false; }
            catch (WorkspaceFailure failure) when (failure.Code is WorkspaceErrorCode.FileTooLarge or WorkspaceErrorCode.InvalidUtf8 or WorkspaceErrorCode.BinaryContent)
            { RevalidateDiscovery(scan, operation, token); }
            if (file is not null)
            {
                var lines = WorkspaceTextLines.Split(file.Content, token);
                for (var index = 0; index < lines.Count; index++)
                {
                    CheckActive(operation.Generation, token);
                    var line = lines[index]; var text = file.Content[line.Start..line.ContentEnd];
                    if (!text.Contains(query!, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)) continue;
                    WorkspaceContextLine[] Context(int from, int to) => Enumerable.Range(from, to - from)
                        .Select(i => new WorkspaceContextLine(i + 1, file.Content[lines[i].Start..lines[i].ContentEnd])).ToArray();
                    var match = new WorkspaceTextMatch(relative, index + 1, text,
                        Context(Math.Max(0, index - contextLines), index), Context(index + 1, Math.Min(lines.Count, index + contextLines + 1)));
                    matches[(relative, index + 1)] = match;
                    if (matches.Count > WorkspaceLimits.MaxSearchResults) { matches.Remove(matches.Last().Key); scan.Truncated = true; }
                }
            }
            if (candidates == WorkspaceLimits.MaxSearchCandidateFiles || budget.Remaining == 0) { scan.Truncated = true; return false; }
            return true;
        });
        WorkspaceTextSearchResult result;
        while (true)
        {
            CheckActive(operation.Generation, token);
            result = WorkspaceTextSearchResult.Success(new(path, matches.Values.ToArray(), scan.Truncated));
            if (WorkspaceResponseWriter.TryCreateResponse(result, _policy!, out _, out var error, token)) break;
            if (error != WorkspaceErrorCode.ResultTooLarge || matches.Count == 0) throw Rejected(error);
            matches.Remove(matches.Last().Key); scan.Truncated = true;
        }
        RevalidateDiscovery(scan, operation, token);
        await CheckRulesResultAsync(operation, token);
        lock (_gate) { CheckActive(operation.Generation, token); return result; }
    }

    private async Task<WorkspaceRangeReadResult> RangeCoreAsync(string rawPath, int start, int end, WorkspaceOperation operation, CancellationToken token)
    {
        var path = ValidateArgument(rawPath, true);
        if (start < 1 || end < start || (long)end - start + 1 > WorkspaceLimits.MaxRangeLines) throw Rejected(WorkspaceErrorCode.InvalidLineRange);
        await CheckRulesOperationAsync(path, false, operation, token);
        BeginTaskOperation(operation, "read_file_range", path, start, end);
        lock (_gate) { CheckActive(operation.Generation, token); _snapshots.Remove(path); }
        var scan = new DiscoveryScan(); operation.Discovery = scan;
        foreach (var observation in InspectPath(path, WorkspaceNodeKind.File, operation)) ObserveDiscovery(scan, observation.Path, observation.State);
        var file = await ReadVerifiedFileAsync(path, operation, token, expected: scan.Observed[Path.Combine(_root, path)]);
        scan.Modes[Path.Combine(_root, path)] = file.Mode;
        var lines = WorkspaceTextLines.Split(file.Content, token);
        if (start > Math.Max(1, lines.Count)) throw Rejected(WorkspaceErrorCode.InvalidLineRange);
        var last = Math.Min(end, lines.Count);
        var content = lines.Count == 0 ? "" : file.Content[lines[start - 1].Start..lines[last - 1].End];
        var data = new WorkspaceRangeReadData(path, lines.Count == 0 ? null : start, lines.Count == 0 ? null : last,
            lines.Count, content, file.Bytes.LongLength, Encoding.UTF8.GetByteCount(content), file.HasBom, last < lines.Count);
        var result = WorkspaceRangeReadResult.Success(data);
        if (!WorkspaceResponseWriter.TryCreateResponse(result, _policy!, out _, out var error, token)) throw Rejected(error);
        RevalidateDiscovery(scan, operation, token);
        await CheckRulesResultAsync(operation, token);
        lock (_gate) { CheckActive(operation.Generation, token); return result; }
    }

    private async Task WalkDiscoveryAsync(string path, DiscoveryPattern pattern, DiscoveryScan scan, WorkspaceOperation operation,
        CancellationToken token, Func<string, WorkspaceNodeState, Task<bool>> onFile)
    {
        CheckActive(operation.Generation, token);
        var observations = InspectPath(path, WorkspaceNodeKind.Directory, operation);
        foreach (var observation in observations) ObserveDiscovery(scan, observation.Path, observation.State);
        async Task<bool> Walk(string directory, int depth)
        {
            await CheckRulesOperationAsync(directory, true, operation, token);
            RevalidateDiscovery(scan, operation, token);
            var children = new List<(string Relative, string Name, WorkspaceNodeState State)>();
            using (var entries = _fileSystem.EnumerateNames(directory == "." ? _root : Path.Combine(_root, directory)).GetEnumerator())
            {
                while (scan.Scanned < WorkspaceLimits.MaxSearchScannedEntries)
                {
                    CheckActive(operation.Generation, token);
                    if (!entries.MoveNext()) break;
                    scan.Scanned++;
                    CheckActive(operation.Generation, token);
                    var name = entries.Current;
                    CheckSensitive(name, ContentBoundary.ToolResult);
                    var relative = directory == "." ? name : directory + "/" + name;
                    CheckSensitive(relative, ContentBoundary.ToolResult);
                    if (name.Contains('/') || name is "." or ".." || !WorkspacePathRules.IsCanonical(relative) || WorkspacePathRules.IsSensitive(relative)) continue;
                    var state = _fileSystem.Inspect(Path.Combine(_root, relative));
                    if (state.Kind == WorkspaceNodeKind.Missing) throw Rejected(WorkspaceErrorCode.PathChanged);
                    if (state.Kind is not (WorkspaceNodeKind.File or WorkspaceNodeKind.Directory)) continue;
                    if (state.Kind == WorkspaceNodeKind.Directory && SearchIgnoredDirectories.Contains(name)) continue;
                    children.Add((relative, name, state));
                }
            }
            if (scan.Scanned == WorkspaceLimits.MaxSearchScannedEntries) scan.Truncated = true;
            RevalidateDiscovery(scan, operation, token);
            children.Sort((a, b) => string.Compare(a.Relative, b.Relative, StringComparison.Ordinal));
            foreach (var child in children)
            {
                CheckActive(operation.Generation, token);
                if (child.State.Kind == WorkspaceNodeKind.Directory)
                {
                    if (depth == WorkspaceLimits.MaxSearchDepth) { scan.Truncated = true; continue; }
                    if (scan.Scanned == WorkspaceLimits.MaxSearchScannedEntries) continue;
                    ObserveDiscovery(scan, Path.Combine(_root, child.Relative), child.State);
                    if (!await Walk(child.Relative, depth + 1)) return false;
                }
                else
                {
                    var fromStart = path == "." ? child.Relative : child.Relative[(path.Length + 1)..];
                    if (pattern.Matches(fromStart, token) && !await onFile(child.Relative, child.State)) return false;
                }
            }
            return true;
        }
        await Walk(path, 0);
    }

    private void ObserveDiscovery(DiscoveryScan scan, string path, WorkspaceNodeState state)
    {
        if (scan.Observed.TryGetValue(path, out var original) && original != state)
        { InvalidateObservation(path, original); throw Rejected(WorkspaceErrorCode.PathChanged); }
        scan.Observed[path] = state;
    }

    private void RevalidateDiscovery(DiscoveryScan scan, WorkspaceOperation operation, CancellationToken token)
    {
        CheckActive(operation.Generation, token);
        var root = InspectRoot();
        if (scan.Observed.TryGetValue(_root, out var originalRoot) && root != originalRoot)
        { _snapshots.InvalidateAllForRootFailure(); throw Rejected(WorkspaceErrorCode.PathChanged); }
        foreach (var item in scan.Observed)
        {
            CheckActive(operation.Generation, token);
            if (item.Key == _root) continue;
            WorkspaceNodeState actual;
            try { actual = _fileSystem.Inspect(item.Key); }
            catch { InvalidateObservation(item.Key, item.Value); throw; }
            if (actual != item.Value) { InvalidateObservation(item.Key, item.Value); throw Rejected(WorkspaceErrorCode.PathChanged); }
            if (scan.Modes.TryGetValue(item.Key, out var mode) && _fileSystem is IWorkspaceWriteFileSystem modes && modes.GetMode(item.Key) != mode)
            { InvalidateObservation(item.Key, item.Value); throw Rejected(WorkspaceErrorCode.PathChanged); }
        }
    }

    private void InvalidateObservation(string fullPath, WorkspaceNodeState state)
    {
        if (fullPath == _root || state.Kind == WorkspaceNodeKind.Directory) _snapshots.InvalidateAllForRootFailure();
        else if (WorkspacePathRules.IsWithinRoot(_root, fullPath)) _snapshots.Remove(Path.GetRelativePath(_root, fullPath));
        else _snapshots.InvalidateAllForRootFailure();
    }

    private sealed class DiscoveryScan
    {
        internal Dictionary<string, WorkspaceNodeState> Observed { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, UnixFileMode> Modes { get; } = new(StringComparer.Ordinal);
        internal int Scanned { get; set; }
        internal bool Truncated { get; set; }
    }

    private sealed class DiscoveryReadBudget
    {
        internal int Remaining { get; private set; } = WorkspaceLimits.MaxSearchReadBytes;
        internal void Consume(int count) => Remaining -= count;
    }
    private sealed class DiscoveryBudgetExhausted : Exception;
}