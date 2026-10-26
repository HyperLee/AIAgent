using System.Collections.Concurrent;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class TerminalLifecycleTests
{
    [TestMethod]
    public async Task TERMAC021_DisposalCancelsAndWaitsForOwnedReaderBeforeRestoringTerminal()
    {
        var terminal = new GatedInputTerminal(); using var session = GeminiFixtures.Session(); var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var read = Task.Run(async () => await interaction.ReadInputAsync(default));
        await terminal.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await Task.Run(interaction.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(read.IsCompleted && terminal.Readers == 0 && terminal.Restored, "TERM-AC-021");
            Assert.IsNull((await read).Text);
        }
        finally { terminal.Keys.Add(new('\u0004', ConsoleKey.D, false, false, true)); await read.WaitAsync(TimeSpan.FromSeconds(5)); interaction.Dispose(); }
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task TERMAC021_AuthenticationChoiceDrainsBeforeAndDuringPrompt(bool during)
    {
        var terminal = new EditorTerminal(); using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        if (during) terminal.OnWrite = block => { if (block.Contains("重新輸入 Key？", StringComparison.Ordinal)) terminal.Buffer("yes\r\u0004"); };
        else terminal.Buffer("yes\r\u0004");
        terminal.Text("no"); terminal.Enter();
        Assert.AreEqual(KeyReplacementChoice.EndOfInput, await interaction.ChooseKeyReplacementAsync(default), "TERM-AC-021-auth");
    }

    [TestMethod]
    public async Task TERMAC021_ReplacementKeyPromptDrainsControlsBeforeAcceptingNewKey()
    {
        var terminal = new EditorTerminal(); using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        terminal.OnWrite = block => { if (block.Contains("請輸入 API Key", StringComparison.Ordinal)) terminal.Buffer("yes\r\u0004"); };
        terminal.Text("SYNTHETIC_NEW_KEY"); terminal.Enter();
        using var result = await interaction.ReadKeyAsync(default);
        Assert.AreEqual(KeyInputStatus.EndOfInput, result.Status, "TERM-AC-021-key");
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task TERMAC022_ReplacementKeySubmitTrailingOutputCannotSwallowStop(bool eof)
    {
        var terminal = new EditorTerminal(); using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        terminal.Text("SYNTHETIC_NEW_KEY"); terminal.Enter();
        terminal.OnWrite = block => { if (block == Environment.NewLine) terminal.Buffer(eof ? "\u0004\u0003" : "\u0003"); };
        using var result = await interaction.ReadKeyAsync(default);
        Assert.AreEqual(eof ? KeyInputStatus.EndOfInput : KeyInputStatus.Cancelled, result.Status, "TERM-AC-022-key-final-output");
    }

    [TestMethod]
    public async Task TERMAC022_PasteAndPreviewStopsDiscardPartialText()
    {
        foreach (var mode in new[] { ConsoleKey.F2, ConsoleKey.F3 })
            foreach (var stop in new[] { '\u0003', '\u0004' })
            {
                var terminal = new EditorTerminal(); terminal.Text("partial"); terminal.Key(mode); terminal.Text(stop.ToString());
                using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
                var result = await interaction.ReadInputAsync(default);
                Assert.IsTrue(result.Text is null && result.Status == (stop == '\u0003' ? ChatInputStatus.Cancelled : ChatInputStatus.EndOfInput));
            }
    }

    [TestMethod]
    public void TERMAC024_ProgressUsesKnownNamesAndDistinguishesGenerationPurpose()
    {
        var unknown = ChatDisplay.Format(new(ChatEventKind.AgentProgress, agentEvent: new(AgentEventKind.ExecutingTool, 1, toolName: "arbitrary-external-name")));
        Assert.IsFalse(unknown.Contains("arbitrary-external-name", StringComparison.Ordinal), "TERM-AC-024");
        Assert.IsTrue(unknown.StartsWith("[工具開始] 未知工具", StringComparison.Ordinal));
        var known = ChatDisplay.Format(new(ChatEventKind.AgentProgress, agentEvent: new(AgentEventKind.ExecutingTool, 1, toolName: "read_file")));
        Assert.IsTrue(known.StartsWith("[工具開始] read_file", StringComparison.Ordinal));
        Assert.IsTrue(ChatDisplay.Format(new(ChatEventKind.Generating, purpose: GenerationPurpose.Compaction)).Contains("摘要維護", StringComparison.Ordinal));
        Assert.IsTrue(ChatDisplay.Format(new(ChatEventKind.Generating)).Contains("模型生成", StringComparison.Ordinal));
    }
}

internal sealed class GatedInputTerminal : ITerminal
{
    internal BlockingCollection<ConsoleKeyInfo> Keys { get; } = new();
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Readers;
    internal bool Restored;
    public bool IsInteractive => true;
    public IDisposable BeginInput() => new Scope(this);
    public ConsoleKeyInfo? ReadKey(CancellationToken token)
    {
        Interlocked.Increment(ref Readers); Started.TrySetResult();
        try { return Keys.Take(token); } finally { Interlocked.Decrement(ref Readers); }
    }
    public void DiscardBufferedInput() { }
    public void Write(string text) { }
    private sealed class Scope(GatedInputTerminal owner) : IDisposable { public void Dispose() => owner.Restored = true; }
}