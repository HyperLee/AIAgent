using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ModelChatTests
{
    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task MODELAC006_DiskSourceChangedDuringPreviewDoesNotSwitch(bool skill)
    {
        var files = new ControlledWorkspaceFileSystem();
        files.File("AGENTS.md", System.Text.Encoding.UTF8.GetBytes("synthetic initial rule"));
        const string skillText = "---\nname: demo\ndescription: synthetic workflow\n---\nsynthetic initial skill";
        if (skill)
        {
            files.Directory("skills"); files.Directory("skills/demo");
            files.File("skills/demo/SKILL.md", System.Text.Encoding.UTF8.GetBytes(skillText));
        }
        var fixture = new ChatFixture(skill ? ["/skills use demo", "/model use gemini gemini-3.6-flash", "/exit"] : ["/model use gemini gemini-3.6-flash", "/exit"]);
        fixture.Interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
        fixture.Interaction.OnModelApproval = () => files.File(skill ? "skills/demo/SKILL.md" : "AGENTS.md",
            System.Text.Encoding.UTF8.GetBytes(skill ? skillText.Replace("initial", "changed", StringComparison.Ordinal) : "synthetic changed rule"));
        using var session = WorkspaceFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "synthetic instruction", new ModelSourceFactory(files));
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.AreEqual(0, fixture.Interaction.KeyReads);
        Assert.AreEqual(1, fixture.Factory.Sessions.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("model_revision_conflict", StringComparison.Ordinal) == true));
    }

    private sealed class ModelSourceFactory(ControlledWorkspaceFileSystem files) : IWorkspaceFilesFactory
    {
        public FileChangeApprovalBridge ApprovalBridge { get; } = new();
        public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? policy = null) =>
            new WorkspaceFiles(session.Workspace, session.Policy, files, ApprovalBridge.ForSession(session), policy);
    }
    [TestMethod]
    public async Task MODELAC011_FixedMetadataCollisionCannotPublishStartupOrReplacement()
    {
        var startup = new ChatFixture("/model", "/exit");
        using var session = new BootstrapSession(new("/ws"), new("gemini-3.5-flash-lite"), new Security.SessionSecrets("Provider".ToCharArray()));
        Assert.AreEqual(1, await startup.Run(session));
        Assert.AreEqual(0, startup.Client.Requests.Count);
        var replacement = new ChatFixture("question", "/exit");
        replacement.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
        replacement.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
        replacement.Interaction.Keys.Enqueue(() => KeyInputResult.Accept("Provider".AsSpan()));
        Assert.AreEqual(1, await replacement.Run());
        Assert.AreEqual(0, replacement.Factory.ReplacementClient.Requests.Count);
    }
    [TestMethod]
    public async Task MODELAC007_SwitchDisposesOldClientAndKeyBeforeReadingNewKey()
    {
        var fixture = new ChatFixture("question", "/model use gemini gemini-3.6-flash", "/model", "new question", "/exit");
        using var initial = GeminiFixtures.Session();
        fixture.Interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
        fixture.Interaction.Keys.Enqueue(() =>
        {
            Assert.IsTrue(fixture.Client.DisposedWithLiveSession);
            Assert.ThrowsExactly<ObjectDisposedException>(() => { _ = initial.Secrets.AuthenticationKey.Length; });
            return KeyInputResult.Accept("NEXT_KEY_FOR_TESTS_ONLY".AsSpan());
        });
        Assert.AreEqual(0, await fixture.Run(initial));
        Assert.AreEqual(1, fixture.Interaction.KeyReads);
        Assert.AreEqual("gemini-3.6-flash", fixture.Factory.Sessions[^1].Settings.Model);
        Assert.AreEqual(1, fixture.Factory.ReplacementClient.Requests.Count);
        Assert.AreEqual(1, fixture.Factory.ReplacementClient.Requests[0].Messages.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text == "[Plan]"));
        Assert.AreEqual(2L, fixture.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task MODELAC006_DeclinedOrCancelledPreviewPreservesOldConversation()
    {
        foreach (var decision in new[] { ModelApprovalDecision.Declined, ModelApprovalDecision.Cancelled })
        {
            var fixture = new ChatFixture("one", "/model use gemini gemini-3.6-flash", "two", "/exit");
            fixture.Interaction.ModelDecisions.Enqueue(decision);
            Assert.AreEqual(0, await fixture.Run());
            Assert.AreEqual(3, fixture.Client.Requests[1].Messages.Count);
            Assert.AreEqual(0, fixture.Interaction.KeyReads);
            Assert.AreEqual(1, fixture.Factory.Sessions.Count);
            Assert.AreEqual(1, fixture.Interaction.ModelPreviews.Count);
        }
    }

    [TestMethod]
    public async Task MODELAC010_DisposalFailureStopsBeforeNewKey()
    {
        var fixture = new ChatFixture("/model use gemini gemini-3.6-flash", "/exit");
        fixture.Interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
        fixture.Client.DisposeFails = true;
        Assert.AreEqual(1, await fixture.Run());
        Assert.AreEqual(0, fixture.Interaction.KeyReads);
    }

    [TestMethod]
    public async Task MODELAC006_ChangedCandidateDoesNotSwitch()
    {
        var fixture = new ChatFixture("/model use gemini gemini-3.6-flash", "/exit");
        fixture.Interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
        fixture.Interaction.OnModelApproval = fixture.Context.Clear;
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(0, fixture.Interaction.KeyReads);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("model_revision_conflict", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task MODELAC010_AfterApprovalKeyEndUsesSpecifiedExitCode()
    {
        foreach (var status in new[] { KeyInputStatus.Cancelled, KeyInputStatus.EndOfInput, KeyInputStatus.Unavailable })
        {
            var fixture = new ChatFixture("/model use gemini gemini-3.6-flash", "/exit");
            fixture.Interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
            fixture.Interaction.Keys.Enqueue(() => KeyInputResult.End(status));
            Assert.AreEqual(status == KeyInputStatus.Cancelled ? 130 : status == KeyInputStatus.EndOfInput ? 0 : 1, await fixture.Run());
            Assert.AreEqual(1, fixture.Factory.Sessions.Count);
            Assert.AreEqual(1, fixture.Interaction.KeyReads);
        }
    }

    [TestMethod]
    public async Task MODELAC010_CancelDuringNewRulesLoadingCannotPublishReady()
    {
        var fixture = new ChatFixture("/model use gemini gemini-3.6-flash", "/exit");
        fixture.Interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
        fixture.Interaction.Keys.Enqueue(() =>
        {
            fixture.Interaction.OnBusy = signal => signal(ChatControlSignal.CancelTurn);
            return KeyInputResult.Accept("NEW_SYNTHETIC_INPUT".AsSpan());
        });
        Assert.AreEqual(130, await fixture.Run());
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains("模型已切換", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task MODELAC012_ReaderCleanupFailureStillDisposesUnpublishedNewKey()
    {
        var fixture = new ChatFixture("/model use gemini gemini-3.6-flash", "/exit");
        fixture.Interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
        fixture.Interaction.Keys.Enqueue(() =>
        {
            fixture.Interaction.OnBusy = signal => signal(ChatControlSignal.CancelTurn);
            fixture.Interaction.OnBusyDisposed = () => throw new IOException("synthetic reader cleanup failure");
            return KeyInputResult.Accept("NEW_SYNTHETIC_INPUT".AsSpan());
        });
        Assert.AreEqual(1, await fixture.Run());
        Assert.AreEqual(2, fixture.Factory.Sessions.Count);
        Assert.ThrowsExactly<ObjectDisposedException>(() => { _ = fixture.Factory.Sessions[1].Secrets.AuthenticationKey.Length; });
    }

    [TestMethod]
    public async Task MODELAC004_LocalQueriesAndSameTupleDoNotGenerateOrClear()
    {
        var fixture = new ChatFixture("question", "/model", "/model list", "/model use gemini gemini-3.5-flash-lite", "next", "/exit");
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(2, fixture.Client.Requests.Count);
        Assert.AreEqual(3, fixture.Client.Requests[1].Messages.Count);
        Assert.AreEqual(0, fixture.Interaction.KeyReads);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("already_selected", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("gemini-3.6-flash", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Where(e => e.Kind == ChatEventKind.Usage).All(e =>
            Cli.ChatDisplay.Format(e).Contains("Provider：gemini", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task MODELAC004_InvalidCommandsAreLocalAndDoNotEchoValues()
    {
        var fixture = new ChatFixture("/Model", "/model list extra", "/model use  gemini gemini-3.6-flash", "/model use gemini unsupported", "/model\nlist", "/exit");
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains("unsupported", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("model_not_supported", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("invalid_model_command", StringComparison.Ordinal) == true));
    }
}