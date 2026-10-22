using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

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

    internal FileWriteState? WriteState { get; }
    internal string? VerifiedPath { get; }
    internal long? VerifiedByteCount { get; }
    internal bool? VerifiedHasUtf8Bom { get; }
    internal bool CleanupFailed { get; }
    internal bool StopAfterOperation { get; }

    internal ToolExecutionResult(ToolStatus status, string errorCode, JsonElement? response,
        string? relativePath = null, long? byteCount = null, int? returnedEntries = null, bool truncated = false,
        FileWriteState? writeState = null, string? verifiedPath = null, long? verifiedByteCount = null,
        bool? verifiedHasUtf8Bom = null, bool cleanupFailed = false, bool stopAfterOperation = false)
    {
        if (!Enum.IsDefined(status) || !ToolErrorCodes.IsValid(errorCode) ||
            response is { ValueKind: not JsonValueKind.Object }) throw new ArgumentException("工具結果無效");
        if (writeState is { } state && !Enum.IsDefined(state) ||
            writeState == FileWriteState.Verified && (verifiedPath is null || verifiedByteCount is not >= 0 || verifiedHasUtf8Bom is null) ||
            writeState != FileWriteState.Verified && (verifiedPath is not null || verifiedByteCount is not null || verifiedHasUtf8Bom is not null) ||
            writeState is not null && status == ToolStatus.Success && (writeState != FileWriteState.Verified || errorCode != "none") ||
            writeState == FileWriteState.Unconfirmed && (status != ToolStatus.Failed || errorCode is not ("write_unconfirmed" or "cleanup_failure")) ||
            cleanupFailed != (errorCode == "cleanup_failure") ||
            cleanupFailed && (status != ToolStatus.Failed || writeState is null)) throw new ArgumentException("工具寫入事實無效");
        WriteState = writeState; VerifiedPath = verifiedPath; VerifiedByteCount = verifiedByteCount;
        VerifiedHasUtf8Bom = verifiedHasUtf8Bom; CleanupFailed = cleanupFailed; StopAfterOperation = stopAfterOperation;
        Status = status; ErrorCode = errorCode; Response = response?.Clone();
        if (status == ToolStatus.Success)
        { RelativePath = relativePath; ByteCount = byteCount; ReturnedEntries = returnedEntries; Truncated = truncated; }
    }
    internal ToolExecutionResult WithoutResponse() => new(Status, ErrorCode, null,
        writeState: WriteState, verifiedPath: VerifiedPath, verifiedByteCount: VerifiedByteCount,
        verifiedHasUtf8Bom: VerifiedHasUtf8Bom, cleanupFailed: CleanupFailed, stopAfterOperation: StopAfterOperation);
    public override string ToString() => "[ToolExecutionResult]";
}

internal static class ToolErrorCodes
{
    internal static bool IsValid(string code) => code is "none" or "unknown_tool" or "invalid_arguments" or
        "sensitive_arguments" or "generation_limit" or "turn_cancelled" or "turn_stopped" or
        "invalid_path" or "outside_workspace" or "sensitive_path" or "symbolic_link" or "not_found" or
        "wrong_entry_type" or "access_denied" or "file_too_large" or "invalid_utf8" or "binary_content" or
        "result_too_large" or "sensitive_content" or "path_changed" or "workspace_unavailable" or "io_failure" or "unavailable" or
        "already_exists" or "snapshot_required" or "empty_old_text" or "text_not_found" or "ambiguous_match" or
        "no_change" or "edit_too_large" or "preview_too_large" or "bom_change_not_allowed" or "unsupported_metadata" or
        "approval_declined" or "conflict" or "publish_unsupported" or "write_unconfirmed" or "cleanup_failure";
}