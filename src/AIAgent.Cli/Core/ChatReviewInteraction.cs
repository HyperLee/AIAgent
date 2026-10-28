using AIAgent.Cli.Git;

namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private GitSnapshotCoordinator? _git;
    private CodeReviewCoordinator? _reviews;
    private long _gitSequence;

    private async Task<bool> HandleReviewCommandAsync(string raw)
    {
        var command = raw.Trim();
        if (!command.StartsWith("/review", StringComparison.OrdinalIgnoreCase)) return false;
        if (!_session!.Policy.CanSendOrUse(raw, ContentBoundary.ModelInput))
        { ShowContextBlock("本機輸入含當前 Key；本次已拒絕。", true); return true; }
        GitSelection selection;
        switch (command)
        {
            case "/review": case "/review all": selection = GitSelection.All; break;
            case "/review staged": selection = GitSelection.Staged; break;
            case "/review unstaged": selection = GitSelection.Unstaged; break;
            case "/review untracked": selection = GitSelection.Untracked; break;
            case "/review show": await ShowReviewAsync(); return true;
            default: ShowContextBlock("審查指令無效；請檢視 /help。", true); return true;
        }
        if (!await CheckRulesReadyAsync() || !await CheckSkillsReadyAsync()) return true;
        if (await RunTurnAsync(LlmMessage.FromUserText(command), reviewSelection: selection) && !Stopping) await ReplaceKeyAsync();
        return true;
    }

    private async Task ShowReviewAsync()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate) { if (_stopReason != StopReason.None) return; _turnCancellation = cancellation; _cancelTurn = false; _state = ChatState.CapturingReview; }
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            var display = _reviews is null ? "尚無成功審查報告。" : await _reviews.ShowAsync(_rules!.Capture().Scope, cancellation.Token);
            if (!cancellation.IsCancellationRequested) ShowContextBlock(display ?? "無法安全完整顯示報告。", display is null);
        }
        catch (GitFailure failure) { await HandleReviewLocalFailureAsync(failure.Code); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { if (!Stopping) ShowContextBlock("審查顯示已取消。"); }
        finally { StopBusyObserver(); lock (_gate) { _turnCancellation = null; _state = Stopping ? ChatState.Stopping : ChatState.AwaitingInput; } }
    }

    private async Task HandleReviewLocalFailureAsync(string error)
    {
        if (_skills?.RequiresContextReset == true) error = "sensitive_content";
        var skillsFatal = _skills?.FatalFailure == true;
        _reviews?.Failed(error);
        if (error is "process_unconfirmed" or "process_cleanup_failed")
        { _processFatalStop = true; _executionPolicy!.StopProcesses(); RequestStop(StopReason.Failure); }
        if (error == "sensitive_content") await ClearConversationStateAsync();
        if (skillsFatal) RequestStop(StopReason.Failure);
        if (error == "workspace_unavailable") _reviews?.Clear();
        ShowContextBlock("Git 審查：" + error + "；本次未發布新報告。", true);
    }
}