using System.Net;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationWorkflowTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MULTIAC035_ThreeProfilesUseProductionFactoryConcurrentChildrenAndSingleParentSummary(bool reviewSecond)
    {
        foreach (var profile in ModelCatalog.Profiles)
        {
            var interaction = Inputs("/budget set turn 6", "/agents on", "inspect both", "/agents", "/exit");
            var barrier = new DelegationHttpBarrier(); var handlers = new List<DelegationHttpHandler>();
            var factory = new ChatRuntimeFactory(p => { var h = new DelegationHttpHandler(p, handlers.Count, barrier, reviewSecond); handlers.Add(h); return h; });
            var usage = new UsageAccumulator(); var files = new DelegationFilesFactory();
            var app = new ChatApplication(interaction, factory, new(), usage, "synthetic instruction", files, gitReadWorkspaceFactory: new DelegationWorkflowGitFactory());
            using var session = new BootstrapSession(new("/ws"), profile.Settings, new SessionSecrets(OpenAiTestData.Key.ToCharArray()));
            Assert.AreEqual(0, await app.RunAsync(session, default).WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.AreEqual(3, handlers.Count); Assert.AreEqual(2, barrier.Peak);
            Assert.AreEqual(2, handlers[0].Bodies.Count); Assert.AreEqual(0, handlers[0].LastToolCount);
            Assert.IsTrue(handlers.All(h => h.Official)); Assert.IsTrue(handlers.All(h => h.Disposed));
            Assert.AreEqual(6, usage.ProfileSnapshots().Single().GenerationsStarted);
            Assert.AreEqual(4, usage.ProfileSnapshots().Single().Purposes.Single(p => p.Purpose.ToString() == "DelegatedRead").GenerationsStarted);
            Assert.IsTrue(interaction.Events.Any(e => e.Text?.Contains("completed", StringComparison.Ordinal) == true));
            Assert.AreEqual(1, interaction.Remembered.Count);
            Assert.AreEqual(0, files.Files!.Snapshots.Count);
        }
    }
    [TestMethod]
    public async Task MULTIAC018_DefaultBudgetRejectsTwoChildrenWithoutCreatingClientsAndClosesTools()
    {
        var profile = ModelCatalog.Profiles[0]; var interaction = Inputs("/agents on", "inspect both", "/exit");
        var handlers = new List<DelegationHttpHandler>(); var barrier = new DelegationHttpBarrier();
        var factory = new ChatRuntimeFactory(p => { var h = new DelegationHttpHandler(p, handlers.Count, barrier); handlers.Add(h); return h; });
        using var session = new BootstrapSession(new("/ws"), profile.Settings, new SessionSecrets(OpenAiTestData.Key.ToCharArray()));
        Assert.AreEqual(0, await new ChatApplication(interaction, factory, new(), new(), "synthetic instruction", new DelegationFilesFactory()).RunAsync(session, default), string.Join(";", interaction.Events.Select(e => e.Kind + ":" + e.StopReason + ":" + e.ErrorCode)));

        Assert.AreEqual(1, handlers.Count); Assert.AreEqual(2, handlers[0].Bodies.Count); Assert.AreEqual(0, handlers[0].LastToolCount);
        Assert.IsTrue(Encoding.UTF8.GetString(handlers[0].Bodies[1]).Contains("delegation_budget_insufficient", StringComparison.Ordinal));
    }
    internal static ScriptInteraction Inputs(params string[] values)
    { var interaction = new ScriptInteraction(); foreach (var value in values) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(value)); return interaction; }
}
internal sealed class DelegationFilesFactory : IWorkspaceFilesFactory
{
    internal WorkspaceFiles? Files;
    public IWorkspaceFiles Create(BootstrapSession session, AgentExecutionPolicy? executionPolicy = null)
    {
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a.cs", Encoding.UTF8.GetBytes("line one\nline two"));
        return Files = new(session.Workspace, session.Policy, fs, executionPolicy: executionPolicy);
    }
}
internal sealed class DelegationHttpBarrier
{
    private int _entered;
    internal int Peak;
    private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal async Task Enter(CancellationToken token) { var count = Interlocked.Increment(ref _entered); Peak = Math.Max(Peak, count); if (count == 2) _both.TrySetResult(); await _both.Task.WaitAsync(token); }
}
internal sealed class DelegationHttpHandler(ModelProfile profile, int client, DelegationHttpBarrier barrier, bool reviewSecond = false) : HttpMessageHandler
{
    internal List<byte[]> Bodies { get; } = [];
    internal int LastToolCount;
    internal bool Disposed;
    internal bool Official = true;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var body = await request.Content!.ReadAsByteArrayAsync(token); Bodies.Add(body);
        Official &= request.RequestUri!.AbsoluteUri == profile.Destination && (profile.Provider == "openai" ? request.Headers.Authorization?.Parameter == OpenAiTestData.Key && !request.Headers.Contains("x-goog-api-key") : request.Headers.TryGetValues("x-goog-api-key", out var keys) && keys.Single() == OpenAiTestData.Key && request.Headers.Authorization is null);
        Official &= !Encoding.UTF8.GetString(body).Contains(OpenAiTestData.Key, StringComparison.Ordinal);
        using var parsed = JsonDocument.Parse(body); LastToolCount = ModelWorkflowHandler.ToolCount(parsed.RootElement, profile);
        if (client > 0 && Bodies.Count == 1) await barrier.Enter(token);
        var call = Bodies.Count == 1;
        var name = client == 0 ? "delegate_readonly" : reviewSecond && client == 2 ? "git_diff" : "read_file";
        var arguments = client == 0 ? """{"tasks":[{"role":"explore","objective":"inspect a","scope_path":".","acceptance":"cite a"},{"role":"explore","objective":"inspect b","scope_path":".","acceptance":"cite a"}]}""" : """{"path":"a.cs"}""";
        if (reviewSecond && client == 0) arguments = arguments.Replace("\"role\":\"explore\",\"objective\":\"inspect b\"", "\"role\":\"review\",\"objective\":\"inspect b\"", StringComparison.Ordinal);
        if (reviewSecond && client == 2) arguments = """{"snapshot_id":"g1","layer":"staged","path":"a.cs"}""";
        var text = client == 0 ? "Analysis from both children; no tests were run." : """{"version":1,"summary":"Observed two lines.","observations":[{"path":"a.cs","start_line":1,"end_line":2,"note":"Two source lines."}],"limitations":["No tests run."]}""";
        if (reviewSecond && client == 2) text = GitChatTests.Report("g1", "all");
        var response = profile.Provider == "gemini" ? call ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"" + name + "\",\"args\":" + arguments + "}}]") : GeminiFixtures.Text(text) :
            OpenAiTestData.Response(call ? "[{\"type\":\"function_call\",\"id\":\"item\",\"call_id\":\"call\",\"name\":\"" + name + "\",\"arguments\":" + JsonSerializer.Serialize(arguments) + "}]" : "[{\"type\":\"message\",\"id\":\"message\",\"role\":\"assistant\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":" + JsonSerializer.Serialize(text) + ",\"annotations\":[]}]}]");
        return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
    }
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
}
internal sealed class DelegationWorkflowGitFactory : AIAgent.Cli.Git.IGitReadWorkspaceFactory
{
    public AIAgent.Cli.Git.IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace) => new ReviewGitWorkspace();
}