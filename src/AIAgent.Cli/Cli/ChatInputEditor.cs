using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Cli;

// All editing state is CLI-owned. The short state gate never covers reads or display I/O.
internal sealed class ChatInputEditor
{
    private readonly object _gate = new();
    private readonly List<string> _history = [];
    private ChatDraft? _draft;
    private ChatDraftSnapshot? _restore;
    private int? _browse;
    private ISensitiveDataPolicy? _policy;
    private string? _candidate;
    private long _generation;
    private int _historyUnits;
    private bool _paste;

    internal void SetPolicy(ISensitiveDataPolicy policy) { lock (_gate) { Clear(); _policy = policy; } }
    internal void Clear()
    {
        lock (_gate)
        {
            _generation++; _candidate = null; _draft?.Dispose(); _draft = null;
            _restore = null; _browse = null; _history.Clear(); _historyUnits = 0; _paste = false;
        }
    }
    internal void AbandonSubmission() { lock (_gate) _candidate = null; }
    internal void Release() { lock (_gate) { Clear(); _policy = null; } }
    internal void Remember(string text)
    {
        lock (_gate)
        {
            // Admission can finish after a clear. Only this reader's still-owned submission is eligible.
            if (!ReferenceEquals(_candidate, text)) return;
            _candidate = null;
            var generation = _generation;
            if (_policy is null || !ChatInputValidation.IsValid(text) || string.IsNullOrWhiteSpace(text) ||
                text.TrimStart().StartsWith('/') || !_policy.CanSendOrUse(text, ContentBoundary.ModelInput)) return;
            if (generation != _generation) return;
            if (_history.Count > 0 && string.Equals(_history[^1], text, StringComparison.Ordinal)) return;
            _history.Add(text); _historyUnits += text.Length;
            while (_history.Count > 20 || _historyUnits > 65536)
            { _historyUnits -= _history[0].Length; _history.RemoveAt(0); }
        }
    }
    internal ChatInputResult Read(ChatInputReader reader, Action<string> write, CancellationToken token)
    {
        ChatInputStatus? pending = null;
        void Signal(ChatControlSignal signal)
        {
            if (signal == ChatControlSignal.Unavailable) pending = ChatInputStatus.Unavailable;
            else if (pending != ChatInputStatus.Unavailable && signal == ChatControlSignal.EndOfInput) pending = ChatInputStatus.EndOfInput;
            else pending ??= ChatInputStatus.Cancelled;
        }
        void Present(string text)
        {
            write(text); reader.DiscardBufferedInput(Signal);
        }
        lock (_gate) { _candidate = null; _draft?.Dispose(); _draft = new(); _restore = null; _browse = null; _paste = false; }
        try
        {
            reader.DiscardBufferedInput(Signal);
            Present(ChatDraftPreview.Hint + "\n你> ");
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (pending is { } control) return ChatInputResult.End(control);
                long observedGeneration;
                lock (_gate) observedGeneration = _generation;
                var input = reader.ReadKey(token);
                token.ThrowIfCancellationRequested();
                if (input is null || HiddenKeyReader.IsControl(input.Value, ConsoleKey.D, '\u0004')) return ChatInputResult.End(ChatInputStatus.EndOfInput);
                var key = input.Value;
                if (HiddenKeyReader.IsControl(key, ConsoleKey.C, '\u0003')) return ChatInputResult.End(ChatInputStatus.Cancelled);
                string? display = null;
                lock (_gate)
                {
                    if (observedGeneration != _generation) continue;
                    _draft ??= new();
                    var draft = _draft;
                    if (key.Key == ConsoleKey.F3)
                    {
                        draft.FinishCharacter(); _paste = !_paste;
                        display = _paste ? "貼上收集中；換行不送出，F3 結束收集。\n" : "貼上收集已結束；尚未送出，F2 預覽或 Enter 送出。\n";
                    }
                    else if (_paste)
                    {
                        if (key.Key == ConsoleKey.Enter || key.KeyChar is '\r' or '\n') draft.Character('\n');
                        else if (IsText(key)) draft.Character(key.KeyChar);
                        else draft.Invalidate();
                    }
                    else if (key.Key == ConsoleKey.Enter || key.KeyChar is '\r' or '\n')
                    {
                        draft.FinishCharacter();
                        if (draft.Invalid) return ChatInputResult.End(ChatInputStatus.Invalid);
                        var text = draft.Text; _candidate = text;
                        return ChatInputResult.Submitted(text);
                    }
                    else if (key.Key == ConsoleKey.F2)
                    {
                        draft.FinishCharacter();
                        if (draft.Invalid) display = ChatDraftPreview.Invalid + "\n";
                        else if (!_policy!.CanSendOrUse(draft.Text, ContentBoundary.ModelInput))
                        {
                            draft.Dispose(); _draft = new(); _restore = null; _browse = null;
                            display = "草稿含當前 Key；已清除且未送出。\n" + ChatDraftPreview.Hint + "\n你> ";
                        }
                        else display = ChatDraftPreview.Build(draft.Snapshot(), _policy) ?? "無法完整安全預覽；草稿尚未送出。\n";
                    }
                    else if (HiddenKeyReader.IsControl(key, ConsoleKey.P, '\u0010') || HiddenKeyReader.IsControl(key, ConsoleKey.N, '\u000e'))
                    { draft.FinishCharacter(); if (!draft.Invalid) display = Recall(HiddenKeyReader.IsControl(key, ConsoleKey.P, '\u0010')); }
                    else if (HiddenKeyReader.IsControl(key, ConsoleKey.U, '\u0015'))
                    { draft.Clear(); _restore = null; _browse = null; }
                    else if (key.Key == ConsoleKey.F4) { draft.FinishCharacter(); draft.Character('\n'); }
                    else if (key.Key is ConsoleKey.Backspace or ConsoleKey.Delete || key.KeyChar is '\b' or '\u007f')
                        draft.Delete(key.Key != ConsoleKey.Delete);
                    else if (HiddenKeyReader.IsNavigation(key.Key)) draft.Move(key.Key);
                    else if (IsText(key)) draft.Character(key.KeyChar);
                    else
                    {
                        draft.FinishCharacter();
                        // Only decoded pure function/navigation events may carry an ignored NUL.
                        if (key.KeyChar != '\0' || key.Key is not (>= ConsoleKey.F1 and <= ConsoleKey.F24)) draft.Invalidate();
                    }
                }
                if (display is not null) Present(display);
            }
        }
        finally
        {
            lock (_gate) { _draft?.Dispose(); _draft = null; _restore = null; _browse = null; _paste = false; }
        }
    }
    private static bool IsText(ConsoleKeyInfo key) => key.KeyChar == '\t' || key.KeyChar != '\0' &&
        !char.IsControl(key.KeyChar) && !key.Modifiers.HasFlag(ConsoleModifiers.Control);

    private string? Recall(bool previous)
    {
        if (_history.Count == 0 || !previous && _browse is null) return null;
        var draft = _draft!;
        var target = previous ? (_browse ?? _history.Count) - 1 : _browse!.Value + 1;
        if (target < 0) return null;
        var text = target == _history.Count ? _restore!.Text : _history[target];
        if (!ChatInputValidation.IsValid(text) || !_policy!.CanSendOrUse(text, ContentBoundary.ModelInput))
        {
            _history.Clear(); _historyUnits = 0; _restore = null; _browse = null;
            if (!_policy!.CanSendOrUse(draft.Text, ContentBoundary.ModelInput)) draft.Clear();
            return "無法安全回取輸入歷史；歷史已清除。\n";
        }
        if (_browse is null) _restore = draft.Snapshot();
        draft.Load(text, target == _history.Count ? _restore!.Cursor : text.Length);
        if (target == _history.Count) { _browse = null; _restore = null; }
        else _browse = target;
        return null;
    }
    public override string ToString() => "[ChatInputEditor]";
}