using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationLifecycleTests
{
    [TestMethod]
    public async Task MULTIAC012_CapabilitiesRejectGeneralSerialization()
    {
        await using var f = await DelegationTestFixture.Create();
        Assert.ThrowsExactly<NotSupportedException>(() => JsonSerializer.Serialize(f.Coordinator));
    }

    [TestMethod]
    public async Task MULTIAC003_CommandsAreLocalExactAndSameValueDoesNotAdvanceRevision()
    {
        var fixture = new ChatFixture("/agents", "/agents on", "/agents on", "/agents off", "/AGENTS on", "/agents on extra", "/agents\non", "/exit");
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(0, fixture.Client.Requests.Count); Assert.AreEqual(0, fixture.Interaction.Remembered.Count);
        Assert.AreEqual(3, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.InputRejected && e.Text!.Contains("invalid_agents_command", StringComparison.Ordinal)));
    }
    [TestMethod]
    public async Task MULTIAC026_SourceVersionChangeDisablesEnabledDelegationWithoutContextHistoryDependency()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        var gateContext = new ConversationContext(); var plans = new PlanStore(session.Policy, gateContext.DecisionGate); var execution = new AgentExecutionPolicy(plans);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
        await execution.Rules!.EnsureInitializedAsync(default);
        var contexts = new ContextCoordinator(gateContext, plans, session.Policy); contexts.BindRules(execution.Rules); contexts.BindSkills(execution.Skills);
        var coordinator = new DelegationCoordinator(contexts, execution, new(gateContext.DecisionGate), files, session.Policy,
            new ChatRuntimeFactory().CreateDelegationFactory(session), () => new ReviewGitWorkspace(), new(), _ => { });
        coordinator.SetEnabled(true); var revision = coordinator.Revision; coordinator.SetEnabled(true); Assert.AreEqual(revision, coordinator.Revision);
        Assert.AreEqual("none", contexts.AddPin("keep this")); Assert.AreEqual(DelegationMode.Enabled, coordinator.Mode);
        execution.Rules.MarkStale(); Assert.AreEqual(DelegationMode.Disabled, coordinator.Mode);
        Assert.IsNull(coordinator.LastResult?.Data);
    }
    [TestMethod]
    public void MULTIAC033_CompactionWhitelistDropsChildReportsAndPaths()
    {
        using var response = JsonDocument.Parse("""{"status":"success","error_code":"none","data":{"version":1,"batch_id":"d1","state":"partial","children":[{"agent_id":"a1","role":"explore","state":"completed","error_code":"none","report":{"summary":"PRIVATE_REPORT"},"scope_path":"PRIVATE_PATH"}],"merged_findings":[]}}""");
        var metadata = GeminiContextTools.Project(new(0, null, "delegate_readonly", response.RootElement));
        Assert.IsNotNull(metadata); var text = metadata.Value.GetRawText();
        Assert.IsTrue(text.Contains("d1", StringComparison.Ordinal)); Assert.IsTrue(text.Contains("completed", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("PRIVATE_REPORT", StringComparison.Ordinal)); Assert.IsFalse(text.Contains("PRIVATE_PATH", StringComparison.Ordinal));
    }
    [TestMethod]
    public void MULTIAC025_OnlyExactlyEqualFindingsMergeAndKeepBothAuthors()
    {
        using var report = JsonDocument.Parse("""{"findings":[{"severity":"P2","title":"one","body":"issue","path":"a.cs","layer":"staged","side":"new","start_line":1,"end_line":1}]}""");
        var task = new DelegationTask(DelegationRole.Review, "inspect", ".", "cite"); var coverage = DelegationJson.Coverage(DelegationRole.Review);
        var children = new[] { new DelegationChildResult("a1", task, DelegationTaskState.Completed, "none", report.RootElement, coverage), new DelegationChildResult("a2", task, DelegationTaskState.Completed, "none", report.RootElement, coverage) };
        var data = DelegationReports.Batch("d1", children);
        Assert.AreEqual("completed", data.GetProperty("state").GetString()); Assert.AreEqual(1, data.GetProperty("merged_findings").GetArrayLength());
        Assert.AreEqual(2, data.GetProperty("merged_findings")[0].GetProperty("agent_ids").GetArrayLength());
    }
}