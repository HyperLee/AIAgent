using System.Text;
using System.Text.Json;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class TerminalBoundaryTests
{
    [DataRow(16384, false, true)]
    [DataRow(16385, false, false)]
    [DataRow(8192, true, true)]
    [DataRow(8193, true, false)]
    [TestMethod]
    public async Task TERMAC003_DraftLimitCountsOriginalUtf16Units(int count, bool supplementary, bool valid)
    {
        var terminal = new EditorTerminal(); terminal.Text(supplementary ? string.Concat(Enumerable.Repeat("😀", count)) : new string('x', count)); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var result = await interaction.ReadInputAsync(default);
        Assert.IsTrue(valid ? result.Status == ChatInputStatus.Submitted && result.Text!.Length == (supplementary ? count * 2 : count) : result.Status == ChatInputStatus.Invalid && result.Text is null, "TERM-AC-003");
    }

    [DataRow(ConsoleKey.Spacebar)]
    [DataRow(ConsoleKey.A)]
    [TestMethod]
    public async Task TERMAC003_NulFromUnconfiguredTextControlIsPermanentlyInvalid(ConsoleKey key)
    {
        var terminal = new EditorTerminal(); terminal.Event(new('\0', key, false, false, true)); terminal.Ctrl(ConsoleKey.U); terminal.Text("safe"); terminal.Enter();
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var result = await interaction.ReadInputAsync(default);
        Assert.IsTrue(result.Status == ChatInputStatus.Invalid && result.Text is null, "TERM-AC-003-nul-control");
    }

    [TestMethod]
    public void TERMAC011_FullPreviewAcceptsExactly65536BytesAndRejects65537()
    {
        using var session = GeminiFixtures.Session();
        // 9001 numbered empty lines plus fixed labels and cursor (1,1) take 62132 UTF-8 bytes.
        var exact = new ChatDraftSnapshot(new string('x', 3404) + new string('\n', 9000), 0);
        var block = ChatDraftPreview.Build(exact, session.Policy);
        Assert.IsNotNull(block, "TERM-AC-011"); Assert.AreEqual(65536, new UTF8Encoding(false, true).GetByteCount(block));
        Assert.IsNull(ChatDraftPreview.Build(new(new string('x', 3405) + new string('\n', 9000), 0), session.Policy));
    }

    [TestMethod]
    public async Task TERMAC010_RenderCollisionPreservesSafeDraftWithoutPartialPreview()
    {
        var terminal = new EditorTerminal(); terminal.Text("safe"); terminal.Key(ConsoleKey.F2); terminal.Text("!"); terminal.Enter();
        using var secrets = new SessionSecrets("1: safe".ToCharArray()); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(new SensitiveDataPolicy(secrets));
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "safe!", "TERM-AC-010");
        Assert.IsFalse(terminal.Output.Contains("草稿預覽", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("無法完整安全預覽", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TERMAC010_PreviewHasNoShortKeyCommandException()
    {
        var terminal = new EditorTerminal(); terminal.Text("/clear"); terminal.Key(ConsoleKey.F2); terminal.Text("ok"); terminal.Enter();
        using var secrets = new SessionSecrets("e".ToCharArray()); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(new SensitiveDataPolicy(secrets));
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "ok");
        Assert.IsFalse(terminal.Output.Contains("/clear", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TERMAC013_HistoryEvictsWholeOldestItemsAtCountAndTotalLimits()
    {
        foreach (var totalLimit in new[] { false, true })
        {
            var terminal = new EditorTerminal(); using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var count = totalLimit ? 4 : 20;
            for (var i = 0; i < count; i++) { terminal.Text(totalLimit ? new string((char)('a' + i), 16384) : "entry" + i); terminal.Enter(); var item = await interaction.ReadInputAsync(default); interaction.RememberAcceptedInput(item.Text!); }
            terminal.Text("z"); terminal.Enter(); interaction.RememberAcceptedInput((await interaction.ReadInputAsync(default)).Text!);
            for (var i = 0; i < 25; i++) terminal.Ctrl(ConsoleKey.P);
            terminal.Enter(); var oldest = (await interaction.ReadInputAsync(default)).Text;
            Assert.IsTrue(oldest == (totalLimit ? new string('b', 16384) : "entry1"), "TERM-AC-013");
        }
    }

    [TestMethod]
    public async Task TERMAC013_OnlyAdjacentExactDuplicatesAreRemoved()
    {
        var terminal = new EditorTerminal(); using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        foreach (var text in new[] { "a", "a", " a", "a" }) { terminal.Text(text); terminal.Enter(); interaction.RememberAcceptedInput((await interaction.ReadInputAsync(default)).Text!); }
        terminal.Ctrl(ConsoleKey.P); terminal.Ctrl(ConsoleKey.P); terminal.Enter();
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == " a", "TERM-AC-013");
        terminal.Ctrl(ConsoleKey.P); terminal.Ctrl(ConsoleKey.P); terminal.Ctrl(ConsoleKey.P); terminal.Ctrl(ConsoleKey.N); terminal.Enter();
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == " a");
    }

    [TestMethod]
    public async Task TERMAC015_RecalledCandidateIsRecheckedAndOriginalSafeDraftIsKept()
    {
        var terminal = new EditorTerminal(); var policy = new MutableInputPolicy(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(policy);
        terminal.Text("old"); terminal.Enter(); interaction.RememberAcceptedInput((await interaction.ReadInputAsync(default)).Text!);
        policy.Blocked = "old"; terminal.Text("safe"); terminal.Ctrl(ConsoleKey.P); terminal.Enter();
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "safe", "TERM-AC-015");
        policy.Blocked = null; terminal.Ctrl(ConsoleKey.P); terminal.Enter(); Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "");
    }

    [TestMethod]
    public async Task TERMAC018_ClearInvalidatesLateAdmissionAndPolicySwitchDropsHistory()
    {
        var terminal = new EditorTerminal(); using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        terminal.Text("old"); terminal.Enter(); var item = await interaction.ReadInputAsync(default);
        interaction.ClearInputState(); interaction.RememberAcceptedInput(item.Text!); terminal.Ctrl(ConsoleKey.P); terminal.Enter();
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "", "TERM-AC-018");
        terminal.Text("safe"); terminal.Enter(); interaction.RememberAcceptedInput((await interaction.ReadInputAsync(default)).Text!);
        interaction.SetPolicy(session.Policy); terminal.Ctrl(ConsoleKey.P); terminal.Enter(); Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "");
    }

    [TestMethod]
    public async Task TERMAC018_ClearDuringAnInFlightReadDiscardsTheOldKey()
    {
        var terminal = new EditorTerminal(); using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        terminal.Text("a"); terminal.Enter(); terminal.OnRead = () => { terminal.OnRead = null; interaction.ClearInputState(); };
        Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "", "TERM-AC-018-late-key");
    }

    [TestMethod]
    public async Task TERMAC015_ClearDuringPolicyCheckCannotPublishAnOldHistoryCandidate()
    {
        var terminal = new EditorTerminal(); var policy = new MutableInputPolicy(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(policy);
        terminal.Text("old"); terminal.Enter(); var item = await interaction.ReadInputAsync(default);
        policy.OnCheck = interaction.ClearInputState; interaction.RememberAcceptedInput(item.Text!);
        terminal.Ctrl(ConsoleKey.P); terminal.Enter(); Assert.IsTrue((await interaction.ReadInputAsync(default)).Text == "", "TERM-AC-015-clear-policy-race");
    }

    [TestMethod]
    public async Task TERMAC022_EditorIoFailuresReturnUnavailableAndRestorationFailureIsVisible()
    {
        foreach (var read in new[] { true, false })
        {
            var terminal = new EditorTerminal { FailRead = read, FailWrite = !read }; using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            Assert.AreEqual(ChatInputStatus.Unavailable, (await interaction.ReadInputAsync(default)).Status);
        }
        var failed = new EditorTerminal { FailRestore = true }; using var other = GeminiFixtures.Session(); var owner = new ConsoleChatInteraction(failed); owner.SetPolicy(other.Policy);
        Assert.ThrowsExactly<IOException>(owner.Dispose);
    }

    [TestMethod]
    public void TERMAC028_EditorObjectsDoNotDumpOrSerializeContents()
    {
        using var draft = new ChatDraft(); foreach (var c in "private draft") draft.Character(c);
        Assert.AreEqual("[ChatDraft]", draft.ToString()); Assert.AreEqual("[ChatDraftSnapshot]", draft.Snapshot().ToString());
        Assert.IsFalse(JsonSerializer.Serialize(draft).Contains("private draft", StringComparison.Ordinal));
        Assert.IsFalse(JsonSerializer.Serialize(draft.Snapshot()).Contains("private draft", StringComparison.Ordinal));
    }
}

internal sealed class MutableInputPolicy : ISensitiveDataPolicy
{
    internal string? Blocked { get; set; }
    internal Action? OnCheck { get; set; }
    public bool ContainsCurrentKey(string content) => Blocked is { } text && content.Contains(text, StringComparison.Ordinal);
    public bool CanSendOrUse(string content, ContentBoundary boundary)
    { var action = OnCheck; OnCheck = null; action?.Invoke(); return !ContainsCurrentKey(content); }
    public string RedactForDisplay(string content) => Blocked is { } text ? content.Replace(text, "[REDACTED]", StringComparison.Ordinal) : content;
}