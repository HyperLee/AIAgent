using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class CheckpointStateTests
{
    [TestMethod]
    public void CKPTAC020_RestoreExcludesAllEarlierFactsWithoutRebindingHistory()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); f.Update("in_progress");
        var request = new TaskRequirement("read_file", "a");
        f.Store.Reserve(request, false, out var old); f.Store.Record(old, ToolStatus.Success, "none");
        var fact = f.Store.Facts.Single(); var recovery = new AgentExecutionPolicy(f.Store).Recovery;
        recovery.Record(1, "a", WorkspaceWriteResult.Success(new("a", 1, false)), true);
        Assert.AreEqual(1L, recovery.Revision); Assert.AreEqual(1L, f.Store.EvidenceEpoch);
        Assert.AreEqual(AgentMode.Plan, f.Store.Mode); Assert.AreEqual(PlanApprovalState.Draft, f.Store.Snapshot!.ApprovalState);
        Assert.AreEqual("workspace_restored", f.Task.BlockReason); Assert.AreSame(fact, f.Store.Facts.Single());
        Assert.AreEqual(0L, fact.EvidenceEpoch); Assert.AreEqual(0L, fact.Context.RulesRevision);
        f.Approve(); f.Update("in_progress");
        Assert.AreEqual("evidence_required", f.Update("completed").ErrorCode);
        f.Store.Reserve(request, false, out var current); f.Store.Record(current, ToolStatus.Success, "none");
        Assert.AreEqual(1L, f.Store.Facts.Last().EvidenceEpoch); Assert.IsTrue(f.Update("completed").Success);
        CollectionAssert.AreEqual(new[] { "op2" }, f.Task.CompletionFactIds.ToArray());
    }

    [TestMethod]
    public void CKPTAC020_CompletedHistoryRemainsCompletedButExplicitlyUnverifiedAfterRestore()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); f.Update("in_progress");
        f.Store.Reserve(new("read_file", "a"), false, out var reservation); f.Store.Record(reservation, ToolStatus.Success, "none");
        f.Update("completed"); var completed = f.Task; var fact = f.Store.Facts.Single();
        f.Store.Recovery.Record(2, "a", WorkspaceWriteResult.Unconfirmed().WithCleanupFailure(), true);
        Assert.AreSame(completed, f.Task); Assert.AreEqual(PlanTaskState.Completed, f.Task.State);
        Assert.AreSame(fact, f.Store.Facts.Single()); Assert.AreEqual("op1", f.Task.CompletionFactIds.Single());
        StringAssert.Contains(f.Store.Display()!, "復原後未重新驗證");
        StringAssert.Contains(f.Store.StateAppendix()!, "completion_evidence_epoch");
        Assert.AreEqual(FileWriteState.Unconfirmed, f.Store.Recovery.Last!.WriteState); Assert.IsTrue(f.Store.Recovery.Last.CleanupFailed);
    }

    [TestMethod]
    public void CKPTAC027_ClearDiscardsLastWithoutReusingRecoveryEpochOrLateResult()
    {
        using var f = new PlanFixture(); var recovery = f.Store.Recovery;
        recovery.Record(1, "a", WorkspaceWriteResult.End(WorkspaceStatus.Rejected, WorkspaceErrorCode.Conflict), true);
        Assert.AreEqual(0L, recovery.Revision); Assert.AreEqual(0L, f.Store.EvidenceEpoch);
        recovery.Record(1, "a", WorkspaceWriteResult.Success(new("a", 4, true)), true);
        var revision = recovery.Revision; var epoch = f.Store.EvidenceEpoch;
        f.Store.Clear(); Assert.IsNull(recovery.Last); Assert.AreEqual("", recovery.BuildAppendix());
        Assert.AreEqual(revision, recovery.Revision); Assert.AreEqual(epoch, f.Store.EvidenceEpoch);
        recovery.Record(2, "a", WorkspaceWriteResult.Unconfirmed(), false);
        Assert.IsNull(recovery.Last); Assert.AreEqual(revision + 1, recovery.Revision); Assert.AreEqual(epoch + 1, f.Store.EvidenceEpoch);
    }

    [TestMethod]
    public void CKPTAC023_RecoveryInvalidatesMetricsCompactionAndSaveStampsEvenWithoutPlan()
    {
        using var session = WorkspaceFixtures.Session(); var context = new ConversationContext(); var plans = new PlanStore(session.Policy);
        var coordinator = new ContextCoordinator(context, plans, session.Policy); var stamp = coordinator.Capture(); var metric = new ContextMetricStamp(stamp);
        plans.Recovery.Record(1, "a", WorkspaceWriteResult.Success(new("a", 1, false)), true);
        Assert.IsFalse(coordinator.IsCurrent(stamp)); Assert.IsFalse(coordinator.IsCurrent(metric));
        Assert.AreEqual(1L, coordinator.Capture().RecoveryRevision);
        var current = coordinator.Capture(); plans.Recovery.Clear();
        Assert.IsTrue(coordinator.IsCurrent(current)); Assert.IsNull(plans.Recovery.Last);
    }

    [TestMethod]
    public async Task CKPTAC023_AllConversationAndCompactionRequestsMeasureMandatoryCurrentAppendix()
    {
        await using var f = new ContextFlowFixture(5);
        f.Plans.Recovery.Record(7, "a", WorkspaceWriteResult.Success(new("a", 9, true)), true);
        var measured = new List<LlmRequest>();
        f.Client.Inspect = request => { measured.Add(request); return new(ContextMeasureStatus.Measured, 1024); };
        var result = await f.Run(); Assert.AreEqual(AgentTurnStatus.Completed, result.Status);
        Assert.AreEqual(2, f.Client.Requests.Count);
        foreach (var request in measured.Concat(f.Client.Requests))
        {
            StringAssert.Contains(request.SystemInstruction, "checkpoint_id"); StringAssert.Contains(request.SystemInstruction, "c7");
            StringAssert.Contains(request.SystemInstruction, "較早對話／摘要／證據是歷史，需重讀目前來源");
        }
        Assert.AreEqual(9L, f.Plans.Recovery.Last!.VerifiedByteCount); Assert.IsTrue(f.Plans.Recovery.Last.VerifiedHasUtf8Bom);
        Assert.IsTrue(Encoding.UTF8.GetByteCount(f.Plans.Recovery.BuildAppendix()) <= 32768);
    }

    [TestMethod]
    public async Task CKPTAC023_WorkSaveSummaryIncludesCurrentRecoveryWithoutExtendingSaveSchema()
    {
        await using var f = new ContextFlowFixture(2);
        f.Plans.Recovery.Record(8, "a", WorkspaceWriteResult.Unconfirmed(), true);
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(ContextTestData.Summary));
        var stamp = f.Coordinator.Capture();
        var result = await f.Runner.RunWorkSaveSummaryAsync(stamp, f.Coordinator.PrepareWorkSource(stamp, new WorkProjectionInspector()), default);
        Assert.AreEqual(AgentTurnStatus.Completed, result.Status); Assert.AreEqual(1, f.Client.Requests.Count);
        StringAssert.Contains(f.Client.Requests.Single().SystemInstruction, "c8");
        StringAssert.Contains(f.Client.Requests.Single().SystemInstruction, "unconfirmed");
    }

    [TestMethod]
    public async Task CKPTAC024_RecoveryDuringMeasureNeverStartsStaleGeneration()
    {
        await using var f = new ContextFlowFixture(0);
        f.Client.Inspect = _ => { f.Plans.Recovery.Record(1, "a", WorkspaceWriteResult.Unconfirmed(), true); return new(ContextMeasureStatus.Measured, 1024); };
        var result = await f.Run(); Assert.AreEqual(0, result.GenerationsStarted); Assert.AreEqual(0, f.Client.Requests.Count);
        Assert.IsFalse(result.RequiresContextReset);
    }

    [TestMethod]
    public async Task CKPTAC023_RecoveryInvalidatesConfirmedSaveAndSaveV1HasNoCheckpointFields()
    {
        await using var f = new WorkFlowFixture(); Assert.AreEqual("none", f.Contexts.AddPin("safe local note"));
        var result = await f.Work.SaveAsync("work", (_, _) =>
        {
            f.Plans.Recovery.Record(1, "a", WorkspaceWriteResult.Success(new("a", 1, false)), true);
            return ValueTask.FromResult(WorkApprovalDecision.Approved);
        }, default);
        Assert.AreEqual("work_revision_conflict", result.ErrorCode); Assert.AreEqual(0, f.Store.Publishes);
        Assert.IsTrue(f.Work.ResumeExecutionConfirmationRequired);
        Assert.AreEqual("none", (await f.Work.ConfirmExecuteAsync(f.Approve, default)).ErrorCode);
        Assert.IsFalse(f.Work.ResumeExecutionConfirmationRequired);
        Assert.AreEqual("none", (await f.Work.SaveAsync("work", f.Approve, default)).ErrorCode);
        using var saved = JsonDocument.Parse(f.Store.Files.Single().Value.Bytes);
        Assert.IsFalse(saved.RootElement.EnumerateObject().Any(property => property.Name.Contains("checkpoint", StringComparison.Ordinal) || property.Name.Contains("recovery", StringComparison.Ordinal)));
        Assert.AreEqual(0, f.Client.Requests.Count);
    }

    [TestMethod]
    public void CKPTAC024_EveryCurrentAppendixFieldIsRecheckedAfterPolicyReplacement()
    {
        using var session = WorkspaceFixtures.Session(); using var replacement = WorkspaceFixtures.Session(key: "checkpoint_id");
        var plans = new PlanStore(session.Policy); plans.Recovery.Record(1, "a", WorkspaceWriteResult.Unconfirmed(), true);
        Assert.IsTrue(plans.Recovery.BuildAppendix().Length > 0);
        plans.BindPolicy(replacement.Policy);
        Assert.ThrowsExactly<ContextInspectionException>(() => plans.Recovery.BuildAppendix());
        plans.Clear(); Assert.AreEqual("", plans.Recovery.BuildAppendix()); Assert.AreEqual(1L, plans.EvidenceEpoch);
    }

    [TestMethod]
    public void CKPTAC024_UnsafeOutcomeNeverPersistsPathButStillSettlesPublicationEpoch()
    {
        using var session = WorkspaceFixtures.Session(key: "private-path"); var plans = new PlanStore(session.Policy);
        plans.Recovery.Record(1, "private-path", WorkspaceWriteResult.Unconfirmed(), true);
        Assert.IsNull(plans.Recovery.Last); Assert.IsTrue(plans.Recovery.RequiresContextReset);
        Assert.IsFalse(plans.Recovery.CanPublish); Assert.AreEqual(1L, plans.Recovery.Revision); Assert.AreEqual(1L, plans.EvidenceEpoch);
        Assert.ThrowsExactly<ContextInspectionException>(() => plans.Recovery.BuildAppendix());
        plans.Clear(); Assert.IsFalse(plans.Recovery.RequiresContextReset); Assert.AreEqual("", plans.Recovery.BuildAppendix());
    }


    [TestMethod]
    public async Task CKPTAC024_CompleteRequestEstimateAboveBodyLimitStartsZeroGenerations()
    {
        await using var f = new ContextFlowFixture(0);
        f.Plans.Recovery.Record(1, "a", WorkspaceWriteResult.Unconfirmed(), true);
        f.Client.Inspect = _ => new(ContextMeasureStatus.Measured, 262145);
        var result = await f.Run();
        Assert.AreEqual(AgentStopReason.ContextCapacity, result.StopReason);
        Assert.AreEqual(0, result.GenerationsStarted); Assert.AreEqual(0, f.Client.Requests.Count);
        Assert.IsNotNull(f.Plans.Recovery.Last); Assert.IsFalse(result.RequiresContextReset);
    }


    [TestMethod]
    public void CKPTAC020_PreRecoveryValidationFactsCannotCompleteAfterRecovery()
    {
        using var f = new PlanFixture(); f.Store.ProcessCapabilityAvailable = true;
        Assert.IsTrue(f.Store.Submit(GeminiFixtures.Element("""
            {"expected_revision":0,"objective":"verify","tasks":[{"kind":"validation","title":"verify","acceptance":"passed","requirements_version":2,"requirements":[{"tool":"run_dotnet_task","path":"a.csproj","task":"build"}]}]}
            """)).Success);
        f.Approve(); f.Update("in_progress");
        var success = new ProcessExecutionSummary(DotnetTaskKind.Build, "a.csproj", ProcessStartState.Started, ProcessEndState.Exited,
            0, 1, true, false, ValidationOutcome.BuildSucceeded);
        f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out var old);
        Assert.AreEqual(0L, old!.EvidenceEpoch); Assert.IsFalse(f.Store.Recovery.CanPublish);
        f.Store.RecordProcess(old, ToolStatus.Success, "none", success); Assert.IsTrue(f.Store.Recovery.CanPublish);
        f.Store.Recovery.Record(1, "a", WorkspaceWriteResult.Success(new("a", 1, false)), true);
        f.Approve(); f.Update("in_progress"); Assert.AreEqual("evidence_required", f.Update("completed").ErrorCode);
        f.Store.ReserveProcess(DotnetTaskKind.Build, "a.csproj", out var current); Assert.AreEqual(1L, current!.EvidenceEpoch);
        f.Store.RecordProcess(current, ToolStatus.Success, "none", success); Assert.IsTrue(f.Update("completed").Success);
        Assert.AreEqual(0L, f.Store.Facts[0].EvidenceEpoch); Assert.AreEqual(1L, f.Store.Facts[1].EvidenceEpoch);
        CollectionAssert.AreEqual(new[] { "op2" }, f.Task.CompletionFactIds.ToArray());
    }

    [DataRow("_revision")]
    [DataRow("_evidenceEpoch")]
    [DataRow("_generation")]
    [TestMethod]
    public void CKPTAC024_OverflowAdmissionRefusesBeforePublicationWithoutPartialEpoch(string field)
    {
        using var session = WorkspaceFixtures.Session(); var plans = new PlanStore(session.Policy); var recovery = plans.Recovery;
        var target = field == "_revision" ? (object)recovery : plans;
        target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(target, long.MaxValue);
        var revision = recovery.Revision; var epoch = plans.EvidenceEpoch; var generation = plans.Generation;
        Assert.IsFalse(recovery.CanPublish);
        Assert.ThrowsExactly<OverflowException>(() => recovery.Record(1, "a", WorkspaceWriteResult.Unconfirmed(), true));
        Assert.AreEqual(revision, recovery.Revision); Assert.AreEqual(epoch, plans.EvidenceEpoch); Assert.AreEqual(generation, plans.Generation);
        Assert.IsNull(recovery.Last);
    }


    [TestMethod]
    public async Task CKPTAC027_LazyWorkCoordinatorAfterClearDoesNotInventRecoveryConfirmation()
    {
        await using var f = new WorkFlowFixture();
        f.Plans.Recovery.Record(1, "a", WorkspaceWriteResult.Unconfirmed(), true); f.Plans.Clear();
        var later = new WorkSessionCoordinator(f.Contexts, f.Plans, f.Rules, f.Files, f.Store, f.Session.Policy, null, null, f.Budget);
        Assert.IsFalse(later.ResumeExecutionConfirmationRequired);
        f.Plans.Recovery.Record(2, "a", WorkspaceWriteResult.Success(new("a", 1, false)), true);
        Assert.IsTrue(later.ResumeExecutionConfirmationRequired);
    }

}