using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationCoordinationTests
{
    [TestMethod]
    public async Task MULTIAC027_FirstCompletedChildDoesNotStartCleanupAndDeadlineCanPublishPartial()
    {
        await using var f = await DelegationTestFixture.Create(); var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Factory.Respond = async (child, request, token) =>
        {
            if (child == 1) { waiting.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            return ChatFixture.Completed(DelegationTestFixture.EmptyReport);
        };
        var run = f.Run(2); await waiting.Task.WaitAsync(TimeSpan.FromSeconds(3));
        f.Clock.Advance(20); Assert.IsFalse(run.IsCompleted);
        f.Clock.Advance(100); var result = await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual("partial", result.Data!.Value.GetProperty("state").GetString());
        Assert.AreEqual("timed_out", result.Data.Value.GetProperty("children")[1].GetProperty("state").GetString());
        Assert.IsTrue(f.Factory.Clients.All(c => c.DisposedWithLiveSession));
    }
    [TestMethod]
    public async Task MULTIAC029_NeverReturningChildRequiresFatalStopWithinSingleCleanupWindow()
    {
        await using var f = await DelegationTestFixture.Create(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource<LlmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Factory.Respond = async (_, _, _) => { entered.TrySetResult(); return await release.Task; };
        var run = f.Run(1); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); f.Clock.Advance(120);
        await f.Clock.CleanupCreated.Task.WaitAsync(TimeSpan.FromSeconds(3)); f.Clock.Advance(10);
        var result = await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue(result.Fatal); Assert.AreEqual("delegation_cleanup_failed", result.ErrorCode); Assert.IsNull(result.Data);
        Assert.IsFalse(f.Factory.Clients[0].DisposedWithLiveSession); release.TrySetResult(ChatFixture.Completed(DelegationTestFixture.EmptyReport));
    }
    [TestMethod]
    public async Task MULTIAC014_SecondFactoryFailureStartsNoChildAndDisposesFirstOnlyOnce()
    {
        await using var f = await DelegationTestFixture.Create(); f.Factory.FailAt = 1;
        var result = await f.Run(2);
        Assert.AreEqual("failed", result.Data!.Value.GetProperty("state").GetString());
        Assert.AreEqual(1, f.Factory.Clients.Count); Assert.AreEqual(0, f.Factory.Clients[0].Requests.Count);
        Assert.IsTrue(f.Factory.Clients[0].DisposedWithLiveSession); Assert.AreEqual(1, f.Factory.Disposals);
    }
    [TestMethod]
    public async Task MULTIAC028_CancellationDiscardsAlreadyCompletedSiblingAndJoinsBeforeReturning()
    {
        await using var f = await DelegationTestFixture.Create(); var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        f.Factory.Respond = async (child, _, token) => { if (child == 1) { waiting.TrySetResult(); await Task.Delay(Timeout.Infinite, token); } return ChatFixture.Completed(DelegationTestFixture.EmptyReport); };
        var run = f.Run(2, cancel.Token); await waiting.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancel.Cancel();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(3)); Assert.AreEqual(ToolStatus.Cancelled, result.Status); Assert.IsNull(result.Data);
        Assert.IsTrue(f.Factory.Clients.All(c => c.DisposedWithLiveSession));
    }
    [TestMethod]
    public async Task MULTIAC011_ZeroGitChangesDoNotCreateChildClientsOrClaimModelReview()
    {
        await using var f = await DelegationTestFixture.Create(); f.Git.ChangeCount = 0;
        var result = await f.Run(1, role: DelegationRole.Review);
        Assert.AreEqual(0, f.Factory.Clients.Count); Assert.AreEqual("completed", result.Data!.Value.GetProperty("state").GetString());
        var child = result.Data.Value.GetProperty("children")[0]; Assert.AreEqual(JsonValueKind.Null, child.GetProperty("report").ValueKind);
    }
    [TestMethod]
    public async Task MULTIAC016_HandoffContainsCurrentUserAndAllPinsButNoOldSummary()
    {
        await using var f = await DelegationTestFixture.Create(); Assert.AreEqual("none", f.Contexts.AddPin("ORIGINAL_PIN"));
        f.Factory.Respond = (_, request, _) =>
        { Assert.IsTrue(request.SystemInstruction.Contains("ORIGINAL_PIN", StringComparison.Ordinal)); Assert.IsTrue(request.Messages[0].UserText!.Contains("ORIGINAL_USER", StringComparison.Ordinal)); return Task.FromResult(ChatFixture.Completed(DelegationTestFixture.EmptyReport)); };
        Assert.AreEqual("completed", (await f.Run(1)).Data!.Value.GetProperty("state").GetString());
    }
}
internal sealed class DelegationTestFixture : IAsyncDisposable
{
    internal const string EmptyReport = """{"version":1,"summary":"No source inspected.","observations":[],"limitations":["No tests run."]}""";
    internal BootstrapSession Session = WorkspaceFixtures.Session();
    internal WorkspaceFiles Files = null!;
    internal ContextCoordinator Contexts = null!;
    internal DelegationCoordinator Coordinator = null!;
    internal DelegationTestFactory Factory = null!;
    internal DelegationTestClock Clock = new();
    internal ReviewGitWorkspace Git = new();
    internal GenerationBudget Budget = null!;
    internal ControlledWorkspaceFileSystem FileSystem = new();
    internal ConversationContext RawContext = null!;
    internal AgentExecutionPolicy Execution = null!;
    internal Action<DelegationEvent> OnEvent = _ => { };
    internal static async Task<DelegationTestFixture> Create()
    {
        var f = new DelegationTestFixture(); var context = f.RawContext = new ConversationContext(); var plans = new PlanStore(f.Session.Policy, context.DecisionGate); var execution = f.Execution = new AgentExecutionPolicy(plans);
        f.Files = new(f.Session.Workspace, f.Session.Policy, f.FileSystem, executionPolicy: execution);
        await execution.Rules!.EnsureInitializedAsync(default); f.Contexts = new(context, plans, f.Session.Policy); f.Contexts.BindRules(execution.Rules); f.Contexts.BindSkills(execution.Skills);
        f.Budget = new(context.DecisionGate); f.Budget.SetTurnLimit(6); f.Factory = new(f.Session);
        f.Coordinator = new(f.Contexts, execution, f.Budget, f.Files, f.Session.Policy, f.Factory, () => new DelegationTestGitSource(f.Git), new(), value => f.OnEvent(value), f.Clock);
        execution.Delegation = f.Coordinator; f.Coordinator.SetEnabled(true); return f;
    }
    internal Task<DelegationBatchResult> Run(int count, CancellationToken token = default, DelegationRole role = DelegationRole.Explore)
    {
        Coordinator.BeginParentTurn("ORIGINAL_USER"); Budget.BeginConversation();
        Budget.TryBeginGeneration(GenerationPurpose.Conversation, false, default, () => true);
        Coordinator.BindGateway(new(Contexts.DecisionGate, new(), new(), ModelCatalog.Profiles[0]));
        return Coordinator.RunBatchAsync(new(Coordinator.ParentTurnId, Enumerable.Range(0, count).Select(_ => new DelegationTask(role, "inspect", ".", "cite"))), token);
    }
    public async ValueTask DisposeAsync() { Coordinator.EndParentTurn(); await Files.DisposeAsync(); Session.Dispose(); }
}
internal sealed class DelegationTestFactory(BootstrapSession session) : IDelegationRuntimeFactory
{
    internal List<ScriptClient> Clients = [];
    internal int FailAt = -1;
    internal int Disposals;
    internal Action<DelegationChildSpec>? ObserveSpec;
    internal Func<int, ScriptClient, ILlmClient>? Decorate;
    internal Func<int, LlmRequest, CancellationToken, Task<LlmResult>> Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(DelegationTestFixture.EmptyReport));
    public Task<DelegationRuntime> CreateAsync(DelegationChildSpec spec, CancellationToken token)
    {
        ObserveSpec?.Invoke(spec);
        var index = Clients.Count; if (index == FailAt) throw new IOException("synthetic failure");
        var client = new ScriptClient { Session = session, Inspect = _ => new(ContextMeasureStatus.Measured, 1024), Respond = (_, request, cancellation) => Respond(index, request, cancellation), OnDispose = () => Disposals++ };
        Clients.Add(client); return Task.FromResult(new DelegationRuntime(Decorate?.Invoke(index, client) ?? client, client));
    }
}

