using System.Security.Cryptography;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal interface IWorkspaceFiles : IAsyncDisposable
{
    IReadSnapshotStore Snapshots { get; }
    Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken cancellationToken);
    Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken cancellationToken);
}

internal interface IWorkspaceFilesFactory
{
    IWorkspaceFiles Create(BootstrapSession session);
}

internal interface IReadSnapshotStore
{
    int Count { get; }
    long TotalBytes { get; }
    bool TryGet(string relativePath, out WorkspaceReadSnapshot? snapshot);
    void Clear();
}

internal enum WorkspaceStatus { Success, Rejected, Cancelled, Failed }
internal enum WorkspaceEntryKind { File, Directory }
internal enum WorkspaceErrorCode
{
    None, InvalidPath, OutsideWorkspace, SensitivePath, SymbolicLink,
    NotFound, WrongEntryType, AccessDenied, FileTooLarge, InvalidUtf8,
    BinaryContent, ResultTooLarge, SensitiveContent, PathChanged,
    WorkspaceUnavailable, IoFailure, Unavailable
}

internal static class WorkspaceLimits
{
    internal const int MaxRelativePathCodeUnits = 4096;
    internal const int MaxFileBytes = 131072;
    internal const int MaxListEntries = 200;
    internal const int MaxScannedEntries = 4096;
    internal const int MaxToolResultBytes = 196608;
    internal const int MaxSnapshotCount = 20;
    internal const int MaxSnapshotBytes = 1048576;
}

internal sealed class WorkspaceEntry(string path, WorkspaceEntryKind kind)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    internal WorkspaceEntryKind Kind { get; } = Enum.IsDefined(kind) ? kind : throw new ArgumentException("項目種類無效");
    public override string ToString() => "[WorkspaceEntry]";
}

internal sealed class WorkspaceListData(string path, IReadOnlyList<WorkspaceEntry> entries, bool truncated)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    internal IReadOnlyList<WorkspaceEntry> Entries { get; } = Array.AsReadOnly(entries.ToArray());
    internal bool Truncated { get; } = truncated;
    internal bool SafetyFiltered => true;
    public override string ToString() => "[WorkspaceListData]";
}

internal sealed class WorkspaceReadData(string path, string content, long byteCount, bool hasUtf8Bom)
{
    internal string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));
    internal string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));
    internal long ByteCount { get; } = byteCount >= 0 ? byteCount : throw new ArgumentOutOfRangeException(nameof(byteCount));
    internal bool HasUtf8Bom { get; } = hasUtf8Bom;
    public override string ToString() => "[WorkspaceReadData]";
}

internal sealed class WorkspaceReadSnapshot
{
    private readonly byte[] _original;
    internal string RelativePath { get; }
    internal long OriginalByteCount => _original.LongLength;
    internal bool HasUtf8Bom { get; }
    internal string Sha256Hex { get; }
    internal WorkspaceReadSnapshot(string relativePath, byte[] original, bool hasUtf8Bom)
    {
        RelativePath = relativePath;
        _original = original.ToArray();
        HasUtf8Bom = hasUtf8Bom;
        Sha256Hex = Convert.ToHexStringLower(SHA256.HashData(_original));
    }
    internal byte[] CopyOriginalBytes() => _original.ToArray();
    public override string ToString() => "[WorkspaceReadSnapshot]";
}

internal sealed class WorkspaceListResult
{
    internal WorkspaceStatus Status { get; }
    internal WorkspaceErrorCode ErrorCode { get; }
    internal WorkspaceListData? Data { get; }
    private WorkspaceListResult(WorkspaceStatus status, WorkspaceErrorCode errorCode, WorkspaceListData? data)
    { Status = status; ErrorCode = errorCode; Data = data; }
    internal static WorkspaceListResult Success(WorkspaceListData data) => new(WorkspaceStatus.Success, WorkspaceErrorCode.None, data ?? throw new ArgumentNullException(nameof(data)));
    internal static WorkspaceListResult End(WorkspaceStatus status, WorkspaceErrorCode code)
    { WorkspaceResultRules.ValidateEnd(status, code); return new(status, code, null); }
    public override string ToString() => "[WorkspaceListResult]";
}

internal sealed class WorkspaceReadResult
{
    internal WorkspaceStatus Status { get; }
    internal WorkspaceErrorCode ErrorCode { get; }
    internal WorkspaceReadData? Data { get; }
    private WorkspaceReadResult(WorkspaceStatus status, WorkspaceErrorCode errorCode, WorkspaceReadData? data)
    { Status = status; ErrorCode = errorCode; Data = data; }
    internal static WorkspaceReadResult Success(WorkspaceReadData data) => new(WorkspaceStatus.Success, WorkspaceErrorCode.None, data ?? throw new ArgumentNullException(nameof(data)));
    internal static WorkspaceReadResult End(WorkspaceStatus status, WorkspaceErrorCode code)
    { WorkspaceResultRules.ValidateEnd(status, code); return new(status, code, null); }
    public override string ToString() => "[WorkspaceReadResult]";
}

internal static class WorkspaceResultRules
{
    internal static void ValidateEnd(WorkspaceStatus status, WorkspaceErrorCode code)
    {
        if (!Enum.IsDefined(status) || !Enum.IsDefined(code) || status == WorkspaceStatus.Success ||
            (status == WorkspaceStatus.Cancelled) != (code == WorkspaceErrorCode.None))
            throw new ArgumentException("工作區結果無效");
    }
}