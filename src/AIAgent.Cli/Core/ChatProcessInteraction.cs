using AIAgent.Cli.Processes;

namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private async ValueTask<ProcessApprovalDecision> ConfirmProcessAsync(ProcessApprovalPreview preview, CancellationToken token)
    {
        var decision = ProcessApprovalDecision.Unavailable;
        try
        {
            lock (_gate)
            {
                if (_turnCancellation is null || _session is null || _stopReason != StopReason.None || _cancelTurn || token.IsCancellationRequested)
                    return ProcessApprovalDecision.Cancelled;
                _state = ChatState.AwaitingProcessApproval;
            }
            StopBusyObserver();
            token.ThrowIfCancellationRequested();
            decision = await interaction.ConfirmProcessAsync(preview, token);
            if (!Enum.IsDefined(decision)) decision = ProcessApprovalDecision.Unavailable;
            if (decision == ProcessApprovalDecision.EndOfInput) HandleSignal(ChatControlSignal.EndOfInput);
            else if (decision == ProcessApprovalDecision.Cancelled) HandleSignal(ChatControlSignal.CancelTurn);
            else if (decision == ProcessApprovalDecision.Unavailable) HandleSignal(ChatControlSignal.Unavailable);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { decision = ProcessApprovalDecision.Cancelled; }
        catch (Exception) { RequestStop(StopReason.Failure); decision = ProcessApprovalDecision.Unavailable; }
        finally
        {
            try
            {
                if (!Stopping) _busyObserver = interaction.ObserveBusyInput(HandleSignal);
                lock (_gate)
                {
                    _state = _stopReason != StopReason.None ? ChatState.Stopping : _cancelTurn ? ChatState.Cancelling : ChatState.ExecutingTools;
                }
            }
            catch (Exception) { RequestStop(StopReason.Failure); decision = ProcessApprovalDecision.Unavailable; }
        }
        return decision;
    }

}