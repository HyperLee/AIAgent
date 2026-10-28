using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WebFlowTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WEBAC010_RealSdksAllowAnswerAfterTwoCallsButStopAnActualThirdCall(bool third)
    {
        await using var f = new McpFixture(new WebClock()); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var registry = new ToolRegistry([], f.Session.Policy, f.Execution); var http = new GeminiHandler();
        http.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(http.Calls switch
        {
            1 => PlanSdkTests.Batch((McpProfile.DocsName, new { query = "public C#" })),
            2 => PlanSdkTests.Batch((WebFixtures.FetchName, new { url = WebFixtures.Url + "?view=net-10.0" })),
            _ => third ? PlanSdkTests.Batch((WebFixtures.FetchName, new { url = WebFixtures.Url })) : GeminiFixtures.Text()
        }));
        await using var client = new GeminiClient(f.Session, http);
        var result = await new AgentRunner(client, registry, f.Session.Policy, "synthetic instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("question")], default);
        Assert.AreEqual(third ? AgentTurnStatus.Stopped : AgentTurnStatus.Completed, result.Status);
        Assert.AreEqual(3, http.Calls); Assert.AreEqual(2, f.Previews.Count); Assert.AreEqual(2, f.Handlers[0].Calls.Count);
        Assert.AreEqual(0, f.Plans.Facts.Count); Assert.AreEqual(0L, f.Plans.EvidenceEpoch);
        if (third) Assert.AreEqual("mcp_limit", result.Operations.Last().ErrorCode);
        else
        {
            using var wire = JsonDocument.Parse(http.Bodies.Last());
            var response = wire.RootElement.GetProperty("contents")[4].GetProperty("parts")[0].GetProperty("functionResponse");
            Assert.AreEqual(WebFixtures.FetchName, response.GetProperty("name").GetString());
            Assert.AreEqual("provider_response", response.GetProperty("response").GetProperty("data").GetProperty("document_source").GetProperty("observation").GetString());
            var projected = GeminiContextTools.Project(result.CompletedTurn[4].Results[0]);
            Assert.IsNotNull(projected); Assert.AreEqual("microsoft_docs_fetch", projected.Value.GetProperty("remote_tool").GetString());
            foreach (var forbidden in new[] { "document_source", "url", "requested_url", "retrieved_at_utc", "safe_payload_sha256", "requested_view", "content", "structured_content", "arguments" })
                Assert.IsFalse(projected.Value.TryGetProperty(forbidden, out _));
        }
    }

    [TestMethod]
    public async Task WEBAC009_UrlRejectionPreservesEarlierLocalReadAndPairsRemainingCalls()
    {
        await using var f = new McpFixture(); File.WriteAllText(f.Root + "/note.md", "public note"); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var registry = new ToolRegistry([new ReadFileTool(f.Files, f.Session.Policy)], f.Session.Policy, f.Execution);
        var client = new ScriptClient();
        client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
            new(0, "read-id", "read_file", JsonSerializer.SerializeToElement(new { path = "note.md" })),
            new(1, "fetch-id", WebFixtures.FetchName, JsonSerializer.SerializeToElement(new { url = WebFixtures.Url + "#fragment" })),
            new(2, "later-id", "read_file", JsonSerializer.SerializeToElement(new { path = "note.md" }))));
        var result = await new AgentRunner(client, registry, f.Session.Policy, "synthetic instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("question")], default);
        Assert.AreEqual(AgentStopReason.McpStopped, result.StopReason); Assert.AreEqual(3, result.Operations.Count);
        Assert.AreEqual(ToolStatus.Success, result.Operations[0].Status); Assert.AreEqual("note.md", result.Operations[0].RelativePath);
        Assert.AreEqual("web_url_rejected", result.Operations[1].ErrorCode); Assert.AreEqual(ToolStatus.NotExecuted, result.Operations[2].Status);
        Assert.AreEqual(1, client.Requests.Count); Assert.AreEqual(0, f.Previews.Count); Assert.AreEqual(0, f.Handlers[0].Calls.Count);
    }

    [TestMethod]
    [DataRow("catalog")]
    [DataRow("mode")]
    [DataRow("rules")]
    [DataRow("skills")]
    public async Task WEBAC016_ConfirmationCannotAuthorizeChangedSources(string change)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        f.OnConfirm = () =>
        {
            switch (change)
            {
                case "catalog": f.Catalog = f.Catalog.Replace("Fetch docs", "Changed docs", StringComparison.Ordinal); break;
                case "mode": f.Plans.SetMode(AgentMode.Plan); break;
                case "rules": File.WriteAllText(f.Root + "/AGENTS.md", "changed public rule"); break;
                case "skills": f.Execution.Skills!.Clear(); break;
            }
            return McpApprovalDecision.Approved;
        };
        var result = await WebFixtures.Fetch(f);
        Assert.AreNotEqual(ToolStatus.Success, result.Status); Assert.AreEqual(0, f.Handlers[0].Calls.Count);
        Assert.AreEqual(McpTransmission.NotSent, result.McpOutcome!.Transmission); Assert.IsFalse(result.McpOutcome.SafeResultAccepted);
    }

    [TestMethod]
    public async Task WEBAC016_LateProviderResponseNeverPublishesSourceOrSuccessHistory()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        f.Observe = method => { if (method == "tools/call") f.Mcp.MarkStale(); };
        var result = await WebFixtures.Fetch(f);
        Assert.AreEqual("mcp_changed", result.ErrorCode); Assert.AreEqual(McpTransmission.ResponseReceived, result.McpOutcome!.Transmission);
        Assert.IsFalse(result.McpOutcome.SafeResultAccepted); Assert.IsNull(result.McpOutcome.Response);
        Assert.IsFalse(result.Response!.Value.TryGetProperty("data", out _));
    }

    [TestMethod]
    public async Task WEBAC008_FetchPreviewExplainsProviderDestinationAndEachCallNeedsNewConsent()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        Assert.AreEqual(ToolStatus.Success, (await WebFixtures.Fetch(f)).Status);
        f.OnConfirm = () => McpApprovalDecision.Declined;
        Assert.AreEqual("approval_declined", (await WebFixtures.Fetch(f)).ErrorCode);
        Assert.AreEqual(2, f.Previews.Count); Assert.AreEqual(1, f.Handlers[0].Calls.Count);
        StringAssert.Contains(f.Previews[0], "將 URL 交給 Learn 服務擷取"); StringAssert.Contains(f.Previews[0], "原站狀態與版次未由本機驗證");
        StringAssert.Contains(f.Previews[0], WebFixtures.Url); StringAssert.Contains(f.Previews[0], McpProfile.Endpoint);
    }

    [TestMethod]
    public async Task WEBAC020_ReviewCannotSendFetchOrCatalogRequests()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var before = f.Handlers[0].Methods.Count;
        using (f.Execution.EnterReview()) Assert.AreEqual("mode_restricted", (await WebFixtures.Fetch(f)).ErrorCode);
        Assert.AreEqual(before, f.Handlers[0].Methods.Count); Assert.AreEqual(0, f.Plans.Facts.Count);
    }

    [TestMethod]
    public async Task WEBAC023_ActualGeminiRequestIncludesSourceInIndependentBodyLimit()
    {
        async Task<(int Bytes, int Calls, AgentTurnResult Result)> Run(int padding)
        {
            await using var f = new McpFixture(new WebClock()); await f.Initialize(); await f.Mcp.ConnectAsync(default);
            var http = new GeminiHandler(); http.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(http.Calls == 1
                ? PlanSdkTests.Batch((WebFixtures.FetchName, new { url = WebFixtures.Url })) : GeminiFixtures.Text()));
            await using var client = new GeminiClient(f.Session, http);
            var result = await new AgentRunner(client, new ToolRegistry([], f.Session.Policy, f.Execution), f.Session.Policy, new string('x', padding), new(), _ => { }).RunAsync([LlmMessage.FromUserText("q")], default);
            return (http.Bodies.Last().Length, http.Calls, result);
        }
        var baseline = await Run(1); var padding = 262144 - baseline.Bytes + 1;
        var accepted = await Run(padding); var rejected = await Run(padding + 1);
        Assert.AreEqual(262144, accepted.Bytes); Assert.AreEqual(2, accepted.Calls); Assert.AreEqual(AgentTurnStatus.Completed, accepted.Result.Status);
        Assert.AreEqual(1, rejected.Calls); Assert.AreEqual(LlmErrorCode.InputTooLarge, rejected.Result.ModelErrorCode);
    }

    [TestMethod]
    public async Task WEBAC023_FormalChatAssemblesSourceAndVersionGuidanceWithoutExtraTools()
    {
        var f = new ChatFixture("public question"); using var session = GeminiFixtures.Session(); f.Factory.Sessions.Add(session); f.Client.Session = session;
        var app = new ChatApplication(f.Interaction, f.Factory, f.Context, f.Usage, ApplicationInstructions.Value, new TestWorkspaceFilesFactory());
        Assert.AreEqual(0, await app.RunAsync(session, default));
        var instruction = f.Client.Requests.Single().SystemInstruction;
        foreach (var term in new[] { "搜尋片段", "Learn服務擷取回應", "requested_view", "未擷取", "未確認", "推論" })
            Assert.IsTrue(instruction.Contains(term, StringComparison.Ordinal));
        Assert.AreEqual(12, f.Client.Requests.Single().Tools.Count);
    }
}