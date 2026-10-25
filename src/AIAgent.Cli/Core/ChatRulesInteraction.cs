namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private ProjectRulesCoordinator? _rules;

    private async Task<bool> HandleRulesCommandAsync(string raw)
    {
        var name = raw.TrimStart();
        if (!name.StartsWith("/rules", StringComparison.OrdinalIgnoreCase)) return false;
        if (!_session!.Policy.CanSendOrUse(raw, ContentBoundary.ModelInput))
        { ShowContextBlock("本機輸入含當前 Key；本次已拒絕。", true); return true; }
        var trimmed = raw.Trim();
        if (trimmed is "/rules" or "/rules show")
        {
            var display = _rules?.Display(trimmed == "/rules show");
            ShowContextBlock(display ?? "無法安全完整顯示規則。", display is null); return true;
        }
        if (_rules is null) { ShowContextBlock("rules_unavailable", true); return true; }
        if (trimmed == "/rules reload")
        { await RunRulesOperationAsync(token => _rules.TryReloadAsync(token)); return true; }
        const string prefix = "/rules scope ";
        if (name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
        { await RunRulesOperationAsync(token => _rules.TryChangeScopeAsync(name[prefix.Length..], token)); return true; }
        ShowContextBlock("規則指令無效；請檢視 /help。", true); return true;
    }

    private async Task RunRulesOperationAsync(Func<CancellationToken, Task<string>> operation, bool showResult = true)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate)
        {
            if (_stopReason != StopReason.None) return;
            _turnCancellation = cancellation; _cancelTurn = false; _state = ChatState.LoadingRules;
        }
        try
        {
            ShowContextBlock("正在讀取專案規則。");
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            var result = await operation(cancellation.Token);
            if (result == "io_failure") RequestStop(StopReason.Failure);
            if (showResult && !Stopping) ShowContextBlock("規則操作：" + result + "；等待新指示。", result is not ("none" or "no_change"));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (!Stopping) ShowContextBlock("規則操作已取消；等待新指示。"); }
        finally
        {
            StopBusyObserver();
            lock (_gate)
            {
                _turnCancellation = null;
                _state = _stopReason == StopReason.None ? ChatState.AwaitingInput : ChatState.Stopping;
            }
        }
    }

    private async Task<bool> CheckRulesReadyAsync(CancellationToken? cancellation = null)
    {
        string? error = null;
        if (State == ChatState.AwaitingInput)
        {
            await RunRulesOperationAsync(async token => error = _rules is null ? "rules_unavailable" : await _rules.ValidateAsync(token), false);
            if (error is null || Stopping) return false;
        }
        else error = _rules is null ? "rules_unavailable" : await _rules.ValidateAsync(cancellation ?? _stop.Token);
        if (error == "none") return true;
        ShowContextBlock(error + "：請使用 /rules 檢視並明確重載；後續需重新確認計畫與新指示。", true);
        return false;
    }
}