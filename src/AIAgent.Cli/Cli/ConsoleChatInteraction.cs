using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Cli;

internal sealed class ConsoleChatInteraction(ITerminal terminal) : IChatInteraction, IDisposable
{
    private readonly object _gate = new();
    private readonly ChatInputReader _reader = new(terminal);
    private ISensitiveDataPolicy? _policy;
    private IDisposable? _inputScope;
    private CancellationTokenSource? _reading;
    private BusyScope? _busy;
    private bool _disposed;

    public void SetPolicy(ISensitiveDataPolicy policy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!terminal.IsInteractive) throw new IOException("終端機無法使用");
        _inputScope ??= terminal.BeginInput();
        _policy = policy;
    }

    private T Read<T>(CancellationToken cancellationToken, Func<CancellationToken, T> action)
    {
        CancellationTokenSource read;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_busy is not null || _reading is not null) throw new InvalidOperationException("終端機已有讀取者");
            read = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _reading = read;
        }
        try { return action(read.Token); }
        finally { lock (_gate) { _reading = null; read.Dispose(); } }
    }

    public ValueTask<ChatInputResult> ReadInputAsync(CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        try
        {
            Write("你> ");
            var result = _reader.Read(token);
            Write("\n");
            return result;
        }
        catch (OperationCanceledException) { return ChatInputResult.End(ChatInputStatus.Cancelled); }
        catch (Exception) { return ChatInputResult.End(ChatInputStatus.Unavailable); }
    }));

    public ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        var decision = FileApprovalDecision.Cancelled;
        FileApprovalDecision? pending = null;
        void Signal(ChatControlSignal signal)
        {
            var incoming = signal switch
            {
                ChatControlSignal.Unavailable => FileApprovalDecision.Unavailable,
                ChatControlSignal.EndOfInput => FileApprovalDecision.EndOfInput,
                _ => FileApprovalDecision.Cancelled
            };
            pending = pending is { } previous ? MergeApprovalControl(previous, incoming) : incoming;
        }
        try
        {
            if (_policy is null || !_policy.CanSendOrUse(preview.DisplayBlock, ContentBoundary.WriteCandidate) ||
                new UTF8Encoding(false, true).GetByteCount(preview.DisplayBlock) > WorkspaceLimits.MaxPreviewBytes)
                return FileApprovalDecision.Unavailable;
            Write(preview.DisplayBlock);
            _reader.DiscardBufferedInput(Signal);
            while (true)
            {
                if (pending is { } control) { decision = control; break; }
                token.ThrowIfCancellationRequested();
                var input = _reader.Read(token);
                Write("\n");
                if (input.Status != ChatInputStatus.Submitted && input.Status != ChatInputStatus.Invalid)
                {
                    decision = input.Status switch
                    {
                        ChatInputStatus.Cancelled => FileApprovalDecision.Cancelled,
                        ChatInputStatus.EndOfInput => FileApprovalDecision.EndOfInput,
                        _ => FileApprovalDecision.Unavailable
                    };
                    break;
                }
                var text = input.Text?.Trim();
                if (input.Status == ChatInputStatus.Submitted)
                {
                    if (string.Equals(text, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
                    { decision = FileApprovalDecision.Approved; break; }
                    if (text?.Length == 0 || string.Equals(text, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
                    { decision = FileApprovalDecision.Declined; break; }
                }
                const string retry = "請輸入 y／yes 或 n／no。\n" + FileChangePreviewBuilder.Prompt;
                if (!_policy.CanSendOrUse(retry, ContentBoundary.WriteCandidate)) { decision = FileApprovalDecision.Unavailable; break; }
                Write(retry);
                _reader.DiscardBufferedInput(Signal);
            }
            _reader.DiscardBufferedInput(Signal);
            if (pending is { } finalControl) decision = MergeApprovalControl(decision, finalControl);
            if (token.IsCancellationRequested) decision = MergeApprovalControl(decision, FileApprovalDecision.Cancelled);
            if (decision is FileApprovalDecision.Approved or FileApprovalDecision.Declined)
                Write(decision == FileApprovalDecision.Approved ? "已接受此候選；正在重新檢查。\n" : "未套用此變更。\n");
            return decision;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { return MergeApprovalControl(decision, pending ?? FileApprovalDecision.Cancelled); }
        catch (Exception) { return FileApprovalDecision.Unavailable; }
    }));

    private static FileApprovalDecision MergeApprovalControl(FileApprovalDecision current, FileApprovalDecision incoming)
    {
        if (current == FileApprovalDecision.Unavailable || incoming == FileApprovalDecision.Unavailable) return FileApprovalDecision.Unavailable;
        if (current == FileApprovalDecision.EndOfInput || incoming == FileApprovalDecision.EndOfInput) return FileApprovalDecision.EndOfInput;
        if (current == FileApprovalDecision.Cancelled || incoming == FileApprovalDecision.Cancelled) return FileApprovalDecision.Cancelled;
        return current;
    }

    public ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        try
        {
            while (true)
            {
                Write("重新輸入 Key？[y/N] ");
                var result = _reader.Read(token);
                Write("\n");
                if (result.Status == ChatInputStatus.Invalid) { Write("選擇無效；請重新選擇。\n"); continue; }
                if (result.Status != ChatInputStatus.Submitted) return result.Status switch
                {
                    ChatInputStatus.Cancelled => KeyReplacementChoice.Cancelled,
                    ChatInputStatus.EndOfInput => KeyReplacementChoice.EndOfInput,
                    _ => KeyReplacementChoice.Unavailable
                };
                var text = result.Text!.Trim();
                if (text.Equals("y", StringComparison.OrdinalIgnoreCase) || text.Equals("yes", StringComparison.OrdinalIgnoreCase)) return KeyReplacementChoice.Reenter;
                if (text.Length == 0 || text.Equals("n", StringComparison.OrdinalIgnoreCase) || text.Equals("no", StringComparison.OrdinalIgnoreCase)) return KeyReplacementChoice.Exit;
                Write("選擇無效；請輸入 y／yes 或 n／no。\n");
            }
        }
        catch (OperationCanceledException) { return KeyReplacementChoice.Cancelled; }
        catch (Exception) { return KeyReplacementChoice.Unavailable; }
    }));

    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken cancellationToken) => new(Read(cancellationToken,
        token => new HiddenKeyReader(new KeyTerminal(this, terminal)).Read(token)));

    public IDisposable ObserveBusyInput(Action<ChatControlSignal> onSignal)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_busy is not null || _reading is not null) throw new InvalidOperationException("終端機已有讀取者");
            _busy = new BusyScope(this, terminal, onSignal);
            return _busy;
        }
    }

    public void Show(ChatEvent message) => Write(ChatDisplay.Format(message));
    private void Write(string block) => ChatDisplay.Write(terminal, _policy ?? throw new InvalidOperationException("顯示政策尚未設定"), block);

    public void Dispose()
    {
        if (_disposed) return;
        try { _busy?.Dispose(); }
        finally
        {
            _disposed = true;
            try { _inputScope?.Dispose(); }
            finally { _inputScope = null; _policy = null; }
        }
    }

    // Key entry shares CRLF handling and the final display boundary with chat/choice entry.
    private sealed class KeyTerminal(ConsoleChatInteraction owner, ITerminal source) : ITerminal
    {
        public bool IsInteractive => source.IsInteractive;
        public IDisposable BeginInput() => source.BeginInput();
        public ConsoleKeyInfo? ReadKey(CancellationToken cancellationToken) => owner._reader.ReadKey(cancellationToken);
        public void DiscardBufferedInput() => source.DiscardBufferedInput();
        public ConsoleKeyInfo? TryReadBufferedKey() => source.TryReadBufferedKey();
        public void Write(string text) => owner.Write(text);
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly ConsoleChatInteraction _owner;
        private readonly ITerminal _terminal;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _worker;
        private readonly Action<ChatControlSignal> _signal;
        private bool _disposed;
        internal BusyScope(ConsoleChatInteraction owner, ITerminal terminal, Action<ChatControlSignal> signal)
        {
            _owner = owner; _terminal = terminal; _signal = signal;
            _worker = Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        var input = owner._reader.ReadKey(_stop.Token);
                        if (input is null || HiddenKeyReader.IsControl(input.Value, ConsoleKey.D, '\u0004')) { signal(ChatControlSignal.EndOfInput); break; }
                        if (HiddenKeyReader.IsControl(input.Value, ConsoleKey.C, '\u0003')) signal(ChatControlSignal.CancelTurn);
                    }
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
                catch (Exception) { signal(ChatControlSignal.Unavailable); }
            });
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _stop.Cancel(); _worker.GetAwaiter().GetResult(); _owner._reader.DiscardBufferedInput(_signal); }
            finally { lock (_owner._gate) _owner._busy = null; _stop.Dispose(); }
        }
    }
}