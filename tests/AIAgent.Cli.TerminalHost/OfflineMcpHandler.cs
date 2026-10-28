using System.Net;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;

// Synthetic protocol fixtures for offline PTY only; no sockets or credentials.
internal sealed class OfflineMcpHandler(string scenario) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
        var root = body.RootElement; var method = root.GetProperty("method").GetString();
        if (method == "tools/list") Console.WriteLine("OFFLINE_MCP_LIST");
        if (method == "tools/call")
        {
            Console.WriteLine("OFFLINE_MCP_CALL_SENT");
            if (scenario.EndsWith("call-gated", StringComparison.Ordinal))
            {
                Console.WriteLine("OFFLINE_MCP_CALL_WAITING");
                using var observed = cancellationToken.Register(() => Console.WriteLine("OFFLINE_MCP_CANCEL_OBSERVED"));
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                finally { Console.WriteLine("OFFLINE_MCP_CALL_CLOSED"); }
            }
        }
        var description = File.Exists("mcp-stale") ? "Changed fixture" : "公開文件搜尋";
        var schema = new { type = "object", properties = new { query = new { type = "string" } }, required = new[] { "query" }, additionalProperties = false };
        var fetchSchema = new { type = "object", properties = new { url = new { type = "string" } }, required = new[] { "url" }, additionalProperties = false };
        var result = method switch
        {
            "server/discover" => JsonSerializer.Serialize(new { resultType = "complete", supportedVersions = new[] { "2026-07-28" }, capabilities = new { tools = new { } }, ttlMs = 0, cacheScope = "public" }),
            "tools/list" => JsonSerializer.Serialize(new
            {
                resultType = "complete",
                tools = new[]
            {
                new { name = McpProfile.DocsTool, description, inputSchema = (object)schema },
                new { name = McpProfile.CodeTool, description = "公開範例搜尋", inputSchema = (object)schema },
                new { name = McpProfile.FetchTool, description = "文件擷取", inputSchema = (object)fetchSchema }
            },
                ttlMs = 0,
                cacheScope = "public"
            }),
            _ => "{\"resultType\":\"complete\",\"content\":[{\"type\":\"text\",\"text\":\"Synthetic public result\"}]}"
        };
        return new(HttpStatusCode.OK) { Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":" + root.GetProperty("id").GetRawText() + ",\"result\":" + result + "}", Encoding.UTF8, "application/json") };
    }
}