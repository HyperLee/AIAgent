using System.Text.Json;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Llm;

internal static class GeminiJson
{
    internal static bool IsSensitive(JsonElement element, ISensitiveDataPolicy policy, ContentBoundary boundary)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Any(p => !policy.CanSendOrUse(p.Name, boundary) || IsSensitive(p.Value, policy, boundary)),
            JsonValueKind.Array => element.EnumerateArray().Any(v => IsSensitive(v, policy, boundary)),
            JsonValueKind.String => !policy.CanSendOrUse(element.GetString()!, boundary),
            _ => false
        };
    }

    internal static LlmUsage? ReadUsage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("usageMetadata", out var usage) || usage.ValueKind != JsonValueKind.Object) return null;
        long? Field(string name) => usage.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0 ? number : null;
        return new(Field("promptTokenCount"), Field("candidatesTokenCount"), Field("thoughtsTokenCount"), Field("cachedContentTokenCount"), Field("totalTokenCount"));
    }

    internal static bool HasSensitiveCombinedText(JsonElement root, ISensitiveDataPolicy policy)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array) return false;
        foreach (var candidate in candidates.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object || !candidate.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array) continue;
            var text = string.Concat(parts.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object && (!p.TryGetProperty("thought", out var thought) || thought.ValueKind != JsonValueKind.True) && p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String).Select(p => p.GetProperty("text").GetString()));
            if (policy.ContainsCurrentKey(text)) return true;
        }
        return false;
    }

    internal static bool HasInvalidParts(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return true;
        if (!root.TryGetProperty("candidates", out var candidates)) return false;
        if (candidates.ValueKind != JsonValueKind.Array) return true;
        foreach (var candidate in candidates.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object) return true;
            if (!candidate.TryGetProperty("content", out var content) || content.ValueKind == JsonValueKind.Null) continue;
            if (content.ValueKind != JsonValueKind.Object) return true;
            if (content.TryGetProperty("role", out var role) && (role.ValueKind != JsonValueKind.String || role.GetString() != "model")) return true;
            if (!content.TryGetProperty("parts", out var parts)) continue;
            if (parts.ValueKind != JsonValueKind.Array) return true;
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object) return true;
                if (part.EnumerateObject().Any(p => p.Name is not ("text" or "thought" or "thoughtSignature" or "functionCall" or "partMetadata"))) return true;
                if (part.TryGetProperty("text", out var text) && text.ValueKind != JsonValueKind.String) return true;
                if (part.TryGetProperty("thought", out var thought) && thought.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return true;
                if (part.TryGetProperty("functionCall", out var call))
                {
                    // Part data is a union. Developer API accepts complete native calls only;
                    // unsupported SDK fields must not escape as executable/reusable content.
                    if (part.TryGetProperty("text", out _)) return true;
                    if (call.ValueKind != JsonValueKind.Object || !call.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString()) || !call.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object) return true;
                    if (call.EnumerateObject().Any(p => p.Name is not ("name" or "args" or "id"))) return true;
                    if (call.TryGetProperty("id", out var id) && id.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)) return true;
                }
            }
        }
        return false;
    }
}
