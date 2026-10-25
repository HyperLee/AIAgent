using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditBoundaryTests
{
    [TestMethod]
    public async Task EDITAC020_UnsafeChangedBaselineStopsAndIsInvalidated()
    {
        foreach (var bytes in new[] { new byte[] { 0 }, new byte[] { 255 }, Encoding.UTF8.GetBytes(GeminiFixtures.Key) })
        {
            var root = WorkspaceFixtures.NewDirectory(); var path = Path.Combine(root, "a"); await File.WriteAllTextAsync(path, "abc");
            using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            await files.ReadFileAsync("a", default); await File.WriteAllBytesAsync(path, bytes);
            var result = await files.ReplaceTextAsync("a", "b", "B", default);
            Assert.IsTrue(result.StopAfterOperation && approval.Calls == 0 && !files.Snapshots.TryGet("a", out _), "EDIT-AC-020-unsafe-baseline");
        }
    }

    [TestMethod]
    public async Task EDITAC018_SameBytesNewInodeOrModeStillConflict()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var mode in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); var path = Path.Combine(root, "a"); await File.WriteAllTextAsync(path, "abc");
            using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            await files.ReadFileAsync("a", default);
            if (mode) File.SetUnixFileMode(path, File.GetUnixFileMode(path) ^ UnixFileMode.UserExecute);
            else { await File.WriteAllTextAsync(Path.Combine(root, "external"), "abc"); File.Move(Path.Combine(root, "external"), path, true); }
            var result = await files.ReplaceTextAsync("a", "b", "B", default);
            Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.Conflict && approval.Calls == 0, "EDIT-AC-018-snapshot-metadata");
        }
    }

    [TestMethod]
    public void EDITAC010_PreviewExactByteBoundaryIncludesPromptAndEscaping()
    {
        using var session = GeminiFixtures.Session();
        // Hand-counted metadata/header/hunk/EOF/prompt overhead: 193 UTF-8 bytes.
        const int acceptedLength = 65343;
        Assert.IsTrue(FileChangePreviewBuilder.TryCreate(FileChangeKind.Create, "a", [], Encoding.UTF8.GetBytes(new string('x', acceptedLength)), session.Policy, default, out var accepted, out _), "EDIT-AC-010-65536");
        Assert.AreEqual(65536, Encoding.UTF8.GetByteCount(accepted!.DisplayBlock), "EDIT-AC-010-measured");
        Assert.IsFalse(FileChangePreviewBuilder.TryCreate(FileChangeKind.Create, "a", [], Encoding.UTF8.GetBytes(new string('x', acceptedLength + 1)), session.Policy, default, out _, out var error), "EDIT-AC-010-65537");
        Assert.AreEqual(WorkspaceErrorCode.PreviewTooLarge, error, "EDIT-AC-010-code");
        Assert.IsFalse(FileChangePreviewBuilder.TryCreate(FileChangeKind.Create, "a", [], Encoding.UTF8.GetBytes(new string('[', 20000)), session.Policy, default, out _, out error), "EDIT-AC-010-expansion");
    }

    [TestMethod]
    public async Task EDITAC009_WholeCandidateLimitIncludesUnchangedBytesAndBom()
    {
        var root = WorkspaceFixtures.NewDirectory(); var path = Path.Combine(root, "a");
        var original = Encoding.UTF8.GetBytes("\uFEFF" + new string('x', 131068) + "z");
        await File.WriteAllBytesAsync(path, original);
        using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval { Decision = FileApprovalDecision.Declined };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("a", default)).Status, "EDIT-AC-009-read-boundary");
        Assert.AreEqual(WorkspaceErrorCode.FileTooLarge, (await files.ReplaceTextAsync("a", "z", "zz", default)).ErrorCode, "EDIT-AC-009-complete-file");
        Assert.AreEqual(0, approval.Calls, "EDIT-AC-009-no-approval");
    }

    [TestMethod]
    public async Task EDITAC036_SensitiveKeyFormedAcrossReplacementFragmentsIsBlocked()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(Path.Combine(root, "a"), "AB-CD");
        using var session = WorkspaceFixtures.Session(root, "ABCD"); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        await files.ReadFileAsync("a", default);
        var result = await files.ReplaceTextAsync("a", "-", "", default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.SensitiveContent && approval.Calls == 0, "EDIT-AC-036-joined-key");
        Assert.AreEqual("AB-CD", await File.ReadAllTextAsync(Path.Combine(root, "a")), "EDIT-AC-036-unchanged");
    }

    [TestMethod]
    public async Task EDITAC004_WriteToolsRejectDuplicateExtraMissingAndWrongFields()
    {
        var fs = new ControlledWorkspaceFileSystem(); using var session = WorkspaceFixtures.Session();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
        var registry = TestRulesRegistry.Create([new CreateFileTool(files, session.Policy), new ReplaceTextTool(files, session.Policy)], session.Policy);
        foreach (var name in new[] { "create_file", "replace_text" })
        {
            var valid = name == "create_file" ? "\"path\":\"a\",\"content\":\"x\"" : "\"path\":\"a\",\"old_text\":\"x\",\"new_text\":\"y\"";
            foreach (var json in new[] { "{}", "[]", "null", "{\"path\":null}", "{\"Path\":\"a\"}", "{" + valid + ",\"confirmed\":true}",
                "{" + valid + ",\"path\":\"b\"}", "{" + valid.Replace("\"path\":\"a\"", "\"path\":123", StringComparison.Ordinal) + "}" })
            {
                using var doc = JsonDocument.Parse(json); var result = await registry.DispatchAsync(new(0, null, name, doc.RootElement), default);
                Assert.AreEqual("invalid_arguments", result.ErrorCode, "EDIT-AC-004");
            }
        }
        Assert.IsTrue(fs.ProductInspectionCount == 0 && fs.Opened == 0, "EDIT-AC-004-zero-workspace-io");
    }

    [TestMethod]
    public async Task EDITAC037_FixedPreviewMatchingShortKeyCannotBeApproved()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root, "y/N"); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        var result = await files.CreateFileAsync("a", "x", default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.SensitiveContent && approval.Calls == 0 && Directory.GetFileSystemEntries(root).Length == 0, "EDIT-AC-037");
    }
    [TestMethod]
    public async Task EDITAC018_ReadRevalidatesModeAfterResponseProjection()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var root = WorkspaceFixtures.NewDirectory(); var path = Path.Combine(root, "a"); await File.WriteAllTextAsync(path, "abc");
        using var session = WorkspaceFixtures.Session(root);
        var policy = new EditProjectionPolicy(session.Policy, () => { if (OperatingSystem.IsMacOS()) File.SetUnixFileMode(path, File.GetUnixFileMode(path) ^ UnixFileMode.UserExecute); });
        await using var files = new WorkspaceFiles(session.Workspace, policy, new MacWorkspaceFileSystem());
        var result = await files.ReadFileAsync("a", default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.PathChanged && files.Snapshots.Count == 0, "EDIT-AC-018-before-snapshot-publication");
    }

    [TestMethod]
    public void EDITAC038_VerifiedResultEscapesPathFormatCharacters()
    {
        using var session = GeminiFixtures.Session(); var terminal = new FakeTerminal();
        using var interaction = new Cli.ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var result = new ToolExecutionResult(ToolStatus.Success, "none", null, writeState: FileWriteState.Verified,
            verifiedPath: "a\u202E[LF]", verifiedByteCount: 1, verifiedHasUtf8Bom: false);
        var operation = new LocalToolOutcome(1, 0, "create_file", result);
        interaction.Show(new(ChatEventKind.AgentProgress, agentEvent: new(AgentEventKind.ToolCompleted, 1, 0, "create_file", operation)));
        Assert.IsFalse(terminal.Output.Contains('\u202E'), "EDIT-AC-038-no-bidi-output");
        Assert.IsTrue(terminal.Output.Contains("\\u{202E}", StringComparison.Ordinal), "EDIT-AC-038-visible-path");
    }

}


internal sealed class EditProjectionPolicy(Security.ISensitiveDataPolicy inner, Action mutation) : Security.ISensitiveDataPolicy
{
    private bool _changed;
    public bool ContainsCurrentKey(string text) => inner.ContainsCurrentKey(text);
    public string RedactForDisplay(string text) => inner.RedactForDisplay(text);
    public bool CanSendOrUse(string text, ContentBoundary boundary)
    {
        if (text == "status" && !_changed) { _changed = true; mutation(); }
        return inner.CanSendOrUse(text, boundary);
    }
}