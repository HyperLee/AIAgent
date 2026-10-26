using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class TerminalCoreTests
{
    [DataRow("/exit\n")]
    [DataRow("/rules reload\n/execute")]
    [DataRow("/review\n")]
    [DataRow("/context pin a\nb")]
    [DataRow(" /rules scope a\nb")]
    [TestMethod]
    public async Task TERMAC016_MultilineCommandsAreRejectedBeforeAnyParser(string text)
    {
        var fixture = new ChatFixture(text, "/exit");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        using var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.AreEqual(0, fixture.Interaction.Remembered.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.InputRejected && e.Text == "本機指令只接受單行；本次未執行。"), "TERM-AC-016");
    }

    [TestMethod]
    public async Task TERMAC012_OnlyAcceptedMessagesAreRememberedBeforeDisplayAndGeneration()
    {
        const string text = "first\n/exit\nyes\n";
        var fixture = new ChatFixture(" ", "/help", "/unknown", GeminiFixtures.Key, text, "/exit");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        using var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        fixture.Interaction.OnShow = e => { if (e.Kind == ChatEventKind.UserMessage) Assert.IsTrue(fixture.Interaction.Remembered.SequenceEqual([text]), "TERM-AC-012-order"); };
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.IsTrue(fixture.Interaction.Remembered.SequenceEqual([text]), "TERM-AC-012");
        Assert.AreEqual(1, fixture.Client.Requests.Count);
        Assert.IsTrue(fixture.Client.Requests[0].Messages[^1].UserText == text);
    }

    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [TestMethod]
    public async Task TERMAC027_CoreRevalidatesSubmittedEncodingBeforeCommandRouting(int kind)
    {
        var text = kind switch { 0 => "bad\rtext", 1 => "bad" + (char)0xd800, _ => "bad\u0085" };
        var fixture = new ChatFixture(text, "/exit");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        using var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.AreEqual(0, fixture.Client.Requests.Count, "TERM-AC-027");
        Assert.AreEqual(0, fixture.Interaction.Remembered.Count);
    }

    [TestMethod]
    public async Task TERMAC019_AuthenticationClearsInputBeforeChoiceEvenIfUserExits()
    {
        var fixture = new ChatFixture("failed message");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        using var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        fixture.Client.Respond = (_, _, _) => Task.FromResult(new LlmResult(LlmStatus.Failed, LlmErrorCode.AuthenticationFailed, "", [], null, null, true, false));
        fixture.Interaction.OnChoice = () =>
        {
            Assert.IsTrue(fixture.Interaction.InputClears > 0, "TERM-AC-019");
            Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Usage));
            Assert.IsTrue(fixture.Client.DisposedWithLiveSession);
        };
        Assert.AreEqual(0, await app.RunAsync(session, default));
    }

    [TestMethod]
    public async Task TERMAC018_ClearAndExitInvokeCommonInputCleanup()
    {
        var fixture = new ChatFixture("one", "/clear", "/exit");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        using var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.IsTrue(fixture.Interaction.InputClears >= 2, "TERM-AC-018");
    }
    [TestMethod]
    public async Task TERMAC017_ShortKeyDoesNotBlockClearOrExitOrEnterHistory()
    {
        var fixture = new ChatFixture("/clear", "/exit");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        using var session = new BootstrapSession(new("/unused-workspace"), new("gemini-3.5-flash-lite"), new AIAgent.Cli.Security.SessionSecrets("e".ToCharArray()));
        fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        Assert.AreEqual(0, await app.RunAsync(session, default)); Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.AreEqual(0, fixture.Interaction.Remembered.Count); Assert.IsTrue(fixture.Interaction.InputClears >= 2);
    }

    [TestMethod]
    public async Task TERMAC024_CancellationReportsPendingCleanupBeforeTurnEnds()
    {
        var fixture = new ChatFixture("question", "/exit");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        using var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        fixture.Client.Respond = (_, _, _) =>
        {
            fixture.Interaction.Signal!(ChatControlSignal.CancelTurn);
            Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text == "正在取消；等待目前操作與收尾完成。"), "TERM-AC-024-cancelling");
            return Task.FromResult(ChatFixture.Completed());
        };
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
    }

    [TestMethod]
    public async Task TERMAC027_ActualSdkBodyCarriesOneOriginalMultilineUserMessage()
    {
        const string text = "  code\n\t中文😀\n/exit\nyes\n";
        var interaction = new ScriptInteraction(); interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(text)); interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
        var handler = new GeminiHandler(); var usage = new UsageAccumulator();
        var app = new ChatApplication(interaction, new SdkChatFactory(handler), new(), usage, "instruction", new TestWorkspaceFilesFactory());
        Assert.AreEqual(0, await app.RunAsync(GeminiFixtures.Session(), default)); Assert.AreEqual(1, handler.Calls);
        using var body = JsonDocument.Parse(handler.Bodies[0]); var contents = body.RootElement.GetProperty("contents");
        Assert.AreEqual(1, contents.GetArrayLength());
        Assert.IsTrue(contents[0].GetProperty("parts")[0].GetProperty("text").GetString() == text, "TERM-AC-027-body");
        Assert.IsTrue(interaction.Remembered.SequenceEqual([text])); Assert.AreEqual(1L, usage.Snapshot().DispatchedRequests);
    }

}