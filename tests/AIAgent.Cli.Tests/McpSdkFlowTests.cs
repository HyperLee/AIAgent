using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Mcp;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpSdkFlowTests
{
    [TestMethod]
    public async Task MCPAC026_ActualGeminiBodyBoundaryIncludesMcpDefinitionsAndNativeResults()
    {
        async Task<(int Bytes, int HttpCalls, AgentTurnResult Result)> Run(int padding)
        {
            await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
            var registry = new ToolRegistry([], f.Session.Policy, f.Execution); var http = new GeminiHandler();
            http.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(http.Calls == 1 ? PlanSdkTests.Batch((McpProfile.DocsName, new { query = "public C#" })) : GeminiFixtures.Text()));
            await using var client = new GeminiClient(f.Session, http);
            var result = await new AgentRunner(client, registry, f.Session.Policy, new string('x', padding), new(), _ => { })
                .RunAsync([LlmMessage.FromUserText("question")], default);
            return (http.Bodies.Last().Length, http.Calls, result);
        }
        var baseline = await Run(1); var padding = 262144 - baseline.Bytes + 1;
        var accepted = await Run(padding); var rejected = await Run(padding + 1);
        Assert.AreEqual(262144, accepted.Bytes); Assert.AreEqual(2, accepted.HttpCalls); Assert.AreEqual(AgentTurnStatus.Completed, accepted.Result.Status);
        Assert.AreEqual(1, rejected.HttpCalls); Assert.AreEqual(2, rejected.Result.GenerationsStarted);
        Assert.AreEqual(LlmErrorCode.InputTooLarge, rejected.Result.ModelErrorCode); Assert.AreEqual(0, rejected.Result.CompletedTurn.Count);
    }

    [TestMethod]
    public async Task MCPAC026_RealGeminiAndMcpSdkPreserveMixedNativeCallPairing()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var registry = new ToolRegistry([], f.Session.Policy, f.Execution);
        var http = new GeminiHandler();
        http.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(http.Calls == 1 ? PlanSdkTests.Batch(
            (McpProfile.DocsName, new { query = "public C#" }), (McpProfile.CodeName, new { query = "public code" })) : GeminiFixtures.Text()));
        await using var client = new GeminiClient(f.Session, http);
        var runner = new AgentRunner(client, registry, f.Session.Policy, "synthetic instruction", new(), _ => { });
        var turn = await runner.RunAsync([LlmMessage.FromUserText("public documentation")], default);
        Assert.AreEqual(AgentTurnStatus.Completed, turn.Status); Assert.AreEqual(2, http.Calls);
        Assert.AreEqual(2, f.Previews.Count); Assert.AreEqual(0, f.Plans.Facts.Count);
        using var body = JsonDocument.Parse(http.Bodies[1]);
        var contents = body.RootElement.GetProperty("contents");
        Assert.AreEqual("c2ln", contents[1].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        var responses = contents[2].GetProperty("parts");
        Assert.AreEqual("call-1", responses[1].GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.AreEqual("external_mcp", responses[0].GetProperty("functionResponse").GetProperty("response").GetProperty("data").GetProperty("source").GetString());
        var projection = GeminiContextTools.Project(turn.CompletedTurn[2].Results[0])!.Value;
        Assert.IsFalse(projection.TryGetProperty("content", out _)); Assert.IsFalse(projection.TryGetProperty("arguments", out _));
        Assert.AreEqual("learn", projection.GetProperty("server_id").GetString());
    }

    [TestMethod]
    public async Task MCPAC005_FormalChatAddsTwoToolsAndDisconnectRestoresBuiltins()
    {
        var root = WorkspaceFixtures.NewDirectory(); var fixture = new ChatFixture("/mcp connect learn", "first", "/plan", "second", "/mcp disconnect", "third");
        using var session = WorkspaceFixtures.Session(root); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        var handler = new McpHttpFixture();
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "synthetic instruction", new TestWorkspaceFilesFactory(),
            mcpClientFactory: guard => new McpReadClient(guard, handler));
        Assert.AreEqual(0, await app.RunAsync(session, default));
        CollectionAssert.AreEqual(new[] { 14, 11, 9 }, fixture.Client.Requests.Select(r => r.Tools.Count).ToArray());
        Assert.AreEqual(McpProfile.CodeName, fixture.Client.Requests[0].Tools[^1].Name);
        Assert.IsTrue(handler.Closed); Assert.AreEqual(3, handler.Methods.Count);
        Assert.AreEqual(3L, fixture.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task MCPAC016_ChangedMcpBeforePlainTextAdoptionNeverCommitsAnswer()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var registry = new ToolRegistry([], f.Session.Policy, f.Execution);
        var client = new ScriptClient();
        client.Respond = (_, _, _) => { f.Mcp.MarkStale(); return Task.FromResult(ChatFixture.Completed("obsolete")); };
        var result = await new AgentRunner(client, registry, f.Session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("question")], default);
        Assert.AreEqual(AgentStopReason.McpChanged, result.StopReason); Assert.AreEqual(0, result.CompletedTurn.Count); Assert.AreEqual("", result.SafeText);
    }
}