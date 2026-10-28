using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WebUrlTests
{
    [TestMethod]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string")]
    [DataRow("https://learn.microsoft.com/zh-tw/aspnet/core/fundamentals?preserve-view=true&view=aspnetcore-10.0")]
    [DataRow("https://learn.microsoft.com/en-us/DotNet/API/System.String?view=net-10.0&preserve-view=true")]
    [DataRow("https://learn.microsoft.com/aa-bb/area/article")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/article?view=unknown_version-1.2")]
    public async Task WEBAC004_AdmittedUrlIsNeverRewrittenBetweenModelPreviewAndWire(string url)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var result = await WebFixtures.Fetch(f, url);
        Assert.AreEqual(ToolStatus.Success, result.Status);
        Assert.AreEqual(url, f.Handlers[0].Calls.Single().GetProperty("arguments").GetProperty("url").GetString());
        StringAssert.Contains(f.Previews.Single(), url);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" https://learn.microsoft.com/en-us/dotnet/api/system.string")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string ")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string#methods")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string%20")]
    [DataRow("https://LEARN.microsoft.com/en-us/dotnet/article")]
    [DataRow("HTTPS://learn.microsoft.com/en-us/dotnet/article")]
    [DataRow("https://learn.microsoft.com:443/en-us/dotnet/article")]
    [DataRow("https://learn.microsoft.com./en-us/dotnet/article")]
    [DataRow("https://learn.microsoft.com.evil.test/en-us/dotnet/article")]
    [DataRow("https://user@learn.microsoft.com/en-us/dotnet/article")]
    [DataRow("http://learn.microsoft.com/en-us/dotnet/article")]
    [DataRow("//learn.microsoft.com/en-us/dotnet/article")]
    [DataRow("/en-us/dotnet/article")]
    [DataRow("https://127.0.0.1/en-us/dotnet/article")]
    [DataRow("https://learn.microsoft.com/api/mcp")]
    [DataRow("https://learn.microsoft.com/en-US/dotnet/article")]
    [DataRow("https://learn.microsoft.com/en/dotnet/article")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet//article")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/../article")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/./article")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/文章")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/a\\b")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/a\tb")]
    [DataRow("https://learn.microsoft.com/en-us/API/article")]
    [DataRow("https://learn.microsoft.com/en-us/Answers/article")]
    [DataRow("https://learn.microsoft.com/en-us/users/article")]
    [DataRow("https://learn.microsoft.com/en-us/search/article")]
    [DataRow("https://learn.microsoft.com/en-us/profile/article")]
    [DataRow("https://learn.microsoft.com/en-us/account/article")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?View=x")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a&view=b")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a&&preserve-view=true")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a&")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a=b")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a;preserve-view=true")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?preserve-view=true")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a&preserve-view=false")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a&Preserve-view=true")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?unknown=x")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a&preserve-view=true&preserve-view=true")]
    [DataRow("https://learn.microsoft.com/en-us/dotnet/api/system.string?view=a?view=b")]
    public async Task WEBAC005_RejectedUrlHasNoSourceOrNetworkWorkAndDoesNotRevokeCatalog(string url)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var before = f.Handlers[0].Methods.Count; var stamp = f.Mcp.Capture();
        var result = await WebFixtures.Fetch(f, url);
        Assert.AreEqual("web_url_rejected", result.ErrorCode); Assert.IsTrue(result.StopAfterOperation);
        Assert.AreEqual(McpTransmission.NotSent, result.McpOutcome!.Transmission);
        Assert.IsFalse(result.McpOutcome.SafeResultAccepted); Assert.IsTrue(f.Mcp.IsCurrent(stamp));
        Assert.AreEqual(before, f.Handlers[0].Methods.Count); Assert.AreEqual(0, f.Previews.Count);
        Assert.IsFalse(result.Response!.Value.TryGetProperty("data", out _));
        Assert.AreEqual(ToolStatus.Success, (await f.Invoke()).Status);
        Assert.AreEqual(ToolStatus.Success, (await f.Invoke()).Status);
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, true)]
    [DataRow(64, true)]
    [DataRow(65, false)]
    public async Task WEBAC006_ViewHasIndependentAsciiLengthLimit(int length, bool accepted)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        Assert.AreEqual(accepted ? "none" : "web_url_rejected", (await WebFixtures.Fetch(f, WebFixtures.Url + "?view=" + new string('x', length))).ErrorCode);
    }

    [TestMethod]
    [DataRow(1024, true)]
    [DataRow(1025, false)]
    public async Task WEBAC005_UrlLimitIsCheckedLocallyBeforeSchemaOrMetadata(int length, bool accepted)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        Assert.AreEqual(accepted ? "none" : "web_url_rejected", (await WebFixtures.Fetch(f, WebFixtures.Url + new string('x', length - WebFixtures.Url.Length))).ErrorCode);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"url\":null}")]
    [DataRow("{\"url\":3}")]
    [DataRow("{\"URL\":\"x\"}")]
    [DataRow("{\"url\":\"x\",\"extra\":\"x\"}")]
    [DataRow("{\"url\":\"x\",\"url\":\"y\"}")]
    public async Task WEBAC015_InvalidArgumentShapeRetainsExistingError(string json)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var count = f.Handlers[0].Methods.Count;
        var result = await f.Mcp.InvokeAsync(WebFixtures.FetchName, GeminiFixtures.Element(json), f.Mcp.Capture(), default);
        Assert.AreEqual("invalid_arguments", result.ErrorCode); Assert.AreEqual(count, f.Handlers[0].Methods.Count);
    }

    [TestMethod]
    public async Task WEBAC007_ProtectedDecodedUrlTakesPrecedenceOverOrdinaryUrlRejection()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var result = await WebFixtures.Fetch(f, "https://other.test/SYNTHETIC_PROTECTED_INSTRUCTION");
        Assert.AreEqual("sensitive_arguments", result.ErrorCode); Assert.IsTrue(f.Mcp.RequiresContextReset);
        Assert.AreEqual(0, f.Previews.Count); Assert.AreEqual(0, f.Handlers[0].Calls.Count);
    }

    [TestMethod]
    public async Task WEBAC009_FirstRejectedFetchPrecedesWorkspaceRevalidation()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var client = new ScriptClient();
        client.Respond = (_, _, _) =>
        {
            File.WriteAllText(f.Root + "/AGENTS.md", "changed public rule");
            return Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, "fetch-id", WebFixtures.FetchName, JsonSerializer.SerializeToElement(new { url = "https://other.test/article" }))));
        };
        var registry = new ToolRegistry([], f.Session.Policy, f.Execution);
        var result = await new AgentRunner(client, registry, f.Session.Policy, "synthetic instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("question")], default);
        Assert.AreEqual(AgentStopReason.McpStopped, result.StopReason);
        Assert.AreEqual("web_url_rejected", result.Operations.Single().ErrorCode);
        Assert.AreEqual(McpStatus.Ready, f.Mcp.Capture().Status); Assert.AreEqual(1, client.Requests.Count);
        Assert.AreEqual(0, f.Previews.Count);
    }
}