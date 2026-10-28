using System.Net;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Mcp;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpCapacityTests
{
    [TestMethod]
    public async Task MCPAC013_InvalidUtf8SseNeverBecomesReplacementDecodedSuccess()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var handler = f.Handlers[0]; handler.Sse = true;
        handler.MutateResponse = response =>
        {
            if (handler.Methods.Last() != "tools/call") return;
            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            bytes[Array.IndexOf(bytes, (byte)'P')] = 255;
            response.Content = new ByteArrayContent(bytes); response.Content.Headers.ContentType = new("text/event-stream");
        };
        var result = await f.Invoke();
        Assert.AreEqual("mcp_invalid_response", result.ErrorCode); Assert.IsFalse(result.McpOutcome!.SafeResultAccepted);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MCPAC002_JsonAndSseWorkWithoutServerIdentity(bool sse)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture { Sse = sse };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler);
        await client.DiscoverAsync(default); Assert.AreEqual(3, (await client.ListToolsAsync(default)).Tools.Count);
    }

    [TestMethod]
    [DataRow(262144, true)]
    [DataRow(262145, false)]
    public async Task MCPAC006_RawBodyCountsEveryByteWithoutTrustingContentLength(int size, bool accepted)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture();
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler);
        await client.DiscoverAsync(default); handler.RawResponse = json => json.PadRight(size);
        if (accepted) Assert.AreEqual(262144L, (await client.ListToolsAsync(default)).Bytes);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.ListToolsAsync(default));
    }

    [TestMethod]
    [DataRow(2048, true)]
    [DataRow(2049, false)]
    public async Task MCPAC014_DescriptionLengthIsIndependentOfResponseCapacity(int length, bool accepted)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture { Override = (m, _) => m == "tools/list" ? McpHttpFixture.Catalog.Replace("Search docs", new string('x', length), StringComparison.Ordinal) : null };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler); await client.DiscoverAsync(default);
        if (accepted) Assert.AreEqual(2048, (await client.ListToolsAsync(default)).Tools[0].Description.Length);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.ListToolsAsync(default));
    }

    [TestMethod]
    [DataRow(4, true)]
    [DataRow(5, false)]
    public async Task MCPAC006_PaginationHasFinitePageBudget(int pages, bool accepted)
    {
        using var session = GeminiFixtures.Session(); var page = 0; var handler = new McpHttpFixture();
        handler.Override = (m, _) => m != "tools/list" ? null : ++page == 1 ? McpHttpFixture.Catalog[..^1] + ",\"nextCursor\":\"p2\"}" :
            "{\"tools\":[],\"ttlMs\":0,\"cacheScope\":\"public\"" + (page < pages ? ",\"nextCursor\":\"p" + (page + 1) + "\"}" : "}");
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler); await client.DiscoverAsync(default);
        if (accepted) Assert.AreEqual(3, (await client.ListToolsAsync(default)).Tools.Count);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.ListToolsAsync(default));
        Assert.AreEqual(4, handler.Methods.Count(m => m == "tools/list"));
    }

    [TestMethod]
    [DataRow(32, true)]
    [DataRow(33, false)]
    public async Task MCPAC006_TotalToolCountIncludesExcludedTools(int count, bool accepted)
    {
        using var document = JsonDocument.Parse(McpHttpFixture.Catalog);
        var tools = document.RootElement.GetProperty("tools").EnumerateArray().Take(3).Select(t => t.GetRawText()).ToList();
        tools.AddRange(Enumerable.Range(0, count - 3).Select(i => "{\"name\":\"excluded_" + i + "\",\"inputSchema\":{\"type\":\"object\"}}"));
        var catalog = "{\"tools\":[" + string.Join(',', tools) + "]}";
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture { Override = (m, _) => m == "tools/list" ? catalog : null };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler); await client.DiscoverAsync(default);
        if (accepted) Assert.AreEqual(29, (await client.ListToolsAsync(default)).Excluded);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.ListToolsAsync(default));
    }

    [TestMethod]
    [DataRow(64, true)]
    [DataRow(65, false)]
    public async Task MCPAC006_ResponseHeaderCountIncludesContentHeaders(int count, bool accepted)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture();
        handler.MutateResponse = response =>
        {
            var existing = response.Headers.Concat(response.Content.Headers).Sum(h => h.Value.Count());
            for (var i = existing; i < count; i++) response.Headers.TryAddWithoutValidation("X-Field-" + i, "public");
        };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler);
        if (accepted) await client.DiscoverAsync(default);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.DiscoverAsync(default));
        Assert.AreEqual(1, handler.Methods.Count);
    }

    [TestMethod]
    [DataRow(16384, true)]
    [DataRow(16385, false)]
    public async Task MCPAC006_ResponseHeaderByteLimitIncludesNamesAndSeparators(int size, bool accepted)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture();
        handler.MutateResponse = response =>
        {
            var used = response.Headers.Concat(response.Content.Headers).Sum(h => h.Value.Sum(v => Encoding.UTF8.GetByteCount(h.Key) + Encoding.UTF8.GetByteCount(v) + 4));
            response.Headers.TryAddWithoutValidation("X-Pad", new string('x', size - used - "X-Pad".Length - 4));
        };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler);
        if (accepted) await client.DiscoverAsync(default);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.DiscoverAsync(default));
    }

    [TestMethod]
    [DataRow(262144, true)]
    [DataRow(262145, false)]
    public async Task MCPAC006_SseCommentsCountTowardsCompleteStreamLimit(int size, bool accepted)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture { Sse = true };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler); await client.DiscoverAsync(default);
        handler.MutateResponse = response =>
        {
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            response.Content = new StringContent(":" + new string('x', size - Encoding.UTF8.GetByteCount(body) - 2) + "\n" + body, Encoding.UTF8, "text/event-stream");
        };
        if (accepted) Assert.AreEqual(262144L, (await client.ListToolsAsync(default)).Bytes);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.ListToolsAsync(default));
    }

    [TestMethod]
    [DataRow(262144, true)]
    [DataRow(262145, false)]
    public async Task MCPAC006_CatalogPagesShareOneCumulativeByteLimit(int size, bool accepted)
    {
        using var session = GeminiFixtures.Session(); var page = 0; var handler = new McpHttpFixture();
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler); await client.DiscoverAsync(default);
        handler.Override = (m, _) => m != "tools/list" ? null : ++page == 1 ? McpHttpFixture.Catalog[..^1] + ",\"nextCursor\":\"second\"}" : "{\"tools\":[]}";
        handler.RawResponse = json => json.PadRight(page == 1 ? 131072 : size - 131072);
        if (accepted) Assert.AreEqual(262144L, (await client.ListToolsAsync(default)).Bytes);
        else await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.ListToolsAsync(default));
        Assert.AreEqual(2, handler.Methods.Count(m => m == "tools/list"));
    }

    [TestMethod]
    [DataRow(401)]
    [DataRow(403)]
    [DataRow(407)]
    [DataRow(302)]
    public async Task MCPAC003_AuthAndRedirectResponsesNeverCauseFollowupRequests(int status)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture { MutateResponse = r => { r.StatusCode = (HttpStatusCode)status; r.Headers.Location = new("https://example.test/login"); } };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler);
        await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.DiscoverAsync(default)); Assert.AreEqual(1, handler.Methods.Count);
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("br")]
    public async Task MCPAC006_CompressionIsRejectedBeforeSdkCanReadBody(string encoding)
    {
        using var session = GeminiFixtures.Session(); var handler = new McpHttpFixture { MutateResponse = r => r.Content.Headers.ContentEncoding.Add(encoding) };
        await using var client = new McpReadClient(new McpContentGuard(session.Policy, []), handler);
        await Assert.ThrowsExactlyAsync<McpFailure>(async () => await client.DiscoverAsync(default)); Assert.AreEqual(1, handler.Methods.Count);
    }

    [TestMethod]
    [DataRow("{\"content\":[{\"type\":\"image\",\"data\":\"fake\",\"mimeType\":\"image/png\"}]}")]
    [DataRow("{\"content\":[{\"type\":\"text\",\"text\":\"safe\"},{\"type\":\"resource_link\",\"uri\":\"https://example.test\",\"name\":\"link\"}]}")]
    [DataRow("{\"content\":[]}")]
    [DataRow("{\"isError\":true,\"content\":[{\"type\":\"text\",\"text\":\"RAW_REMOTE_ERROR\"}]}")]
    [DataRow("{\"content\":[{\"type\":\"text\",\"text\":\"a\",\"text\":\"b\"}]}")]
    public async Task MCPAC013_UnsupportedAndAmbiguousResultsNeverPartiallySucceed(string result)
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default); f.Handlers[0].ToolResult = result;
        var executed = await f.Invoke(); var outcome = executed.McpOutcome!;
        Assert.AreNotEqual("none", executed.ErrorCode); Assert.IsNull(outcome.Response); Assert.IsFalse(outcome.SafeResultAccepted);
        Assert.AreEqual(1, f.Handlers[0].Methods.Count(m => m == "tools/call"));
    }

    [TestMethod]
    [DataRow(32768, true)]
    [DataRow(32769, false)]
    public async Task MCPAC014_SuccessDataLimitIncludesAllFixedFields(int bytes, bool accepted)
    {
        const string skeleton = "{\"source\":\"external_mcp\",\"server_id\":\"learn\",\"remote_tool\":\"microsoft_docs_search\",\"catalog_revision\":1,\"content\":[{\"type\":\"text\",\"text\":\"\"}],\"structured_content\":null}";
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        f.Handlers[0].ToolResult = "{\"content\":[{\"type\":\"text\",\"text\":\"" + new string('x', bytes - Encoding.UTF8.GetByteCount(skeleton)) + "\"}]}";
        var executed = await f.Invoke(); var outcome = executed.McpOutcome!;
        Assert.AreEqual(accepted, outcome.SafeResultAccepted);
        if (accepted) Assert.AreEqual(32768, Encoding.UTF8.GetByteCount(outcome.Response!.Value.GetProperty("data").GetRawText()));
        else Assert.AreEqual("mcp_limit", outcome.ErrorCode);
    }
}