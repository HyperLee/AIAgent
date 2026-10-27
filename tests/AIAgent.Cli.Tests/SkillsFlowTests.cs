using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SkillsFlowTests
{
    [TestMethod]
    public async Task SKILLAC015_StaleBatchDoesNotRunAnyRequestedToolAndKeepsPairings()
    {
        await using var f = new SkillsFixture(); f.Add("a"); f.Fs.File("target", [65]); await f.Skills.TryUseAsync("a", default);
        var opened = new List<string>(); f.Fs.OnOpen = opened.Add;
        var client = new ScriptClient { Session = f.Session };
        client.Respond = (_, _, _) =>
        {
            f.Add("a", "Changed source.");
            LlmToolCall[] calls = [new(0, "first", "read_file", JsonSerializer.SerializeToElement(new { path = "target" })), new(1, "second", "read_file", JsonSerializer.SerializeToElement(new { path = "target" }))];
            return Task.FromResult(new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), null, true, false));
        };
        var registry = TestRulesRegistry.Create([new ReadFileTool(f.Files, f.Session.Policy)], f.Session.Policy, f.Execution);
        var result = await new AgentRunner(client, registry, f.Session.Policy, "fixture", new(), _ => { }, contextInspector: client).RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.AreEqual(AgentStopReason.SkillsChanged, result.StopReason); Assert.AreEqual(AgentTurnStatus.Stopped, result.Status);
        Assert.AreEqual(2, result.Operations.Count); Assert.IsTrue(result.Operations.All(o => o.Status == ToolStatus.NotExecuted && o.ErrorCode == "skills_changed"));
        Assert.AreEqual(0, f.Files.Snapshots.Count); Assert.AreEqual(0, f.Plans.Facts.Count); Assert.IsFalse(opened.Contains("/ws/target"));
    }

    [TestMethod]
    public async Task SKILLAC021_CompactionOmitsSkillBodyAndRejectsChangedVersion()
    {
        var root = SkillsIntegrationTests.SkillRoot(); using var session = WorkspaceFixtures.Session(root);
        var f = new ChatFixture("/skills use demo", "one", "two", "three", "/compact");
        f.Client.Respond = async (_, request, _) =>
        {
            if (request.Tools.Count != 0) return ChatFixture.Completed();
            Assert.IsFalse(request.SystemInstruction.Contains("UNIQUE_SKILL_BODY", StringComparison.Ordinal));
            Assert.IsFalse(request.Messages.Single().UserText!.Contains("UNIQUE_SKILL_BODY", StringComparison.Ordinal));
            await File.WriteAllTextAsync(root + "/skills/demo/SKILL.md", SkillsIntegrationTests.Source("changed"));
            return ChatFixture.Completed(ContextTestData.Summary);
        };
        f.Interaction.Inputs.Enqueue(() => { Assert.AreEqual(3, f.Context.CompletedTurnCount); Assert.IsNull(f.Context.Summary); return ChatInputResult.Submitted("/skills"); });
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(4, f.Client.Requests.Count);
        Assert.IsTrue(f.Interaction.Events.Any(e => e.StopReason == AgentStopReason.SkillsChanged));
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SKILLAC022_SaveCandidateRequiresCurrentSkillAtSummaryAndApproval(bool duringSummary)
    {
        await using var f = new WorkFlowFixture(); var skills = f.Plans.Skills!;
        f.Contexts.BindSkills(skills); f.FileSystem.Directory("skills"); f.FileSystem.Directory("skills/demo");
        f.FileSystem.File("skills/demo/SKILL.md", Encoding.UTF8.GetBytes(SkillsIntegrationTests.Source()));
        await skills.TryUseAsync("demo", default); f.Contexts.AddPin("keep this");
        if (duringSummary)
        {
            f.Context.CommitCompletedTurn([LlmMessage.FromUserText("q"), LlmMessage.FromModel(new object())]);
            f.Client.Respond = (_, request, _) =>
            {
                Assert.IsFalse(request.SystemInstruction.Contains("UNIQUE_SKILL_BODY", StringComparison.Ordinal));
                f.FileSystem.File("skills/demo/SKILL.md", Encoding.UTF8.GetBytes(SkillsIntegrationTests.Source("changed")));
                return Task.FromResult(ChatFixture.Completed(ContextTestData.Summary));
            };
        }
        var saved = await f.Work.SaveAsync("title", (preview, token) =>
        {
            if (!duringSummary) f.FileSystem.File("skills/demo/SKILL.md", Encoding.UTF8.GetBytes(SkillsIntegrationTests.Source("changed")));
            return ValueTask.FromResult(WorkApprovalDecision.Approved);
        }, default);
        Assert.AreEqual("skills_changed", saved.ErrorCode); Assert.AreEqual(0, f.Store.Publishes); Assert.AreEqual(duringSummary ? 1 : 0, saved.GenerationsStarted);
    }

    [TestMethod]
    public async Task SKILLAC019_RestoreActiveSourceRecordsRecoveryBeforeStale()
    {
        var root = SkillsIntegrationTests.SkillRoot(); using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), execution);
        await execution.Skills!.TryUseAsync("demo", default); await files.ReadFileAsync("skills/demo/SKILL.md", default);
        Assert.AreEqual(FileWriteState.Verified, (await files.ReplaceTextAsync("skills/demo/SKILL.md", "UNIQUE_SKILL_BODY", "UPDATED_SKILL_BODY", default)).WriteState);
        Assert.AreEqual("skills_changed", (await files.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default)).ErrorCode);
        await execution.Skills.TryReloadAsync(default);
        var restored = await files.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
        Assert.AreEqual(FileWriteState.Verified, restored.WriteState); Assert.AreEqual(1L, execution.Recovery.Revision); Assert.AreEqual(1L, execution.Plans.EvidenceEpoch);
        Assert.AreEqual(SkillsStatus.Stale, execution.Skills.Capture().Status); StringAssert.Contains(File.ReadAllText(root + "/skills/demo/SKILL.md"), "UNIQUE_SKILL_BODY");
    }

    [TestMethod]
    public async Task SKILLAC018_LastFactIsRecordedBeforeSkillRevocation()
    {
        var root = SkillsIntegrationTests.SkillRoot(); File.WriteAllText(root + "/a", "A"); using var session = WorkspaceFixtures.Session(root);
        var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), execution);
        await execution.Skills!.TryUseAsync("demo", default); plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(plans.Submit(PlanFixture.Submission()).Success); Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!)); Assert.IsTrue(ProjectRulesLifecycleTests.Update(plans, "in_progress").Success);
        for (var i = 0; i < 254; i++) Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("a", default)).Status);
        plans.SetMode(AgentMode.Plan); Assert.IsTrue(plans.Submit(PlanFixture.Submission(expected: 1, kind: "write", tool: "replace_text", path: "skills/demo/SKILL.md")).Success);
        Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!)); Assert.IsTrue(ProjectRulesLifecycleTests.Update(plans, "in_progress").Success);
        await files.ReadFileAsync("skills/demo/SKILL.md", default);
        var result = await files.ReplaceTextAsync("skills/demo/SKILL.md", "UNIQUE_SKILL_BODY", "UPDATED_SKILL_BODY", default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual(256, plans.Facts.Count); Assert.AreEqual(FileWriteState.Verified, plans.Facts[^1].WriteState);
        Assert.AreEqual(PlanApprovalState.Draft, plans.Snapshot!.ApprovalState); Assert.AreEqual("skills_changed", plans.Snapshot.Tasks[0].BlockReason);
        Assert.IsTrue(PlanProjection.State(plans.Mode, plans.Snapshot, true).Length <= 32768); Assert.IsTrue(Encoding.UTF8.GetByteCount(plans.Display()!) <= 65536);
    }

    [TestMethod]
    public async Task SKILLAC028_RealSdkBodyIncludesCompleteSkillsAndKeepsHardBoundary()
    {
        var calibration = await RunSdkAsync(1);
        var padding = 262144 - calibration.Bytes + 1;
        var accepted = await RunSdkAsync(padding); Assert.AreEqual(262144, accepted.Bytes); Assert.AreEqual(1, accepted.HttpCalls); Assert.AreEqual(AgentTurnStatus.Completed, accepted.Result.Status);
        var rejected = await RunSdkAsync(padding + 1); Assert.AreEqual(0, rejected.HttpCalls); Assert.AreNotEqual(AgentTurnStatus.Completed, rejected.Result.Status);
    }

    private static async Task<(int Bytes, int HttpCalls, AgentTurnResult Result)> RunSdkAsync(int padding)
    {
        await using var f = new SkillsFixture(); f.Add("a", "Full body 中文😀\t"); await f.Skills.TryUseAsync("a", default);
        var handler = new GeminiHandler(); await using var client = new GeminiClient(f.Session, handler);
        var registry = TestRulesRegistry.Create([new ReadFileTool(f.Files, f.Session.Policy)], f.Session.Policy, f.Execution);
        var runner = new AgentRunner(client, registry, f.Session.Policy, new string('x', padding), new(), _ => { }, contextInspector: client.CreateContextInspector());
        var result = await runner.RunAsync([LlmMessage.FromUserText("q")], default);
        if (handler.Calls > 0)
        {
            using var parsed = JsonDocument.Parse(handler.Bodies.Single());
            Assert.IsTrue(parsed.RootElement.ToString().Contains("skill", StringComparison.Ordinal));
        }
        return (handler.Bodies.LastOrDefault()?.Length ?? 0, handler.Calls, result);
    }

    [TestMethod]
    public async Task SKILLAC020_ProcessConsentRevalidatesSkillBeforeAnyNativeLaunch()
    {
        var root = await NativeBuildFixture.CreateAsync(false); Directory.CreateDirectory(root + "/skills/demo");
        File.WriteAllText(root + "/skills/demo/SKILL.md", SkillsIntegrationTests.Source());
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        await execution.Skills!.TryUseAsync("demo", default); var approvals = 0;
        await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
            (_, _) => { approvals++; File.WriteAllText(root + "/skills/demo/SKILL.md", SkillsIntegrationTests.Source("changed")); return new(ProcessApprovalDecision.Approved); }, _ => { });
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "Fixture.csproj", default);
        Assert.AreEqual("skills_changed", result.ErrorCode); Assert.AreEqual(1, approvals); Assert.AreEqual(ProcessStartState.NotStarted, result.Summary!.StartState);
    }

    [TestMethod]
    public async Task SKILLAC020_PlanYesCannotAuthorizeChangedSkillOrSkipNewConfirmation()
    {
        var root = SkillsIntegrationTests.SkillRoot(); using var session = WorkspaceFixtures.Session(root);
        var client = new ScriptClient { Session = session }; var interaction = new PlanChatInteraction(); var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "submit_plan", PlanFixture.Submission(kind: "analysis"))) : ChatFixture.Completed());
        interaction.OnPlan = (_, _) => { if (interaction.PlanApprovals == 1) File.WriteAllText(root + "/skills/demo/SKILL.md", SkillsIntegrationTests.Source("changed")); return new(PlanApprovalDecision.Approved); };
        foreach (var command in new[] { "/skills use demo", "/plan", "plan", "/execute", "/skills reload", "/execute", "continue", "/exit" })
            interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(command));
        var app = new ChatApplication(interaction, factory, new(), new(), "fixture", new TestWorkspaceFilesFactory());
        Assert.AreEqual(0, await app.RunAsync(session, default)); Assert.AreEqual(2, interaction.PlanApprovals); Assert.AreEqual(3, client.Requests.Count);
        Assert.AreEqual(12, client.Requests[^1].Tools.Count);
    }

    [TestMethod]
    public async Task SKILLAC024_ReplacementKeyRetainsCounterAndDropsSkillBody()
    {
        var root = SkillsIntegrationTests.SkillRoot(); using var session = WorkspaceFixtures.Session(root);
        var f = new ChatFixture("/skills use demo", "authenticate", "next");
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
        f.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter); f.Interaction.Keys.Enqueue(() => KeyInputResult.Accept("REPLACEMENT_SYNTHETIC_ONLY"));
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(1, f.Factory.ReplacementClient.Requests.Count);
        var request = f.Factory.ReplacementClient.Requests.Single(); Assert.IsFalse(request.SystemInstruction.Contains("UNIQUE_SKILL_BODY", StringComparison.Ordinal));
        Assert.IsTrue(request.SystemInstruction.Contains("\"skills_revision\":2", StringComparison.Ordinal));
    }
}