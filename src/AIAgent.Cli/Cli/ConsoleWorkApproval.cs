using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Cli;

internal sealed partial class ConsoleChatInteraction
{
    public ValueTask<WorkApprovalDecision> ConfirmWorkAsync(WorkApprovalPreview preview, CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        WorkApprovalDecision? pending = null;
        void Signal(ChatControlSignal signal)
        {
            var incoming = signal switch { ChatControlSignal.Unavailable => WorkApprovalDecision.Unavailable, ChatControlSignal.EndOfInput => WorkApprovalDecision.EndOfInput, _ => WorkApprovalDecision.Cancelled };
            if (pending is null || incoming == WorkApprovalDecision.Unavailable || pending != WorkApprovalDecision.Unavailable && incoming == WorkApprovalDecision.EndOfInput) pending = incoming;
        }
        try
        {
            if (_policy is null) return WorkApprovalDecision.Unavailable;
            var persistence = new WorkPersistencePolicy(_policy);
            if (!persistence.IsSafe(preview.DisplayBlock) || new UTF8Encoding(false, true).GetByteCount(preview.DisplayBlock) > 65536) return WorkApprovalDecision.Unavailable;
            _reader.DiscardBufferedInput(Signal);
            Write(preview.DisplayBlock);
            _reader.DiscardBufferedInput(Signal);
            while (true)
            {
                if (pending is { } control) return control;
                token.ThrowIfCancellationRequested();
                var input = _reader.Read(token); Write("\n");
                if (input.Status is not (ChatInputStatus.Submitted or ChatInputStatus.Invalid))
                    return input.Status switch { ChatInputStatus.Cancelled => WorkApprovalDecision.Cancelled, ChatInputStatus.EndOfInput => WorkApprovalDecision.EndOfInput, _ => WorkApprovalDecision.Unavailable };
                var decision = (WorkApprovalDecision?)null;
                if (input.Status == ChatInputStatus.Submitted && input.Text is { } answer && persistence.IsSafe(answer))
                {
                    var value = answer.Trim();
                    if (value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase)) decision = WorkApprovalDecision.Approved;
                    else if (value.Length == 0 || value.Equals("n", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase)) decision = WorkApprovalDecision.Declined;
                }
                _reader.DiscardBufferedInput(Signal);
                if (pending is { } finalControl) return finalControl;
                token.ThrowIfCancellationRequested();
                if (decision is { } accepted) return accepted;
                var retry = "請輸入 y／yes 或 n／no。\n" + WorkSessionCoordinator.Prompt(preview.Kind);
                if (!persistence.IsSafe(retry)) return WorkApprovalDecision.Unavailable;
                Write(retry); _reader.DiscardBufferedInput(Signal);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return pending ?? WorkApprovalDecision.Cancelled; }
        catch (Exception) { return WorkApprovalDecision.Unavailable; }
    }));
}