using System.Security.Cryptography;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal interface IWorkspaceFiles : IAsyncDisposable
{
    IReadSnapshotStore Snapshots { get; }
    Task<WorkspaceListResult> ListFilesAsync(string path, CancellationToken cancellationToken);
    Task<WorkspaceReadResult> ReadFileAsync(string path, CancellationToken cancellationToken);
    Task<WorkspaceFileSearchResult> SearchFilesAsync(string path, string pattern, CancellationToken cancellationToken);
    Task<WorkspaceTextSearchResult> SearchTextAsync(string path, string query, string filePattern, bool caseSensitive, int contextLines, CancellationToken cancellationToken);
    Task<WorkspaceRangeReadResult> ReadFileRangeAsync(string path, int startLine, int endLine, CancellationToken cancellationToken);
    Task<WorkspaceWriteResult> CreateFileAsync(string path, string content, CancellationToken cancellationToken);
    Task<WorkspaceWriteResult> ReplaceTextAsync(string path, string oldText, string newText, CancellationToken cancellationToken);
}

internal interface IWorkspaceFilesFactory
{
    IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null);
    FileChangeApprovalBridge? ApprovalBridge => null;
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
    None, InvalidArguments, InvalidPattern, InvalidQuery, InvalidLineRange, InvalidPath, OutsideWorkspace, SensitivePath, SymbolicLink,
    NotFound, WrongEntryType, AccessDenied, FileTooLarge, InvalidUtf8,
    BinaryContent, ResultTooLarge, SensitiveContent, PathChanged,
    WorkspaceUnavailable, IoFailure, Unavailable, AlreadyExists, SnapshotRequired, EmptyOldText,
    TextNotFound, AmbiguousMatch, NoChange, EditTooLarge, PreviewTooLarge, BomChangeNotAllowed,
    UnsupportedMetadata, ApprovalDeclined, Conflict, PublishUnsupported, WriteUnconfirmed, CleanupFailure,
    ModeRestricted, PlanNotApproved, TaskRequired, TaskScopeMismatch, EvidenceLimit, PlanTooLarge
}

internal static class WorkspaceLimits
{
    internal const int MaxRelativePathCodeUnits = 4096;
    internal const int MaxSearchScannedEntries = 8192;
    internal const int MaxSearchCandidateFiles = 512;
    internal const int MaxSearchReadBytes = 8388608;
    internal const int MaxSearchDepth = 32;
    internal const int MaxSearchResults = 200;
    internal const int MaxSearchPatternCodeUnits = 1024;
    internal const int MaxSearchQueryCodeUnits = 1024;
    internal const int DefaultSearchContextLines = 2;
    internal const int MaxSearchContextLines = 3;
    internal const int MaxRangeLines = 200;
    internal const int MaxDiscoveryResultBytes = 65536;
    internal const int MaxFileBytes = 131072;
    internal const int MaxEditArgumentCodeUnits = 131072;
    internal const int MaxEditArgumentBytes = 131072;
    internal const int MaxPreviewBytes = 65536;
    internal const int PreviewContextLines = 3;
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
    internal ulong OriginalDevice { get; }
    internal ulong OriginalIdentity { get; }
    internal UnixFileMode OriginalMode { get; }
    internal WorkspaceReadSnapshot(string relativePath, byte[] original, bool hasUtf8Bom,
        ulong originalDevice = 0, ulong originalIdentity = 0, UnixFileMode originalMode = 0)
    {
        RelativePath = relativePath;
        OriginalDevice = originalDevice; OriginalIdentity = originalIdentity; OriginalMode = originalMode;
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