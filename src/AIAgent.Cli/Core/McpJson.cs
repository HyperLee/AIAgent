using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIAgent.Cli.Core;

internal static class McpJson
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    internal static JsonElement Parse(byte[] bytes, IMcpContentGuard guard)
    {
        try
        {
            var text = Utf8.GetString(bytes);
            if (!guard.IsSafe(text)) throw new McpFailure("sensitive_content");
            using var doc = JsonDocument.Parse(bytes);
            Validate(doc.RootElement);
            if (!guard.IsSafe(doc.RootElement)) throw new McpFailure("sensitive_content");
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or ArgumentException)
        { throw new McpFailure("mcp_invalid_response"); }
    }
    internal static void Validate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name) || property.Name.Any(char.IsControl) || !ValidText(property.Name)) throw new McpFailure("mcp_invalid_response");
                Validate(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) Validate(item);
        else if (element.ValueKind == JsonValueKind.String && !ValidText(element.GetString()!)) throw new McpFailure("mcp_invalid_response");
        else if (element.ValueKind == JsonValueKind.Number && (!element.TryGetDouble(out var number) || !double.IsFinite(number))) throw new McpFailure("mcp_invalid_response");
    }
    internal static bool ValidText(string text)
    {
        try { _ = Utf8.GetByteCount(text); }
        catch (EncoderFallbackException) { return false; }
        return !text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t') || c is '\u2028' or '\u2029');
    }
    internal static byte[] Write(JsonElement element) => Write(w => Canonical(w, element));
    internal static byte[] Write(Action<Utf8JsonWriter> write)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) { write(writer); writer.Flush(); }
        return output.ToArray();
    }
    internal static string Fingerprint(JsonElement element) => Convert.ToHexStringLower(SHA256.HashData(Write(element)));
    private static void Canonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(p.Name); Canonical(writer, p.Value); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Canonical(writer, item); writer.WriteEndArray(); }
        else element.WriteTo(writer);
    }
    internal static JsonElement Element(byte[] bytes) { using var d = JsonDocument.Parse(bytes); return d.RootElement.Clone(); }
    internal static void Limit(long actual, long limit) { if (actual > limit) throw new McpFailure("mcp_limit"); }
}