using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tools;

internal interface ITool
{
    LlmToolDefinition Definition { get; }
    Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}

internal enum ToolStatus { Success, Rejected, Cancelled, Failed, NotExecuted }

internal sealed class ToolExecutionResult
{
    internal ToolStatus Status { get; }
    internal string ErrorCode { get; }
    internal JsonElement? Response { get; }
    internal string? RelativePath { get; }
    internal long? ByteCount { get; }
    internal int? ReturnedEntries { get; }
    internal bool Truncated { get; }

    internal ToolExecutionResult(ToolStatus status, string errorCode, JsonElement? response,
        string? relativePath = null, long? byteCount = null, int? returnedEntries = null, bool truncated = false)
    {
        if (!Enum.IsDefined(status) || !ToolErrorCodes.IsValid(errorCode) ||
            response is { ValueKind: not JsonValueKind.Object }) throw new ArgumentException("工具結果無效");
        Status = status; ErrorCode = errorCode; Response = response?.Clone();
        if (status == ToolStatus.Success)
        { RelativePath = relativePath; ByteCount = byteCount; ReturnedEntries = returnedEntries; Truncated = truncated; }
    }
    public override string ToString() => "[ToolExecutionResult]";
}

internal static class ToolErrorCodes
{
    internal static bool IsValid(string code) => code is "none" or "unknown_tool" or "invalid_arguments" or
        "sensitive_arguments" or "generation_limit" or "turn_cancelled" or "turn_stopped" or
        "invalid_path" or "outside_workspace" or "sensitive_path" or "symbolic_link" or "not_found" or
        "wrong_entry_type" or "access_denied" or "file_too_large" or "invalid_utf8" or "binary_content" or
        "result_too_large" or "sensitive_content" or "path_changed" or "workspace_unavailable" or "io_failure" or "unavailable";
}