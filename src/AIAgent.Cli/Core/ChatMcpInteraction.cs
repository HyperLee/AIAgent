namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private McpCoordinator? _mcp;
    private async Task<bool> HandleMcpCommandAsync(string raw)
    {
        if (!raw.TrimStart().StartsWith("/mcp", StringComparison.OrdinalIgnoreCase)) return false;
        var command = raw.Trim();
        if (command is not ("/mcp" or "/mcp tools" or "/mcp connect learn" or "/mcp reload" or "/mcp disconnect") || raw.Contains('\n'))
        { ShowContextBlock("invalid_mcp_command", true); return true; }
        if (!_session!.Policy.CanSendOrUse(raw, ContentBoundary.ModelInput)) { ShowContextBlock("sensitive_content", true); return true; }
        if (_mcp is null) { ShowContextBlock("mcp_not_connected", true); return true; }
        try
        {
            if (command is "/mcp" or "/mcp tools") { ShowContextBlock(_mcp.Display(command == "/mcp tools")); return true; }
            if (command == "/mcp connect learn")
                ShowContextBlock("目的地：https://learn.microsoft.com/api/mcp。公開免認證搜尋與文件擷取；本次只授權最小服務發現 metadata，查詢仍會外送且每次另需完整預覽及新確認。");
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            lock (_gate) { _turnCancellation = cancellation; _cancelTurn = false; _cancellationNotice = false; _state = ChatState.ExecutingTools; }
            try
            {
                _busyObserver = interaction.ObserveBusyInput(HandleSignal);
                var result = command switch
                {
                    "/mcp connect learn" => await _mcp.ConnectAsync(cancellation.Token),
                    "/mcp reload" => await _mcp.ReloadAsync(cancellation.Token),
                    _ => await _mcp.DisconnectAsync(cancellation.Token)
                };
                if (_mcp.RequiresContextReset) await ClearConversationStateAsync();
                if (_mcp.FatalFailure) RequestStop(StopReason.Failure);
                if (!Stopping) ShowContextBlock((result.Display ?? "") + "\nMCP：" + result.ErrorCode + "；等待新指示。", result.ErrorCode is not ("none" or "no_change"));
            }
            finally { StopBusyObserver(); lock (_gate) { _turnCancellation = null; _state = Stopping ? ChatState.Stopping : ChatState.AwaitingInput; } }
        }
        catch (McpFailure error) { if (error.Code == "sensitive_content") await ClearConversationStateAsync(); ShowContextBlock(error.Code, true); }
        return true;
    }
    private async ValueTask<McpApprovalDecision> ConfirmMcpAsync(McpApprovalPreview preview, CancellationToken token)
    {
        try
        {
            lock (_gate)
            {
                if (_turnCancellation is null || Stopping || _cancelTurn || token.IsCancellationRequested) return McpApprovalDecision.Cancelled;
                _state = ChatState.AwaitingMcpApproval;
            }
            StopBusyObserver();
            var decision = await interaction.ConfirmMcpAsync(preview, token);
            if (decision == McpApprovalDecision.EndOfInput) HandleSignal(ChatControlSignal.EndOfInput);
            else if (decision == McpApprovalDecision.Cancelled) HandleSignal(ChatControlSignal.CancelTurn);
            else if (!Enum.IsDefined(decision) || decision == McpApprovalDecision.Unavailable) { HandleSignal(ChatControlSignal.Unavailable); return McpApprovalDecision.Unavailable; }
            return decision;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return McpApprovalDecision.Cancelled; }
        catch { RequestStop(StopReason.Failure); return McpApprovalDecision.Unavailable; }
        finally
        {
            try
            {
                if (!Stopping) _busyObserver = interaction.ObserveBusyInput(HandleSignal);
                lock (_gate) _state = Stopping ? ChatState.Stopping : _cancelTurn ? ChatState.Cancelling : ChatState.ExecutingTools;
            }
            catch { RequestStop(StopReason.Failure); }
        }
    }
    private bool CheckMcpReady()
    {
        if (_mcp?.StatusCode is null or "none") return true;
        ShowContextBlock(_mcp.StatusCode + "：請明確 /mcp reload 或 /mcp disconnect，之後重新確認並給新指示。", true); return false;
    }
}