using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SkillsIntegrationTests
{
    [TestMethod]
    public async Task SKILLAC009_CommandsAreLocalExplicitAndClearDropsSelection()
    {
        var root = SkillRoot(); using var session = WorkspaceFixtures.Session(root);
        var f = new ChatFixture("/skills", "/Skills use demo", "/skills use  demo", "/skills show demo", "/skills list", "first", "/skills use demo", "second", "/skills reload", "/clear", "third");
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(3, f.Client.Requests.Count);
        Assert.IsTrue(f.Client.Requests[0].SystemInstruction.Contains("\"active\":null", StringComparison.Ordinal));
        Assert.IsFalse(f.Client.Requests[0].SystemInstruction.Contains("UNIQUE_SKILL_BODY", StringComparison.Ordinal));
        StringAssert.Contains(f.Client.Requests[1].SystemInstruction, "UNIQUE_SKILL_BODY");
        Assert.IsFalse(f.Client.Requests[2].SystemInstruction.Contains("UNIQUE_SKILL_BODY", StringComparison.Ordinal));
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("invalid_skills_command", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task SKILLAC015_TextResultMutationStopsHistoryAndPreservesUsage()
    {
        var root = SkillRoot(); using var session = WorkspaceFixtures.Session(root);
        var f = new ChatFixture("/skills use demo", "question", "/skills");
        f.Client.Respond = async (_, _, _) => { await File.WriteAllTextAsync(root + "/skills/demo/SKILL.md", Source("Different body.")); return ChatFixture.Completed("obsolete answer"); };
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(1, f.Client.Requests.Count);
        Assert.IsFalse(f.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
        Assert.IsTrue(f.Interaction.Events.Any(e => e.StopReason == AgentStopReason.SkillsChanged));
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("Stale", StringComparison.Ordinal) == true));
        Assert.AreEqual(1L, f.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task SKILLAC020_DirectWriteRevalidatesAfterApprovalAndDoesNotReuseYes()
    {
        var root = SkillRoot(); using var session = WorkspaceFixtures.Session(root); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        var approval = new EditApproval { OnConfirm = _ => File.WriteAllText(root + "/skills/demo/SKILL.md", Source("Changed while confirming.")) };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval, execution);
        Assert.AreEqual("none", (await execution.Skills!.TryUseAsync("demo", default)).ErrorCode);
        var result = await files.CreateFileAsync("new", "candidate", default);
        Assert.AreEqual(WorkspaceErrorCode.SkillsChanged, result.ErrorCode); Assert.AreEqual(FileWriteState.NotApplied, result.WriteState); Assert.IsFalse(File.Exists(root + "/new"));
    }

    [TestMethod]
    public async Task SKILLAC019_OwnWriteRecordsFactAndCheckpointBeforeStale()
    {
        var root = SkillRoot(); using var session = WorkspaceFixtures.Session(root); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval(), execution);
        Assert.AreEqual("none", (await execution.Skills!.TryUseAsync("demo", default)).ErrorCode); plans.SetMode(AgentMode.Plan);
        Assert.IsTrue(plans.Submit(PlanFixture.Submission(kind: "write", tool: "replace_text", path: "skills/demo/SKILL.md")).Success);
        Assert.IsTrue(plans.Approve(plans.ApprovalPreview()!)); Assert.IsTrue(ProjectRulesLifecycleTests.Update(plans, "in_progress").Success);
        Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("skills/demo/SKILL.md", default)).Status);
        var result = await files.ReplaceTextAsync("skills/demo/SKILL.md", "UNIQUE_SKILL_BODY", "UPDATED_SKILL_BODY", default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual(FileWriteState.Verified, plans.Facts.Last().WriteState);
        Assert.AreEqual(SkillsStatus.Stale, execution.Skills.Capture().Status); Assert.AreEqual(PlanApprovalState.Draft, plans.Snapshot!.ApprovalState);
        Assert.AreEqual("skills_changed", plans.Snapshot.Tasks[0].BlockReason); Assert.AreEqual(0L, plans.EvidenceEpoch);
        StringAssert.Contains((await files.Checkpoints.ListAsync(default)).DisplayBlock!, "1/32");
    }

    [TestMethod]
    public async Task SKILLAC021_ContextMeasurementsBindSkillVersion()
    {
        await using var f = new SkillsFixture(); f.Add("demo"); await f.Rules.TryReloadAsync(default);
        var context = new ContextCoordinator(new(), f.Plans, f.Session.Policy); context.BindRules(f.Rules); context.BindSkills(f.Skills);
        var stamp = context.Capture(); context.RecordMeasure(stamp, GenerationPurpose.Conversation, new(ContextMeasureStatus.Measured, 100));
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("demo", default)).ErrorCode);
        Assert.IsFalse(context.IsCurrent(context.LastMeasurement!.Stamp));
    }

    internal static string SkillRoot()
    {
        var root = WorkspaceFixtures.NewDirectory(); Directory.CreateDirectory(root + "/skills/demo"); File.WriteAllText(root + "/skills/demo/SKILL.md", Source(), new UTF8Encoding(false)); return root;
    }
    internal static string Source(string body = "UNIQUE_SKILL_BODY") => "---\nname: demo\ndescription: Local workflow.\n---\n" + body;

    [TestMethod]
    public async Task SKILLAC019_PublishedWriteSurvivesSensitiveActiveSourceObservedDuringSettlement()
    {
        var root = SkillRoot(); using var session = WorkspaceFixtures.Session(root); var plans = new PlanStore(session.Policy); var execution = new AgentExecutionPolicy(plans);
        var fs = new AfterPublishFileSystem(() => File.WriteAllText(root + "/skills/demo/SKILL.md", Source(GeminiFixtures.Key)));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval(), execution);
        await execution.Skills!.TryUseAsync("demo", default);
        var result = await files.CreateFileAsync("new", "candidate", default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual("candidate", File.ReadAllText(root + "/new"));
        Assert.IsTrue(execution.Skills.RequiresContextReset);
    }

    private sealed class AfterPublishFileSystem(Action changed) : IWorkspaceWriteFileSystem
    {
        private readonly MacWorkspaceFileSystem _inner = new();
        public bool HasExactName(string path) => _inner.HasExactName(path);
        public WorkspaceNodeState Inspect(string path) => _inner.Inspect(path);
        public IEnumerable<string> EnumerateNames(string path) => _inner.EnumerateNames(path);
        public IWorkspaceReadFile OpenRead(string path) => _inner.OpenRead(path);
        public UnixFileMode GetMode(string path) => _inner.GetMode(path);
        public IWorkspaceStagedFile CreateStagedFile(string path) => _inner.CreateStagedFile(path);
        public void PublishNew(string staged, string target) { _inner.PublishNew(staged, target); changed(); }
        public void PublishReplacement(string staged, string target) { _inner.PublishReplacement(staged, target); changed(); }
        public void DeleteOwnedStagedFile(string path) => _inner.DeleteOwnedStagedFile(path);
    }
}