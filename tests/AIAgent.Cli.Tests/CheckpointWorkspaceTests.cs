using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class CheckpointWorkspaceTests
{
    [TestMethod]
    public async Task CKPTAC001_ControlledWritesExposeIndependentCheckpointWorkspace()
    {
        var root = WorkspaceFixtures.NewDirectory();
        using var session = WorkspaceFixtures.Session(root);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval());
        var property = typeof(IWorkspaceFiles).GetProperty("Checkpoints");
        Assert.IsNotNull(property, "CKPT-AC-001: controlled workspace must own checkpoint history");
        Assert.IsNotNull(property.GetValue(files));
    }

    [TestMethod]
    public async Task CKPTAC001_ReadSearchAndRejectedWritesNeverCapture()
    {
        await using var f = await CheckpointFixture.Create("abc");
        await f.Files.ReadFileAsync("a", default);
        await f.Files.SearchTextAsync(".", "abc", "*", true, 0, default);
        Assert.IsFalse((await f.Checkpoints.ListAsync(default)).DisplayBlock!.Contains("c1 ", StringComparison.Ordinal));
        await f.Files.ReadFileAsync("a", default);
        Assert.AreEqual(WorkspaceErrorCode.NoChange, (await f.Files.ReplaceTextAsync("a", "b", "b", default)).ErrorCode);
        f.WriteApproval.Decision = FileApprovalDecision.Declined;
        await f.Files.ReplaceTextAsync("a", "b", "B", default);
        Assert.IsFalse((await f.Checkpoints.ListAsync(default)).DisplayBlock!.Contains("c1 ", StringComparison.Ordinal));
        f.WriteApproval.Decision = FileApprovalDecision.Approved;
        await f.Files.ReadFileAsync("a", default);
        Assert.AreEqual(FileWriteState.Verified, (await f.Files.ReplaceTextAsync("a", "b", "B", default)).WriteState);
        Assert.AreEqual("checkpoint_not_found", (await f.Checkpoints.ShowAsync(1, default)).ErrorCode);
        Assert.AreEqual("none", (await f.Checkpoints.ShowAsync(2, default)).ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC013_ReplaceRestoresExactDirtyBytesBomNewlinesModeAndInvalidatesReadBaseline()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var original in new[] { "\uFEFF前\r\n草稿\n後\r末", "前\r\n草稿\n後\r末", "\uFEFF草稿", "草稿" })
        {
            await using var f = await CheckpointFixture.Create(original);
            File.SetUnixFileMode(f.Path, (UnixFileMode)493);
            await f.Replace("草稿", "正式版");
            var captured = await f.Checkpoints.ShowAsync(1, default);
            Assert.IsTrue(captured.DisplayBlock!.Contains("原始修改", StringComparison.Ordinal));
            Assert.IsFalse(captured.DisplayBlock.EndsWith(FileChangePreviewBuilder.Prompt, StringComparison.Ordinal));
            var approval = new CheckpointTestApproval();
            var result = await f.Checkpoints.RestoreAsync(1, approval, default);
            Assert.AreEqual("none", result.ErrorCode); Assert.AreEqual(FileWriteState.Verified, result.WriteState);
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(original), await File.ReadAllBytesAsync(f.Path));
            Assert.AreEqual((UnixFileMode)493, File.GetUnixFileMode(f.Path));
            Assert.AreEqual(1, approval.Calls); Assert.IsTrue(approval.Preview!.DisplayBlock.EndsWith(CheckpointLimits.RestorePrompt, StringComparison.Ordinal));
            Assert.AreEqual(AgentMode.Plan, f.Plans.Mode); Assert.AreEqual(1L, f.Plans.EvidenceEpoch);
            Assert.AreEqual(1L, f.Policy.Recovery.Revision); Assert.AreEqual(0, f.Files.Snapshots.Count);
            f.Plans.SetMode(AgentMode.Execute);
            Assert.AreEqual("checkpoint_already_restored", (await f.Checkpoints.RestoreAsync(1, approval, default)).ErrorCode);
            Assert.AreEqual(WorkspaceErrorCode.SnapshotRequired, (await f.Files.ReplaceTextAsync("a", "草稿", "x", default)).ErrorCode);
        }
    }

    [TestMethod]
    public async Task CKPTAC011_CreatedEmptyAndNonemptyFilesRemainManualAndNeverDeleted()
    {
        foreach (var text in new[] { "", "hello" })
        {
            await using var f = await CheckpointFixture.Create(null);
            Assert.AreEqual(FileWriteState.Verified, (await f.Files.CreateFileAsync("a", text, default)).WriteState);
            var approval = new CheckpointTestApproval();
            var result = await f.Checkpoints.RestoreAsync(1, approval, default);
            Assert.AreEqual("checkpoint_manual_required", result.ErrorCode);
            Assert.IsTrue(result.DisplayBlock!.Contains("created_file", StringComparison.Ordinal));
            Assert.IsTrue(File.Exists(f.Path)); Assert.AreEqual(text, await File.ReadAllTextAsync(f.Path));
            Assert.AreEqual(0, approval.Calls); Assert.AreEqual(0L, f.Policy.Recovery.Revision);
        }
    }

    [TestMethod]
    public async Task CKPTAC012_ObservedExternalContentIdentityModeAndRemovalConflictsStayManual()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var change in new[] { "same-length", "inode", "mode", "remove" })
        {
            await using var f = await CheckpointFixture.Create("abc"); await f.Replace("b", "B");
            if (change == "same-length") await File.WriteAllTextAsync(f.Path, "xyz");
            else if (change == "inode") { var alternate = f.Path + ".other"; await File.WriteAllTextAsync(alternate, "aBc"); File.Move(alternate, f.Path, true); }
            else if (change == "mode") File.SetUnixFileMode(f.Path, (UnixFileMode)384);
            else File.Delete(f.Path);
            var approval = new CheckpointTestApproval();
            var result = await f.Checkpoints.RestoreAsync(1, approval, default);
            Assert.AreEqual("checkpoint_conflict", result.ErrorCode, change); Assert.AreEqual(0, approval.Calls);
            await File.WriteAllTextAsync(f.Path, "aBc");
            Assert.AreEqual("checkpoint_manual_required", (await f.Checkpoints.RestoreAsync(1, approval, default)).ErrorCode);
            Assert.AreEqual(0L, f.Policy.Recovery.Revision);
        }
    }

    [TestMethod]
    public async Task CKPTAC012_ChangesAfterPreviewRefusePublication()
    {
        await using var f = await CheckpointFixture.Create("abc"); await f.Replace("b", "B");
        var approval = new CheckpointTestApproval { OnConfirm = _ => File.WriteAllText(f.Path, "xyz") };
        var result = await f.Checkpoints.RestoreAsync(1, approval, default);
        Assert.AreEqual("checkpoint_conflict", result.ErrorCode); Assert.AreEqual("xyz", await File.ReadAllTextAsync(f.Path));
        Assert.AreEqual(FileWriteState.NotApplied, result.WriteState); Assert.AreEqual(0L, f.Policy.Recovery.Revision);
    }

    [TestMethod]
    public async Task CKPTAC003_EachRestoreRequiresFreshApprovalAndDeclineRetainsAnchor()
    {
        await using var f = await CheckpointFixture.Create("abc"); await f.Replace("b", "B");
        foreach (var decision in new[] { FileApprovalDecision.Declined, FileApprovalDecision.Cancelled, FileApprovalDecision.EndOfInput })
        {
            var approval = new CheckpointTestApproval { Decision = decision };
            var result = await f.Checkpoints.RestoreAsync(1, approval, default);
            Assert.AreEqual(FileWriteState.NotApplied, result.WriteState); Assert.AreEqual(1, approval.Calls);
            Assert.AreEqual("aBc", await File.ReadAllTextAsync(f.Path)); Assert.AreEqual(0L, f.Policy.Recovery.Revision);
        }
        var final = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
        Assert.AreEqual(FileWriteState.Verified, final.WriteState);
    }

    [TestMethod]
    public async Task CKPTAC017_ReverseChainRequiresNewestFirstAndRebindsPredecessorInode()
    {
        await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B"); await f.Replace("B", "C");
        var approval = new CheckpointTestApproval();
        Assert.AreEqual("checkpoint_order", (await f.Checkpoints.RestoreAsync(1, approval, default)).ErrorCode);
        Assert.AreEqual(0, approval.Calls);
        Assert.AreEqual(FileWriteState.Verified, (await f.Checkpoints.RestoreAsync(2, approval, default)).WriteState);
        Assert.AreEqual("B", await File.ReadAllTextAsync(f.Path));
        Assert.AreEqual("mode_restricted", (await f.Checkpoints.RestoreAsync(1, approval, default)).ErrorCode);
        f.Plans.SetMode(AgentMode.Execute);
        Assert.AreEqual(FileWriteState.Verified, (await f.Checkpoints.RestoreAsync(1, approval, default)).WriteState);
        Assert.AreEqual("A", await File.ReadAllTextAsync(f.Path)); Assert.AreEqual(2L, f.Plans.EvidenceEpoch);
        Assert.AreEqual(2, approval.Calls); Assert.AreEqual("checkpoint_not_found", (await f.Checkpoints.ShowAsync(3, default)).ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC018_ForgetMiddleBreaksEarlierChainButRestoredHistoryCanBeForgotten()
    {
        await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B"); await f.Replace("B", "C"); await f.Replace("C", "D");
        Assert.AreEqual("none", (await f.Checkpoints.ForgetAsync(2, new CheckpointTestApproval(), default)).ErrorCode);
        Assert.AreEqual("checkpoint_manual_required", (await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default)).ErrorCode);
        Assert.AreEqual(FileWriteState.Verified, (await f.Checkpoints.RestoreAsync(3, new CheckpointTestApproval(), default)).WriteState);
        Assert.AreEqual("C", await File.ReadAllTextAsync(f.Path));
        await using var g = await CheckpointFixture.Create("A"); await g.Replace("A", "B"); await g.Replace("B", "C");
        await g.Checkpoints.RestoreAsync(2, new CheckpointTestApproval(), default);
        await g.Checkpoints.ForgetAsync(2, new CheckpointTestApproval(), default); g.Plans.SetMode(AgentMode.Execute);
        Assert.AreEqual(FileWriteState.Verified, (await g.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default)).WriteState);
    }

    [TestMethod]
    public async Task CKPTAC004_ListShowAndForgetNeverReadOrChangeTarget()
    {
        await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B"); File.Delete(f.Path);
        Assert.AreEqual("none", (await f.Checkpoints.ListAsync(default)).ErrorCode);
        Assert.AreEqual("none", (await f.Checkpoints.ShowAsync(1, default)).ErrorCode);
        Assert.AreEqual("none", (await f.Checkpoints.ForgetAsync(1, new CheckpointTestApproval(), default)).ErrorCode);
        Assert.IsFalse(File.Exists(f.Path)); Assert.AreEqual("checkpoint_not_found", (await f.Checkpoints.ShowAsync(1, default)).ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC003_ReversePreviewMetadataOverLimitNeverAsksApproval()
    {
        await using var calibration = await CheckpointFixture.Create("xA"); await calibration.Replace("A", "B");
        var declined = new CheckpointTestApproval { Decision = FileApprovalDecision.Declined };
        await calibration.Checkpoints.RestoreAsync(1, declined, default);
        var padding = (65537 - Encoding.UTF8.GetByteCount(declined.Preview!.DisplayBlock) + 1) / 2 + 1;
        var source = new string('x', padding) + "A";
        await using var f = await CheckpointFixture.Create(source); await f.Replace("A", "B");
        var approval = new CheckpointTestApproval();
        var result = await f.Checkpoints.RestoreAsync(1, approval, default);
        Assert.AreEqual("preview_too_large", result.ErrorCode); Assert.AreEqual(0, approval.Calls);
        Assert.AreEqual(new string('x', padding) + "B", await File.ReadAllTextAsync(f.Path));
        Assert.AreEqual(0L, f.Policy.Recovery.Revision);
    }

    [TestMethod]
    public async Task CKPTAC003_CompleteDisplayAccepts65536BytesAndRefuses65537()
    {
        await using var f = await CheckpointFixture.Create(null);
        var display = typeof(WorkspaceFiles).GetMethod("CheckpointDisplay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        foreach (var length in new[] { 65536, 65537 })
        {
            var result = (CheckpointOperationResult)display.Invoke(f.Files, [new string('x', length), null, null])!;
            Assert.AreEqual(length == 65536 ? "none" : "preview_too_large", result.ErrorCode);
            Assert.AreEqual(length == 65536, result.DisplayBlock is not null);
        }
    }

    [TestMethod]
    public async Task CKPTAC005_CountLimitStopsWriteBeforeApprovalAndForgetAllowsNewId()
    {
        await using var f = await CheckpointFixture.Create(null);
        for (var i = 0; i < 32; i++) Assert.AreEqual(FileWriteState.Verified, (await f.Files.CreateFileAsync("f" + i, "x", default)).WriteState);
        var calls = f.WriteApproval.Calls;
        var refused = await f.Files.CreateFileAsync("extra", "x", default);
        Assert.AreEqual(WorkspaceErrorCode.CheckpointLimit, refused.ErrorCode); Assert.AreEqual(FileWriteState.NotApplied, refused.WriteState);
        Assert.AreEqual(calls, f.WriteApproval.Calls); Assert.IsFalse(File.Exists(System.IO.Path.Combine(f.Root, "extra")));
        await f.Checkpoints.ForgetAsync(32, new CheckpointTestApproval(), default);
        Assert.AreEqual(FileWriteState.Verified, (await f.Files.CreateFileAsync("extra", "x", default)).WriteState);
        Assert.AreEqual("none", (await f.Checkpoints.ShowAsync(33, default)).ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC005_CountLimitStopsToolBatchAndBlocksTaskAsResourceLimit()
    {
        await using var f = await CheckpointFixture.Create(null);
        for (var i = 0; i < 32; i++) await f.Files.CreateFileAsync("f" + i, "x", default);
        f.Plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Plans.Submit(PlanFixture.Submission(kind: "write", tool: "create_file", path: "extra")).Success);
        Assert.IsTrue(f.Plans.Approve(f.Plans.ApprovalPreview()!));
        var plan = f.Plans.Snapshot!;
        Assert.IsTrue(f.Plans.Update(JsonSerializer.SerializeToElement(new
        {
            plan_id = plan.PlanId,
            expected_revision = plan.Revision,
            task_id = plan.Tasks[0].TaskId,
            status = "in_progress",
            summary = "starting"
        })).Success);
        using var args = JsonDocument.Parse("{\"path\":\"extra\",\"content\":\"x\"}");
        var client = new ScriptClient
        {
            Session = f.Session,
            Respond = (_, _, _) =>
        {
            LlmToolCall[] calls = [new(0, null, "create_file", args.RootElement), new(1, null, "create_file", args.RootElement)];
            return Task.FromResult(new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), null, true, false));
        }
        };
        var registry = TestRulesRegistry.Create([new CreateFileTool(f.Files, f.Session.Policy)], f.Session.Policy, f.Policy);
        var result = await new AgentRunner(client, registry, f.Session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("write")], default);
        Assert.AreEqual(AgentStopReason.TaskResourceLimit, result.StopReason);
        Assert.AreEqual(ToolStatus.NotExecuted, result.Operations[1].Status);
        Assert.AreEqual("resource_limit", f.Plans.Snapshot!.Tasks[0].BlockReason);
        Assert.AreEqual(32, f.WriteApproval.Calls);
    }

    [TestMethod]
    public void CKPTAC005_ExactSideAndTotalByteReservationsIncludeAllInFlightRecords()
    {
        var store = new CheckpointStore(new()); var side = new byte[131072];
        for (var i = 0; i < 32; i++) Assert.IsNotNull(store.Reserve(side, side));
        Assert.AreEqual(8388608L, store.TotalBytes); Assert.AreEqual(32, store.Count); Assert.IsNull(store.Reserve([], []));
        var second = new CheckpointStore(new()); Assert.IsNull(second.Reserve(new byte[131073], [])); Assert.IsNull(second.Reserve([], new byte[131073]));
        var reservation = second.Reserve(side, side)!; second.Release(reservation); Assert.AreEqual(0L, second.TotalBytes);
        Assert.AreEqual(2L, second.Reserve([], [])!.Id); second.Clear(); Assert.AreEqual(3L, second.Reserve([], [])!.Id);
    }

    [TestMethod]
    public async Task CKPTAC027_WorkspaceReplacementWithSameApplicationKeepsMonotonicIds()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var plans = new PlanStore(session.Policy);
        await using (var first = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), new(plans)))
        { await first.CreateFileAsync("first", "a", default); Assert.AreEqual("none", (await first.Checkpoints.ShowAsync(1, default)).ErrorCode); }
        await using var next = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), new(plans));
        await next.CreateFileAsync("next", "b", default);
        Assert.AreEqual("checkpoint_not_found", (await next.Checkpoints.ShowAsync(1, default)).ErrorCode);
        Assert.AreEqual("none", (await next.Checkpoints.ShowAsync(2, default)).ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC027_ClearDuringApprovalCancelsCandidateWithoutRevivingData()
    {
        await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B");
        var result = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval { OnConfirm = _ => f.Checkpoints.Clear() }, default);
        Assert.AreEqual("checkpoint_revision_conflict", result.ErrorCode); Assert.AreEqual("B", await File.ReadAllTextAsync(f.Path));
        Assert.AreEqual("checkpoint_not_found", (await f.Checkpoints.ShowAsync(1, default)).ErrorCode); Assert.AreEqual(0L, f.Policy.Recovery.Revision);
        await f.Replace("B", "C"); Assert.AreEqual("none", (await f.Checkpoints.ShowAsync(2, default)).ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC028_ClearedCaptureReservationCannotPublishLateOriginalApproval()
    {
        await using var f = await CheckpointFixture.Create("A");
        await f.Files.ReadFileAsync("a", default);
        f.WriteApproval.OnConfirm = _ => f.Checkpoints.Clear();
        var result = await f.Files.ReplaceTextAsync("a", "A", "B", default);
        Assert.AreEqual(FileWriteState.NotApplied, result.WriteState);
        Assert.AreEqual("A", await File.ReadAllTextAsync(f.Path));
        Assert.AreEqual("checkpoint_not_found", (await f.Checkpoints.ShowAsync(1, default)).ErrorCode);
    }

    [TestMethod]
    public async Task CKPTAC025_GuardCancellationAndUncertainCleanupCannotDowngradeIntoWriteApproval()
    {
        foreach (var code in new[] { "turn_cancelled", "process_cleanup_failed", "process_unconfirmed", "sensitive_content" })
        {
            var root = WorkspaceFixtures.NewDirectory(); Directory.CreateDirectory(System.IO.Path.Combine(root, ".git"));
            var target = System.IO.Path.Combine(root, "a"); await File.WriteAllTextAsync(target, "A");
            using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval,
                checkpointGitAdapter: new CheckpointFailingGitAdapter(code));
            await files.ReadFileAsync("a", default);
            var result = await files.ReplaceTextAsync("a", "A", "B", default);
            Assert.AreEqual(code == "turn_cancelled" ? WorkspaceStatus.Cancelled : code == "sensitive_content" ? WorkspaceStatus.Rejected : WorkspaceStatus.Failed, result.Status, code);
            Assert.AreEqual(FileWriteState.NotApplied, result.WriteState); Assert.AreEqual(0, approval.Calls);
            Assert.AreEqual("A", await File.ReadAllTextAsync(target));
        }
    }

    [TestMethod]
    public async Task CKPTAC008_InitialUnavailableGitWarnsAndRetainsManualWriteHistory()
    {
        var root = WorkspaceFixtures.NewDirectory(); Directory.CreateDirectory(System.IO.Path.Combine(root, ".git"));
        var target = System.IO.Path.Combine(root, "a"); await File.WriteAllTextAsync(target, "A");
        using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval,
            checkpointGitAdapter: new CheckpointFailingGitAdapter("git_unavailable"));
        await files.ReadFileAsync("a", default);
        Assert.AreEqual(FileWriteState.Verified, (await files.ReplaceTextAsync("a", "A", "B", default)).WriteState);
        Assert.IsTrue(approval.Preview!.DisplayBlock.Contains("git_unverified", StringComparison.Ordinal));
        Assert.IsTrue((await files.Checkpoints.ListAsync(default)).DisplayBlock!.Contains("ManualRequired；git_unverified", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CKPTAC006_UnconfirmedOriginalWriteConsumesOneManualRecord()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var fs = new EditFileSystem { Fault = "publish-after" };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
        Assert.AreEqual(FileWriteState.Unconfirmed, (await files.CreateFileAsync("a", "expected", default)).WriteState);
        var list = await files.Checkpoints.ListAsync(default);
        Assert.IsTrue(list.DisplayBlock!.Contains("容量：1/32", StringComparison.Ordinal));
        Assert.IsTrue(list.DisplayBlock.Contains("ManualRequired；source_unconfirmed", StringComparison.Ordinal));
        var approval = new CheckpointTestApproval();
        Assert.AreEqual("checkpoint_manual_required", (await files.Checkpoints.RestoreAsync(1, approval, default)).ErrorCode);
        Assert.AreEqual(0, approval.Calls); Assert.AreEqual("expected", await File.ReadAllTextAsync(System.IO.Path.Combine(root, "a")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CKPTAC008_NewRootGitPresenceAfterApprovalRefusesWithoutReadmittingLayout(bool restore)
    {
        await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B");
        Action change = () => Directory.CreateDirectory(System.IO.Path.Combine(f.Root, ".git"));
        if (restore)
        {
            var result = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval { OnConfirm = _ => change() }, default);
            Assert.AreEqual("checkpoint_git_conflict", result.ErrorCode);
        }
        else
        {
            f.WriteApproval.OnConfirm = _ => change(); await f.Files.ReadFileAsync("a", default);
            var result = await f.Files.ReplaceTextAsync("a", "B", "C", default);
            Assert.AreEqual(WorkspaceErrorCode.CheckpointGitConflict, result.ErrorCode);
        }
        Assert.AreEqual("B", await File.ReadAllTextAsync(f.Path)); Assert.AreEqual(0, f.Files.Snapshots.Count);
        Assert.IsTrue((await f.Checkpoints.ListAsync(default)).DisplayBlock!.Contains("ManualRequired", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CKPTAC012_InvalidUtf8BinaryAndOversizedExternalChangesPermanentlyBreakAnchor()
    {
        foreach (var external in new[] { new byte[] { 255 }, new byte[] { 0 }, new byte[131073] })
        {
            await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B");
            await f.Files.ReadFileAsync("a", default);
            await File.WriteAllBytesAsync(f.Path, external);
            Assert.AreEqual(FileWriteState.NotApplied, (await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default)).WriteState);
            Assert.AreEqual(0, f.Files.Snapshots.Count);
            await File.WriteAllTextAsync(f.Path, "B");
            Assert.AreEqual("checkpoint_manual_required", (await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default)).ErrorCode);
        }
    }

    [TestMethod]
    public async Task CKPTAC014_RootFailureObservedByRulesPreflightPermanentlyInvalidatesAllAnchors()
    {
        await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B");
        var moved = f.Root + "-temporarily-moved";
        Directory.Move(f.Root, moved);
        try { Assert.AreEqual("workspace_unavailable", await f.Policy.Rules!.ValidateAsync(default)); }
        finally { Directory.Move(moved, f.Root); }
        Assert.AreEqual("none", await f.Policy.Rules!.TryReloadAsync(default));
        f.Plans.SetMode(AgentMode.Execute);
        var result = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
        Assert.AreEqual("checkpoint_manual_required", result.ErrorCode);
        Assert.AreEqual("B", await File.ReadAllTextAsync(f.Path));
    }

    [TestMethod]
    public async Task CKPTAC004_BusySecondOperationNeverQueuesOrStartsAnotherApproval()
    {
        await using var f = await CheckpointFixture.Create("A"); await f.Replace("A", "B");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approval = new CheckpointTestApproval { OnConfirm = _ => entered.SetResult(), Wait = _ => finish.Task };
        var operation = f.Checkpoints.RestoreAsync(1, approval, default); await entered.Task;
        Assert.AreEqual("checkpoint_unavailable", (await f.Checkpoints.ListAsync(default)).ErrorCode);
        Assert.AreEqual(WorkspaceErrorCode.Unavailable, (await f.Files.ReadFileAsync("a", default)).ErrorCode);
        finish.SetResult(); Assert.AreEqual(FileWriteState.Verified, (await operation).WriteState); Assert.AreEqual(1, approval.Calls);
    }

    [TestMethod]
    public async Task CKPTAC015_PrepublicationFaultsNeverChangeTargetOrAdvanceEpoch()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var fault in new[] { "create", "write", "flush", "mode", "close", "staged-read", "staged-close" })
        {
            var fs = new EditFileSystem(); await using var f = await CheckpointFixture.Create("A", fs); await f.Replace("A", "B");
            var publications = fs.PublishCalls; fs.Fault = fault;
            var result = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
            Assert.AreEqual(FileWriteState.NotApplied, result.WriteState, fault); Assert.AreEqual(publications, fs.PublishCalls, fault);
            Assert.AreEqual("B", await File.ReadAllTextAsync(f.Path)); Assert.AreEqual(0L, f.Policy.Recovery.Revision);
            Assert.AreEqual(1, Directory.GetFileSystemEntries(f.Root).Length, fault);
        }
    }

    [TestMethod]
    public async Task CKPTAC015_PublicationOrReadbackFaultsRemainUnconfirmedAndBreakChain()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var fault in new[] { "publish-before", "publish-after", "different-bytes", "target-read", "target-close" })
        {
            var fs = new EditFileSystem(); await using var f = await CheckpointFixture.Create("A", fs); await f.Replace("A", "B");
            var publications = fs.PublishCalls;
            fs.OnPublish = () => fs.Fault = fault;
            var result = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
            Assert.AreEqual(FileWriteState.Unconfirmed, result.WriteState, fault); Assert.IsTrue(result.Fatal, fault);
            Assert.AreEqual(publications + 1, fs.PublishCalls, fault); Assert.AreEqual(1L, f.Policy.Recovery.Revision);
            Assert.AreEqual(1L, f.Plans.EvidenceEpoch); Assert.AreEqual(AgentMode.Plan, f.Plans.Mode);
            Assert.AreEqual(FileWriteState.Unconfirmed, f.Policy.Recovery.Last!.WriteState);
            Assert.IsTrue((await f.Checkpoints.ListAsync(default)).DisplayBlock!.Contains("source_unconfirmed", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task CKPTAC016_PostpublicationCancelClearAndDisposeKeepVerifiedFileFact()
    {
        if (!OperatingSystem.IsMacOS()) return;
        foreach (var action in new[] { "cancel", "clear", "dispose" })
        {
            var fs = new EditFileSystem(); var f = await CheckpointFixture.Create("A", fs); await f.Replace("A", "B");
            using var cancellation = new CancellationTokenSource(); Task? disposal = null;
            fs.OnPublish = () =>
            {
                if (action == "cancel") cancellation.Cancel();
                else if (action == "clear") { f.Checkpoints.Clear(); f.Policy.Recovery.Clear(); f.Files.Snapshots.Clear(); f.Plans.Clear(); }
                else disposal = f.Files.DisposeAsync().AsTask();
            };
            var result = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), cancellation.Token);
            Assert.AreEqual(FileWriteState.Verified, result.WriteState, action); Assert.AreEqual("A", await File.ReadAllTextAsync(f.Path));
            Assert.AreEqual(1L, f.Policy.Recovery.Revision); Assert.AreEqual(1L, f.Plans.EvidenceEpoch);
            if (action == "clear") { Assert.IsNull(f.Policy.Recovery.Last); Assert.AreEqual("checkpoint_not_found", (await f.Checkpoints.ShowAsync(1, default)).ErrorCode); }
            if (disposal is not null) await disposal;
            await f.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task CKPTAC016_CleanupFailurePreservesVerifiedStateAndStops()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var fs = new EditFileSystem(); await using var f = await CheckpointFixture.Create("A", fs); await f.Replace("A", "B");
        fs.Fault = "recreate-staged";
        var result = await f.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.IsTrue(result.CleanupFailed); Assert.IsTrue(result.Fatal);
        Assert.AreEqual("cleanup_failure", result.ErrorCode); Assert.AreEqual("A", await File.ReadAllTextAsync(f.Path));
        Assert.AreEqual(FileWriteState.Verified, f.Policy.Recovery.Last!.WriteState); Assert.IsTrue(f.Policy.Recovery.Last.CleanupFailed);
    }

    [TestMethod]
    public async Task CKPTAC022_RulesFileRestorationSettlesRecoveryBeforeStaleRules()
    {
        if (!OperatingSystem.IsMacOS()) return;
        var root = WorkspaceFixtures.NewDirectory(); var target = System.IO.Path.Combine(root, "AGENTS.md");
        await File.WriteAllTextAsync(target, "original project rule");
        using var session = WorkspaceFixtures.Session(root); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), execution);
        await files.ReadFileAsync("AGENTS.md", default);
        Assert.AreEqual(FileWriteState.Verified, (await files.ReplaceTextAsync("AGENTS.md", "original", "updated", default)).WriteState);
        Assert.AreEqual(ProjectRulesStatus.Stale, execution.Rules!.Capture().Status);
        Assert.AreEqual("rules_changed", (await files.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default)).ErrorCode);
        Assert.AreEqual("none", await execution.Rules.TryReloadAsync(default));
        plans.SetMode(AgentMode.Execute);
        var result = await files.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual(1L, plans.EvidenceEpoch);
        Assert.AreEqual(ProjectRulesStatus.Stale, execution.Rules.Capture().Status); Assert.AreEqual("original project rule", await File.ReadAllTextAsync(target));
    }
}

