using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal sealed partial class ConsoleChatInteraction
{
    private bool _modelKeyOnly;
    private static readonly string[] ModelKeyPrompts = ["請輸入 API Key（不回顯）：", "輸入無效，請重新輸入。" + Environment.NewLine, Environment.NewLine];

    public bool PrepareModelKeyInput() => _policy is not null && ModelKeyPrompts.All(p => _policy.CanSendOrUse(p, ContentBoundary.ToolResult));
    public void DetachPolicy()
    {
        lock (_gate)
        {
            if (_reading is not null) throw new InvalidOperationException("終端機仍有讀取者");
            _editor.Release(); _policy = null; _modelKeyOnly = true;
        }
    }

    public ValueTask<ModelApprovalDecision> ConfirmModelAsync(ModelApprovalPreview preview, CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        ModelApprovalDecision? pending = null;
        void Signal(ChatControlSignal signal)
        {
            var value = signal switch { ChatControlSignal.Unavailable => ModelApprovalDecision.Unavailable, ChatControlSignal.EndOfInput => ModelApprovalDecision.EndOfInput, _ => ModelApprovalDecision.Cancelled };
            if (pending is null || value == ModelApprovalDecision.Unavailable || pending != ModelApprovalDecision.Unavailable && value == ModelApprovalDecision.EndOfInput) pending = value;
        }
        try
        {
            if (_policy is null || !ContextDisplay.Safe(preview.DisplayBlock, _policy)) return ModelApprovalDecision.Unavailable;
            _reader.DiscardBufferedInput(Signal);
            Write(preview.DisplayBlock);
            _reader.DiscardBufferedInput(Signal);
            if (pending is { } before) return before;
            token.ThrowIfCancellationRequested();
            var input = _reader.Read(token); Write("\n");
            _reader.DiscardBufferedInput(Signal);
            if (pending is { } after) return after;
            token.ThrowIfCancellationRequested();
            if (input.Status != ChatInputStatus.Submitted) return input.Status switch
            {
                ChatInputStatus.Cancelled => ModelApprovalDecision.Cancelled,
                ChatInputStatus.EndOfInput => ModelApprovalDecision.EndOfInput,
                ChatInputStatus.Invalid => ModelApprovalDecision.Declined,
                _ => ModelApprovalDecision.Unavailable
            };
            return input.Text is { } answer && _policy.CanSendOrUse(answer, ContentBoundary.ModelInput) &&
                (answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase))
                ? ModelApprovalDecision.Approved : ModelApprovalDecision.Declined;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return pending ?? ModelApprovalDecision.Cancelled; }
        catch (Exception) { return ModelApprovalDecision.Unavailable; }
    }));
}