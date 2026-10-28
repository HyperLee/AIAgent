namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private SkillsCoordinator? _skills;
    private async Task<bool> HandleSkillsCommandAsync(string raw)
    {
        if (!raw.TrimStart().StartsWith("/skills", StringComparison.OrdinalIgnoreCase)) return false;
        if (!_session!.Policy.CanSendOrUse(raw, ContentBoundary.ModelInput))
        { ShowContextBlock("本機輸入含當前 Key；本次已拒絕。", true); return true; }
        var command = SkillsCommands.Parse(raw);
        if (command.Kind == SkillsCommandKind.Invalid) { ShowContextBlock("invalid_skills_command", true); return true; }
        if (_skills is null) { ShowContextBlock("skills_unavailable", true); return true; }
        if (command.Kind is SkillsCommandKind.Status or SkillsCommandKind.Off)
        {
            try
            {
                if (command.Kind == SkillsCommandKind.Status) ShowContextBlock(_skills.Display() ?? "skills_limit");
                else await ShowSkillsResultAsync(_skills.Disable());
            }
            catch (SkillsSensitiveException) { ShowContextBlock("sensitive_content", true); }
            return true;
        }
        await RunSkillsOperationAsync(token => command.Kind switch
        {
            SkillsCommandKind.List => _skills.ListAsync(token),
            SkillsCommandKind.Show => _skills.ShowAsync(command.Id!, token),
            SkillsCommandKind.Use => _skills.TryUseAsync(command.Id!, token),
            _ => _skills.TryReloadAsync(token)
        });
        return true;
    }
    private async Task ShowSkillsResultAsync(SkillsOperationResult result)
    {
        if (result.RequiresContextReset) await ClearConversationStateAsync();
        if (result.DisplayBlock is { } display) ShowContextBlock(display);
        ShowContextBlock("技能操作：" + result.ErrorCode + "；等待新指示。", result.ErrorCode is not ("none" or "no_change"));
        if (result.ErrorCode == "io_failure" || _skills?.FatalFailure == true) RequestStop(StopReason.Failure);
    }
    private async Task RunSkillsOperationAsync(Func<CancellationToken, Task<SkillsOperationResult>> operation, bool show = true)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate)
        {
            if (Stopping) return;
            _turnCancellation = cancellation; _cancelTurn = false; _cancellationNotice = false; _state = ChatState.LoadingSkills;
        }
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            var result = await operation(cancellation.Token);
            if (show && !Stopping) await ShowSkillsResultAsync(result);
        }
        catch (SkillsSensitiveException) { await ClearConversationStateAsync(); ShowContextBlock("sensitive_content", true); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { if (!Stopping) ShowContextBlock("技能操作已取消；等待新指示。"); }
        catch (Exception) { RequestStop(StopReason.Failure); }
        finally
        {
            StopBusyObserver();
            lock (_gate) { _turnCancellation = null; _state = Stopping ? ChatState.Stopping : ChatState.AwaitingInput; }
        }
    }
    private async Task<bool> CheckSkillsReadyAsync(CancellationToken? token = null)
    {
        if (_skills?.StatusCode == "none" && _skills.Capture().Status == SkillsStatus.Disabled) return true;
        string? error = null;
        if (State == ChatState.AwaitingInput)
        {
            await RunSkillsOperationAsync(async cancellation => new(error = _skills is null ? "skills_unavailable" : await _skills.ValidateAsync(cancellation)), false);
            if (error is null || Stopping) return false;
        }
        else error = _skills is null ? "skills_unavailable" : await _skills.ValidateAsync(token ?? _stop.Token);
        if (error == "none") return true;
        ShowContextBlock(error + "：請使用 /skills 檢視、明確 reload／use 或 off，之後重新確認並給予新指示。", true);
        return false;
    }
}