using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class AgentRunnerTests
{
    [TestMethod]
    public async Task LOOPAC015_FifthAnswerCompletesButFifthCallsNeverExecute()
    {
        foreach (var finalAnswer in new[] { true, false })
        {
            await using var f = new RunnerFixture();
            f.Client.Respond = (i, _, _) => Task.FromResult(i == 4 && finalAnswer ? ChatFixture.Completed() : RunnerFixture.Calls(RunnerFixture.Read(0)));
            var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
            Assert.AreEqual(5, result.GenerationsStarted, "LOOP-AC-015");
            Assert.AreEqual(4, f.FileSystem.Opened, "LOOP-AC-015");
            Assert.AreEqual(finalAnswer ? AgentTurnStatus.Completed : AgentTurnStatus.Stopped, result.Status, "LOOP-AC-015");
            Assert.AreEqual(finalAnswer ? 10 : 0, result.CompletedTurn.Count, "LOOP-AC-015");
            Assert.AreEqual(finalAnswer ? 4 : 5, result.Operations.Count, "LOOP-AC-015");
            if (!finalAnswer) Assert.IsTrue(result.Operations[^1].ErrorCode == "generation_limit", "LOOP-AC-015");
            Assert.AreEqual(5L, f.Usage.Snapshot().DispatchedRequests, "LOOP-AC-032");
        }
    }

    [TestMethod]
    public async Task LOOPAC008_SameNameNoIdCallsExecuteInPartOrderAndStayDistinct()
    {
        await using var f = new RunnerFixture();
        f.Client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(RunnerFixture.Read(1), RunnerFixture.Read(3)) : ChatFixture.Completed());
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.AreEqual(AgentTurnStatus.Completed, result.Status, "LOOP-AC-008");
        Assert.IsTrue(result.Operations.Select(o => o.PartIndex).SequenceEqual(new[] { 1, 3 }) && f.FileSystem.Opened == 2, "LOOP-AC-008");
        Assert.AreEqual(2, f.Client.Requests[1].Messages[^1].Results.Count, "LOOP-AC-008");
        Assert.IsTrue(f.Client.Requests.All(r => r.Tools.Count == 2), "LOOP-AC-003");
    }

    [TestMethod]
    public async Task LOOPAC026_EntireBatchIsCheckedBeforeAnyToolIo()
    {
        foreach (var mode in new[] { "name", "id", "args", "text", "projection", "order" })
        {
            await using var f = new RunnerFixture();
            var a = RunnerFixture.Read(0);
            var b = mode switch
            {
                "name" => new LlmToolCall(1, null, GeminiFixtures.Key, GeminiFixtures.Element("{}")),
                "id" => new LlmToolCall(1, GeminiFixtures.Key, "read_file", GeminiFixtures.Element("{}")),
                "args" => new LlmToolCall(1, null, "unknown", GeminiFixtures.Element("{\"extra\":\"" + GeminiFixtures.Key + "\"}")),
                "order" => RunnerFixture.Read(0),
                _ => RunnerFixture.Read(1)
            };
            var calls = new[] { a, b };
            f.Client.Respond = (_, _, _) => Task.FromResult(new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None,
                mode == "text" ? GeminiFixtures.Key : "", calls,
                LlmMessage.FromModel(new object(), mode == "projection" ? [a] : calls), new(1, 2, 3, 4, 5), true, false));
            var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
            Assert.IsTrue(result.RequiresContextReset && result.Status == AgentTurnStatus.Failed && result.CompletedTurn.Count == 0, "LOOP-AC-026/010");
            Assert.AreEqual(0, f.FileSystem.Opened, "LOOP-AC-026");
            Assert.AreEqual(1L, result.TurnUsage.DispatchedRequests, "LOOP-AC-032");
        }
    }

    [TestMethod]
    public async Task LOOPAC021_CancelBetweenToolsRetainsPublishedSuccessAndMarksRemainder()
    {
        await using var f = new RunnerFixture();
        using var cancel = new CancellationTokenSource();
        f.OnEvent = e => { if (e.Kind == AgentEventKind.ToolCompleted) cancel.Cancel(); };
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1)));
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], cancel.Token);
        Assert.IsTrue(result.Status == AgentTurnStatus.Cancelled && result.Operations.Count == 2 && f.FileSystem.Opened == 1, "LOOP-AC-021");
        Assert.IsTrue(result.Operations[0].Status == ToolStatus.Success && result.Operations[1].Status == ToolStatus.NotExecuted, "LOOP-AC-022");
        Assert.AreEqual(1, f.Workspace.Snapshots.Count, "LOOP-AC-022");
    }

    [TestMethod]
    public async Task LOOPAC011_RejectionsContinueAndFailedGenerationPreservesReads()
    {
        await using var f = new RunnerFixture();
        f.Client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(
            new(0, null, "shell", GeminiFixtures.Element("{}")), RunnerFixture.Read(1)) :
            new LlmResult(LlmStatus.Failed, LlmErrorCode.NetworkFailure, "", [], null, null, true, false));
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.IsTrue(result.Status == AgentTurnStatus.Failed && result.ModelErrorCode == LlmErrorCode.NetworkFailure && result.CompletedTurn.Count == 0, "LOOP-AC-030");
        Assert.IsTrue(result.Operations.Count == 2 && result.Operations[0].ToolName is null && result.Operations[0].Status == ToolStatus.Rejected && result.Operations[1].Status == ToolStatus.Success, "LOOP-AC-011");
        Assert.AreEqual(1, f.FileSystem.Opened, "LOOP-AC-030");
    }

    [TestMethod]
    public async Task LOOPAC002_ConcurrentInvalidAndPreCancelledTurnsDoNotStartWork()
    {
        await using var f = new RunnerFixture();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<LlmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.Respond = (_, _, _) => { started.SetResult(); return release.Task; };
        var first = f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var concurrent = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.AreEqual(AgentStopReason.RunnerUnavailable, concurrent.StopReason, "LOOP-AC-002");
        release.SetResult(ChatFixture.Completed()); await first;
        Assert.AreEqual(0, (await f.Runner.RunAsync([], default)).GenerationsStarted, "LOOP-AC-002");
        Assert.AreEqual(0, (await f.Runner.RunAsync([LlmMessage.FromUserText("q")], new CancellationToken(true))).GenerationsStarted, "LOOP-AC-016");
        Assert.AreEqual(1, f.Client.Requests.Count, "LOOP-AC-002");
    }
}

