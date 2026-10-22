using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tools;

internal sealed class CreateFileTool(IWorkspaceFiles workspace, ISensitiveDataPolicy policy) : ITool
{
    public LlmToolDefinition Definition { get; } = EditToolProjection.Definition("create_file", "核准後建立工作區內不存在的 UTF-8 檔案，父目錄須存在", ["path", "content"]);
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken token)
    {
        if (!ToolArguments.TryStrings(arguments, ["path", "content"], out var values))
            return EditToolProjection.NotApplied(ToolResponseWriter.Create(ToolStatus.Rejected, "invalid_arguments", policy));
        return EditToolProjection.Result(await workspace.CreateFileAsync(values[0], values[1], token), policy);
    }
    public override string ToString() => "[CreateFileTool]";
}

internal sealed class ReplaceTextTool(IWorkspaceFiles workspace, ISensitiveDataPolicy policy) : ITool
{
    public LlmToolDefinition Definition { get; } = EditToolProjection.Definition("replace_text", "核准後替換已讀檔案中唯一的精確文字，衝突須重新讀取", ["path", "old_text", "new_text"]);
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken token)
    {
        if (!ToolArguments.TryStrings(arguments, ["path", "old_text", "new_text"], out var values))
            return EditToolProjection.NotApplied(ToolResponseWriter.Create(ToolStatus.Rejected, "invalid_arguments", policy));
        return EditToolProjection.Result(await workspace.ReplaceTextAsync(values[0], values[1], values[2], token), policy);
    }
    public override string ToString() => "[ReplaceTextTool]";
}

internal static class EditToolProjection
{
    internal static LlmToolDefinition Definition(string name, string description, string[] fields)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject(); writer.WriteString("type", "object"); writer.WriteStartObject("properties");
            foreach (var field in fields) { writer.WriteStartObject(field); writer.WriteString("type", "string"); writer.WriteEndObject(); }
            writer.WriteEndObject(); writer.WriteStartArray("required");
            foreach (var field in fields) writer.WriteStringValue(field);
            writer.WriteEndArray(); writer.WriteBoolean("additionalProperties", false); writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(output.ToArray());
        return new(name, description, document.RootElement);
    }
    internal static ToolExecutionResult Result(WorkspaceWriteResult result, ISensitiveDataPolicy policy)
    {
        var safe = WorkspaceResponseWriter.TryCreateResponse(result, policy, out var response, out _);
        return new(WorkspaceToolProjection.Status(result.Status), WorkspaceToolProjection.Code(result.ErrorCode), safe ? response : null,
            result.Data?.Path, result.Data?.ByteCount, writeState: result.WriteState, verifiedPath: result.VerifiedPath,
            verifiedByteCount: result.VerifiedByteCount, verifiedHasUtf8Bom: result.VerifiedHasUtf8Bom,
            cleanupFailed: result.CleanupFailed, stopAfterOperation: result.StopAfterOperation);
    }
    internal static ToolExecutionResult NotApplied(ToolExecutionResult result) => new(result.Status, result.ErrorCode, result.Response,
        writeState: FileWriteState.NotApplied, cleanupFailed: result.CleanupFailed, stopAfterOperation: result.StopAfterOperation);
}