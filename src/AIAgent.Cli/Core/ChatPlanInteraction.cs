namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private void ShowTasks()
    {
        var block = _plans!.Display();
        if (block is null) interaction.Show(new(ChatEventKind.InputRejected, text: "計畫無法完整安全顯示；未確認此版本。"));
        else interaction.Show(new(ChatEventKind.Ready, text: block));
    }

    private void ShowTaskSummary()
    {
        if (_plans?.Snapshot is not { } snapshot) return;
        var block = _plans.Display();
        if (block is not null) interaction.Show(new(ChatEventKind.TaskSummary, text: block, planSummary: snapshot));
    }

    private async Task ExecuteModeAsync()
    {
        if (_plans!.Snapshot is not { } plan || plan.ApprovalState == PlanApprovalState.Approved)
        { _plans.SetMode(AgentMode.Execute); ShowTasks(); return; }
        _plans.SetMode(AgentMode.Plan);
        var preview = _plans.ApprovalPreview();
        if (preview is null)
        { interaction.Show(new(ChatEventKind.InputRejected, text: "計畫無法完整安全顯示；未確認此版本。")); return; }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate)
        {
            if (_stopReason != StopReason.None) return;
            _planCancellation = cancellation; _state = ChatState.AwaitingPlanApproval;
        }
        try
        {
            var decision = await interaction.ConfirmPlanAsync(preview, cancellation.Token);
            lock (_gate)
            {
                if (_stopReason != StopReason.None) return;
                if (decision == PlanApprovalDecision.EndOfInput) { RequestStop(StopReason.Normal); return; }
                if (!Enum.IsDefined(decision) || decision == PlanApprovalDecision.Unavailable) { RequestStop(StopReason.Failure); return; }
                if (!cancellation.IsCancellationRequested && decision == PlanApprovalDecision.Approved && _plans.Approve(preview))
                    interaction.Show(new(ChatEventKind.Ready, text: "已確認此版本並切換 Execute；等待下一則訊息，寫入仍須逐次核准。"));
                else
                {
                    _plans.SetMode(AgentMode.Plan);
                    interaction.Show(new(ChatEventKind.Ready, text: "未確認此版本；維持 Plan。"));
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { _plans.SetMode(AgentMode.Plan); }
        catch (Exception) { RequestStop(StopReason.Failure); }
        finally
        {
            lock (_gate)
            {
                _planCancellation = null;
                _state = _stopReason == StopReason.None ? ChatState.AwaitingInput : ChatState.Stopping;
            }
        }
    }
}