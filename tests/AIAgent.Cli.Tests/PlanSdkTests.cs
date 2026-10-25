using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanSdkTests
{
    [TestMethod]
    public async Task PLANAC023_RealSdkSwitchesDefinitionsAndPreservesNativeMixedBatchOrder()
    {
        var root = WorkspaceFixtures.NewDirectory(); var interaction = new PlanChatInteraction();
        foreach (var text in new[] { "/plan", "draft", "/execute", "execute", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        var handler = new GeminiHandler(); var context = new ConversationContext(); var usage = new UsageAccumulator();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls switch
        {
            1 => Batch(("submit_plan", PlanFixture.Submission(kind: "write", tool: "create_file"))),
            3 => Batch(("update_task", Update("in_progress")), ("create_file", new { path = "a", content = "中文\r\n" }), ("update_task", Update("completed"))),
            _ => GeminiFixtures.Text()
        }));
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), context, usage, "test instruction", new TestWorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default));
        Assert.AreEqual(4, handler.Calls); Assert.AreEqual(1, interaction.FileApprovals); Assert.AreEqual(1, interaction.PlanApprovals);
        Assert.AreEqual("中文\r\n", await File.ReadAllTextAsync(Path.Combine(root, "a")));
        using var first = JsonDocument.Parse(handler.Bodies[0]); using var third = JsonDocument.Parse(handler.Bodies[2]); using var fourth = JsonDocument.Parse(handler.Bodies[3]);
        var planDefinitions = first.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations");
        var executeDefinitions = third.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations");
        CollectionAssert.AreEqual(new[] { "list_files", "read_file", "search_files", "search_text", "read_file_range", "submit_plan", "git_status", "git_diff", "read_git_file_range" }, Names(planDefinitions));
        CollectionAssert.AreEqual(new[] { "list_files", "read_file", "create_file", "replace_text", "search_files", "search_text", "read_file_range", "update_task", "run_dotnet_task", "git_status", "git_diff", "read_git_file_range" }, Names(executeDefinitions));
        var schema = planDefinitions[5].GetProperty("parametersJsonSchema");
        Assert.IsFalse(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.IsFalse(schema.GetProperty("properties").GetProperty("tasks").GetProperty("items").GetProperty("additionalProperties").GetBoolean());
        var contents = fourth.RootElement.GetProperty("contents");
        var responses = contents[6].GetProperty("parts");
        CollectionAssert.AreEqual(new[] { "update_task", "create_file", "update_task" }, responses.EnumerateArray().Select(p => p.GetProperty("functionResponse").GetProperty("name").GetString()).ToArray());
        Assert.AreEqual("call-2", responses[2].GetProperty("functionResponse").GetProperty("id").GetString());
        Assert.AreEqual("c2ln", contents[5].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        Assert.AreEqual("local_operation", responses[2].GetProperty("functionResponse").GetProperty("response").GetProperty("data").GetProperty("completion_basis").GetString());
        Assert.AreEqual(4L, usage.Snapshot().DispatchedRequests);
        Assert.IsTrue(interaction.Inner.Events.Any(e => e.PlanSummary?.Tasks[0].State == PlanTaskState.Completed));
    }

    [TestMethod]
    public async Task PLANAC028_ActualBodyLimitIncludesPlanHistorySignaturesAndVerifiedWrite()
    {
        async Task<(int Bytes, int Calls, AgentTurnResult Result, PlanSnapshot? Plan)> Run(int padding)
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            var plans = new PlanStore(session.Policy); plans.SetMode(AgentMode.Plan);
            Assert.IsTrue(plans.Submit(PlanFixture.Submission(kind: "write", tool: "create_file")).Success);
            plans.Approve(plans.ApprovalPreview()!); var execution = new AgentExecutionPolicy(plans);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), execution);
            var registry = TestRulesRegistry.Create([new ListFilesTool(files, session.Policy), new ReadFileTool(files, session.Policy),
                new CreateFileTool(files, session.Policy), new ReplaceTextTool(files, session.Policy), new SearchFilesTool(files, session.Policy),
                new SearchTextTool(files, session.Policy), new ReadFileRangeTool(files, session.Policy), new SubmitPlanTool(plans, session.Policy),
                new UpdateTaskTool(plans, session.Policy)], session.Policy, execution);
            var handler = new GeminiHandler();
            handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 2 ? Batch(("update_task", Update("in_progress")),
                ("create_file", new { path = "a", content = "A" }), ("read_file", new { path = "a" }), ("update_task", Update("completed"))) : GeminiFixtures.Text()));
            await using var client = new GeminiClient(session, handler);
            var runner = new AgentRunner(client, registry, session.Policy, new string('x', padding), new(), _ => { });
            var prior = await runner.RunAsync([LlmMessage.FromUserText("prior")], default);
            var result = await runner.RunAsync(prior.CompletedTurn.Append(LlmMessage.FromUserText("write")).ToArray(), default);
            Assert.AreEqual("A", await File.ReadAllTextAsync(Path.Combine(root, "a")));
            return (handler.Bodies[^1].Length, handler.Calls, result, plans.Snapshot);
        }
        var calibration = await Run(1); var padding = 262144 - calibration.Bytes + 1;
        var accepted = await Run(padding); var rejected = await Run(padding + 1);
        Assert.AreEqual(262144, accepted.Bytes); Assert.AreEqual(3, accepted.Calls); Assert.AreEqual(AgentTurnStatus.Completed, accepted.Result.Status);
        Assert.AreEqual(2, rejected.Calls); Assert.AreEqual(LlmErrorCode.InputTooLarge, rejected.Result.ModelErrorCode);
        Assert.AreEqual(PlanTaskState.Completed, rejected.Plan!.Tasks[0].State); Assert.AreEqual(0, rejected.Result.CompletedTurn.Count);
        Assert.AreEqual(FileWriteState.Verified, rejected.Result.Operations[1].WriteState);
    }

    [TestMethod]
    public async Task PLANAC006_WireDuplicateTaskFieldsAreRejectedByOriginalProtocolBoundary()
    {
        using var f = new PlanFixture(); var execution = new AgentExecutionPolicy(f.Store);
        await using var files = new WorkspaceFiles(f.Session.Workspace, f.Session.Policy, new ControlledWorkspaceFileSystem(), executionPolicy: execution);
        var registry = PlanWorkflowTests.Registry(files, f, execution);
        var arguments = PlanFixture.Submission().GetRawText().Replace("\"title\":\"task\"", "\"title\":\"task\",\"t\\u0069tle\":\"other\"", StringComparison.Ordinal);
        var handler = new GeminiHandler
        {
            Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(
            "[{\"functionCall\":{\"name\":\"submit_plan\",\"args\":" + arguments + "}}]")))
        };
        await using var client = new GeminiClient(f.Session, handler);
        var result = await new AgentRunner(client, registry, f.Session.Policy, "test instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("plan")], default);
        Assert.AreEqual(LlmErrorCode.InvalidResponse, result.ModelErrorCode); Assert.IsNull(f.Store.Snapshot); Assert.AreEqual(0, f.Store.Facts.Count);
    }

    internal static object Update(string status) => new { plan_id = "p1", expected_revision = 1, task_id = "t1", status, summary = "summary" };
    private static string[] Names(JsonElement definitions) => definitions.EnumerateArray().Select(d => d.GetProperty("name").GetString()!).ToArray();
    internal static string Batch(params (string Name, object Arguments)[] calls) => GeminiFixtures.Response(JsonSerializer.Serialize(
        calls.Select((c, i) => new { functionCall = new { name = c.Name, args = c.Arguments, id = "call-" + i }, thoughtSignature = "c2ln" })));
}