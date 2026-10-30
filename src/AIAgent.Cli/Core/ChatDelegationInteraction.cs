using System.Text;

using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private readonly DelegationIdentitySource _delegationIdentities = new();
    private DelegationCoordinator? _delegation;
    private bool _delegationFatal;
    private async Task<bool> HandleAgentsCommandAsync(string raw)
    {
        if (!raw.TrimStart().StartsWith("/agents", StringComparison.OrdinalIgnoreCase)) return false;
        if (!_session!.Policy.CanSendOrUse(raw, ContentBoundary.ModelInput)) { ShowContextBlock("sensitive_content", true); return true; }
        var command = raw.Trim();
        if (raw.Contains('\n') || command is not ("/agents" or "/agents on" or "/agents off")) { ShowContextBlock("invalid_agents_command", true); return true; }
        if (_delegation is null) { ShowContextBlock("delegation_runtime_failed", true); return true; }
        if (command == "/agents off") { _delegation.SetEnabled(false); ShowAgents(); return true; }
        if (command == "/agents on")
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            lock (_gate) { _turnCancellation = cancellation; _cancelTurn = false; _state = ChatState.LoadingRules; }
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            try
            {
                var code = await _delegation.ValidateAvailableAsync(cancellation.Token);
                if (code != "none") { ShowContextBlock(code, true); return true; }
                var profile = ModelCatalog.Find(_session.Settings)!;
                if (!ShowContextBlock("啟用唯讀子 Agent：最多兩子，同模型 " + profile.Provider + "/" + profile.Model + "；目的地：" + profile.Destination +
                    "\n用途限定認證轉接只在供應商傳輸層借用目前認證，不向子任務提供 Key。Explore：list_files、read_file、search_files、search_text、read_file_range；Review：git_status、git_diff、read_git_file_range。" +
                    "\n父子共用生成額度與 API 成本；預設五次通常僅容一子，兩子分派時至少需五次剩餘額度，請檢視 /budget。沒有子寫入、程序、MCP／網頁、核准或讀鍵。" +
                    "\nCtrl+C 取消父回合與整批；清除、換 Key／模型、Scope／來源失效時停用，須明確重新 on。啟用本身不生成或派任務。")) return true;
                cancellation.Token.ThrowIfCancellationRequested();
                var definitions = _registry!.Definitions.Where(d => d.Name != DelegateReadonlyTool.Name).Append(new DelegateReadonlyTool(_delegation, _session.Policy).Definition).ToArray();
                if (_client is ILlmRuntimePolicyValidator validator && !validator.ValidatePolicy(systemInstruction, definitions) ||
                    runtimeFactory.CreateContextInspector(_client!) is ILlmToolCapabilityInspector tools && !tools.CanRepresentTools(definitions))
                { ShowContextBlock("delegation_runtime_failed", true); return true; }
                _delegation.SetEnabled(true);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            finally { StopBusyObserver(); lock (_gate) { _turnCancellation = null; _state = Stopping ? ChatState.Stopping : ChatState.AwaitingInput; } }
        }
        ShowAgents(); return true;
    }
    private void ShowAgents()
    {
        var snapshot = _generationBudget.Snapshot(); var profile = ModelCatalog.Find(_session!.Settings)!;
        var text = "Agents：" + _delegation!.Mode + "；" + profile.Provider + "/" + profile.Model + "；Scope：" + FileChangePreviewBuilder.EscapePath(_rules!.Capture().Scope) +
            "；最多兩子；共同剩餘：" + Math.Min(snapshot.TurnRemaining, snapshot.ProgramRemaining) + "。\n";
        if (_delegation.LastResult is { } batch)
        {
            text += batch.Data is { } data ? data.GetRawText() : batch.ErrorCode;
            foreach (var child in batch.Children) text += "\n" + child.AgentId + "；生成開始：" + child.GenerationsStarted + "；傳輸：" + (child.Usage?.DispatchedRequests.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "不可得") + "；估算 bytes：" + (child.Measure?.EstimatedJsonBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "不可得") + "；實際 bytes：" + (child.BodyBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "不可得");
        }
        else text += "最近批次：無資料。";
        if (Encoding.UTF8.GetByteCount(text) > 65536) { ShowContextBlock("result_too_large", true); return; }
        ShowContextBlock(text);
    }
    private void HandleDelegationEvent(DelegationEvent value)
    {
        if (!ShowContextBlock("[" + value.Sequence + "] " + value.BatchId + "/" + value.AgentId + " " + value.Role + " " + value.Kind + " " + value.Count))
            throw new DelegationFailure("delegation_cleanup_failed", fatal: true);
    }
}