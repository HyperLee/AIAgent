using System.Text;

using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private WorkSessionCoordinator? _work;
    private IWorkSaveStore? _workStore;
    internal WorkOperationResult? LastWork { get; private set; }

    private WorkSessionCoordinator WorkCoordinator()
    {
        _workStore ??= (workSaveStoreFactory ?? (() => new MacWorkSaveStore()))();
        return _work ??= new(_contexts!, _plans!, _rules ?? throw new WorkDocumentException("rules_unavailable"),
            _workspace!.WorkReferences ?? new UnavailableWorkReferences(), _workStore, _session!.Policy, _runner,
            _client is null ? null : runtimeFactory.CreateContextInspector(_client), _generationBudget, modelProfile: ModelCatalog.Find(_session.Settings));
    }

    private async Task<bool> HandleWorkCommandAsync(string raw)
    {
        if (!raw.TrimStart().StartsWith("/work", StringComparison.OrdinalIgnoreCase)) return false;
        if (!new WorkPersistencePolicy(_session!.Policy).IsSafe(raw))
        { ShowContextBlock("工作指令未完成：work_sensitive_content", true); return true; }
        var command = WorkCommands.Parse(raw);
        if (command.Kind == WorkCommandKind.Invalid) { ShowContextBlock(command.ErrorCode, true); return true; }
        await RunWorkOperationAsync((work, token) => command.Kind switch
        {
            WorkCommandKind.Save => work.SaveAsync(command.Value!, ConfirmWorkAsync, token),
            WorkCommandKind.List => work.ListAsync(token),
            WorkCommandKind.Show => work.ShowAsync(command.Value!, token),
            WorkCommandKind.Delete => work.DeleteAsync(command.Value!, ConfirmWorkAsync, token),
            _ => Task.FromResult(new WorkOperationResult("invalid_work_command"))
        });
        if (LastWork?.ModelErrorCode == LlmErrorCode.AuthenticationFailed && !Stopping)
        { SetState(ChatState.AwaitingKeyChoice); await DisposeClientAsync(); if (!Stopping) await ReplaceKeyAsync(); }
        return true;
    }

    private async Task<bool> RunWorkOperationAsync(Func<WorkSessionCoordinator, CancellationToken, Task<WorkOperationResult>> operation, bool startup = false)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate)
        {
            if (_stopReason != StopReason.None) return false;
            _turnCancellation = cancellation; _cancelTurn = false; _cancellationNotice = false; _state = ChatState.Working;
        }
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            var result = await operation(WorkCoordinator(), cancellation.Token);
            LastWork = result;
            ShowWorkResult(result);
            if (result.RequiresContextReset || result.ModelErrorCode == LlmErrorCode.AuthenticationFailed)
            {
                await ClearConversationStateAsync(); LastWork = result;
            }
            if (result.Fatal || result.Decision == WorkApprovalDecision.Unavailable || _processFatalStop) RequestStop(StopReason.Failure);
            else if (result.Decision == WorkApprovalDecision.EndOfInput) RequestStop(StopReason.Normal);
            else if (startup && result.ErrorCode != "none" && !Stopping)
                RequestStop(result.Decision == WorkApprovalDecision.Declined ? StopReason.Normal :
                    result.Decision == WorkApprovalDecision.Cancelled || cancellation.IsCancellationRequested ? StopReason.Cancelled : StopReason.Failure);
            return result.ErrorCode == "none" && !Stopping;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (startup && !Stopping) RequestStop(StopReason.Cancelled); return false; }
        catch (Exception) { RequestStop(StopReason.Failure); return false; }
        finally
        {
            StopBusyObserver();
            lock (_gate) { _turnCancellation = null; _state = _stopReason == StopReason.None ? ChatState.AwaitingInput : ChatState.Stopping; }
        }
    }

    private void ShowWorkResult(WorkOperationResult result)
    {
        var text = result.DisplayBlock ?? "[工作操作] " + result.ErrorCode + "；" + result.PublicationState +
            (result.DeleteState == WorkDeleteState.None ? "" : "；" + result.DeleteState) +
            (result.WorkId is null ? "" : "\nID：" + result.WorkId) + (result.TemporaryName is null ? "" : "\n自有暫存：" + result.TemporaryName) +
            (result.StoreStopped ? "\n本程序儲存操作已停用，須人工檢查所列單檔。" : "");
        text += "\n本次保存摘要生成：" + result.GenerationsStarted + "；傳輸嘗試：" + (result.Usage?.DispatchedRequests ?? 0) +
            "；API Total：" + (result.Usage is { } usageValue ? (usageValue.Total.HasKnownValue ? usageValue.Total.KnownTotal.ToString(System.Globalization.CultureInfo.InvariantCulture) : "不可得") + "（缺值次數 " + usageValue.Total.MissingRequestCount + "）" : "不可得") + "。\n";
        if (new UTF8Encoding(false, true).GetByteCount(text) > 65536 || !new WorkPersistencePolicy(_session!.Policy).IsSafe(text))
        { interaction.Show(new(ChatEventKind.InputRejected, text: "無法安全完整顯示工作操作結果。")); return; }
        interaction.Show(new(result.ErrorCode == "none" ? ChatEventKind.Ready : ChatEventKind.InputRejected, text: text));
        if (result.GenerationsStarted > 0 && result.Usage is { } summary)
            interaction.Show(new(ChatEventKind.Usage, sessionUsage: usage.Snapshot(), generationsStarted: result.GenerationsStarted, turnSummary: summary,
                purpose: GenerationPurpose.WorkSaveSummary, profile: ModelCatalog.Find(_session.Settings), profileUsage: usage.ProfileSnapshots(),
                programGenerationsStarted: _generationBudget.Snapshot().ProgramGenerationsStarted));
    }

    private async ValueTask<WorkApprovalDecision> ConfirmWorkAsync(WorkApprovalPreview preview, CancellationToken token)
    {
        StopBusyObserver();
        SetState(ChatState.AwaitingWorkApproval);
        try { return await interaction.ConfirmWorkAsync(preview, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return WorkApprovalDecision.Cancelled; }
        catch (Exception) { return WorkApprovalDecision.Unavailable; }
        finally
        {
            if (!Stopping) _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            SetState(Stopping ? ChatState.Stopping : ChatState.Working);
        }
    }

    private sealed class UnavailableWorkReferences : IWorkReferenceWorkspace
    {
        public Task<WorkWorkspaceIdentity> CaptureWorkRootAsync(CancellationToken token) => throw new WorkDocumentException("store_unavailable");
        public Task<WorkReferenceSet> ReadWorkReferencesAsync(IReadOnlyList<string> paths, CancellationToken token) => throw new WorkDocumentException("store_unavailable");
    }
}