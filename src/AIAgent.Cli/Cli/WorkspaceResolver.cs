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
    void EnsureDirectoryTraversable(string path);
    void EnsureDirectoryAccessible(string path);
}

internal sealed class WorkspaceResolver(IWorkspaceFileSystem fileSystem) : IWorkspaceResolver
{
    public ConfirmedWorkspace Resolve(BootstrapOptions options)
    {
        try
        {
            var supplied = options.WorkspaceArgument ?? options.StartupDirectory;
            var path = Path.IsPathRooted(supplied) ? supplied : Path.Combine(options.StartupDirectory, supplied);
            var root = Path.GetPathRoot(path)!;
            var current = root;
            var pending = new LinkedList<string>(path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
            var states = new HashSet<string>(StringComparer.Ordinal);
            var links = 0;
            fileSystem.EnsureDirectoryTraversable(current);
            while (pending.First is { } next)
            {
                pending.RemoveFirst();
                if (next.Value == ".") continue;
                if (next.Value == "..")
                {
                    current = Path.GetDirectoryName(current) ?? root;
                    continue;
                }
                var candidate = Path.Combine(current, next.Value);
                var target = fileSystem.GetLinkTarget(candidate);
                if (target is null)
                {
                    fileSystem.EnsureDirectoryTraversable(candidate);
                    current = candidate;
                    continue;
                }
                if (++links > 256 || !states.Add(candidate + "\0" + string.Join('/', pending))) throw Failure();
                // Resolve links before interpreting parent segments in their targets.
                if (Path.IsPathRooted(target)) { root = Path.GetPathRoot(target)!; current = root; target = target[root.Length..]; }
                foreach (var part in target.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Reverse()) pending.AddFirst(part);
            }
            fileSystem.EnsureDirectoryAccessible(current);
            return new(current);
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
    public void EnsureDirectoryTraversable(string path)
    {
        if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory)) throw new IOException("工作區不是目錄");
    }
    public void EnsureDirectoryAccessible(string path)
    {
        EnsureDirectoryTraversable(path);
        using var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
        _ = entries.MoveNext(); // Force opening the directory; do not retain or display entries.
    }
}
