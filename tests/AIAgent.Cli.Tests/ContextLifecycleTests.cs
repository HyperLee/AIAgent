using System.Net;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ContextLifecycleTests
{
    [TestMethod]
    public async Task CTXAC029_ClearAndKeyReplacementKeepProgramConsumption()
    {
        foreach (var replace in new[] { false, true })
        {
            var f = new ChatFixture("/budget set program 1", "first", "/clear", "second", "/budget");
            if (replace)
            {
                f.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Authentication());
                f.Interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
                f.Interaction.Keys.Enqueue(() => KeyInputResult.Accept("REPLACEMENT_SYNTHETIC_ONLY"));
            }
            Assert.AreEqual(0, await f.Run()); Assert.AreEqual(1, f.Client.Requests.Count);
            Assert.AreEqual(0, f.Factory.ReplacementClient.Requests.Count);
            Assert.IsTrue(f.Interaction.Events.Any(e => e.StopReason == AgentStopReason.ProgramGenerationLimit));
            Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("程序已消耗：1", StringComparison.Ordinal) == true));
        }
    }

    [TestMethod]
    public async Task CTXAC025_EmptyCompactDoesNotGenerateOrReplaceLastOutcome()
    {
        var f = new ChatFixture("question", "/compact", "/budget");
        Assert.AreEqual(0, await f.Run()); Assert.AreEqual(1, f.Client.Requests.Count);
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("nothing_to_compact", StringComparison.Ordinal) == true));
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("最近一般回合生成：1", StringComparison.Ordinal) == true && e.Text.Contains("最近維護生成：0", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CTXAC029_ManualMaintenancePreservesLastOutcomeAndOwnUsage()
    {
        var f = new ChatFixture("one", "two", "three", "/compact", "/budget");
        f.Client.Respond = (_, request, _) => Task.FromResult(ChatFixture.Completed(request.Tools.Count == 0 ? ContextTestData.Summary : "answer"));
        Assert.AreEqual(0, await f.Run());
        var usages = f.Interaction.Events.Where(e => e.Kind == ChatEventKind.Usage).ToArray();
        Assert.AreEqual(4, usages.Length); Assert.AreEqual(GenerationPurpose.Compaction, usages[^1].Purpose);
        Assert.AreEqual(1L, usages[^1].TurnSummary!.DispatchedRequests); Assert.AreEqual(4L, usages[^1].SessionUsage!.DispatchedRequests);
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("最近一般回合生成：1", StringComparison.Ordinal) == true && e.Text.Contains("最近維護生成：1", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CTXAC030_VerifiedWriteRemainsWhenNextGenerationUsesLastOpportunity()
    {
        var root = WorkspaceFixtures.NewDirectory(); var interaction = new EditChatInteraction();
        foreach (var input in new[] { "/budget set turn 2", "write", "/budget reset", "/budget" })
            interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(input));
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(BudgetFixture.Calls("create_file", new
        { path = handler.Calls == 1 ? "a" : "b", content = "A" })));
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "test", new TestWorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default));
        var operations = interaction.Inner.Events.Where(e => e.Agent?.Operation is not null).Select(e => e.Agent!.Operation!).ToArray();
        Assert.AreEqual(2, handler.Calls); Assert.AreEqual(1, interaction.Approvals);
        Assert.AreEqual(FileWriteState.Verified, operations[0].WriteState); Assert.AreEqual(ToolStatus.NotExecuted, operations[1].Status);
        Assert.AreEqual("A", await File.ReadAllTextAsync(Path.Combine(root, "a"))); Assert.IsFalse(File.Exists(Path.Combine(root, "b")));
    }

    [TestMethod]
    public async Task CTXAC023_AuthenticationDuringCompactDoesNotResendPendingInput()
    {
        var first = new GeminiHandler(); var second = new GeminiHandler(); var normal = 0;
        first.Respond = async (request, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token));
            if (!body.RootElement.TryGetProperty("tools", out _)) return GeminiHandler.Json("{}", HttpStatusCode.Unauthorized);
            normal++; return GeminiHandler.Json(GeminiFixtures.Text());
        };
        var interaction = new ScriptInteraction();
        foreach (var input in new[] { "one", "two", "three", "/compact", "/budget" }) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(input));
        interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
        interaction.Keys.Enqueue(() => KeyInputResult.Accept("REPLACEMENT_SYNTHETIC_ONLY"));
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(first, second), new(), new(), "test", new TestWorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(WorkspaceFixtures.NewDirectory()), default));
        Assert.AreEqual(3, normal); Assert.AreEqual(4, first.Calls); Assert.AreEqual(0, second.Calls);
        Assert.IsTrue(interaction.Events.Any(e => e.Text?.Contains("程序已消耗：4", StringComparison.Ordinal) == true));
    }
}