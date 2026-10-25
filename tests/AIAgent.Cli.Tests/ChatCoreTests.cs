using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ChatCoreTests
{
    [TestMethod]
    public void CLIAC001_SessionTransferIsSingleAndOuterDisposalDoesNotDisposeSession()
    {
        using var result = BootstrapResult.Ready(GeminiFixtures.Session());
        using var session = result.TakeSession();
        result.Dispose();
        Assert.IsNull(result.Session, "CLI-AC-001");
        Assert.IsTrue(session.Policy.ContainsCurrentKey(GeminiFixtures.Key), "CLI-AC-001");
        Assert.ThrowsExactly<InvalidOperationException>(() => result.TakeSession());
        using var failed = BootstrapResult.Failed(BootstrapErrorCode.IoFailure);
        Assert.ThrowsExactly<InvalidOperationException>(() => failed.TakeSession());
    }

    [TestMethod]
    public void CLIAC001_NewContentContractsAreInternalAndCannotDumpContent()
    {
        var input = ChatInputResult.Submitted("private content");
        var message = new ChatEvent(ChatEventKind.Answer, text: "private content");
        Assert.IsFalse(typeof(ChatInputResult).IsPublic || typeof(IChatInteraction).IsPublic, "CLI-AC-001");
        Assert.AreEqual("[ChatInputResult]", input.ToString());
        Assert.AreEqual("[ChatEvent]", message.ToString());
        Assert.IsFalse(JsonSerializer.Serialize(input).Contains("private content", StringComparison.Ordinal), "CLI-AC-001");
        Assert.IsFalse(JsonSerializer.Serialize(message).Contains("private content", StringComparison.Ordinal), "CLI-AC-001");
        Assert.IsNull(ChatInputResult.End(ChatInputStatus.Invalid).Text, "CLI-AC-001");
    }

    [TestMethod]
    public void CLIAC007_SixthTurnKeepsFivePriorTurnsBeforeCommit()
    {
        var context = new ConversationContext();
        var users = Enumerable.Range(0, 7).Select(i => LlmMessage.FromUserText("user " + i)).ToArray();
        var models = Enumerable.Range(0, 7).Select(_ => LlmMessage.FromModel(new object())).ToArray();
        for (var i = 0; i < 5; i++)
        {
            var messages = context.BuildMessages(users[i]);
            Assert.AreEqual(Math.Min(i, 5) * 2 + 1, messages.Count, "CLI-AC-007");
            Assert.AreSame(users[Math.Max(0, i - 5)], messages[0], "CLI-AC-007");
            if (i > 0) Assert.AreSame(models[i - 1], messages[^2], "CLI-AC-007");
            Assert.AreSame(users[i], messages[^1], "CLI-AC-007");
            context.CommitCompletedTurn([users[i], models[i]]);
            Assert.AreEqual(Math.Min(i + 1, 5), context.CompletedTurnCount, "CLI-AC-007");
        }
        Assert.AreEqual(11, context.BuildMessages(users[5]).Count);
        Assert.ThrowsExactly<InvalidOperationException>(() => context.CommitCompletedTurn([users[5], models[5]]));
        context.Clear(); context.Clear();
        Assert.AreEqual(0, context.CompletedTurnCount);
        Assert.AreEqual(1, context.BuildMessages(users[0]).Count);
    }

    [TestMethod]
    public void CLIAC008_ContextSnapshotsAndStrictTwoMessageTurn()
    {
        var context = new ConversationContext();
        var user = LlmMessage.FromUserText("user");
        var model = LlmMessage.FromModel(new object());
        var source = new List<LlmMessage> { user, model };
        context.CommitCompletedTurn(source); source.Clear();
        var snapshot = context.BuildMessages(user);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<LlmMessage>)snapshot)[0] = model);
        context.Clear();
        Assert.AreEqual(3, snapshot.Count, "CLI-AC-008");
        Assert.AreSame(model, snapshot[1]);
        foreach (var invalid in new LlmMessage[][] { [], [user], [model, user], [user, user], [user, LlmMessage.FromToolResults([])], [user, model, user] })
            Assert.ThrowsExactly<ArgumentException>(() => context.CommitCompletedTurn(invalid));
        Assert.ThrowsExactly<ArgumentException>(() => context.BuildMessages(model));
    }

    [TestMethod]
    public void CLIAC024_UsageKeepsMissingZeroNegativeAndIndependentTotals()
    {
        var usage = new UsageAccumulator();
        Assert.IsFalse(usage.Snapshot().Total.HasKnownValue);
        usage.Record(Result(new(10, 2, 3, 4, 17)));
        usage.Record(Result(new(0, null, -1, 0, null)));
        usage.Record(Result(null));
        usage.Record(Result(new(90, 90, 90, 90, 90), false));
        var sum = usage.Snapshot();
        Assert.AreEqual(3L, sum.DispatchedRequests);
        Assert.AreEqual(new UsageFieldSnapshot(10, true, 1, false), sum.Prompt);
        Assert.AreEqual(new UsageFieldSnapshot(2, true, 2, false), sum.Output);
        Assert.AreEqual(new UsageFieldSnapshot(3, true, 2, false), sum.Thought);
        Assert.AreEqual(new UsageFieldSnapshot(4, true, 1, false), sum.Cached);
        Assert.AreEqual(new UsageFieldSnapshot(17, true, 2, false), sum.Total);
    }

    [TestMethod]
    public void CLIAC025_AllReturnedStatusesRecordOnlyDispatchedResults()
    {
        var usage = new UsageAccumulator();
        foreach (var status in Enum.GetValues<LlmStatus>())
        {
            usage.Record(new(status, LlmErrorCode.None, "", [], null, new(0, 0, 0, 0, 0), true, false));
            usage.Record(new(status, LlmErrorCode.None, "", [], null, null, false, false));
        }
        Assert.AreEqual((long)Enum.GetValues<LlmStatus>().Length, usage.Snapshot().DispatchedRequests);
        Assert.AreEqual(new UsageFieldSnapshot(0, true, 0, false), usage.Snapshot().Total);
    }

    [TestMethod]
    public void CLIAC026_OverflowKeepsLastRepresentableSubtotalAndOtherFieldsContinue()
    {
        var usage = new UsageAccumulator();
        usage.Record(Result(new(3000000000, 0, null, null, long.MaxValue)));
        usage.Record(Result(new(2, 0, null, null, 1)));
        usage.Record(Result(new(3, 0, null, null, 0)));
        var sum = usage.Snapshot();
        Assert.AreEqual(3000000005L, sum.Prompt.KnownTotal);
        Assert.AreEqual(long.MaxValue, sum.Total.KnownTotal);
        Assert.IsTrue(sum.Total.Overflowed);
        Assert.AreEqual(3L, sum.DispatchedRequests);
    }

    [TestMethod]
    public void CLIAC026_CounterOverflowDoesNotPreventKnownValueUpdates()
    {
        var usage = new UsageAccumulator();
        // Reach otherwise impractical counter boundaries without a product test shortcut.
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(UsageAccumulator).GetField("_dispatchedRequests", flags)!.SetValue(usage, long.MaxValue);
        var field = typeof(UsageAccumulator).GetField("_total", flags)!.GetValue(usage)!;
        field.GetType().GetField("Missing", flags | System.Reflection.BindingFlags.Public)!.SetValue(field, long.MaxValue);
        usage.Record(Result(null));
        usage.Record(Result(new(null, null, null, null, 7)));
        var sum = usage.Snapshot();
        Assert.AreEqual(long.MaxValue, sum.DispatchedRequests);
        Assert.IsTrue(sum.CounterOverflowed && sum.Total.Overflowed);
        Assert.AreEqual(7L, sum.Total.KnownTotal);
        Assert.AreEqual(long.MaxValue, sum.Total.MissingRequestCount);
    }

    private static LlmResult Result(LlmUsage? usage, bool dispatched = true) => new(LlmStatus.Failed, LlmErrorCode.NetworkFailure, "", [], null, usage, dispatched, false);
}