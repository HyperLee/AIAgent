using System.Text.Json.Serialization;
namespace AIAgent.Cli.Core;

internal sealed partial class GenerationBudget
{
    private DelegationBudgetLease? _delegation;
    private int Reserved => _delegation?.Reserved ?? 0;
    internal bool TryReserveDelegation(long parentTurnId, int childCount, CancellationToken cancellationToken, out DelegationBudgetLease? lease)
    {
        lock (_gate)
        {
            lease = null;
            if (parentTurnId <= 0 || childCount is < 1 or > 2 || cancellationToken.IsCancellationRequested || _delegation is not null) return false;
            var remaining = Math.Min(_turnLimit - _turnStarted, _programLimit - _programStarted);
            if (remaining < 2 * childCount + 1) return false;
            var counts = Enumerable.Repeat(2, childCount).ToArray();
            for (var i = 0; i < counts.Length && remaining > counts.Sum() + 1; i++) counts[i]++;
            lease = _delegation = new(this, _gate, parentTurnId, counts); return true;
        }
    }
    internal bool BeginDelegated(DelegationBudgetLease lease, int index, CancellationToken token, Func<bool> current)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_delegation, lease) || token.IsCancellationRequested || !current() ||
                _programStarted >= _programLimit || _turnStarted >= _turnLimit || !lease.Take(index)) return false;
            _programStarted++; _turnStarted++; return true;
        }
    }
    internal void ReleaseDelegation(DelegationBudgetLease lease) { lock (_gate) { if (ReferenceEquals(_delegation, lease)) _delegation = null; } }
}

[JsonConverter(typeof(DelegationOpaqueConverter))]
internal sealed class DelegationBudgetLease(GenerationBudget owner, object gate, long parentTurnId, int[] counts) : IDisposable
{
    private readonly int[] _remaining = counts.ToArray();
    private int _parent = 1;
    private bool _disposed;
    internal long ParentTurnId { get; } = parentTurnId;
    internal int Reserved => _disposed ? 0 : _remaining.Sum() + _parent;
    internal int Remaining(int index) { lock (gate) return _disposed ? 0 : _remaining[index]; }
    internal bool TryBeginChild(int index, CancellationToken token, Func<bool> current) => owner.BeginDelegated(this, index, token, current);
    internal bool TryBeginParent(CancellationToken token, Func<bool> current) => owner.BeginDelegated(this, -1, token, current);
    internal bool Take(int index)
    {
        if (_disposed) return false;
        if (index == -1) { if (_parent == 0) return false; _parent = 0; return true; }
        if (index < 0 || index >= _remaining.Length || _remaining[index] == 0) return false;
        _remaining[index]--; return true;
    }
    internal void ReleaseChild(int index) { lock (gate) _remaining[index] = 0; }
    public void Dispose() { lock (gate) { if (_disposed) return; _disposed = true; Array.Clear(_remaining); _parent = 0; owner.ReleaseDelegation(this); } }
    public override string ToString() => "[DelegationBudgetLease]";
}