namespace AIAgent.Cli.Workspace;

/// <summary>檔案與程序共用單一排他租約；取消不提早釋放執行權。</summary>
internal sealed class WorkspaceOperationCoordinator : IAsyncDisposable
{
    private readonly object _gate = new();
    private WorkspaceOperationLease? _active;
    private readonly LinkedList<Waiter> _waiters = [];
    private long _generation;
    private bool _disposed;
    internal bool IsDisposed { get { lock (_gate) return _disposed; } }
    internal long Generation { get { lock (_gate) return _generation; } }

    internal bool TryAcquire(CancellationToken cancellationToken, out WorkspaceOperationLease? lease)
    {
        lock (_gate)
        {
            lease = null;
            if (_disposed || _active is not null || _waiters.Count != 0 || cancellationToken.IsCancellationRequested) return false;
            lease = new(this, _generation, cancellationToken);
            _active = lease;
            return true;
        }
    }

    internal async Task<WorkspaceOperationLease> AcquireAsync(CancellationToken token)
    {
        Waiter waiter;
        lock (_gate)
        {
            token.ThrowIfCancellationRequested();
            if (_disposed) throw new ObjectDisposedException(nameof(WorkspaceOperationCoordinator));
            if (_active is null && _waiters.Count == 0) return _active = new(this, _generation, token);
            waiter = new(token); waiter.Node = _waiters.AddLast(waiter);
        }
        using var registration = token.Register(() =>
        {
            lock (_gate)
            {
                if (waiter.Node?.List is null) return;
                _waiters.Remove(waiter.Node); waiter.Node = null;
                waiter.Completion.TrySetException(new OperationCanceledException(token));
            }
        });
        return await waiter.Completion.Task;
    }
    private sealed class Waiter(CancellationToken token)
    {
        internal CancellationToken Token { get; } = token;
        internal LinkedListNode<Waiter>? Node;
        internal TaskCompletionSource<WorkspaceOperationLease> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private void CancelWaiters()
    {
        while (_waiters.First is { } node)
        { _waiters.RemoveFirst(); node.Value.Node = null; node.Value.Completion.TrySetException(new OperationCanceledException()); }
    }

    internal bool IsCurrent(WorkspaceOperationLease lease)
    { lock (_gate) return !_disposed && ReferenceEquals(_active, lease) && lease.Generation == _generation && !lease.CancellationToken.IsCancellationRequested; }

    internal bool IsActive(WorkspaceOperationLease lease)
    { lock (_gate) return ReferenceEquals(_active, lease); }

    internal void Clear()
    {
        WorkspaceOperationLease? active;
        lock (_gate) { _generation = checked(_generation + 1); active = _active; CancelWaiters(); }
        active?.Cancel();
    }

    internal void Release(WorkspaceOperationLease lease)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_active, lease)) return;
            _active = null;
            lease.Complete();
            while (!_disposed && _waiters.First is { } next)
            {
                _waiters.RemoveFirst(); next.Value.Node = null;
                if (next.Value.Token.IsCancellationRequested) { next.Value.Completion.TrySetException(new OperationCanceledException(next.Value.Token)); continue; }
                _active = new(this, _generation, next.Value.Token); next.Value.Completion.TrySetResult(_active); break;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        WorkspaceOperationLease? active;
        lock (_gate) { _disposed = true; active = _active; CancelWaiters(); }
        active?.Cancel();
        return new(active?.Completion ?? Task.CompletedTask);
    }
    public override string ToString() => "[WorkspaceOperationCoordinator]";
}

internal sealed class WorkspaceOperationLease : IDisposable, IAsyncDisposable
{
    private readonly WorkspaceOperationCoordinator _owner;
    private readonly CancellationTokenSource _cancellation;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;
    internal WorkspaceOperationLease(WorkspaceOperationCoordinator owner, long generation, CancellationToken cancellationToken)
    {
        _owner = owner; Generation = generation;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken = _cancellation.Token;
    }
    internal long Generation { get; }
    internal CancellationToken CancellationToken { get; }
    internal bool IsCurrent => _owner.IsCurrent(this);
    internal bool BelongsTo(WorkspaceOperationCoordinator owner) => ReferenceEquals(_owner, owner);
    internal Task Completion => _completion.Task;
    internal void Cancel()
    {
        try { _cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }
    internal void Complete() => _completion.TrySetResult();
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _owner.Release(this); _cancellation.Dispose();
    }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    public override string ToString() => "[WorkspaceOperationLease]";
}