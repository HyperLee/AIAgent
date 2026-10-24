using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class AgentLifecycleTests
{
    [TestMethod]
    public async Task LOOPAC024_OutputFailureWithCancellationStillExitsOne()
    {
        var root = WorkspaceFixtures.NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, "a"), "safe");
        var interaction = new ScriptInteraction();
        interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("read"));
        interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
        var client = new ScriptClient { Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0))) };
        var session = WorkspaceFixtures.Session(root);
        var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        var app = new ChatApplication(interaction, factory, new(), new(), "instruction", new WorkspaceFilesFactory());
        interaction.OnShow = e =>
        {
            if (e.Agent?.Operation?.Status != ToolStatus.Success) return;
            app.Interrupt(); throw new IOException(GeminiFixtures.Key);
        };
        Assert.AreEqual(1, await app.RunAsync(session, default), "LOOP-AC-024");
        Assert.AreEqual(1, client.Requests.Count, "LOOP-AC-024");
    }

    [TestMethod]
    public async Task LOOPAC024_CancelDuringAuthUsageSuppressesKeyPrompt()
    {
        var fixture = new ChatFixture("read", "/exit");
        fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new WorkspaceFilesFactory());
        fixture.Interaction.OnShow = e => { if (e.Kind == ChatEventKind.Usage) app.Interrupt(); };
        var session = WorkspaceFixtures.Session(); fixture.Factory.Sessions.Add(session);
        Assert.AreEqual(0, await app.RunAsync(session, default), "LOOP-AC-024");
        Assert.AreEqual(0, fixture.Interaction.ChoiceReads, "LOOP-AC-024");
    }

    [TestMethod]
    public async Task LOOPAC022_ToolCancellationWaitsForLateReadBeforeAcceptingNextRound()
    {
        await using var f = new RunnerFixture();
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.FileSystem.OnRead = async _ => { started.TrySetResult(); await release.Task; };
        f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1)));
        var run = f.Runner.RunAsync([LlmMessage.FromUserText("read")], cancel.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        Assert.IsFalse(run.IsCompleted, "LOOP-AC-022");
        release.SetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(result.Status == AgentTurnStatus.Cancelled && f.Workspace.Snapshots.Count == 0 && result.Operations.Count == 2, "LOOP-AC-022");
        f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed());
        Assert.AreEqual(AgentTurnStatus.Completed, (await f.Runner.RunAsync([LlmMessage.FromUserText("next")], default)).Status, "LOOP-AC-021");
    }

    [TestMethod]
    public async Task LOOPAC014_UnavailableServiceAndUnexpectedToolExceptionRetainPriorRead()
    {
        foreach (var unexpected in new[] { false, true })
        {
            await using var f = new RunnerFixture();
            var registry = new ToolRegistry([new ReadFileTool(f.Workspace, f.Session.Policy), new ThrowingTool()], f.Session.Policy);
            if (!unexpected) f.OnEvent = e => { if (e.Operation?.Status == ToolStatus.Success) f.Workspace.DisposeAsync().AsTask().GetAwaiter().GetResult(); };
            f.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0),
                unexpected ? new(1, null, "throw", GeminiFixtures.Element("{\"path\":\"a\"}")) : RunnerFixture.Read(1), RunnerFixture.Read(2)));
            var runner = new AgentRunner(f.Client, registry, f.Session.Policy, "instruction", f.Usage, e => f.OnEvent?.Invoke(e));
            var result = await runner.RunAsync([LlmMessage.FromUserText("read")], default);
            Assert.IsTrue(result.StopReason == AgentStopReason.LocalFailure && result.Operations.Count == 3 &&
                result.Operations[0].Status == ToolStatus.Success && result.Operations[1].Status == ToolStatus.Failed &&
                result.Operations[2].Status == ToolStatus.NotExecuted && f.Client.Requests.Count == 1, "LOOP-AC-014");
        }
    }

    [TestMethod]
    public async Task LOOPAC020_ClearDropsLastOutcomeAndSnapshotsWhileKeepingProgramUsage()
    {
        var root = WorkspaceFixtures.NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, "a"), "safe");
        var interaction = new ScriptInteraction();
        var client = new ScriptClient { Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(RunnerFixture.Read(0)) : ChatFixture.Completed()) };
        var workspaceFactory = new ObservedWorkspaceFactory();
        var usage = new UsageAccumulator();
        var session = WorkspaceFixtures.Session(root);
        var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        var app = new ChatApplication(interaction, factory, new(), usage, "instruction", workspaceFactory);
        interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("read"));
        interaction.Inputs.Enqueue(() =>
        {
            Assert.IsTrue(app.LastOutcome is { GenerationsStarted: 2, Operations.Count: 1 } && workspaceFactory.Services[0].Snapshots.Count == 1, "LOOP-AC-020");
            return ChatInputResult.Submitted("/clear");
        });
        interaction.Inputs.Enqueue(() =>
        {
            Assert.IsTrue(app.LastOutcome is null && workspaceFactory.Services[0].Snapshots.Count == 0 && usage.Snapshot().DispatchedRequests == 2, "LOOP-AC-020");
            return ChatInputResult.Submitted("/exit");
        });
        Assert.AreEqual(0, await app.RunAsync(session, default), "LOOP-AC-020");
        Assert.IsNull(app.LastOutcome, "LOOP-AC-020");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LOOPAC025_CancelDuringCommittedAuthCleanupNeverOpensKeyReaders(bool duringClientDisposal)
    {
        var fixture = new ChatFixture("request", "/exit");
        fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "instruction", new WorkspaceFilesFactory());
        var session = WorkspaceFixtures.Session(); fixture.Factory.Sessions.Add(session);
        if (duringClientDisposal) fixture.Client.OnDispose = app.Interrupt;
        else fixture.Interaction.OnBusyDisposed = app.Interrupt;
        Assert.AreEqual(130, await app.RunAsync(session, default), "LOOP-AC-025");
        Assert.IsTrue(fixture.Interaction.ChoiceReads == 0 && fixture.Interaction.KeyReads == 0 && fixture.Client.Requests.Count == 1, "LOOP-AC-025");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task LOOPAC024_UnavailableServiceAlwaysExitsOneDespiteCancelOrUnsafeProjection(bool projectionFails, bool cancelTurn)
    {
        var interaction = new ScriptInteraction();
        interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("request"));
        interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
        var client = new ScriptClient { Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0), RunnerFixture.Read(1))) };
        var factory = new ScriptFactory(client);
        var session = WorkspaceFixtures.Session(key: projectionFails ? "status" : GeminiFixtures.Key); factory.Sessions.Add(session);
        var app = new ChatApplication(interaction, factory, new(), new(), "instruction", new UnavailableWorkspaceFactory());
        interaction.OnShow = e => { if (cancelTurn && e.Agent?.Operation?.Status == ToolStatus.Failed) app.Interrupt(); };
        Assert.AreEqual(1, await app.RunAsync(session, default), "LOOP-AC-024/028");
        Assert.AreEqual(1, client.Requests.Count, "LOOP-AC-024");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task LOOPAC014_UnexpectedToolExceptionKeepsFatalCauseAcrossCancellationAndProjection(bool projectionFails, bool cancelTurn)
    {
        using var session = WorkspaceFixtures.Session(key: projectionFails ? "status" : GeminiFixtures.Key);
        using var cancel = new CancellationTokenSource();
        var registry = new ToolRegistry([new ThrowingTool()], session.Policy);
        var client = new ScriptClient
        {
            Session = session,
            Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
            new(0, null, "throw", GeminiFixtures.Element("{\"path\":\"a\"}")), RunnerFixture.Read(1)))
        };
        var result = await new AgentRunner(client, registry, session.Policy, "instruction", new(), e =>
        { if (cancelTurn && e.Operation?.Status == ToolStatus.Failed) cancel.Cancel(); })
            .RunAsync([LlmMessage.FromUserText("request")], cancel.Token);
        Assert.IsTrue(result.Status == AgentTurnStatus.Failed && result.StopReason == AgentStopReason.LocalFailure &&
            result.RequiresContextReset == projectionFails && result.Operations.Count == 2 &&
            result.Operations[0].Status == ToolStatus.Failed && result.Operations[1].Status == ToolStatus.NotExecuted, "LOOP-AC-014/028");
        await client.DisposeAsync();
    }
}

internal sealed class ThrowingTool : ITool
{
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition { get; } = WorkspaceToolProjection.Definition("throw", "controlled failure");
    public Task<ToolExecutionResult> ExecuteAsync(System.Text.Json.JsonElement arguments, CancellationToken cancellationToken) => throw new IOException(GeminiFixtures.Key);
}

internal sealed class ObservedWorkspaceFactory : IWorkspaceFilesFactory
{
    internal List<IWorkspaceFiles> Services { get; } = [];
    public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null)
    {
        var service = new WorkspaceFilesFactory().Create(session, executionPolicy); Services.Add(service); return service;
    }
}

internal sealed class UnavailableWorkspaceFactory : IWorkspaceFilesFactory
{
    public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null)
    {
        var workspace = new WorkspaceFilesFactory().Create(session, executionPolicy);
        workspace.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return workspace;
    }
}