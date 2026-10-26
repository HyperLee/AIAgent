using System.Globalization;

namespace AIAgent.Cli.Workspace;

internal static class CheckpointLimits
{
    internal const int MaxCheckpointCount = 32;
    internal const int MaxCheckpointBytes = 8388608;
    internal const int MaxCheckpointSideBytes = 131072;
    internal const int MaxCheckpointDisplayBytes = 65536;
    internal const int MaxCheckpointAppendixBytes = 32768;
    internal const string RestorePrompt = "復原此單一檔案變更？[y/N] ";
    internal const string ForgetPrompt = "忘記此單一檢查點？[y/N] ";
}

internal enum CheckpointState { Available, ManualRequired, Restored }

internal interface ICheckpointWorkspace
{
    Task<CheckpointOperationResult> ListAsync(CancellationToken cancellationToken);
    Task<CheckpointOperationResult> ShowAsync(long id, CancellationToken cancellationToken);
    Task<CheckpointOperationResult> RestoreAsync(long id, ICheckpointApproval approval, CancellationToken cancellationToken);
    Task<CheckpointOperationResult> ForgetAsync(long id, ICheckpointApproval approval, CancellationToken cancellationToken);
    void Clear();
}

internal interface ICheckpointApproval
{
    ValueTask<FileApprovalDecision> ConfirmAsync(CheckpointApprovalPreview preview, CancellationToken cancellationToken);
}

internal sealed class CheckpointApprovalPreview(long checkpointId, bool isForget, string displayBlock)
{
    internal long CheckpointId { get; } = checkpointId;
    internal bool IsForget { get; } = isForget;
    internal string DisplayBlock { get; } = displayBlock;
    internal string Prompt => IsForget ? CheckpointLimits.ForgetPrompt : CheckpointLimits.RestorePrompt;
    public override string ToString() => "[CheckpointApprovalPreview]";
}

internal sealed class CheckpointOperationResult(string errorCode, string? displayBlock = null,
    long? checkpointId = null, string? relativePath = null, FileWriteState writeState = FileWriteState.NotApplied,
    long? verifiedByteCount = null, bool? verifiedHasUtf8Bom = null, bool cleanupFailed = false,
    bool fatal = false, FileApprovalDecision? decision = null)
{
    internal string ErrorCode { get; } = errorCode;
    internal string? DisplayBlock { get; } = displayBlock;
    internal long? CheckpointId { get; } = checkpointId;
    internal string? RelativePath { get; } = relativePath;
    internal FileWriteState WriteState { get; } = writeState;
    internal long? VerifiedByteCount { get; } = verifiedByteCount;
    internal bool? VerifiedHasUtf8Bom { get; } = verifiedHasUtf8Bom;
    internal bool CleanupFailed { get; } = cleanupFailed;
    internal bool Fatal { get; } = fatal;
    internal FileApprovalDecision? Decision { get; } = decision;
    public override string ToString() => "[CheckpointOperationResult]";
}

internal static class CheckpointIds
{
    internal static string Display(long id) => "c" + id.ToString(CultureInfo.InvariantCulture);
}