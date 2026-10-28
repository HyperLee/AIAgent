using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Cli;

internal sealed partial class ConsoleChatInteraction
{
    public ValueTask<McpApprovalDecision> ConfirmMcpAsync(McpApprovalPreview preview, CancellationToken cancellationToken) => new(Read(cancellationToken, token =>
    {
        var decision = McpApprovalDecision.Cancelled;
        McpApprovalDecision? pending = null;
        void Signal(ChatControlSignal signal)
        {
            var incoming = signal switch
            {
                ChatControlSignal.Unavailable => McpApprovalDecision.Unavailable,
                ChatControlSignal.EndOfInput => McpApprovalDecision.EndOfInput,
                _ => McpApprovalDecision.Cancelled
            };
            pending = pending is { } previous ? MergeMcpControl(previous, incoming) : incoming;
        }
        try
        {
            if (_policy is null || !_policy.CanSendOrUse(preview.DisplayBlock, ContentBoundary.ToolResult) ||
                new UTF8Encoding(false, true).GetByteCount(preview.DisplayBlock) > WorkspaceLimits.MaxPreviewBytes)
                return McpApprovalDecision.Unavailable;
            _reader.DiscardBufferedInput(Signal);
            Write("[等待 MCP 外送確認] 完整預覽後請另行輸入決定。\n");
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
                        ChatInputStatus.Cancelled => McpApprovalDecision.Cancelled,
                        ChatInputStatus.EndOfInput => McpApprovalDecision.EndOfInput,
                        _ => McpApprovalDecision.Unavailable
                    };
                    break;
                }
                var text = input.Text?.Trim();
                if (input.Status == ChatInputStatus.Submitted)
                {
                    if (string.Equals(text, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
                    { decision = McpApprovalDecision.Approved; break; }
                    if (text?.Length == 0 || string.Equals(text, "n", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
                    { decision = McpApprovalDecision.Declined; break; }
                }
                const string retry = "請輸入 y／yes 或 n／no。\n" + "確認本次外送？[y/N] ";
                if (!_policy.CanSendOrUse(retry, ContentBoundary.ToolResult)) { decision = McpApprovalDecision.Unavailable; break; }
                Write(retry);
                _reader.DiscardBufferedInput(Signal);
            }
            _reader.DiscardBufferedInput(Signal);
            if (pending is { } finalControl) decision = MergeMcpControl(decision, finalControl);
            if (token.IsCancellationRequested) decision = MergeMcpControl(decision, McpApprovalDecision.Cancelled);
            if (decision is McpApprovalDecision.Approved or McpApprovalDecision.Declined)
                Write(decision == McpApprovalDecision.Approved ? "已接受此候選；正在重新檢查。\n" : "未核准本次外送。\n");
            return decision;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { return MergeMcpControl(decision, pending ?? McpApprovalDecision.Cancelled); }
        catch (Exception) { return McpApprovalDecision.Unavailable; }
    }));

    private static McpApprovalDecision MergeMcpControl(McpApprovalDecision current, McpApprovalDecision incoming)
    {
        if (current == McpApprovalDecision.Unavailable || incoming == McpApprovalDecision.Unavailable) return McpApprovalDecision.Unavailable;
        if (current == McpApprovalDecision.EndOfInput || incoming == McpApprovalDecision.EndOfInput) return McpApprovalDecision.EndOfInput;
        if (current == McpApprovalDecision.Cancelled || incoming == McpApprovalDecision.Cancelled) return McpApprovalDecision.Cancelled;
        return current;
    }

}