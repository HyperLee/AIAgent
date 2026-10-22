using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditWorkspaceTests
{
    [TestMethod]
    public async Task EDITAC006_CreatePublishesExactBytesOnlyAfterApproval()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var content in new[] { "Hello\n", "中文😀", "", "\uFEFF", "a\uFEFFz", "no newline" })
        {
            var root = WorkspaceFixtures.NewDirectory();
            using var session = WorkspaceFixtures.Session(root);
            var approval = new EditApproval { OnConfirm = _ => Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-025-zero-staging") };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            var result = await files.CreateFileAsync("new.md", content, default);
            Assert.IsTrue(result.Status == WorkspaceStatus.Success && result.WriteState == FileWriteState.Verified, "EDIT-AC-006");
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(content), await File.ReadAllBytesAsync(Path.Combine(root, "new.md")), "EDIT-AC-006-bytes");
            Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(root, "new.md")), "EDIT-AC-021-mode");
            Assert.AreEqual(1, approval.Calls, "EDIT-AC-006-approval");
            Assert.AreEqual(1, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-006-no-staging");
            Assert.AreEqual(0, files.Snapshots.Count, "EDIT-AC-017-no-synthetic-read");
        }
    }

    [TestMethod]
    public async Task EDITAC008_ReplacePreservesBytesBomAndMode()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var source in new[] { "\uFEFF前\r\n草稿\n後\r末", "前\r\n草稿\n後\r末", "\uFEFF草稿", "草稿" })
        {
            var root = WorkspaceFixtures.NewDirectory(); var path = Path.Combine(root, "notes.md");
            await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(source));
            File.SetUnixFileMode(path, (UnixFileMode)Convert.ToInt32("755", 8));
            using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("notes.md", default)).Status, "EDIT-AC-008-read");
            Assert.IsTrue(files.Snapshots.TryGet("notes.md", out var snapshot) && snapshot!.OriginalIdentity != 0, "EDIT-AC-018-metadata");
            var result = await files.ReplaceTextAsync("notes.md", "草稿", "正式版\r\n", default);
            Assert.IsTrue(result.Status == WorkspaceStatus.Success && result.WriteState == FileWriteState.Verified, "EDIT-AC-008");
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(source.Replace("草稿", "正式版\r\n", StringComparison.Ordinal)), await File.ReadAllBytesAsync(path), "EDIT-AC-008-exact");
            Assert.AreEqual((UnixFileMode)Convert.ToInt32("755", 8), File.GetUnixFileMode(path), "EDIT-AC-021-preserve-mode");
            Assert.IsFalse(files.Snapshots.TryGet("notes.md", out _), "EDIT-AC-017-invalidated");
            Assert.AreEqual(WorkspaceErrorCode.SnapshotRequired, (await files.ReplaceTextAsync("notes.md", "正式版", "x", default)).ErrorCode, "EDIT-AC-033-second-replace");
        }
    }

    [TestMethod]
    public async Task EDITAC007_InvalidMatchesNeverAskForApproval()
    {
        foreach (var (source, oldText, newText, code) in new[] {
            ("aaa", "aa", "b", WorkspaceErrorCode.AmbiguousMatch), ("abc", "", "x", WorkspaceErrorCode.EmptyOldText),
            ("abc", "ABC", "x", WorkspaceErrorCode.TextNotFound), ("abc", "b", "b", WorkspaceErrorCode.NoChange),
            ("", "x", "", WorkspaceErrorCode.TextNotFound), ("\uFEFF", "x", "", WorkspaceErrorCode.TextNotFound),
            ("abc", "a", "\uFEFF", WorkspaceErrorCode.BomChangeNotAllowed) })
        {
            var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllBytesAsync(Path.Combine(root, "a"), Encoding.UTF8.GetBytes(source));
            using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("a", default)).Status, "EDIT-AC-007-read");
            var result = await files.ReplaceTextAsync("a", oldText, newText, default);
            Assert.IsTrue(result.ErrorCode == code && result.WriteState == FileWriteState.NotApplied && approval.Calls == 0, "EDIT-AC-007");
        }
    }

    [TestMethod]
    public async Task EDITAC015_DeclinedAndCancelledApprovalNeverPublish()
    {
        foreach (var decision in new[] { FileApprovalDecision.Declined, FileApprovalDecision.Cancelled, FileApprovalDecision.EndOfInput, FileApprovalDecision.Unavailable, (FileApprovalDecision)99 })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var approval = new EditApproval { Decision = decision };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            var result = await files.CreateFileAsync("new", "x", default);
            Assert.AreEqual(FileWriteState.NotApplied, result.WriteState, "EDIT-AC-015");
            Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-025-no-files");
            Assert.AreEqual(decision == FileApprovalDecision.Declined ? WorkspaceErrorCode.ApprovalDeclined :
                decision is FileApprovalDecision.Cancelled or FileApprovalDecision.EndOfInput ? WorkspaceErrorCode.None : WorkspaceErrorCode.Unavailable, result.ErrorCode, "EDIT-AC-016-decision");
        }
    }

    [TestMethod]
    public async Task EDITAC019_CreateRefusesExistingAndMissingParent()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(Path.Combine(root, "a"), "original");
        Directory.CreateDirectory(Path.Combine(root, "dir"));
        using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        foreach (var (path, code) in new[] { ("a", WorkspaceErrorCode.AlreadyExists), ("dir", WorkspaceErrorCode.WrongEntryType),
            ("missing/a", WorkspaceErrorCode.NotFound), ("a/", WorkspaceErrorCode.WrongEntryType), (".", WorkspaceErrorCode.WrongEntryType),
            (".env", WorkspaceErrorCode.SensitivePath), ("../outside", WorkspaceErrorCode.InvalidPath) })
            Assert.AreEqual(code, (await files.CreateFileAsync(path, "x", default)).ErrorCode, "EDIT-AC-019");
        Assert.AreEqual(0, approval.Calls, "EDIT-AC-019-no-approval");
        Assert.AreEqual("original", await File.ReadAllTextAsync(Path.Combine(root, "a")), "EDIT-AC-019-preserved");
    }

    [TestMethod]
    public async Task EDITAC018_ApprovalCannotOverwriteExternalChanges()
    {
        var root = WorkspaceFixtures.NewDirectory(); var path = Path.Combine(root, "a"); await File.WriteAllTextAsync(path, "abc");
        using var session = WorkspaceFixtures.Session(root);
        var approval = new EditApproval { OnConfirm = _ => File.WriteAllText(path, "xyz") };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        await files.ReadFileAsync("a", default);
        var result = await files.ReplaceTextAsync("a", "b", "B", default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.Conflict && result.StopAfterOperation && result.WriteState == FileWriteState.NotApplied, "EDIT-AC-018");
        Assert.AreEqual("xyz", await File.ReadAllTextAsync(path), "EDIT-AC-018-other-writer");
        Assert.IsFalse(files.Snapshots.TryGet("a", out _), "EDIT-AC-018-invalidate");
    }

    [TestMethod]
    public async Task EDITAC027_ApprovalReturningYesAfterClearOrCancelCannotWrite()
    {
        foreach (var clear in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); using var cancel = new CancellationTokenSource();
            var approval = new EditApproval();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            approval.OnConfirm = _ => { if (clear) files.Snapshots.Clear(); else cancel.Cancel(); };
            var result = await files.CreateFileAsync("a", "x", cancel.Token);
            Assert.IsTrue(result.Status == WorkspaceStatus.Cancelled && result.WriteState == FileWriteState.NotApplied, "EDIT-AC-027");
            Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-027-zero-staging");
        }
    }

    [TestMethod]
    public async Task EDITAC009_ArgumentsCandidatesAndControlsAreRejectedBeforeApproval()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        foreach (var (text, code) in new[] { (new string('x', 131073), WorkspaceErrorCode.EditTooLarge),
            (new string('中', 43691), WorkspaceErrorCode.EditTooLarge), ("\ud800", WorkspaceErrorCode.InvalidUtf8),
            ("a\0", WorkspaceErrorCode.BinaryContent), ("a\u0085", WorkspaceErrorCode.BinaryContent),
            (GeminiFixtures.Key, WorkspaceErrorCode.SensitiveContent) })
            Assert.AreEqual(code, (await files.CreateFileAsync("a", text, default)).ErrorCode, "EDIT-AC-009");
        Assert.AreEqual(0, approval.Calls, "EDIT-AC-009-no-approval");
    }

    [TestMethod]
    public async Task EDITAC012_PreviewShowsCompleteEscapedDiff()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "草稿\r\n", new UTF8Encoding(false));
        using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval { Decision = FileApprovalDecision.Declined };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        await files.ReadFileAsync("notes.md", default); await files.ReplaceTextAsync("notes.md", "草稿", "正式版", default);
        Assert.AreEqual("[預覽] 精確替換；目標：\"notes.md\"\n原始：8 bytes；UTF-8 BOM：無\n候選：11 bytes；UTF-8 BOM：無\n--- \"notes.md\"（原始）\n+++ \"notes.md\"（預計）\n@@ -1,1 +1,1 @@\n-草稿 [CRLF]\n+正式版 [CRLF]\n套用此變更？[y/N] ", approval.Preview!.DisplayBlock, "EDIT-AC-012-literal");
        Assert.AreEqual(Encoding.UTF8.GetByteCount(approval.Preview.DisplayBlock), approval.Preview.DisplayByteCount, "EDIT-AC-010-size");
        await files.CreateFileAsync("quoted\"[x]", "\\\t[LF]\u202E\u2028\uFEFF", default);
        var display = approval.Preview.DisplayBlock;
        Assert.IsTrue(display.Contains("\\\\\\t\\u{005B}LF\\u{005D}\\u{202E}\\u{2028}\\u{FEFF} [EOF]", StringComparison.Ordinal), "EDIT-AC-012-escaped");
        Assert.IsFalse(display.Contains('\u202E') || display.Contains('\u2028'), "EDIT-AC-038-no-format-injection");
    }
}

internal sealed class EditApproval : IFileChangeApproval
{
    internal int Calls { get; private set; }
    internal FileApprovalDecision Decision { get; set; } = FileApprovalDecision.Approved;
    internal FileChangePreview? Preview { get; private set; }
    internal Action<FileChangePreview>? OnConfirm { get; set; }
    internal Func<CancellationToken, Task>? Wait { get; set; }
    public async ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken token)
    { Calls++; Preview = preview; OnConfirm?.Invoke(preview); if (Wait is not null) await Wait(token); return Decision; }
}