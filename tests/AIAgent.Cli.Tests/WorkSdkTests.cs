using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkSdkTests
{
    [TestMethod]
    public async Task SAVEAC011_RealSdkSaveProjectsLatestTurnsAndSendsOneNativeFreeRequest()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        handler.Respond = async (request, token) =>
        {
            if (handler.Calls <= 2) return GeminiHandler.Json(GeminiFixtures.Response("[{\"thought\":true,\"text\":\"PRIVATE_REASONING_ONLY\"},{\"text\":\"visible\",\"thoughtSignature\":\"c2ln\"}]"));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token));
            Assert.IsFalse(body.RootElement.TryGetProperty("tools", out _));
            var contents = body.RootElement.GetProperty("contents"); Assert.AreEqual(1, contents.GetArrayLength());
            var source = contents[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
            Assert.IsFalse(source.Contains("PRIVATE_REASONING_ONLY", StringComparison.Ordinal)); Assert.IsFalse(source.Contains("c2ln", StringComparison.Ordinal));
            using var projected = JsonDocument.Parse(source); Assert.AreEqual(2, projected.RootElement.GetProperty("turns").GetArrayLength());
            return GeminiHandler.Json(GeminiFixtures.Text(ContextTestData.Summary));
        };
        await using var client = new GeminiClient(session, handler); var context = new ConversationContext();
        for (var i = 0; i < 2; i++)
        {
            var user = LlmMessage.FromUserText("latest " + i); var answer = await client.GenerateAsync(new("test", [user], []), default);
            context.CommitCompletedTurn([user, answer.ModelMessage!]);
        }
        var plans = new PlanStore(session.Policy, context.DecisionGate); var coordinator = new ContextCoordinator(context, plans, session.Policy);
        var inspector = client.CreateContextInspector(); var budget = new GenerationBudget(context.DecisionGate); var usage = new UsageAccumulator();
        var runner = new AgentRunner(client, TestRulesRegistry.Create([], session.Policy, new(plans)), session.Policy, "test", usage, _ => { }, budget, coordinator, inspector);
        var stamp = coordinator.Capture(); var result = await runner.RunWorkSaveSummaryAsync(stamp, coordinator.PrepareWorkSource(stamp, inspector), default);
        Assert.AreEqual(AgentTurnStatus.Completed, result.Status); Assert.AreEqual(3, handler.Calls); Assert.AreEqual(1, result.GenerationsStarted);
        Assert.AreEqual(1L, usage.Snapshot().DispatchedRequests); Assert.AreEqual(2, context.CompletedTurnCount); Assert.IsNull(context.Summary);
    }
}