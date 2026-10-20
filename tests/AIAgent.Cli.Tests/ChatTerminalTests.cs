using System.Collections.Concurrent;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ChatTerminalTests
{
    [TestMethod]
    public async Task CLIAC003_SubmittedInputIsHiddenUntilApplicationAcceptsIt()
    {
        var terminal = new FakeTerminal(); terminal.Text(" 安全中文😀 "); terminal.Enter();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal);
        interaction.SetPolicy(session.Policy);
        var input = await interaction.ReadInputAsync(default);
        Assert.AreEqual(" 安全中文😀 ", input.Text);
        Assert.IsFalse(terminal.Output.Contains("安全", StringComparison.Ordinal) || terminal.Output.Contains('*'), "CLI-AC-003");
        Assert.IsTrue(terminal.Output.Contains("你> ", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CLIAC004_InputBoundaryAndInvalidLinesNeverRecoverThroughDeletion()
    {
        foreach (var (text, status) in new[] {
            (new string('a', 16384), ChatInputStatus.Submitted),
            (new string('a', 16385), ChatInputStatus.Invalid),
            (string.Concat(Enumerable.Repeat("😀", 8192)), ChatInputStatus.Submitted),
            ("a\t", ChatInputStatus.Invalid), ("a\u2028", ChatInputStatus.Invalid),
            ("a\u2029", ChatInputStatus.Invalid), ("a\u001b", ChatInputStatus.Invalid),
            ("a\ud800x", ChatInputStatus.Invalid), ("a\udc00", ChatInputStatus.Invalid),
            ("a\ud800", ChatInputStatus.Invalid) })
        {
            var terminal = new FakeTerminal(); terminal.Text(text); terminal.Enter();
            using var session = GeminiFixtures.Session();
            using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var result = await interaction.ReadInputAsync(default);
            Assert.AreEqual(status, result.Status, "CLI-AC-004");
            if (status == ChatInputStatus.Invalid) Assert.IsNull(result.Text);
        }
        var retry = new FakeTerminal(); retry.Text(new string('a', 16385)); retry.Add(KeyInputTests.Key(ConsoleKey.Backspace)); retry.Enter(); retry.Text("next"); retry.Enter();
        using var retrySession = GeminiFixtures.Session();
        using var reader = new ConsoleChatInteraction(retry); reader.SetPolicy(retrySession.Policy);
        Assert.AreEqual(ChatInputStatus.Invalid, (await reader.ReadInputAsync(default)).Status);
        Assert.AreEqual("next", (await reader.ReadInputAsync(default)).Text);
    }

    [TestMethod]
    public async Task CLIAC005_UnicodeScalarDeletionNavigationAndCrLf()
    {
        var terminal = new FakeTerminal();
        terminal.Add(KeyInputTests.Key(ConsoleKey.Delete)); terminal.Text(" 文😀");
        terminal.Add(KeyInputTests.Key(ConsoleKey.Backspace), KeyInputTests.Key(ConsoleKey.LeftArrow));
        terminal.Text("字"); terminal.Enter(); terminal.Text("\nnext"); terminal.Enter();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.AreEqual(" 文字", (await interaction.ReadInputAsync(default)).Text);
        Assert.AreEqual("next", (await interaction.ReadInputAsync(default)).Text);
    }

    [TestMethod]
    public async Task CLIAC005_PartialInputTerminationNeverReturnsText()
    {
        foreach (var control in new[] { '\u0003', '\u0004', '\0' })
        {
            var terminal = new FakeTerminal(); terminal.Text("partial");
            if (control != '\0') terminal.Text(control.ToString());
            using var session = GeminiFixtures.Session();
            using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var result = await interaction.ReadInputAsync(default);
            Assert.AreEqual(control == '\u0003' ? ChatInputStatus.Cancelled : ChatInputStatus.EndOfInput, result.Status);
            Assert.IsNull(result.Text);
            Assert.IsFalse(terminal.Output.Contains("partial", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task CLIAC019_KeyChoiceIsHiddenAndInvalidChoicesOnlyReprompt()
    {
        var terminal = new FakeTerminal(); terminal.Text("invalid"); terminal.Enter(); terminal.Text(" YeS "); terminal.Enter();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.AreEqual(KeyReplacementChoice.Reenter, await interaction.ChooseKeyReplacementAsync(default));
        Assert.IsFalse(terminal.Output.Contains("invalid", StringComparison.Ordinal) || terminal.Output.Contains("YeS", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("重新輸入 Key？[y/N]", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CLIAC019_InvalidChoiceLineIsRetriedRatherThanIoFailure()
    {
        var terminal = new FakeTerminal(); terminal.Text("bad\tchoice"); terminal.Enter(); terminal.Text("y"); terminal.Enter();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.AreEqual(KeyReplacementChoice.Reenter, await interaction.ChooseKeyReplacementAsync(default), "CLI-AC-019");
    }

    [TestMethod]
    public async Task CLIAC020_ReentryCrLfDoesNotBecomeAnExtraBlankKeyAttempt()
    {
        var terminal = new FakeTerminal(); terminal.Text("yes\r\n" + GeminiFixtures.Key + "\r\nnext\r");
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.AreEqual(KeyReplacementChoice.Reenter, await interaction.ChooseKeyReplacementAsync(default));
        using var input = await interaction.ReadKeyAsync(default);
        Assert.AreEqual(KeyInputStatus.Accepted, input.Status);
        Assert.IsFalse(terminal.Output.Contains("輸入無效", StringComparison.Ordinal), "CLI-AC-020");
        Assert.AreEqual("next", (await interaction.ReadInputAsync(default)).Text);
    }

    [TestMethod]
    public async Task CLIAC020_ReenteredKeyKeepsAsciiAndLengthRulesUnderOldPolicy()
    {
        var terminal = new FakeTerminal(); terminal.Text(new string('x', 513) + "\rFAKE\tINVALID\r" + new string('y', 512) + "\r");
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        using var input = await interaction.ReadKeyAsync(default);
        using var secrets = input.TakeSecrets();
        Assert.AreEqual(512, secrets.AuthenticationKey.Length);
        Assert.IsFalse(terminal.Output.Contains("FAKE", StringComparison.Ordinal) || terminal.Output.Contains('*'));
    }

    [TestMethod]
    public void CLIAC023_DisplayEscapesControlsThenRedactsWholeBlock()
    {
        var terminal = new FakeTerminal();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        interaction.Show(new(ChatEventKind.Answer, text: "one\r\ntwo\r\u001b]0;bad\u0007\t" + GeminiFixtures.Key));
        Assert.IsFalse(terminal.Output.Contains('\u001b') || terminal.Output.Contains('\u0007') || terminal.Output.Contains(GeminiFixtures.Key, StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("one\ntwo\\u000D\\u001B]0;bad\\u0007\t[REDACTED]", StringComparison.Ordinal));
        using var overlap = new AIAgent.Cli.Security.SessionSecrets("\\u001B".ToCharArray());
        interaction.SetPolicy(new AIAgent.Cli.Security.SensitiveDataPolicy(overlap));
        interaction.Show(new(ChatEventKind.UserMessage, text: "\u001b"));
        Assert.IsTrue(terminal.Output.EndsWith("使用者：[REDACTED]\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CLIAC024_UsageDisplayKeepsMissingAndOverflowExplicit()
    {
        var terminal = new FakeTerminal();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var usage = new UsageAccumulator();
        usage.Record(new(LlmStatus.Failed, LlmErrorCode.SensitiveOutput, "", [], null, new(0, null, -1, 2, long.MaxValue), true, true));
        usage.Record(new(LlmStatus.Failed, LlmErrorCode.NetworkFailure, "", [], null, new(null, null, null, null, 1), true, false));
        interaction.Show(new(ChatEventKind.Usage, turnUsage: new(0, null, -1, 2, 3), sessionUsage: usage.Snapshot(), requestDispatched: true));
        Assert.IsTrue(terminal.Output.Contains("本次用量", StringComparison.Ordinal) && terminal.Output.Contains("程序累計", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("不完整（1 次缺值）", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("不可完整累計（溢位）", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("不可得", StringComparison.Ordinal));
        Assert.IsFalse(terminal.Output.Contains("-1", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CLIAC006_BusyReaderDiscardsTextAndStopsBeforeNextReader()
    {
        var terminal = new LiveTerminal();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (interaction.ObserveBusyInput(signal => { if (signal == ChatControlSignal.CancelTurn) cancelled.TrySetResult(); }))
        {
            terminal.Text("discard /exit\u0003");
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(terminal.Output.Contains("discard", StringComparison.Ordinal));
            Assert.ThrowsExactly<InvalidOperationException>(() => interaction.ReadInputAsync(default).GetAwaiter().GetResult());
        }
        terminal.Text("next\r");
        Assert.AreEqual("next", (await interaction.ReadInputAsync(default)).Text);
        Assert.AreEqual(1, terminal.MaximumReaders);
    }
}

internal sealed class LiveTerminal : ITerminal
{
    private readonly BlockingCollection<ConsoleKeyInfo> _keys = new();
    private int _readers;
    internal int MaximumReaders { get; private set; }
    internal string Output { get; private set; } = "";
    public bool IsInteractive => true;
    public IDisposable BeginInput() => new EmptyScope();
    public ConsoleKeyInfo? ReadKey(CancellationToken cancellationToken)
    {
        var readers = Interlocked.Increment(ref _readers);
        MaximumReaders = Math.Max(MaximumReaders, readers);
        try { return _keys.Take(cancellationToken); }
        finally { Interlocked.Decrement(ref _readers); }
    }
    internal void Text(string text) { foreach (var c in text) _keys.Add(new(c, ConsoleKey.NoName, false, false, false)); }
    public void DiscardBufferedInput() { while (_keys.TryTake(out _)) { } }
    public void Write(string text) => Output += text;
    private sealed class EmptyScope : IDisposable { public void Dispose() { } }
}