using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ContextManagementTests
{
    [TestMethod]
    public void CTXAC002_CommandParserIsExactAndPinTextIsNotTrimmed()
    {
        var pin = ContextCommands.Parse("  /context pin   中文  ");
        Assert.AreEqual(ContextCommandKind.Pin, pin.Kind); Assert.AreEqual("  中文  ", pin.Text);
        foreach (var invalid in new[] { "/budget set turn 01", "/budget set turn +1", "/budget set turn 1.0", "/budget set program 1001", "/budget set turn 21", "/budget set turn 1 x", "/Context", "/context  show", "/context unpin n01" })
            Assert.AreEqual(ContextCommandKind.Invalid, ContextCommands.Parse(invalid).Kind);
        Assert.AreEqual(20, ContextCommands.Parse(" /budget set turn 20 ").Number);
    }
    [TestMethod]
    public void CTXAC003_PinsPreserveOriginalTextAndNeverReuseIdentity()
    {
        using var session = GeminiFixtures.Session();
        var context = new ConversationContext(); var plans = new PlanStore(session.Policy);
        var coordinator = new ContextCoordinator(context, plans, session.Policy);
        Assert.AreEqual("none", coordinator.AddPin("  中文 😀  "));
        Assert.AreEqual("  中文 😀  ", coordinator.Capture().Context.Pins[0].Text);
        Assert.AreEqual("n1", coordinator.Capture().Context.Pins[0].PinId);
        var revision = context.Revision;
        foreach (var invalid in new[] { " ", "x\n", "x\u2028", "\ud800", new string('a', 1025) })
            Assert.AreEqual("invalid_context_command", coordinator.AddPin(invalid));
        Assert.AreEqual(revision, context.Revision);
        Assert.AreEqual("pin_not_found", coordinator.RemovePin("n2"));
        Assert.AreEqual("none", coordinator.RemovePin("n1"));
        coordinator.Clear();
        Assert.AreEqual("none", coordinator.AddPin("again"));
        Assert.AreEqual("n2", coordinator.Capture().Context.Pins[0].PinId);
    }

    [TestMethod]
    public void CTXAC014_SummarySchemaRejectsAmbiguousOrLossyCandidates()
    {
        using var session = GeminiFixtures.Session();
        foreach (var text in new[] { "{}", "null", "```json\n{}\n```", "{\"version\":1,\"version\":1}",
            "{\"version\":1.0,\"goals\":[\"x\"],\"decisions\":[],\"open_questions\":[],\"background\":[]}",
            "{\"version\":1,\"goals\":[],\"decisions\":[],\"open_questions\":[],\"background\":[]}" })
            Assert.IsNull(ContextDocuments.ParseSummary(text, 3, 3, session.Policy));
        var valid = ContextDocuments.ParseSummary("{\"background\":[],\"open_questions\":[],\"decisions\":[],\"goals\":[\"  原文  \"],\"version\":1}", 3, 3, session.Policy);
        Assert.IsNotNull(valid); Assert.AreEqual("  原文  ", valid.Goals[0]);
    }

    [TestMethod]
    public void CTXAC026_BudgetResetNeverRefundsAndLimitsAreAtomic()
    {
        var budget = new GenerationBudget(); budget.BeginConversation();
        for (var i = 0; i < 5; i++) Assert.IsTrue(budget.TryBeginGeneration(GenerationPurpose.Conversation, false, default, () => true));
        Assert.IsFalse(budget.TryBeginGeneration(GenerationPurpose.Conversation, false, default, () => true));
        Assert.IsFalse(budget.SetTurnLimit(21)); Assert.IsFalse(budget.SetProgramLimit(0));
        Assert.IsTrue(budget.SetProgramLimit(1)); Assert.AreEqual(0, budget.Snapshot().ProgramRemaining);
        budget.RestoreDefaults(); Assert.AreEqual(5, budget.Snapshot().ProgramGenerationsStarted);
        budget.BeginConversation(); Assert.AreEqual(5, budget.Snapshot().ProgramGenerationsStarted);
        Assert.IsTrue(budget.TryBeginGeneration(GenerationPurpose.Compaction, false, default, () => true));
        Assert.AreEqual(1, budget.Snapshot().CompactionGenerationsStarted);
    }

    [TestMethod]
    public void CTXAC007_SixthRawCommitRejectsWithoutLosingHistory()
    {
        var context = new ConversationContext();
        var first = LlmMessage.FromUserText("first");
        context.CommitCompletedTurn([first, LlmMessage.FromModel(new object())]);
        for (var i = 1; i < 5; i++)
            context.CommitCompletedTurn([LlmMessage.FromUserText("q"), LlmMessage.FromModel(new object())]);
        Assert.ThrowsExactly<InvalidOperationException>(() => context.CommitCompletedTurn(
            [LlmMessage.FromUserText("sixth"), LlmMessage.FromModel(new object())]));
        Assert.AreSame(first, context.BuildMessages(LlmMessage.FromUserText("next"))[0]);
        Assert.AreEqual(5, context.CompletedTurnCount);
    }
}