using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditLifecycleTests
{
    [TestMethod]
    public async Task EDITAC035_SixWriteTurnsKeepFiveCompleteTurnsAndClearDoesNotUndoFiles()
    {
        var root = WorkspaceFixtures.NewDirectory(); var interaction = new EditChatInteraction(); var context = new ConversationContext();
        for (var i = 0; i < 6; i++)
        {
            var turn = i;
            interaction.Inner.Inputs.Enqueue(() => { Assert.AreEqual(Math.Min(turn, 5), context.CompletedTurnCount, "EDIT-AC-035-whole-turns"); return ChatInputResult.Submitted(turn.ToString()); });
        }
        interaction.Inner.Inputs.Enqueue(() => { Assert.AreEqual(3, context.CompletedTurnCount, "EDIT-AC-035-compacted"); return ChatInputResult.Submitted("/clear"); });
        interaction.Inner.Inputs.Enqueue(() => { Assert.AreEqual(0, context.CompletedTurnCount, "EDIT-AC-035-clear"); return ChatInputResult.Submitted("/exit"); });
        var handler = new GeminiHandler();
        var normalCalls = 0;
        handler.Respond = async (request, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token));
            if (!body.RootElement.TryGetProperty("tools", out _)) return GeminiHandler.Json(GeminiFixtures.Text(ContextTestData.Summary));
            normalCalls++;
            return GeminiHandler.Json(normalCalls % 2 == 1 ? BudgetFixture.Calls("create_file", new { path = "f" + normalCalls / 2, content = "x" }) : GeminiFixtures.Text());
        };
        var usage = new UsageAccumulator();
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), context, usage, "instruction", new WorkspaceFilesFactory()).RunAsync(WorkspaceFixtures.Session(root), default), "EDIT-AC-035");
        Assert.IsTrue(Directory.GetFileSystemEntries(root).Length == 6 && handler.Calls == 13 && interaction.Approvals == 6 && usage.Snapshot().DispatchedRequests == 13, "EDIT-AC-035-files-and-usage");
    }

    [TestMethod]
    public async Task EDITAC005_CreateUsesLiteralUnicodeAndWhitespacePaths()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval());
        foreach (var path in new[] { " ", " 中文😀 ", "$LITERAL", "~literal", "escaped\"[name]" })
        {
            var result = await files.CreateFileAsync(path, "x", default);
            Assert.IsTrue(result.Status == WorkspaceStatus.Success && result.VerifiedPath == path && File.Exists(Path.Combine(root, path)), "EDIT-AC-005-literal");
        }
        foreach (var path in new[] { "", "../a", "C:a", "./C:a", "/absolute", "a\\b", "a\u2028b", new string('x', 4097), ".env.example", ".git/config" })
            Assert.AreNotEqual(WorkspaceStatus.Success, (await files.CreateFileAsync(path, "x", default)).Status, "EDIT-AC-005-restricted");
    }

    [TestMethod]
    public async Task EDITAC021_SpecialModeAndSetModeFailuresDoNotPublish()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var special in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); var path = Path.Combine(root, "a"); await File.WriteAllTextAsync(path, "abc");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | (special ? UnixFileMode.StickyBit : 0));
            using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval(); var fs = new EditFileSystem { Fault = special ? "" : "mode" };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, approval);
            await files.ReadFileAsync("a", default);
            var result = await files.ReplaceTextAsync("a", "b", "B", default);
            Assert.IsTrue(result.WriteState == FileWriteState.NotApplied && fs.PublishCalls == 0 && approval.Calls == (special ? 0 : 1), "EDIT-AC-021");
            Assert.AreEqual(special ? WorkspaceErrorCode.UnsupportedMetadata : WorkspaceErrorCode.IoFailure, result.ErrorCode, "EDIT-AC-021-error");
            Assert.IsTrue(await File.ReadAllTextAsync(path) == "abc" && Directory.GetFileSystemEntries(root).Length == 1, "EDIT-AC-023-preserved");
        }
    }

    [TestMethod]
    public async Task EDITAC026_CleanupFailurePreservesNotAppliedAndVerifiedIndependently()
    {
        foreach (var verified in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var fs = new EditFileSystem { Fault = verified ? "recreate-staged" : "cleanup" };
            if (!verified) fs.OnFlush = () => throw new IOException("FAKE_FLUSH_ONLY");
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            var result = await files.CreateFileAsync("a", "candidate", default);
            Assert.IsTrue(result.CleanupFailed && result.WriteState == (verified ? FileWriteState.Verified : FileWriteState.NotApplied) &&
                result.Data is null && (verified ? result.VerifiedPath == "a" : result.VerifiedPath is null), "EDIT-AC-026-independent-state");
            Assert.AreEqual(verified ? 0 : 1, fs.DeleteCalls, "EDIT-AC-026-owned-delete");
        }
    }

    [TestMethod]
    public void EDITAC012_PreviewHasThreeContextLinesAndExactEmptySideRanges()
    {
        using var session = GeminiFixtures.Session();
        var original = Encoding.UTF8.GetBytes("1\n2\n3\n4\n5\n6\n7\n8\n9\n");
        var candidate = Encoding.UTF8.GetBytes("1\n2\n3\n4\nX\n6\n7\n8\n9\n");
        Assert.IsTrue(FileChangePreviewBuilder.TryCreate(FileChangeKind.Replace, "a", original, candidate, session.Policy, default, out var preview, out _), "EDIT-AC-012-preview");
        Assert.IsTrue(preview!.DisplayBlock.Contains("@@ -2,7 +2,7 @@\n 2 [LF]\n 3 [LF]\n 4 [LF]\n-5 [LF]\n+X [LF]\n 6 [LF]\n 7 [LF]\n 8 [LF]\n", StringComparison.Ordinal), "EDIT-AC-012-context");
        Assert.IsTrue(FileChangePreviewBuilder.TryCreate(FileChangeKind.Replace, "a", Encoding.UTF8.GetBytes("x"), [], session.Policy, default, out preview, out _) &&
            preview!.DisplayBlock.Contains("@@ -1,1 +0,0 @@\n-x [EOF]\n", StringComparison.Ordinal), "EDIT-AC-012-empty-side");
    }
}