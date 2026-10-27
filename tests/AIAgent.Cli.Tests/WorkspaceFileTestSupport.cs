using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

internal static class WorkspaceFixtures
{
    internal static BootstrapSession Session(string root = "/ws", string key = GeminiFixtures.Key) => new(new(root), new("gemini-3.5-flash-lite"), new SessionSecrets(key.ToCharArray()));
    internal static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aiagent-workspace-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new Cli.WorkspaceResolver(new Cli.WorkspaceFileSystem()).Resolve(new(path, null)).RootPath;
    }
    internal static void Check(WorkspaceReadResult result, WorkspaceStatus status, WorkspaceErrorCode code, string id)
    { Assert.IsTrue(result.Status == status && result.ErrorCode == code && (status == WorkspaceStatus.Success || result.Data is null), id); }
    internal static void Check(WorkspaceListResult result, WorkspaceStatus status, WorkspaceErrorCode code, string id)
    { Assert.IsTrue(result.Status == status && result.ErrorCode == code && (status == WorkspaceStatus.Success || result.Data is null), id); }
}

internal sealed class ControlledWorkspaceFileSystem : IWorkspaceReadFileSystem
{
    internal sealed class Node(WorkspaceNodeState state, byte[]? bytes = null)
    {
        internal WorkspaceNodeState State { get; set; } = state;
        internal UnixFileMode Mode { get; set; } = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        internal byte[] Bytes { get; set; } = bytes ?? [];
    }
    internal Dictionary<string, Node> Nodes { get; } = new(StringComparer.Ordinal) { ["/ws"] = new(new(WorkspaceNodeKind.Directory, Identity: 1)) };
    internal List<string>? Names { get; set; }
    internal List<string> Inspected { get; } = [];
    internal int ProductInspectionCount => Inspected.Count(p => p is not ("/ws" or "/ws/AGENTS.md"));
    internal List<string> Enumerated { get; } = [];
    internal List<Handle> Handles { get; } = [];
    internal int Scanned { get; private set; }
    internal int Opened { get; private set; }
    internal int EnumeratorsClosed { get; private set; }
    internal Action<string>? OnInspect { get; set; }
    internal Action<int>? OnEnumerate { get; set; }
    internal Action<string>? OnOpen { get; set; }
    internal Func<CancellationToken, Task>? OnRead { get; set; }
    internal Action? OnHandleState { get; set; }
    internal bool FailDispose { get; set; }
    internal int TotalReadBytes => Handles.Sum(handle => handle.Input.BytesRead);
    internal void File(string path, byte[] bytes)
    { Nodes["/ws/" + path] = new(new(WorkspaceNodeKind.File, bytes.LongLength, Identity: (ulong)Nodes.Count + 1), bytes); }
    internal void Directory(string path) => Nodes["/ws/" + path] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)Nodes.Count + 1));
    public bool HasExactName(string path) => Nodes.ContainsKey(path);
    public WorkspaceNodeState Inspect(string path)
    { Inspected.Add(path); OnInspect?.Invoke(path); return Nodes.TryGetValue(path, out var node) ? node.State : new(WorkspaceNodeKind.Missing); }
    public IEnumerable<string> EnumerateNames(string path)
    {
        Enumerated.Add(path);
        var names = Names ?? Nodes.Keys.Where(item => item.StartsWith(path + "/", StringComparison.Ordinal) && !item[(path.Length + 1)..].Contains('/')).Select(item => item[(path.Length + 1)..]).ToList();
        try { foreach (var name in names) { Scanned++; OnEnumerate?.Invoke(Scanned); yield return name; } }
        finally { EnumeratorsClosed++; }
    }
    public IWorkspaceReadFile OpenRead(string path)
    {
        Opened++; OnOpen?.Invoke(path);
        if (!Nodes.TryGetValue(path, out var node)) throw new FileNotFoundException("FAKE_ONLY");
        var handle = new Handle(this, node); Handles.Add(handle); return handle;
    }
    internal sealed class Handle : IWorkspaceReadFile
    {
        private readonly ControlledWorkspaceFileSystem _owner;
        private readonly Node _node;
        internal ProbeStream Input { get; }
        internal bool Disposed { get; private set; }
        public Stream Stream => Input;
        internal Handle(ControlledWorkspaceFileSystem owner, Node node) { _owner = owner; _node = node; Input = new(node.Bytes, owner); }
        public UnixFileMode GetMode() => _node.Mode;
        public WorkspaceNodeState GetState() { _owner.OnHandleState?.Invoke(); return _node.State; }
        public ValueTask DisposeAsync()
        {
            Disposed = true; Input.Dispose();
            return _owner.FailDispose ? ValueTask.FromException(new IOException("FAKE_DISPOSE_ONLY")) : ValueTask.CompletedTask;
        }
    }
    internal sealed class ProbeStream(byte[] bytes, ControlledWorkspaceFileSystem owner) : MemoryStream(bytes, writable: false)
    {
        internal int BytesRead { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (owner.OnRead is not null) await owner.OnRead(cancellationToken);
            var count = await base.ReadAsync(buffer, cancellationToken); BytesRead += count; return count;
        }
    }
}