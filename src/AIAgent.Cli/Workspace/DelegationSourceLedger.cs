using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

[JsonConverter(typeof(DelegationOpaqueConverter))]
internal sealed class DelegationSourceLedger
{
    internal sealed class Source(string path, WorkspaceNodeState state, string? hash = null, UnixFileMode? mode = null)
    {
        internal string Path { get; } = path;
        internal WorkspaceNodeState State { get; } = state;
        internal string? Hash { get; } = hash;
        internal UnixFileMode? Mode { get; } = mode;
        internal bool ExistenceDelivered;
        internal List<(int Start, int End)> Ranges { get; } = [];
        public override string ToString() => "[DelegationSource]";
    }
    private readonly Dictionary<string, Source> _observed = new(StringComparer.Ordinal);
    private readonly List<(Source Source, int? Start, int? End)> _pending = [];
    private readonly Dictionary<string, Source> _delivered = new(StringComparer.Ordinal);
    internal bool Truncated { get; private set; }
    internal int SourceCount => _delivered.Count;
    internal IReadOnlyList<Source> Sources => Array.AsReadOnly(_delivered.Values.ToArray());
    internal void Observe(string path, WorkspaceNodeState state, byte[]? bytes = null, UnixFileMode? mode = null)
    { _observed[path] = new(path, state, bytes is null ? null : Convert.ToHexStringLower(SHA256.HashData(bytes)), mode); }
    internal void BeginTool() => _observed.Clear();
    internal bool CanStartBatch(IReadOnlyList<LlmToolCall> calls)
    {
        var known = _delivered.Keys.Concat(_pending.Select(p => p.Source.Path)).ToHashSet(StringComparer.Ordinal);
        var newPaths = new HashSet<string>(StringComparer.Ordinal); var scans = 0;
        foreach (var call in calls)
        {
            if (call.Name is "list_files" or "search_files" or "search_text") { scans++; continue; }
            if (call.Arguments.ValueKind == JsonValueKind.Object && call.Arguments.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String &&
                WorkspacePathRules.TryNormalize(path.GetString()!, out var canonical, out _) && !known.Contains(canonical)) newPaths.Add(canonical);
        }
        return known.Count + newPaths.Count + scans <= 16;
    }
    internal void AddPending(string tool, JsonElement response)
    {
        if (!response.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return;
        var additions = new List<(Source, int?, int?)>();
        void Add(string path, int? start, int? end)
        {
            if (!_observed.TryGetValue(path, out var source)) throw new DelegationFailure("delegation_source_changed");
            additions.Add((source, start, end));
        }
        if (tool == "list_files" && data.TryGetProperty("entries", out var entries))
            foreach (var entry in entries.EnumerateArray()) Add(entry.GetProperty("path").GetString()!, null, null);
        else if (tool == "search_files" && data.TryGetProperty("matches", out var paths))
            foreach (var entry in paths.EnumerateArray()) Add(entry.GetProperty("path").GetString()!, null, null);
        else if (tool == "search_text" && data.TryGetProperty("matches", out var matches))
            foreach (var entry in matches.EnumerateArray())
            {
                var line = entry.GetProperty("line_number").GetInt32(); var start = line; var end = line;
                foreach (var name in new[] { "before_lines", "after_lines" })
                    if (entry.TryGetProperty(name, out var context)) foreach (var row in context.EnumerateArray())
                        { var n = row.GetProperty("line_number").GetInt32(); start = Math.Min(start, n); end = Math.Max(end, n); }
                Add(entry.GetProperty("path").GetString()!, start, end);
            }
        else if (tool is "read_file" or "read_file_range")
        {
            var path = data.GetProperty("path").GetString()!;
            if (tool == "read_file")
            { var count = WorkspaceTextLines.Split(data.GetProperty("content").GetString()!, default).Count; if (count > 0) Add(path, 1, count); }
            else if (data.GetProperty("start_line").ValueKind == JsonValueKind.Number) Add(path, data.GetProperty("start_line").GetInt32(), data.GetProperty("end_line").GetInt32());
        }
        if (_delivered.Keys.Concat(_pending.Select(p => p.Source.Path)).Concat(additions.Select(p => p.Item1.Path)).Distinct(StringComparer.Ordinal).Count() > 16 ||
            _pending.Count + additions.Count > 16) throw new DelegationFailure("delegation_limit");
        _pending.AddRange(additions);
        Truncated |= data.TryGetProperty("truncated", out var truncated) && truncated.ValueKind == JsonValueKind.True;
        _observed.Clear();
    }
    internal void MarkDispatched()
    {
        foreach (var (source, start, end) in _pending)
        {
            if (_delivered.TryGetValue(source.Path, out var previous))
            {
                if (previous.State != source.State || previous.Hash is not null && source.Hash is not null && previous.Hash != source.Hash)
                    throw new DelegationFailure("delegation_source_changed");
                if (source.Hash is null || previous.Hash is not null) { if (start is null) previous.ExistenceDelivered = true; else previous.Ranges.Add((start.Value, end!.Value)); continue; }
                source.ExistenceDelivered = previous.ExistenceDelivered; source.Ranges.AddRange(previous.Ranges);
            }
            _delivered[source.Path] = source;
            if (start is null) source.ExistenceDelivered = true; else source.Ranges.Add((start.Value, end!.Value));
        }
        _pending.Clear();
    }
    internal bool Delivered(string path, int? start, int? end) => _delivered.TryGetValue(path, out var source) &&
        (start is null && end is null ? source.ExistenceDelivered : start is > 0 && end >= start && source.Hash is not null &&
        Enumerable.Range(start.Value, end!.Value - start.Value + 1).All(line => source.Ranges.Any(r => line >= r.Start && line <= r.End)));
    internal void Clear() { _observed.Clear(); _pending.Clear(); _delivered.Clear(); }
    public override string ToString() => "[DelegationSourceLedger]";
}
internal sealed class DelegationReadSession(WorkspaceFiles files, DelegationSourceLedger ledger) : IAsyncDisposable
{
    internal IWorkspaceFiles Files { get; } = new DelegationReadFacade(files);
    internal DelegationSourceLedger Ledger { get; } = ledger;
    internal Task<string> ValidateAsync(CancellationToken token) => files.ValidateDelegationSourcesAsync(Ledger, token);
    public async ValueTask DisposeAsync() { await files.DisposeAsync(); Ledger.Clear(); }
    public override string ToString() => "[DelegationReadSession]";
}
internal sealed class DelegationReadFacade(WorkspaceFiles files) : IWorkspaceFiles
{
    public IReadSnapshotStore Snapshots => files.Snapshots;
    public Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken token) => files.ListFilesAsync(path, token);
    public Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken token) => files.ReadFileAsync(path, token);
    public Task<WorkspaceFileSearchResult> SearchFilesAsync(string path, string pattern, CancellationToken token) => files.SearchFilesAsync(path, pattern, token);
    public Task<WorkspaceTextSearchResult> SearchTextAsync(string path, string query, string pattern, bool sensitive, int context, CancellationToken token) => files.SearchTextAsync(path, query, pattern, sensitive, context, token);
    public Task<WorkspaceRangeReadResult> ReadFileRangeAsync(string path, int start, int end, CancellationToken token) => files.ReadFileRangeAsync(path, start, end, token);
    public Task<WorkspaceWriteResult> CreateFileAsync(string path, string content, CancellationToken token) => Task.FromResult(WorkspaceWriteResult.End(WorkspaceStatus.Rejected, WorkspaceErrorCode.ModeRestricted));
    public Task<WorkspaceWriteResult> ReplaceTextAsync(string path, string oldText, string newText, CancellationToken token) => Task.FromResult(WorkspaceWriteResult.End(WorkspaceStatus.Rejected, WorkspaceErrorCode.ModeRestricted));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public override string ToString() => "[DelegationReadFacade]";
}