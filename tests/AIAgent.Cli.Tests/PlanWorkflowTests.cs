using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanWorkflowTests
{
    [TestMethod]
    public async Task PLANAC025_ExhaustedEvidenceRejectsActualReadBeforeMetadata()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); f.Update("in_progress");
        for (var i = 0; i < 256; i++)
        {
            Assert.AreEqual("none", f.Store.Reserve(new("read_file", "a"), false, out var reservation));
            f.Store.Record(reservation, ToolStatus.Success, "none");
        }
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: new(f.Store));
        Assert.AreEqual(WorkspaceErrorCode.EvidenceLimit, (await files.ReadFileAsync("a", default)).ErrorCode);
        Assert.AreEqual(0, fs.Opened); Assert.AreEqual(0, fs.ProductInspectionCount);
        Assert.AreEqual(256, f.Store.Facts.Count); Assert.AreEqual("resource_limit", f.Task.BlockReason);
    }

    [TestMethod]
    public async Task PLANAC002_ModeIsCheckedAtRegistryAndActualWorkspaceBeforeMetadata()
    {
        using var f = new PlanFixture(); var fs = new ControlledWorkspaceFileSystem(); var execution = new AgentExecutionPolicy(f.Store);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
        var registry = Registry(files, f, execution);
        var call = Call("create_file", new { path = "a", content = "A" });
        Assert.AreEqual("mode_restricted", (await registry.DispatchAsync(call, default)).ErrorCode);
        Assert.AreEqual(WorkspaceErrorCode.ModeRestricted, (await files.CreateFileAsync("a", "A", default)).ErrorCode);
        Assert.AreEqual(WorkspaceErrorCode.ModeRestricted, (await files.ReplaceTextAsync("a", "A", "B", default)).ErrorCode);
        Assert.AreEqual(0, fs.ProductInspectionCount); Assert.AreEqual(0, fs.Opened);
        Assert.AreEqual("unknown_tool", (await registry.DispatchAsync(Call("shell", new { }), default)).ErrorCode);
    }

    [TestMethod]
    public async Task PLANAC021_MixedModeBatchAndSensitiveTailPerformNoEarlierOperations()
    {
        foreach (var sensitive in new[] { false, true })
        {
            using var f = new PlanFixture(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
            var execution = new AgentExecutionPolicy(f.Store);
            await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
            var calls = new[] { Call("read_file", new { path = "a" }), Call("create_file", new { path = "b", content = sensitive ? GeminiFixtures.Key : "B" }, 1) };
            var client = new ScriptClient { Session = f.Session, Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(calls)) };
            var result = await Runner(client, Registry(files, f, execution), f).RunAsync([LlmMessage.FromUserText("explore")], default);
            Assert.AreEqual(sensitive ? AgentStopReason.SensitiveData : AgentStopReason.ModeRestricted, result.StopReason);
            Assert.AreEqual(2, result.Operations.Count); Assert.AreEqual(ToolStatus.NotExecuted, result.Operations[0].Status);
            Assert.AreEqual(0, fs.ProductInspectionCount); Assert.AreEqual(0, fs.Opened); Assert.AreEqual(1, client.Requests.Count);
        }
    }

    [TestMethod]
    public async Task PLANAC012_ActivePlanRequiresApprovedMatchingWriteTaskAtDirectEntry()
    {
        using var f = new PlanFixture(); var fs = new ControlledWorkspaceFileSystem(); var execution = new AgentExecutionPolicy(f.Store);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
        f.Store.Submit(PlanFixture.Submission(kind: "write", tool: "create_file")); f.Store.SetMode(AgentMode.Execute);
        Assert.AreEqual(WorkspaceErrorCode.PlanNotApproved, (await files.CreateFileAsync("a", "A", default)).ErrorCode);
        f.Store.SetMode(AgentMode.Plan); f.Approve();
        Assert.AreEqual(WorkspaceErrorCode.TaskRequired, (await files.CreateFileAsync("a", "A", default)).ErrorCode);
        f.Update("in_progress");
        Assert.AreEqual(WorkspaceErrorCode.TaskScopeMismatch, (await files.CreateFileAsync("b", "B", default)).ErrorCode);
        Assert.AreEqual(WorkspaceErrorCode.TaskScopeMismatch, (await files.ReplaceTextAsync("a", "A", "B", default)).ErrorCode);
        Assert.AreEqual(0, fs.ProductInspectionCount); Assert.AreEqual(0, f.Store.Facts.Count);
    }

    [TestMethod]
    public async Task PLANAC013_SequentialTaskReadAndCompleteUseActualWorkspaceFacts()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve();
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]); var execution = new AgentExecutionPolicy(f.Store);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
        var client = new ScriptClient
        {
            Session = f.Session,
            Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(
            Update(f, "in_progress"), Call("read_file", new { path = "./a" }, 1), Update(f, "completed", 2)) : ChatFixture.Completed())
        };
        var result = await Runner(client, Registry(files, f, execution), f).RunAsync([LlmMessage.FromUserText("read")], default);
        Assert.AreEqual(AgentTurnStatus.Completed, result.Status); Assert.AreEqual(PlanTaskState.Completed, f.Task.State);
        Assert.AreEqual(TaskCompletionBasis.LocalOperation, f.Task.CompletionBasis); Assert.AreEqual("op1", f.Task.CompletionFactIds.Single());
        Assert.AreEqual("a", f.Store.Facts.Single().RelativePath); Assert.AreEqual(1L, f.Store.Facts.Single().ByteCount);
        Assert.AreEqual(2, client.Requests.Count); Assert.AreEqual(4, result.CompletedTurn.Count);
        Assert.AreEqual("[LocalToolOutcome]", result.Operations[1].ToString());
    }

    [TestMethod]
    public async Task PLANAC015_RangeFactsKeepRequestedAndActualRangesWithoutCreatingSnapshot()
    {
        foreach (var empty in new[] { false, true })
        {
            using var f = new PlanFixture();
            f.Store.Submit(PlanFixture.Submission(tool: "read_file_range", start: 1, end: 100)); f.Approve(); f.Update("in_progress");
            var fs = new ControlledWorkspaceFileSystem(); fs.File("a", empty ? [] : [65, 10, 66]);
            await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: new(f.Store));
            var result = await files.ReadFileRangeAsync("a", 1, 100, default);
            Assert.AreEqual(WorkspaceStatus.Success, result.Status);
            Assert.AreEqual(100, f.Store.Facts.Single().Request!.EndLine);
            Assert.AreEqual(empty ? null : 2, f.Store.Facts.Single().DiscoverySummary!.EndLine);
            Assert.AreEqual(0, files.Snapshots.Count); Assert.IsTrue(f.Update("completed").Success);
        }
    }

    [TestMethod]
    public async Task PLANAC017_DeclinedSecondWritePreservesFirstFactAndStopsBatch()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var plans = new PlanStore(session.Policy); plans.SetMode(AgentMode.Plan);
        plans.Submit(JsonSerializer.SerializeToElement(new
        {
            expected_revision = 0,
            objective = "two writes",
            tasks = new[] {
            new { kind = "write", title = "write", acceptance = "two files", requirements = new[] { new { tool = "create_file", path = "a" }, new { tool = "create_file", path = "b" } } } }
        }));
        plans.Approve(plans.ApprovalPreview()!); var policy = new AgentExecutionPolicy(plans);
        var approval = new EditApproval { OnConfirm = p => { } };
        approval.OnConfirm = p => approval.Decision = p.RelativePath == "a" ? FileApprovalDecision.Approved : FileApprovalDecision.Declined;
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval, policy);
        var registry = TestRulesRegistry.Create([new CreateFileTool(files, session.Policy), new UpdateTaskTool(plans, session.Policy)], session.Policy, policy);
        var start = Call("update_task", new { plan_id = "p1", expected_revision = 1, task_id = "t1", status = "in_progress", summary = "starting" });
        var client = new ScriptClient
        {
            Session = session,
            Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(start,
            Call("create_file", new { path = "a", content = "A" }, 1), Call("create_file", new { path = "b", content = "B" }, 2),
            Call("update_task", new { plan_id = "p1", expected_revision = 1, task_id = "t1", status = "completed", summary = "done" }, 3)))
        };
        var result = await new AgentRunner(client, registry, session.Policy, "test instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("write")], default);
        Assert.AreEqual(AgentStopReason.ApprovalDeclined, result.StopReason); Assert.AreEqual(1, client.Requests.Count);
        Assert.AreEqual(2, approval.Calls); Assert.AreEqual("A", await File.ReadAllTextAsync(Path.Combine(root, "a")));
        Assert.IsFalse(File.Exists(Path.Combine(root, "b"))); Assert.AreEqual(2, plans.Facts.Count);
        Assert.AreEqual(FileWriteState.Verified, plans.Facts[0].WriteState); Assert.AreEqual("approval_declined", plans.Snapshot!.Tasks[0].BlockReason);
        Assert.AreEqual(ToolStatus.NotExecuted, result.Operations[^1].Status);
    }

    [TestMethod]
    public async Task PLANAC018_NormalTurnPausesTaskWhileLaterModelFailureKeepsAcceptedCompletion()
    {
        foreach (var complete in new[] { false, true })
        {
            using var f = new PlanFixture(); f.Submit(kind: "analysis"); f.Approve();
            var fs = new ControlledWorkspaceFileSystem(); var execution = new AgentExecutionPolicy(f.Store);
            await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
            var client = new ScriptClient
            {
                Session = f.Session,
                Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(complete ?
                [Update(f, "in_progress"), Update(f, "completed", 1)] : [Update(f, "in_progress")]) : complete ?
                new LlmResult(LlmStatus.Failed, LlmErrorCode.NetworkFailure, "", [], null, null, true, false) : ChatFixture.Completed())
            };
            var result = await Runner(client, Registry(files, f, execution), f).RunAsync([LlmMessage.FromUserText("analyze")], default);
            Assert.AreEqual(complete ? PlanTaskState.Completed : PlanTaskState.Blocked, f.Task.State);
            if (!complete) { Assert.AreEqual("turn_paused", f.Task.BlockReason); Assert.AreEqual(AgentTurnStatus.Completed, result.Status); }
            else { Assert.AreEqual(TaskCompletionBasis.ModelAssessment, f.Task.CompletionBasis); Assert.AreEqual(0, result.CompletedTurn.Count); }
        }
    }

    [TestMethod]
    public async Task PLANAC022_FifthGenerationRejectsTaskUpdatesBeforeStateOrOperations()
    {
        using var f = new PlanFixture(); f.Submit(kind: "analysis"); f.Approve();
        var fs = new ControlledWorkspaceFileSystem(); var execution = new AgentExecutionPolicy(f.Store);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
        var client = new ScriptClient
        {
            Session = f.Session,
            Respond = (i, _, _) => Task.FromResult(RunnerFixture.Calls(i < 4 ?
            Call("list_files", new { path = "." }) : Update(f, "in_progress")))
        };
        var result = await Runner(client, Registry(files, f, execution), f).RunAsync([LlmMessage.FromUserText("loop")], default);
        Assert.AreEqual(AgentStopReason.GenerationLimit, result.StopReason); Assert.AreEqual(5, client.Requests.Count);
        Assert.AreEqual(PlanTaskState.Pending, f.Task.State); Assert.AreEqual(0, f.Store.Facts.Count);
    }

    [TestMethod]
    public async Task PLANAC031_UnspecifiedCapabilitiesCannotDispatchOrBeAdvertised()
    {
        using var f = new PlanFixture(); var tool = new UnspecifiedPlanTool();
        var registry = TestRulesRegistry.Create([tool], f.Session.Policy, new(f.Store));
        Assert.AreEqual(0, registry.Definitions.Count);
        Assert.AreEqual("mode_restricted", (await registry.DispatchAsync(Call("future_process", new { exit_code = 0 }), default)).ErrorCode);
        Assert.AreEqual(0, tool.Calls);
    }

    internal static LlmToolCall Call(string name, object args, int index = 0) => new(index, "c" + index, name, JsonSerializer.SerializeToElement(args));
    internal static LlmToolCall Update(PlanFixture f, string status, int index = 0) => Call("update_task", new
    {
        plan_id = f.Store.Snapshot!.PlanId,
        expected_revision = f.Store.Snapshot.Revision,
        task_id = f.Task.TaskId,
        status,
        summary = "summary"
    }, index);
    internal static ToolRegistry Registry(IWorkspaceFiles files, PlanFixture f, AgentExecutionPolicy execution) => TestRulesRegistry.Create([
        new ListFilesTool(files, f.Session.Policy), new ReadFileTool(files, f.Session.Policy), new CreateFileTool(files, f.Session.Policy),
        new ReplaceTextTool(files, f.Session.Policy), new SearchFilesTool(files, f.Session.Policy), new SearchTextTool(files, f.Session.Policy),
        new ReadFileRangeTool(files, f.Session.Policy), new SubmitPlanTool(f.Store, f.Session.Policy), new UpdateTaskTool(f.Store, f.Session.Policy)], f.Session.Policy, execution);
    private static AgentRunner Runner(ScriptClient client, ToolRegistry registry, PlanFixture f) => new(client, registry, f.Session.Policy, "test instruction", new(), _ => { });
}

internal sealed class UnspecifiedPlanTool : ITool
{
    internal int Calls { get; private set; }
    public LlmToolDefinition Definition { get; } = WorkspaceToolProjection.Definition("future_process", "unavailable");
    public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    { Calls++; throw new InvalidOperationException("must not execute"); }
}