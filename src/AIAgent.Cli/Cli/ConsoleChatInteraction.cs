using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

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
        public void Write(string text) => owner.Write(text);
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly ConsoleChatInteraction _owner;
        private readonly ITerminal _terminal;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _worker;
        private bool _disposed;
        internal BusyScope(ConsoleChatInteraction owner, ITerminal terminal, Action<ChatControlSignal> signal)
        {
            _owner = owner; _terminal = terminal;
            _worker = Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        var input = terminal.ReadKey(_stop.Token);
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
            try { _stop.Cancel(); _worker.GetAwaiter().GetResult(); _terminal.DiscardBufferedInput(); }
            finally { lock (_owner._gate) _owner._busy = null; _stop.Dispose(); }
        }
    }
}