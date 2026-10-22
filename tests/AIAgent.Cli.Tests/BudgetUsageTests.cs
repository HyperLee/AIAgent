using System.Reflection;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BudgetUsageTests
{
    [TestMethod]
    public async Task BUDAC003_ThreeGenerationsTwoDispatchesKeepIndependentSubtotals()
    {
        await using var f = new BudgetFixture();
        await File.WriteAllTextAsync(Path.Combine(f.Root, "a"), "safe");
        var client = new ScriptClient { Session = f.Session };
        client.Respond = (index, _, _) =>
        {
            var call = index == 0 ? BudgetFixture.Call("list_files", new { path = "." }) : BudgetFixture.Call("read_file", new { path = "a" });
            return Task.FromResult(index < 2 ? new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None, "", [call],
                LlmMessage.FromModel(new object(), [call]), index == 0 ? new(2, 3, 4, 5, 10) : new(null, 0, -1, 2, null), true, false)
                : new LlmResult(LlmStatus.Failed, LlmErrorCode.InputTooLarge, "", [], null, new(99, 99, 99, 99, 99), false, false));
        };
        var result = await f.RunWith(client);
        Assert.IsTrue(result.GenerationsStarted == 3 && result.TurnUsage.DispatchedRequests == 2 && client.Requests.Count == 3, "BUD-AC-003-counts");
        Assert.AreEqual(new UsageFieldSnapshot(10, true, 1, false), result.TurnUsage.Total, "BUD-AC-003-total");
        Assert.AreEqual(new UsageFieldSnapshot(2, true, 1, false), result.TurnUsage.Prompt, "BUD-AC-003-prompt");
        Assert.AreEqual(new UsageFieldSnapshot(3, true, 0, false), result.TurnUsage.Output, "BUD-AC-003-output");
        Assert.AreEqual(new UsageFieldSnapshot(4, true, 1, false), result.TurnUsage.Thought, "BUD-AC-003-thought");
        Assert.AreEqual(new UsageFieldSnapshot(7, true, 0, false), result.TurnUsage.Cached, "BUD-AC-003-cached");
        Assert.AreEqual(result.TurnUsage, f.Usage.Snapshot(), "BUD-AC-003-once-per-accumulator");
        var display = ChatDisplay.Format(new(ChatEventKind.Usage, sessionUsage: f.Usage.Snapshot(), generationsStarted: 3, turnSummary: result.TurnUsage));
        Assert.IsTrue(display.Contains("已知小計 10／不完整（1 次缺值）", StringComparison.Ordinal), "BUD-AC-003-display");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task BUDAC004_ZeroMissingNegativeAndUndispatchedUsageRemainDistinct()
    {
        foreach (var (value, dispatched, known, missing) in new (long?, bool, bool, long)[]
        { (0, true, true, 0), (null, true, false, 1), (-1, true, false, 1), (99, false, false, 0) })
        {
            await using var f = new BudgetFixture();
            var client = new ScriptClient
            {
                Session = f.Session,
                Respond = (_, _, _) => Task.FromResult(new LlmResult(
                LlmStatus.Empty, LlmErrorCode.None, "", [], null, value is null ? null : new(value, value, value, value, value), dispatched, false))
            };
            var result = await f.RunWith(client);
            foreach (var field in new[] { result.TurnUsage.Prompt, result.TurnUsage.Output, result.TurnUsage.Thought, result.TurnUsage.Cached, result.TurnUsage.Total })
                Assert.IsTrue(field.HasKnownValue == known && field.MissingRequestCount == missing && field.KnownTotal == 0, "BUD-AC-004-fields");
            Assert.IsTrue(result.GenerationsStarted == 1 && result.TurnUsage.DispatchedRequests == (dispatched ? 1 : 0), "BUD-AC-004-counts");
            var text = ChatDisplay.Format(new(ChatEventKind.Usage, sessionUsage: f.Usage.Snapshot(), generationsStarted: 1, turnSummary: result.TurnUsage));
            Assert.IsTrue(text.Contains(known ? "總計 0" : "總計 不可得", StringComparison.Ordinal), "BUD-AC-004-no-false-zero");
            await client.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task BUDAC005_OverflowAndNonAdditiveApiFieldsDoNotCorruptNextTurn()
    {
        await using var f = new BudgetFixture();
        typeof(UsageAccumulator).GetField("_dispatchedRequests", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Usage, long.MaxValue);
        var call = BudgetFixture.Call("list_files", new { path = "." });
        var client = new ScriptClient { Session = f.Session };
        client.Respond = (index, _, _) => Task.FromResult(index == 0
            ? new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None, "", [call], LlmMessage.FromModel(new object(), [call]), new(3000000000, 0, 4, 7, long.MaxValue), true, false)
            : new LlmResult(LlmStatus.Completed, LlmErrorCode.None, "answer", [], LlmMessage.FromModel(new object()), new(1, 2, 3, 4, 1), true, false));
        var result = await f.RunWith(client);
        Assert.IsTrue(result.TurnUsage.Total.Overflowed && result.TurnUsage.Total.KnownTotal == long.MaxValue &&
            result.TurnUsage.Prompt.KnownTotal == 3000000001 && result.TurnUsage.Output.KnownTotal == 2 &&
            result.TurnUsage.Thought.KnownTotal == 7 && result.TurnUsage.Cached.KnownTotal == 11, "BUD-AC-005-independent");
        var next = await f.RunWith(client);
        Assert.IsTrue(!next.TurnUsage.Total.Overflowed && next.TurnUsage.Total.KnownTotal == 1 && f.Usage.Snapshot().CounterOverflowed &&
            f.Usage.Snapshot().Total.Overflowed && f.Usage.Snapshot().DispatchedRequests == long.MaxValue, "BUD-AC-005-lifecycle");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task BUDAC006_RealSdkClearSensitiveResetAndRekeyKeepProgramUsage()
    {
        var root = WorkspaceFixtures.NewDirectory();
        var interaction = new EditChatInteraction();
        foreach (var input in new[] { "one", "/clear", "sensitive", "auth", "new", "/exit" })
            interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted(input));
        interaction.Inner.Choices.Enqueue(KeyReplacementChoice.Reenter);
        interaction.Inner.Keys.Enqueue(() => KeyInputResult.Accept("FAKE_REPLACEMENT_ONLY"));
        var old = new GeminiHandler();
        old.Respond = (_, _) => Task.FromResult(old.Calls switch
        {
            1 => GeminiHandler.Json(GeminiFixtures.Text(usage: "{\"totalTokenCount\":9223372036854775807}")),
            2 => GeminiHandler.Json(GeminiFixtures.Text(GeminiFixtures.Key, usage: "{\"totalTokenCount\":17}")),
            _ => GeminiHandler.Json("{}", System.Net.HttpStatusCode.Unauthorized)
        });
        var replacement = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Text(usage: "{\"totalTokenCount\":0}"))) };
        var usage = new UsageAccumulator(); var factory = new ObservedWorkspaceFactory();
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(old, replacement), new(), usage, "test instruction", factory)
            .RunAsync(WorkspaceFixtures.Session(root), default), "BUD-AC-006-exit");
        Assert.IsTrue(old.Calls == 3 && replacement.Calls == 1 && usage.Snapshot().DispatchedRequests == 4 &&
            usage.Snapshot().Total.Overflowed && usage.Snapshot().Total.KnownTotal == long.MaxValue && usage.Snapshot().Total.MissingRequestCount == 1,
            "BUD-AC-006-program");
        var summaries = interaction.Inner.Events.Where(e => e.Kind == ChatEventKind.Usage).Select(e => e.TurnSummary!).ToArray();
        Assert.IsTrue(summaries.Length == 4 && summaries[1].Total.KnownTotal == 17 && summaries[3].Total.HasKnownValue &&
            summaries[3].Total.KnownTotal == 0 && !summaries[3].Total.Overflowed && factory.Services.All(service => service.Snapshots.Count == 0), "BUD-AC-006-fresh-turns");
    }

    [TestMethod]
    public async Task BUDAC007_AllReturnedOutcomesKeepUsageWithoutRetry()
    {
        foreach (var (status, error) in new[] { (LlmStatus.Empty, LlmErrorCode.None), (LlmStatus.Refused, LlmErrorCode.None),
            (LlmStatus.Truncated, LlmErrorCode.None), (LlmStatus.Failed, LlmErrorCode.SensitiveOutput), (LlmStatus.Failed, LlmErrorCode.Timeout),
            (LlmStatus.Cancelled, LlmErrorCode.None), (LlmStatus.Failed, LlmErrorCode.NetworkFailure) })
        {
            await using var f = new BudgetFixture();
            var client = new ScriptClient
            {
                Session = f.Session,
                Respond = (_, _, _) => Task.FromResult(new LlmResult(
                status, error, "", [], null, new(0, null, -1, 4, 17), true, error == LlmErrorCode.SensitiveOutput))
            };
            var result = await f.RunWith(client);
            Assert.IsTrue(client.Requests.Count == 1 && result.GenerationsStarted == 1 && result.CompletedTurn.Count == 0 &&
                result.TurnUsage.Total.KnownTotal == 17 && result.TurnUsage.Output.MissingRequestCount == 1 &&
                result.TurnUsage.Thought.MissingRequestCount == 1 && f.Usage.Snapshot().Total.KnownTotal == 17, "BUD-AC-007");
            await client.DisposeAsync();
        }
    }
}