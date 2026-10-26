using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class CheckpointSdkCapacityTests
{
    [TestMethod]
    public async Task CKPTAC024_RecoveryAcceptsExactSdkBodyAndRefusesNextByteBeforeGeneration()
    {
        var calibration = await RunRecoveryAsync(1);
        var padding = checked(262144 - calibration.BodyBytes + 1);
        var accepted = await RunRecoveryAsync(padding);
        Assert.AreEqual(262144, accepted.BodyBytes); Assert.AreEqual(1, accepted.HttpCalls);
        Assert.AreEqual(AgentTurnStatus.Completed, accepted.Result.Status); Assert.AreEqual(1, accepted.Result.GenerationsStarted);
        Assert.AreEqual(1L, accepted.Result.TurnUsage.DispatchedRequests); Assert.AreEqual(1, accepted.ProgramGenerations);
        var rejected = await RunRecoveryAsync(padding + 1);
        Assert.AreEqual(AgentStopReason.ContextCapacity, rejected.Result.StopReason);
        Assert.AreEqual(0, rejected.HttpCalls); Assert.AreEqual(0, rejected.Result.GenerationsStarted);
        Assert.AreEqual(0L, rejected.Result.TurnUsage.DispatchedRequests); Assert.AreEqual(0, rejected.ProgramGenerations);
        Assert.IsFalse(rejected.Result.RequiresContextReset);
    }

    private static async Task<(int BodyBytes, int HttpCalls, AgentTurnResult Result, int ProgramGenerations)> RunRecoveryAsync(int padding)
    {
        using var session = WorkspaceFixtures.Session(); var plans = new PlanStore(session.Policy);
        var execution = new AgentExecutionPolicy(plans);
        var registry = TestRulesRegistry.Create([], session.Policy, execution);
        plans.Recovery.Record(1, "a", WorkspaceWriteResult.Success(new("a", 1, false)), true);
        var handler = new GeminiHandler(); await using var client = new GeminiClient(session, handler);
        var budget = new GenerationBudget(); var inspector = client.CreateContextInspector();
        var runner = new AgentRunner(client, registry, session.Policy, new string('x', padding), new(), _ => { }, budget, contextInspector: inspector);
        var result = await runner.RunAsync([LlmMessage.FromUserText("question")], default);
        Assert.IsNotNull(plans.Recovery.Last);
        return (handler.Bodies.LastOrDefault()?.Length ?? 0, handler.Calls, result, budget.Snapshot().ProgramGenerationsStarted);
    }

    [TestMethod]
    public async Task CKPTAC024_ActualMeasurementHasNoHttpUsageOrBodyTelemetryAndPreservesClient()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler); var inspector = client.CreateContextInspector();
        Assert.IsInstanceOfType<ILlmRequestCapacityInspector>(inspector);
        var precise = (ILlmRequestCapacityInspector)inspector; var reports = 0;
        using var telemetry = ((ILlmContextTelemetry)inspector).ObserveRequest((_, _) => reports++);
        var request = GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]);
        var measured = await precise.MeasureActualRequestAsync(request, default);
        Assert.AreEqual(ContextMeasureStatus.Measured, measured.Status); Assert.IsTrue(measured.EstimatedJsonBytes > 0);
        Assert.AreEqual(0, handler.Calls); Assert.AreEqual(0, reports); Assert.IsFalse(measured.RequiresContextReset);
        var actual = await client.GenerateAsync(request, default);
        Assert.AreEqual(LlmStatus.Completed, actual.Status); Assert.IsTrue(actual.RequestDispatched);
        Assert.AreEqual(1, handler.Calls); Assert.AreEqual(1, reports);
        Assert.AreEqual(measured.EstimatedJsonBytes, handler.Bodies.Single().LongLength);
    }

    [TestMethod]
    public async Task CKPTAC024_PreCancelledActualMeasurementDoesNotOccupyClient()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler); var precise = (ILlmRequestCapacityInspector)client.CreateContextInspector();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => precise.MeasureActualRequestAsync(GeminiFixtures.Request(), cancellation.Token));
        Assert.AreEqual(0, handler.Calls);
        Assert.AreEqual(LlmStatus.Completed, (await client.GenerateAsync(GeminiFixtures.Request(), default)).Status);
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    public async Task CKPTAC024_ConcurrentGenerationPreventsMeasureAndItsSlotCanBeReused()
    {
        using var session = GeminiFixtures.Session(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new GeminiHandler
        {
            Respond = async (_, token) =>
        { entered.TrySetResult(); await release.Task.WaitAsync(token); return GeminiHandler.Json(GeminiFixtures.Text()); }
        };
        await using var client = new GeminiClient(session, handler); var precise = (ILlmRequestCapacityInspector)client.CreateContextInspector();
        var pending = client.GenerateAsync(GeminiFixtures.Request(), default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var measured = await precise.MeasureActualRequestAsync(GeminiFixtures.Request(), default);
            Assert.AreEqual(ContextMeasureStatus.Unavailable, measured.Status); Assert.AreEqual(1, handler.Calls);
        }
        finally { release.TrySetResult(); }
        Assert.AreEqual(LlmStatus.Completed, (await pending).Status);
        Assert.AreEqual(ContextMeasureStatus.Measured, (await precise.MeasureActualRequestAsync(GeminiFixtures.Request(), default)).Status);
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    public async Task CKPTAC024_LifetimeWaitsForOwnedPreflightWithoutAllowingConcurrentGeneration()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler(); using var clock = new CapacityClock();
        await using var client = new GeminiClient(session, handler, clock); var precise = (ILlmRequestCapacityInspector)client.CreateContextInspector();
        var pending = Task.Run(() => precise.MeasureActualRequestAsync(GeminiFixtures.Request(), default));
        try
        {
            await clock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var concurrent = await client.GenerateAsync(GeminiFixtures.Request(), default);
            Assert.AreEqual(LlmErrorCode.InvalidRequest, concurrent.ErrorCode); Assert.AreEqual(0, handler.Calls);
            var disposal = client.DisposeAsync().AsTask(); Assert.IsFalse(disposal.IsCompleted);
            clock.Release.Set();
            Assert.AreEqual(ContextMeasureStatus.Unavailable, (await pending).Status);
            await disposal.WaitAsync(TimeSpan.FromSeconds(5)); Assert.IsTrue(handler.Disposed);
            Assert.AreEqual(ContextMeasureStatus.Unavailable, (await precise.MeasureActualRequestAsync(GeminiFixtures.Request(), default)).Status);
        }
        finally { clock.Release.Set(); }
        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    public async Task CKPTAC024_CancellationDuringOwnedPreflightReleasesSlotWithoutDispatch()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler(); using var clock = new CapacityClock();
        await using var client = new GeminiClient(session, handler, clock); var precise = (ILlmRequestCapacityInspector)client.CreateContextInspector();
        using var cancellation = new CancellationTokenSource();
        var pending = Task.Run(() => precise.MeasureActualRequestAsync(GeminiFixtures.Request(), cancellation.Token));
        try
        {
            await clock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel(); clock.Release.Set();
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        }
        finally { clock.Release.Set(); }
        Assert.AreEqual(0, handler.Calls);
        Assert.AreEqual(ContextMeasureStatus.Measured, (await precise.MeasureActualRequestAsync(GeminiFixtures.Request(), default)).Status);
        Assert.AreEqual(LlmStatus.Completed, (await client.GenerateAsync(GeminiFixtures.Request(), default)).Status);
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    public async Task CKPTAC024_FailingMeasurementSetupReleasesOperationSlot()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler(); var clock = new FailingCapacityClock();
        await using var client = new GeminiClient(session, handler, clock); var precise = (ILlmRequestCapacityInspector)client.CreateContextInspector();
        Assert.AreEqual(ContextMeasureStatus.Unavailable, (await precise.MeasureActualRequestAsync(GeminiFixtures.Request(), default)).Status);
        Assert.AreEqual(0, handler.Calls);
        Assert.AreEqual(LlmStatus.Completed, (await client.GenerateAsync(GeminiFixtures.Request(), default)).Status);
        Assert.AreEqual(1, handler.Calls);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task CKPTAC024_ActualMeasurementRejectsSensitiveAndForeignProtocolBeforeDispatch(bool foreign)
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler); var precise = (ILlmRequestCapacityInspector)client.CreateContextInspector();
        var request = foreign ? new LlmRequest("safe", [LlmMessage.FromUserText("question"), LlmMessage.FromModel(new object())], []) :
            new LlmRequest("safe " + GeminiFixtures.Key, [LlmMessage.FromUserText("question")], []);
        var measured = await precise.MeasureActualRequestAsync(request, default);
        Assert.AreEqual(ContextMeasureStatus.Unavailable, measured.Status); Assert.IsTrue(measured.RequiresContextReset);
        Assert.AreEqual(0, handler.Calls);
    }

    private sealed class CapacityClock : TimeProvider, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("容量測試等待逾時");
            return new CapacityTimer();
        }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }

    private sealed class FailingCapacityClock : TimeProvider
    {
        private int _calls;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Interlocked.Increment(ref _calls) == 1 ? throw new InvalidOperationException("synthetic timer failure") : new CapacityTimer();
    }

    private sealed class CapacityTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

}