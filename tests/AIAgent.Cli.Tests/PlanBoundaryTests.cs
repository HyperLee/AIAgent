using System.Reflection;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanBoundaryTests
{
    [TestMethod]
    public void PLANAC025_AnalysisCompletionRespectsOutstandingIdentityReservations()
    {
        using var f = new PlanFixture(); f.Submit(kind: "analysis"); f.Approve(); f.Update("in_progress");
        typeof(PlanStore).GetField("_operationSequence", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Store, long.MaxValue - 1);
        Assert.AreEqual("none", f.Store.Reserve(new("read_file", "a"), false, out var reservation));
        Assert.AreEqual("unavailable", f.Update("completed").ErrorCode);
        Assert.AreEqual(PlanTaskState.InProgress, f.Task.State);
        f.Store.Record(reservation, ToolStatus.Success, "none");
        Assert.AreEqual("op" + long.MaxValue, f.Store.Facts.Single().OperationId);
    }

    [TestMethod]
    public void PLANAC005_AnalysisCannotShiftReservedOperationsOntoSensitiveIdentity()
    {
        using var session = WorkspaceFixtures.Session(key: "op2"); var store = new PlanStore(session.Policy); store.SetMode(AgentMode.Plan);
        Assert.IsTrue(store.Submit(PlanFixture.Submission(kind: "analysis")).Success); store.Approve(store.ApprovalPreview()!);
        store.Update(JsonSerializer.SerializeToElement(PlanSdkTests.Update("in_progress")));
        Assert.AreEqual("none", store.Reserve(new("read_file", "a"), false, out var reservation));
        Assert.AreEqual("sensitive_content", store.Update(JsonSerializer.SerializeToElement(PlanSdkTests.Update("completed"))).ErrorCode);
        store.Record(reservation, ToolStatus.Success, "none");
        Assert.AreEqual("op1", store.Facts.Single().OperationId);
    }

    [TestMethod]
    public void PLANAC008_OverflowDoesNotRecycleIdentifiersOrInvalidateNoChange()
    {
        using var f = new PlanFixture(); f.Submit(); var before = f.Store.Snapshot!;
        typeof(PlanStore).GetField("_plan", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Store,
            new PlanSnapshot(before.PlanId, long.MaxValue, before.Objective, before.ApprovalState, before.Tasks));
        Assert.AreEqual("no_change", f.Submit(long.MaxValue, "t1").ErrorCode);
        Assert.AreEqual("unavailable", f.Submit(long.MaxValue, "t1", title: "different").ErrorCode);
        Assert.AreEqual(long.MaxValue, f.Store.Snapshot!.Revision);
        f.Store.Clear(); Assert.IsTrue(f.Submit().Success);
        Assert.AreEqual("p2", f.Store.Snapshot!.PlanId); Assert.AreEqual("t2", f.Task.TaskId);
    }

    [TestMethod]
    public async Task PLANAC005_ShortCredentialMatchingGeneratedFactIdStopsBeforeIo()
    {
        using var session = WorkspaceFixtures.Session(key: "op1"); var store = new PlanStore(session.Policy); store.SetMode(AgentMode.Plan);
        Assert.IsTrue(store.Submit(PlanFixture.Submission()).Success); Assert.IsTrue(store.Approve(store.ApprovalPreview()!));
        Assert.IsTrue(store.Update(JsonSerializer.SerializeToElement(PlanSdkTests.Update("in_progress"))).Success);
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: new(store));
        Assert.AreEqual(WorkspaceErrorCode.SensitiveContent, (await files.ReadFileAsync("a", default)).ErrorCode);
        Assert.AreEqual(0, fs.Opened); Assert.AreEqual(0, fs.ProductInspectionCount); Assert.AreEqual(0, store.Facts.Count);
    }

    [TestMethod]
    public void PLANAC007_TaskRequirementAndTextBoundariesAreAtomic()
    {
        foreach (var taskCount in new[] { 20, 21 })
        {
            using var f = new PlanFixture();
            var tasks = Enumerable.Range(0, taskCount).Select(_ => new { kind = "analysis", title = "task", acceptance = "checked", requirements = Array.Empty<object>() });
            Assert.AreEqual(taskCount == 20 ? "none" : "invalid_arguments", f.Store.Submit(JsonSerializer.SerializeToElement(new { expected_revision = 0, objective = "o", tasks })).ErrorCode);
        }
        foreach (var count in new[] { 8, 9 })
        {
            using var f = new PlanFixture(); var requirements = Enumerable.Range(0, count).Select(i => new { tool = "read_file", path = "file" + i });
            Assert.AreEqual(count == 8 ? "none" : "invalid_arguments", f.Store.Submit(JsonSerializer.SerializeToElement(new
            {
                expected_revision = 0,
                objective = "o",
                tasks = new[] {
                new { kind = "read", title = "task", acceptance = "checked", requirements } }
            })).ErrorCode);
        }
        foreach (var field in new[] { "objective", "title", "acceptance" })
            foreach (var extra in new[] { 0, 1 })
            {
                using var f = new PlanFixture(); var limit = field == "objective" ? 2048 : field == "title" ? 256 : 1024;
                var task = new Dictionary<string, object> { ["kind"] = "analysis", ["title"] = "task", ["acceptance"] = "checked", ["requirements"] = Array.Empty<object>() };
                if (field != "objective") task[field] = new string('x', limit + extra);
                var args = JsonSerializer.SerializeToElement(new { expected_revision = 0, objective = field == "objective" ? new string('x', limit + extra) : "o", tasks = new[] { task } });
                Assert.AreEqual(extra == 0 ? "none" : "invalid_arguments", f.Store.Submit(args).ErrorCode);
            }
    }

    [TestMethod]
    public void PLANAC010_NewObjectiveCannotRebindRetiredEvidence()
    {
        using var f = new PlanFixture(); f.Submit(kind: "analysis"); f.Approve(); f.Update("in_progress"); f.Update("completed");
        var fact = f.Store.Facts.Single(); f.Store.SetMode(AgentMode.Plan);
        Assert.IsTrue(f.Submit(1, kind: "analysis", objective: "second objective").Success); f.Approve();
        Assert.AreEqual("p2", f.Store.Snapshot!.PlanId); Assert.AreEqual("t2", f.Task.TaskId);
        Assert.AreEqual("p1", fact.Context.PlanId); Assert.AreEqual(1L, fact.Context.PlanRevision);
        Assert.AreEqual(PlanTaskState.Pending, f.Task.State); Assert.AreEqual(0, f.Task.CompletionFactIds.Count); Assert.AreEqual(1, f.Store.Facts.Count);
    }

    [TestMethod]
    public async Task PLANAC019_RetainedApprovalDoesNotAuthorizePlanWritesOrRefreshSnapshots()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); f.Update("in_progress");
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: new(f.Store));
        await files.ReadFileAsync("a", default); var count = fs.ProductInspectionCount;
        f.Store.SetMode(AgentMode.Plan);
        Assert.AreEqual("mode_changed", f.Task.BlockReason); Assert.AreEqual(PlanApprovalState.Approved, f.Store.Snapshot!.ApprovalState);
        Assert.AreEqual(WorkspaceErrorCode.ModeRestricted, (await files.CreateFileAsync("b", "B", default)).ErrorCode);
        Assert.AreEqual(count, fs.ProductInspectionCount); Assert.AreEqual(1, files.Snapshots.Count);
        f.Store.SetMode(AgentMode.Execute); Assert.AreEqual(PlanTaskState.Blocked, f.Task.State);
    }

    [TestMethod]
    public async Task PLANAC030_ClearAtModelReturnPreventsLateInitialPlanFromBeingCreated()
    {
        using var f = new PlanFixture(); var execution = new AgentExecutionPolicy(f.Store);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: execution);
        var client = new ScriptClient
        {
            Session = f.Session,
            Respond = (_, _, _) =>
        {
            f.Store.Clear(); return Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, null, "submit_plan", PlanFixture.Submission())));
        }
        };
        var result = await new AgentRunner(client, PlanWorkflowTests.Registry(files, f, execution), f.Session.Policy, "test instruction", new(), _ => { })
            .RunAsync([LlmMessage.FromUserText("plan")], default);
        Assert.AreEqual(AgentTurnStatus.Cancelled, result.Status); Assert.IsNull(f.Store.Snapshot); Assert.AreEqual(0, f.Store.Facts.Count);
    }

    [TestMethod]
    public void PLANAC032_PlanContractsAreInternalSealedAndHaveNoPublicSerializationSurface()
    {
        foreach (var type in new[] { typeof(PlanSnapshot), typeof(PlanTaskSnapshot), typeof(TaskRequirement), typeof(TaskExecutionContext),
            typeof(TaskOperationFact), typeof(PlanApprovalPreview), typeof(PlanStore), typeof(AgentExecutionPolicy) })
        {
            Assert.IsTrue(type.IsSealed && !type.IsPublic);
            Assert.AreEqual(0, type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length);
        }
    }

    [TestMethod]
    public async Task PLANAC030_StoppedPlanToolReturnsUnavailableAndCancellationIsNotRejection()
    {
        using var f = new PlanFixture(); var tool = new SubmitPlanTool(f.Store, f.Session.Policy);
        f.Store.Stop();
        Assert.AreEqual(ToolStatus.Failed, (await tool.ExecuteAsync(PlanFixture.Submission(), default)).Status);
        var cancelled = PlanProjection.Result(new("turn_cancelled"), true, f.Session.Policy);
        Assert.AreEqual(ToolStatus.Cancelled, cancelled.Status); Assert.AreEqual("none", cancelled.ErrorCode);
    }
}