using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ContextChatTests
{
    [TestMethod]
    public async Task CTXAC001_LocalCommandsDoNotGenerateAndTurnLimitStopsWholeBatch()
    {
        var fixture = new ChatFixture("/context pin   中文  ", "/context show", "/budget set turn 1", "question");
        fixture.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, null, "list_files", GeminiFixtures.Element("{\"path\":\".\"}"))));
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(1, fixture.Client.Requests.Count);
        Assert.IsTrue(fixture.Client.Requests[0].SystemInstruction.Contains("\\u4E2D\\u6587", StringComparison.Ordinal));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.StopReason == AgentStopReason.GenerationLimit));
        Assert.IsTrue(fixture.Interaction.Events.Where(e => e.Agent?.Operation is not null).All(e => e.Agent!.Operation!.Status == Tools.ToolStatus.NotExecuted));
    }

    [TestMethod]
    public async Task CTXAC025_ManualCompactionUsesOneGenerationAndKeepsLastConversationOutcome()
    {
        var fixture = new ChatFixture("one", "two", "three", "/compact", "/context show");
        fixture.Client.Respond = (_, request, _) => Task.FromResult(ChatFixture.Completed(
            request.Tools.Count == 0 ? ContextTestData.Summary : "answer"));
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(4, fixture.Client.Requests.Count);
        var request = fixture.Client.Requests[^1];
        Assert.AreEqual(0, request.Tools.Count); Assert.AreEqual(1, request.Messages.Count);
        Assert.IsTrue(request.Messages[0].UserText!.Contains("one", StringComparison.Ordinal));
        Assert.IsFalse(request.Messages[0].UserText!.Contains("three", StringComparison.Ordinal));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("model_summary", StringComparison.Ordinal) == true));
    }
}

internal static class ContextTestData
{
    internal const string Summary = "{\"version\":1,\"goals\":[\"continue\"],\"decisions\":[],\"open_questions\":[],\"background\":[]}";
}