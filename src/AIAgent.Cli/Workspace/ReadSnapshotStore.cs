namespace AIAgent.Cli.Workspace;

/// <summary>與服務共用提交鎖；外部僅能查詢及清除，不能自行提交基準。</summary>
internal sealed class ReadSnapshotStore(object gate) : IReadSnapshotStore
{
    private readonly Dictionary<string, LinkedListNode<WorkspaceReadSnapshot>> _index = new(StringComparer.Ordinal);
    private readonly LinkedList<WorkspaceReadSnapshot> _order = new();
    private long _totalBytes;
    internal long Generation { get; private set; }
    public int Count { get { lock (gate) return _index.Count; } }
    public long TotalBytes { get { lock (gate) return _totalBytes; } }

    public bool TryGet(string relativePath, out WorkspaceReadSnapshot? snapshot)
    {
        lock (gate)
        {
            snapshot = null;
            if (!WorkspacePathRules.IsCanonical(relativePath) || !_index.TryGetValue(relativePath, out var node)) return false;
            snapshot = node.Value;
            return true;
        }
    }

    public void Clear()
    {
        lock (gate) { InvalidateAllForRootFailure(); Generation++; }
    }

    // Root failure ends the only admitted operation. Drop all baselines without consuming
    // the caller's Clear generation, so concurrent Clear still wins over a late failure.
    internal void InvalidateAllForRootFailure()
    {
        lock (gate) { _index.Clear(); _order.Clear(); _totalBytes = 0; }
    }

    internal void Remove(string relativePath)
    {
        lock (gate)
        {
            if (!_index.Remove(relativePath, out var node)) return;
            _totalBytes -= node.Value.OriginalByteCount;
            _order.Remove(node);
        }
    }

    internal void Publish(WorkspaceReadSnapshot snapshot)
    {
        lock (gate)
        {
            Remove(snapshot.RelativePath);
            while (_order.First is { } oldest && (_index.Count >= WorkspaceLimits.MaxSnapshotCount ||
                _totalBytes + snapshot.OriginalByteCount > WorkspaceLimits.MaxSnapshotBytes)) Remove(oldest.Value.RelativePath);
            _index.Add(snapshot.RelativePath, _order.AddLast(snapshot));
            _totalBytes += snapshot.OriginalByteCount;
        }
    }

    public override string ToString() => "[ReadSnapshotStore]";
}