namespace AIAgent.Cli.Workspace;

internal enum WorkspaceNodeKind { Missing, File, Directory, Link, Other }

internal readonly record struct WorkspaceNodeState(WorkspaceNodeKind Kind, long Length = 0,
    long ModifiedSeconds = 0, long ModifiedNanoseconds = 0, ulong Device = 0, ulong Identity = 0, ulong Links = 1, uint User = 0, ushort Mode = 0);

internal interface IWorkspaceReadFileSystem
{
    WorkspaceNodeState Inspect(string fullPath);
    bool HasExactName(string fullPath) => throw new PlatformNotSupportedException("無法核對精確來源名稱");
    IEnumerable<string> EnumerateNames(string fullDirectoryPath);
    IWorkspaceReadFile OpenRead(string fullPath);
}

internal interface IWorkspaceReadFile : IAsyncDisposable
{
    Stream Stream { get; }
    WorkspaceNodeState GetState();
    UnixFileMode GetMode();
}

internal interface IWorkspaceWriteFileSystem : IWorkspaceReadFileSystem
{
    UnixFileMode GetMode(string fullPath);
    IWorkspaceStagedFile CreateStagedFile(string fullPath);
    void PublishNew(string stagedPath, string targetPath);
    void PublishReplacement(string stagedPath, string targetPath);
    void DeleteOwnedStagedFile(string fullPath);
}

internal interface IWorkspaceStagedFile : IAsyncDisposable
{
    Stream Stream { get; }
    WorkspaceNodeState GetState();
    void SetMode(UnixFileMode mode);
    void FlushToDisk();
}

internal sealed class WorkspacePublishException(WorkspaceErrorCode code) : IOException("檔案發布無法完成")
{
    internal WorkspaceErrorCode Code { get; } = code;
}