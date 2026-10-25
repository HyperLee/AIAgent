using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildSdkBoundaryTests
{
    [TestMethod]
    public async Task BUILDAC033_ActualBodyLimitIncludesProcessResultAndPreservesLocalFact()
    {
        async Task<(int Bytes, int Calls, AgentTurnResult Result)> Run(int padding)
        {
            using var session = WorkspaceFixtures.Session();
            var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
            await using var files = new TestWorkspaceFilesFactory().Create(session, execution);
            await using var process = new TestProcessFactory().Create(session.Workspace, session.Policy, execution, files,
                (_, _) => new(ProcessApprovalDecision.Approved), _ => { });
            var registry = TestRulesRegistry.Create([new RunDotnetTaskTool(process, session.Policy)], session.Policy, execution);
            var handler = new GeminiHandler();
            handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 2
                ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"run_dotnet_task\",\"id\":\"p\",\"args\":{\"task\":\"build\",\"project_path\":\"a.csproj\"}},\"thoughtSignature\":\"c2ln\"}]")
                : GeminiFixtures.Text("done")));
            await using var client = new GeminiClient(session, handler);
            var runner = new AgentRunner(client, registry, session.Policy, new string('x', padding), new(), _ => { });
            var prior = await runner.RunAsync([LlmMessage.FromUserText("prior")], default);
            var result = await runner.RunAsync(prior.CompletedTurn.Append(LlmMessage.FromUserText("build")).ToArray(), default);
            return (handler.Bodies[^1].Length, handler.Calls, result);
        }
        var calibration = await Run(1); var padding = 262144 - calibration.Bytes + 1;
        var allowed = await Run(padding); var rejected = await Run(padding + 1);
        Assert.AreEqual(262144, allowed.Bytes); Assert.AreEqual(3, allowed.Calls);
        Assert.AreEqual(AgentTurnStatus.Completed, allowed.Result.Status);
        Assert.AreEqual(2, rejected.Calls); Assert.AreEqual(LlmErrorCode.InputTooLarge, rejected.Result.ModelErrorCode);
        Assert.AreEqual(ValidationOutcome.BuildSucceeded, rejected.Result.Operations[0].ProcessSummary!.ValidationOutcome);
    }

    [TestMethod]
    public async Task BUILDAC027_FifthMixedBatchNeverCallsProcessApprovalOrTaskUpdate()
    {
        using var session = WorkspaceFixtures.Session(); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        await using var files = new TestWorkspaceFilesFactory().Create(session, execution);
        var factory = new TestProcessFactory();
        await using var tasks = factory.Create(session.Workspace, session.Policy, execution, files, (_, _) => throw new AssertFailedException("approval"), _ => { });
        var registry = TestRulesRegistry.Create([new RunDotnetTaskTool(tasks, session.Policy), new UpdateTaskTool(plans, session.Policy)], session.Policy, execution);
        var client = new ScriptClient { Session = session };
        client.Respond = (i, _, _) => Task.FromResult(i < 4 ? RunnerFixture.Calls(new LlmToolCall(0, null, "unknown", GeminiFixtures.Element("{}"))) :
            RunnerFixture.Calls(new LlmToolCall(0, "p", "run_dotnet_task", GeminiFixtures.Element("{\"task\":\"build\",\"project_path\":\"a.csproj\"}")),
                new(1, "t", "update_task", GeminiFixtures.Element("{}"))));
        var result = await new AgentRunner(client, registry, session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("build")], default);
        Assert.AreEqual(5, client.Requests.Count); Assert.AreEqual(0, factory.Calls); Assert.IsNull(plans.Snapshot);
        CollectionAssert.AreEqual(new[] { "generation_limit", "generation_limit" }, result.Operations.TakeLast(2).Select(o => o.ErrorCode).ToArray());
    }

    [TestMethod]
    public async Task BUILDAC003_LaterSensitiveArgumentsPreventEarlierProcessFromStarting()
    {
        using var session = WorkspaceFixtures.Session(); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        await using var files = new TestWorkspaceFilesFactory().Create(session, execution); var factory = new TestProcessFactory();
        await using var tasks = factory.Create(session.Workspace, session.Policy, execution, files, (_, _) => throw new AssertFailedException("approval"), _ => { });
        var registry = TestRulesRegistry.Create([new RunDotnetTaskTool(tasks, session.Policy)], session.Policy, execution);
        var client = new ScriptClient { Session = session };
        client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
            new(0, "p", "run_dotnet_task", GeminiFixtures.Element("{\"task\":\"build\",\"project_path\":\"a.csproj\"}")),
            new(1, "later", "unknown", System.Text.Json.JsonSerializer.SerializeToElement(new { text = GeminiFixtures.Key }))));
        var result = await new AgentRunner(client, registry, session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("build")], default);
        Assert.AreEqual(0, factory.Calls); Assert.AreEqual(1, client.Requests.Count);
        Assert.AreEqual("turn_stopped", result.Operations[0].ErrorCode);
        Assert.AreEqual("sensitive_arguments", result.Operations[1].ErrorCode);
    }
}