using AIAgent.Cli.Git;

namespace AIAgent.Cli.Workspace;

/// <summary>歷史與目前資格分開；只接受受控寫入持有的預留，不淘汰任何紀錄。</summary>
internal sealed class CheckpointStore(object gate, CheckpointSequence? sequence = null)
{
    private readonly Dictionary<long, CheckpointEntry> _entries = [];
    private readonly Dictionary<long, CheckpointReservation> _reservations = [];
    private readonly CheckpointSequence _sequence = sequence ?? new();
    internal bool IdsExhausted => _sequence.Exhausted;
    internal long StoreRevision { get; private set; }
    internal long LifetimeGeneration { get; private set; }
    internal long TotalBytes { get; private set; }
    internal int Count { get { lock (gate) return _entries.Count + _reservations.Count; } }
    internal IReadOnlyList<CheckpointEntry> Entries { get { lock (gate) return Array.AsReadOnly(_entries.Values.OrderByDescending(x => x.Record.Id).ToArray()); } }

    internal CheckpointReservation? Reserve(byte[] before, byte[] after)
    {
        lock (gate)
        {
            var bytes = (long)before.Length + after.Length;
            if (before.Length > CheckpointLimits.MaxCheckpointSideBytes || after.Length > CheckpointLimits.MaxCheckpointSideBytes ||
                Count >= CheckpointLimits.MaxCheckpointCount || TotalBytes + bytes > CheckpointLimits.MaxCheckpointBytes || !_sequence.TryNext(out var id)) return null;
            var reservation = new CheckpointReservation(this, id, LifetimeGeneration, bytes);
            _reservations.Add(reservation.Id, reservation); TotalBytes += bytes; Bump();
            return reservation;
        }
    }
    internal bool IsCurrent(CheckpointReservation reservation)
    {
        lock (gate) return reservation.Owner == this && reservation.Generation == LifetimeGeneration &&
            _reservations.TryGetValue(reservation.Id, out var owned) && ReferenceEquals(owned, reservation);
    }
    internal void Release(CheckpointReservation? reservation)
    {
        lock (gate)
        {
            if (reservation is null || !IsCurrent(reservation)) return;
            _reservations.Remove(reservation.Id); TotalBytes -= reservation.Bytes; Bump();
        }
    }
    internal bool Complete(CheckpointReservation reservation, CheckpointRecord record, CheckpointRecoveryAnchor? anchor, string? manualReason)
    {
        lock (gate)
        {
            if (!IsCurrent(reservation)) return false;
            if (reservation.Id != record.Id || reservation.Bytes != record.ByteCount) throw new InvalidOperationException("檢查點所有權不符");
            _reservations.Remove(reservation.Id);
            _entries.Add(record.Id, new(record, anchor is null ? CheckpointState.ManualRequired : CheckpointState.Available, anchor, manualReason));
            Bump(); return true;
        }
    }
    internal CheckpointEntry? Find(long id) { lock (gate) return _entries.GetValueOrDefault(id); }
    internal CheckpointEntry? Latest(string path)
    { lock (gate) return _entries.Values.Where(x => x.Record.Path == path && x.State != CheckpointState.Restored).MaxBy(x => x.Record.Id); }
    internal bool HasLater(CheckpointEntry entry)
    { lock (gate) return _entries.Values.Any(x => x.Record.Path == entry.Record.Path && x.Record.Id > entry.Record.Id && x.State != CheckpointState.Restored); }
    internal void MarkPathManual(string path, string reason)
    {
        lock (gate)
            foreach (var entry in _entries.Values.Where(x => x.Record.Path == path && x.State != CheckpointState.Restored))
            { entry.MakeManual(reason); Bump(); }
    }
    internal void MarkAllManual(string reason)
    { lock (gate) foreach (var path in _entries.Values.Select(x => x.Record.Path).Distinct().ToArray()) MarkPathManual(path, reason); }
    internal void Restored(CheckpointEntry entry, CheckpointRecoveryAnchor actual)
    {
        lock (gate)
        {
            if (!ReferenceEquals(Find(entry.Record.Id), entry)) return;
            entry.MarkRestored(); Bump();
            if (entry.Record.PredecessorId is { } id && Find(id) is { State: CheckpointState.Available } previous &&
                previous.Record.AfterEquals(entry.Record.CopyBefore()) && previous.Record.AfterMode == actual.Mode &&
                previous.Anchor is { } old && old.Guard.SameAs(actual.Guard))
            { previous.Rebind(actual); Bump(); }
        }
    }
    internal bool Forget(long id, long revision, long generation)
    {
        lock (gate)
        {
            if (revision != StoreRevision || generation != LifetimeGeneration || !_entries.Remove(id, out var removed)) return false;
            TotalBytes -= removed.Record.ByteCount; Bump();
            if (removed.State != CheckpointState.Restored)
            {
                var prior = removed.Record.PredecessorId;
                while (prior is { } predecessor && _entries.TryGetValue(predecessor, out var entry))
                { entry.MakeManual("chain_broken"); Bump(); prior = entry.Record.PredecessorId; }
            }
            return true;
        }
    }
    internal void Clear()
    {
        lock (gate) { _entries.Clear(); _reservations.Clear(); TotalBytes = 0; LifetimeGeneration = checked(LifetimeGeneration + 1); Bump(); }
    }
    private void Bump() => StoreRevision = checked(StoreRevision + 1);
    public override string ToString() => "[CheckpointStore]";
}

