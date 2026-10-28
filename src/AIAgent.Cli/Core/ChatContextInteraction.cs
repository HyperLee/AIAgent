namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private ContextCoordinator? _contexts;
    private readonly GenerationBudget _generationBudget = new(context.DecisionGate);
    internal LastMaintenanceOutcome? LastMaintenance { get; private set; }
    private async Task<bool> HandleContextCommandAsync(string raw)
    {
        var name = raw.TrimStart();
        if (!name.StartsWith("/context", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("/compact", StringComparison.OrdinalIgnoreCase) &&
            !name.StartsWith("/budget", StringComparison.OrdinalIgnoreCase)) return false;
        if (!_session!.Policy.CanSendOrUse(raw, ContentBoundary.ModelInput))
        { ShowContextBlock("本機輸入含當前 Key；本次已拒絕。", true); return true; }
        var command = ContextCommands.Parse(raw); string? error = null;
        switch (command.Kind)
        {
            case ContextCommandKind.Context: ShowContextBlock(ContextDisplay.Context(_contexts!, false)); break;
            case ContextCommandKind.Show: ShowContextBlock(ContextDisplay.Context(_contexts!, true)); break;
            case ContextCommandKind.Pin: error = _contexts!.AddPin(command.Text!); break;
            case ContextCommandKind.Unpin: error = _contexts!.RemovePin(command.Text!); break;
            case ContextCommandKind.Budget: ShowContextBlock(ContextDisplay.Budget(_generationBudget, LastOutcome, LastMaintenance)); break;
            case ContextCommandKind.TurnLimit: error = _generationBudget.SetTurnLimit(command.Number) ? "none" : "invalid_budget"; break;
            case ContextCommandKind.ProgramLimit: error = _generationBudget.SetProgramLimit(command.Number) ? "none" : "invalid_budget"; break;
            case ContextCommandKind.Reset: _generationBudget.RestoreDefaults(); error = "none"; break;
            case ContextCommandKind.Compact:
                if (!await CheckRulesReadyAsync() || !await CheckSkillsReadyAsync() || !CheckMcpReady()) break;
                if (await RunTurnAsync(null, true) && !Stopping) await ReplaceKeyAsync(); break;
            default: error = name.StartsWith("/budget", StringComparison.Ordinal) ? "invalid_budget" : "invalid_context_command"; break;
        }
        if (error is not null) ShowContextBlock(error == "none" ? "本機設定已接受；等待下一則指示。" : "本機指令未完成：" + error, error != "none");
        return true;
    }
    private void ShowContextBlock(string text, bool rejected = false)
    {
        if (!ContextDisplay.Safe(text + "\n", _session!.Policy))
        { interaction.Show(new(ChatEventKind.InputRejected, text: "無法安全完整顯示本機資訊。")); return; }
        interaction.Show(new(rejected ? ChatEventKind.InputRejected : ChatEventKind.Ready, text: text));
    }
}