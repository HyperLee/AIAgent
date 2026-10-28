using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpLifecycleTests
{
    [TestMethod]
    public async Task MCPAC025_CandidateCloseFailureStillReleasesWorkspaceLease()
    {
        await using var f = new McpFixture(); await f.Initialize();
        await using var coordinator = new McpCoordinator(f.Execution, f.Session.Policy, new Security.McpContentGuard(f.Session.Policy, []),
            (_, _) => new(McpApprovalDecision.Declined), () => { }, _ => new FailingCloseClient());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await coordinator.ConnectAsync(default));
        var acquired = f.Execution.WorkspaceOperations.TryAcquire(default, out var lease);
        ReleaseAfterRegressionFailure(f.Execution.WorkspaceOperations, lease);
        Assert.IsTrue(acquired); Assert.IsTrue(coordinator.FatalFailure);
    }

    [TestMethod]
    public async Task MCPAC025_UsageOverflowStillReleasesLeaseBeforeFatalShutdown()
    {
        await using var f = new McpFixture(); await f.Initialize();
        typeof(McpCoordinator).GetField("_receivedBytes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(f.Mcp, long.MaxValue);
        await Assert.ThrowsExactlyAsync<OverflowException>(async () => await f.Mcp.ConnectAsync(default));
        var acquired = f.Execution.WorkspaceOperations.TryAcquire(default, out var lease);
        ReleaseAfterRegressionFailure(f.Execution.WorkspaceOperations, lease);
        Assert.IsTrue(acquired); Assert.IsTrue(f.Mcp.FatalFailure);
    }

    [TestMethod]
    public async Task MCPAC017_StaleSaveIsControlledAndLeavesRepairCommandsAvailable()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default); f.Mcp.MarkStale();
        var work = new WorkSessionCoordinator(f.Contexts, f.Plans, f.Execution.Rules!, f.Files, new MemoryWorkStore(), f.Session.Policy, null, null, new());
        var result = await work.SaveAsync("public task", (_, _) => new(WorkApprovalDecision.Approved), default);
        Assert.AreEqual("mcp_catalog_stale", result.ErrorCode); Assert.IsFalse(result.Fatal);
        Assert.AreEqual("none", (await f.Mcp.DisconnectAsync(default)).ErrorCode);
    }

    private static void ReleaseAfterRegressionFailure(Workspace.WorkspaceOperationCoordinator operations, Workspace.WorkspaceOperationLease? acquired)
    {
        // Ensure a deliberately regressed implementation cannot hang the test fixture's disposal.
        if (acquired is not null) acquired.Dispose();
        else ((IDisposable?)typeof(Workspace.WorkspaceOperationCoordinator).GetField("_active", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(operations))?.Dispose();
    }

    private sealed class FailingCloseClient : IMcpReadClient
    {
        public Task DiscoverAsync(CancellationToken token) => Task.FromException(new McpFailure("mcp_catalog_invalid"));
        public Task<McpCatalogObservation> ListToolsAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<McpCallOutcome> CallAsync(McpCallPermit permit, CancellationToken token) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.FromException(new IOException("SYNTHETIC_CLOSE_FAILURE"));
    }

    [TestMethod]
    public async Task MCPAC027_CatalogChecksShareThirtySecondActiveBudgetAcrossApproval()
    {
        var clock = new McpClock(); await using var f = new McpFixture(clock); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var checks = 0;
        f.Observe = method => { if (method == "tools/list") clock.Advance(++checks == 1 ? 20 : 11); };
        var result = await f.Invoke();
        Assert.AreEqual("mcp_timed_out", result.ErrorCode);
        Assert.AreEqual(0, f.Handlers.Sum(h => h.Methods.Count(m => m == "tools/call")));
    }

    [TestMethod]
    public async Task MCPAC027_HiddenHumanWaitingDoesNotConsumeActiveNetworkTime()
    {
        var clock = new McpClock(); await using var f = new McpFixture(clock); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        f.OnConfirm = () => { clock.Advance(3600); return McpApprovalDecision.Approved; };
        Assert.AreEqual("none", (await f.Invoke()).ErrorCode);
        Assert.AreEqual(1, f.Handlers.Sum(h => h.Methods.Count(m => m == "tools/call")));
    }

    [TestMethod]
    public async Task MCPAC025_ResetCancelsApprovalWaitBeforeReleasingClientAndRejectsLateYes()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<McpApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AsyncConfirm = (_, token) => { entered.TrySetResult(); return new(released.Task.WaitAsync(token)); };
        var call = f.Invoke(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Mcp.ResetAsync(); released.TrySetResult(McpApprovalDecision.Approved);
        var result = await call;
        Assert.AreEqual(Tools.ToolStatus.Cancelled, result.Status);
        Assert.AreEqual(McpStatus.Disconnected, f.Mcp.Capture().Status); Assert.IsTrue(f.Handlers.All(h => h.Closed));
        Assert.AreEqual(0, f.Handlers.Sum(h => h.Methods.Count(m => m == "tools/call")));
    }

    [TestMethod]
    public async Task MCPAC025_CancellationCallbackFailureStillDrainsAndClosesClient()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource<McpApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AsyncConfirm = (_, token) =>
        {
            token.Register(() => throw new IOException("SYNTHETIC_CANCEL_FAILURE"));
            entered.TrySetResult(); return new(never.Task.WaitAsync(token));
        };
        var call = f.Invoke(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await f.Mcp.ResetAsync());
        await call.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(f.Mcp.FatalFailure); Assert.IsTrue(f.Handlers.All(h => h.Closed));
        Assert.AreEqual(McpStatus.Disconnected, f.Mcp.Capture().Status);
    }

    [TestMethod]
    public async Task MCPAC011_ConcurrentEntryImmediatelyRejectsAndCannotShareApproval()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource<McpApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.AsyncConfirm = (_, _) => { entered.TrySetResult(); return new(released.Task); };
        var first = f.Invoke(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("mcp_busy", (await f.Invoke()).ErrorCode); Assert.AreEqual("mcp_busy", (await f.Mcp.DisconnectAsync(default)).ErrorCode);
        released.TrySetResult(McpApprovalDecision.Approved); Assert.AreEqual("none", (await first).ErrorCode);
    }
}

internal sealed class McpClock : TimeProvider
{
    private long _ticks;
    private readonly List<ManualTimer> _timers = [];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    { var timer = new ManualTimer(this, callback, state); _timers.Add(timer); timer.Change(dueTime, period); return timer; }
    internal void Advance(int seconds)
    {
        _ticks += TimeSpan.FromSeconds(seconds).Ticks;
        foreach (var timer in _timers.ToArray()) timer.Fire();
    }
    private sealed class ManualTimer(McpClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long _due = long.MaxValue;
        private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period) { _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks; return !_disposed; }
        internal void Fire() { if (!_disposed && _due <= clock._ticks) { _due = long.MaxValue; callback(state); } }
        public void Dispose() => _disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}