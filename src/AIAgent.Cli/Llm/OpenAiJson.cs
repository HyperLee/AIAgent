using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Llm;

internal sealed class OpenAiBoundaryException(LlmErrorCode code) : Exception("模型邊界已停止本次操作")
{
    internal LlmErrorCode Code { get; } = code;
    public override string ToString() => "[OpenAiBoundaryException]";
}

internal static class OpenAiJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static JsonDocument Parse(ReadOnlyMemory<byte> bytes)
    {
        _ = StrictUtf8.GetString(bytes.Span);
        return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
    }
    internal static void Validate(JsonElement value, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid();
                ValidateText(property.Name); Validate(property.Value, token);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) Validate(item, token);
        else if (value.ValueKind == JsonValueKind.String) ValidateText(value.GetString()!);
    }
    private static void ValidateText(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            { if (++i == value.Length || !char.IsLowSurrogate(value[i])) throw Invalid(); }
            else if (char.IsLowSurrogate(value[i])) throw Invalid();
        }
    }
    internal static string? String(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    internal static void Check(JsonElement value, ISensitiveDataPolicy policy, ContentBoundary boundary, LlmErrorCode code)
    { if (SensitiveJson.Contains(value, policy, boundary)) throw new OpenAiBoundaryException(code); }
    internal static void Check(string value, ISensitiveDataPolicy policy, ContentBoundary boundary, LlmErrorCode code)
    { if (!policy.CanSendOrUse(value, boundary)) throw new OpenAiBoundaryException(code); }
    internal static LlmUsage? ReadUsage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
        try { Validate(usage, default); }
        catch (Exception) { return null; }
        static long? Field(JsonElement parent, string name) => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0 ? number : null;
        long? Detail(string parent, string name) => usage.TryGetProperty(parent, out var value) ? Field(value, name) : null;
        return new(Field(usage, "input_tokens"), Field(usage, "output_tokens"), Detail("output_tokens_details", "reasoning_tokens"),
            Detail("input_tokens_details", "cached_tokens"), Field(usage, "total_tokens"));
    }
    internal static OpenAiBoundaryException Invalid() => new(LlmErrorCode.InvalidResponse);
}