using System.Text.Json;

using AIAgent.Cli.Security;

namespace AIAgent.Cli.Workspace;

internal static partial class WorkspaceResponseWriter
{
    internal static bool TryCreateResponse(WorkspaceFileSearchResult result, ISensitiveDataPolicy policy,
        out JsonElement response, out WorkspaceErrorCode error, CancellationToken token = default) =>
        TryWrite(result.Status, result.ErrorCode, policy, writer =>
        {
            if (result.Data is not { } data) return;
            Property(writer, policy, "data"); writer.WriteStartObject(); Text(writer, policy, "path", data.Path);
            Property(writer, policy, "matches"); writer.WriteStartArray();
            foreach (var match in data.Matches)
            {
                token.ThrowIfCancellationRequested();
                writer.WriteStartObject(); Text(writer, policy, "path", match.Path); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            Property(writer, policy, "truncated"); writer.WriteBooleanValue(data.Truncated);
            Property(writer, policy, "safety_filtered"); writer.WriteBooleanValue(data.SafetyFiltered); writer.WriteEndObject();
        }, out response, out error, WorkspaceLimits.MaxDiscoveryResultBytes, token);

    internal static bool TryCreateResponse(WorkspaceTextSearchResult result, ISensitiveDataPolicy policy,
        out JsonElement response, out WorkspaceErrorCode error, CancellationToken token = default) =>
        TryWrite(result.Status, result.ErrorCode, policy, writer =>
        {
            if (result.Data is not { } data) return;
            Property(writer, policy, "data"); writer.WriteStartObject(); Text(writer, policy, "path", data.Path);
            Property(writer, policy, "matches"); writer.WriteStartArray();
            foreach (var match in data.Matches)
            {
                token.ThrowIfCancellationRequested();
                writer.WriteStartObject(); Text(writer, policy, "path", match.Path);
                Property(writer, policy, "line_number"); writer.WriteNumberValue(match.LineNumber);
                Text(writer, policy, "content", match.Content);
                void Context(string name, IReadOnlyList<WorkspaceContextLine> lines)
                {
                    Property(writer, policy, name); writer.WriteStartArray();
                    foreach (var line in lines)
                    {
                        token.ThrowIfCancellationRequested();
                        writer.WriteStartObject(); Property(writer, policy, "line_number"); writer.WriteNumberValue(line.LineNumber);
                        Text(writer, policy, "content", line.Content); writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                Context("before_lines", match.BeforeLines); Context("after_lines", match.AfterLines); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            Property(writer, policy, "truncated"); writer.WriteBooleanValue(data.Truncated);
            Property(writer, policy, "safety_filtered"); writer.WriteBooleanValue(data.SafetyFiltered); writer.WriteEndObject();
        }, out response, out error, WorkspaceLimits.MaxDiscoveryResultBytes, token);

    internal static bool TryCreateResponse(WorkspaceRangeReadResult result, ISensitiveDataPolicy policy,
        out JsonElement response, out WorkspaceErrorCode error, CancellationToken token = default) =>
        TryWrite(result.Status, result.ErrorCode, policy, writer =>
        {
            if (result.Data is not { } data) return;
            Property(writer, policy, "data"); writer.WriteStartObject(); Text(writer, policy, "path", data.Path);
            void Nullable(string name, int? value)
            { Property(writer, policy, name); if (value is { } number) writer.WriteNumberValue(number); else writer.WriteNullValue(); }
            Nullable("start_line", data.StartLine); Nullable("end_line", data.EndLine);
            Property(writer, policy, "total_lines"); writer.WriteNumberValue(data.TotalLines);
            Text(writer, policy, "content", data.Content);
            Property(writer, policy, "file_byte_count"); writer.WriteNumberValue(data.FileByteCount);
            Property(writer, policy, "content_byte_count"); writer.WriteNumberValue(data.ContentByteCount);
            Property(writer, policy, "has_utf8_bom"); writer.WriteBooleanValue(data.HasUtf8Bom);
            Property(writer, policy, "has_more"); writer.WriteBooleanValue(data.HasMore); writer.WriteEndObject();
        }, out response, out error, WorkspaceLimits.MaxDiscoveryResultBytes, token);
}