using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Git;

internal sealed class GitDelivery(string path, GitLayer layer, GitSide side, int start, int end, bool fullDiff)
{
    internal string Path { get; } = path;
    internal GitLayer Layer { get; } = layer;
    internal GitSide Side { get; } = side;
    internal int Start { get; } = start;
    internal int End { get; } = end;
    internal bool FullDiff { get; } = fullDiff;
    public override string ToString() => "[GitDelivery]";
}

internal sealed class GitSnapshotCoordinator(IGitReadWorkspace workspace, ISensitiveDataPolicy policy, Func<string> nextId) : IAsyncDisposable
{
    private readonly List<GitDelivery> _pending = [];
    private readonly List<GitDelivery> _delivered = [];
    private long _generation;
    internal GitSnapshot? Current { get; private set; }
    internal GitSelection Selection { get; private set; } = GitSelection.All;
    internal bool Bound { get; private set; }
    internal bool Stale { get; private set; }
    internal long Generation => _generation;
    internal int EligibleChangeCount => Current?.Changes.Count(c => GitLimits.Selected(Selection, c.Layer) && c.ContentAvailable) ?? 0;
    internal int DeliveredChangeCount => Current?.Changes.Count(c => GitLimits.Selected(Selection, c.Layer) && c.ContentAvailable && _delivered.Any(d => d.Path == c.Path && d.Layer == c.Layer && d.FullDiff)) ?? 0;
    internal IReadOnlyList<GitExclusion> Exclusions => Current is { } snapshot ? Array.AsReadOnly(snapshot.Exclusions.Where(e => e.Layer is null || GitLimits.Selected(Selection, e.Layer.Value)).ToArray()) : Array.Empty<GitExclusion>();
    internal async Task<string> CaptureAsync(CancellationToken token)
    {
        if (Bound) return await ValidateAsync(token);
        Clear(); var generation = _generation;
        var result = await workspace.CaptureAsync(token);
        if (token.IsCancellationRequested || generation != _generation) return "turn_cancelled";
        if (result.ErrorCode != "none" || result.Snapshot is null) return result.ErrorCode;
        string id;
        try { id = nextId(); } catch (OverflowException) { return "unavailable"; }
        if (!policy.CanSendOrUse(id, ContentBoundary.ToolResult)) return "sensitive_content";
        Current = result.Snapshot.WithId(id);
        var projection = GitProjection.Result(ToolStatus.Success, "none", policy, data: w => GitProjection.Status(w, this));
        if (projection.Status != ToolStatus.Success) { Clear(); return projection.ErrorCode; }
        return "none";
    }
    internal void Bind(GitSelection selection)
    {
        if (Current is null || Stale || !Enum.IsDefined(selection)) throw new GitFailure("git_snapshot_required");
        Selection = selection; Bound = true; _pending.Clear(); _delivered.Clear();
    }
    internal async Task<string> ValidateAsync(CancellationToken token)
    {
        if (Current is not { } snapshot) return "git_snapshot_required";
        if (Stale) return "git_snapshot_stale";
        var generation = _generation; var error = await workspace.ValidateAsync(snapshot, token);
        if (generation != _generation || token.IsCancellationRequested) return "turn_cancelled";
        if (error != "none") Stale = true;
        return error;
    }
    internal Task<string> ValidateManifestAsync(GitValidationManifest manifest, CancellationToken token) => workspace.ValidateAsync(manifest, token);
    internal GitChange Require(string id, GitLayer layer, string path)
    {
        if (Current is null) throw new GitFailure("git_snapshot_required");
        if (Stale || id != Current.SnapshotId) throw new GitFailure("git_snapshot_stale");
        if (!GitLimits.Selected(Selection, layer)) throw new GitFailure("git_source_unavailable");
        return Current.Changes.FirstOrDefault(c => c.Path == path && c.Layer == layer) ?? throw new GitFailure("git_source_unavailable");
    }
    internal void PendingDiff(GitChange change, GitDiffData diff)
    {
        if (!Bound) return;
        foreach (var hunk in diff.Hunks)
        {
            _pending.Add(new(change.Path, change.Layer, GitSide.Old, hunk.OldStart, hunk.OldStart + hunk.OldCount - 1, true));
            _pending.Add(new(change.Path, change.Layer, GitSide.New, hunk.NewStart, hunk.NewStart + hunk.NewCount - 1, true));
        }
    }
    internal void PendingRange(GitChange change, GitSide side, int start, int end)
    { if (Bound && start > 0 && end >= start) _pending.Add(new(change.Path, change.Layer, side, start, end, false)); }
    internal void MarkDispatched() { if (!Bound || Stale) return; _delivered.AddRange(_pending); _pending.Clear(); }
    internal bool Delivered(string path, GitLayer layer, GitSide side, int start, int end) =>
        Enumerable.Range(start, end - start + 1).All(line => _delivered.Any(d => d.Path == path && d.Layer == layer && d.Side == side && line >= d.Start && line <= d.End));
    internal void Clear()
    { _generation = checked(_generation + 1); Current = null; Bound = false; Stale = false; Selection = GitSelection.All; _pending.Clear(); _delivered.Clear(); }
    internal void Unbind() { Bound = false; _pending.Clear(); _delivered.Clear(); }
    public async ValueTask DisposeAsync() { Clear(); await workspace.DisposeAsync(); }
    public override string ToString() => "[GitSnapshotCoordinator]";
}

internal sealed class GitSummary(string? snapshotId, GitSelection selection, string observation, int eligible = 0)
{
    internal string? SnapshotId { get; } = snapshotId;
    internal GitSelection Selection { get; } = selection;
    internal string Observation { get; } = observation;
    internal int EligibleChangeCount { get; } = eligible;
    public override string ToString() => "[GitSummary]";
}