internal sealed class DelegationTestClock : TimeProvider
{
    private readonly object _gate = new();
    private long _ticks;
    private readonly List<ManualTimer> _timers = [];
    internal TaskCompletionSource CleanupCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (_gate) return _ticks; }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            var timer = new ManualTimer(this, callback, state); _timers.Add(timer); timer.Change(dueTime, period);
            if (dueTime == TimeSpan.FromSeconds(10)) CleanupCreated.TrySetResult(); return timer;
        }
    }
    internal void Advance(int seconds)
    {
        ManualTimer[] timers; lock (_gate) { _ticks += TimeSpan.FromSeconds(seconds).Ticks; timers = _timers.ToArray(); }
        foreach (var timer in timers) timer.Fire();
    }
    private sealed class ManualTimer(DelegationTestClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long _due = long.MaxValue; private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period) { lock (clock._gate) { _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks; return !_disposed; } }
        internal void Fire() { lock (clock._gate) { if (_disposed || _due > clock._ticks) return; _due = long.MaxValue; } callback(state); }
        public void Dispose() { lock (clock._gate) _disposed = true; }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
internal sealed class DelegationTestGitSource(ReviewGitWorkspace inner) : IGitReadWorkspace
{
    private GitSnapshot? _captured;
    public async Task<GitCaptureResult> CaptureAsync(CancellationToken token)
    { var result = await inner.CaptureAsync(token); _captured = result.Snapshot; return result; }
    public Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken token) => inner.ValidateAsync(snapshot, token);
    public Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken token) => inner.ValidateAsync(_captured!, token);
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}