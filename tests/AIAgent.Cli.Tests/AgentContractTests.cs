using System.Reflection;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class AgentContractTests
{
    [TestMethod]
    public void LOOPAC001_InternalAgentContractsAndCallProjectionExist()
    {
        var assembly = typeof(LlmMessage).Assembly;
        foreach (var name in new[] { "AgentTurnResult", "LocalToolOutcome", "AgentEvent" })
        {
            var type = assembly.GetType("AIAgent.Cli.Core." + name);
            Assert.IsTrue(type is { IsSealed: true, IsPublic: false }, "LOOP-AC-001");
        }
        Assert.IsNotNull(typeof(LlmMessage).GetProperty("ModelCalls", BindingFlags.Instance | BindingFlags.NonPublic), "LOOP-AC-001");
    }
    [TestMethod]
    public void LOOPAC019_CompleteToolTurnsKeepPairingAndWholeTurnEviction()
    {
        var call = new LlmToolCall(2, null, "read_file", GeminiFixtures.Element("{\"path\":\"a\"}"));
        var calls = new List<LlmToolCall> { call };
        var model = LlmMessage.FromModel(new object(), calls);
        calls.Clear();
        Assert.AreEqual(1, model.ModelCalls.Count, "LOOP-AC-002");
        var response = new LlmToolResult(2, null, "read_file", GeminiFixtures.Element("{}"));
        var turn = new List<LlmMessage> { LlmMessage.FromUserText("q"), model,
            LlmMessage.FromToolResults([response]), LlmMessage.FromModel(new object()) };
        var context = new ConversationContext();
        for (var i = 0; i < 5; i++) context.CommitCompletedTurn(turn);
        Assert.ThrowsExactly<InvalidOperationException>(() => context.CommitCompletedTurn(turn));
        turn.Clear();
        Assert.AreEqual(5, context.CompletedTurnCount, "LOOP-AC-019");
        Assert.AreEqual(21, context.BuildMessages(LlmMessage.FromUserText("next")).Count, "LOOP-AC-019");
    }

    [TestMethod]
    public void LOOPAC010_ContextRejectsMissingReorderedAndWrongResults()
    {
        var a = new LlmToolCall(0, "a", "read_file", GeminiFixtures.Element("{}"));
        var b = new LlmToolCall(1, null, "read_file", GeminiFixtures.Element("{}"));
        var first = new LlmToolResult(0, "a", "read_file", GeminiFixtures.Element("{}"));
        var second = new LlmToolResult(1, null, "read_file", GeminiFixtures.Element("{}"));
        foreach (var results in new IReadOnlyList<LlmToolResult>[] { [], [first], [second, first], [first, first] })
            Assert.Throws<ArgumentException>(() => new ConversationContext().CommitCompletedTurn([
                LlmMessage.FromUserText("q"), LlmMessage.FromModel(new object(), [a, b]),
                LlmMessage.FromToolResults(results), LlmMessage.FromModel(new object())]), "LOOP-AC-010");
    }
}