internal sealed class CheckpointTestApproval : ICheckpointApproval
{
    internal int Calls { get; private set; }
    internal CheckpointApprovalPreview? Preview { get; private set; }
    internal FileApprovalDecision Decision { get; set; } = FileApprovalDecision.Approved;
    internal Action<CheckpointApprovalPreview>? OnConfirm { get; set; }
    internal Func<CancellationToken, Task>? Wait { get; set; }
    public async ValueTask<FileApprovalDecision> ConfirmAsync(CheckpointApprovalPreview preview, CancellationToken token)
    { Calls++; Preview = preview; OnConfirm?.Invoke(preview); if (Wait is not null) await Wait(token); return Decision; }
}

internal sealed class CheckpointFailingGitAdapter(string code) : IGitReadAdapter
{
    public string? ExecutableIdentity => "synthetic-checkpoint-git";
    public ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new GitReadResult(code));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class CheckpointFixture : IAsyncDisposable
{
    internal string Root { get; } = WorkspaceFixtures.NewDirectory();
    internal string Path => System.IO.Path.Combine(Root, "a");
    internal BootstrapSession Session { get; }
    internal PlanStore Plans { get; }
    internal AgentExecutionPolicy Policy { get; }
    internal EditApproval WriteApproval { get; } = new();
    internal WorkspaceFiles Files { get; }
    internal ICheckpointWorkspace Checkpoints => Files.Checkpoints;
    private CheckpointFixture(IWorkspaceReadFileSystem? fileSystem = null)
    {
        Session = WorkspaceFixtures.Session(Root); Plans = new(Session.Policy); Policy = new(Plans);
        Files = new(Session.Workspace, Session.Policy, fileSystem ?? new MacWorkspaceFileSystem(), WriteApproval, Policy);
    }
    internal static async Task<CheckpointFixture> Create(string? original, IWorkspaceReadFileSystem? fileSystem = null)
    {
        var fixture = new CheckpointFixture(fileSystem);
        if (original is not null) await File.WriteAllBytesAsync(fixture.Path, Encoding.UTF8.GetBytes(original));
        return fixture;
    }
    internal async Task Replace(string oldText, string newText)
    {
        Assert.AreEqual(WorkspaceStatus.Success, (await Files.ReadFileAsync("a", default)).Status);
        Assert.AreEqual(FileWriteState.Verified, (await Files.ReplaceTextAsync("a", oldText, newText, default)).WriteState);
    }
    public async ValueTask DisposeAsync() { await Files.DisposeAsync(); Session.Dispose(); }
}