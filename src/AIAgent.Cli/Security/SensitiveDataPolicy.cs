using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Security;

public interface ISensitiveDataPolicy
{
    bool ContainsCurrentKey(string content);
    bool CanSendOrUse(string content, ContentBoundary boundary);
    string RedactForDisplay(string content);
}

public sealed class SensitiveDataPolicy : ISensitiveDataPolicy
{
    private readonly SessionSecrets secrets;
    private static readonly HashSet<string> CredentialFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "x-goog-api-key", "ApiKey", "api_key", "key",
        "access_token", "refresh_token", "client_secret"
    };

    internal SensitiveDataPolicy(SessionSecrets secrets) => this.secrets = secrets;
    public bool ContainsCurrentKey(string content) => content.AsSpan().IndexOf(secrets.AuthenticationKey, StringComparison.Ordinal) >= 0;
    public bool CanSendOrUse(string content, ContentBoundary boundary) => Enum.IsDefined(boundary) && !ContainsCurrentKey(content) && !OpenAiCredentialShape.Contains(content);

    public string RedactForDisplay(string content)
    {
        var key = secrets.AuthenticationKey;
        var remaining = content.AsSpan();
        var result = new StringBuilder();
        int index;
        while ((index = remaining.IndexOf(key, StringComparison.Ordinal)) >= 0)
        {
            result.Append(remaining[..index]);
            result.Append("[REDACTED]");
            remaining = remaining[(index + key.Length)..];
        }
        return OpenAiCredentialShape.Redact(result.Append(remaining).ToString());
    }

    internal string RedactDiagnostic(JsonElement element)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output)) WriteRedacted(writer, element);
        return RedactForDisplay(Encoding.UTF8.GetString(output.ToArray()));
    }

    private void WriteRedacted(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(RedactForDisplay(property.Name));
                    if (CredentialFields.Contains(property.Name)) writer.WriteStringValue("[REDACTED]");
                    else WriteRedacted(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteRedacted(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(RedactForDisplay(element.GetString()!)); break;
            default: element.WriteTo(writer); break;
        }
    }
}