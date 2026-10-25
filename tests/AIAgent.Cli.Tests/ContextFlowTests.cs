using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ContextFlowTests
{
    [DataRow(1)]
    [DataRow(5)]
    [DataRow(20)]
    [TestMethod]
    public async Task CTXAC027_ConfiguredLastGenerationAlwaysStopsWholeToolBatch(int limit)
    {
        await using var f = new ContextFlowFixture(0); f.Budget.SetTurnLimit(limit);
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0)));
        var result = await f.Run();
        Assert.AreEqual(limit, result.GenerationsStarted); Assert.AreEqual(limit, f.Client.Requests.Count);
        Assert.AreEqual(AgentStopReason.GenerationLimit, result.StopReason); Assert.AreEqual(limit, result.Operations.Count);
        Assert.AreEqual(ToolStatus.NotExecuted, result.Operations[^1].Status); Assert.AreEqual(limit - 1, f.FileSystem.Opened);
    }

    [TestMethod]
    public async Task CTXAC016_DefaultFiveIncludesSummaryAndOnlyFourConversationGenerations()
    {
        await using var f = new ContextFlowFixture(5);
        f.Client.Respond = (_, request, _) => Task.FromResult(request.Tools.Count == 0 ? ChatFixture.Completed(ContextTestData.Summary) : RunnerFixture.Calls(RunnerFixture.Read(0)));
        var result = await f.Run();
        Assert.AreEqual(5, result.GenerationsStarted); Assert.AreEqual(4, result.Operations.Count);
        Assert.AreEqual(3, f.FileSystem.Opened); Assert.AreEqual(ToolStatus.NotExecuted, result.Operations[^1].Status);
        Assert.AreEqual(1, f.Budget.Snapshot().CompactionGenerationsStarted);
    }

    [TestMethod]
    public async Task CTXAC013_OversizedSafeSourceNeverStartsSummaryOrEvicts()
    {
        await using var f = new ContextFlowFixture(0);
        for (var i = 0; i < 5; i++) f.Context.CommitCompletedTurn([LlmMessage.FromUserText(new string('中', 8000)), LlmMessage.FromModel(new object())]);
        var result = await f.Run();
        Assert.AreEqual(AgentStopReason.ContextCapacity, result.StopReason); Assert.AreEqual(0, result.GenerationsStarted);
        Assert.AreEqual(0, f.Client.Requests.Count); Assert.AreEqual(5, f.Context.CompletedTurnCount); Assert.IsNull(f.Context.Summary);
    }

    [TestMethod]
    public async Task CTXAC028_UndispatchedSummaryCountsStartButNotMissingUsage()
    {
        await using var f = new ContextFlowFixture(5);
        f.Client.Respond = (_, request, _) => Task.FromResult(request.Tools.Count == 0
            ? new LlmResult(LlmStatus.Completed, LlmErrorCode.None, ContextTestData.Summary, [], LlmMessage.FromModel(new object()), new(999, 999, 999, 999, 999), false, false)
            : ChatFixture.Completed());
        var result = await f.Run(); Assert.AreEqual(2, result.GenerationsStarted);
        Assert.AreEqual(1L, result.TurnUsage.DispatchedRequests); Assert.AreEqual(5L, result.TurnUsage.Total.KnownTotal);
        Assert.AreEqual(0L, result.TurnUsage.Total.MissingRequestCount);
    }

    [DataRow(2, 196608, false)]
    [DataRow(3, 196607, false)]
    [DataRow(3, 196608, true)]
    [DataRow(4, 196609, true)]
    [DataRow(5, 1024, true)]
    [TestMethod]
    public async Task CTXAC015_AutoTriggerOnlyBeforeFirstGeneration(int turns, int bytes, bool compact)
    {
        await using var f = new ContextFlowFixture(turns);
        f.Client.Inspect = _ => new(ContextMeasureStatus.Measured, bytes);
        var result = await f.Run();
        Assert.AreEqual(AgentTurnStatus.Completed, result.Status);
        Assert.AreEqual(compact ? 2 : 1, result.GenerationsStarted);
        Assert.AreEqual(compact ? 2 : turns, f.Context.CompletedTurnCount);
        if (compact)
        {
            Assert.AreEqual(0, f.Client.Requests[0].Tools.Count);
            Assert.AreEqual(1, f.Client.Requests[0].Messages.Count);
            Assert.AreEqual("current", f.Client.Requests[1].Messages[^1].UserText);
            Assert.AreEqual(turns - 2L, f.Context.Summary!.CoveredTurnCount);
            Assert.AreEqual(2, f.Client.Requests.Count); // Remaining high estimate must not cause a second compaction.
        }
    }

    [DataRow(1, 100, (int)AgentStopReason.GenerationLimit)]
    [DataRow(5, 1, (int)AgentStopReason.ProgramGenerationLimit)]
    [TestMethod]
    public async Task CTXAC016_AutoNeedsTwoOpportunitiesBeforeStarting(int turnLimit, int programLimit, int expected)
    {
        await using var f = new ContextFlowFixture(5); f.Budget.SetTurnLimit(turnLimit); f.Budget.SetProgramLimit(programLimit);
        var result = await f.Run();
        Assert.AreEqual((AgentStopReason)expected, result.StopReason); Assert.AreEqual(0, result.GenerationsStarted);
        Assert.AreEqual(0, f.Client.Requests.Count); Assert.AreEqual(5, f.Context.CompletedTurnCount);
    }

    [TestMethod]
    public async Task CTXAC016_AutoSharesOneTurnUsageAndLastTextCanComplete()
    {
        await using var f = new ContextFlowFixture(5); f.Budget.SetTurnLimit(2); f.Budget.SetProgramLimit(2);
        var result = await f.Run();
        Assert.AreEqual(AgentTurnStatus.Completed, result.Status); Assert.AreEqual(2, result.GenerationsStarted);
        Assert.AreEqual(2L, result.TurnUsage.DispatchedRequests); Assert.AreEqual(10L, result.TurnUsage.Total.KnownTotal);
        Assert.AreEqual(2, f.Budget.Snapshot().ProgramGenerationsStarted); Assert.AreEqual(1, f.Budget.Snapshot().CompactionGenerationsStarted);
    }

    [DataRow("invalid")]
    [DataRow("refused")]
    [DataRow("truncated")]
    [DataRow("timeout")]
    [DataRow("cancel")]
    [TestMethod]
    public async Task CTXAC022_SummaryFailureNeverFallsBackOrCommits(string failure)
    {
        await using var f = new ContextFlowFixture(5);
        using var cancellation = new CancellationTokenSource();
        f.Client.Respond = (_, _, _) =>
        {
            if (failure == "cancel") cancellation.Cancel();
            return Task.FromResult(failure switch
            {
                "invalid" => ChatFixture.Completed("{}"),
                "refused" => new(LlmStatus.Refused, LlmErrorCode.None, "refused", [], null, null, true, false),
                "truncated" => new(LlmStatus.Truncated, LlmErrorCode.None, "partial", [], null, null, true, false),
                "timeout" => new(LlmStatus.Failed, LlmErrorCode.Timeout, "timeout", [], null, null, true, false),
                _ => ChatFixture.Completed(ContextTestData.Summary)
            });
        };
        var result = await f.Run(cancellation.Token);
        Assert.AreEqual(1, f.Client.Requests.Count); Assert.AreEqual(1, result.GenerationsStarted);
        Assert.AreEqual(5, f.Context.CompletedTurnCount); Assert.IsNull(f.Context.Summary);
        Assert.IsFalse(result.RequiresContextReset); Assert.AreEqual(0, result.CompletedTurn.Count);
        Assert.AreEqual(failure == "cancel" ? AgentStopReason.TurnCancelled : AgentStopReason.ContextCompactionFailed, result.StopReason);
    }

    [TestMethod]
    public async Task CTXAC021_ChangedPlanSnapshotDropsCandidateWithoutRebinding()
    {
        await using var f = new ContextFlowFixture(5);
        f.Client.Respond = (_, _, _) => { f.Plans.SetMode(AgentMode.Plan); return Task.FromResult(ChatFixture.Completed(ContextTestData.Summary)); };
        var result = await f.Run();
        Assert.AreEqual(AgentStopReason.ContextCompactionFailed, result.StopReason);
        Assert.AreEqual(1, result.GenerationsStarted); Assert.IsNull(f.Context.Summary);
        Assert.AreEqual(5, f.Context.CompletedTurnCount); Assert.AreEqual(AgentMode.Plan, f.Plans.Mode);
    }

    [DataRow(1, 100, (int)AgentStopReason.GenerationLimit, "generation_limit")]
    [DataRow(20, 1, (int)AgentStopReason.ProgramGenerationLimit, "program_generation_limit")]
    [DataRow(1, 1, (int)AgentStopReason.GenerationLimit, "generation_limit")]
    [TestMethod]
    public async Task CTXAC027_LastOpportunityCannotStartAnyTool(int turnLimit, int programLimit, int expected, string code)
    {
        await using var f = new ContextFlowFixture(0); f.Budget.SetTurnLimit(turnLimit); f.Budget.SetProgramLimit(programLimit);
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1)));
        var result = await f.Run();
        Assert.AreEqual((AgentStopReason)expected, result.StopReason); Assert.AreEqual(2, result.Operations.Count);
        Assert.IsTrue(result.Operations.All(o => o.Status == ToolStatus.NotExecuted && o.ErrorCode == code));
        Assert.AreEqual(0, f.FileSystem.Opened); Assert.AreEqual(1, result.GenerationsStarted);
    }

    [TestMethod]
    public async Task CTXAC028_ThrowCountsStartWithoutInventingUsage()
    {
        await using var f = new ContextFlowFixture(0);
        f.Client.Respond = (_, _, _) => throw new IOException("synthetic");
        var result = await f.Run();
        Assert.AreEqual(1, result.GenerationsStarted); Assert.AreEqual(1, f.Budget.Snapshot().ProgramGenerationsStarted);
        Assert.AreEqual(0L, result.TurnUsage.DispatchedRequests); Assert.AreEqual(0L, result.TurnUsage.Total.MissingRequestCount);
    }

    [TestMethod]
    public async Task CTXAC019_UnavailableInspectorNeverStartsGeneration()
    {
        await using var f = new ContextFlowFixture(0);
        f.Client.Inspect = _ => new(ContextMeasureStatus.Unavailable);
        var result = await f.Run();
        Assert.AreEqual(AgentStopReason.ContextCapacity, result.StopReason); Assert.AreEqual(0, result.GenerationsStarted);
        Assert.AreEqual(0, f.Client.Requests.Count); Assert.IsFalse(result.RequiresContextReset);
    }

    [TestMethod]
    public async Task CTXAC023_SensitiveSummaryRequiresCommonReset()
    {
        await using var f = new ContextFlowFixture(5);
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(GeminiFixtures.Key));
        var result = await f.Run(); Assert.IsTrue(result.RequiresContextReset);
        Assert.AreEqual(1, result.GenerationsStarted); Assert.AreEqual(0, result.CompletedTurn.Count);
    }

    [TestMethod]
    public async Task CTXAC004_EscapedSensitiveSummaryIsNotAnOrdinaryFormatFailure()
    {
        await using var f = new ContextFlowFixture(5);
        var escaped = string.Concat(GeminiFixtures.Key.Select(c => "\\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(ContextTestData.Summary.Replace("continue", escaped, StringComparison.Ordinal)));
        var result = await f.Run(); Assert.IsTrue(result.RequiresContextReset);
        Assert.AreEqual(1, result.GenerationsStarted);
    }

    [DataRow("extra")]
    [DataRow("duplicate")]
    [DataRow("later")]
    [TestMethod]
    public async Task CTXAC023_WholeDecodedSummaryIsCheckedBeforeSchemaFailure(string position)
    {
        await using var f = new ContextFlowFixture(5);
        var escaped = string.Concat(GeminiFixtures.Key.Select(c => "\\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
        var text = position switch
        {
            "extra" => ContextTestData.Summary[..^1] + ",\"extra\":\"" + escaped + "\"}",
            "duplicate" => ContextTestData.Summary[..^1] + ",\"goals\":[\"" + escaped + "\"]}",
            _ => "{\"version\":1,\"goals\":null,\"decisions\":[\"" + escaped + "\"],\"open_questions\":[],\"background\":[]}"
        };
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(text));
        var result = await f.Run(); Assert.IsTrue(result.RequiresContextReset);
        Assert.AreEqual(1, result.GenerationsStarted);
    }

    [TestMethod]
    public async Task CTXAC028_TelemetryPrecheckFailureDoesNotChargeModelStart()
    {
        await using var f = new ContextFlowFixture(0);
        var registry = TestRulesRegistry.Create([], f.Session.Policy, new(f.Plans));
        var runner = new AgentRunner(f.Client, registry, f.Session.Policy, "test", new(), _ => { }, f.Budget, f.Coordinator, new ThrowingTelemetry());
        var result = await runner.RunAsync(f.Context.BuildMessages(LlmMessage.FromUserText("question")), default);
        Assert.AreEqual(0, result.GenerationsStarted); Assert.AreEqual(0, f.Budget.Snapshot().ProgramGenerationsStarted);
        Assert.AreEqual(0, f.Client.Requests.Count);
    }
    private sealed class ThrowingTelemetry : ILlmContextInspector, ILlmContextTelemetry
    {
        public ContextRequestMeasure Measure(LlmRequest request) => new(ContextMeasureStatus.Measured, 1);
        public ContextTurnProjection ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages) => throw new NotSupportedException();
        public IDisposable ObserveRequest(Action<long, bool> report) => throw new ContextInspectionException();
    }

    [TestMethod]
    public async Task CTXAC019_DiagnosticsDoNotOwnSummarizedNativeMessages()
    {
        await using var f = new ContextFlowFixture(5);
        var weak = OriginalProtocol(f);
        var result = await f.Runner.RunMaintenanceAsync(default); Assert.AreEqual(AgentTurnStatus.Completed, result.Status);
        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
        Assert.IsFalse(weak.IsAlive, "Persisted capacity diagnostics must not retain the compacted native prefix.");
        Assert.IsNotNull(f.Coordinator.LastMeasurement);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference OriginalProtocol(ContextFlowFixture f) => new(f.Context.Capture().CompletedTurns[0].Messages[1].Protocol!);

    [TestMethod]
    public async Task CTXAC021_StampChangedDuringSummaryMeasurePreventsModelStart()
    {
        await using var f = new ContextFlowFixture(5);
        f.Client.Inspect = request =>
        {
            if (request.Tools.Count == 0) f.Coordinator.AddPin("changed");
            return new(ContextMeasureStatus.Measured, 1024);
        };
        var result = await f.Run();
        Assert.AreEqual(0, result.GenerationsStarted); Assert.AreEqual(0, f.Client.Requests.Count);
        Assert.IsNull(f.Context.Summary); Assert.AreEqual(5, f.Context.CompletedTurnCount);
    }

    [TestMethod]
    public async Task CTXAC027_BudgetChangedAtToolStartCannotDispatch()
    {
        await using var f = new ContextFlowFixture(0);
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0)));
        f.OnEvent = e => { if (e.Kind == AgentEventKind.ExecutingTool) f.Budget.SetProgramLimit(1); };
        var result = await f.Run();
        Assert.AreEqual(AgentStopReason.ProgramGenerationLimit, result.StopReason);
        Assert.AreEqual(0, f.FileSystem.Opened); Assert.AreEqual(ToolStatus.NotExecuted, result.Operations.Single().Status);
    }

    [TestMethod]
    public async Task CTXAC012_CompactionPreservesSnapshotsButPinsCannotCreateOne()
    {
        await using var f = new ContextFlowFixture(5);
        Assert.AreEqual(WorkspaceStatus.Success, (await f.Files.ReadFileAsync("a", default)).Status);
        Assert.IsTrue(f.Files.Snapshots.TryGet("a", out var snapshot));
        Assert.AreEqual("none", f.Coordinator.AddPin("file a old content"));
        var result = await f.Run(); Assert.AreEqual(AgentTurnStatus.Completed, result.Status);
        Assert.IsTrue(f.Files.Snapshots.TryGet("a", out var after)); Assert.AreSame(snapshot, after);
        Assert.AreEqual(1, f.Files.Snapshots.Count);
    }
}

