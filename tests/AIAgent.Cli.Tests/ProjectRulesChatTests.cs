using System.Text;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ProjectRulesChatTests
{
    [TestMethod]
    public async Task RULEAC002_StartupLoadsRootAndRulesCommandsDoNotGenerate()
    {
        var root = WorkspaceFixtures.NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, "AGENTS.md"), "Use deterministic tests.", new UTF8Encoding(false));
        using var session = WorkspaceFixtures.Session(root);
        var fixture = new ChatFixture("/rules", "/rules show", "/rules reload", "question");
        Assert.AreEqual(0, await fixture.Run(session));
        Assert.AreEqual(1, fixture.Client.Requests.Count);
        Assert.IsTrue(fixture.Client.Requests[0].SystemInstruction.Contains("project_rule", StringComparison.Ordinal));
        Assert.IsTrue(fixture.Client.Requests[0].SystemInstruction.Contains("Use deterministic tests.", StringComparison.Ordinal));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("no_change", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task RULEAC019_SourceChangesDuringTextGenerationRejectAnswerAndKeepUsage()
    {
        var root = WorkspaceFixtures.NewDirectory(); var rules = Path.Combine(root, "AGENTS.md");
        await File.WriteAllTextAsync(rules, "first");
        using var session = WorkspaceFixtures.Session(root);
        var fixture = new ChatFixture("question", "/rules");
        fixture.Client.Respond = async (_, _, _) => { await File.WriteAllTextAsync(rules, "other"); return ChatFixture.Completed("obsolete answer"); };
        Assert.AreEqual(0, await fixture.Run(session));
        Assert.AreEqual(1, fixture.Client.Requests.Count);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("Stale", StringComparison.Ordinal) == true));
        Assert.AreEqual(1L, fixture.Usage.Snapshot().DispatchedRequests);
    }
    [TestMethod]
    public async Task RULEAC024_CancelDuringFinalHistoryGateReturnsToInputAndKeepsUsage()
    {
        var fs = new ControlledWorkspaceFileSystem(); fs.File("AGENTS.md", [65]);
        var fixture = new ChatFixture("question", "/rules", "/exit");
        using var session = WorkspaceFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        fixture.Client.Respond = (_, _, _) =>
        {
            var opens = 0;
            fs.OnOpen = _ => { if (++opens == 2) fs.OnRead = _ => { fixture.Interaction.Signal?.Invoke(ChatControlSignal.CancelTurn); return Task.CompletedTask; }; };
            return Task.FromResult(ChatFixture.Completed());
        };
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "fixture", new ControlledRuleChatFactory(fs));
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.AreEqual(1L, fixture.Usage.Snapshot().DispatchedRequests);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.TurnStatus == AgentTurnStatus.Cancelled));
    }

    [TestMethod]
    public async Task RULEAC025_CommonSensitiveResetRevalidatesWithoutApplyingChangedRules()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(root + "/AGENTS.md", "old");
        using var session = WorkspaceFixtures.Session(root); var f = new ChatFixture("question", "/rules");
        f.Client.Respond = async (_, _, _) => { await File.WriteAllTextAsync(root + "/AGENTS.md", "new"); return ChatFixture.Completed(GeminiFixtures.Key); };
        Assert.AreEqual(0, await f.Run(session)); Assert.AreEqual(1, f.Client.Requests.Count);
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("Stale", StringComparison.Ordinal) == true));
        Assert.IsFalse(f.Interaction.Events.Any(e => e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true));
    }

    private sealed class ControlledRuleChatFactory(ControlledWorkspaceFileSystem fs) : Workspace.IWorkspaceFilesFactory
    {
        public Workspace.IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null) =>
            new Workspace.WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: executionPolicy);
    }

}