using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditFaultTests
{
    [TestMethod]
    public async Task EDITAC023_StagingFailuresNeverPublishAndCleanOnlyOwnedFile()
    {
        foreach (var fault in new[] { "create", "write", "flush", "close", "staged-read", "staged-close" })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var fs = new EditFileSystem { Fault = fault };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            var result = await files.CreateFileAsync("a", "candidate", default);
            Assert.IsTrue(result.Status == WorkspaceStatus.Failed && result.WriteState == FileWriteState.NotApplied && fs.PublishCalls == 0, "EDIT-AC-023-" + fault);
            Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-023-owned-cleanup");
        }
    }

    [TestMethod]
    public async Task EDITAC024_PublishErrorsAndReadbackFailureNeverInventSuccess()
    {
        foreach (var fault in new[] { "publish-before", "publish-after", "target-read", "target-close", "different-bytes" })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var fs = new EditFileSystem { Fault = fault };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            var result = await files.CreateFileAsync("a", "candidate", default);
            Assert.IsTrue(result.Status == WorkspaceStatus.Failed && result.WriteState == FileWriteState.Unconfirmed && result.Data is null && result.VerifiedPath is null, "EDIT-AC-024-" + fault);
            Assert.IsTrue(fs.PublishCalls == 1 && fs.TargetOpens <= 1 && files.Snapshots.Count == 0, "EDIT-AC-024-one-attempt");
        }
    }

    [TestMethod]
    public async Task EDITAC022_ExclusiveCreateDoesNotOverwriteRacingWriterOrFallback()
    {
        foreach (var unsupported in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var fs = new EditFileSystem { Fault = unsupported ? "unsupported" : "exists" };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            var result = await files.CreateFileAsync("a", "candidate", default);
            Assert.AreEqual(unsupported ? WorkspaceErrorCode.PublishUnsupported : WorkspaceErrorCode.Conflict, result.ErrorCode, "EDIT-AC-022");
            Assert.IsTrue(result.WriteState == FileWriteState.NotApplied && fs.PublishCalls == 1, "EDIT-AC-022-no-fallback");
            if (!unsupported) Assert.AreEqual("other", await File.ReadAllTextAsync(Path.Combine(root, "a")), "EDIT-AC-019-exclusive");
            Assert.AreEqual(unsupported ? 0 : 1, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-022-cleaned");
        }
    }

    [TestMethod]
    public async Task EDITAC029_CancelClearOrDisposeAfterPublishKeepsVerifiedFact()
    {
        foreach (var action in new[] { "cancel", "clear", "dispose" })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); using var cancel = new CancellationTokenSource();
            var fs = new EditFileSystem(); var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            Task? disposal = null;
            fs.OnPublish = () => { if (action == "cancel") cancel.Cancel(); else if (action == "clear") files.Snapshots.Clear(); else disposal = files.DisposeAsync().AsTask(); };
            var result = await files.CreateFileAsync("a", "candidate", cancel.Token);
            Assert.IsTrue(result.Status == WorkspaceStatus.Success && result.WriteState == FileWriteState.Verified && result.VerifiedByteCount == 9, "EDIT-AC-029-" + action);
            Assert.AreEqual("candidate", await File.ReadAllTextAsync(Path.Combine(root, "a")), "EDIT-AC-029-kept-bytes");
            if (disposal is not null) await disposal;
            await files.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task EDITAC028_CancellationDuringStagingStopsPublication()
    {
        foreach (var clear in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); using var cancel = new CancellationTokenSource();
            var fs = new EditFileSystem(); await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            fs.OnFlush = () => { if (clear) files.Snapshots.Clear(); else cancel.Cancel(); };
            var result = await files.CreateFileAsync("a", "candidate", cancel.Token);
            Assert.IsTrue(result.Status == WorkspaceStatus.Cancelled && result.WriteState == FileWriteState.NotApplied && fs.PublishCalls == 0, "EDIT-AC-028");
            Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-028-close-cleanup");
        }
    }

    [TestMethod]
    public async Task EDITAC026_CleanupFailureDoesNotDeleteForeignFileOrLoseFacts()
    {
        foreach (var fault in new[] { "cleanup", "foreign-staged" })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var fs = new EditFileSystem { Fault = fault };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            var result = await files.CreateFileAsync("a", "candidate", default);
            Assert.IsTrue(result.CleanupFailed && result.ErrorCode == WorkspaceErrorCode.CleanupFailure && result.WriteState == FileWriteState.Unconfirmed, "EDIT-AC-026");
            Assert.AreEqual(1, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-026-retained");
            if (fault == "foreign-staged") Assert.AreEqual(0, fs.DeleteCalls, "EDIT-AC-026-no-foreign-delete");
        }
    }

    [TestMethod]
    public async Task EDITAC025_RealWriteSurvivesUnsafeResponseProjection()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root, "status");
        var approval = new EditApproval(); await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        var registry = TestRulesRegistry.Create([new CreateFileTool(files, session.Policy)], session.Policy);
        using var arguments = JsonDocument.Parse("{\"path\":\"a\",\"content\":\"x\"}");
        var result = await registry.DispatchAsync(new(0, null, "create_file", arguments.RootElement), default);
        Assert.IsTrue(result.Response is null && result.WriteState == FileWriteState.Verified && result.VerifiedPath == "a" && result.VerifiedByteCount == 1, "EDIT-AC-025");
        Assert.AreEqual("x", await File.ReadAllTextAsync(Path.Combine(root, "a")), "EDIT-AC-025-real-disk");
    }

    [TestMethod]
    public async Task EDITAC040_ClearCancelsWaitingApprovalAndRejectsParallelOperation()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approval = new EditApproval { Wait = async token => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); } };
        var fs = new EditFileSystem(); var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, approval);
        var pending = files.CreateFileAsync("a", "candidate", default); await entered.Task;
        Assert.AreEqual(WorkspaceErrorCode.Unavailable, (await files.CreateFileAsync("b", "x", default)).ErrorCode, "EDIT-AC-040-parallel");
        files.Snapshots.Clear();
        Assert.AreEqual(WorkspaceStatus.Cancelled, (await pending).Status, "EDIT-AC-040-clear-cancels-wait");
        await files.DisposeAsync(); await files.DisposeAsync();
        Assert.IsTrue(fs.Created == 0 && (await files.CreateFileAsync("a", "x", default)).ErrorCode == WorkspaceErrorCode.Unavailable, "EDIT-AC-040-disposed");
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EDITAC024_ObservedStagedLengthOrModeChangeNeverReplacesTarget(bool mode)
    {
        if (!OperatingSystem.IsMacOS()) return;
        var root = WorkspaceFixtures.NewDirectory(); var target = Path.Combine(root, "a"); await File.WriteAllTextAsync(target, "abc");
        using var session = WorkspaceFixtures.Session(root); var fs = new EditFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
        await files.ReadFileAsync("a", default);
        var baselineReads = 0; var changed = false;
        fs.OnOpen = path =>
        {
            if (path != target || ++baselineReads != 3) return;
            changed = true;
            if (mode) { if (OperatingSystem.IsMacOS()) File.SetUnixFileMode(fs.StagedPath!, File.GetUnixFileMode(fs.StagedPath!) ^ UnixFileMode.UserExecute); }
            else File.AppendAllText(fs.StagedPath!, "changed");
        };
        var result = await files.ReplaceTextAsync("a", "b", "B", default);
        Assert.IsTrue(changed && fs.PublishCalls == 0 && result.WriteState == FileWriteState.NotApplied && result.StopAfterOperation, "EDIT-AC-024-observed-staging-change");
        Assert.AreEqual("abc", await File.ReadAllTextAsync(target), "EDIT-AC-024-original-preserved");
        Assert.AreEqual(1, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-024-owned-stage-cleaned");
    }

}

