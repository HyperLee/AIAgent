using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;

namespace AIAgent.Cli.Cli;

internal sealed partial class ConsoleChatInteraction
{
    public ValueTask<ProcessApprovalDecision> ConfirmProcessAsync(ProcessApprovalPreview preview, CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        var decision = ProcessApprovalDecision.Cancelled;
        ProcessApprovalDecision? pending = null;
        void Signal(ChatControlSignal signal)
        {
            var incoming = signal switch
            {
                ChatControlSignal.Unavailable => ProcessApprovalDecision.Unavailable,
                ChatControlSignal.EndOfInput => ProcessApprovalDecision.EndOfInput,
                _ => ProcessApprovalDecision.Cancelled
            };
            pending = pending is { } previous ? Merge(previous, incoming) : incoming;
        }
        try
        {
            if (_policy is null || !preview.DisplayBlock.EndsWith(ProcessApprovalText.Prompt, StringComparison.Ordinal) || !PlanProjection.SafeDisplay(preview.DisplayBlock, _policy)) return ProcessApprovalDecision.Unavailable;
            _reader.DiscardBufferedInput(Signal);
            Write("[等待程序核准] 完整預覽後請另行輸入決定。\n");
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
                        ChatInputStatus.Cancelled => ProcessApprovalDecision.Cancelled,
                        ChatInputStatus.EndOfInput => ProcessApprovalDecision.EndOfInput,
                        _ => ProcessApprovalDecision.Unavailable
                    };
                    break;
                }
                var text = input.Text?.Trim();
                if (input.Status == ChatInputStatus.Submitted)
                {
                    if (string.Equals(text, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
                    { decision = ProcessApprovalDecision.Approved; break; }
                    if (text?.Length == 0 || string.Equals(text, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
                    { decision = ProcessApprovalDecision.Declined; break; }
                }
                const string retry = "請輸入 y／yes 或 n／no。\n" + ProcessApprovalText.Prompt;
                if (!PlanProjection.SafeDisplay(retry, _policy)) { decision = ProcessApprovalDecision.Unavailable; break; }
                Write(retry); _reader.DiscardBufferedInput(Signal);
            }
            _reader.DiscardBufferedInput(Signal);
            if (pending is { } finalControl) decision = Merge(decision, finalControl);
            if (token.IsCancellationRequested) decision = Merge(decision, ProcessApprovalDecision.Cancelled);
            if (decision == ProcessApprovalDecision.Approved) Write("已收到同意；正在重新檢查。\n");
            return decision;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { return Merge(decision, pending ?? ProcessApprovalDecision.Cancelled); }
        catch (Exception) { return ProcessApprovalDecision.Unavailable; }
    }));

    private static ProcessApprovalDecision Merge(ProcessApprovalDecision current, ProcessApprovalDecision incoming)
    {
        if (current == ProcessApprovalDecision.Unavailable || incoming == ProcessApprovalDecision.Unavailable) return ProcessApprovalDecision.Unavailable;
        if (current == ProcessApprovalDecision.EndOfInput || incoming == ProcessApprovalDecision.EndOfInput) return ProcessApprovalDecision.EndOfInput;
        if (current == ProcessApprovalDecision.Cancelled || incoming == ProcessApprovalDecision.Cancelled) return ProcessApprovalDecision.Cancelled;
        return current;
    }
}