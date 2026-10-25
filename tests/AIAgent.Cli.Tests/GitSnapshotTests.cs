using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitSnapshotTests
{
    [TestMethod]
    public async Task GITAC002_OpposingStagedAndUnstagedChangesRemainSeparate()
    {
        using var session = WorkspaceFixtures.Session();
        var fs = Files(); fs.File("a.cs", Encoding.UTF8.GetBytes("A\n"));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => new ScriptedGitAdapter());
        var result = await git.CaptureAsync(default);
        Assert.AreEqual("none", result.ErrorCode);
        Assert.AreEqual(2, result.Snapshot!.Changes.Count);
        Assert.AreEqual(GitLayer.Staged, result.Snapshot.Changes[0].Layer);
        Assert.AreEqual("A\n", result.Snapshot.Changes[0].Old.Content);
        Assert.AreEqual("B\n", result.Snapshot.Changes[0].New.Content);
        Assert.AreEqual(GitLayer.Unstaged, result.Snapshot.Changes[1].Layer);
        Assert.AreEqual("B\n", result.Snapshot.Changes[1].Old.Content);
        Assert.AreEqual("A\n", result.Snapshot.Changes[1].New.Content);
        Assert.AreEqual(0, files.Snapshots.Count);
    }

    [TestMethod]
    public async Task GITAC010_NewCandidateAndSameLengthChangesInvalidateSnapshot()
    {
        using var session = WorkspaceFixtures.Session(); var fs = Files(); fs.File("a.cs", Encoding.UTF8.GetBytes("A\n"));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => new ScriptedGitAdapter());
        var snapshot = (await git.CaptureAsync(default)).Snapshot!;
        fs.File("new.cs", Encoding.UTF8.GetBytes("new\n"));
        Assert.AreEqual("git_snapshot_stale", await git.ValidateAsync(snapshot, default));
        fs.Nodes.Remove("/ws/new.cs"); fs.Nodes["/ws/a.cs"].Bytes[0] = (byte)'C';
        Assert.AreEqual("git_snapshot_stale", await git.ValidateAsync(snapshot, default));
    }

    [TestMethod]
    public void GITAC013_ManifestParsersRejectBrokenProtocolAndSparseIndex()
    {
        const string oid = "1111111111111111111111111111111111111111";
        Assert.AreEqual("a.cs", GitManifestParser.Entries(Encoding.UTF8.GetBytes("H 100644 " + oid + " 0\ta.cs\0"), "sha1", true)[0].Path);
        Assert.ThrowsExactly<GitFailure>(() => GitManifestParser.Entries(Encoding.UTF8.GetBytes("H 100644 " + oid + " 0\ta.cs"), "sha1", true));
        var error = Assert.ThrowsExactly<GitFailure>(() => GitManifestParser.Entries(Encoding.UTF8.GetBytes("S 100644 " + oid + " 0\ta.cs\0"), "sha1", true));
        Assert.AreEqual("unsupported_git_layout", error.Code);
    }

    [TestMethod]
    public async Task GITAC010_UnsupportedSameLengthBytesAreStillRevalidated()
    {
        using var session = WorkspaceFixtures.Session(); var fs = Files(); fs.File("a.cs", [0, 65]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => new ScriptedGitAdapter());
        var snapshot = (await git.CaptureAsync(default)).Snapshot!;
        fs.Nodes["/ws/a.cs"].Bytes[1] = 66;
        Assert.AreEqual("git_snapshot_stale", await git.ValidateAsync(snapshot, default));
    }

    [TestMethod]
    public async Task GITAC010_CapturedBlobBytesAreRevalidatedEvenWhenObjectIdIsUnchanged()
    {
        using var session = WorkspaceFixtures.Session(); var fs = Files(); fs.File("a.cs", Encoding.UTF8.GetBytes("A\n"));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        var adapter = new ScriptedGitAdapter();
        await using var git = new GitReadWorkspace(files, () => adapter);
        var snapshot = (await git.CaptureAsync(default)).Snapshot!;
        adapter.IndexText = "C\n";
        Assert.AreEqual("git_snapshot_stale", await git.ValidateAsync(snapshot, default));
    }

    [TestMethod]
    public async Task GITAC005_MalformedIgnoreRecordRejectsOnlyUntrackedLayer()
    {
        using var session = WorkspaceFixtures.Session(); var fs = Files();
        fs.File(".gitignore", Encoding.UTF8.GetBytes("*.cs\n")); fs.File("new.cs", Encoding.UTF8.GetBytes("new\n"));
        var adapter = new ScriptedGitAdapter { IgnoreOutput = "\0\0\0.gitignore\0.gitignore\0not-a-line-number\0*.cs\0new.cs\0" };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => adapter);
        var result = await git.CaptureAsync(default);
        Assert.AreEqual("none", result.ErrorCode);
        Assert.IsTrue(result.Snapshot!.Exclusions.Any(e => e.Reason == "ignore_unavailable"));
        Assert.IsTrue(result.Snapshot.Changes.Any(c => c.Layer == GitLayer.Staged));
        Assert.IsFalse(result.Snapshot.Changes.Any(c => c.Layer == GitLayer.Untracked));
    }

    internal static ControlledWorkspaceFileSystem Files()
    {
        var fs = new ControlledWorkspaceFileSystem(); fs.Directory(".git"); fs.Directory(".git/objects");
        fs.Directory(".git/refs"); fs.File(".git/index", [1]); fs.File(".git/HEAD", Encoding.UTF8.GetBytes("ref: refs/heads/main\n"));
        fs.File(".git/config", []); return fs;
    }
}

