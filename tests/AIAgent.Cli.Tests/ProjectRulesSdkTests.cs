using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ProjectRulesSdkTests
{
    [TestMethod]
    public async Task RULEAC031_ExactSdkBodyIncludesRulesMemoryPlanAndMultiCallSignatures()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", Encoding.UTF8.GetBytes("Use tests.")); await f.Rules.TryReloadAsync(default);
        var handler = new GeminiHandler(); handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1
            ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"read_file\",\"id\":\"a\",\"args\":{\"path\":\"a\"}},\"thoughtSignature\":\"c2lnLWE=\"},{\"functionCall\":{\"name\":\"read_file\",\"id\":\"b\",\"args\":{\"path\":\"b\"}},\"thoughtSignature\":\"c2lnLWI=\"}]")
            : GeminiFixtures.Text("done")));
        await using var client = new GeminiClient(f.Session, handler);
        var tools = new[] { GeminiFixtures.Tool("read_file") }; var user = LlmMessage.FromUserText("question");
        var calls = await client.GenerateAsync(new("fixture" + f.Rules.BuildAppendix(), [user], tools), default);
        Assert.AreEqual(2, calls.ToolCalls.Count);
        var responses = calls.ToolCalls.Select(c => new LlmToolResult(c.PartIndex, c.CallId, c.Name,
            GeminiFixtures.Element("{\"status\":\"success\",\"error_code\":\"none\",\"data\":{\"path\":\"a\",\"content\":\"data\",\"byte_count\":4}}"))).ToArray();
        var messages = new[] { user, calls.ModelMessage!, LlmMessage.FromToolResults(responses) };
        var memory = Encoding.UTF8.GetString(ContextDocuments.Memory(new(1, 1, ["goal"], [], [], []), [new("n1", "pin")]));
        var appendix = f.Rules.BuildAppendix() + memory + f.Plans.StateAppendix();
        var inspector = client.CreateContextInspector(); long actual = 0; var dispatched = false;
        using var report = ((ILlmContextTelemetry)inspector).ObserveRequest((bytes, sent) => { actual = bytes; dispatched = sent; });
        await client.GenerateAsync(new("x" + appendix, messages, tools), default);
        var padding = checked(262144 - (int)actual + 1);
        Assert.IsTrue(padding > 0);
        var accepted = await client.GenerateAsync(new(new string('x', padding) + appendix, messages, tools), default);
        Assert.AreEqual(LlmStatus.Completed, accepted.Status); Assert.AreEqual(262144L, actual); Assert.IsTrue(dispatched);
        var before = handler.Calls;
        var rejected = await client.GenerateAsync(new(new string('x', padding + 1) + appendix, messages, tools), default);
        Assert.AreEqual(LlmErrorCode.InputTooLarge, rejected.ErrorCode); Assert.AreEqual(262145L, actual);
        Assert.IsFalse(dispatched); Assert.AreEqual(before, handler.Calls);
        using var body = JsonDocument.Parse(handler.Bodies.Last()); var contents = body.RootElement.GetProperty("contents");
        Assert.AreEqual("c2lnLWE=", contents[1].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        Assert.AreEqual("c2lnLWI=", contents[1].GetProperty("parts")[1].GetProperty("thoughtSignature").GetString());
        Assert.AreEqual("a", contents[2].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("id").GetString());
    }

    [TestMethod]
    public async Task RULEAC031_RealAdapterRejectsResponseFromChangedRulesWithoutRefund()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/AGENTS.md", "old");
        var interaction = new ScriptInteraction(); interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("question"));
        var handler = new GeminiHandler(); handler.Respond = async (_, _) =>
        { await File.WriteAllTextAsync(root + "/AGENTS.md", "new"); return GeminiHandler.Json(GeminiFixtures.Text("obsolete")); };
        var usage = new UsageAccumulator();
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), usage, "fixture", new Workspace.WorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default));
        Assert.AreEqual(1, handler.Calls); Assert.AreEqual(1L, usage.Snapshot().DispatchedRequests);
        Assert.IsFalse(interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
        Assert.IsTrue(interaction.Events.Any(e => e.StopReason == AgentStopReason.RulesChanged));
    }

    [TestMethod]
    public async Task RULEAC031_RealSummaryDoesNotDirectlyIncludeRules()
    {
        var root = WorkspaceFixtures.NewDirectory(); const string marker = "RULE_SOURCE_MARKER"; await File.WriteAllTextAsync(root + "/AGENTS.md", marker);
        var interaction = new ScriptInteraction(); foreach (var input in new[] { "one", "two", "three", "/compact" }) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(input));
        var handler = new GeminiHandler(); var summaries = 0;
        handler.Respond = async (request, token) =>
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(token); using var body = JsonDocument.Parse(bytes);
            if (!body.RootElement.TryGetProperty("tools", out _))
            {
                summaries++; Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains(marker, StringComparison.Ordinal));
                return GeminiHandler.Json(GeminiFixtures.Text(ContextTestData.Summary));
            }
            Assert.IsTrue(Encoding.UTF8.GetString(bytes).Contains(marker, StringComparison.Ordinal));
            return GeminiHandler.Json(GeminiFixtures.Text("answer"));
        };
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "fixture", new Workspace.WorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default));
        Assert.AreEqual(1, summaries); Assert.AreEqual(4, handler.Calls);
    }

    [TestMethod]
    public async Task RULEAC021_CancelDuringBatchRuleGateStillPairsEveryUnstartedCall()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); await f.Rules.TryReloadAsync(default);
        using var cancel = new CancellationTokenSource(); var client = new ScriptClient { Session = f.Session };
        client.Respond = (_, _, _) =>
        {
            f.Fs.OnRead = _ => { cancel.Cancel(); return Task.CompletedTask; };
            return Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1)));
        };
        var registry = new ToolRegistry([new ReadFileTool(f.Files, f.Session.Policy)], f.Session.Policy, f.Execution);
        var result = await new AgentRunner(client, registry, f.Session.Policy, "fixture", new(), _ => { }).RunAsync([LlmMessage.FromUserText("question")], cancel.Token);
        Assert.AreEqual(AgentTurnStatus.Cancelled, result.Status); Assert.AreEqual(2, result.Operations.Count);
        Assert.IsTrue(result.Operations.All(o => o.Status == ToolStatus.NotExecuted));
    }
    [TestMethod]
    public async Task RULEAC019_ValidNewRevisionCannotAdoptAnOlderGenerationResponse()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); await f.Rules.TryReloadAsync(default);
        var client = new ScriptClient
        {
            Session = f.Session,
            Respond = async (_, _, _) =>
        {
            f.Fs.Nodes["/ws/AGENTS.md"].Bytes = [66]; Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
            return ChatFixture.Completed("old revision response");
        }
        };
        var registry = new ToolRegistry([new ReadFileTool(f.Files, f.Session.Policy)], f.Session.Policy, f.Execution);
        var result = await new AgentRunner(client, registry, f.Session.Policy, "fixture", new(), _ => { }).RunAsync([LlmMessage.FromUserText("question")], default);
        Assert.AreEqual(AgentStopReason.RulesChanged, result.StopReason); Assert.AreEqual(0, result.CompletedTurn.Count);
        Assert.AreEqual(ProjectRulesStatus.Valid, f.Rules.Capture().Status); Assert.AreEqual(2L, f.Rules.Capture().RulesRevision);
        Assert.AreEqual(1, result.GenerationsStarted);
    }

}