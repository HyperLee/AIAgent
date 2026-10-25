using System.Reflection;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ContextPlanProcessTests
{
    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task CTXAC030_CompletedAAndBlockedBSurviveBudgetOrCapacityStop(bool capacity)
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
        var context = new ConversationContext(); var plans = new PlanStore(session.Policy, context.DecisionGate);
        plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(plans.Submit(JsonSerializer.SerializeToElement(new
        {
            expected_revision = 0,
            objective = "write A then B",
            tasks = new[]
            {
                new { kind = "write", title = "A", acceptance = "verified", requirements = new[] { new { tool = "create_file", path = "a" } } },
                new { kind = "write", title = "B", acceptance = "verified", requirements = new[] { new { tool = "create_file", path = "b" } } }
            }
        })).Success);
        Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!)); var plan = plans.Snapshot!;
        var execution = new AgentExecutionPolicy(plans); var approval = new EditApproval(); var approved = new List<string>();
        approval.OnConfirm = preview => approved.Add(preview.RelativePath);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval, execution);
        var client = new ScriptClient { Session = session };
        LlmToolCall Update(int part, int task, string status) => new(part, null, "update_task", JsonSerializer.SerializeToElement(new
        { plan_id = plan.PlanId, expected_revision = plan.Revision, task_id = plan.Tasks[task].TaskId, status, summary = "local work" }));
        LlmToolCall Create(int part, string path) => new(part, null, "create_file", JsonSerializer.SerializeToElement(new { path, content = path.ToUpperInvariant() }));
        client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(Update(0, 0, "in_progress"), Create(1, "a"), Update(2, 0, "completed"), Update(3, 1, "in_progress")) : RunnerFixture.Calls(Create(0, "b")));
        if (capacity) client.Inspect = _ => new(client.Requests.Count == 0 ? ContextMeasureStatus.Measured : ContextMeasureStatus.Unavailable, client.Requests.Count == 0 ? 1024 : null);
        var budget = new GenerationBudget(context.DecisionGate); budget.SetTurnLimit(2);
        var registry = new ToolRegistry([new CreateFileTool(files, session.Policy), new UpdateTaskTool(plans, session.Policy)], session.Policy, execution);
        var coordinator = new ContextCoordinator(context, plans, session.Policy);
        var runner = new AgentRunner(client, registry, session.Policy, "test", new(), _ => { }, budget, coordinator, client);
        var result = await runner.RunAsync([LlmMessage.FromUserText("start")], default);
        Assert.AreEqual(capacity ? AgentStopReason.ContextCapacity : AgentStopReason.GenerationLimit, result.StopReason);
        Assert.AreEqual(PlanTaskState.Completed, plans.Snapshot!.Tasks[0].State);
        Assert.AreEqual(TaskCompletionBasis.LocalOperation, plans.Snapshot.Tasks[0].CompletionBasis);
        Assert.AreEqual(PlanTaskState.Blocked, plans.Snapshot.Tasks[1].State);
        Assert.AreEqual(capacity ? "resource_limit" : "generation_limit", plans.Snapshot.Tasks[1].BlockReason);
        Assert.AreEqual("A", await File.ReadAllTextAsync(Path.Combine(root, "a"))); Assert.IsFalse(File.Exists(Path.Combine(root, "b")));
        var calls = client.Requests.Count; budget.RestoreDefaults(); Assert.AreEqual(calls, client.Requests.Count);
        client.Inspect = null;
        client.Respond = (i, _, _) => Task.FromResult(i == calls ? RunnerFixture.Calls(Update(0, 1, "in_progress"), Create(1, "b"), Update(2, 1, "completed")) : ChatFixture.Completed());
        var resumed = await runner.RunAsync([LlmMessage.FromUserText("continue B")], default);
        Assert.AreEqual(AgentTurnStatus.Completed, resumed.Status);
        CollectionAssert.AreEqual(new[] { "a", "b" }, approved);
        Assert.AreEqual(2, plans.Facts.Count); Assert.AreEqual("A", await File.ReadAllTextAsync(Path.Combine(root, "a")));
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task CTXAC031_TwentyGenerationsCannotResetRealProcessBudget()
    {
        using var session = WorkspaceFixtures.Session(); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans) { ProcessCapabilityAvailable = true };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: execution);
        await using var processes = new ControlledDotnetTasks(session.Workspace, session.Policy, execution, files,
            (_, _) => throw new InvalidOperationException("Exhausted process budget must not reach approval."), _ => { });
        var processBudget = (ProcessTurnBudget)typeof(ControlledDotnetTasks).GetField("_budget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(processes)!;
        for (var i = 0; i < 2; i++) { Assert.IsTrue(processBudget.TryStart(DotnetTaskKind.Build, out var lease)); lease!.Dispose(); }
        var client = new ScriptClient
        {
            Session = session,
            Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
            new LlmToolCall(0, null, "run_dotnet_task", GeminiFixtures.Element("{\"task\":\"build\",\"project_path\":\"a.csproj\"}")), RunnerFixture.Read(1)))
        };
        var budget = new GenerationBudget(); budget.SetTurnLimit(20);
        var registry = new ToolRegistry([new RunDotnetTaskTool(processes, session.Policy), new ReadFileTool(files, session.Policy)], session.Policy, execution);
        var result = await new AgentRunner(client, registry, session.Policy, "test", new(), _ => { }, budget).RunAsync([LlmMessage.FromUserText("build")], default);
        Assert.AreEqual(AgentStopReason.ProcessResourceLimit, result.StopReason); Assert.AreEqual(1, result.GenerationsStarted);
        Assert.AreEqual("process_limit", result.Operations[0].ErrorCode); Assert.AreEqual(ToolStatus.NotExecuted, result.Operations[1].Status);
        Assert.IsFalse(processBudget.CanStart); await client.DisposeAsync();
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task CTXAC025_CompactionNeverBeginsAnotherProcessTurn(bool automatic)
    {
        using var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
        client.Respond = (_, request, _) => Task.FromResult(ChatFixture.Completed(request.Tools.Count == 0 ? ContextTestData.Summary : "answer"));
        var runtime = new ScriptFactory(client); runtime.Sessions.Add(session); var interaction = new ScriptInteraction();
        var messages = automatic ? 6 : 3;
        interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/budget set turn 20"));
        for (var i = 0; i < messages; i++) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("question"));
        if (!automatic) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/compact"));
        var processes = new TestProcessFactory();
        Assert.AreEqual(0, await new ChatApplication(interaction, runtime, new(), new(), "test", new WorkspaceFilesFactory(), processes).RunAsync(session, default));
        Assert.AreEqual(messages, processes.Turns); Assert.AreEqual(messages + 1, client.Requests.Count);
    }
}