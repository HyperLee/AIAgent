using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class AgentBoundaryTests
{
    [TestMethod]
    public async Task LOOPAC026_SensitiveBatchRetainsSafeClassificationsForEveryKnownCall()
    {
        await using var f = new RunnerFixture();
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0),
            new(1, null, "unknown", GeminiFixtures.Element("{\"unused\":\"" + GeminiFixtures.Key + "\"}"))));
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.AreEqual(2, result.Operations.Count, "LOOP-AC-026/048");
        Assert.IsTrue(result.Operations[0].Status == ToolStatus.NotExecuted && result.Operations[1].ErrorCode == "sensitive_arguments", "LOOP-AC-026");
        Assert.IsTrue(result.RequiresContextReset && f.FileSystem.Opened == 0, "LOOP-AC-026");
    }

    [TestMethod]
    public async Task LOOPAC014_RootLossRetainsSuccessAndStopsRemainingCalls()
    {
        await using var f = new RunnerFixture();
        f.OnEvent = e => { if (e.Operation?.Status == ToolStatus.Success) f.FileSystem.Nodes.Remove("/ws"); };
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1), RunnerFixture.Read(2)));
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.IsTrue(result.StopReason == AgentStopReason.WorkspaceUnavailable && result.Operations.Count == 3, "LOOP-AC-014");
        Assert.IsTrue(result.Operations[0].Status == ToolStatus.Success && result.Operations[1].ErrorCode == "workspace_unavailable" && result.Operations[2].Status == ToolStatus.NotExecuted, "LOOP-AC-014");
        Assert.IsTrue(f.Workspace.Snapshots.Count == 0 && f.Client.Requests.Count == 1, "LOOP-AC-014");
    }

    [TestMethod]
    public async Task LOOPAC022_CancelBeforeWorkspacePublicationNeverReturnsSuccess()
    {
        await using var f = new RunnerFixture();
        using var cancel = new CancellationTokenSource();
        f.FileSystem.OnRead = _ => { cancel.Cancel(); return Task.CompletedTask; };
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1)));
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], cancel.Token);
        Assert.IsTrue(result.Status == AgentTurnStatus.Cancelled && result.Operations.Count == 2 &&
            result.Operations[0].Status == ToolStatus.Cancelled && result.Operations[1].Status == ToolStatus.NotExecuted, "LOOP-AC-022");
        Assert.AreEqual(0, f.Workspace.Snapshots.Count, "LOOP-AC-022");
    }

    [TestMethod]
    public async Task LOOPAC016_CancelFromGeneratingEventDoesNotAcquireStartPermission()
    {
        await using var f = new RunnerFixture();
        using var cancel = new CancellationTokenSource();
        f.OnEvent = _ => cancel.Cancel();
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], cancel.Token);
        Assert.IsTrue(result.GenerationsStarted == 0 && f.Client.Requests.Count == 0 && result.TurnUsage.DispatchedRequests == 0, "LOOP-AC-016");
        f.OnEvent = null;
        Assert.AreEqual(AgentTurnStatus.Completed, (await f.Runner.RunAsync([LlmMessage.FromUserText("next")], default)).Status, "LOOP-AC-021");
    }

    [TestMethod]
    public async Task LOOPAC032_ThreeMethodCallsTwoTransportsAndOneMissingTotal()
    {
        await using var f = new RunnerFixture();
        f.Client.Respond = (i, _, _) =>
        {
            if (i == 2) return Task.FromResult(new LlmResult(LlmStatus.Failed, LlmErrorCode.InputTooLarge, "", [], null, null, false, false));
            var original = RunnerFixture.Calls(RunnerFixture.Read(0));
            return Task.FromResult(new LlmResult(original.Status, original.ErrorCode, original.SafeText, original.ToolCalls,
                original.ModelMessage, i == 0 ? new(0, null, -1, 0, 10) : null, true, false));
        };
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.IsTrue(result.GenerationsStarted == 3 && result.TurnUsage.DispatchedRequests == 2 &&
            result.TurnUsage.Total.KnownTotal == 10 && result.TurnUsage.Total.MissingRequestCount == 1, "LOOP-AC-032");
        Assert.IsTrue(result.TurnUsage.Prompt.HasKnownValue && result.TurnUsage.Prompt.KnownTotal == 0 &&
            result.TurnUsage.Prompt.MissingRequestCount == 1 && result.TurnUsage.Thought.MissingRequestCount == 2, "LOOP-AC-032");
        Assert.AreEqual(result.TurnUsage, f.Usage.Snapshot(), "LOOP-AC-032");
    }

    [TestMethod]
    public async Task LOOPAC017_NonCompletedStatusesNeverExecuteAttachedCalls()
    {
        foreach (var status in new[] { LlmStatus.Empty, LlmStatus.Refused, LlmStatus.Truncated, LlmStatus.Cancelled, LlmStatus.Failed, (LlmStatus)999 })
        {
            await using var f = new RunnerFixture();
            f.Client.Respond = (_, _, _) => Task.FromResult(new LlmResult(status,
                status == LlmStatus.Failed ? LlmErrorCode.NetworkFailure : LlmErrorCode.None, "safe",
                [RunnerFixture.Read(0)], null, null, true, false));
            var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
            Assert.IsTrue(result.StopReason == AgentStopReason.InvalidProtocol && result.ModelStatus is null &&
                result.RequiresContextReset && f.FileSystem.Opened == 0, "LOOP-AC-017/018");
        }
    }

    [TestMethod]
    public async Task LOOPAC028_UnsafeFixedResultFieldsStopAndClearWithoutPayloadFallback()
    {
        using var session = WorkspaceFixtures.Session(key: "status");
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
        await using var workspace = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var registry = new ToolRegistry([new ReadFileTool(workspace, session.Policy)], session.Policy);
        var client = new ScriptClient { Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1))) };
        var result = await new AgentRunner(client, registry, session.Policy, "instruction", new(), _ => { })
            .RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.IsTrue(result.RequiresContextReset && result.StopReason == AgentStopReason.SensitiveData &&
            result.Operations.Count == 2 && workspace.Snapshots.Count == 0 && client.Requests.Count == 1, "LOOP-AC-028");
        Assert.IsTrue(result.Operations.All(o => o.RelativePath is null), "LOOP-AC-028");
    }

    [TestMethod]
    public void LOOPAC002_ContentContractsCannotDumpJsonOrRetainSourceCollections()
    {
        using var document = JsonDocument.Parse("{\"data\":1}");
        var result = new ToolExecutionResult(ToolStatus.Success, "none", document.RootElement, "a", 1);
        var operations = new List<LocalToolOutcome> { new(1, 0, "read_file", result) };
        var turn = new AgentTurnResult(AgentTurnStatus.Failed, AgentStopReason.LocalFailure, null, null, "", [], operations, 1, new UsageAccumulator().Snapshot(), false);
        var last = new LastTurnOutcome(turn, turn.Status, turn.StopReason);
        operations.Clear(); document.Dispose();
        Assert.AreEqual(1, result.Response!.Value.GetProperty("data").GetInt32(), "LOOP-AC-002");
        Assert.AreEqual(1, last.Operations.Count, "LOOP-AC-002");
        foreach (var item in new object[] { result, turn, last, last.Operations[0], new AgentEvent(AgentEventKind.Generating, 1) })
        {
            Assert.IsTrue(JsonSerializer.Serialize(item) == "{}", "LOOP-AC-002");
            Assert.IsFalse(item.ToString()!.Contains("read_file", StringComparison.Ordinal), "LOOP-AC-002");
        }
        Assert.IsFalse(typeof(LastTurnOutcome).GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Any(p => p.PropertyType == typeof(LlmMessage) || p.Name is "CompletedTurn" or "SafeText" or "Response"), "LOOP-AC-054");
    }
    [TestMethod]
    public async Task LOOPAC017_FifthResponseStillValidatesSensitiveAndMalformedCallsFirst()
    {
        foreach (var sensitive in new[] { true, false })
        {
            await using var f = new RunnerFixture();
            f.Client.Respond = (i, _, _) => Task.FromResult(i < 4 ? RunnerFixture.Calls(RunnerFixture.Read(0)) : RunnerFixture.Calls(RunnerFixture.Read(0),
                sensitive ? new(1, null, "unknown", GeminiFixtures.Element("{\"extra\":\"" + GeminiFixtures.Key + "\"}")) : RunnerFixture.Read(0)));
            var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
            Assert.IsTrue(result.GenerationsStarted == 5 && result.RequiresContextReset && f.FileSystem.Opened == 4 &&
                result.StopReason == (sensitive ? AgentStopReason.SensitiveData : AgentStopReason.InvalidProtocol), "LOOP-AC-017");
        }
    }

    [TestMethod]
    public async Task LOOPAC008_BatchIdsAndPositionsStayLocalToEachSequentialGeneration()
    {
        await using var f = new RunnerFixture();
        var active = 0; var maximum = 0;
        f.FileSystem.OnRead = async _ => { active++; maximum = Math.Max(maximum, active); await Task.Yield(); active--; };
        static LlmToolCall Call(int index, string id) => new(index, id, "read_file", GeminiFixtures.Element("{\"path\":\"a\"}"));
        f.Client.Respond = (i, _, _) => Task.FromResult(i switch
        {
            0 => RunnerFixture.Calls(Call(1, "a"), Call(3, "b")),
            1 => RunnerFixture.Calls(Call(1, "a")),
            _ => ChatFixture.Completed()
        });
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.IsTrue(result.Status == AgentTurnStatus.Completed && result.CompletedTurn.Count == 6 && f.FileSystem.Opened == 3 && maximum == 1, "LOOP-AC-008");
        Assert.IsTrue(result.Operations.Select(o => o.GenerationNumber).SequenceEqual(new[] { 1, 1, 2 }) &&
            f.Client.Requests[2].Messages[^1].Results.Single().CallId == "a", "LOOP-AC-008");
    }

}