namespace AIAgent.Cli.Workspace;

internal interface IWorkSaveStore : IAsyncDisposable
{
    bool Stopped { get; }
    Task<string> PreflightSaveAsync(CancellationToken token);
    Task<IReadOnlyList<WorkStoreFile>> ListAsync(CancellationToken token);
    Task<WorkStoreFile> ReadAsync(string workId, CancellationToken token);
    Task<WorkStoreMutation> PublishAsync(string workId, ReadOnlyMemory<byte> bytes, CancellationToken token);
    Task<WorkStoreMutation> DeleteAsync(WorkStoreFile expected, CancellationToken token);
}

internal sealed class WorkStoreException(string code) : IOException("工作存檔操作無法安全完成")
{
    internal string Code { get; } = code;
    public override string ToString() => "[WorkStoreException]";
}

internal sealed class WorkStoreFile
{
    private readonly byte[] _bytes;

    internal WorkStoreFile(string workId, byte[] bytes, WorkspaceNodeState fileState, ulong rootDevice, ulong rootIdentity)
    {
        WorkId = workId;
        _bytes = (byte[])bytes.Clone();
        FileState = fileState;
        RootDevice = rootDevice;
        RootIdentity = rootIdentity;
    }

    internal string WorkId { get; }
    internal ReadOnlyMemory<byte> Bytes => (byte[])_bytes.Clone();
    internal WorkspaceNodeState FileState { get; }
    internal ulong RootDevice { get; }
    internal ulong RootIdentity { get; }
    internal bool Matches(WorkStoreFile other) => WorkId == other.WorkId && FileState == other.FileState &&
        RootDevice == other.RootDevice && RootIdentity == other.RootIdentity && _bytes.AsSpan().SequenceEqual(other._bytes);
    public override string ToString() => "[WorkStoreFile]";
}

internal enum WorkPublicationState { NotPublished, PublishedVerified, PublishedUnconfirmed }
internal enum WorkDeleteState { None, DeletedVerified, DeleteUnconfirmed }

internal sealed class WorkStoreMutation(string errorCode, WorkPublicationState publicationState = WorkPublicationState.NotPublished,
    WorkDeleteState deleteState = WorkDeleteState.None, bool cleanupFailed = false, bool storeStopped = false, string? temporaryName = null)
{
    internal string ErrorCode { get; } = errorCode;
    internal WorkPublicationState PublicationState { get; } = publicationState;
    internal WorkDeleteState DeleteState { get; } = deleteState;
    internal bool CleanupFailed { get; } = cleanupFailed;
    internal bool StoreStopped { get; } = storeStopped;
    internal string? TemporaryName { get; } = temporaryName;
    public override string ToString() => "[WorkStoreMutation]";
}