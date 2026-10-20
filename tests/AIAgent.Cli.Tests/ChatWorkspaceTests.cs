using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ChatWorkspaceTests
{
    [TestMethod]
    public async Task WSAC025_ClearAndForcedResetsClearRealSnapshotsWithoutGeneratingTools()
    {
        foreach (var mode in new[] { "clear", "flag", "malformed", "tool", "sensitive" })
        {
            var fixture = new ChatFixture(); var factory = new ChatWorkspaceFactory();
            fixture.Interaction.Inputs.Enqueue(() =>
            {
                Seed(factory.Services[0]); return ChatInputResult.Submitted(mode == "clear" ? "/clear" : "one");
            });
            fixture.Interaction.Inputs.Enqueue(() =>
            {
                Assert.IsTrue(factory.Services[0].Snapshots.Count == 0, "WS-AC-025");
                return ChatInputResult.Submitted("next");
            });
            fixture.Interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
            fixture.Client.Respond = (index, _, _) => Task.FromResult(index > 0 ? ChatFixture.Completed() : mode switch
            {
                "flag" => new(LlmStatus.Completed, LlmErrorCode.None, "safe", [], LlmMessage.FromModel(new object()), null, true, true),
                "malformed" => new(LlmStatus.Completed, LlmErrorCode.None, "safe", [], null, null, true, false),
                "tool" => new(LlmStatus.ToolCalls, LlmErrorCode.None, "", [], LlmMessage.FromModel(new object()), null, true, false),
                "sensitive" => ChatFixture.Completed(GeminiFixtures.Key),
                _ => ChatFixture.Completed()
            });
            Assert.IsTrue(await Run(fixture, factory) == 0, "WS-AC-025");
            Assert.IsTrue(fixture.Client.Requests.Count == (mode == "clear" ? 1 : 2) && fixture.Client.Requests.All(request => request.Tools.Count == 0), "WS-AC-025");
            Assert.IsTrue(factory.Services.All(service => service.Snapshots.Count == 0), "WS-AC-025");
            WorkspaceFixtures.Check(await factory.Services[0].ReadFileAsync("a", default), WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-025");
        }
    }

    [TestMethod]
    public async Task WSAC026_RekeyDisposesOldServiceBeforeNewSessionAndBindsNewPolicy()
    {
        var fixture = new ChatFixture(); var factory = new ChatWorkspaceFactory();
        fixture.Interaction.Inputs.Enqueue(() => { Seed(factory.Services[0]); return ChatInputResult.Submitted("one"); });
        fixture.Interaction.Inputs.Enqueue(() =>
        {
            Assert.IsTrue(factory.Services.Count == 2 && factory.Services[0].Snapshots.Count == 0, "WS-AC-026");
            WorkspaceFixtures.Check(factory.Services[0].ReadFileAsync("a", default).GetAwaiter().GetResult(), WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-026");
            Assert.IsTrue(factory.Services[1].ReadFileAsync("FAKE_REPLACEMENT_ONLY", default).GetAwaiter().GetResult().ErrorCode == WorkspaceErrorCode.SensitiveContent, "WS-AC-026");
            Seed(factory.Services[1]); return ChatInputResult.Submitted("new question");
        });
        fixture.Interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
        fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
        fixture.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
        fixture.Interaction.Keys.Enqueue(() => KeyInputResult.Accept("FAKE_REPLACEMENT_ONLY"));
        factory.OnCreate = index =>
        {
            if (index != 1) return;
            Assert.IsTrue(factory.Services[0].Snapshots.Count == 0, "WS-AC-026");
            WorkspaceFixtures.Check(factory.Services[0].ReadFileAsync("a", default).GetAwaiter().GetResult(), WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-026");
        };
        Assert.IsTrue(await Run(fixture, factory) == 0, "WS-AC-026");
        Assert.IsTrue(factory.Services.All(service => service.Snapshots.Count == 0) && fixture.Usage.Snapshot().DispatchedRequests == 2, "WS-AC-026");
    }

    [TestMethod]
    public async Task WSAC026_ConstructionSwitchAndTerminationFailuresDisposeEveryService()
    {
        foreach (var mode in new[] { "initial-workspace", "replacement-workspace", "session", "client", "policy", "eof", "cancel" })
        {
            var fixture = new ChatFixture(); var factory = new ChatWorkspaceFactory();
            factory.FailIndex = mode == "initial-workspace" ? 0 : mode == "replacement-workspace" ? 1 : -1;
            fixture.Interaction.Inputs.Enqueue(() => { Seed(factory.Services[0]); return ChatInputResult.Submitted("one"); });
            fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
            fixture.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
            fixture.Interaction.Keys.Enqueue(() => mode == "eof" ? KeyInputResult.End(KeyInputStatus.EndOfInput) : mode == "cancel" ? KeyInputResult.End(KeyInputStatus.Cancelled) : KeyInputResult.Accept("FAKE_REPLACEMENT_ONLY"));
            fixture.Factory.FailSession = mode == "session";
            fixture.Factory.FailReplacementClient = mode == "client";
            fixture.Interaction.FailPolicyChange = mode == "policy";
            var exit = await Run(fixture, factory);
            Assert.IsTrue(exit == (mode == "eof" ? 0 : mode == "cancel" ? 130 : 1), "WS-AC-026");
            foreach (var service in factory.Services)
            {
                Assert.IsTrue(service.Snapshots.Count == 0, "WS-AC-026");
                WorkspaceFixtures.Check(await service.ReadFileAsync("a", default), WorkspaceStatus.Failed, WorkspaceErrorCode.Unavailable, "WS-AC-026");
            }
            Assert.IsTrue(fixture.Factory.Sessions.All(IsDisposed), "WS-AC-026");
        }
    }

    private static Task<int> Run(ChatFixture fixture, IWorkspaceFilesFactory workspaceFactory)
    {
        var session = WorkspaceFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        return new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "test instruction", workspaceFactory).RunAsync(session, default);
    }
    private static void Seed(IWorkspaceFiles files) => Assert.IsTrue(files.ReadFileAsync("a", default).GetAwaiter().GetResult().Status == WorkspaceStatus.Success && files.Snapshots.Count == 1, "WS-AC-025-seed");
    private static bool IsDisposed(BootstrapSession session)
    { try { session.Policy.ContainsCurrentKey("safe"); return false; } catch (ObjectDisposedException) { return true; } }
}

internal sealed class ChatWorkspaceFactory : IWorkspaceFilesFactory
{
    internal List<IWorkspaceFiles> Services { get; } = [];
    internal int FailIndex { get; set; } = -1;
    internal Action<int>? OnCreate { get; set; }
    public IWorkspaceFiles Create(BootstrapSession session)
    {
        OnCreate?.Invoke(Services.Count);
        if (Services.Count == FailIndex) throw new IOException("FAKE_WORKSPACE_CONSTRUCTION_ONLY");
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [97]);
        var service = new WorkspaceFiles(session.Workspace, session.Policy, fs); Services.Add(service); return service;
    }
}