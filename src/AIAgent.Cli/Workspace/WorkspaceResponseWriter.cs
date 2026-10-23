using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Workspace;

/// <summary>唯一的資料投影；大小與敏感檢查使用將來送出的同一份 JSON。</summary>
internal static partial class WorkspaceResponseWriter
{
    internal static bool TryCreateResponse(WorkspaceListResult result, ISensitiveDataPolicy policy,
        out JsonElement response, out WorkspaceErrorCode error) => TryWrite(result.Status, result.ErrorCode, policy, writer =>
        {
            if (result.Data is not { } data) return;
            Property(writer, policy, "data"); writer.WriteStartObject();
            Text(writer, policy, "path", data.Path);
            Property(writer, policy, "entries"); writer.WriteStartArray();
            foreach (var entry in data.Entries)
            {
                writer.WriteStartObject(); Text(writer, policy, "path", entry.Path);
                Text(writer, policy, "type", entry.Kind == WorkspaceEntryKind.File ? "file" : "directory");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            Property(writer, policy, "truncated"); writer.WriteBooleanValue(data.Truncated);
            Property(writer, policy, "safety_filtered"); writer.WriteBooleanValue(data.SafetyFiltered);
            writer.WriteEndObject();
        }, out response, out error);

    internal static bool TryCreateResponse(WorkspaceReadResult result, ISensitiveDataPolicy policy,
        out JsonElement response, out WorkspaceErrorCode error) => TryWrite(result.Status, result.ErrorCode, policy, writer =>
        {
            if (result.Data is not { } data) return;
            Property(writer, policy, "data"); writer.WriteStartObject();
            Text(writer, policy, "path", data.Path); Text(writer, policy, "content", data.Content);
            Property(writer, policy, "byte_count"); writer.WriteNumberValue(data.ByteCount);
            Property(writer, policy, "has_utf8_bom"); writer.WriteBooleanValue(data.HasUtf8Bom);
            writer.WriteEndObject();
        }, out response, out error);

    internal static bool TryCreateResponse(WorkspaceWriteResult result, ISensitiveDataPolicy policy,
        out JsonElement response, out WorkspaceErrorCode error) => TryWrite(result.Status, result.ErrorCode, policy, writer =>
        {
            if (result.Data is not { } data) return;
            Property(writer, policy, "data"); writer.WriteStartObject();
            Text(writer, policy, "path", data.Path);
            Property(writer, policy, "byte_count"); writer.WriteNumberValue(data.ByteCount);
            Property(writer, policy, "has_utf8_bom"); writer.WriteBooleanValue(data.HasUtf8Bom);
            writer.WriteEndObject();
        }, out response, out error);

    private static bool TryWrite(WorkspaceStatus status, WorkspaceErrorCode code, ISensitiveDataPolicy policy,
        Action<Utf8JsonWriter> writeData, out JsonElement response, out WorkspaceErrorCode error,
        int limit = WorkspaceLimits.MaxToolResultBytes, CancellationToken token = default)
    {
        response = default;
        try
        {
            token.ThrowIfCancellationRequested();
            using var output = new BoundedOutput(limit);
            using (var writer = new Utf8JsonWriter(output, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                writer.WriteStartObject();
                Text(writer, policy, "status", SnakeCase(status.ToString()));
                Text(writer, policy, "error_code", SnakeCase(code.ToString()));
                writeData(writer); writer.WriteEndObject(); CheckSize(writer);
            }
            token.ThrowIfCancellationRequested();
            if (output.TooLarge) throw new ProjectionFailure(WorkspaceErrorCode.ResultTooLarge);
            using var document = JsonDocument.Parse(output.GetBuffer().AsMemory(0, (int)output.Length));
            response = document.RootElement.Clone();
            error = WorkspaceErrorCode.None;
            return true;
        }
        catch (ProjectionFailure failure) { error = failure.Code; return false; }
    }

    private static void Property(Utf8JsonWriter writer, ISensitiveDataPolicy policy, string name)
    { Check(policy, name); writer.WritePropertyName(name); CheckSize(writer); }
    private static void Text(Utf8JsonWriter writer, ISensitiveDataPolicy policy, string name, string value)
    { Property(writer, policy, name); Check(policy, value); writer.WriteStringValue(value); CheckSize(writer); }
    private static void CheckSize(Utf8JsonWriter writer)
    {
        if (writer.BytesCommitted + writer.BytesPending > WorkspaceLimits.MaxToolResultBytes)
            throw new ProjectionFailure(WorkspaceErrorCode.ResultTooLarge);
    }
    private static void Check(ISensitiveDataPolicy policy, string value)
    { if (!policy.CanSendOrUse(value, ContentBoundary.ToolResult)) throw new ProjectionFailure(WorkspaceErrorCode.SensitiveContent); }
    private static string SnakeCase(string value)
    {
        var text = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsUpper(character) && text.Length > 0) text.Append('_');
            text.Append(char.ToLowerInvariant(character));
        }
        return text.ToString();
    }

    private sealed class ProjectionFailure(WorkspaceErrorCode code) : Exception("資料投影無法發布")
    { internal WorkspaceErrorCode Code { get; } = code; }

    private sealed class BoundedOutput(int limit) : MemoryStream(limit)
    {
        private readonly int _limit = limit;
        internal bool TooLarge { get; private set; }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var end = Position + buffer.Length;
            // Never throw during Utf8JsonWriter disposal. The writer checks its logical byte
            // count after each string and at completion; excess bytes are never retained.
            if (end > _limit) { TooLarge = true; return; }
            if (end > Length) SetLength(end);
            buffer.CopyTo(GetBuffer().AsSpan((int)Position, buffer.Length));
            Position = end;
        }
    }
}