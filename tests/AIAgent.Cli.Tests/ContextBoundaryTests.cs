using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ContextBoundaryTests
{
    [DataRow(16, 1, true)]
    [DataRow(17, 1, false)]
    [DataRow(1, 1024, true)]
    [DataRow(1, 1025, false)]
    [TestMethod]
    public void CTXAC014_SummaryCollectionsAndUtf16ItemsAreBounded(int count, int length, bool accepted)
    {
        using var session = GeminiFixtures.Session();
        var text = JsonSerializer.Serialize(new { version = 1, goals = Enumerable.Repeat(new string('x', length), count), decisions = Array.Empty<string>(), open_questions = Array.Empty<string>(), background = Array.Empty<string>() });
        Assert.AreEqual(accepted, ContextDocuments.ParseSummary(text, 1, 1, session.Policy) is not null);
    }

    [TestMethod]
    public void CTXAC021_TurnAndPinIdentityOverflowDoesNotPublish()
    {
        using var session = GeminiFixtures.Session(); var context = ThreeTurns(); var coordinator = new ContextCoordinator(context, new(session.Policy), session.Policy);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(ConversationContext).GetField("_turnSequence", flags)!.SetValue(context, long.MaxValue);
        Assert.ThrowsExactly<OverflowException>(() => context.CommitCompletedTurn([LlmMessage.FromUserText("next"), LlmMessage.FromModel(new object())]));
        Assert.AreEqual(3, context.CompletedTurnCount);
        typeof(ConversationContext).GetField("_pinSequence", flags)!.SetValue(context, long.MaxValue);
        Assert.ThrowsExactly<OverflowException>(() => coordinator.AddPin("next")); Assert.AreEqual(0, context.Capture().Pins.Count);
    }
    [DataRow("n1")]
    [DataRow("pins")]
    [DataRow("1")]
    [DataRow("n1\",\"text")]
    [TestMethod]
    public void CTXAC004_ShortKeyIdFieldNumberAndCombinedEnvelopeRejectAtomically(string key)
    {
        using var session = WorkspaceFixtures.Session(key: key); var context = new ConversationContext();
        var coordinator = new ContextCoordinator(context, new(session.Policy), session.Policy);
        Assert.AreEqual("sensitive_content", coordinator.AddPin("safe"));
        Assert.AreEqual(0, context.Capture().Pins.Count); Assert.AreEqual(0L, context.Revision);
    }

    [DataRow(16383, true)]
    [DataRow(16384, true)]
    [DataRow(16385, false)]
    [TestMethod]
    public void CTXAC014_CanonicalExpansionBoundaryDiffersFromRawBytes(int target, bool accepted)
    {
        using var session = GeminiFixtures.Session();
        var values = new[] { new string('中', 1024), new string('中', 1024), "" };
        object Shape() => new { version = 1, goals = values, decisions = Array.Empty<string>(), open_questions = Array.Empty<string>(), background = Array.Empty<string>() };
        var overhead = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Shape()));
        values[2] = new string('中', (target - overhead) / 6) + new string('x', (target - overhead) % 6);
        Assert.AreEqual(target, Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Shape())));
        var raw = JsonSerializer.Serialize(Shape(), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.IsTrue(Encoding.UTF8.GetByteCount(raw) < 16384);
        Assert.AreEqual(accepted, ContextDocuments.ParseSummary(raw, 1, 1, session.Policy) is not null);
    }
    [DataRow(1023, "none")]
    [DataRow(1024, "none")]
    [DataRow(1025, "invalid_context_command")]
    [TestMethod]
    public void CTXAC003_PinUtf16Boundary(int length, string expected)
    {
        using var session = GeminiFixtures.Session(); var context = new ConversationContext();
        var coordinator = new ContextCoordinator(context, new(session.Policy), session.Policy);
        Assert.AreEqual(expected, coordinator.AddPin(new string('x', length)));
        Assert.AreEqual(expected == "none" ? 1 : 0, coordinator.Capture().Context.Pins.Count);
    }

    [DataRow(8191, "none")]
    [DataRow(8192, "none")]
    [DataRow(8193, "pins_limit")]
    [TestMethod]
    public void CTXAC005_PinsEnvelopeBoundaryIsAtomic(int target, string expected)
    {
        using var session = GeminiFixtures.Session(); var context = new ConversationContext();
        var coordinator = new ContextCoordinator(context, new(session.Policy), session.Policy);
        for (var i = 0; i < 7; i++) Assert.AreEqual("none", coordinator.AddPin(new string('x', 1024)));
        var pins = coordinator.Capture().Context.Pins.Append(new ContextPin("n8", "x")).ToArray();
        var overhead = ContextDocuments.Pins(pins).Length - 1;
        var revision = context.Revision;
        Assert.AreEqual(expected, coordinator.AddPin(new string('x', target - overhead)));
        if (expected == "none") Assert.AreEqual(target, ContextDocuments.Pins(coordinator.Capture().Context.Pins).Length);
        else { Assert.AreEqual(revision, context.Revision); Assert.AreEqual(7, coordinator.Capture().Context.Pins.Count); }
    }

    [TestMethod]
    public void CTXAC005_SixteenthPinAndDuplicateTextArePreserved()
    {
        using var session = GeminiFixtures.Session(); var coordinator = new ContextCoordinator(new(), new(session.Policy), session.Policy);
        for (var i = 0; i < 16; i++) Assert.AreEqual("none", coordinator.AddPin("same"));
        var before = coordinator.Capture(); Assert.AreEqual("pins_limit", coordinator.AddPin("next"));
        Assert.AreEqual(before.ContextRevision, coordinator.Capture().ContextRevision);
        CollectionAssert.AreEqual(Enumerable.Range(1, 16).Select(i => "n" + i).ToArray(), coordinator.Capture().Context.Pins.Select(p => p.PinId).ToArray());
    }

    [DataRow(16383, true)]
    [DataRow(16384, true)]
    [DataRow(16385, false)]
    [TestMethod]
    public void CTXAC014_RawSummaryBoundaryIncludesOuterWhitespace(int target, bool accepted)
    {
        using var session = GeminiFixtures.Session();
        var text = ContextTestData.Summary.PadRight(target);
        Assert.AreEqual(accepted, ContextDocuments.ParseSummary(text, 1, 1, session.Policy) is not null);
    }

    [DataRow(32767, true)]
    [DataRow(32768, true)]
    [DataRow(32769, false)]
    [TestMethod]
    public void CTXAC006_MemoryEnvelopeWriterBoundary(int target, bool accepted)
    {
        var summary = new ContextSummary(1, 1, ["x"], [], [], []);
        var baseline = ContextDocuments.Memory(summary, []).Length;
        var candidate = new ContextSummary(1, 1, [new string('x', target - baseline + 1)], [], [], []);
        if (accepted) Assert.AreEqual(target, ContextDocuments.Memory(candidate, []).Length);
        else Assert.ThrowsExactly<ContextCapacityException>(() => ContextDocuments.Memory(candidate, []));
    }

    [DataRow(65535, true)]
    [DataRow(65536, true)]
    [DataRow(65537, false)]
    [TestMethod]
    public void CTXAC006_CliWholeBlockBoundary(int target, bool accepted)
    {
        using var session = GeminiFixtures.Session();
        Assert.AreEqual(accepted, ContextDisplay.Safe(new string('x', target), session.Policy));
    }

    [DataRow(131071, true)]
    [DataRow(131072, true)]
    [DataRow(131073, false)]
    [TestMethod]
    public void CTXAC013_SourceEnvelopeBoundary(int target, bool accepted)
    {
        var baseline = ContextDocuments.Source(null, [new(1, "x", "answer", [])]).Length;
        var projection = new ContextTurnProjection(1, new string('x', target - baseline + 1), "answer", []);
        if (accepted) Assert.AreEqual(target, ContextDocuments.Source(null, [projection]).Length);
        else Assert.ThrowsExactly<ContextCapacityException>(() => ContextDocuments.Source(null, [projection]));
    }

    [DataRow("{\"version\":1,\"goals\":[\"x\"],\"decisions\":[],\"open_questions\":[],\"background\":[],\"extra\":0}")]
    [DataRow("{\"version\":1e0,\"goals\":[\"x\"],\"decisions\":[],\"open_questions\":[],\"background\":[]}")]
    [DataRow("{\"version\":1,\"goals\":[\"x\"],\"decisions\":null,\"open_questions\":[],\"background\":[]}")]
    [DataRow("{\"version\":1,\"goals\":[\"\\uD800\"],\"decisions\":[],\"open_questions\":[],\"background\":[]}")]
    [TestMethod]
    public void CTXAC014_InvalidSummaryNeverProducesCandidate(string text)
    {
        using var session = GeminiFixtures.Session();
        Assert.IsNull(ContextDocuments.ParseSummary(text, 1, 1, session.Policy));
    }

    [TestMethod]
    public void CTXAC021_PinsClearModeAndOverflowInvalidatePublication()
    {
        using var session = GeminiFixtures.Session();
        foreach (var change in new[] { "pin", "clear", "mode" })
        {
            var context = ThreeTurns(); var plans = new PlanStore(session.Policy, context.DecisionGate);
            var coordinator = new ContextCoordinator(context, plans, session.Policy);
            var stamp = coordinator.Capture(); var candidate = coordinator.PrepareCandidate(stamp, ContextTestData.Summary)!;
            if (change == "pin") coordinator.AddPin("new");
            else if (change == "clear") coordinator.Clear(); else plans.SetMode(AgentMode.Plan);
            Assert.IsFalse(coordinator.TryPublishCompaction(candidate, default));
            Assert.IsNull(context.Summary);
        }
        var overflow = ThreeTurns(); var coordinator2 = new ContextCoordinator(overflow, new(session.Policy), session.Policy);
        typeof(ConversationContext).GetField("<Revision>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(overflow, long.MaxValue);
        Assert.ThrowsExactly<OverflowException>(() => coordinator2.AddPin("new"));
        Assert.AreEqual(0, overflow.Capture().Pins.Count);
    }

    [TestMethod]
    public void CTXAC007_RepeatedCompactionKeepsOneSummaryAndOriginalRetainedMessages()
    {
        using var session = GeminiFixtures.Session(); var context = ThreeTurns();
        var coordinator = new ContextCoordinator(context, new(session.Policy), session.Policy);
        var retained = context.Capture().CompletedTurns[1].Messages[1];
        var candidate = coordinator.PrepareCandidate(coordinator.Capture(), ContextTestData.Summary)!;
        Assert.IsTrue(coordinator.TryPublishCompaction(candidate, default));
        Assert.AreEqual(1L, context.Summary!.CoveredTurnCount); Assert.AreEqual(2, context.CompletedTurnCount);
        Assert.AreSame(retained, context.BuildMessages(LlmMessage.FromUserText("q"))[1]);
        context.CommitCompletedTurn([LlmMessage.FromUserText("again"), LlmMessage.FromModel(new object())]);
        candidate = coordinator.PrepareCandidate(coordinator.Capture(), ContextTestData.Summary)!;
        Assert.IsTrue(coordinator.TryPublishCompaction(candidate, default));
        Assert.AreEqual(2L, context.Summary!.CoveredTurnCount); Assert.AreEqual(2L, context.Summary.ThroughTurnId);
    }
    internal static ConversationContext ThreeTurns()
    {
        var context = new ConversationContext();
        for (var i = 0; i < 3; i++) context.CommitCompletedTurn([LlmMessage.FromUserText("question " + i), LlmMessage.FromModel(new object())]);
        return context;
    }

    [TestMethod]
    public void CTXAC010_SamePlanRevisionTaskMutationRejectsOldSummary()
    {
        using var f = new PlanFixture(); f.Submit(); f.Approve(); f.Update("in_progress");
        var context = ThreeTurns(); var coordinator = new ContextCoordinator(context, f.Store, f.Session.Policy);
        var stamp = coordinator.Capture(); var candidate = coordinator.PrepareCandidate(stamp, ContextTestData.Summary)!;
        f.Store.BlockInProgress("turn_paused");
        Assert.AreEqual(stamp.PlanRevision, f.Store.Snapshot!.Revision);
        Assert.IsFalse(coordinator.TryPublishCompaction(candidate, default)); Assert.IsNull(context.Summary);
        Assert.AreEqual(PlanTaskState.Blocked, f.Task.State);
    }

    [TestMethod]
    public void CTXAC026_ThousandStartsCannotBeRefundedAndWarningIsPerRevision()
    {
        var budget = new GenerationBudget(); budget.SetProgramLimit(1000); budget.SetTurnLimit(20);
        for (var i = 0; i < 1000; i++)
        {
            if (i % 20 == 0) budget.BeginConversation();
            Assert.IsTrue(budget.TryBeginGeneration(GenerationPurpose.Conversation, false, default, () => true));
            if (i == 798) Assert.IsFalse(budget.TakeNearLimitWarning());
            if (i == 799) { Assert.IsTrue(budget.TakeNearLimitWarning()); Assert.IsFalse(budget.TakeNearLimitWarning()); }
        }
        budget.BeginConversation(); budget.RestoreDefaults(); budget.SetProgramLimit(1000);
        Assert.IsFalse(budget.TryBeginGeneration(GenerationPurpose.Compaction, true, default, () => true));
        Assert.AreEqual(1000, budget.Snapshot().ProgramGenerationsStarted); Assert.AreEqual(0, budget.Snapshot().ProgramRemaining);
    }
}