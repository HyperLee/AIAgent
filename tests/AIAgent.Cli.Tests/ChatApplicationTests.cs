using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ChatApplicationTests
{
    [TestMethod]
    public async Task CLIAC016_InterruptDuringUserMessageNeverStartsGeneration()
    {
        var fixture = new ChatFixture("question");
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        fixture.Interaction.OnShow = message => { if (message.Kind == ChatEventKind.UserMessage) app.Interrupt(); };
        Assert.AreEqual(130, await app.RunAsync(session, default));
        Assert.AreEqual(0, fixture.Client.Requests.Count, "CLI-AC-016");
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind is ChatEventKind.Generating or ChatEventKind.Usage));
    }

    [TestMethod]
    public async Task CLIAC016_ExternalInterruptBetweenInputAndGenerationUsesIdleState()
    {
        var fixture = new ChatFixture();
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        fixture.Interaction.Inputs.Enqueue(() => { app.Interrupt(); return ChatInputResult.Submitted("not submitted"); });
        Assert.AreEqual(130, await app.RunAsync(session, default));
        Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.UserMessage));
    }

    [TestMethod]
    public async Task CLIAC016_PreCancelledApplicationDoesNotUseUninitializedDisplayPolicy()
    {
        using var terminal = new ConsoleChatInteraction(new FakeTerminal());
        var factory = new ScriptFactory(new ScriptClient());
        var app = new ChatApplication(terminal, factory, new(), new(), "instruction", new TestWorkspaceFilesFactory());
        Assert.AreEqual(130, await app.RunAsync(GeminiFixtures.Session(), new CancellationToken(true)), "CLI-AC-016");
    }

    [TestMethod]
    public async Task CLIAC014_InterruptRoutesByStateAndWaitsForCancelledGeneration()
    {
        var fixture = new ChatFixture("one", "two", "/exit");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<LlmResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new TestWorkspaceFilesFactory());
        var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        fixture.Client.Respond = async (i, _, token) =>
        {
            if (i != 0) { Assert.IsFalse(token.IsCancellationRequested); return ChatFixture.Completed(); }
            started.SetResult();
            return await release.Task;
        };
        var run = app.RunAsync(session, default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Interaction.Signal!(ChatControlSignal.CancelTurn);
        Assert.IsTrue(await Task.Run(() => SpinWait.SpinUntil(() => app.State == ChatState.Cancelling, TimeSpan.FromSeconds(5))));
        Assert.IsFalse(run.IsCompleted);
        release.SetResult(ChatFixture.Completed());
        Assert.AreEqual(0, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, fixture.Client.Requests[1].Messages.Count);
        Assert.AreEqual(1, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.Answer));
    }

    [TestMethod]
    public async Task CLIAC002_LocalCommandsNeverGenerateAndDoNotEchoUnknownInput()
    {
        var fixture = new ChatFixture("", "  ", "/help", " /clear ", "/clear", "/CLEAR", "/help extra", "/", "/tmp/a", "/exit");
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.UserMessage || e.Kind == ChatEventKind.Usage));
        Assert.AreEqual(2, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.ContextCleared));
        Assert.AreEqual(4, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.InputRejected));
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains("/tmp/a", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task CLIAC003_KeyInputIsRejectedBeforeDisplayAndFixedMetadataCollisionStopsRuntime()
    {
        var fixture = new ChatFixture("prefix" + GeminiFixtures.Key, "/exit");
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.UserMessage));
        var shortKey = new ChatFixture("/clear", "/exit");
        Assert.AreEqual(1, await shortKey.Run(new(new("/unused"), new("gemini-3.5-flash-lite"), new Security.SessionSecrets("e".ToCharArray()))));
        Assert.AreEqual(0, shortKey.Client.Requests.Count);
    }

    [TestMethod]
    public async Task CLIAC006_PlainTextUsesOneGenerationWithReadOnlyToolsAndSeparateInstruction()
    {
        var fixture = new ChatFixture("  安全問題  ", "/exit");
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(1, fixture.Client.Requests.Count);
        var request = fixture.Client.Requests[0];
        Assert.IsTrue(request.SystemInstruction.StartsWith("test instruction\n", StringComparison.Ordinal));
        Assert.IsTrue(request.Tools.Select(tool => tool.Name).SequenceEqual(new[] { "list_files", "read_file", "create_file", "replace_text", "search_files", "search_text", "read_file_range", "update_task", "run_dotnet_task", "git_status", "git_diff", "read_git_file_range" }));
        Assert.AreEqual(1, request.Messages.Count);
        Assert.AreEqual("  安全問題  ", request.Messages[0].UserText);
        Assert.AreEqual(1, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.Answer));
        Assert.AreEqual(1L, fixture.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task CLIAC007_ApplicationCarriesFiveCompleteTurnsThroughSeventhRequest()
    {
        var fixture = new ChatFixture(Enumerable.Range(1, 7).Select(i => "question " + i).Append("/exit").ToArray());
        fixture.Client.Respond = (_, request, _) => Task.FromResult(ChatFixture.Completed(request.Tools.Count == 0 ? ContextTestData.Summary : "answer"));
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(1, fixture.Client.Requests[5].Messages.Count);
        Assert.AreEqual(0, fixture.Client.Requests[5].Tools.Count);
        Assert.AreEqual("question 4", fixture.Client.Requests[6].Messages[0].UserText);
        Assert.AreSame(fixture.Client.Returned[4].ModelMessage, fixture.Client.Requests[6].Messages[3]);
    }

    [TestMethod]
    public async Task CLIAC009_FailedTurnsNeverEnterHistoryAndTruncationIsExplicit()
    {
        foreach (var status in new[] { LlmStatus.Empty, LlmStatus.Refused, LlmStatus.Truncated, LlmStatus.Cancelled, LlmStatus.Failed })
        {
            var fixture = new ChatFixture("one", "failed question", "three", "/exit");
            fixture.Client.Respond = (i, _, _) => Task.FromResult(i == 1
                ? new LlmResult(status, status == LlmStatus.Failed ? LlmErrorCode.NetworkFailure : LlmErrorCode.None, "partial", [], null, null, true, false)
                : ChatFixture.Completed());
            Assert.AreEqual(0, await fixture.Run());
            Assert.AreEqual(3, fixture.Client.Requests[2].Messages.Count, "CLI-AC-009");
            Assert.AreEqual("one", fixture.Client.Requests[2].Messages[0].UserText);
            Assert.AreEqual(status == LlmStatus.Truncated ? 3 : 2, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.Answer));
            if (status == LlmStatus.Truncated) Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer && e.Status == LlmStatus.Truncated));
        }
    }

    [TestMethod]
    public async Task CLIAC010_ClearAndResetKeepInstructionRuntimeAndUsage()
    {
        foreach (var forced in new[] { false, true })
        {
            var fixture = new ChatFixture(forced ? ["one", "reset", "next", "/exit"] : ["one", "/clear", "next", "/exit"]);
            fixture.Client.Respond = (i, _, _) => Task.FromResult(forced && i == 1
                ? new LlmResult(LlmStatus.Completed, LlmErrorCode.None, "ignored", [], LlmMessage.FromModel(new object()), new(1, 2, 3, 4, 5), true, true)
                : ChatFixture.Completed());
            Assert.AreEqual(0, await fixture.Run());
            Assert.AreEqual(1, fixture.Client.Requests[^1].Messages.Count);
            Assert.IsTrue(fixture.Client.Requests[^1].SystemInstruction.StartsWith("test instruction\n", StringComparison.Ordinal));
            Assert.AreEqual(forced ? 3L : 2L, fixture.Usage.Snapshot().DispatchedRequests);
            Assert.AreEqual(1, fixture.Factory.Sessions.Count);
            Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.ContextCleared));
        }
    }

    [TestMethod]
    public async Task CLIAC012_MalformedAndToolResultsResetInsteadOfPartialSuccess()
    {
        var variants = new LlmResult[] {
            new(LlmStatus.Completed, LlmErrorCode.None, "answer", [], null, null, true, false),
            new(LlmStatus.Completed, LlmErrorCode.None, "  ", [], LlmMessage.FromModel(new object()), null, true, false),
            new(LlmStatus.Completed, LlmErrorCode.NetworkFailure, "answer", [], LlmMessage.FromModel(new object()), null, true, false),
            new(LlmStatus.Completed, LlmErrorCode.None, "answer", [], LlmMessage.FromUserText("wrong"), null, true, false),
            new((LlmStatus)999, LlmErrorCode.None, "answer", [], null, null, true, false),
            new(LlmStatus.Failed, (LlmErrorCode)999, "answer", [], null, null, true, false),
            new(LlmStatus.ToolCalls, LlmErrorCode.None, "answer", [], LlmMessage.FromModel(new object()), null, true, false),
            new(LlmStatus.Completed, LlmErrorCode.None, "answer", [new(0, null, "fake", GeminiFixtures.Element("{}"))], LlmMessage.FromModel(new object()), null, true, false)
        };
        foreach (var result in variants)
        {
            var fixture = new ChatFixture("one", "bad", "next", "/exit");
            fixture.Client.Respond = (i, _, _) => Task.FromResult(i == 1 ? result : ChatFixture.Completed());
            Assert.AreEqual(0, await fixture.Run());
            Assert.AreEqual(1, fixture.Client.Requests[2].Messages.Count);
            Assert.AreEqual(2, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.Answer));
            Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.ContextCleared));
        }
    }

    [TestMethod]
    public async Task CLIAC013_OrdinaryErrorsKeepHistoryAndNeverAskForKey()
    {
        foreach (var code in new[] { LlmErrorCode.InputTooLarge, LlmErrorCode.SensitiveInput, LlmErrorCode.ModelUnavailable, LlmErrorCode.PermissionDenied, LlmErrorCode.QuotaOrRateLimited, LlmErrorCode.NetworkFailure, LlmErrorCode.Timeout, LlmErrorCode.InvalidRequest })
        {
            var fixture = new ChatFixture("one", "two", "three", "/exit");
            fixture.Client.Respond = (i, _, _) => Task.FromResult(i == 1 ? new(LlmStatus.Failed, code, "untrusted error", [], null, null, false, false) : ChatFixture.Completed());
            Assert.AreEqual(0, await fixture.Run());
            Assert.AreEqual(3, fixture.Client.Requests[2].Messages.Count);
            Assert.AreEqual(0, fixture.Interaction.ChoiceReads);
            Assert.AreEqual(2L, fixture.Usage.Snapshot().DispatchedRequests);
        }
    }

    [TestMethod]
    public async Task CLIAC014_CancelBeforeGenerationDoesNotCancelApplication()
    {
        var fixture = new ChatFixture("one", "two", "/exit");
        var cancelled = false;
        fixture.Interaction.OnBusy = signal => { if (!cancelled) { cancelled = true; signal(ChatControlSignal.CancelTurn); signal(ChatControlSignal.CancelTurn); } };
        fixture.Client.Respond = (_, _, token) => Task.FromResult(token.IsCancellationRequested
            ? new(LlmStatus.Cancelled, LlmErrorCode.None, "", [], null, null, false, false) : ChatFixture.Completed());
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(1, fixture.Client.Requests.Count);
        Assert.AreEqual(1, fixture.Client.Requests[0].Messages.Count);
        Assert.AreEqual(1L, fixture.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task CLIAC015_LateCompletedAfterCancellationIsCountedButNeverCommitted()
    {
        var fixture = new ChatFixture("one", "two", "/exit");
        fixture.Client.Respond = (i, _, _) =>
        {
            if (i == 0) fixture.Interaction.Signal!(ChatControlSignal.CancelTurn);
            return Task.FromResult(ChatFixture.Completed());
        };
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(1, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.Answer));
        Assert.AreEqual(1, fixture.Client.Requests[1].Messages.Count);
        Assert.AreEqual(2L, fixture.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task CLIAC015_PostCommitBusyCancelDoesNotUndoCommitOrCancelNextTurn()
    {
        var fixture = new ChatFixture("one", "next", "/exit");
        fixture.Interaction.OnShow = e => { if (e.Kind == ChatEventKind.Answer) fixture.Interaction.Signal!(ChatControlSignal.CancelTurn); };
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(2, fixture.Client.Requests.Count);
        Assert.AreEqual(3, fixture.Client.Requests[1].Messages.Count);
        Assert.AreEqual(2, fixture.Interaction.Events.Count(e => e.Kind == ChatEventKind.Answer));
    }

    [TestMethod]
    public async Task CLIAC016_BusyStopSignalsAndInputTerminationUseCorrectExitCodes()
    {
        foreach (var signal in new[] { ChatControlSignal.EndOfInput, ChatControlSignal.Unavailable })
        {
            var fixture = new ChatFixture("one", "unread");
            fixture.Client.Respond = (_, _, _) => { fixture.Interaction.Signal!(signal); return Task.FromResult(ChatFixture.Completed()); };
            Assert.AreEqual(signal == ChatControlSignal.EndOfInput ? 0 : 1, await fixture.Run());
            Assert.AreEqual(1, fixture.Client.Requests.Count);
            Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
        }
        foreach (var status in new[] { ChatInputStatus.EndOfInput, ChatInputStatus.Cancelled, ChatInputStatus.Unavailable })
        {
            var fixture = new ChatFixture(); fixture.Interaction.Inputs.Enqueue(() => ChatInputResult.End(status));
            Assert.AreEqual(status == ChatInputStatus.Cancelled ? 130 : status == ChatInputStatus.Unavailable ? 1 : 0, await fixture.Run());
        }
    }

    [TestMethod]
    public async Task CLIAC016_ApplicationCancellationBeatsEofAndWaitsForGeneration()
    {
        var fixture = new ChatFixture("one");
        using var stop = new CancellationTokenSource();
        fixture.Client.Respond = (_, _, token) =>
        {
            fixture.Interaction.Signal!(ChatControlSignal.EndOfInput); stop.Cancel();
            Assert.IsTrue(token.IsCancellationRequested);
            return Task.FromResult(ChatFixture.Completed());
        };
        Assert.AreEqual(130, await fixture.Run(cancellationToken: stop.Token));
        Assert.AreEqual(1L, fixture.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task CLIAC017_LocalFailuresDisposeClientBeforeSessionAndHideExceptions()
    {
        foreach (var mode in new[] { "create", "generate", "show", "dispose", "unavailable", "observer" })
        {
            var fixture = new ChatFixture("one", "/exit");
            if (mode == "create") fixture.Factory.FailClient = true;
            if (mode == "generate") fixture.Client.Respond = (_, _, _) => throw new IOException(GeminiFixtures.Key);
            if (mode == "show") fixture.Interaction.OnShow = _ => throw new IOException(GeminiFixtures.Key);
            if (mode == "dispose") fixture.Client.DisposeFails = true;
            if (mode == "observer") fixture.Interaction.ObserverFails = true;
            if (mode == "unavailable") fixture.Client.Respond = (_, _, _) => Task.FromResult(new LlmResult(LlmStatus.Failed, LlmErrorCode.ClientUnavailable, "", [], null, null, false, false));
            Assert.AreEqual(1, await fixture.Run(), "CLI-AC-017");
            if (mode != "create") Assert.IsTrue(fixture.Client.DisposedWithLiveSession);
            Assert.IsTrue(fixture.Factory.Sessions.All(SessionIsDisposed));
            Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true));
            Assert.IsNull(fixture.Interaction.Signal);
        }
    }

    [TestMethod]
    public void CLIAC017_ClientConstructionFailureDisposesAllocatedTransport()
    {
        var session = GeminiFixtures.Session(); session.Dispose();
        var handler = new GeminiHandler();
        Assert.ThrowsExactly<ObjectDisposedException>(() => new GeminiClient(session, handler));
        Assert.IsTrue(handler.Disposed, "CLI-AC-017");
    }

    [TestMethod]
    public async Task CLIAC027_OverflowAndMissingCountersSurviveClearResetAndRekey()
    {
        var fixture = new ChatFixture("one", "/clear", "reset", "authentication", "new question", "/exit");
        fixture.Usage.Record(new(LlmStatus.Failed, LlmErrorCode.NetworkFailure, "", [], null, new(null, null, null, null, long.MaxValue), true, false));
        fixture.Usage.Record(new(LlmStatus.Failed, LlmErrorCode.NetworkFailure, "", [], null, null, true, false));
        fixture.Client.Respond = (i, _, _) => Task.FromResult(i switch
        {
            1 => new(LlmStatus.Failed, LlmErrorCode.InvalidResponse, "", [], null, null, true, true),
            2 => ChatFixture.Authentication(),
            _ => ChatFixture.Completed()
        });
        fixture.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
        fixture.Interaction.Keys.Enqueue(() => KeyInputResult.Accept(GeminiFixtures.Key));
        Assert.AreEqual(0, await fixture.Run());
        var snapshot = fixture.Usage.Snapshot();
        Assert.IsTrue(snapshot.Total.Overflowed);
        Assert.AreEqual(long.MaxValue, snapshot.Total.KnownTotal);
        Assert.AreEqual(2L, snapshot.Total.MissingRequestCount);
        Assert.AreEqual(6L, snapshot.DispatchedRequests);
        Assert.AreEqual(1, fixture.Factory.ReplacementClient.Requests.Count);
    }

    [TestMethod]
    public async Task CLIAC019_RekeyDisposesOldClientBeforeChoiceAndNeverReplays()
    {
        var fixture = new ChatFixture("one", "authentication", "new question", "/exit");
        fixture.Client.Respond = (i, _, _) => Task.FromResult(i == 1 ? ChatFixture.Authentication() : ChatFixture.Completed());
        fixture.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
        fixture.Interaction.Keys.Enqueue(() => KeyInputResult.Accept("FAKE_REPLACEMENT_ONLY"));
        fixture.Interaction.OnChoice = () =>
        {
            Assert.IsTrue(fixture.Client.DisposedWithLiveSession);
            Assert.AreEqual(ChatEventKind.Usage, fixture.Interaction.Events[^1].Kind);
            Assert.IsFalse(SessionIsDisposed(fixture.Factory.Sessions[0]));
        };
        fixture.Factory.ReplacementClient.Respond = (_, request, _) =>
        {
            Assert.IsTrue(SessionIsDisposed(fixture.Factory.Sessions[0]));
            Assert.AreEqual(1, request.Messages.Count);
            Assert.AreEqual("new question", request.Messages[0].UserText);
            return Task.FromResult(ChatFixture.Completed());
        };
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(2, fixture.Client.Requests.Count);
        Assert.AreEqual(1, fixture.Factory.ReplacementClient.Requests.Count);
        Assert.AreEqual(3L, fixture.Usage.Snapshot().DispatchedRequests);
        Assert.IsTrue(fixture.Factory.Sessions.All(SessionIsDisposed));
        Assert.AreEqual(fixture.Factory.Sessions[0].Workspace, fixture.Factory.Sessions[1].Workspace);
        Assert.AreEqual(fixture.Factory.Sessions[0].Settings, fixture.Factory.Sessions[1].Settings);
    }

    [TestMethod]
    public async Task CLIAC020_RekeyTerminationAndConstructionFailureNeverReuseOldRuntime()
    {
        foreach (var mode in new[] { "exit", "eof", "cancel", "choice-io", "key-eof", "key-cancel", "key-io", "session", "client", "policy" })
        {
            var fixture = new ChatFixture("one", "unread");
            fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
            fixture.Interaction.Choices.Enqueue(mode switch { "exit" => KeyReplacementChoice.Exit, "eof" => KeyReplacementChoice.EndOfInput, "cancel" => KeyReplacementChoice.Cancelled, "choice-io" => KeyReplacementChoice.Unavailable, _ => KeyReplacementChoice.Reenter });
            fixture.Interaction.Keys.Enqueue(() => mode switch { "key-eof" => KeyInputResult.End(KeyInputStatus.EndOfInput), "key-cancel" => KeyInputResult.End(KeyInputStatus.Cancelled), "key-io" => KeyInputResult.End(KeyInputStatus.Unavailable), _ => KeyInputResult.Accept(GeminiFixtures.Key) });
            fixture.Factory.FailSession = mode == "session";
            fixture.Factory.FailReplacementClient = mode == "client";
            fixture.Interaction.FailPolicyChange = mode == "policy";
            Assert.AreEqual(mode is "cancel" or "key-cancel" ? 130 : mode is "exit" or "eof" or "key-eof" ? 0 : 1, await fixture.Run(), "CLI-AC-020");
            Assert.AreEqual(1, fixture.Client.Requests.Count);
            Assert.AreEqual(0, fixture.Factory.ReplacementClient.Requests.Count);
            Assert.IsTrue(fixture.Factory.Sessions.All(SessionIsDisposed));
        }
    }

    [TestMethod]
    public async Task CLIAC022_UntrustedSafeTextIsBlockedAndUsageSurvivesContextReset()
    {
        var fixture = new ChatFixture("one", "sensitive", "next", "/exit");
        fixture.Client.Respond = (i, _, _) => Task.FromResult(i == 1 ? ChatFixture.Completed(GeminiFixtures.Key + GeminiFixtures.Key) : ChatFixture.Completed());
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(1, fixture.Client.Requests[2].Messages.Count);
        Assert.AreEqual(3L, fixture.Usage.Snapshot().DispatchedRequests);
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.StopReason == AgentStopReason.SensitiveData));
    }

    private static bool SessionIsDisposed(BootstrapSession session)
    {
        try { _ = session.Secrets.AuthenticationKey.Length; return false; }
        catch (ObjectDisposedException) { return true; }
    }
}

