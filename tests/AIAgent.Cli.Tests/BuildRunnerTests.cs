using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildRunnerTests
{
    [DataRow(5)]
    [DataRow(20)]
    [TestMethod]
    public async Task BUILDAC030_StandaloneFailedProcessStopsRemainderAndGeneration(int limit)
    {
        using var session = WorkspaceFixtures.Session();
        var client = new ScriptClient { Session = session };
        client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
            new LlmToolCall(0, "p", "run_dotnet_task", GeminiFixtures.Element("{}")), RunnerFixture.Read(1)));
        var registry = new ToolRegistry([new FailedProcess(session.Policy)], session.Policy);
        var budget = new GenerationBudget(); budget.SetTurnLimit(limit);
        var runner = new AgentRunner(client, registry, session.Policy, "instruction", new(), _ => { }, budget);
        var result = await runner.RunAsync([LlmMessage.FromUserText("build")], default);
        Assert.AreEqual(1, client.Requests.Count);
        Assert.AreEqual(AgentTurnStatus.Failed, result.Status);
        Assert.AreEqual(2, result.Operations.Count);
        Assert.AreEqual("turn_stopped", result.Operations[1].ErrorCode);
    }
    private sealed class FailedProcess(Security.ISensitiveDataPolicy policy) : ITool
    {
        public ToolCapability Capability => ToolCapability.DotnetTask;
        public LlmToolDefinition Definition { get; } = new("run_dotnet_task", "test", GeminiFixtures.Element("{\"type\":\"object\"}"));
        public Task<ToolExecutionResult> ExecuteAsync(System.Text.Json.JsonElement args, CancellationToken token) =>
            Task.FromResult(ToolResponseWriter.Create(ToolStatus.Failed, "process_failed", policy).WithProcessStop());
    }
}