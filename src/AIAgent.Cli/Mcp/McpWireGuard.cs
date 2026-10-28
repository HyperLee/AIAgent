using System.Net;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Mcp;

/// <summary>SDK 外側的封閉傳輸閘門；任何未明確預留的要求都不接觸網路。</summary>
internal sealed class McpWireGuard(IMcpContentGuard guard, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    private string? _method;
    private string? _name;
    private JsonElement? _arguments;
    private McpCallPermit? _permit;
    private CancellationToken _token;
    private int _remaining;
    internal JsonElement Result { get; private set; }
    internal long Bytes { get; private set; }
    internal long TotalResponseBytes { get; private set; }
    internal bool CleanupFailed { get; private set; }
    internal McpTransmission Transmission { get; private set; }
    internal void Begin(string method, CancellationToken token, McpCallPermit? permit = null)
    {
        _method = method; _name = permit?.Tool.RemoteName; _arguments = permit?.Arguments; _permit = permit; _token = token;
        Result = default; Bytes = 0; Transmission = McpTransmission.NotSent; Interlocked.Exchange(ref _remaining, 1);
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _token);
        var token = linked.Token; token.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _remaining, 0) != 1) throw new McpFailure("mcp_protocol_unsupported");
        if (request.Method != HttpMethod.Post || request.RequestUri?.OriginalString != McpProfile.Endpoint || request.RequestUri.Port != 443)
            throw new McpFailure("mcp_destination_rejected");
        foreach (var header in request.Headers)
            if (header.Key is not ("Accept" or "MCP-Protocol-Version" or "Mcp-Method" or "Mcp-Name") ||
                !guard.IsSafe(header.Key + string.Concat(header.Value))) throw new McpFailure("mcp_destination_rejected");
        CheckHeaders(request.Headers.Concat(request.Content!.Headers));
        var outgoing = McpJson.Parse(await ReadBoundedAsync(await request.Content!.ReadAsStreamAsync(token), token), guard);
        if (outgoing.GetProperty("method").GetString() != _method || _method is not ("server/discover" or "tools/list" or "tools/call"))
            throw new McpFailure("mcp_protocol_unsupported");
        var parameters = outgoing.GetProperty("params"); var meta = parameters.GetProperty("_meta");
        if (meta.GetProperty("io.modelcontextprotocol/protocolVersion").GetString() != McpProfile.ProtocolVersion ||
            meta.GetProperty("io.modelcontextprotocol/clientCapabilities").EnumerateObject().Any() ||
            meta.GetProperty("io.modelcontextprotocol/clientInfo").GetProperty("name").GetString() != "AIAgent" ||
            request.Headers.GetValues("MCP-Protocol-Version").SingleOrDefault() != McpProfile.ProtocolVersion ||
            request.Headers.GetValues("Mcp-Method").SingleOrDefault() != _method) throw new McpFailure("mcp_protocol_unsupported");
        if (meta.EnumerateObject().Any(p => p.Name is not ("io.modelcontextprotocol/protocolVersion" or "io.modelcontextprotocol/clientInfo" or "io.modelcontextprotocol/clientCapabilities")))
            throw new McpFailure("mcp_protocol_unsupported");
        if (_method == "tools/call")
        {
            if (_permit is null || !_permit.IsCurrent || parameters.GetProperty("name").GetString() != _name ||
                !JsonElement.DeepEquals(parameters.GetProperty("arguments"), _arguments!.Value) ||
                request.Headers.GetValues("Mcp-Name").SingleOrDefault() != _name ||
                parameters.EnumerateObject().Any(p => p.Name is not ("name" or "arguments" or "_meta"))) throw new McpFailure("mcp_changed");
        }
        else if (parameters.EnumerateObject().Any(p => p.Name != "_meta" && (_method != "tools/list" || p.Name != "cursor")))
            throw new McpFailure("mcp_protocol_unsupported");
        token.ThrowIfCancellationRequested();
        if (_method == "tools/call") Transmission = McpTransmission.SentUnconfirmed;
        var response = await base.SendAsync(request, token);
        try
        {
            var headers = response.Headers.Concat(response.Content.Headers).ToArray();
            CheckHeaders(headers);
            McpJson.Limit(headers.Sum(h => h.Value.Count()), 64);
            McpJson.Limit(headers.Sum(h => h.Value.Sum(v => Encoding.UTF8.GetByteCount(h.Key) + Encoding.UTF8.GetByteCount(v) + 4)), 16384);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.ProxyAuthenticationRequired)
                throw new McpFailure("mcp_auth_unsupported");
            if ((int)response.StatusCode is >= 300 and < 400) throw new McpFailure("mcp_destination_rejected");
            if (response.Content.Headers.ContentEncoding.Any(e => !string.Equals(e, "identity", StringComparison.OrdinalIgnoreCase))) throw new McpFailure("mcp_result_unsupported");
            var body = await ReadBoundedAsync(await response.Content.ReadAsStreamAsync(token), token, true); Bytes = body.LongLength;
            _ = McpJson.Utf8.GetString(body);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var final = 0;
            void Observe(byte[] message)
            {
                var raw = McpJson.Parse(message, guard);
                if (raw.ValueKind != JsonValueKind.Object || raw.GetProperty("jsonrpc").GetString() != "2.0") throw new McpFailure("mcp_invalid_response");
                if (raw.TryGetProperty("method", out _))
                {
                    if (raw.TryGetProperty("id", out _) || raw.TryGetProperty("result", out _) || raw.TryGetProperty("error", out _)) throw new McpFailure("mcp_result_unsupported");
                    return;
                }
                if (!raw.TryGetProperty("id", out var id) || !JsonElement.DeepEquals(id, outgoing.GetProperty("id")) || ++final != 1)
                    throw new McpFailure("mcp_invalid_response");
                if (_method == "tools/call") Transmission = McpTransmission.ResponseReceived;
                if (raw.TryGetProperty("error", out _)) throw new McpFailure(_method == "server/discover" ? "mcp_protocol_unsupported" : "mcp_remote_error");
                if (!raw.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object) throw new McpFailure("mcp_invalid_response");
                if (result.TryGetProperty("resultType", out var kind) && (kind.ValueKind != JsonValueKind.String || kind.GetString() != "complete"))
                    throw new McpFailure("mcp_result_unsupported");
                Result = result.Clone();
            }
            if (mediaType == "application/json") Observe(body);
            else if (mediaType == "text/event-stream")
            {
                using var stream = new MemoryStream(body, false);
                await foreach (var item in SseParser.Create(stream).EnumerateAsync(token))
                    if (item.Data.Length != 0) Observe(McpJson.Utf8.GetBytes(item.Data));
            }
            else throw new McpFailure("mcp_invalid_response");
            if (final != 1 || !response.IsSuccessStatusCode) throw new McpFailure("mcp_invalid_response");
            var replacement = new ByteArrayContent(body);
            foreach (var header in response.Content.Headers) replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
            try { response.Content.Dispose(); } catch { CleanupFailed = true; throw new McpFailure("unavailable"); }
            response.Content = replacement;
            token.ThrowIfCancellationRequested(); return response;
        }
        catch { try { response.Dispose(); } catch { CleanupFailed = true; throw new McpFailure("unavailable"); } throw; }
    }
    private void CheckHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        var values = new StringBuilder(); var all = new StringBuilder();
        foreach (var header in headers)
        {
            if (!guard.IsSafe(header.Key)) throw new McpFailure("sensitive_content");
            all.Append(header.Key);
            foreach (var value in header.Value)
            { if (!guard.IsSafe(value)) throw new McpFailure("sensitive_content"); values.Append(value); all.Append(value); }
        }
        if (!guard.IsSafe(values.ToString()) || !guard.IsSafe(all.ToString())) throw new McpFailure("sensitive_content");
    }
    private async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken token, bool response = false)
    {
        using var output = new MemoryStream(); var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, McpProfile.MaxWireBytes + 1 - checked((int)output.Length))), token);
            if (read == 0) break;
            if (response) { Bytes = checked(Bytes + read); TotalResponseBytes = checked(TotalResponseBytes + read); }
            output.Write(buffer, 0, read); McpJson.Limit(output.Length, McpProfile.MaxWireBytes);
        }
        return output.ToArray();
    }
}