internal sealed class RunnerFixture : IAsyncDisposable
{
    internal BootstrapSession Session { get; } = WorkspaceFixtures.Session();
    internal ControlledWorkspaceFileSystem FileSystem { get; } = new();
    internal ScriptClient Client { get; } = new();
    internal WorkspaceFiles Workspace { get; }
    internal UsageAccumulator Usage { get; } = new();
    internal ToolRegistry Registry { get; }
    internal AgentRunner Runner { get; }
    internal Action<AgentEvent>? OnEvent { get; set; }
    internal List<AgentEvent> Events { get; } = [];
    internal RunnerFixture()
    {
        FileSystem.File("a", [65]); Client.Session = Session;
        Workspace = new(Session.Workspace, Session.Policy, FileSystem);
        Registry = new([new ListFilesTool(Workspace, Session.Policy), new ReadFileTool(Workspace, Session.Policy)], Session.Policy);
        Runner = new(Client, Registry, Session.Policy, "test instruction", Usage, e => { Events.Add(e); OnEvent?.Invoke(e); });
    }
    internal static LlmToolCall Read(int index) => new(index, null, "read_file", GeminiFixtures.Element("{\"path\":\"a\"}"));
    internal static LlmResult Calls(params LlmToolCall[] calls) => new(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls,
        LlmMessage.FromModel(new object(), calls), new(1, 2, 3, 4, 5), true, false);
    public async ValueTask DisposeAsync() { await Workspace.DisposeAsync(); await Client.DisposeAsync(); Session.Dispose(); }
}