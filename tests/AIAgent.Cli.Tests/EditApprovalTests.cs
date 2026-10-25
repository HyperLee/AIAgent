using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditApprovalTests
{
    [TestMethod]
    public async Task EDITAC014_OnlyExplicitFreshYesApprovesAndInputIsHidden()
    {
        foreach (var (input, expected) in new[] { ("y\r", FileApprovalDecision.Approved), (" YeS \r", FileApprovalDecision.Approved),
            ("n\r", FileApprovalDecision.Declined), ("NO\r", FileApprovalDecision.Declined), (" \r", FileApprovalDecision.Declined),
            ("\u0003", FileApprovalDecision.Cancelled), ("\u0004", FileApprovalDecision.EndOfInput) })
        {
            var terminal = new ApprovalTerminal(); terminal.Buffered("yes\r\n"); terminal.Bursts.Enqueue(input);
            using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var preview = new FileChangePreview(FileChangeKind.Create, "a", 0, 1, false, false, "preview\n套用此變更？[y/N] ", System.Text.Encoding.UTF8.GetByteCount("preview\n套用此變更？[y/N] "));
            var decision = await ((IChatInteraction)interaction).ConfirmAsync(preview, default);
            Assert.AreEqual(expected, decision, "EDIT-AC-014");
            Assert.AreEqual(1, terminal.MaximumReaders, "EDIT-AC-013-one-reader");
            Assert.IsFalse(terminal.Output.Contains("yes\r", StringComparison.Ordinal) || terminal.Output.Contains("YeS", StringComparison.Ordinal), "EDIT-AC-014-hidden");
        }
    }

    [TestMethod]
    public async Task EDITAC014_InvalidChoicesRepromptWithoutExecutingCommands()
    {
        var terminal = new ApprovalTerminal();
        foreach (var input in new[] { "/clear\r", "/exit\r", "bad\t\r", "yes\r" }) terminal.Bursts.Enqueue(input);
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        var preview = new FileChangePreview(FileChangeKind.Create, "a", 0, 1, false, false, "preview\n套用此變更？[y/N] ", System.Text.Encoding.UTF8.GetByteCount("preview\n套用此變更？[y/N] "));
        Assert.AreEqual(FileApprovalDecision.Approved, await ((IChatInteraction)interaction).ConfirmAsync(preview, default), "EDIT-AC-014-retry");
        Assert.AreEqual(3, terminal.Output.Split("請輸入 y／yes 或 n／no。", StringSplitOptions.None).Length - 1, "EDIT-AC-014-invalid-count");
        Assert.IsFalse(terminal.Output.Contains("/exit", StringComparison.Ordinal) || terminal.Output.Contains("bad", StringComparison.Ordinal), "EDIT-AC-041-data-only");
    }

    [TestMethod]
    public async Task EDITAC016_UnboundOrDifferentSessionCannotApprove()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); using var other = WorkspaceFixtures.Session(root);
        var factory = new TestWorkspaceFilesFactory(); await using var files = factory.Create(session);
        Assert.AreEqual(WorkspaceErrorCode.Unavailable, (await files.CreateFileAsync("a", "x", default)).ErrorCode, "EDIT-AC-016-unbound");
        using (factory.ApprovalBridge.Bind(other, (_, _) => new(FileApprovalDecision.Approved)))
            Assert.AreEqual(WorkspaceErrorCode.Unavailable, (await files.CreateFileAsync("a", "x", default)).ErrorCode, "EDIT-AC-016-session");
        Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length, "EDIT-AC-016-no-write");
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EDITAC030_ObservedEofCannotBeDowngradedByBufferedCancel(bool buffered)
    {
        var terminal = new ApprovalTerminal();
        if (buffered) terminal.Buffered("\u0004\u0003");
        else terminal.Bursts.Enqueue("\u0004\u0003");
        using var session = GeminiFixtures.Session(); using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        const string block = "preview\n套用此變更？[y/N] ";
        var preview = new FileChangePreview(FileChangeKind.Create, "a", 0, 1, false, false, block, System.Text.Encoding.UTF8.GetByteCount(block));
        Assert.AreEqual(FileApprovalDecision.EndOfInput, await interaction.ConfirmAsync(preview, default), "EDIT-AC-030-eof-priority");
    }

    [TestMethod]
    public async Task EDITAC030_RealChatApprovalEofAndCancelExitWithoutAnotherTurn()
    {
        var root = WorkspaceFixtures.NewDirectory(); var terminal = new ApprovalEofTerminal();
        using var interaction = new ConsoleChatInteraction(terminal);
        var handler = new GeminiHandler
        {
            Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(
            "[{\"functionCall\":{\"name\":\"create_file\",\"args\":{\"path\":\"a\",\"content\":\"x\"}}}]")))
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var app = new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "instruction", new TestWorkspaceFilesFactory());
        var exit = await Task.Run(() => app.RunAsync(WorkspaceFixtures.Session(root), cancellation.Token));
        Assert.IsTrue(exit == 0 && terminal.ChatPrompts == 1 && handler.Calls == 1 && Directory.GetFileSystemEntries(root).Length == 0, "EDIT-AC-030-eof-stops-chat");
    }

}

internal sealed class ApprovalTerminal : ITerminal
{
    private readonly Queue<ConsoleKeyInfo> _pending = new();
    internal Queue<string> Bursts { get; } = new();
    internal string Output { get; private set; } = "";
    internal int MaximumReaders { get; private set; }
    private int _readers;
    public bool IsInteractive => true;
    public IDisposable BeginInput() => new Scope();
    internal void Buffered(string text) { foreach (var c in text) _pending.Enqueue(new(c, ConsoleKey.NoName, false, false, false)); }
    public ConsoleKeyInfo? TryReadBufferedKey() => _pending.TryDequeue(out var key) ? key : null;
    public ConsoleKeyInfo? ReadKey(CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); MaximumReaders = Math.Max(MaximumReaders, ++_readers);
        try
        {
            if (_pending.Count == 0 && Bursts.TryDequeue(out var burst)) Buffered(burst);
            return _pending.TryDequeue(out var key) ? key : null;
        }
        finally { _readers--; }
    }
    public void DiscardBufferedInput() => _pending.Clear();
    public void Write(string text) => Output += text;
    private sealed class Scope : IDisposable { public void Dispose() { } }
}

internal sealed class ApprovalEofTerminal : ITerminal
{
    private readonly LiveTerminal _inner = new();
    private bool _previewPrinted;
    private bool _injectOnRead;
    internal int ChatPrompts { get; private set; }
    public bool IsInteractive => true;
    public IDisposable BeginInput() => _inner.BeginInput();
    public void DiscardBufferedInput() => _inner.DiscardBufferedInput();
    public ConsoleKeyInfo? TryReadBufferedKey()
    {
        var key = _inner.TryReadBufferedKey();
        if (key is null && _previewPrinted) { _previewPrinted = false; _injectOnRead = true; }
        return key;
    }
    public ConsoleKeyInfo? ReadKey(CancellationToken token)
    {
        if (_injectOnRead) { _injectOnRead = false; _inner.Text("\u0004\u0003"); }
        return _inner.ReadKey(token);
    }
    public void Write(string text)
    {
        _inner.Write(text);
        if (text == "你> " && ++ChatPrompts == 1) _inner.Text("create\r");
        if (text.EndsWith(FileChangePreviewBuilder.Prompt, StringComparison.Ordinal)) _previewPrinted = true;
    }
}