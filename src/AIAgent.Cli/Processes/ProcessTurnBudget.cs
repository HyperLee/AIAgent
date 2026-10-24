namespace AIAgent.Cli.Processes;

/// <summary>每個一般訊息的固定程序額度；核准等待不計入。</summary>
internal sealed class ProcessTurnBudget(TimeProvider? timeProvider = null)
{
    private readonly object _gate = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private int _attempts;
    private TimeSpan _elapsed;
    private ProcessBudgetLease? _active;
    internal bool CanStart { get { lock (_gate) return _active is null && _attempts < 2 && _elapsed < TimeSpan.FromSeconds(300); } }
    internal void BeginTurn()
    {
        lock (_gate)
        {
            if (_active is not null) throw new InvalidOperationException("程序尚未收尾");
            _attempts = 0; _elapsed = TimeSpan.Zero;
        }
    }
    internal TimeSpan PreviewTimeout(DotnetTaskKind task)
    {
        lock (_gate)
        {
            if (!Enum.IsDefined(task) || !CanStart) return TimeSpan.Zero;
            var remaining = TimeSpan.FromSeconds(300) - _elapsed;
            var recipe = TimeSpan.FromSeconds(task == DotnetTaskKind.Build ? 120 : 180);
            return remaining < recipe ? remaining : recipe;
        }
    }
    internal bool TryStart(DotnetTaskKind task, out ProcessBudgetLease? lease)
    {
        lock (_gate)
        {
            lease = null;
            if (!Enum.IsDefined(task) || !CanStart) return false;
            var remaining = TimeSpan.FromSeconds(300) - _elapsed;
            var recipe = TimeSpan.FromSeconds(task == DotnetTaskKind.Build ? 120 : 180);
            lease = new(this, _time, remaining < recipe ? remaining : recipe);
            _attempts++; _active = lease; return true;
        }
    }
    internal void Finish(ProcessBudgetLease lease, TimeSpan elapsed)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_active, lease)) return;
            _elapsed += elapsed; _active = null;
        }
    }
    public override string ToString() => "[ProcessTurnBudget]";
}

internal sealed class ProcessBudgetLease : IDisposable
{
    private readonly ProcessTurnBudget _owner;
    private readonly TimeProvider _time;
    private readonly long _start;
    private TimeSpan? _elapsed;
    internal ProcessBudgetLease(ProcessTurnBudget owner, TimeProvider time, TimeSpan timeout)
    { _owner = owner; _time = time; _start = time.GetTimestamp(); Timeout = timeout; }
    internal TimeSpan Timeout { get; }
    internal long ElapsedMilliseconds => (long)(_elapsed ?? _time.GetElapsedTime(_start)).TotalMilliseconds;
    public void Dispose()
    {
        if (_elapsed is not null) return;
        _elapsed = _time.GetElapsedTime(_start);
        _owner.Finish(this, _elapsed.Value);
    }
    public override string ToString() => "[ProcessBudgetLease]";
}