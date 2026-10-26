using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tools;

internal interface ITool
{
    ToolCapability Capability => ToolCapability.Unspecified;
    IProjectRulesWorkspace? ProjectRulesWorkspace => null;
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
    internal WorkspaceDiscoverySummary? DiscoverySummary { get; }

    internal FileWriteState? WriteState { get; }
    internal string? VerifiedPath { get; }
    internal long? VerifiedByteCount { get; }
    internal bool? VerifiedHasUtf8Bom { get; }
    internal bool CleanupFailed { get; }
    internal bool StopAfterOperation { get; }
    internal ProcessExecutionSummary? ProcessSummary { get; }
    internal bool ProcessCleanupFailed { get; }
    internal GitSummary? GitSummary { get; }

    internal ToolExecutionResult(ToolStatus status, string errorCode, JsonElement? response,
        string? relativePath = null, long? byteCount = null, int? returnedEntries = null, bool truncated = false,
        FileWriteState? writeState = null, string? verifiedPath = null, long? verifiedByteCount = null,
        bool? verifiedHasUtf8Bom = null, bool cleanupFailed = false, bool stopAfterOperation = false,
        WorkspaceDiscoverySummary? discoverySummary = null, ProcessExecutionSummary? processSummary = null, bool processCleanupFailed = false,
        GitSummary? gitSummary = null)
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
        if (processSummary is not null && writeState is not null ||
            processCleanupFailed != (processSummary?.CleanupFailed ?? false) ||
            processCleanupFailed && (status != ToolStatus.Failed || errorCode != "process_cleanup_failed"))
            throw new ArgumentException("工具程序事實無效");
        ProcessSummary = processSummary; ProcessCleanupFailed = processCleanupFailed; GitSummary = gitSummary;
        WriteState = writeState; VerifiedPath = verifiedPath; VerifiedByteCount = verifiedByteCount;
        VerifiedHasUtf8Bom = verifiedHasUtf8Bom; CleanupFailed = cleanupFailed; StopAfterOperation = stopAfterOperation;
        Status = status; ErrorCode = errorCode; Response = response?.Clone();
        if (status == ToolStatus.Success && response is not null) DiscoverySummary = discoverySummary;
        if (status == ToolStatus.Success)
        { RelativePath = relativePath; ByteCount = byteCount; ReturnedEntries = returnedEntries; Truncated = truncated; }
    }
    internal ToolExecutionResult WithoutResponse() => new(Status, ErrorCode, null,
        writeState: WriteState, verifiedPath: VerifiedPath, verifiedByteCount: VerifiedByteCount,
        verifiedHasUtf8Bom: VerifiedHasUtf8Bom, cleanupFailed: CleanupFailed, stopAfterOperation: StopAfterOperation,
        processSummary: ProcessSummary, processCleanupFailed: ProcessCleanupFailed, gitSummary: GitSummary);
    internal ToolExecutionResult WithProcessStop() => new(Status, ErrorCode, Response,
        stopAfterOperation: true, processSummary: ProcessSummary, processCleanupFailed: ProcessCleanupFailed, gitSummary: GitSummary);
    public override string ToString() => "[ToolExecutionResult]";
}

internal static class ToolErrorCodes
{
    internal static bool IsValid(string code) => code is "not_git_repository" or "git_unavailable" or "unsupported_git_layout" or "git_snapshot_required" or "git_snapshot_stale" or "git_source_unavailable" or "git_limit" or "git_failed" or "git_timed_out" or "ignore_unavailable" or "review_invalid" or "review_incomplete" or "rules_unavailable" or "rules_changed" or "rules_scope_mismatch" or "rules_limit" or "none" or "unknown_tool" or "invalid_arguments" or
        "invalid_pattern" or "invalid_query" or "invalid_line_range" or
        "mode_restricted" or "plan_required" or "plan_not_approved" or "plan_revision_conflict" or "task_not_found" or
        "invalid_task_transition" or "task_in_progress" or "task_required" or "task_scope_mismatch" or "evidence_required" or
        "capability_unavailable" or "evidence_limit" or "plan_too_large" or "checkpoint_limit" or "checkpoint_git_conflict" or "checkpoint_manual_required" or "checkpoint_unavailable" or
        "sensitive_arguments" or "generation_limit" or "program_generation_limit" or "turn_cancelled" or "turn_stopped" or
        "invalid_path" or "outside_workspace" or "sensitive_path" or "symbolic_link" or "not_found" or
        "wrong_entry_type" or "access_denied" or "file_too_large" or "invalid_utf8" or "binary_content" or
        "result_too_large" or "sensitive_content" or "path_changed" or "workspace_unavailable" or "io_failure" or "unavailable" or
        "already_exists" or "snapshot_required" or "empty_old_text" or "text_not_found" or "ambiguous_match" or
        "no_change" or "edit_too_large" or "preview_too_large" or "bom_change_not_allowed" or "unsupported_metadata" or
        "unsupported_project_layout" or "unsafe_output_path" or "protected_runtime_path" or "process_limit" or
        "process_start_failed" or "process_failed" or "process_timed_out" or "process_output_limit" or
        "process_io_failure" or "process_output_invalid" or "validation_inconclusive" or "process_unconfirmed" or "process_cleanup_failed" or
        "approval_declined" or "conflict" or "publish_unsupported" or "write_unconfirmed" or "cleanup_failure";
}