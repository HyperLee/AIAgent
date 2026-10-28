using System.Net;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Mcp;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpTransportTests
{
    [TestMethod]
    public async Task MCPAC011_ForgedPermitWithoutCoordinatorApprovalNeverReachesTransport()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var forged = new McpCallPermit(f.Mcp, f.Mcp.GetTools()[0], GeminiFixtures.Element("{\"query\":\"public\"}"), 1);
        var result = await f.Clients[0].CallAsync(forged, default);
        Assert.AreEqual("mcp_changed", result.ErrorCode); Assert.AreEqual(0, f.Handlers[0].Methods.Count(m => m == "tools/call"));
    }

    [TestMethod]
    public async Task MCPAC024_ResponseHeadersPassSensitiveBoundaryBeforeSdkAdoptsResult()
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture { MutateResponse = r => r.Headers.Add("X-Remote", GeminiFixtures.Key) };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler);
        await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.DiscoverAsync(default));
        Assert.AreEqual(1, handler.Methods.Count);
    }

    [TestMethod]
    public async Task MCPAC002_RealSdkUsesOnlyModernDiscoveryAndTwoBoundedTools()
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture();
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, ["SYNTHETIC_SECRET_INSTRUCTION"]), handler);
        await client.DiscoverAsync(default);
        var catalog = await client.ListToolsAsync(default);
        Assert.AreEqual(2, catalog.Tools.Count); Assert.AreEqual(1, catalog.Excluded);
        CollectionAssert.AreEqual(new[] { "server/discover", "tools/list" }, handler.Methods.ToArray());
        Assert.IsTrue(handler.ValidMetadata); Assert.AreEqual("mcp__learn__microsoft_docs_search", catalog.Tools[0].LocalName);
    }

    [TestMethod]
    public async Task MCPAC004_InputRequiredNeverTriggersSdkContinuationOrClientInput()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        f.Handlers[0].ToolResult = "{\"resultType\":\"input_required\",\"requestState\":\"opaque\"}";
        McpCallPermit? issued = null;
        f.Observe = method =>
        {
            if (method == "tools/call") issued = (McpCallPermit?)typeof(McpCoordinator).GetField("_issuedPermit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Mcp);
        };
        var result = await f.Invoke();
        Assert.AreNotEqual("none", result.ErrorCode); Assert.IsFalse(result.McpOutcome!.SafeResultAccepted);
        Assert.AreEqual(1, f.Handlers[0].Methods.Count(m => m == "tools/call"));
        Assert.IsNotNull(issued);
        var repeated = await f.Clients[0].CallAsync(issued, default);
        Assert.AreNotEqual("none", repeated.ErrorCode); Assert.AreEqual(1, f.Handlers[0].Methods.Count(m => m == "tools/call"));
    }

    [TestMethod]
    [DataRow("wrong_id")]
    [DataRow("duplicate_id")]
    [DataRow("two_final")]
    [DataRow("remote_request")]
    public async Task MCPAC013_AmbiguousRawSseNeverReachesSdkAsSuccess(string kind)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var handler = f.Handlers[0]; handler.Sse = true;
        handler.RawResponse = raw =>
        {
            if (handler.Methods.Last() != "tools/call") return raw;
            using var document = JsonDocument.Parse(raw); var id = document.RootElement.GetProperty("id").GetRawText();
            return kind switch
            {
                "wrong_id" => raw.Replace("\"id\":" + id, "\"id\":\"foreign\"", StringComparison.Ordinal),
                "duplicate_id" => raw[..^1] + ",\"id\":" + id + "}",
                "two_final" => raw + "\n\ndata: " + raw,
                _ => "{\"jsonrpc\":\"2.0\",\"id\":42,\"method\":\"sampling/createMessage\",\"params\":{}}"
            };
        };
        var result = await f.Invoke();
        Assert.AreNotEqual("none", result.ErrorCode); Assert.IsFalse(result.McpOutcome!.SafeResultAccepted);
        Assert.AreEqual(1, handler.Methods.Count(m => m == "tools/call"));
    }

    [TestMethod]
    public async Task MCPAC013_TextAndStructuredContentProduceOnlyExternalEnvelope()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var result = await f.Invoke(); var outcome = result.McpOutcome!;
        Assert.AreEqual("none", outcome.ErrorCode); Assert.IsTrue(outcome.SafeResultAccepted);
        Assert.AreEqual(McpTransmission.ResponseReceived, outcome.Transmission);
        var data = result.Response!.Value.GetProperty("data");
        Assert.AreEqual("external_mcp", data.GetProperty("source").GetString());
        Assert.AreEqual(1L, data.GetProperty("catalog_revision").GetInt64());
        Assert.IsFalse(data.TryGetProperty("path", out _)); Assert.IsFalse(data.TryGetProperty("Verified", out _));
    }


}

