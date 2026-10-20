using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIAgent.Cli.Security;

[JsonConverter(typeof(SecretsJsonConverter))]
public sealed class SessionSecrets : IDisposable
{
    private char[]? key;

    // Ownership is transferred; callers must discard their reference after construction.
    internal SessionSecrets(char[] ownedKey)
    {
        if (ownedKey.Length is < 1 or > 512 || ownedKey.Any(c => c is < ' ' or > '~') ||
            ownedKey[0] == ' ' || ownedKey[^1] == ' ')
        {
            Array.Clear(ownedKey);
            throw new ArgumentException("認證資料無效");
        }
        key = ownedKey;
    }

    // Only the future authentication adapter may use this span; never put it in general content.
    internal ReadOnlySpan<char> AuthenticationKey => key ?? throw new ObjectDisposedException(nameof(SessionSecrets));

    public void Dispose()
    {
        if (key is not null) { Array.Clear(key); key = null; }
    }
    public override string ToString() => "[REDACTED]";
}

internal sealed class SecretsJsonConverter : JsonConverter<SessionSecrets>
{
    public override SessionSecrets Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("認證資料不可序列化");
    public override void Write(Utf8JsonWriter writer, SessionSecrets value, JsonSerializerOptions options) => throw new NotSupportedException("認證資料不可序列化");
}