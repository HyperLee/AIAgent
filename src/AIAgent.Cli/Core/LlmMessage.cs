using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIAgent.Cli.Core;

[JsonConverter(typeof(LlmMessageJsonConverter))]
public sealed class LlmMessage
{
    internal string? UserText { get; }
    internal IReadOnlyList<LlmToolResult> Results { get; }
    internal object? Protocol { get; }
    internal LlmMessageKind Kind { get; }

    private LlmMessage(LlmMessageKind kind, string? text, IReadOnlyList<LlmToolResult>? results, object? protocol)
    {
        Kind = kind; UserText = text; Results = Array.AsReadOnly(results?.ToArray() ?? []); Protocol = protocol;
    }
    public static LlmMessage FromUserText(string text) => new(LlmMessageKind.User, text, null, null);
    public static LlmMessage FromToolResults(IReadOnlyList<LlmToolResult> results) => new(LlmMessageKind.ToolResults, null, results, null);
    internal static LlmMessage FromModel(object protocol) => new(LlmMessageKind.Model, null, null, protocol);
    public override string ToString() => "[LlmMessage]";
}

internal enum LlmMessageKind { User, Model, ToolResults }
internal sealed class LlmMessageJsonConverter : JsonConverter<LlmMessage>
{
    public override LlmMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("模型訊息不可序列化");
    public override void Write(Utf8JsonWriter writer, LlmMessage value, JsonSerializerOptions options) => throw new NotSupportedException("模型訊息不可序列化");
}