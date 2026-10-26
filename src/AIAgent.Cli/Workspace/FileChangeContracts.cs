namespace AIAgent.Cli.Workspace;

internal interface IFileChangeApproval
{
    ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken cancellationToken);
}

internal enum FileChangeKind { Create, Replace }
internal enum FileApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }
internal enum FileWriteState { NotApplied, Verified, Unconfirmed }

internal sealed class FileChangePreview
{
    internal FileChangeKind Kind { get; }
    internal string RelativePath { get; }
    internal long OriginalByteCount { get; }
    internal long CandidateByteCount { get; }
    internal bool OriginalHasUtf8Bom { get; }
    internal bool CandidateHasUtf8Bom { get; }
    internal string DisplayBlock { get; }
    internal int DisplayByteCount { get; }
    internal FileChangePreview(FileChangeKind kind, string relativePath, long originalByteCount,
        long candidateByteCount, bool originalHasUtf8Bom, bool candidateHasUtf8Bom, string displayBlock, int displayByteCount)
    {
        if (!Enum.IsDefined(kind) || !WorkspacePathRules.IsCanonical(relativePath) ||
            originalByteCount is < 0 or > WorkspaceLimits.MaxFileBytes || candidateByteCount is < 0 or > WorkspaceLimits.MaxFileBytes ||
            displayBlock is null || displayByteCount is < 0 or > WorkspaceLimits.MaxPreviewBytes ||
            new System.Text.UTF8Encoding(false, true).GetByteCount(displayBlock) != displayByteCount ||
            kind == FileChangeKind.Create && (originalByteCount != 0 || originalHasUtf8Bom)) throw new ArgumentException("預覽資料無效");
        Kind = kind; RelativePath = relativePath; OriginalByteCount = originalByteCount; CandidateByteCount = candidateByteCount;
        OriginalHasUtf8Bom = originalHasUtf8Bom; CandidateHasUtf8Bom = candidateHasUtf8Bom;
        DisplayBlock = displayBlock; DisplayByteCount = displayByteCount;
    }
    public override string ToString() => "[FileChangePreview]";
}

internal sealed class WorkspaceWriteData(string path, long byteCount, bool hasUtf8Bom)
{
    internal string Path { get; } = WorkspacePathRules.IsCanonical(path) ? path : throw new ArgumentException("核對路徑無效");
    internal long ByteCount { get; } = byteCount >= 0 ? byteCount : throw new ArgumentOutOfRangeException(nameof(byteCount));
    internal bool HasUtf8Bom { get; } = hasUtf8Bom;
    public override string ToString() => "[WorkspaceWriteData]";
}

internal sealed class WorkspaceWriteResult
{
    internal WorkspaceStatus Status { get; }
    internal WorkspaceErrorCode ErrorCode { get; }
    internal FileWriteState WriteState { get; }
    internal WorkspaceWriteData? Data { get; }
    internal string? VerifiedPath { get; }
    internal long? VerifiedByteCount { get; }
    internal bool? VerifiedHasUtf8Bom { get; }
    internal bool CleanupFailed { get; }
    internal bool StopAfterOperation { get; }

    private WorkspaceWriteResult(WorkspaceStatus status, WorkspaceErrorCode code, FileWriteState state,
        WorkspaceWriteData? verified, bool cleanupFailed, bool stopAfterOperation)
    {
        if (!Enum.IsDefined(status) || !Enum.IsDefined(code) || !Enum.IsDefined(state) ||
            (state == FileWriteState.Verified) != (verified is not null) ||
            code == WorkspaceErrorCode.WriteUnconfirmed && state != FileWriteState.Unconfirmed ||
            status == WorkspaceStatus.Success && (code != WorkspaceErrorCode.None || state != FileWriteState.Verified || cleanupFailed) ||
            status != WorkspaceStatus.Success && !cleanupFailed && (status == WorkspaceStatus.Cancelled) != (code == WorkspaceErrorCode.None) ||
            status == WorkspaceStatus.Cancelled && state != FileWriteState.NotApplied ||
            cleanupFailed && (status != WorkspaceStatus.Failed || code != WorkspaceErrorCode.CleanupFailure) ||
            state == FileWriteState.Unconfirmed && (status != WorkspaceStatus.Failed || code is not (WorkspaceErrorCode.WriteUnconfirmed or WorkspaceErrorCode.CleanupFailure)))
            throw new ArgumentException("寫入結果無效");
        Status = status; ErrorCode = code; WriteState = state; CleanupFailed = cleanupFailed;
        StopAfterOperation = stopAfterOperation;
        Data = status == WorkspaceStatus.Success ? verified : null;
        VerifiedPath = verified?.Path; VerifiedByteCount = verified?.ByteCount; VerifiedHasUtf8Bom = verified?.HasUtf8Bom;
    }
    internal static WorkspaceWriteResult Success(WorkspaceWriteData data) => new(WorkspaceStatus.Success,
        WorkspaceErrorCode.None, FileWriteState.Verified, data, false, false);
    internal static WorkspaceWriteResult End(WorkspaceStatus status, WorkspaceErrorCode code, bool stopAfterOperation = false) =>
        new(status, code, FileWriteState.NotApplied, null, false, stopAfterOperation);
    internal static WorkspaceWriteResult Unconfirmed() => new(WorkspaceStatus.Failed,
        WorkspaceErrorCode.WriteUnconfirmed, FileWriteState.Unconfirmed, null, false, true);
    internal WorkspaceWriteResult WithCleanupFailure() => new(WorkspaceStatus.Failed, WorkspaceErrorCode.CleanupFailure,
        WriteState, WriteState == FileWriteState.Verified ? new(VerifiedPath!, VerifiedByteCount!.Value, VerifiedHasUtf8Bom!.Value) : null, true, true);
    internal WorkspaceWriteResult WithCheckpointFailure() => CleanupFailed || WriteState == FileWriteState.Unconfirmed ? this : new(WorkspaceStatus.Failed, WorkspaceErrorCode.CheckpointUnavailable,
        WriteState, WriteState == FileWriteState.Verified ? new(VerifiedPath!, VerifiedByteCount!.Value, VerifiedHasUtf8Bom!.Value) : null, CleanupFailed, true);
    public override string ToString() => "[WorkspaceWriteResult]";
}

internal sealed class UnavailableFileChangeApproval : IFileChangeApproval
{
    internal static readonly UnavailableFileChangeApproval Instance = new();
    public ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken cancellationToken) => new(FileApprovalDecision.Unavailable);
    public override string ToString() => "[UnavailableFileChangeApproval]";
}