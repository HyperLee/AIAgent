namespace AIAgent.Cli.Workspace;

internal enum WorkspaceNodeKind { Missing, File, Directory, Link, Other }

internal readonly record struct WorkspaceNodeState(WorkspaceNodeKind Kind, long Length = 0,
    long ModifiedSeconds = 0, long ModifiedNanoseconds = 0, ulong Device = 0, ulong Identity = 0);

internal interface IWorkspaceReadFileSystem
{
    WorkspaceNodeState Inspect(string fullPath);
    IEnumerable<string> EnumerateNames(string fullDirectoryPath);
    IWorkspaceReadFile OpenRead(string fullPath);
}

internal interface IWorkspaceReadFile : IAsyncDisposable
{
    Stream Stream { get; }
    WorkspaceNodeState GetState();
}