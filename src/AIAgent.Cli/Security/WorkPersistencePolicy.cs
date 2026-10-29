using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Security;

/// <summary>用途限定的整份拒絕政策；不遮蔽或修剪持久化資料。</summary>
internal sealed class WorkPersistencePolicy(ISensitiveDataPolicy policy)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex GoogleShape = new("AI" + "za[A-Za-z0-9_-]{35}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex PrivateShape = new("-{5}BEGIN (?:RSA |DSA |EC |ENCRYPTED |OPENSSH )?PRIVATE " + "KEY-{5}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly string[] CredentialNames = ["ApiKey", "api_key", "Authorization", "Proxy-Authorization", "x-goog-api-key", "access_token", "refresh_token", "client_secret"];
    private static readonly Regex Assignment = new("(?:^|[^A-Za-z0-9_])(?:" + string.Join('|', CredentialNames.Select(AsciiPattern)) + ")[\"']?[ \\t]*[:=]([^\\r\\n]*)",
        RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.NonBacktracking);
    private static readonly HashSet<string> Placeholders = new(StringComparer.Ordinal)
    { "", "YOUR_GEMINI_API_KEY", "YOUR_OPENAI_API_KEY", "YOUR_API_KEY", "YOUR_ACCESS_TOKEN", "YOUR_CLIENT_SECRET", "EXAMPLE_ONLY_NOT_A_SECRET", "[REDACTED]" };

    internal ISensitiveDataPolicy CurrentPolicy { get; } = policy;

    private static string AsciiPattern(string name) => string.Concat(name.Select(c => char.IsAsciiLetter(c)
        ? "[" + char.ToLowerInvariant(c) + char.ToUpperInvariant(c) + "]" : Regex.Escape(c.ToString())));

    internal bool IsSafe(string value)
    {
        if (!SafeCharacters(value)) return false;
        foreach (Match match in Assignment.Matches(value))
        {
            var candidate = match.Groups[1].Value.Trim(' ', '\t');
            if (candidate.Length >= 2 && candidate[0] is '\'' or '"' && candidate[^1] == candidate[0])
                candidate = candidate[1..^1];
            if (!Placeholders.Contains(candidate)) return false;
        }
        return true;
    }

    private bool SafeCharacters(string value) => CurrentPolicy.CanSendOrUse(value, ContentBoundary.WriteCandidate) &&
        !GoogleShape.IsMatch(value) && !PrivateShape.IsMatch(value) && !OpenAiCredentialShape.Contains(value);

    internal bool IsSafe(ReadOnlySpan<byte> bytes)
    {
        if (!IsSafeRaw(bytes)) return false;
        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException) { return true; }
        try
        {
            using var json = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
            return IsSafeJson(json.RootElement);
        }
        catch (JsonException) { return IsSafe(text); }
    }

    internal bool IsSafeRaw(ReadOnlySpan<byte> bytes)
    {
        // Latin-1 preserves raw ASCII contiguity even when the surrounding bytes are not UTF-8.
        if (!SafeCharacters(Encoding.Latin1.GetString(bytes))) return false;
        for (var offset = 0; offset < 2; offset++)
        {
            if (!SafeCharacters(Utf16Ascii(bytes, offset, false)) || !SafeCharacters(Utf16Ascii(bytes, offset, true))) return false;
        }
        return true;
    }

    private static string Utf16Ascii(ReadOnlySpan<byte> bytes, int offset, bool bigEndian)
    {
        var text = new StringBuilder(bytes.Length / 2);
        for (var index = offset; index + 1 < bytes.Length; index += 2)
        {
            var first = bytes[index]; var second = bytes[index + 1];
            var zero = bigEndian ? first : second; var ascii = bigEndian ? second : first;
            text.Append(zero == 0 && ascii < 128 ? (char)ascii : '\ufffd');
        }
        return text.ToString();
    }

    internal bool IsSafeJson(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (!IsSafe(property.Name) || !IsSafeJson(property.Value)) return false;
                    if (CredentialNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) &&
                        (property.Value.ValueKind != JsonValueKind.String || !Placeholders.Contains(property.Value.GetString()!.Trim(' ', '\t')))) return false;
                }
                return true;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray()) if (!IsSafeJson(item)) return false;
                return true;
            case JsonValueKind.String:
                try { return IsSafe(value.GetString()!); }
                catch (InvalidOperationException) { return false; }
            default: return IsSafe(value.GetRawText());
        }
    }

    public override string ToString() => "[WorkPersistencePolicy]";
}