internal sealed class ContextFlowFixture : IAsyncDisposable
{
    internal BootstrapSession Session { get; } = WorkspaceFixtures.Session();
    internal ConversationContext Context { get; } = new();
    internal PlanStore Plans { get; }
    internal ContextCoordinator Coordinator { get; }
    internal GenerationBudget Budget { get; }
    internal ScriptClient Client { get; }
    internal ControlledWorkspaceFileSystem FileSystem { get; } = new();
    internal WorkspaceFiles Files { get; }
    internal AgentRunner Runner { get; }
    internal List<AgentEvent> Events { get; } = [];
    internal Action<AgentEvent>? OnEvent { get; set; }
    internal ContextFlowFixture(int turns)
    {
        for (var i = 0; i < turns; i++) Context.CommitCompletedTurn([LlmMessage.FromUserText("q" + i), LlmMessage.FromModel(new object())]);
        Plans = new(Session.Policy, Context.DecisionGate); Coordinator = new(Context, Plans, Session.Policy); Budget = new(Context.DecisionGate);
        FileSystem.File("a", [65]); Files = new(Session.Workspace, Session.Policy, FileSystem);
        Client = new() { Session = Session, Respond = (_, request, _) => Task.FromResult(ChatFixture.Completed(request.Tools.Count == 0 ? ContextTestData.Summary : "answer")) };
        var registry = TestRulesRegistry.Create([new ReadFileTool(Files, Session.Policy)], Session.Policy, new(Plans));
        Runner = new(Client, registry, Session.Policy, "test instruction", new(), e => { Events.Add(e); OnEvent?.Invoke(e); }, Budget, Coordinator, Client);
    }
    internal Task<AgentTurnResult> Run(CancellationToken token = default) => Runner.RunAsync(Context.BuildMessages(LlmMessage.FromUserText("current")), token);
    public async ValueTask DisposeAsync() { await Files.DisposeAsync(); await Client.DisposeAsync(); Session.Dispose(); }
}