internal sealed class ChatFixture
{
    internal ScriptInteraction Interaction { get; } = new();
    internal ScriptClient Client { get; } = new();
    internal ScriptFactory Factory { get; }
    internal UsageAccumulator Usage { get; } = new();
    internal ConversationContext Context { get; } = new();
    internal ChatFixture(params string[] inputs)
    {
        Factory = new(Client);
        foreach (var text in inputs) Interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
    }
    internal Task<int> Run(BootstrapSession? session = null, CancellationToken cancellationToken = default)
    {
        var initial = session ?? GeminiFixtures.Session(); Factory.Sessions.Add(initial); Client.Session = initial;
        return new ChatApplication(Interaction, Factory, Context, Usage, "test instruction", new TestWorkspaceFilesFactory()).RunAsync(initial, cancellationToken);
    }
    internal static LlmResult Completed(string text = "answer") => new(LlmStatus.Completed, LlmErrorCode.None, text, [], LlmMessage.FromModel(new object()), new(1, 2, 3, 4, 5), true, false);
    internal static LlmResult Authentication() => new(LlmStatus.Failed, LlmErrorCode.AuthenticationFailed, "untrusted error", [], null, new(1, null, null, null, 1), true, false);
}

internal sealed class ScriptInteraction : IChatInteraction
{
    internal Queue<ModelApprovalDecision> ModelDecisions { get; } = new();
    internal List<ModelApprovalPreview> ModelPreviews { get; } = [];
    internal Action? OnModelApproval { get; set; }
    internal List<WorkApprovalPreview> WorkPreviews { get; } = [];
    internal Queue<WorkApprovalDecision> WorkDecisions { get; } = new();
    internal Queue<McpApprovalDecision> McpDecisions { get; } = new();
    internal List<McpApprovalPreview> McpPreviews { get; } = [];
    public ValueTask<McpApprovalDecision> ConfirmMcpAsync(McpApprovalPreview preview, CancellationToken token)
    { McpPreviews.Add(preview); return new(McpDecisions.Count > 0 ? McpDecisions.Dequeue() : McpApprovalDecision.Unavailable); }
    public ValueTask<WorkApprovalDecision> ConfirmWorkAsync(WorkApprovalPreview preview, CancellationToken token)
    { WorkPreviews.Add(preview); return new(WorkDecisions.Count > 0 ? WorkDecisions.Dequeue() : WorkApprovalDecision.Unavailable); }
    public ValueTask<ModelApprovalDecision> ConfirmModelAsync(ModelApprovalPreview preview, CancellationToken token)
    { ModelPreviews.Add(preview); OnModelApproval?.Invoke(); return new(ModelDecisions.Count > 0 ? ModelDecisions.Dequeue() : ModelApprovalDecision.Declined); }
    internal List<string> Remembered { get; } = [];
    internal int InputClears { get; private set; }
    internal Action<string>? OnRemember { get; set; }
    public void RememberAcceptedInput(string text) { Remembered.Add(text); OnRemember?.Invoke(text); }
    public void ClearInputState() { InputClears++; }
    internal Queue<Func<ChatInputResult>> Inputs { get; } = new();
    internal Queue<KeyReplacementChoice> Choices { get; } = new();
    internal Queue<Func<KeyInputResult>> Keys { get; } = new();
    internal List<ChatEvent> Events { get; } = [];
    internal Action<ChatControlSignal>? Signal { get; private set; }
    internal Action<Action<ChatControlSignal>>? OnBusy { get; set; }
    internal Action<Action<ChatControlSignal>>? OnRulesBusy { get; set; }
    internal Action<ChatEvent>? OnShow { get; set; }
    internal Action? OnChoice { get; set; }
    internal Action? OnBusyDisposed { get; set; }
    internal int KeyReads { get; private set; }
    internal int ChoiceReads { get; private set; }
    internal bool FailPolicyChange { get; set; }
    internal bool ObserverFails { get; set; }
    private int _policyChanges;
    public ValueTask<ChatInputResult> ReadInputAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Inputs.TryDequeue(out var input) ? input() : ChatInputResult.End(ChatInputStatus.EndOfInput));
    }
    public ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken cancellationToken)
    { ChoiceReads++; OnChoice?.Invoke(); return new(Choices.Count > 0 ? Choices.Dequeue() : KeyReplacementChoice.Exit); }
    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken)
    { KeyReads++; return new(Keys.Count > 0 ? Keys.Dequeue()() : KeyInputResult.End(KeyInputStatus.EndOfInput)); }
    public IDisposable ObserveBusyInput(Action<ChatControlSignal> onSignal)
    {
        if (ObserverFails) throw new IOException("FAKE_OBSERVER_ONLY");
        var rulesBusy = Events.LastOrDefault()?.Text == "正在讀取專案規則。";
        Signal = onSignal;
        if (rulesBusy) OnRulesBusy?.Invoke(onSignal); else OnBusy?.Invoke(onSignal);
        return new Scope(() => { Signal = null; if (!rulesBusy) OnBusyDisposed?.Invoke(); });
    }
    public void SetPolicy(Security.ISensitiveDataPolicy policy)
    { if (++_policyChanges > 1 && FailPolicyChange) throw new IOException("FAKE_POLICY_ONLY"); }
    public void Show(ChatEvent message) { Events.Add(message); OnShow?.Invoke(message); }
    private sealed class Scope(Action dispose) : IDisposable { public void Dispose() => dispose(); }
}

