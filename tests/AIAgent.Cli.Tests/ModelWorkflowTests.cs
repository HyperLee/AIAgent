using System.Net;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Mcp;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ModelWorkflowTests
{
    [TestMethod]
    public async Task MODELAC032_AllProfilesRetainFullMcpCatalogAndFreshPerSendApproval()
    {
        foreach (var profile in ModelCatalog.Profiles)
        {
            var interaction = Inputs("/mcp connect learn", "search", "/exit"); interaction.McpDecisions.Enqueue(McpApprovalDecision.Approved);
            var handler = new ModelWorkflowHandler(profile, "mcp"); var external = new McpHttpFixture();
            var app = new ChatApplication(interaction, new ChatRuntimeFactory(_ => handler), new(), new(), "synthetic instruction", new TestWorkspaceFilesFactory(),
                mcpClientFactory: guard => new McpReadClient(guard, external));
            Assert.AreEqual(0, await app.RunAsync(Session(profile), default));
            Assert.AreEqual(2, handler.Bodies.Count); Assert.AreEqual(1, interaction.McpPreviews.Count); Assert.AreEqual(1, external.Calls.Count);
            foreach (var bytes in handler.Bodies)
            {
                using var request = JsonDocument.Parse(bytes);
                Assert.AreEqual(15, ModelWorkflowHandler.ToolCount(request.RootElement, profile));
            }
            Assert.IsTrue(external.Closed);
        }
    }

    [TestMethod]
    public async Task MODELAC025_RevisitingProfilesKeepsThreeGroupsAndSharedProgramConsumption()
    {
        var profiles = ModelCatalog.Profiles;
        var interaction = Inputs("one", "/model use " + profiles[1].Provider + " " + profiles[1].Model, "two",
            "/model use " + profiles[2].Provider + " " + profiles[2].Model, "three",
            "/model use " + profiles[0].Provider + " " + profiles[0].Model, "four", "/exit");
        for (var i = 0; i < 3; i++) { interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved); interaction.Keys.Enqueue(() => KeyInputResult.Accept(OpenAiTestData.Key.AsSpan())); }
        var usage = new UsageAccumulator();
        Assert.AreEqual(0, await App(interaction, new ChatRuntimeFactory(p => new ModelWorkflowHandler(p)), usage, new()).RunAsync(Session(profiles[0]), default));
        Assert.AreEqual(3, usage.ProfileSnapshots().Count);
        Assert.AreEqual(2L, usage.ProfileSnapshots().Single(p => p.Profile == profiles[0]).GenerationsStarted);
        Assert.AreEqual(4L, usage.Snapshot().DispatchedRequests);
        Assert.AreEqual(4, interaction.Events.Last(e => e.Kind == ChatEventKind.Usage).ProgramGenerationsStarted);
    }

    [TestMethod]
    public async Task MODELAC022_SwitchCannotRefundLastProgramGeneration()
    {
        var a = ModelCatalog.Profiles[0]; var b = ModelCatalog.Profiles[2];
        var interaction = Inputs("/budget set program 1", "one", "/model use " + b.Provider + " " + b.Model, "two", "/exit");
        interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved); interaction.Keys.Enqueue(() => KeyInputResult.Accept(OpenAiTestData.Key.AsSpan()));
        var handlers = new List<ModelWorkflowHandler>(); var usage = new UsageAccumulator();
        var factory = new ChatRuntimeFactory(p => { var h = new ModelWorkflowHandler(p); handlers.Add(h); return h; });
        Assert.AreEqual(0, await App(interaction, factory, usage, new()).RunAsync(Session(a), default));
        Assert.AreEqual(1, handlers[0].Bodies.Count); Assert.AreEqual(0, handlers[1].Bodies.Count);
        Assert.AreEqual(1L, usage.Snapshot().DispatchedRequests);
        Assert.IsTrue(interaction.Events.Any(e => e.StopReason == AgentStopReason.ProgramGenerationLimit));
    }
    [TestMethod]
    public async Task MODELAC016_AllProfilesExecuteSameLocalToolThroughProductFactory()
    {
        foreach (var profile in ModelCatalog.Profiles)
        {
            var interaction = Inputs("read", "/exit"); var handler = new ModelWorkflowHandler(profile, "list");
            var usage = new UsageAccumulator(); var context = new ConversationContext();
            var completed = 0;
            interaction.OnShow = e => { if (e.Kind == ChatEventKind.Answer) completed = context.Capture().CompletedTurns.Count; };
            var app = App(interaction, new ChatRuntimeFactory(_ => handler), usage, context);
            Assert.AreEqual(0, await app.RunAsync(Session(profile), default));
            Assert.AreEqual(2, handler.Bodies.Count); Assert.IsTrue(handler.OfficialAndAuthenticated);
            Assert.IsTrue(interaction.Events.Any(e => e.Agent?.Operation?.Status == ToolStatus.Success));
            Assert.AreEqual(1, completed);
            Assert.AreEqual(2L, usage.ProfileSnapshots().Single().GenerationsStarted);
        }
    }

    [TestMethod]
    public async Task MODELAC021_AllProfilesCompactAndSaveWithSameWhitelistAndBudget()
    {
        foreach (var profile in ModelCatalog.Profiles)
        {
            var interaction = Inputs("one", "two", "three", "four", "five", "six", "/work save title", "/exit");
            interaction.WorkDecisions.Enqueue(WorkApprovalDecision.Approved);
            var handler = new ModelWorkflowHandler(profile); var usage = new UsageAccumulator(); var store = new MemoryWorkStore();
            Assert.AreEqual(0, await App(interaction, new ChatRuntimeFactory(_ => handler), usage, new(), store).RunAsync(Session(profile), default));
            Assert.AreEqual(8, handler.Bodies.Count); Assert.AreEqual(1, store.Publishes);
            var purposes = usage.ProfileSnapshots().Single().Purposes;
            Assert.AreEqual(6L, purposes.Single(p => p.Purpose == GenerationPurpose.Conversation).GenerationsStarted);
            Assert.AreEqual(1L, purposes.Single(p => p.Purpose == GenerationPurpose.Compaction).GenerationsStarted);
            Assert.AreEqual(1L, purposes.Single(p => p.Purpose == GenerationPurpose.WorkSaveSummary).GenerationsStarted);
            var preview = interaction.WorkPreviews.Single().DisplayBlock;
            Assert.IsTrue(preview.Contains("Provider：" + profile.Provider, StringComparison.Ordinal));
            Assert.IsTrue(preview.Contains(profile.Destination, StringComparison.Ordinal));
            var saved = Encoding.UTF8.GetString(store.Files.Values.Single().Bytes.Span);
            Assert.IsFalse(saved.Contains(profile.Model, StringComparison.Ordinal));
            Assert.IsFalse(saved.Contains("native-item", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task MODELAC008_AllDirectedSwitchesUseNewOwnerAndEmptyPlanHistory()
    {
        foreach (var from in ModelCatalog.Profiles)
            foreach (var to in ModelCatalog.Profiles.Where(p => p != from))
            {
                var interaction = Inputs("first", "/model use " + to.Provider + " " + to.Model, "second", "/exit");
                interaction.ModelDecisions.Enqueue(ModelApprovalDecision.Approved);
                interaction.Keys.Enqueue(() => KeyInputResult.Accept(OpenAiTestData.Key.AsSpan()));
                var handlers = new List<ModelWorkflowHandler>(); var usage = new UsageAccumulator();
                var factory = new ChatRuntimeFactory(profile => { var handler = new ModelWorkflowHandler(profile); handlers.Add(handler); return handler; });
                Assert.AreEqual(0, await App(interaction, factory, usage, new()).RunAsync(Session(from), default));
                Assert.AreEqual(2, handlers.Count); Assert.AreEqual(1, interaction.KeyReads);
                Assert.IsTrue(handlers.All(h => h.OfficialAndAuthenticated && h.Bodies.Count == 1));
                using var request = JsonDocument.Parse(handlers[1].Bodies[0]);
                var root = request.RootElement;
                var messages = root.GetProperty(to.Provider == "gemini" ? "contents" : "input");
                Assert.AreEqual(1, messages.GetArrayLength());
                Assert.AreEqual(9, ModelWorkflowHandler.ToolCount(root, to));
                Assert.AreEqual(2, usage.ProfileSnapshots().Count);
            }
    }

    [TestMethod]
    public async Task MODELAC030_CrossProviderResumeUsesCurrentDestinationWithoutGeneration()
    {
        var store = new MemoryWorkStore(); var initialProfile = ModelCatalog.Profiles[0];
        var first = Inputs("/context pin safe background", "/work save title", "/exit"); first.WorkDecisions.Enqueue(WorkApprovalDecision.Approved);
        Assert.AreEqual(0, await App(first, new ChatRuntimeFactory(p => new ModelWorkflowHandler(p)), new(), new(), store).RunAsync(Session(initialProfile), default));
        var current = ModelCatalog.Find("openai", "gpt-6.1-terra")!;
        var resumed = Inputs("/model", "/exit"); resumed.WorkDecisions.Enqueue(WorkApprovalDecision.Approved);
        var handler = new ModelWorkflowHandler(current);
        Assert.AreEqual(0, await App(resumed, new ChatRuntimeFactory(_ => handler), new(), new(), store).RunAsync(Session(current, store.Files.Keys.Single()), default));
        Assert.AreEqual(0, handler.Bodies.Count);
        Assert.IsTrue(resumed.WorkPreviews.Single().DisplayBlock.Contains("https://api.openai.com/v1/responses", StringComparison.Ordinal));
        Assert.IsTrue(resumed.Events.Any(e => e.Text == "[Plan]"));
    }

    [TestMethod]
    public async Task MODELAC032_AllProfilesReviewWithExactlyThreeGitTools()
    {
        foreach (var profile in ModelCatalog.Profiles)
        {
            var interaction = Inputs("/review staged", "/review show", "/exit"); var handler = new ModelWorkflowHandler(profile, "review");
            var usage = new UsageAccumulator(); var reader = new ReviewGitWorkspace();
            var app = new ChatApplication(interaction, new ChatRuntimeFactory(_ => handler), new(), usage, "synthetic instruction", new TestWorkspaceFilesFactory(), gitReadWorkspaceFactory: new ModelGitFactory(reader));
            Assert.AreEqual(0, await app.RunAsync(Session(profile), default));
            Assert.AreEqual(2, handler.Bodies.Count);
            Assert.IsTrue(interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
            foreach (var bytes in handler.Bodies)
            {
                using var request = JsonDocument.Parse(bytes);
                Assert.AreEqual(3, ModelWorkflowHandler.ToolCount(request.RootElement, profile));
            }
            Assert.AreEqual(2L, usage.ProfileSnapshots().Single().Purposes.Single(p => p.Purpose == GenerationPurpose.Review).GenerationsStarted);
        }
    }

    private static ScriptInteraction Inputs(params string[] texts)
    { var result = new ScriptInteraction(); foreach (var text in texts) result.Inputs.Enqueue(() => ChatInputResult.Submitted(text)); return result; }
    private static BootstrapSession Session(ModelProfile profile, string? resume = null) => new(new("/ws"), profile.Settings, new SessionSecrets(OpenAiTestData.Key.ToCharArray()), resume);
    private static ChatApplication App(ScriptInteraction interaction, ChatRuntimeFactory factory, UsageAccumulator usage, ConversationContext context, MemoryWorkStore? store = null) =>
        new(interaction, factory, context, usage, "synthetic instruction", new TestWorkspaceFilesFactory(), workSaveStoreFactory: store is null ? null : () => store);
    private sealed class ModelGitFactory(ReviewGitWorkspace reader) : IGitReadWorkspaceFactory
    { public IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace) => reader; }
}

internal sealed class ModelWorkflowHandler(ModelProfile profile, string mode = "plain") : HttpMessageHandler
{
    internal List<byte[]> Bodies { get; } = [];
    internal bool OfficialAndAuthenticated { get; private set; } = true;
    internal static int ToolCount(JsonElement root, ModelProfile profile) => !root.TryGetProperty("tools", out var tools) || tools.ValueKind == JsonValueKind.Null ? 0 :
        profile.Provider == "openai" ? tools.GetArrayLength() : tools[0].GetProperty("functionDeclarations").GetArrayLength();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var bytes = await request.Content!.ReadAsByteArrayAsync(token); Bodies.Add(bytes);
        OfficialAndAuthenticated &= request.RequestUri!.AbsoluteUri == profile.Destination && (profile.Provider == "openai"
            ? request.Headers.Authorization?.Parameter == OpenAiTestData.Key && !request.Headers.Contains("x-goog-api-key")
            : request.Headers.TryGetValues("x-goog-api-key", out var keys) && keys.Single() == OpenAiTestData.Key && request.Headers.Authorization is null);
        using var parsed = JsonDocument.Parse(bytes); var tools = ToolCount(parsed.RootElement, profile);
        var text = tools == 0 ? ContextTestData.Summary : mode == "review" ? GitChatTests.Report("g1") : "answer";
        var call = Bodies.Count == 1 && mode is "list" or "review" or "mcp";
        var name = mode == "review" ? "git_diff" : mode == "mcp" ? McpProfile.DocsName : "list_files";
        var arguments = mode == "review" ? "{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}" : mode == "mcp" ? "{\"query\":\"public C#\"}" : "{\"path\":\".\"}";
        string response;
        if (profile.Provider == "gemini")
            response = call ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"" + name + "\",\"args\":" + arguments + "}}]") : GeminiFixtures.Text(text);
        else
        {
            var output = call ? "[{\"type\":\"function_call\",\"id\":\"native-item\",\"call_id\":\"native-call\",\"name\":\"" + name + "\",\"arguments\":" + JsonSerializer.Serialize(arguments) + "}]" :
                "[{\"type\":\"message\",\"id\":\"native-message\",\"role\":\"assistant\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":" + JsonSerializer.Serialize(text) + ",\"annotations\":[]}]}]";
            response = OpenAiTestData.Response(output);
        }
        return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
    }
}