using System.Text.Json;

namespace AIAgent.Cli.Core;

internal static class McpResult
{
    internal static JsonElement Project(JsonElement result, McpCallPermit permit, IMcpContentGuard guard, TimeProvider clock)
    {
        if (result.TryGetProperty("isError", out var error))
        {
            if (error.ValueKind == JsonValueKind.True) throw new McpFailure("mcp_remote_error");
            if (error.ValueKind != JsonValueKind.False) throw new McpFailure("mcp_invalid_response");
        }
        var texts = new List<string>(); JsonElement? structured = null;
        if (result.TryGetProperty("content", out var content))
        {
            if (content.ValueKind != JsonValueKind.Array) throw new McpFailure("mcp_result_unsupported");
            McpJson.Limit(content.GetArrayLength(), 32);
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("type", out var type) || type.GetString() != "text" ||
                    !item.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String) throw new McpFailure("mcp_result_unsupported");
                texts.Add(text.GetString()!);
            }
        }
        if (result.TryGetProperty("structuredContent", out var s))
        {
            if (s.ValueKind != JsonValueKind.Object || Depth(s) > 8) throw new McpFailure("mcp_result_unsupported");
            structured = s;
        }
        if (!texts.Any(t => t.Length > 0) && !(structured?.EnumerateObject().Any() ?? false)) throw new McpFailure("mcp_result_unsupported");
        void WritePayload(Utf8JsonWriter writer)
        {
            writer.WriteStartArray("content");
            foreach (var text in texts) { writer.WriteStartObject(); writer.WriteString("type", "text"); writer.WriteString("text", text); writer.WriteEndObject(); }
            writer.WriteEndArray();
            writer.WritePropertyName("structured_content"); if (structured is { } value) value.WriteTo(writer); else writer.WriteNullValue();
        }
        WebDocumentSource? source = null;
        if (permit.Tool.RemoteName == McpProfile.FetchTool)
        {
            var payload = McpJson.Parse(McpJson.Write(w => { w.WriteStartObject(); WritePayload(w); w.WriteEndObject(); }), guard);
            var url = permit.Arguments.GetProperty("url").GetString()!;
            var code = WebDocumentUrlPolicy.Validate(url, guard, out var view);
            if (code != "none") throw new McpFailure(code);
            var fingerprint = McpJson.Fingerprint(payload);
            source = new(url, clock.GetUtcNow(), fingerprint, view);
        }
        var data = McpJson.Write(w =>
        {
            w.WriteStartObject(); w.WriteString("source", "external_mcp"); w.WriteString("server_id", "learn");
            w.WriteString("remote_tool", permit.Tool.RemoteName); w.WriteNumber("catalog_revision", permit.CatalogRevision);
            WritePayload(w);
            if (source is not null) { w.WritePropertyName("document_source"); source.WriteTo(w); }
            w.WriteEndObject();
        });
        _ = McpJson.Parse(data, guard);
        McpJson.Limit(data.Length, 32768);
        var envelope = McpJson.Write(w =>
        {
            w.WriteStartObject(); w.WriteString("status", "success"); w.WriteString("error_code", "none");
            w.WritePropertyName("data"); McpJson.Element(data).WriteTo(w); w.WriteBoolean("truncated", false); w.WriteBoolean("safety_filtered", false); w.WriteEndObject();
        });
        McpJson.Limit(envelope.Length, 65536); return McpJson.Parse(envelope, guard);
    }
    private static int Depth(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => 1 + element.EnumerateObject().Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max(),
        JsonValueKind.Array => 1 + element.EnumerateArray().Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0
    };
}