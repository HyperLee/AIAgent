using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildChatTests
{
    [TestMethod]
    public async Task BUILDAC007_ProcessApprovalControlsAreScopedToTurnAndSingleReader()
    {
        foreach (var decision in new[] { ProcessApprovalDecision.Declined, ProcessApprovalDecision.Cancelled,
            ProcessApprovalDecision.EndOfInput, ProcessApprovalDecision.Unavailable })
        {
            using var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
            client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(Call()));
            var runtime = new ScriptFactory(client); runtime.Sessions.Add(session);
            var interaction = new ProcessChatInteraction { Decision = decision };
            interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("build"));
            interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
            var processes = new TestProcessFactory();
            var app = new ChatApplication(interaction, runtime, new(), new(), "instruction", new TestWorkspaceFilesFactory(), processes);
            interaction.OnApproval = () => Assert.AreEqual(ChatState.AwaitingProcessApproval, app.State);
            Assert.AreEqual(decision == ProcessApprovalDecision.Unavailable ? 1 : 0, await app.RunAsync(session, default));
            Assert.AreEqual(1, client.Requests.Count); Assert.AreEqual(1, interaction.Approvals);
            Assert.AreEqual(1, processes.Turns); Assert.IsTrue(processes.Disposed);
        }
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task BUILDAC031_UnconfirmedStopsSessionBeforeQueuedClearOrAnotherMessage(bool increaseLimit)
    {
        using var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
        client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(Call()));
        var runtime = new ScriptFactory(client); runtime.Sessions.Add(session);
        var interaction = new ProcessChatInteraction();
        if (increaseLimit) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("/budget set turn 20"));
        foreach (var input in new[] { "build", "/clear", "retry", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(input));
        var processes = new TestProcessFactory { Unconfirmed = true };
        Assert.AreEqual(1, await new ChatApplication(interaction, runtime, new(), new(), "instruction", new TestWorkspaceFilesFactory(), processes).RunAsync(session, default));
        Assert.AreEqual(1, client.Requests.Count); Assert.AreEqual(1, processes.Turns);
        Assert.IsTrue(interaction.Inner.Events.Any(e => e.StopReason == AgentStopReason.ProcessUnconfirmed));
        Assert.IsTrue(interaction.Inner.Events.Any(e => e.Agent?.Operation?.ProcessSummary?.EndState == ProcessEndState.Unconfirmed));
    }

    [TestMethod]
    public async Task BUILDAC033_RealSdkContainsNineToolsAndPreservesNativeProcessPairing()
    {
        var interaction = new ProcessChatInteraction();
        foreach (var input in new[] { "build", "/exit" }) interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(input));
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1
            ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"run_dotnet_task\",\"id\":\"process-1\",\"args\":{\"task\":\"build\",\"project_path\":\"a.csproj\"}},\"thoughtSignature\":\"c2ln\"}]")
            : GeminiFixtures.Text("done")));
        var usage = new UsageAccumulator();
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), usage,
            "instruction", new TestWorkspaceFilesFactory(), new TestProcessFactory()).RunAsync(WorkspaceFixtures.Session(), default));
        using var json = System.Text.Json.JsonDocument.Parse(handler.Bodies[1]);
        Assert.AreEqual(9, json.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations").GetArrayLength());
        var contents = json.RootElement.GetProperty("contents");
        Assert.AreEqual("c2ln", contents[1].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        var response = contents[2].GetProperty("parts")[0].GetProperty("functionResponse");
        Assert.AreEqual("process-1", response.GetProperty("id").GetString());
        Assert.AreEqual("build_succeeded", response.GetProperty("response").GetProperty("data").GetProperty("validation").GetString());
        Assert.AreEqual(2L, usage.Snapshot().DispatchedRequests);
    }
    private static LlmToolCall Call() => new(0, "p", "run_dotnet_task", GeminiFixtures.Element("{\"task\":\"build\",\"project_path\":\"a.csproj\"}"));
}

internal sealed class ProcessChatInteraction : IChatInteraction
{
    internal ScriptInteraction Inner { get; } = new();
    internal ProcessApprovalDecision Decision = ProcessApprovalDecision.Approved;
    internal Action? OnApproval;
    internal int Approvals;
    public ValueTask<ProcessApprovalDecision> ConfirmProcessAsync(ProcessApprovalPreview preview, CancellationToken token)
    { Approvals++; OnApproval?.Invoke(); return new(Decision); }
    public ValueTask<ChatInputResult> ReadInputAsync(CancellationToken token) => Inner.ReadInputAsync(token);
    public ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken token) => Inner.ChooseKeyReplacementAsync(token);
    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken token) => Inner.ReadKeyAsync(token);
    public IDisposable ObserveBusyInput(Action<ChatControlSignal> signal) => Inner.ObserveBusyInput(signal);
    public void SetPolicy(ISensitiveDataPolicy policy) => Inner.SetPolicy(policy);
    public void Show(ChatEvent message) => Inner.Show(message);
}

internal sealed class TestProcessFactory : IControlledDotnetTasksFactory
{
    internal bool Unconfirmed;
    internal int Turns;
    internal int Calls;
    internal bool Disposed;
    public bool IsAvailable => true;
    public IControlledDotnetTasks Create(ConfirmedWorkspace workspace, ISensitiveDataPolicy policy, AgentExecutionPolicy execution,
        IWorkspaceFiles files, Func<ProcessApprovalPreview, CancellationToken, ValueTask<ProcessApprovalDecision>> approve, Action<string> progress) => new Tasks(this, policy, approve);
    private sealed class Tasks(TestProcessFactory owner, ISensitiveDataPolicy policy,
        Func<ProcessApprovalPreview, CancellationToken, ValueTask<ProcessApprovalDecision>> approve) : IControlledDotnetTasks, IProcessTurnLifecycle
    {
        public void BeginTurn() => owner.Turns++;
        public async Task<DotnetTaskResult> RunAsync(DotnetTaskKind task, string path, CancellationToken token)
        {
            owner.Calls++;
            var choice = await approve(new("trusted test\n確認此單次受控 .NET 任務？[y/N] ", this, 0, null, "once"), token);
            if (choice != ProcessApprovalDecision.Approved)
                return new(ToolResponseWriter.Create(choice == ProcessApprovalDecision.Unavailable ? ToolStatus.Failed :
                    choice == ProcessApprovalDecision.Declined ? ToolStatus.Rejected : ToolStatus.Cancelled,
                    choice == ProcessApprovalDecision.Unavailable ? "unavailable" : choice == ProcessApprovalDecision.Declined ? "approval_declined" : "turn_cancelled", policy).WithProcessStop());
            var summary = new ProcessExecutionSummary(task, path, ProcessStartState.Started,
                owner.Unconfirmed ? ProcessEndState.Unconfirmed : ProcessEndState.Exited, owner.Unconfirmed ? null : 0,
                1, !owner.Unconfirmed, false, owner.Unconfirmed ? ValidationOutcome.NotVerified : ValidationOutcome.BuildSucceeded);
            return new(ProcessResultWriter.Create(owner.Unconfirmed ? ToolStatus.Failed : ToolStatus.Success,
                owner.Unconfirmed ? "process_unconfirmed" : "none", summary, null, policy));
        }
        public ValueTask DisposeAsync() { owner.Disposed = true; return ValueTask.CompletedTask; }
    }
}