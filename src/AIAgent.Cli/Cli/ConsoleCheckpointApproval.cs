using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Cli;

internal sealed partial class ConsoleChatInteraction
{
    public ValueTask<FileApprovalDecision> ConfirmCheckpointAsync(CheckpointApprovalPreview preview, CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        FileApprovalDecision? pending = null;
        void Signal(ChatControlSignal signal)
        {
            var incoming = signal switch { ChatControlSignal.Unavailable => FileApprovalDecision.Unavailable, ChatControlSignal.EndOfInput => FileApprovalDecision.EndOfInput, _ => FileApprovalDecision.Cancelled };
            pending = pending is { } previous ? MergeApprovalControl(previous, incoming) : incoming;
        }
        try
        {
            if (_policy is null) return FileApprovalDecision.Unavailable;
            var policy = new WorkPersistencePolicy(_policy);
            if (preview.Prompt != (preview.IsForget ? CheckpointCommands.ForgetPrompt : CheckpointCommands.RestorePrompt) ||
                !preview.DisplayBlock.EndsWith(preview.Prompt, StringComparison.Ordinal) || !policy.IsSafe(preview.DisplayBlock) ||
                new UTF8Encoding(false, true).GetByteCount(preview.DisplayBlock) > 65536) return FileApprovalDecision.Unavailable;
            _reader.DiscardBufferedInput(Signal);
            Write(preview.DisplayBlock);
            _reader.DiscardBufferedInput(Signal);
            while (true)
            {
                if (pending is { } control) return control;
                token.ThrowIfCancellationRequested();
                var input = _reader.Read(token); Write("\n");
                if (input.Status is not (ChatInputStatus.Submitted or ChatInputStatus.Invalid))
                    return input.Status switch { ChatInputStatus.Cancelled => FileApprovalDecision.Cancelled, ChatInputStatus.EndOfInput => FileApprovalDecision.EndOfInput, _ => FileApprovalDecision.Unavailable };
                FileApprovalDecision? decision = null;
                if (input.Status == ChatInputStatus.Submitted && input.Text is { } answer && policy.IsSafe(answer))
                {
                    var text = answer.Trim();
                    if (text.Equals("y", StringComparison.OrdinalIgnoreCase) || text.Equals("yes", StringComparison.OrdinalIgnoreCase)) decision = FileApprovalDecision.Approved;
                    else if (text.Length == 0 || text.Equals("n", StringComparison.OrdinalIgnoreCase) || text.Equals("no", StringComparison.OrdinalIgnoreCase)) decision = FileApprovalDecision.Declined;
                }
                _reader.DiscardBufferedInput(Signal);
                if (pending is { } finalControl) return finalControl;
                token.ThrowIfCancellationRequested();
                if (decision is { } accepted) return accepted;
                var retry = "請輸入 y／yes 或 n／no。\n" + preview.Prompt;
                if (!policy.IsSafe(retry)) return FileApprovalDecision.Unavailable;
                Write(retry); _reader.DiscardBufferedInput(Signal);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return pending ?? FileApprovalDecision.Cancelled; }
        catch (Exception) { return FileApprovalDecision.Unavailable; }
    }));
}