internal sealed class ScriptedGitAdapter(bool empty = false) : IGitReadAdapter
{
    internal int RepeatedFiles { get; set; }
    internal string RepeatedText { get; set; } = "";
    internal Action? OnBlobRead { get; set; }
    internal string IndexText { get; set; } = "B\n";
    internal string? IgnoreOutput { get; set; }
    internal int Calls { get; private set; }
    internal const string Old = "1111111111111111111111111111111111111111";
    internal const string Next = "2222222222222222222222222222222222222222";
    public string? ExecutableIdentity => "synthetic-git-installation";
    public ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (recipe == GitReadRecipe.Batch) OnBlobRead?.Invoke();
        if (RepeatedFiles > 0)
        {
            var repeated = recipe switch
            {
                GitReadRecipe.HeadManifest => string.Concat(Enumerable.Range(0, RepeatedFiles).Select(i => "100644 blob " + Next + "\tf" + i + ".cs\0")),
                GitReadRecipe.IndexManifest => string.Concat(Enumerable.Range(0, RepeatedFiles).Select(i => "H 100644 " + Next + " 0\tf" + i + ".cs\0")),
                GitReadRecipe.BatchCheck => request.ObjectId + " blob " + Encoding.UTF8.GetByteCount(RepeatedText) + "\n",
                GitReadRecipe.Batch => request.ObjectId + " blob " + Encoding.UTF8.GetByteCount(RepeatedText) + "\n" + RepeatedText + "\n",
                _ => null
            };
            if (repeated is not null) return ValueTask.FromResult(new GitReadResult("none", 0, Encoding.UTF8.GetBytes(repeated), true));
        }
        var text = recipe switch
        {
            GitReadRecipe.Version => "git version 2.54.0\n",
            GitReadRecipe.InsideWorkTree => "true\n",
            GitReadRecipe.BareRepository => "false\n",
            GitReadRecipe.ObjectFormat => "sha1\n",
            GitReadRecipe.SymbolicHead => "refs/heads/main\n",
            GitReadRecipe.VerifyHead => Old + "\n",
            GitReadRecipe.HeadManifest => empty ? "" : "100644 blob " + Old + "\ta.cs\0",
            GitReadRecipe.IndexManifest => empty ? "" : "H 100644 " + Next + " 0\ta.cs\0",
            GitReadRecipe.BatchCheck => request.ObjectId + " blob 2\n",
            GitReadRecipe.Batch => request.ObjectId + " blob 2\n" + (request.ObjectId == Old ? "A\n" : IndexText) + "\n",
            GitReadRecipe.CheckIgnore => IgnoreOutput ?? string.Concat(request.Paths.Select(p => "\0\0\0" + p + "\0")),
            _ => ""
        };
        var exit = recipe is GitReadRecipe.SparseCheckoutConfig or GitReadRecipe.SparseIndexConfig or GitReadRecipe.WorktreeConfig or GitReadRecipe.BareConfig or GitReadRecipe.CheckIgnore ? 1 : 0;
        return ValueTask.FromResult(new GitReadResult("none", exit, Encoding.UTF8.GetBytes(text), true));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}