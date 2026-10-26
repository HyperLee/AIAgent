using System.Text;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class TerminalEditorTests
{
    [TestMethod]
    public async Task TERMAC002_LiteralWhitespaceAndExplicitNewlineAreSubmittedTogether()
    {
        var terminal = new EditorTerminal(); terminal.Text("  中文"); terminal.Key(ConsoleKey.F4); terminal.Text("\t尾 "); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var result = await interaction.ReadInputAsync(default);
        Assert.IsTrue(result.Status == ChatInputStatus.Submitted && result.Text == "  中文\n\t尾 ", "TERM-AC-002");
        Assert.IsFalse(terminal.Output.Contains("中文", StringComparison.Ordinal) || terminal.Output.Contains('*'));
    }

    [DataRow("e\u0301", "")]
    [DataRow("🇹🇼", "")]
    [DataRow("👩🏽‍💻", "")]
    [DataRow("文😀", "文")]
    [TestMethod]
    public async Task TERMAC004_BackspaceDeletesACompleteTextElement(string text, string expected)
    {
        var terminal = new EditorTerminal(); terminal.Text(text); terminal.Key(ConsoleKey.Backspace); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == expected, "TERM-AC-004");
    }

    [TestMethod]
    public async Task TERMAC004_InsertionAndLineDeletionSnapAcrossMergedTextElements()
    {
        var join = new EditorTerminal(); join.Text("👩💻"); join.Key(ConsoleKey.LeftArrow); join.Text("\u200d!"); join.Enter();
        var flag = new EditorTerminal(); flag.Text("🇹"); flag.Key(ConsoleKey.F4); flag.Text("🇼"); flag.Key(ConsoleKey.Home); flag.Key(ConsoleKey.Backspace); flag.Text("!"); flag.Enter();
        foreach (var (terminal, expected) in new[] { (join, "👩‍💻!"), (flag, "🇹🇼!") })
        {
            using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == expected, "TERM-AC-004-merge");
        }
    }

    [TestMethod]
    public async Task TERMAC005_LogicalColumnsUseReachedColumnAndDeleteMergesLines()
    {
        var terminal = new EditorTerminal(); terminal.Text("abc"); terminal.Key(ConsoleKey.F4); terminal.Text("字"); terminal.Key(ConsoleKey.F4); terminal.Text("XYZ");
        terminal.Key(ConsoleKey.UpArrow); terminal.Key(ConsoleKey.UpArrow); terminal.Text("!"); terminal.Key(ConsoleKey.End); terminal.Key(ConsoleKey.Delete); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "a!bc字\nXYZ", "TERM-AC-005");
    }

    [TestMethod]
    public async Task TERMAC006_PasteCrLfAndTabsWaitForSeparateSubmission()
    {
        var terminal = new EditorTerminal(); terminal.Text("pre"); terminal.Key(ConsoleKey.F3); terminal.Text("a\r\nb\rc\n\t"); terminal.Key(ConsoleKey.F3); terminal.Text("post"); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "prea\nb\nc\n\tpost", "TERM-AC-006");
        Assert.IsTrue(terminal.Output.Contains("尚未送出", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TERMAC007_InvalidPasteConsumesThroughEndAndCannotBeRepaired()
    {
        var terminal = new EditorTerminal(); terminal.Key(ConsoleKey.F3); terminal.Text("a\n"); terminal.Key(ConsoleKey.F2); terminal.Text("b\n"); terminal.Key(ConsoleKey.F3);
        terminal.Ctrl(ConsoleKey.U); terminal.Text("safe"); terminal.Enter(); terminal.Text("next"); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var invalid = await interaction.ReadInputAsync(default);
        Assert.IsTrue(invalid.Status == ChatInputStatus.Invalid && invalid.Text is null, "TERM-AC-007");
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "next");
        Assert.IsFalse(terminal.Output.Contains("草稿預覽", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TERMAC009_PreviewEscapesTabsAndBackslashesAndPreservesDraftCursor()
    {
        var terminal = new EditorTerminal(); terminal.Text("A\t\\t"); terminal.Key(ConsoleKey.F4); terminal.Key(ConsoleKey.F2); terminal.Text("尾"); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "A\t\\t\n尾", "TERM-AC-009");
        Assert.IsTrue(terminal.Output.Contains("草稿預覽；尚未送出", StringComparison.Ordinal) && terminal.Output.Contains("1: A\\t\\\\t\n2: \n", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("游標：第 2 行，第 1 文字元素欄", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TERMAC010_SensitivePreviewClearsDraftAndResumesSameReader()
    {
        var terminal = new EditorTerminal(); terminal.Text("safe"); terminal.Key(ConsoleKey.F4); terminal.Text(GeminiFixtures.Key); terminal.Key(ConsoleKey.F2); terminal.Text("new"); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "new", "TERM-AC-010");
        Assert.IsFalse(terminal.Output.Contains(GeminiFixtures.Key, StringComparison.Ordinal) || terminal.Output.Contains("1: safe", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("草稿含當前 Key", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TERMAC011_PreviewDrainsOrdinaryInputButKeepsControlSignals()
    {
        foreach (var stop in new[] { '\0', '\u0003', '\u0004' })
        {
            var terminal = new EditorTerminal(); terminal.Text("draft"); terminal.Key(ConsoleKey.F2); terminal.Text("x"); terminal.Enter();
            terminal.OnWrite = block => { if (block.StartsWith("草稿預覽", StringComparison.Ordinal)) { terminal.Buffer("yes\r"); if (stop != '\0') terminal.Buffer(stop.ToString()); } };
            using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var result = await interaction.ReadInputAsync(default);
            Assert.IsTrue(stop == '\0' ? result.Text == "draftx" : result.Text is null && result.Status == (stop == '\u0003' ? ChatInputStatus.Cancelled : ChatInputStatus.EndOfInput), "TERM-AC-011");
        }
    }

    [TestMethod]
    public async Task TERMAC014_HistoryRestoresOriginalDraftAndCursorWithoutChangingStoredItem()
    {
        var terminal = new EditorTerminal(); terminal.Text("old"); terminal.Enter(); terminal.Text("ab"); terminal.Key(ConsoleKey.LeftArrow); terminal.Ctrl(ConsoleKey.P); terminal.Text("edited"); terminal.Ctrl(ConsoleKey.N); terminal.Text("!"); terminal.Enter();
        terminal.Ctrl(ConsoleKey.P); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var first = await interaction.ReadInputAsync(default); Remember(interaction, first.Text!);
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "a!b", "TERM-AC-014");
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "old");
    }

    [TestMethod]
    public async Task TERMAC003_PendingSurrogateCannotBeRepairedByNavigationOrClear()
    {
        var terminal = new EditorTerminal(); terminal.Text("\ud800"); terminal.Key(ConsoleKey.LeftArrow); terminal.Ctrl(ConsoleKey.U); terminal.Text("ok"); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.AreEqual(ChatInputStatus.Invalid, (await interaction.ReadInputAsync(default)).Status, "TERM-AC-003");
    }

    internal static void Remember(ConsoleChatInteraction interaction, string text) => interaction.RememberAcceptedInput(text);
}

internal sealed class EditorTerminal : ITerminal
{
    private readonly Queue<ConsoleKeyInfo?> _keys = new();
    private readonly Queue<ConsoleKeyInfo> _buffered = new();
    internal string Output { get; private set; } = "";
    internal Action<string>? OnWrite { get; set; }
    internal Action? OnRead { get; set; }
    internal bool FailWrite { get; set; }
    internal bool FailRead { get; set; }
    internal bool FailRestore { get; set; }
    public bool IsInteractive => true;
    internal void Text(string text) { foreach (var c in text) _keys.Enqueue(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false)); }
    internal void Event(ConsoleKeyInfo key) => _keys.Enqueue(key);
    internal void Key(ConsoleKey key) => _keys.Enqueue(KeyInputTests.Key(key));
    internal void Ctrl(ConsoleKey key) => _keys.Enqueue(new ConsoleKeyInfo((char)(key - ConsoleKey.A + 1), key, false, false, true));
    internal void Enter() => Key(ConsoleKey.Enter);
    internal void Buffer(string text) { foreach (var c in text) _buffered.Enqueue(new(c, ConsoleKey.NoName, false, false, false)); }
    public IDisposable BeginInput() => new Scope(this);
    public ConsoleKeyInfo? ReadKey(CancellationToken token)
    { token.ThrowIfCancellationRequested(); OnRead?.Invoke(); if (FailRead) throw new IOException("synthetic"); return _keys.TryDequeue(out var key) ? key : null; }
    public ConsoleKeyInfo? TryReadBufferedKey() => _buffered.TryDequeue(out var key) ? key : null;
    public void DiscardBufferedInput() { while (_buffered.TryDequeue(out _)) { } }
    public void Write(string text) { if (FailWrite) throw new IOException("synthetic"); Output += text; OnWrite?.Invoke(text); }
    private sealed class Scope(EditorTerminal owner) : IDisposable { public void Dispose() { if (owner.FailRestore) throw new IOException("synthetic"); } }
}