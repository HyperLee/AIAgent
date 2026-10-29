using System.Text.Json;
using System.Text.Json.Serialization;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Llm;

[JsonConverter(typeof(OpenAiModelContentConverter))]
internal sealed class OpenAiModelContent(IReadOnlyList<JsonElement> items, object owner, IReadOnlyList<LlmToolCall> calls)
{
    internal IReadOnlyList<JsonElement> Items { get; } = Array.AsReadOnly(items.Select(item => item.Clone()).ToArray());
    internal object Owner { get; } = owner;
    internal IReadOnlyList<LlmToolCall> Calls { get; } = Array.AsReadOnly(calls.ToArray());
    public override string ToString() => "[OpenAiModelContent]";
}
internal sealed class OpenAiModelContentConverter : JsonConverter<OpenAiModelContent>
{
    public override OpenAiModelContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("模型原生紀錄不可序列化");
    public override void Write(Utf8JsonWriter writer, OpenAiModelContent value, JsonSerializerOptions options) => throw new NotSupportedException("模型原生紀錄不可序列化");
}

internal static class OpenAiProtocol
{
    internal static bool CanRepresentTools(IReadOnlyList<LlmToolDefinition> tools)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            return tools.All(t => t is not null && ValidName(t.Name) && t.Description is not null && names.Add(t.Name) &&
            OpenAiJson.String(t.Parameters, "type") == "object" && SchemaSupported(t.Parameters));
        }
        catch (Exception) { return false; }
    }
    internal static bool ValidName(string? name) => name is { Length: >= 1 and <= 64 } && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    private static OpenAiBoundaryException Invalid() => new(LlmErrorCode.InvalidRequest);
    internal static IReadOnlyList<JsonElement> Convert(LlmRequest request, object owner, ISensitiveDataPolicy policy, CancellationToken token, bool completedTurn = false)
    {
        if (request is null || request.SystemInstruction is null || request.Messages.Count == 0) throw Invalid();
        var input = new List<JsonElement>();
        IReadOnlyList<LlmToolCall> pending = [];
        var previous = LlmMessageKind.Model;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var message in request.Messages)
        {
            token.ThrowIfCancellationRequested();
            if (message is null) throw Invalid();
            switch (message.Kind)
            {
                case LlmMessageKind.User:
                    if (pending.Count != 0 || string.IsNullOrWhiteSpace(message.UserText)) throw Invalid();
                    OpenAiJson.Check(message.UserText, policy, ContentBoundary.ModelInput, LlmErrorCode.SensitiveInput);
                    input.Add(JsonSerializer.SerializeToElement(new { role = "user", content = new[] { new { type = "input_text", text = message.UserText } } }));
                    break;
                case LlmMessageKind.Model:
                    if (input.Count == 0 || previous == LlmMessageKind.Model || pending.Count != 0 || message.Protocol is not OpenAiModelContent original ||
                        !ReferenceEquals(original.Owner, owner) || !ToolCallPairing.MatchesProjection(message, original.Calls)) throw Invalid();
                    foreach (var item in original.Items)
                    {
                        OpenAiJson.Check(item, policy, ContentBoundary.ModelInput, LlmErrorCode.SensitiveInput);
                        input.Add(item.Clone());
                    }
                    foreach (var call in original.Calls) if (string.IsNullOrWhiteSpace(call.CallId) || !ids.Add(call.CallId)) throw Invalid();
                    pending = original.Calls;
                    break;
                case LlmMessageKind.ToolResults:
                    if (previous != LlmMessageKind.Model || !ToolCallPairing.MatchesResults(pending, message.Results)) throw Invalid();
                    foreach (var result in message.Results)
                    {
                        OpenAiJson.Check(result.Response, policy, ContentBoundary.ToolResult, LlmErrorCode.SensitiveInput);
                        OpenAiJson.Check(result.Name, policy, ContentBoundary.ToolResult, LlmErrorCode.SensitiveInput);
                        OpenAiJson.Check(result.CallId!, policy, ContentBoundary.ToolResult, LlmErrorCode.SensitiveInput);
                        input.Add(JsonSerializer.SerializeToElement(new { type = "function_call_output", call_id = result.CallId, output = result.Response.GetRawText() }));
                    }
                    pending = [];
                    break;
                default: throw Invalid();
            }
            previous = message.Kind;
        }
        if ((!completedTurn && previous == LlmMessageKind.Model) || pending.Count != 0 || completedTurn && !ToolCallPairing.ValidTurn(request.Messages)) throw Invalid();
        OpenAiJson.Check(request.SystemInstruction, policy, ContentBoundary.ModelInput, LlmErrorCode.SensitiveInput);
        if (!CanRepresentTools(request.Tools)) throw Invalid();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in request.Tools)
        {
            if (tool is null || !ValidName(tool.Name) || tool.Description is null || !names.Add(tool.Name) || !SchemaSupported(tool.Parameters)) throw Invalid();
            OpenAiJson.Check(tool.Name + tool.Description, policy, ContentBoundary.ToolArguments, LlmErrorCode.SensitiveInput);
            OpenAiJson.Check(tool.Parameters, policy, ContentBoundary.ToolArguments, LlmErrorCode.SensitiveInput);
        }
        return input.AsReadOnly();
    }

    internal static byte[] Serialize(LlmRequest request, string model, object owner, ISensitiveDataPolicy policy, CancellationToken token)
    {
        var input = Convert(request, owner, policy, token);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteString("model", model); writer.WriteString("instructions", request.SystemInstruction);
            writer.WriteStartArray("input"); foreach (var item in input) { token.ThrowIfCancellationRequested(); item.WriteTo(writer); }
            writer.WriteEndArray();
            writer.WriteNumber("max_output_tokens", 8192); writer.WriteBoolean("stream", false); writer.WriteBoolean("store", false); writer.WriteBoolean("background", false);
            writer.WriteStartObject("reasoning"); writer.WriteString("effort", "none"); writer.WriteEndObject();
            writer.WriteStartArray("include"); writer.WriteStringValue("reasoning.encrypted_content"); writer.WriteEndArray();
            writer.WriteStartArray("tools");
            foreach (var tool in request.Tools)
            {
                writer.WriteStartObject(); writer.WriteString("type", "function"); writer.WriteString("name", tool.Name); writer.WriteString("description", tool.Description);
                writer.WritePropertyName("parameters"); tool.Parameters.WriteTo(writer); writer.WriteBoolean("strict", false); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteString("tool_choice", request.Tools.Count == 0 ? "none" : "auto"); writer.WriteBoolean("parallel_tool_calls", false); writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        using var parsed = OpenAiJson.Parse(bytes);
        OpenAiJson.Validate(parsed.RootElement, token);
        OpenAiJson.Check(parsed.RootElement, policy, ContentBoundary.ModelInput, LlmErrorCode.SensitiveInput);
        return bytes;
    }

    private static bool SchemaSupported(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type)) return false;
        static bool Type(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString() is "object" or "array" or "string" or "integer" or "number" or "boolean" or "null";
        if (!(Type(type) || type.ValueKind == JsonValueKind.Array && type.GetArrayLength() > 0 && type.EnumerateArray().All(Type))) return false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in schema.EnumerateObject())
        {
            if (!names.Add(p.Name)) return false;
            switch (p.Name)
            {
                case "properties":
                    if (p.Value.ValueKind != JsonValueKind.Object || p.Value.EnumerateObject().Any(v => !SchemaSupported(v.Value))) return false;
                    break;
                case "items": if (!SchemaSupported(p.Value)) return false; break;
                case "additionalProperties": if (p.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) && !SchemaSupported(p.Value)) return false; break;
                case "required": if (p.Value.ValueKind != JsonValueKind.Array || p.Value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String)) return false; break;
                case "type":
                case "$schema":
                case "description":
                case "title":
                case "enum":
                case "default":
                case "minimum":
                case "maximum":
                case "minLength":
                case "maxLength":
                case "minItems":
                case "maxItems": break;
                default: return false;
            }
        }
        return true;
    }
}