using System.Globalization;
using System.Text;

using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private CheckpointApproval? _checkpointApproval;

    private async Task<bool> HandleCheckpointCommandAsync(string raw)
    {
        if (!raw.TrimStart().StartsWith("/checkpoint", StringComparison.OrdinalIgnoreCase)) return false;
        if (!new WorkPersistencePolicy(_session!.Policy).IsSafe(raw))
        { ShowContextBlock("檢查點指令未完成：sensitive_content", true); return true; }
        var command = CheckpointCommands.Parse(raw);
        if (command.Kind == CheckpointCommandKind.Invalid)
        { ShowContextBlock("檢查點指令未完成：" + command.ErrorCode, true); return true; }
        if (_workspace?.Checkpoints is not { } checkpoints)
        { ShowContextBlock("checkpoint_unavailable", true); return true; }
        if (command.Kind == CheckpointCommandKind.Restore)
        {
            if (_plans!.Mode != AgentMode.Execute) { ShowContextBlock("mode_restricted", true); return true; }
            if (!await CheckRulesReadyAsync() || !await CheckSkillsReadyAsync()) return true;
        }
        await RunCheckpointOperationAsync(checkpoints, command);
        return true;
    }

    private async Task RunCheckpointOperationAsync(ICheckpointWorkspace checkpoints, CheckpointCommand command)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate)
        {
            if (_stopReason != StopReason.None || _state != ChatState.AwaitingInput) return;
            _turnCancellation = cancellation; _cancelTurn = false; _cancellationNotice = false; _state = ChatState.CheckpointOperation;
            _checkpointApproval = new(this, _session!, command, cancellation.Token);
        }
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            cancellation.Token.ThrowIfCancellationRequested();
            var result = await (command.Kind switch
            {
                CheckpointCommandKind.List => checkpoints.ListAsync(cancellation.Token),
                CheckpointCommandKind.Show => checkpoints.ShowAsync(command.CheckpointId, cancellation.Token),
                CheckpointCommandKind.Restore => checkpoints.RestoreAsync(command.CheckpointId, _checkpointApproval!, cancellation.Token),
                CheckpointCommandKind.Forget => checkpoints.ForgetAsync(command.CheckpointId, _checkpointApproval!, cancellation.Token),
                _ => throw new InvalidOperationException("檢查點指令無效")
            });
            // Known publication facts are displayed before cancellation, EOF, or rule changes settle.
            await ShowCheckpointResultAsync(result);
            var skillsFatal = _skills?.FatalFailure == true;
            if (result.ErrorCode == "sensitive_content" || _executionPolicy!.Recovery.RequiresContextReset || _skills?.RequiresContextReset == true) await ClearConversationStateAsync();
            if (skillsFatal || result.Fatal || result.CleanupFailed || result.WriteState == FileWriteState.Unconfirmed || result.Decision == FileApprovalDecision.Unavailable)
                RequestStop(StopReason.Failure);
            else if (result.Decision == FileApprovalDecision.EndOfInput) RequestStop(StopReason.Normal);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (!Stopping) ShowContextBlock("檢查點操作已取消；等待新指示。"); }
        catch (Exception) { RequestStop(StopReason.Failure); }
        finally
        {
            lock (_gate) { _checkpointApproval = null; }
            StopBusyObserver();
            lock (_gate) { _turnCancellation = null; _state = _stopReason == StopReason.None ? ChatState.AwaitingInput : ChatState.Stopping; }
        }
    }

    private async Task ShowCheckpointResultAsync(CheckpointOperationResult result)
    {
        var text = result.DisplayBlock ?? "[檢查點操作] " + result.ErrorCode;
        if (result.WriteState != FileWriteState.NotApplied || result.CleanupFailed)
        {
            text += "\n[單檔復原結果] " + result.WriteState +
                (result.CheckpointId is { } id ? "；c" + id.ToString(CultureInfo.InvariantCulture) : "") +
                (result.RelativePath is { } path ? "；" + FileChangePreviewBuilder.EscapePath(path) : "") +
                (result.VerifiedByteCount is { } bytes ? "；" + bytes.ToString(CultureInfo.InvariantCulture) + " bytes" : "") +
                (result.VerifiedHasUtf8Bom is { } bom ? "；UTF-8 BOM：" + (bom ? "有" : "無") : "") +
                (result.CleanupFailed ? "；清理未完成，須人工處理" : "") +
                "\n復原只涵蓋此工作檔；等待新指示，須重新檢查來源、確認計畫及驗證。";
        }
        if (!CheckpointDisplaySafe(text))
        {
            await ClearConversationStateAsync();
            interaction.Show(new(ChatEventKind.InputRejected, text: "無法安全完整顯示檢查點操作結果；已清除記憶體。"));
            return;
        }
        interaction.Show(new(result.ErrorCode == "none" ? ChatEventKind.Ready : ChatEventKind.InputRejected, text: text));
    }

    private bool CheckpointDisplaySafe(string text)
    {
        try { return new UTF8Encoding(false, true).GetByteCount(text) <= 65536 && new WorkPersistencePolicy(_session!.Policy).IsSafe(text); }
        catch (EncoderFallbackException) { return false; }
    }

    private async ValueTask<FileApprovalDecision> ConfirmCheckpointAsync(CheckpointApproval binding, CheckpointApprovalPreview preview, CancellationToken token)
    {
        var decision = FileApprovalDecision.Unavailable;
        var readerStopped = false;
        try
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_checkpointApproval, binding) || !ReferenceEquals(_session, binding.Session) ||
                    _turnCancellation is null || _state != ChatState.CheckpointOperation || _stopReason != StopReason.None || _cancelTurn ||
                    binding.Token.IsCancellationRequested || token.IsCancellationRequested || !binding.TryUse()) return FileApprovalDecision.Unavailable;
                if (preview.CheckpointId != binding.Command.CheckpointId || preview.IsForget != (binding.Command.Kind == CheckpointCommandKind.Forget) ||
                    binding.Command.Kind is not (CheckpointCommandKind.Restore or CheckpointCommandKind.Forget) ||
                    preview.Prompt != (preview.IsForget ? CheckpointCommands.ForgetPrompt : CheckpointCommands.RestorePrompt) ||
                    !preview.DisplayBlock.EndsWith(preview.Prompt, StringComparison.Ordinal) || !CheckpointDisplaySafe(preview.DisplayBlock)) return FileApprovalDecision.Unavailable;
                _state = ChatState.AwaitingCheckpointApproval;
            }
            StopBusyObserver();
            readerStopped = true;
            token.ThrowIfCancellationRequested();
            decision = await interaction.ConfirmCheckpointAsync(preview, token);
            if (!Enum.IsDefined(decision)) decision = FileApprovalDecision.Unavailable;
            if (decision == FileApprovalDecision.EndOfInput) HandleSignal(ChatControlSignal.EndOfInput);
            else if (decision == FileApprovalDecision.Cancelled) HandleSignal(ChatControlSignal.CancelTurn);
            else if (decision == FileApprovalDecision.Unavailable) HandleSignal(ChatControlSignal.Unavailable);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { decision = FileApprovalDecision.Cancelled; }
        catch (Exception) { RequestStop(StopReason.Failure); decision = FileApprovalDecision.Unavailable; }
        finally
        {
            try
            {
                if (readerStopped && !Stopping) _busyObserver = interaction.ObserveBusyInput(HandleSignal);
                if (readerStopped)
                    lock (_gate) _state = _stopReason != StopReason.None ? ChatState.Stopping : _cancelTurn ? ChatState.Cancelling : ChatState.CheckpointOperation;
            }
            catch (Exception) { RequestStop(StopReason.Failure); decision = FileApprovalDecision.Unavailable; }
        }
        return decision;
    }

    private sealed class CheckpointApproval(ChatApplication owner, BootstrapSession session, CheckpointCommand command, CancellationToken token) : ICheckpointApproval
    {
        private int _used;
        internal BootstrapSession Session { get; } = session;
        internal CheckpointCommand Command { get; } = command;
        internal CancellationToken Token { get; } = token;
        internal bool TryUse() => Interlocked.Exchange(ref _used, 1) == 0;
        public ValueTask<FileApprovalDecision> ConfirmAsync(CheckpointApprovalPreview preview, CancellationToken cancellationToken) => owner.ConfirmCheckpointAsync(this, preview, cancellationToken);
        public override string ToString() => "[CheckpointApproval]";
    }
}