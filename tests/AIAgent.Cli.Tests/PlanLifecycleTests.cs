using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanLifecycleTests
{
    [TestMethod]
    public async Task PLANAC030_StaleInheritedOrInterposedGenerationCannotStartModelRequest()
    {
        foreach (var inherited in new[] { false, true })
        {
            using var f = new PlanFixture(); var execution = new AgentExecutionPolicy(f.Store);
            await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: execution);
            var client = new ScriptClient { Session = f.Session };
            var runner = new AgentRunner(client, PlanWorkflowTests.Registry(files, f, execution), f.Session.Policy, "test instruction", new(), _ => { });
            AgentTurnResult result;
            if (inherited)
            {
                using var previous = f.Store.BorrowGeneration();
                f.Store.Clear();
                result = await runner.RunAsync([LlmMessage.FromUserText("late")], default);
            }
            else result = await runner.RunAsync(new ClearingPlanMessages(f.Store.Clear), default);
            Assert.AreEqual(AgentTurnStatus.Cancelled, result.Status);
            Assert.AreEqual(0, client.Requests.Count); Assert.AreEqual(0, result.GenerationsStarted);
            Assert.IsNull(f.Store.Snapshot); Assert.AreEqual(0, f.Store.Facts.Count);
        }
    }

    [TestMethod]
    public void PLANAC030_ExpiredBorrowCannotProjectStateForNewRequest()
    {
        using var f = new PlanFixture();
        using var previous = f.Store.BorrowGeneration();
        f.Store.Clear();
        Assert.IsNull(f.Store.StateAppendix());
    }

    [TestMethod]
    public async Task PLANAC027_CompletedTaskEvidenceSurvivesFiveTurnHistoryEviction()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); var execution = new AgentExecutionPolicy(f.Store);
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
        var client = new ScriptClient
        {
            Session = f.Session,
            Respond = (i, request, _) => Task.FromResult(request.Tools.Count == 0 ? ChatFixture.Completed(ContextTestData.Summary) : i == 0 ? RunnerFixture.Calls(
            PlanWorkflowTests.Update(f, "in_progress"), PlanWorkflowTests.Call("read_file", new { path = "a" }, 1), PlanWorkflowTests.Update(f, "completed", 2)) : ChatFixture.Completed())
        };
        var context = new ConversationContext(); var coordinator = new ContextCoordinator(context, f.Store, f.Session.Policy);
        var runner = new AgentRunner(client, PlanWorkflowTests.Registry(files, f, execution), f.Session.Policy, "test instruction", new(), _ => { }, contextCoordinator: coordinator);
        for (var i = 0; i < 8; i++)
        {
            var result = await runner.RunAsync(context.BuildMessages(LlmMessage.FromUserText("q" + i)), default);
            Assert.AreEqual(AgentTurnStatus.Completed, result.Status); context.CommitCompletedTurn(result.CompletedTurn);
        }
        Assert.AreEqual(5, context.CompletedTurnCount); Assert.AreEqual(PlanTaskState.Completed, f.Task.State);
        Assert.AreEqual("op1", f.Task.CompletionFactIds.Single()); Assert.AreEqual(1, f.Store.Facts.Count);
        Assert.IsTrue(client.Requests[^1].SystemInstruction.Contains("\"completion_fact_ids\":[\"op1\"]", StringComparison.Ordinal));
        Assert.IsFalse(client.Requests[^1].Messages.Any(m => m.UserText == "q0"));
    }

    [TestMethod]
    public async Task PLANAC018_CancellationAtStartReadAndCompletionDecisionNeverLeavesInProgress()
    {
        foreach (var point in new[] { "start", "read", "complete", "model" })
        {
            using var f = new PlanFixture(); f.Submit(); f.Approve(); using var cancellation = new CancellationTokenSource();
            var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]); var execution = new AgentExecutionPolicy(f.Store);
            await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, fs, executionPolicy: execution);
            var client = new ScriptClient
            {
                Session = f.Session,
                Respond = (i, _, _) =>
            {
                if (i != 0 && point == "model") cancellation.Cancel();
                return Task.FromResult(i == 0 ? RunnerFixture.Calls(PlanWorkflowTests.Update(f, "in_progress"),
                    PlanWorkflowTests.Call("read_file", new { path = "a" }, 1), PlanWorkflowTests.Update(f, "completed", 2)) : ChatFixture.Completed());
            }
            };
            var runner = new AgentRunner(client, PlanWorkflowTests.Registry(files, f, execution), f.Session.Policy, "test instruction", new(), e =>
            {
                if (e.Kind == AgentEventKind.ExecutingTool && (point == "start" && e.PartIndex == 0 || point == "read" && e.PartIndex == 1 || point == "complete" && e.PartIndex == 2)) cancellation.Cancel();
            });
            var result = await runner.RunAsync([LlmMessage.FromUserText("work")], cancellation.Token);
            Assert.AreEqual(AgentTurnStatus.Cancelled, result.Status); Assert.AreNotEqual(PlanTaskState.InProgress, f.Task.State);
            Assert.AreEqual(point == "model" ? PlanTaskState.Completed : point == "start" ? PlanTaskState.Pending : PlanTaskState.Blocked, f.Task.State);
            Assert.AreEqual(point is "model" or "complete" ? 1 : 0, f.Store.Facts.Count);
            Assert.AreEqual(0, result.CompletedTurn.Count);
        }
    }

    [TestMethod]
    public async Task PLANAC017_UnconfirmedAndCleanupFactsOutrankCancellationWithActiveWriteTask()
    {
        foreach (var cleanup in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var plans = new PlanStore(session.Policy); plans.SetMode(AgentMode.Plan);
            plans.Submit(PlanFixture.Submission(kind: "write", tool: "create_file")); plans.Approve(plans.ApprovalPreview()!);
            using var cancel = new CancellationTokenSource(); var fs = new EditFileSystem { Fault = cleanup ? "cleanup" : "publish-after", OnPublish = cancel.Cancel };
            var execution = new AgentExecutionPolicy(plans);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval(), execution);
            var registry = new ToolRegistry([new CreateFileTool(files, session.Policy), new UpdateTaskTool(plans, session.Policy)], session.Policy, execution);
            var client = new ScriptClient
            {
                Session = session,
                Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
                PlanWorkflowTests.Call("update_task", PlanSdkTests.Update("in_progress")), PlanWorkflowTests.Call("create_file", new { path = "a", content = "A" }, 1)))
            };
            var result = await new AgentRunner(client, registry, session.Policy, "test instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("write")], cancel.Token);
            Assert.AreEqual(cleanup ? AgentStopReason.LocalFailure : AgentStopReason.WriteUnconfirmed, result.StopReason);
            Assert.AreEqual(FileWriteState.Unconfirmed, plans.Facts.Single().WriteState);
            Assert.AreEqual(cleanup, plans.Facts.Single().CleanupFailed);
            Assert.AreEqual(PlanTaskState.Blocked, plans.Snapshot!.Tasks[0].State);
        }
    }
}

internal sealed class ClearingPlanMessages(Action clear) : IReadOnlyList<LlmMessage>
{
    private bool _cleared;
    public int Count { get { if (!_cleared) { _cleared = true; clear(); } return 1; } }
    public LlmMessage this[int index] => index == 0 ? LlmMessage.FromUserText("late") : throw new ArgumentOutOfRangeException(nameof(index));
    public IEnumerator<LlmMessage> GetEnumerator() { yield return this[0]; }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}