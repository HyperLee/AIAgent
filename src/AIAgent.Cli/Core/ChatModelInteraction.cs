using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication
{
    private ModelSessionCoordinator? _models;
    private WorkWorkspaceIdentity? _modelRoot;
    private CancellationTokenSource? _modelApprovalCancellation;
    private bool _switchingModel;
    private bool _modelSwitchBlocked;
    private async Task<bool> HandleModelCommandAsync(string raw)
    {
        if (!raw.TrimStart().StartsWith("/model", StringComparison.OrdinalIgnoreCase)) return false;
        if (!_session!.Policy.CanSendOrUse(raw, ContentBoundary.ModelInput))
        { ShowContextBlock("invalid_model_command", true); return true; }
        var command = ModelCommands.Parse(raw);
        var current = ModelCatalog.Find(_session.Settings);
        switch (command.Kind)
        {
            case ModelCommandKind.Show:
                ShowContextBlock(current is null ? "model_capability_unavailable" : ModelCommands.Describe(current) +
                    "\n代次：" + (_models?.Generation ?? 0) + "\nKey 僅本機隱藏輸入；服務授權尚須正常請求確認。"); break;
            case ModelCommandKind.List:
                ShowContextBlock("產品支援清單；不代表帳戶授權或配額。\n" + string.Join("\n", ModelCatalog.Profiles.Select(ModelCommands.Describe))); break;
            case ModelCommandKind.Use:
                if (current == command.Profile) ShowContextBlock("already_selected：目前已選擇此模型。");
                else await SwitchModelAsync(command.Profile!);
                break;
            default: ShowContextBlock(command.ErrorCode, true); break;
        }
        return true;
    }

    private async Task SwitchModelAsync(ModelProfile profile)
    {
        if (_models is null || _modelRoot is null || _contexts is null || _rules is null || _workspace?.WorkReferences is null)
        { ShowContextBlock("model_capability_unavailable", true); return; }
        if (_modelSwitchBlocked || _processFatalStop || _executionPolicy!.ProcessSessionStopped || _workStore?.Stopped == true || _mcp?.FatalFailure == true || _skills?.FatalFailure == true)
        { RequestStop(StopReason.Failure); return; }
        if (State != ChatState.AwaitingInput || _plans!.HasReservations) return;
        var stamp = _contexts.Capture();
        var oldProfile = _models.Profile;
        var preview = new ModelApprovalPreview("[模型切換預覽]\n目前：\n" + ModelCommands.Describe(oldProfile) + "\n切換至：\n" + ModelCommands.Describe(profile) +
            "\n目前 Scope：" + FileChangePreviewBuilder.EscapePath(_rules.Capture().Scope) +
            "\n將清除：對話、摘要、pins；Plan、任務、完成證據與所有核准；讀取與 Git 快照、審查報告；檢查點、anchor 與復原歷史；技能啟用；MCP 連線、目錄及 Permit；未發布工作候選；草稿與輸入歷史。\n" +
            "不復原已寫檔案或 Git index，不刪私人存檔。檢查點丟棄後無法由本程序復原。程序消耗與預算設定保留；即使同供應商也須重新隱藏輸入 Key。成功後回根 Scope 與 Plan，等待新指示。\n" +
            "需要保存或復原時，請拒絕後自行處理。完整預覽後另輸入 y／yes 才同意；其他輸入拒絕。確認切換？[y/N] ");
        if (!ContextDisplay.Safe(preview.DisplayBlock, _session!.Policy)) { ShowContextBlock("model_capability_unavailable", true); return; }
        using var approvalCancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        ModelApprovalDecision decision;
        lock (_gate) { _state = ChatState.AwaitingModelApproval; _modelApprovalCancellation = approvalCancellation; }
        try { decision = await interaction.ConfirmModelAsync(preview, approvalCancellation.Token); }
        catch (OperationCanceledException) when (approvalCancellation.IsCancellationRequested) { decision = ModelApprovalDecision.Cancelled; }
        finally { lock (_gate) { _modelApprovalCancellation = null; if (!Stopping) _state = ChatState.AwaitingInput; } }
        if (Stopping) return;
        if (decision != ModelApprovalDecision.Approved || approvalCancellation.IsCancellationRequested)
        {
            if (decision == ModelApprovalDecision.EndOfInput) RequestStop(StopReason.Normal);
            else if (decision == ModelApprovalDecision.Unavailable || !Enum.IsDefined(decision)) RequestStop(StopReason.Failure);
            return;
        }
        var sourceCurrent = false;
        WorkWorkspaceIdentity before;
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            var rulesCode = await _rules.ValidateAsync(_stop.Token);
            var skillsCode = rulesCode == "none" && _skills is not null ? await _skills.ValidateAsync(_stop.Token) : "none";
            sourceCurrent = rulesCode == "none" && skillsCode == "none";
            before = await _workspace.WorkReferences.CaptureWorkRootAsync(_stop.Token);
        }
        finally { StopBusyObserver(); }
        if (Stopping) return;
        bool conflict;
        lock (_gate) conflict = !sourceCurrent || !_contexts.IsCurrent(stamp) || _models.Profile != oldProfile || !SameModelRoot(before);
        if (conflict) { ShowContextBlock("model_revision_conflict", true); return; }
        if (!interaction.PrepareModelKeyInput()) { RequestStop(StopReason.Failure); return; }
        lock (_gate)
        {
            if (!_contexts.IsCurrent(stamp) || Stopping) return;
            _models.BeginSwitch(); _switchingModel = true; _state = ChatState.SwitchingModel;
        }
        BootstrapSession? replacement = null;
        ILlmClient? client = null;
        IWorkspaceFiles? workspace = null;
        var confirmedWorkspace = _session.Workspace;
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            await ClearConversationStateAsync();
            _work = null;
            _rules.DetachPolicy(); _skills?.DetachPolicy(); _mcp?.DetachPolicy();
            _plans.BindPolicy(UnavailableSensitivePolicy.Instance);
            _contexts.BindPolicy(UnavailableSensitivePolicy.Instance);
            await DisposeWorkspaceAsync();
            await DisposeClientAsync();
            interaction.DetachPolicy();
            _session.Dispose(); _session = null;
            StopBusyObserver();
            if (Stopping) return;
            SetState(ChatState.ReadingModelKey);
            using var input = await interaction.ReadKeyAsync(_stop.Token);
            if (Stopping) return;
            if (input.Status != KeyInputStatus.Accepted)
            {
                RequestStop(input.Status switch { KeyInputStatus.EndOfInput => StopReason.Normal, KeyInputStatus.Cancelled => StopReason.Cancelled, _ => StopReason.Failure });
                return;
            }
            replacement = runtimeFactory.CreateReplacementSession(confirmedWorkspace, profile.Settings, input);
            replacement.ModelSession = _models.Capture();
            _plans.BindPolicy(replacement.Policy);
            _contexts.BindPolicy(replacement.Policy);
            _executionPolicy = new(_plans) { Rules = _rules, Skills = _skills, Mcp = _mcp };
            _mcp?.BindPolicy(_executionPolicy, replacement.Policy, AgentRunner.CreateMcpGuard(replacement.Policy, systemInstruction));
            client = runtimeFactory.CreateClient(replacement);
            var inspector = runtimeFactory.CreateContextInspector(client);
            if (inspector is null) throw new InvalidOperationException("模型能力不可用");
            workspace = workspaceFilesFactory.Create(replacement, _executionPolicy);
            if (workspace.WorkReferences is null || !SameModelRoot(await workspace.WorkReferences.CaptureWorkRootAsync(_stop.Token)) || workspace.ProjectRulesWorkspace is not { } reader)
                throw new InvalidOperationException("工作區無法重新驗證");
            _rules.BindPolicy(replacement.Policy, reader, resetScope: true);
            _skills?.BindPolicy(replacement.Policy, workspace.SkillsWorkspace, _executionPolicy);
            interaction.SetPolicy(replacement.Policy);
            SetState(ChatState.LoadingRules);
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            _stop.Token.ThrowIfCancellationRequested();
            var rulesCode = await _rules.EnsureInitializedAsync(_stop.Token);
            if (rulesCode == "io_failure") throw new InvalidOperationException("根規則不可用");
            _stop.Token.ThrowIfCancellationRequested();
            if (rulesCode != "none") throw new InvalidOperationException("根規則不可用");
            var agent = CreateAgent(replacement, client, workspace);
            RequireSafeModelRuntime(replacement, client, agent.Registry);
            StopBusyObserver();
            lock (_gate)
            {
                if (Stopping) return;
                _plans.SetMode(AgentMode.Plan);
                _session = replacement; _client = client; _workspace = workspace; _registry = agent.Registry; _runner = agent.Runner;
                replacement = null; client = null; workspace = null;
                _models.Publish(profile); _switchingModel = false; _state = ChatState.AwaitingInput;
            }
            ShowContextBlock("模型已切換；目前 Provider：" + profile.Provider + "；Model：" + profile.Model + "。已回到 Plan，請輸入新的指示。");
        }
        finally
        {
            var cleanupFailed = false;
            try { StopBusyObserver(); } catch (Exception) { cleanupFailed = true; }
            if (replacement is not null)
            {
                try
                {
                    _rules.DetachPolicy(); _skills?.DetachPolicy(); _mcp?.DetachPolicy();
                    _plans.Clear(); _plans.BindPolicy(UnavailableSensitivePolicy.Instance);
                    _contexts.BindPolicy(UnavailableSensitivePolicy.Instance);
                }
                catch (Exception) { cleanupFailed = true; }
            }
            try { if (workspace is not null) await workspace.DisposeAsync(); } catch (Exception) { cleanupFailed = true; }
            try { if (client is not null) await client.DisposeAsync(); } catch (Exception) { cleanupFailed = true; }
            try { replacement?.Dispose(); } catch (Exception) { cleanupFailed = true; }
            if (cleanupFailed) RequestStop(StopReason.Failure);
        }
    }

    private bool SameModelRoot(WorkWorkspaceIdentity root) => _modelRoot is { } original &&
        root.Root == original.Root && root.Device == original.Device && root.Identity != 0 && root.Identity == original.Identity;

    private void RequireSafeModelRuntime(BootstrapSession session, ILlmClient client, Tools.ToolRegistry registry)
    {
        var profile = ModelCatalog.Find(session.Settings) ?? throw new InvalidOperationException("模型能力不可用");
        var definitions = registry.AllDefinitions;
        var text = systemInstruction + ModelCommands.Describe(profile) + "\nKey 僅本機隱藏輸入；服務授權尚須正常請求確認。";
        if (!session.Policy.CanSendOrUse(text, ContentBoundary.ModelInput) ||
            definitions.Any(t => !session.Policy.CanSendOrUse(t.Name + t.Description, ContentBoundary.ToolArguments) || SensitiveJson.Contains(t.Parameters, session.Policy, ContentBoundary.ToolArguments)) ||
            client is ILlmRuntimePolicyValidator validator && !validator.ValidatePolicy(systemInstruction, definitions))
            throw new InvalidOperationException("模型能力無法安全建立");
    }

    private static async Task<WorkWorkspaceIdentity?> CaptureModelRootAsync(IWorkspaceFiles workspace, CancellationToken token)
    {
        try { return workspace.WorkReferences is { } roots ? await roots.CaptureWorkRootAsync(token) : null; }
        catch (WorkDocumentException) { return null; }
    }
}