internal sealed class EditFileSystem : IWorkspaceWriteFileSystem
{
    private readonly MacWorkspaceFileSystem _inner = new();
    internal string Fault { get; set; } = "";
    internal Action? OnPublish { get; set; }
    internal Action? OnFlush { get; set; }
    internal int Created { get; private set; }
    internal int PublishCalls { get; private set; }
    internal int TargetOpens { get; private set; }
    internal int DeleteCalls { get; private set; }
    private string? _staged;
    internal string? StagedPath => _staged;
    internal Action<string>? OnOpen { get; set; }
    internal Action<string>? OnInspect { get; set; }
    public WorkspaceNodeState Inspect(string path) { OnInspect?.Invoke(path); return _inner.Inspect(path); }
    public IEnumerable<string> EnumerateNames(string path) => _inner.EnumerateNames(path);
    public UnixFileMode GetMode(string path) => _inner.GetMode(path);
    public IWorkspaceReadFile OpenRead(string path)
    {
        OnOpen?.Invoke(path);
        var staged = path == _staged;
        if (PublishCalls > 0 && !staged) TargetOpens++;
        if (Fault == "staged-read" && staged || Fault == "target-read" && PublishCalls > 0 && !staged) throw new IOException("FAKE_READ_ONLY");
        return new ReadFile(_inner.OpenRead(path), Fault == "staged-close" && staged || Fault == "target-close" && PublishCalls > 0 && !staged);
    }
    public IWorkspaceStagedFile CreateStagedFile(string path)
    {
        Created++;
        if (Fault == "create") throw new IOException("FAKE_CREATE_ONLY");
        _staged = path;
        return new StagedFile(this, _inner.CreateStagedFile(path));
    }
    public void PublishNew(string stagedPath, string targetPath) => Publish(stagedPath, targetPath, true);
    public void PublishReplacement(string stagedPath, string targetPath) => Publish(stagedPath, targetPath, false);
    private void Publish(string staged, string target, bool create)
    {
        PublishCalls++; OnPublish?.Invoke();
        if (Fault == "unsupported") throw new WorkspacePublishException(WorkspaceErrorCode.PublishUnsupported);
        if (Fault == "exists") File.WriteAllText(target, "other");
        if (Fault is "publish-before" or "cleanup") throw new IOException("FAKE_PUBLISH_ONLY");
        if (Fault == "foreign-staged")
        {
            File.Delete(staged); File.WriteAllText(staged, "foreign");
            throw new IOException("FAKE_FOREIGN_ONLY");
        }
        if (create) _inner.PublishNew(staged, target); else _inner.PublishReplacement(staged, target);
        if (Fault == "recreate-staged") File.WriteAllText(staged, "foreign");
        if (Fault == "publish-after") throw new IOException("FAKE_PUBLISH_ONLY");
        if (Fault == "different-bytes") File.WriteAllText(target, "other");
    }
    public void DeleteOwnedStagedFile(string path)
    { DeleteCalls++; if (Fault == "cleanup") throw new IOException("FAKE_CLEANUP_ONLY"); _inner.DeleteOwnedStagedFile(path); }
    private sealed class ReadFile(IWorkspaceReadFile inner, bool failClose) : IWorkspaceReadFile
    {
        public Stream Stream => inner.Stream;
        public WorkspaceNodeState GetState() => inner.GetState();
        public UnixFileMode GetMode() => inner.GetMode();
        public async ValueTask DisposeAsync() { await inner.DisposeAsync(); if (failClose) throw new IOException("FAKE_CLOSE_ONLY"); }
    }
    private sealed class StagedFile(EditFileSystem owner, IWorkspaceStagedFile inner) : IWorkspaceStagedFile
    {
        public Stream Stream => owner.Fault == "write" ? new FailingWriteStream(inner.Stream) : inner.Stream;
        public WorkspaceNodeState GetState() => inner.GetState();
        public void SetMode(UnixFileMode mode) { if (owner.Fault == "mode") throw new IOException("FAKE_MODE_ONLY"); inner.SetMode(mode); }
        public void FlushToDisk() { owner.OnFlush?.Invoke(); if (owner.Fault == "flush") throw new IOException("FAKE_FLUSH_ONLY"); inner.FlushToDisk(); }
        public async ValueTask DisposeAsync() { await inner.DisposeAsync(); if (owner.Fault == "close") throw new IOException("FAKE_CLOSE_ONLY"); }
    }
    private sealed class FailingWriteStream(Stream inner) : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
            throw new IOException("FAKE_WRITE_ONLY");
        }
    }
}