internal sealed class CheckpointSequence
{
    private readonly object _gate = new();
    private long _nextId;
    internal bool Exhausted { get { lock (_gate) return _nextId == long.MaxValue; } }
    internal bool TryNext(out long id)
    {
        lock (_gate)
        {
            id = 0;
            if (_nextId == long.MaxValue) return false;
            id = ++_nextId; return true;
        }
    }
    public override string ToString() => "[CheckpointSequence]";
}

internal sealed class CheckpointReservation(CheckpointStore owner, long id, long generation, long bytes)
{
    internal CheckpointStore Owner { get; } = owner;
    internal long Id { get; } = id;
    internal long Generation { get; } = generation;
    internal long Bytes { get; } = bytes;
    public override string ToString() => "[CheckpointReservation]";
}

internal sealed class CheckpointParent(string path, WorkspaceNodeState state)
{
    internal string Path { get; } = path;
    internal WorkspaceNodeState State { get; } = state;
    public override string ToString() => "[CheckpointParent]";
}

internal sealed class CheckpointRecoveryAnchor(WorkspaceNodeState target, UnixFileMode mode,
    IReadOnlyList<CheckpointParent> parents, CheckpointGitGuard guard)
{
    internal WorkspaceNodeState Target { get; } = target;
    internal UnixFileMode Mode { get; } = mode;
    internal IReadOnlyList<CheckpointParent> Parents { get; } = Array.AsReadOnly(parents.ToArray());
    internal CheckpointGitGuard Guard { get; } = guard;
    public override string ToString() => "[CheckpointRecoveryAnchor]";
}

internal sealed class CheckpointRecord(long id, FileChangeKind kind, string path, byte[] before, byte[] after,
    UnixFileMode beforeMode, UnixFileMode afterMode, WorkspaceNodeState? original, WorkspaceNodeState? published,
    IReadOnlyList<CheckpointParent> parents, long rulesRevision, string scope, Guid sourceOperationId, long? predecessorId)
{
    private readonly byte[] _before = before.ToArray();
    private readonly byte[] _after = after.ToArray();
    internal int CaptureVersion => 1;
    internal long Id { get; } = id;
    internal FileChangeKind Kind { get; } = kind;
    internal string Path { get; } = path;
    internal int BeforeBytes => _before.Length;
    internal int AfterBytes => _after.Length;
    internal long ByteCount => (long)BeforeBytes + AfterBytes;
    internal bool BeforeAbsent => Kind == FileChangeKind.Create;
    internal UnixFileMode BeforeMode { get; } = beforeMode;
    internal UnixFileMode AfterMode { get; } = afterMode;
    internal WorkspaceNodeState? Original { get; } = original;
    internal WorkspaceNodeState? Published { get; } = published;
    internal IReadOnlyList<CheckpointParent> Parents { get; } = Array.AsReadOnly(parents.ToArray());
    internal long RulesRevision { get; } = rulesRevision;
    internal string Scope { get; } = scope;
    internal Guid SourceOperationId { get; } = sourceOperationId;
    internal long? PredecessorId { get; } = predecessorId;
    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal bool AfterEquals(byte[] bytes) => _after.AsSpan().SequenceEqual(bytes);
    public override string ToString() => "[CheckpointRecord]";
}

internal sealed class CheckpointEntry(CheckpointRecord record, CheckpointState state, CheckpointRecoveryAnchor? anchor, string? reason)
{
    internal CheckpointRecord Record { get; } = record;
    internal CheckpointState State { get; private set; } = state;
    internal CheckpointRecoveryAnchor? Anchor { get; private set; } = anchor;
    internal string? ManualReason { get; private set; } = reason;
    internal void MakeManual(string reason) { State = CheckpointState.ManualRequired; Anchor = null; ManualReason = reason; }
    internal void MarkRestored() { State = CheckpointState.Restored; Anchor = null; ManualReason = null; }
    internal void Rebind(CheckpointRecoveryAnchor value) { Anchor = value; }
    public override string ToString() => "[CheckpointEntry]";
}