internal sealed class McpHttpFixture : HttpMessageHandler
{
    internal List<string> Methods { get; } = [];
    internal bool ValidMetadata { get; private set; } = true;
    internal string ToolResult { get; set; } = "{\"resultType\":\"complete\",\"content\":[{\"type\":\"text\",\"text\":\"Public result\"}],\"structuredContent\":{\"url\":\"https://example.test/reference\"}}";
    internal Func<string, int, string?>? Override { get; set; }
    internal bool Sse { get; set; }
    internal Func<string, string>? RawResponse { get; set; }
    internal Action<HttpResponseMessage>? MutateResponse { get; set; }
    internal bool Closed { get; private set; }
    protected override void Dispose(bool disposing) { Closed = true; base.Dispose(disposing); }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.AreEqual(HttpMethod.Post, request.Method); Assert.AreEqual("https://learn.microsoft.com/api/mcp", request.RequestUri!.AbsoluteUri);
        Assert.IsNull(request.Headers.Authorization); Assert.IsFalse(request.Headers.Contains("Cookie")); Assert.IsFalse(request.Headers.Contains("Mcp-Session-Id"));
        using var body = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
        var root = body.RootElement; var method = root.GetProperty("method").GetString()!; Methods.Add(method);
        var meta = root.GetProperty("params").GetProperty("_meta");
        ValidMetadata &= meta.GetProperty("io.modelcontextprotocol/protocolVersion").GetString() == "2026-07-28" &&
            meta.GetProperty("io.modelcontextprotocol/clientCapabilities").EnumerateObject().Count() == 0 &&
            request.Headers.GetValues("MCP-Protocol-Version").Single() == "2026-07-28";
        var value = Override?.Invoke(method, Methods.Count) ?? (method switch
        {
            "server/discover" => "{\"resultType\":\"complete\",\"supportedVersions\":[\"2026-07-28\"],\"capabilities\":{\"tools\":{}},\"ttlMs\":0,\"cacheScope\":\"public\"}",
            "tools/list" => Catalog,
            _ => ToolResult
        });
        var json = "{\"jsonrpc\":\"2.0\",\"id\":" + root.GetProperty("id").GetRawText() + ",\"result\":" + value + "}";
        json = RawResponse?.Invoke(json) ?? json;
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Sse ? "data: " + json + "\n\n" : json, Encoding.UTF8, Sse ? "text/event-stream" : "application/json") };
        MutateResponse?.Invoke(response); return response;
    }
    internal const string Catalog = "{\"resultType\":\"complete\",\"tools\":[{\"name\":\"microsoft_docs_search\",\"description\":\"Search docs\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\",\"minLength\":1}},\"required\":[\"query\"]}},{\"name\":\"microsoft_code_sample_search\",\"description\":\"Search code\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}},{\"name\":\"microsoft_docs_fetch\",\"inputSchema\":{\"type\":\"object\"}}],\"ttlMs\":0,\"cacheScope\":\"public\"}";
}