internal sealed class ScriptClient : ILlmClient, ILlmContextInspector
{
    internal Func<LlmRequest, ContextRequestMeasure>? Inspect { get; set; }
    ContextRequestMeasure ILlmContextInspector.Measure(LlmRequest request) => Inspect?.Invoke(request) ?? new ContextTestInspector(this).Measure(request);
    ContextTurnProjection ILlmContextInspector.ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages) =>
        new ContextTestInspector(this).ProjectCompletedTurn(turnId, messages);
    internal BootstrapSession? Session { get; set; }
    internal List<LlmRequest> Requests { get; } = [];
    internal List<LlmResult> Returned { get; } = [];
    internal bool DisposedWithLiveSession { get; private set; }
    internal bool DisposeFails { get; set; }
    internal Action? OnDispose { get; set; }
    internal Func<int, LlmRequest, CancellationToken, Task<LlmResult>> Respond { get; set; } = (_, _, _) => Task.FromResult(ChatFixture.Completed());
    public async Task<LlmResult> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var index = Requests.Count; Requests.Add(request);
        var result = await Respond(index, request, cancellationToken); Returned.Add(result); return result;
    }
    public ValueTask DisposeAsync()
    {
        _ = Session!.Secrets.AuthenticationKey.Length; DisposedWithLiveSession = true;
        OnDispose?.Invoke();
        if (DisposeFails) throw new IOException(GeminiFixtures.Key);
        return ValueTask.CompletedTask;
    }
}

internal sealed class ScriptFactory(ScriptClient initial) : IChatRuntimeFactory
{
    public ILlmContextInspector? CreateContextInspector(ILlmClient client) => client as ILlmContextInspector;
    internal List<BootstrapSession> Sessions { get; } = [];
    internal ScriptClient ReplacementClient { get; } = new();
    internal bool FailClient { get; set; }
    internal bool FailSession { get; set; }
    internal bool FailReplacementClient { get; set; }
    public BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput)
    {
        if (FailSession) throw new IOException(GeminiFixtures.Key);
        var session = new ChatRuntimeFactory().CreateReplacementSession(workspace, settings, acceptedInput);
        Sessions.Add(session); return session;
    }
    public ILlmClient CreateClient(BootstrapSession session)
    {
        if (FailClient || Sessions.Count > 1 && FailReplacementClient) throw new IOException(GeminiFixtures.Key);
        var client = Sessions.Count == 1 ? initial : ReplacementClient; client.Session = session; return client;
    }
}