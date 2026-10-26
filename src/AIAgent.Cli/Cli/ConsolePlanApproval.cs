using AIAgent.Cli.Core;

namespace AIAgent.Cli.Cli;

internal sealed partial class ConsoleChatInteraction
{
    public ValueTask<PlanApprovalDecision> ConfirmPlanAsync(PlanApprovalPreview preview, CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        var decision = PlanApprovalDecision.Cancelled;
        PlanApprovalDecision? pending = null;
        void Signal(ChatControlSignal signal)
        {
            var incoming = signal switch
            {
                ChatControlSignal.Unavailable => PlanApprovalDecision.Unavailable,
                ChatControlSignal.EndOfInput => PlanApprovalDecision.EndOfInput,
                _ => PlanApprovalDecision.Cancelled
            };
            pending = pending is { } previous ? Merge(previous, incoming) : incoming;
        }
        try
        {
            if (_policy is null || !PlanProjection.SafeDisplay(preview.DisplayBlock, _policy)) return PlanApprovalDecision.Unavailable;
            _reader.DiscardBufferedInput(Signal);
            Write("[等待計畫核准] 完整預覽後請另行輸入決定。\n");
            Write(preview.DisplayBlock);
            _reader.DiscardBufferedInput(Signal);
            while (true)
            {
                if (pending is { } control) { decision = control; break; }
                token.ThrowIfCancellationRequested();
                var input = _reader.Read(token);
                Write("\n");
                if (input.Status is not (ChatInputStatus.Submitted or ChatInputStatus.Invalid))
                {
                    decision = input.Status switch
                    {
                        ChatInputStatus.Cancelled => PlanApprovalDecision.Cancelled,
                        ChatInputStatus.EndOfInput => PlanApprovalDecision.EndOfInput,
                        _ => PlanApprovalDecision.Unavailable
                    };
                    break;
                }
                var text = input.Text?.Trim();
                if (input.Status == ChatInputStatus.Submitted)
                {
                    if (string.Equals(text, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
                    { decision = PlanApprovalDecision.Approved; break; }
                    if (text?.Length == 0 || string.Equals(text, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
                    { decision = PlanApprovalDecision.Declined; break; }
                }
                const string retry = "請輸入 y／yes 或 n／no。\n" + PlanProjection.Prompt;
                if (!PlanProjection.SafeDisplay(retry, _policy)) { decision = PlanApprovalDecision.Unavailable; break; }
                Write(retry); _reader.DiscardBufferedInput(Signal);
            }
            _reader.DiscardBufferedInput(Signal);
            if (pending is { } finalControl) decision = Merge(decision, finalControl);
            if (token.IsCancellationRequested) decision = Merge(decision, PlanApprovalDecision.Cancelled);
            if (decision == PlanApprovalDecision.Approved) Write("已收到同意；正在重新檢查。\n");
            return decision;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { return Merge(decision, pending ?? PlanApprovalDecision.Cancelled); }
        catch (Exception) { return PlanApprovalDecision.Unavailable; }
    }));

    private static PlanApprovalDecision Merge(PlanApprovalDecision current, PlanApprovalDecision incoming)
    {
        if (current == PlanApprovalDecision.Unavailable || incoming == PlanApprovalDecision.Unavailable) return PlanApprovalDecision.Unavailable;
        if (current == PlanApprovalDecision.EndOfInput || incoming == PlanApprovalDecision.EndOfInput) return PlanApprovalDecision.EndOfInput;
        if (current == PlanApprovalDecision.Cancelled || incoming == PlanApprovalDecision.Cancelled) return PlanApprovalDecision.Cancelled;
        return current;
    }
}