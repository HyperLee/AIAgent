using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

public interface IWorkspaceResolver
{
    ConfirmedWorkspace Resolve(BootstrapOptions options);
    void Revalidate(ConfirmedWorkspace workspace);
}

internal interface IWorkspaceFileSystem
{
    string? GetLinkTarget(string path);
    void EnsureDirectoryAccessible(string path);
}

internal sealed class WorkspaceResolver(IWorkspaceFileSystem fileSystem) : IWorkspaceResolver
{
    public ConfirmedWorkspace Resolve(BootstrapOptions options)
    {
        try
        {
            var path = Path.GetFullPath(options.WorkspaceArgument ?? options.StartupDirectory, options.StartupDirectory);
            var states = new HashSet<string>(StringComparer.Ordinal);
            var links = 0;
            while (true)
            {
                var root = Path.GetPathRoot(path)!;
                var parts = path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                var current = root;
                fileSystem.EnsureDirectoryAccessible(current);
                var restarted = false;
                for (var i = 0; i < parts.Length; i++)
                {
                    current = Path.Combine(current, parts[i]);
                    var target = fileSystem.GetLinkTarget(current);
                    if (target is not null)
                    {
                        // Restart from the target root so links in the target's ancestors are also resolved.
                        if (++links > 256 || !states.Add(current + "\0" + string.Join('/', parts[(i + 1)..]))) throw Failure();
                        var resolved = Path.GetFullPath(target, Path.GetDirectoryName(current)!);
                        path = Path.GetFullPath(Path.Combine([resolved, .. parts[(i + 1)..]]));
                        restarted = true;
                        break;
                    }
                    fileSystem.EnsureDirectoryAccessible(current);
                }
                if (!restarted) return new(current);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Failure();
        }
    }

    public void Revalidate(ConfirmedWorkspace workspace)
    {
        try { fileSystem.EnsureDirectoryAccessible(workspace.RootPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { throw Failure(); }
    }

    private static BootstrapFailureException Failure() => new(BootstrapErrorCode.WorkspaceUnavailable);
}

internal sealed class WorkspaceFileSystem : IWorkspaceFileSystem
{
    public string? GetLinkTarget(string path) => new DirectoryInfo(path).LinkTarget;
    public void EnsureDirectoryAccessible(string path)
    {
        if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory)) throw new IOException("工作區不是目錄");
        using var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
        _ = entries.MoveNext(); // Force opening the directory; do not retain or display entries.
    }
}
