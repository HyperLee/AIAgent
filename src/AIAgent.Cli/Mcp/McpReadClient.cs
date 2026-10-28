using System.Net;
using System.Text.Json;

using AIAgent.Cli.Core;

using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AIAgent.Cli.Mcp;

internal sealed class McpReadClient : IMcpReadClient
{
    private readonly IMcpContentGuard _guard;
    private readonly McpWireGuard _wire;
    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private McpClient? _client;
    private HttpClientTransport? _transport;
    public long ReceivedBytes => _wire.TotalResponseBytes;
    internal McpReadClient(IMcpContentGuard guard, HttpMessageHandler? handler = null, TimeProvider? clock = null)
    {
        _guard = guard; _clock = clock ?? TimeProvider.System;
        _wire = new(guard, handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            Credentials = null,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            MaxResponseHeadersLength = 16,
            ActivityHeadersPropagator = null
        });
        _http = new(_wire) { Timeout = Timeout.InfiniteTimeSpan };
    }
    public async Task DiscoverAsync(CancellationToken token)
    {
        if (_client is not null)
        {
            _wire.Begin("server/discover", token);
            await _client.SendRequestAsync(new JsonRpcRequest { Method = "server/discover" }, CancellationToken.None);
        }
        else
        {
            _wire.Begin("server/discover", token);
            _transport = new(new HttpClientTransportOptions
            {
                Endpoint = new(McpProfile.Endpoint),
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
                OwnsSession = false,
                MaxReconnectionAttempts = 0,
                OAuth = null,
                ConnectionTimeout = TimeSpan.FromSeconds(10)
            }, _http, NullLoggerFactory.Instance);
            _client = await McpClient.CreateAsync(_transport, new McpClientOptions
            {
                ProtocolVersion = McpProfile.ProtocolVersion,
                ClientInfo = new() { Name = "AIAgent", Version = typeof(McpReadClient).Assembly.GetName().Version?.ToString() ?? "1.0.0" },
                Capabilities = new(),
                InitializationTimeout = TimeSpan.FromSeconds(20),
                DiscoverProbeTimeout = TimeSpan.FromSeconds(20),
                TimeProvider = _clock
            }, NullLoggerFactory.Instance, token);
        }
        var result = _wire.Result;
        if (_client.NegotiatedProtocolVersion != McpProfile.ProtocolVersion ||
            !result.TryGetProperty("supportedVersions", out var versions) || versions.ValueKind != JsonValueKind.Array ||
            !versions.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.String && v.GetString() == McpProfile.ProtocolVersion) ||
            !result.TryGetProperty("capabilities", out var capabilities) || !capabilities.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Object)
            throw new McpFailure("mcp_protocol_unsupported");
    }
    public async Task<McpCatalogObservation> ListToolsAsync(CancellationToken token)
    {
        if (_client is null) throw new McpFailure("mcp_not_connected");
        var names = new HashSet<string>(StringComparer.Ordinal); var cursors = new HashSet<string>(StringComparer.Ordinal);
        var accepted = new List<McpToolDescriptor>(); string? cursor = null; long bytes = 0; var count = 0;
        for (var page = 0; page < 4; page++)
        {
            _wire.Begin("tools/list", token);
            _ = await _client.ListToolsAsync(new ListToolsRequestParams { Cursor = cursor }, CancellationToken.None);
            bytes = checked(bytes + _wire.Bytes); McpJson.Limit(bytes, McpProfile.MaxWireBytes);
            var root = _wire.Result;
            if (!root.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array) throw new McpFailure("mcp_catalog_invalid");
            foreach (var tool in tools.EnumerateArray())
            {
                McpJson.Limit(++count, 32);
                if (tool.ValueKind != JsonValueKind.Object || !tool.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(n.GetString()) || !names.Add(n.GetString()!)) throw new McpFailure("mcp_catalog_invalid");
                var name = n.GetString()!;
                if (McpProfile.LocalName(name) is null) continue;
                var description = tool.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString()! : "";
                McpJson.Limit(description.Length, 2048);
                if (!tool.TryGetProperty("inputSchema", out var schema)) throw new McpFailure("mcp_catalog_invalid");
                accepted.Add(new(name, description, McpSchema.Admit(schema), McpJson.Fingerprint(tool)));
            }
            if (!root.TryGetProperty("nextCursor", out var next) || next.ValueKind == JsonValueKind.Null)
            {
                if (accepted.Count != 2) throw new McpFailure("mcp_catalog_invalid");
                var ordered = accepted.OrderBy(t => t.RemoteName == McpProfile.DocsTool ? 0 : 1).ToArray();
                McpJson.Limit(McpJson.Write(w =>
                {
                    w.WriteStartArray(); foreach (var tool in ordered)
                    { w.WriteStartObject(); w.WriteString("name", tool.LocalName); w.WriteString("description", tool.Definition.Description); w.WritePropertyName("parameters"); tool.Schema.WriteTo(w); w.WriteEndObject(); }
                    w.WriteEndArray();
                }).Length, McpProfile.MaxProjectionBytes);
                return new(ordered, count - 2, bytes);
            }
            if (next.ValueKind != JsonValueKind.String || !cursors.Add(next.GetString()!)) throw new McpFailure("mcp_catalog_invalid");
            cursor = next.GetString();
        }
        throw new McpFailure("mcp_limit");
    }
    public async Task<McpCallOutcome> CallAsync(McpCallPermit permit, CancellationToken token)
    {
        if (_client is null || !permit.Consume(this)) return new("mcp_changed", McpTransmission.NotSent);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!_guard.IsSafe(permit.Arguments) || !McpSchema.ValidArguments(permit.Tool.Schema, permit.Arguments)) throw new McpFailure("sensitive_arguments");
            _wire.Begin("tools/call", token, permit);
            _ = await _client.CallToolAsync(new CallToolRequestParams
            {
                Name = permit.Tool.RemoteName,
                Arguments = permit.Arguments.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal)
            }, CancellationToken.None);
            token.ThrowIfCancellationRequested();
            var response = McpResult.Project(_wire.Result, permit, _guard);
            return new("none", McpTransmission.ResponseReceived, response, true, _wire.Bytes);
        }
        catch (OperationCanceledException) { return new("cancelled", _wire.Transmission, bytes: _wire.Bytes); }
        catch (Exception ex) { return new(_wire.CleanupFailed ? "unavailable" : Error(ex), _wire.Transmission, bytes: _wire.Bytes); }
    }
    internal static string Error(Exception error)
    {
        if (error is OverflowException) return "unavailable";
        if (error is McpFailure failure) return failure.Code;
        if (error is System.Security.Authentication.AuthenticationException or HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError }) return "mcp_destination_rejected";
        if (error is OperationCanceledException) return "cancelled";
        if (error.InnerException is { } inner && Error(inner) is { } code && code != "mcp_transport_failed") return code;
        return error is JsonException or System.Text.DecoderFallbackException or System.Text.EncoderFallbackException or InvalidOperationException or KeyNotFoundException ? "mcp_invalid_response" : "mcp_transport_failed";
    }
    public async ValueTask DisposeAsync()
    {
        try { if (_client is not null) await _client.DisposeAsync(); }
        finally { try { if (_transport is not null) await _transport.DisposeAsync(); } finally { _http.Dispose(); } }
        _client = null; _transport = null;
    }
    public override string ToString() => "[McpReadClient]";
}