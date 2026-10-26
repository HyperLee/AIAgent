using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanChatTests
{
    [TestMethod]
    public async Task PLANAC020_ForcedResetDoesNotRepublishLastOutcome()
    {
        var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
        client.Respond = (_, _, _) => Task.FromResult(new LlmResult(LlmStatus.Failed, LlmErrorCode.SensitiveOutput, "", [], null, null, true, true));
        var interaction = new PlanChatInteraction(); var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        var app = new ChatApplication(interaction, factory, new(), new(), "test instruction", new TestWorkspaceFilesFactory());
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("/plan"));
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("fail"));
        LastTurnOutcome? observed = null;
        interaction.Inner.Inputs.Enqueue(() => { observed = app.LastOutcome; return ChatInputResult.Submitted("/exit"); });
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.IsNull(observed);
    }

    [TestMethod]
    public async Task PLANAC003_ApprovalSwitchesModeAndWaitsForNextUserMessage()
    {
        var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
        var interaction = new PlanChatInteraction(); var context = new ConversationContext(); var usage = new UsageAccumulator();
        client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, "s", "submit_plan", PlanFixture.Submission(kind: "analysis"))) : ChatFixture.Completed());
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("/plan"));
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("make a plan"));
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("/execute"));
        interaction.OnPlan = (_, _) => { Assert.AreEqual(2, client.Requests.Count); return new(PlanApprovalDecision.Approved); };
        interaction.Inner.Inputs.Enqueue(() =>
        {
            Assert.AreEqual(2, client.Requests.Count); Assert.AreEqual(1, interaction.PlanApprovals);
            Assert.AreEqual(1, context.CompletedTurnCount); Assert.AreEqual(2L, usage.Snapshot().DispatchedRequests);
            return ChatInputResult.Submitted("continue");
        });
        foreach (var text in new[] { "/plan", "/execute", "/tasks", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        Assert.AreEqual(0, await new ChatApplication(interaction, factory, context, usage, "test instruction", new TestWorkspaceFilesFactory()).RunAsync(session, default));
        Assert.AreEqual(1, interaction.PlanApprovals); Assert.AreEqual(3, client.Requests.Count);
        Assert.IsTrue(client.Requests[^1].SystemInstruction.Contains("\"approval\":\"approved\"", StringComparison.Ordinal));
        Assert.IsTrue(interaction.Inner.Events.Any(e => e.Kind == ChatEventKind.TaskSummary && e.PlanSummary?.PlanId == "p1"));
    }

    [TestMethod]
    public async Task PLANAC004_PlanApprovalControlDecisionsHaveSeparateExitSemantics()
    {
        foreach (var decision in new[] { PlanApprovalDecision.Declined, PlanApprovalDecision.Cancelled, PlanApprovalDecision.EndOfInput, PlanApprovalDecision.Unavailable })
        {
            var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
            client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "submit_plan", PlanFixture.Submission())) : ChatFixture.Completed());
            var interaction = new PlanChatInteraction { OnPlan = (_, _) => new(decision) };
            foreach (var text in new[] { "/plan", "plan", "/execute", "/tasks", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
            var factory = new ScriptFactory(client); factory.Sessions.Add(session);
            Assert.AreEqual(decision == PlanApprovalDecision.Unavailable ? 1 : 0, await new ChatApplication(interaction, factory, new(), new(), "test instruction", new TestWorkspaceFilesFactory()).RunAsync(session, default));
            Assert.AreEqual(2, client.Requests.Count);
            if (decision is PlanApprovalDecision.Declined or PlanApprovalDecision.Cancelled)
                Assert.IsTrue(interaction.Inner.Events.Any(e => e.Text?.Contains("維持 Plan", StringComparison.Ordinal) == true));
        }
    }

    [TestMethod]
    public async Task PLANAC004_InterruptDuringApprovalCannotApproveLateYesOrExit130()
    {
        var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
        client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "submit_plan", PlanFixture.Submission())) : ChatFixture.Completed());
        var interaction = new PlanChatInteraction(); var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        var app = new ChatApplication(interaction, factory, new(), new(), "test instruction", new TestWorkspaceFilesFactory());
        interaction.OnPlan = (_, token) => { Assert.AreEqual(ChatState.AwaitingPlanApproval, app.State); app.Interrupt(); Assert.IsTrue(token.IsCancellationRequested); return new(PlanApprovalDecision.Approved); };
        foreach (var text in new[] { "/plan", "plan", "/execute", "/tasks", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.IsTrue(interaction.Inner.Events.Any(e => e.Text?.Contains("／draft", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task PLANAC020_ClearAndSensitiveResetRetainPlanModeButRemovePlanProjection()
    {
        foreach (var reset in new[] { false, true })
        {
            var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
            client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "submit_plan", PlanFixture.Submission())) :
                reset && i == 2 ? new LlmResult(LlmStatus.Failed, LlmErrorCode.SensitiveOutput, "", [], null, null, true, true) : ChatFixture.Completed());
            var interaction = new PlanChatInteraction();
            foreach (var text in new[] { "/plan", "plan", reset ? "fail" : "/clear", "next", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
            var factory = new ScriptFactory(client); factory.Sessions.Add(session);
            await new ChatApplication(interaction, factory, new(), new(), "test instruction", new TestWorkspaceFilesFactory()).RunAsync(session, default);
            Assert.IsTrue(client.Requests[^1].SystemInstruction.Contains("\"mode\":\"plan\",\"plan\":null}", StringComparison.Ordinal));
            Assert.AreEqual(9, client.Requests[^1].Tools.Count);
        }
    }

    [TestMethod]
    public async Task PLANAC029_KeyReplacementClearsPlanRetainsModeAndCumulativeUsage()
    {
        var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
        client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "submit_plan", PlanFixture.Submission())) : i == 1 ? ChatFixture.Completed() : ChatFixture.Authentication());
        var interaction = new PlanChatInteraction(); interaction.Inner.Choices.Enqueue(KeyReplacementChoice.Reenter);
        interaction.Inner.Keys.Enqueue(() => KeyInputResult.Accept("SECOND_SYNTHETIC_CREDENTIAL".AsSpan()));
        foreach (var text in new[] { "/plan", "plan", "auth", "next", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        var factory = new ScriptFactory(client); factory.Sessions.Add(session); var usage = new UsageAccumulator();
        Assert.AreEqual(0, await new ChatApplication(interaction, factory, new(), usage, "test instruction", new TestWorkspaceFilesFactory()).RunAsync(session, default));
        Assert.AreEqual(1, factory.ReplacementClient.Requests.Count);
        Assert.IsTrue(factory.ReplacementClient.Requests[0].SystemInstruction.Contains("\"mode\":\"plan\",\"plan\":null}", StringComparison.Ordinal));
        Assert.AreEqual(4L, usage.Snapshot().DispatchedRequests);
    }
}

internal sealed class PlanChatInteraction : IChatInteraction
{
    internal ScriptInteraction Inner { get; } = new();
    internal Func<PlanApprovalPreview, CancellationToken, ValueTask<PlanApprovalDecision>> OnPlan { get; set; } = (_, _) => new(PlanApprovalDecision.Approved);
    internal Func<FileChangePreview, FileApprovalDecision>? OnFile { get; set; }
    internal int PlanApprovals { get; private set; }
    internal int FileApprovals { get; private set; }
    public ValueTask<ChatInputResult> ReadInputAsync(CancellationToken token) => Inner.ReadInputAsync(token);
    public ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken token) => Inner.ChooseKeyReplacementAsync(token);
    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken token) => Inner.ReadKeyAsync(token);
    public IDisposable ObserveBusyInput(Action<ChatControlSignal> onSignal) => Inner.ObserveBusyInput(onSignal);
    public void SetPolicy(ISensitiveDataPolicy policy) => Inner.SetPolicy(policy);
    public void RememberAcceptedInput(string text) => Inner.RememberAcceptedInput(text);
    public void ClearInputState() => Inner.ClearInputState();
    public void Show(ChatEvent message) => Inner.Show(message);
    public ValueTask<PlanApprovalDecision> ConfirmPlanAsync(PlanApprovalPreview preview, CancellationToken token)
    { PlanApprovals++; return OnPlan(preview, token); }
    public ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken token)
    { FileApprovals++; return new(OnFile?.Invoke(preview) ?? FileApprovalDecision.Approved); }
}