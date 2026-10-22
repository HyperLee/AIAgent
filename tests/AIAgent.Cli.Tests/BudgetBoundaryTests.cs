using System.Text;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BudgetBoundaryTests
{
    [TestMethod]
    public async Task BUDAC009_InputAndPathUseUtf16UnitsBeforeIo()
    {
        foreach (var length in new[] { 16383, 16384, 16385 })
        {
            var text = new string('x', length - 2) + "😀";
            var terminal = new ApprovalTerminal(); terminal.Buffered(text + "\r");
            var result = new ChatInputReader(terminal).Read(default);
            Assert.IsTrue(length <= 16384 ? result.Status == ChatInputStatus.Submitted && result.Text!.Length == length :
                result.Status == ChatInputStatus.Invalid && result.Text is null, "BUD-AC-009-input");
        }
        foreach (var length in new[] { 4095, 4096, 4097 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
            var path = new string('x', length - 2) + "😀"; fs.File(path, [65]);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var result = await files.ReadFileAsync(path, default);
            Assert.IsTrue(length <= 4096 ? result.Status == WorkspaceStatus.Success && fs.Opened == 1 :
                result.ErrorCode == WorkspaceErrorCode.InvalidPath && fs.Inspected.Count == 0 && fs.Opened == 0, "BUD-AC-009-path");
        }
    }

    [TestMethod]
    public async Task BUDAC010_FileArgumentsCandidateAndFullPreviewHaveIndependentLimits()
    {
        foreach (var size in new[] { 131071, 131072, 131073 })
        {
            await using var f = new BudgetFixture();
            var source = "\uFEFF" + new string('x', size - 9) + "草稿";
            await File.WriteAllBytesAsync(Path.Combine(f.Root, "a"), Encoding.UTF8.GetBytes(source));
            var read = await f.Files.ReadFileAsync("a", default);
            Assert.IsTrue(size <= 131072 ? read.Status == WorkspaceStatus.Success && read.Data!.ByteCount == size :
                read.ErrorCode == WorkspaceErrorCode.FileTooLarge && f.Files.Snapshots.Count == 0, "BUD-AC-010-original");
            if (size == 131072)
                Assert.AreEqual(WorkspaceErrorCode.FileTooLarge, (await f.Files.ReplaceTextAsync("a", "草稿", "草稿x", default)).ErrorCode, "BUD-AC-010-candidate");
            Assert.IsTrue(f.Approval.Calls == 0 && f.FileSystem.Created == 0, "BUD-AC-010-no-side-effects");
        }
        foreach (var content in new[] { new string('中', 43691), new string('x', 131073) })
        {
            await using var f = new BudgetFixture();
            var result = await f.Files.CreateFileAsync("a", content, default);
            Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.EditTooLarge && f.Approval.Calls == 0 && f.FileSystem.Created == 0, "BUD-AC-010-argument-scales");
        }
        foreach (var (content, expected, approvals) in new[]
        { (new string('x', 65343), WorkspaceErrorCode.ApprovalDeclined, 1), (new string('x', 65344), WorkspaceErrorCode.PreviewTooLarge, 0),
          (new string('中', 22000), WorkspaceErrorCode.PreviewTooLarge, 0), (new string('[', 20000), WorkspaceErrorCode.PreviewTooLarge, 0) })
        {
            await using var f = new BudgetFixture(); f.Approval.Decision = FileApprovalDecision.Declined;
            var result = await f.Files.CreateFileAsync("a", content, default);
            Assert.IsTrue(result.ErrorCode == expected && f.Approval.Calls == approvals && f.FileSystem.Created == 0, "BUD-AC-010-full-preview");
        }
    }

    [TestMethod]
    public async Task BUDAC011_ListResultAndSnapshotLimitsDoNotLeakOrPartiallySucceed()
    {
        foreach (var count in new[] { 199, 200, 201 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
            for (var index = 0; index < count; index++) fs.File("a" + index.ToString("D3"), []);
            for (var index = 0; index < 50; index++) fs.File(".env." + index, []);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var list = await files.ListFilesAsync(".", default);
            Assert.IsTrue(list.Data!.Entries.Count == Math.Min(count, 200) && list.Data.Truncated == (count > 200) && list.Data.SafetyFiltered &&
                list.Data.Entries.All(e => !e.Path.Contains(".env", StringComparison.Ordinal)), "BUD-AC-011-visible-limit");
        }
        foreach (var count in new[] { 4095, 4096, 4097 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem { Names = Enumerable.Range(0, count).Select(i => ".env." + i).ToList() };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var list = await files.ListFilesAsync(".", default);
            Assert.IsTrue(list.Data!.Entries.Count == 0 && list.Data.Truncated == (count >= 4096) && fs.Scanned == Math.Min(count, 4096), "BUD-AC-011-scanned-limit");
        }
        foreach (var extra in new[] { 0, 1 })
        {
            using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
            fs.File("a", Encoding.UTF8.GetBytes(new string('\\', 98247) + new string('a', 1 + extra)));
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var registry = new ToolRegistry([new ReadFileTool(files, session.Policy)], session.Policy);
            var result = await registry.DispatchAsync(BudgetFixture.Call("read_file", new { path = "a" }), default);
            Assert.IsTrue(extra == 0 ? result.Status == ToolStatus.Success && Encoding.UTF8.GetByteCount(result.Response!.Value.GetRawText()) == 196608 && files.Snapshots.Count == 1 :
                result.ErrorCode == "result_too_large" && files.Snapshots.Count == 0, "BUD-AC-011-result-envelope");
        }
        using var snapshotSession = WorkspaceFixtures.Session(); var snapshotFs = new ControlledWorkspaceFileSystem();
        await using var snapshots = new WorkspaceFiles(snapshotSession.Workspace, snapshotSession.Policy, snapshotFs);
        for (var i = 0; i < 21; i++) { snapshotFs.File("empty" + i, []); await snapshots.ReadFileAsync("empty" + i, default); }
        Assert.IsTrue(snapshots.Snapshots.Count == 20 && snapshots.Snapshots.TotalBytes == 0 && !snapshots.Snapshots.TryGet("empty0", out _), "BUD-AC-011-empty-count");
        snapshots.Snapshots.Clear();
        for (var i = 0; i < 8; i++) { snapshotFs.File("full" + i, new byte[131072].Select(_ => (byte)'x').ToArray()); await snapshots.ReadFileAsync("full" + i, default); }
        Assert.AreEqual(1048576L, snapshots.Snapshots.TotalBytes, "BUD-AC-011-snapshot-byte-limit");
        Assert.IsTrue(snapshots.Snapshots.TryGet("full0", out _), "BUD-AC-011-no-refresh-query");
        snapshotFs.File("full8", Enumerable.Repeat((byte)'x', 131072).ToArray()); await snapshots.ReadFileAsync("full8", default);
        Assert.IsTrue(snapshots.Snapshots.Count == 8 && snapshots.Snapshots.TotalBytes == 1048576 && !snapshots.Snapshots.TryGet("full0", out _), "BUD-AC-011-byte-eviction");
    }
}