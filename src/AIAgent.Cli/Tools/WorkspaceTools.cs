using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tools;

internal sealed class ListFilesTool(IWorkspaceFiles workspace, ISensitiveDataPolicy policy) : ITool
{
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition { get; } = WorkspaceToolProjection.Definition("list_files", "列出指定工作區相對目錄的安全第一層項目，根目錄用 .");
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!ToolArguments.TryPath(arguments, out var path)) return ToolResponseWriter.Create(ToolStatus.Rejected, "invalid_arguments", policy);
        var result = await workspace.ListFilesAsync(path, cancellationToken);
        var safe = WorkspaceResponseWriter.TryCreateResponse(result, policy, out var response, out _);
        return new(WorkspaceToolProjection.Status(result.Status), WorkspaceToolProjection.Code(result.ErrorCode), safe ? response : null,
            result.Data?.Path, returnedEntries: result.Data?.Entries.Count, truncated: result.Data?.Truncated ?? false);
    }
    public override string ToString() => "[ListFilesTool]";
}

internal sealed class ReadFileTool(IWorkspaceFiles workspace, ISensitiveDataPolicy policy) : ITool
{
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition { get; } = WorkspaceToolProjection.Definition("read_file", "完整讀取指定工作區相對路徑的小型 UTF-8 文字檔");
    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!ToolArguments.TryPath(arguments, out var path)) return ToolResponseWriter.Create(ToolStatus.Rejected, "invalid_arguments", policy);
        var result = await workspace.ReadFileAsync(path, cancellationToken);
        var safe = WorkspaceResponseWriter.TryCreateResponse(result, policy, out var response, out _);
        return new(WorkspaceToolProjection.Status(result.Status), WorkspaceToolProjection.Code(result.ErrorCode), safe ? response : null,
            result.Data?.Path, result.Data?.ByteCount);
    }
    public override string ToString() => "[ReadFileTool]";
}

internal static class WorkspaceToolProjection
{
    internal static LlmToolDefinition Definition(string name, string description)
    {
        using var document = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}},\"required\":[\"path\"],\"additionalProperties\":false}");
        return new(name, description, document.RootElement);
    }
    internal static ToolStatus Status(WorkspaceStatus status) => status switch
    {
        WorkspaceStatus.Success => ToolStatus.Success,
        WorkspaceStatus.Rejected => ToolStatus.Rejected,
        WorkspaceStatus.Cancelled => ToolStatus.Cancelled,
        WorkspaceStatus.Failed => ToolStatus.Failed,
        _ => throw new InvalidOperationException("工作區分類無效")
    };
    internal static string Code(WorkspaceErrorCode code) => JsonNamingPolicy.SnakeCaseLower.ConvertName(code.ToString());
}