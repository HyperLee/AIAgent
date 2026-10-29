using AIAgent.Cli.Git;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed partial class ChatApplication(IChatInteraction interaction, IChatRuntimeFactory runtimeFactory,
    ConversationContext context, UsageAccumulator usage, string systemInstruction, IWorkspaceFilesFactory workspaceFilesFactory,
    IControlledDotnetTasksFactory? controlledTasksFactory = null, IGitReadWorkspaceFactory? gitReadWorkspaceFactory = null, Func<IWorkSaveStore>? workSaveStoreFactory = null, Func<IMcpContentGuard, IMcpReadClient>? mcpClientFactory = null)
{
    private readonly object _gate = context.DecisionGate;
    private readonly CancellationTokenSource _stop = new();
    private CancellationTokenSource? _turnCancellation;
    private CancellationTokenSource? _planCancellation;
    private PlanStore? _plans;
    private AgentExecutionPolicy? _executionPolicy;
    private TaskCompletionSource? _turnSignal;
    private BootstrapSession? _session;
    private ILlmClient? _client;
    private IWorkspaceFiles? _workspace;
    private IControlledDotnetTasks? _processes;
    private bool _processFatalStop;
    private ToolRegistry? _registry;
    private AgentRunner? _runner;
    private IDisposable? _busyObserver;
    private ChatState _state = ChatState.AwaitingInput;
    private StopReason _stopReason;
    private bool _cancelTurn;
    private bool _cancellationNotice;
    private bool _finalized;
    private int _started;
    internal ChatState State { get { lock (_gate) return _state; } }
    internal LastTurnOutcome? LastOutcome { get; private set; }
    internal void Interrupt() => HandleSignal(ChatControlSignal.CancelTurn);

    internal async Task<int> RunAsync(BootstrapSession initialSession, CancellationToken applicationCancellation)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) throw new InvalidOperationException("聊天應用僅可執行一次");
        _session = initialSession;
        var policyReady = false;
        var registration = applicationCancellation.Register(() => RequestStop(StopReason.Cancelled));
        try
        {
            if (!Stopping)
            {
                interaction.SetPolicy(initialSession.Policy);
                policyReady = true;
                _models = ModelCatalog.Find(initialSession.Settings) is { } profile ? new(_gate, profile) : null;
                initialSession.ModelSession = _models?.Capture();
                _plans = new(initialSession.Policy, _gate) { ModelSession = _models };
                _contexts = new(context, _plans, initialSession.Policy);
                _executionPolicy = new(_plans);
                _client = runtimeFactory.CreateClient(initialSession);
                _workspace = workspaceFilesFactory.Create(initialSession, _executionPolicy);
                _modelRoot = await CaptureModelRootAsync(_workspace, _stop.Token);
                _rules = _executionPolicy.Rules;
                _skills = _executionPolicy.Skills ??= new(_workspace.SkillsWorkspace, initialSession.Policy, _executionPolicy);
                _mcp = new(_executionPolicy, initialSession.Policy, AgentRunner.CreateMcpGuard(initialSession.Policy, systemInstruction), ConfirmMcpAsync, () => _workspace?.Snapshots.Clear(), mcpClientFactory);
                _plans.Mcp = _mcp;
                _contexts.BindMcp(_mcp);
                _contexts.BindSkills(_skills);
                _contexts.BindRules(_rules);
                if (initialSession.ResumeWorkId is { } resumeId)
                {
                    await RunWorkOperationAsync((work, token) => work.ResumeAsync(resumeId, ConfirmWorkAsync, token), true);
                }
                else if (_rules is not null) await RunRulesOperationAsync(token => _rules.EnsureInitializedAsync(token));
                if (!Stopping)
                {
                    (_registry, _runner) = CreateAgent(initialSession, _client, _workspace);
                    RequireSafeModelRuntime(initialSession, _client, _registry);
                    _work?.BindRunner(_runner, runtimeFactory.CreateContextInspector(_client));
                    interaction.Show(new(ChatEventKind.Ready, text: "聊天已就緒；輸入 /help 查看操作。一般輸入隱藏，F2 可安全預覽。\n" +
                        "本機累計不是正式帳單；傳輸嘗試不證明 供應商已收到或計費。"));
                }
            }
            while (!Stopping)
            {
                SetState(ChatState.AwaitingInput);
                interaction.Show(new(ChatEventKind.Ready, text: _plans!.Mode == AgentMode.Plan ? "[Plan]" : "[Execute]"));
                var budget = _generationBudget.Snapshot();
                if (_generationBudget.TakeNearLimitWarning()) ShowContextBlock("程序生成額度已接近上限；請檢視 /budget。");
                ShowContextBlock("回合生成上限：" + budget.TurnLimit + "；程序剩餘：" + budget.ProgramRemaining + "。請用 /budget 檢視額度。");
                var input = await interaction.ReadInputAsync(_stop.Token);
                if (Stopping) break;
                if (input.Status != ChatInputStatus.Submitted)
                {
                    if (input.Status == ChatInputStatus.Invalid) ShowInvalidInput();
                    else RequestStop(input.Status switch { ChatInputStatus.EndOfInput => StopReason.Normal, ChatInputStatus.Cancelled => StopReason.Cancelled, _ => StopReason.Failure });
                    continue;
                }
                var text = input.Text!;
                if (!ChatInputValidation.IsValid(text)) { ShowInvalidInput(); continue; }
                var command = text.Trim();
                if (command.Length == 0) continue;
                if (text.Contains('\n') && text.TrimStart().StartsWith('/'))
                {
                    interaction.Show(new(ChatEventKind.InputRejected, text: text.TrimStart().StartsWith("/model", StringComparison.OrdinalIgnoreCase) ? "invalid_model_command：本機指令只接受單行；本次未執行。" : text.TrimStart().StartsWith("/mcp", StringComparison.OrdinalIgnoreCase) ? "invalid_mcp_command：本機指令只接受單行；本次未執行。" : "本機指令只接受單行；本次未執行。"));
                    continue;
                }
                if (await HandleModelCommandAsync(text)) continue;
                if (await HandleMcpCommandAsync(text)) continue;
                if (await HandleSkillsCommandAsync(text)) continue;
                if (await HandleCheckpointCommandAsync(text)) continue;
                if (await HandleWorkCommandAsync(text)) continue;
                if (await HandleRulesCommandAsync(text)) continue;
                if (await HandleContextCommandAsync(text)) continue;
                if (await HandleReviewCommandAsync(text)) continue;
                if (command == "/exit") { RequestStop(StopReason.Normal); continue; }
                if (command == "/help")
                {
                    ShowContextBlock("/model：目前 Provider／Model／官方目的地／能力與代次；/model list：產品固定支援清單，零網路；/model use PROVIDER MODEL：完整清除預覽、新確認及隱藏新 Key，切換後進 Plan。切換不復原檔案、不刪存檔、程序消耗保留。\n" +
                        "/mcp、/mcp tools：零網路狀態／目錄；/mcp connect learn：固定公開 Microsoft Learn；/mcp reload：明確重載；/mcp disconnect：停用。每次外送完整預覽及新隱藏確認；Stale 須重載或斷線，clear／換 Key／續接不自動連線。\n" +
                        "/skills：上次觀察狀態；/skills list：目前 Scope 的 skills/<id>/SKILL.md；/skills show ID：完整磁碟檢視；/skills use ID：手動單一選用；/skills reload：明確重載；/skills off：停用。\n" +
                        "技能可能送至目前供應商，只能指導既有能力，不代表執行或核准；檢視不會啟用，來源過時須重載或停用，再重新確認計畫並給新指示。clear／換 Key／退出會丟棄技能，續接不恢復；每筆操作仍逐次核准。\n" +
                        "/checkpoint list：程序內修改歷史；/checkpoint show cN：原始修改差異；/checkpoint restore cN：Execute 下逐筆新確認復原；/checkpoint forget cN：新確認後丟棄單筆記憶體。\n" +
                        "檢查點只在本程序記憶體保留，clear／換 Key／退出會丟棄；容量滿停止新寫入，新建檔須人工核對及刪除。復原不復原 Git index、建置副作用、owner、ACL、xattr 或硬連結關係，仍須重新確認計畫並給新指示。\n" +
                        "/work save TITLE：完整預覽後確認保存安全摘要；/work list、/work show ID：歷史查詢；/work delete ID：新確認後只刪單一存檔。\n" +
                        "存檔是私人目錄中的明文，摘要可能有損；只有保存可能消耗一次摘要生成及 API 用量。--resume ID 重啟時重新確認工作區、輸入 Key 並載入目前規則，續接後進入 Plan，不恢復操作核准或完成證據。clear 只清記憶體，delete 只刪指定存檔。\n" +
                        "/rules、/rules show：完整來源與規則；/rules scope DIR：明確切換目錄範圍；/rules reload：手動重載。\n" +
                        "/review [all|staged|unstaged|untracked]：目前 Scope 的唯讀模型審查；/review show：最近成功報告，零生成。差異可能送至目前供應商，消耗共用生成額度；使用原始 bytes 視角，排除範圍會明示，本回合不執行建置或測試。\n" +
                        "確認工作區及輸入 Key 後自動載入根規則，來源可能送至目前供應商；越過巢狀規則須先切 Scope，來源變動需重載及重新確認。\n" +
                        "/context、/context show：容量與完整記憶；/context pin TEXT、/context unpin ID：原文備註；/compact：整理既有歷史，消耗生成與 API 用量。\n" +
                        "/budget、/budget set turn N（1～20）、/budget set program N（1～1000）、/budget reset（恢復5／100，不清消耗）；/clear 不解除程序總額度。\n" +
                        "未釘選資訊可能於模型摘要中遺失；模式、計畫、證據及逐次核准仍由本機管理。");
                    interaction.Show(new(ChatEventKind.Ready, text: "/help：操作說明；/plan：唯讀探索與規劃；/execute：確認目前版本後切換執行；/tasks：本機任務與完成依據。\n" +
                        "版本確認不直接執行，須等待下一則訊息；每筆寫入與受控 .NET 任務仍逐次核准。分析判斷不等於本機驗證；程序僅支援已驗收 macOS、可信 .csproj 的 Debug 建置；SDK 10.0.100 的 test 因私人 IPC 限制不支援。\n" +
                        "計畫最多 20 任務、1 項在途及 256 筆事實；容量不足時請 /clear 後重新規劃。\n" +
                        "/clear：清除對話、快照、檢查點、復原結果、計畫、證據與核准記憶體，保留模式與累計；不復原已寫檔案；/exit：退出。\n" +
                        "支援 list_files、read_file、create_file、replace_text、search_files、search_text、read_file_range、run_dotnet_task；程序固定離線、標準 bin／obj 與私人暫存，產物保留；每回合兩次／300 秒，失敗即停止。文字搜尋與範圍讀取限 128 KiB UTF-8 文字檔，搜尋固定忽略產物與敏感目錄；每筆寫入先預覽並核准，拒絕即停止回合。內容可能送至目前供應商。空白不送出；EOF／Ctrl+D 正常退出。\n" +
                        "等待輸入時 Ctrl+C 退出；生成、工具或核准中 Ctrl+C 只取消本回合，Ctrl+D 結束。\n" +
                        "草稿上限 16384 個 UTF-16 單位；輸入不回顯，Enter 送出，F2 安全預覽，F3 貼上收集，F4 換行。\n" +
                        "方向鍵移動文字元素／邏輯行，Home／End 移至行首尾；Backspace／Delete 刪前後文字元素，Tab 保留縮排。Ctrl+P／N 回取安全歷史，Ctrl+U 清空草稿。\n" +
                        "多行貼上前先按 F3，等待提示後貼上，完成後再 F3；回到編輯後另按 Enter 送出。不自動辨識貼上或來源；macOS 可能需 Fn＋功能鍵。本機指令只接受單行。\n" +
                        "輸入歷史只在記憶體保留20筆／65536 UTF-16單位；clear、換Key或安全重設會清除，不會恢復舊核准。\n" +
                        "替換保留普通權限位元，不保證 owner、ACL、xattr 或硬連結關係；取消不復原已寫內容。"));
                    continue;
                }
                if (command == "/plan") { _plans!.SetMode(AgentMode.Plan); ShowTasks(); continue; }
                if (command == "/tasks") { ShowTasks(); continue; }
                if (command == "/execute") { await ExecuteModeAsync(); continue; }
                if (command == "/clear")
                {
                    await ClearConversationStateAsync(); LastOutcome = null;
                    if (_rules?.Capture().Status == ProjectRulesStatus.Valid) await CheckRulesReadyAsync();
                    interaction.Show(new(ChatEventKind.ContextCleared, text: "對話已清除。"));
                    continue;
                }
                if (command.StartsWith('/'))
                {
                    interaction.Show(new(ChatEventKind.InputRejected, text: "未知指令；請輸入 /help 查看操作。"));
                    continue;
                }
                if (!_session!.Policy.CanSendOrUse(text, ContentBoundary.ModelInput))
                {
                    interaction.Show(new(ChatEventKind.InputRejected, text: "模型輸入含當前 Key；已阻擋本次傳送。"));
                    continue;
                }
                if (!await CheckRulesReadyAsync() || !await CheckSkillsReadyAsync() || !CheckMcpReady()) continue;
                interaction.RememberAcceptedInput(text);
                interaction.Show(new(ChatEventKind.UserMessage, text: text));
                if (await RunTurnAsync(LlmMessage.FromUserText(text)) && !Stopping) await ReplaceKeyAsync();
            }
        }
        catch (OperationCanceledException) { if (!Stopping) RequestStop(applicationCancellation.IsCancellationRequested ? StopReason.Cancelled : StopReason.Failure); }
        catch (Exception)
        {
            RequestStop(StopReason.Failure);
            if (policyReady && _session is not null) TryShow(new(ChatEventKind.Stopping, text: "本機流程無法完成；已安全停止。"));
        }
        finally
        {
            SetState(ChatState.Stopping);
            if (policyReady && _session is not null) TryShow(new(ChatEventKind.Stopping, text: "聊天已結束。"));
            try { await ClearConversationStateAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { if (_workStore is not null) await _workStore.DisposeAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { if (_mcp is not null) await _mcp.DisposeAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { if (_skills is not null) await _skills.DisposeAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { if (_rules is not null) await _rules.DisposeAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { await DisposeWorkspaceAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { await DisposeClientAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { _plans?.Stop(); } catch (Exception) { RequestStop(StopReason.Failure); }
            _executionPolicy = null;
            try { _session?.Dispose(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            _session = null;
            LastOutcome = null;
            _models?.Stop();
            registration.Dispose();
            lock (_gate) _finalized = true;
            _stop.Dispose();
        }
        return _stopReason switch { StopReason.Failure => 1, StopReason.Cancelled => 130, _ => 0 };
    }

    private async Task<bool> RunTurnAsync(LlmMessage? user, bool maintenance = false, GitSelection? reviewSelection = null)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate)
        {
            if (_stopReason != StopReason.None) return false;
            _turnCancellation = cancellation;
            _turnSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _cancelTurn = false; _cancellationNotice = false;
            _state = ChatState.Generating;
        }
        var keyHandoff = false;
        IDisposable? reviewScope = null;
        CodeReviewCoordinator? review = null;
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            if (reviewSelection is { } selection)
            {
                reviewScope = _executionPolicy!.EnterReview(); review = _reviews!; review.BeginAttempt();
                SetState(ChatState.CapturingReview); ShowContextBlock("正在捕捉目前 Scope 的 Git 原始來源。");
                var captureError = await _git!.CaptureAsync(cancellation.Token);
                if (captureError != "none") { await HandleReviewLocalFailureAsync(captureError); return false; }
                _git.Bind(selection);
                if (_git.EligibleChangeCount == 0)
                {
                    var noChanges = !_git.Current!.Changes.Any(c => GitLimits.Selected(selection, c.Layer)) && _git.Exclusions.Count == 0;
                    review.Failed(noChanges ? "no_changes" : "review_incomplete");
                    ShowContextBlock((noChanges ? "no_changes：選定層在最後重驗時點沒有變更。" : "沒有可審查的文字差異；仍有未檢查範圍。") + "\nScope 限定、安全過濾、原始 bytes 視角；零新增生成。本回合未執行建置或測試。");
                    return false;
                }
            }
            using var approvalBinding = workspaceFilesFactory.ApprovalBridge?.Bind(_session!, ConfirmChangeAsync);
            if (_processFatalStop) { RequestStop(StopReason.Failure); return false; }
            if (!maintenance && _processes is IProcessTurnLifecycle lifecycle) lifecycle.BeginTurn();
            var result = maintenance ? await _runner!.RunMaintenanceAsync(cancellation.Token) :
                await _runner!.RunAsync(context.BuildMessages(user!), cancellation.Token, review);
            var status = result.Status;
            if (result.StopReason == AgentStopReason.WriteUnconfirmed || result.Operations.Any(o => o.WriteState == FileWriteState.Unconfirmed || o.CleanupFailed)) _modelSwitchBlocked = true;
            var reason = result.StopReason;
            var requiresContextReset = result.RequiresContextReset || _mcp?.RequiresContextReset == true;
            var publishAnswer = false;
            if (result.McpStamp is { } mcpStamp && _mcp?.IsCurrent(mcpStamp) != true) { status = AgentTurnStatus.Stopped; reason = AgentStopReason.McpChanged; }
            if (status == AgentTurnStatus.Completed && !requiresContextReset)
            {
                var rulesError = "none";
                try { rulesError = _rules is null ? "rules_unavailable" : await _rules.ValidateAsync(cancellation.Token); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                { status = AgentTurnStatus.Cancelled; reason = AgentStopReason.TurnCancelled; }
                if (status == AgentTurnStatus.Completed && rulesError == "none" && result.RulesStamp is { } stamp && !_rules!.IsCurrent(stamp)) rulesError = "rules_changed";
                if (rulesError != "none") { status = AgentTurnStatus.Stopped; reason = new ProjectRulesFlowException(rulesError).Reason; }
                if (status == AgentTurnStatus.Completed)
                {
                    try
                    {
                        var skillsError = _skills is null ? "skills_unavailable" : await _skills.ValidateAsync(cancellation.Token);
                        if (skillsError == "none" && result.SkillsStamp is { } skillStamp && !_skills!.IsCurrent(skillStamp)) skillsError = "skills_changed";
                        if (skillsError != "none") { status = AgentTurnStatus.Stopped; reason = new SkillsFlowException(skillsError).Reason; }
                    }
                    catch (SkillsSensitiveException) { requiresContextReset = true; status = AgentTurnStatus.Failed; reason = AgentStopReason.SensitiveData; }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { status = AgentTurnStatus.Cancelled; reason = AgentStopReason.TurnCancelled; }
                }
                if (status == AgentTurnStatus.Completed && review is not null)
                {
                    var error = await _git!.ValidateAsync(cancellation.Token);
                    if (error != "none")
                    {
                        review.Failed(error); status = AgentTurnStatus.Stopped;
                        requiresContextReset |= error == "sensitive_content";
                        reason = error == "sensitive_content" ? AgentStopReason.SensitiveData : error is "rules_changed" or "rules_unavailable" or "rules_scope_mismatch" or "rules_limit" ? new ProjectRulesFlowException(error).Reason : error is "process_unconfirmed" or "process_cleanup_failed" ? AgentStopReason.ProcessUnconfirmed : AgentStopReason.GitStopped;
                    }
                }
            }
            if (requiresContextReset)
            {
                await ClearConversationStateAsync();
                if (_rules?.Capture().Status == ProjectRulesStatus.Valid) _ = await _rules.ValidateAsync(CancellationToken.None);
            }
            lock (_gate)
            {
                // Cancellation and final publication use the same CLI decision point.
                if ((_cancelTurn || _stopReason != StopReason.None) &&
                    reason is not (AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable or AgentStopReason.WriteUnconfirmed or AgentStopReason.ProcessUnconfirmed))
                { status = AgentTurnStatus.Cancelled; reason = AgentStopReason.TurnCancelled; }
                else if (status == AgentTurnStatus.Completed && !requiresContextReset && result.McpStamp is { } finalMcp && _mcp?.IsCurrent(finalMcp) != true)
                { status = AgentTurnStatus.Stopped; reason = AgentStopReason.McpChanged; }
                else if (status == AgentTurnStatus.Completed && !requiresContextReset &&
                    (result.SkillsStamp is { } finalSkill && _skills?.IsCurrent(finalSkill) != true))
                { status = AgentTurnStatus.Stopped; reason = AgentStopReason.SkillsChanged; }
                else if (status == AgentTurnStatus.Completed && !requiresContextReset && !maintenance)
                {
                    if (review is not null && (review.Candidate is not { } candidate || !review.Publish(candidate)))
                    { status = AgentTurnStatus.Stopped; reason = AgentStopReason.GitStopped; }
                    else { context.CommitCompletedTurn(result.CompletedTurn); publishAnswer = true; }
                }
                if (reason == AgentStopReason.ProcessUnconfirmed || result.Operations.Any(o => o.ProcessCleanupFailed || o.ProcessSummary?.EndState == ProcessEndState.Unconfirmed))
                {
                    _processFatalStop = true; _executionPolicy!.StopProcesses(); RequestStop(StopReason.Failure);
                }
                if (maintenance) LastMaintenance = requiresContextReset ? null : new(result);
                else LastOutcome = requiresContextReset ? null : new(result, status, reason);
            }
            if (publishAnswer) interaction.Show(new(ChatEventKind.Answer, text: result.SafeText, status: LlmStatus.Completed));
            else if (review is not null && !requiresContextReset) review.Failed(review.LastAttemptError ?? (reason == AgentStopReason.ReviewInvalid ? "review_invalid" : reason == AgentStopReason.TurnCancelled ? "turn_cancelled" : "review_incomplete"));
            ShowTaskSummary();
            if (maintenance && (status == AgentTurnStatus.Completed || result.SafeText == "nothing_to_compact"))
                ShowContextBlock(status == AgentTurnStatus.Completed ? "對話整理已完成；未釘選資訊可能遺失，等待下一則指示。" : "nothing_to_compact：至少需要三個原始完整回合。");
            else if (status != AgentTurnStatus.Completed)
            {
                if (review is null && status != AgentTurnStatus.Cancelled && result.ModelStatus == LlmStatus.Truncated &&
                    !requiresContextReset && !string.IsNullOrWhiteSpace(result.SafeText))
                    interaction.Show(new(ChatEventKind.Answer, text: result.SafeText, status: LlmStatus.Truncated));
                else interaction.Show(new(ChatEventKind.TurnEnded,
                    status: status == AgentTurnStatus.Cancelled ? LlmStatus.Cancelled : result.ModelStatus,
                    errorCode: result.ModelErrorCode, turnStatus: status, stopReason: reason));
            }
            if (requiresContextReset)
                interaction.Show(new(ChatEventKind.ContextCleared, text: "模型 Context、讀取快照、計畫、確認與證據已清除；模式保留，請開始新的對話。"));
            interaction.Show(new(ChatEventKind.Usage, sessionUsage: usage.Snapshot(),
                generationsStarted: result.GenerationsStarted, turnSummary: result.TurnUsage, purpose: maintenance ? GenerationPurpose.Compaction : review is null ? GenerationPurpose.Conversation : GenerationPurpose.Review,
                profile: ModelCatalog.Find(_session!.Settings), profileUsage: usage.ProfileSnapshots(), programGenerationsStarted: _generationBudget.Snapshot().ProgramGenerationsStarted));
            if (result.ModelErrorCode == LlmErrorCode.ClientUnavailable ||
                result.StopReason is AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable || _mcp?.FatalFailure == true) RequestStop(StopReason.Failure);
            lock (_gate)
            {
                keyHandoff = !_cancelTurn && _stopReason == StopReason.None &&
                    status == AgentTurnStatus.Failed && result.ModelErrorCode == LlmErrorCode.AuthenticationFailed;
                // Once the completed turn hands off to key choice, interrupts stop that
                // next phase, including while client/busy-reader cleanup is finishing.
                if (keyHandoff) _state = ChatState.AwaitingKeyChoice;
            }
            if (keyHandoff) await DisposeClientAsync();
            return keyHandoff;
        }
        finally
        {
            if (review is not null) _git?.Unbind();
            reviewScope?.Dispose();
            try { StopBusyObserver(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            lock (_gate)
            {
                _turnCancellation = null; _turnSignal = null;
                if (_stopReason == StopReason.None) _state = keyHandoff ? ChatState.AwaitingKeyChoice : ChatState.AwaitingInput;
            }
        }
    }

    private void StopBusyObserver()
    {
        IDisposable? observer;
        lock (_gate) { observer = _busyObserver; _busyObserver = null; }
        observer?.Dispose();
    }

    private async ValueTask<FileApprovalDecision> ConfirmChangeAsync(FileChangePreview preview, CancellationToken token)
    {
        var decision = FileApprovalDecision.Unavailable;
        try
        {
            lock (_gate)
            {
                if (_turnCancellation is null || _session is null || _stopReason != StopReason.None || _cancelTurn || token.IsCancellationRequested)
                    return FileApprovalDecision.Cancelled;
                _state = ChatState.AwaitingApproval;
            }
            StopBusyObserver();
            token.ThrowIfCancellationRequested();
            decision = await interaction.ConfirmAsync(preview, token);
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
                if (!Stopping) _busyObserver = interaction.ObserveBusyInput(HandleSignal);
                lock (_gate)
                {
                    _state = _stopReason != StopReason.None ? ChatState.Stopping : _cancelTurn ? ChatState.Cancelling : ChatState.ExecutingTools;
                }
            }
            catch (Exception) { RequestStop(StopReason.Failure); decision = FileApprovalDecision.Unavailable; }
        }
        return decision;
    }

    private (ToolRegistry Registry, AgentRunner Runner) CreateAgent(BootstrapSession session, ILlmClient client, IWorkspaceFiles workspace)
    {
        _git = new((gitReadWorkspaceFactory ?? new GitReadWorkspaceFactory()).Create(session, workspace), session.Policy,
            () => "g" + checked(++_gitSequence).ToString(System.Globalization.CultureInfo.InvariantCulture));
        _reviews = new(_git, session.Policy);
        var processFactory = controlledTasksFactory ?? new ControlledDotnetTasksFactory();
        _processes = processFactory.Create(session.Workspace, session.Policy, _executionPolicy!, workspace,
            ConfirmProcessAsync, text => interaction.Show(new(ChatEventKind.Ready, text: text)));
        _executionPolicy!.ProcessCapabilityAvailable = processFactory.IsAvailable;
        _executionPolicy.ProcessTestCapabilityAvailable = processFactory.Supports(DotnetTaskKind.Test);
        var registry = new ToolRegistry([new ListFilesTool(workspace, session.Policy), new ReadFileTool(workspace, session.Policy),
            new CreateFileTool(workspace, session.Policy), new ReplaceTextTool(workspace, session.Policy),
            new SearchFilesTool(workspace, session.Policy), new SearchTextTool(workspace, session.Policy),
            new ReadFileRangeTool(workspace, session.Policy), new SubmitPlanTool(_plans!, session.Policy),
            new UpdateTaskTool(_plans!, session.Policy), new RunDotnetTaskTool(_processes, session.Policy),
            new GitStatusTool(_git, session.Policy, _executionPolicy), new GitDiffTool(_git, session.Policy, _executionPolicy),
            new ReadGitFileRangeTool(_git, session.Policy, _executionPolicy)], session.Policy, _executionPolicy);
        return (registry, new AgentRunner(client, registry, session.Policy, systemInstruction, usage, HandleAgentEvent,
            _generationBudget, _contexts, runtimeFactory.CreateContextInspector(client), ModelCatalog.Find(session.Settings)));
    }

    private void HandleAgentEvent(AgentEvent message)
    {
        lock (_gate)
        {
            if (_cancelTurn || _stopReason != StopReason.None) _state = ChatState.Cancelling;
            else _state = message.Kind == AgentEventKind.Generating ? ChatState.Generating : ChatState.ExecutingTools;
        }
        if (message.Notice is not null) ShowContextBlock(message.Notice);
        interaction.Show(message.Kind == AgentEventKind.Generating ? new(ChatEventKind.Generating,
            text: message.Purpose == GenerationPurpose.Compaction ? "[摘要維護] 正在整理對話；模型摘要可能遺失未釘選資訊。" : null, purpose: message.Purpose) :
            new(ChatEventKind.AgentProgress, agentEvent: message));
    }

    private async Task ReplaceKeyAsync()
    {
        if (_processFatalStop) { RequestStop(StopReason.Failure); return; }
        interaction.ClearInputState();
        SetState(ChatState.AwaitingKeyChoice);
        var choice = await interaction.ChooseKeyReplacementAsync(_stop.Token);
        if (Stopping) return;
        if (choice != KeyReplacementChoice.Reenter)
        {
            RequestStop(choice switch { KeyReplacementChoice.Exit or KeyReplacementChoice.EndOfInput => StopReason.Normal, KeyReplacementChoice.Cancelled => StopReason.Cancelled, _ => StopReason.Failure });
            return;
        }
        SetState(ChatState.ReadingReplacementKey);
        using var input = await interaction.ReadKeyAsync(_stop.Token);
        if (Stopping) return;
        if (input.Status != KeyInputStatus.Accepted)
        {
            RequestStop(input.Status switch { KeyInputStatus.EndOfInput => StopReason.Normal, KeyInputStatus.Cancelled => StopReason.Cancelled, _ => StopReason.Failure });
            return;
        }
        await ClearConversationStateAsync(); LastOutcome = null;
        _work = null;
        _rules?.DetachPolicy();
        await DisposeWorkspaceAsync();
        BootstrapSession? replacement = null;
        ILlmClient? client = null;
        IWorkspaceFiles? workspace = null;
        try
        {
            replacement = runtimeFactory.CreateReplacementSession(_session!.Workspace, _session.Settings, input);
            replacement.ModelSession = _models?.Capture();
            _plans!.BindPolicy(replacement.Policy);
            _contexts!.BindPolicy(replacement.Policy);
            _executionPolicy = new(_plans) { Rules = _rules, Skills = _skills, Mcp = _mcp };
            _mcp?.BindPolicy(_executionPolicy, replacement.Policy, AgentRunner.CreateMcpGuard(replacement.Policy, systemInstruction));
            client = runtimeFactory.CreateClient(replacement);
            workspace = workspaceFilesFactory.Create(replacement, _executionPolicy);
            if (_rules is not null && workspace.ProjectRulesWorkspace is { } reader) _rules.BindPolicy(replacement.Policy, reader);
            _skills?.BindPolicy(replacement.Policy, workspace.SkillsWorkspace, _executionPolicy);
            var agent = CreateAgent(replacement, client, workspace);
            RequireSafeModelRuntime(replacement, client, agent.Registry);
            interaction.SetPolicy(replacement.Policy);
            var old = _session;
            _session = replacement; _client = client; _workspace = workspace;
            _registry = agent.Registry; _runner = agent.Runner;
            replacement = null; client = null; workspace = null;
            old.Dispose();
            if (_rules is not null) await RunRulesOperationAsync(token => _rules.TryReloadAsync(token));
            interaction.Show(new(ChatEventKind.ContextCleared, text: "認證資料已更新；對話已清除，請重新輸入訊息。"));
        }
        finally
        {
            try { if (workspace is not null) await workspace.DisposeAsync(); }
            finally
            {
                try { if (client is not null) await client.DisposeAsync(); }
                finally { replacement?.Dispose(); }
            }
        }
    }

    private async Task ClearConversationStateAsync()
    {
        if (_mcp is not null) await _mcp.ResetAsync();
        interaction.ClearInputState();
        lock (_gate)
        {
            LastWork = null;
            _rules?.InvalidateCandidates();
            _skills?.Clear();
            _reviews?.Clear();
            if (_contexts is not null) _contexts.Clear(); else context.Clear();
            LastMaintenance = null;
            LastOutcome = null;
            _plans?.Clear();
            _executionPolicy?.Recovery.Clear();
            _work?.Clear();
        }
        _workspace?.Snapshots.Clear();
        _workspace?.Checkpoints?.Clear();
    }

    private async Task DisposeWorkspaceAsync()
    {
        _runner = null; _registry = null;
        var git = _git; _git = null; _reviews = null;
        if (git is not null) await git.DisposeAsync();
        var processes = _processes; _processes = null;
        if (processes is not null) await processes.DisposeAsync();
        var workspace = _workspace;
        _workspace = null;
        if (workspace is not null) await workspace.DisposeAsync();
    }

    private async Task DisposeClientAsync()
    {
        var client = _client;
        _client = null;
        if (client is not null) await client.DisposeAsync();
    }

    private void HandleSignal(ChatControlSignal signal)
    {
        var showCancellation = false;
        lock (_gate)
        {
            if (_finalized) return;
            if (signal == ChatControlSignal.EndOfInput) RequestStop(StopReason.Normal);
            else if (signal == ChatControlSignal.Unavailable) RequestStop(StopReason.Failure);
            else if (_switchingModel) RequestStop(StopReason.Cancelled);
            else if (_state == ChatState.AwaitingModelApproval)
            {
                try { _modelApprovalCancellation?.Cancel(); }
                catch (Exception) { RequestStop(StopReason.Failure); }
            }
            else if (_state == ChatState.AwaitingPlanApproval)
            {
                try { _planCancellation?.Cancel(); }
                catch (Exception) { RequestStop(StopReason.Failure); }
            }
            else if (_state is ChatState.Working or ChatState.AwaitingWorkApproval or ChatState.CheckpointOperation or ChatState.AwaitingCheckpointApproval or ChatState.LoadingRules or ChatState.LoadingSkills or ChatState.CapturingReview or ChatState.Generating or ChatState.ExecutingTools or ChatState.AwaitingApproval or ChatState.AwaitingProcessApproval or ChatState.AwaitingMcpApproval or ChatState.Cancelling)
            {
                _cancelTurn = true;
                _state = ChatState.Cancelling;
                try { _turnCancellation?.Cancel(); }
                catch (Exception) { RequestStop(StopReason.Failure); }
            }
            else RequestStop(StopReason.Cancelled);
            if (!_cancellationNotice && signal != ChatControlSignal.Unavailable &&
                _state is ChatState.Cancelling or ChatState.LoadingRules or ChatState.LoadingSkills or ChatState.CapturingReview or ChatState.Generating or ChatState.ExecutingTools or ChatState.AwaitingApproval or ChatState.AwaitingProcessApproval or ChatState.AwaitingMcpApproval)
            { _cancellationNotice = true; showCancellation = true; }
            _turnSignal?.TrySetResult();
        }
        if (showCancellation) TryShow(new(ChatEventKind.Ready, text: "正在取消；等待目前操作與收尾完成。"));
    }

    private void RequestStop(StopReason reason)
    {
        lock (_gate)
        {
            if (_finalized) return;
            if (reason > _stopReason) _stopReason = reason;
            try { _stop.Cancel(); }
            catch (Exception) { _stopReason = StopReason.Failure; }
            _turnSignal?.TrySetResult();
        }
    }
    private bool Stopping { get { lock (_gate) return _stopReason != StopReason.None; } }
    private void SetState(ChatState state) { lock (_gate) _state = state; }
    private void ShowInvalidInput() => interaction.Show(new(ChatEventKind.InputRejected, text: "輸入無效或超過 16384 個 UTF-16 單位；本次未送出。"));
    private void TryShow(ChatEvent message) { try { interaction.Show(message); } catch (Exception) { RequestStop(StopReason.Failure); } }
    private enum StopReason { None, Normal, Cancelled, Failure }
    public override string ToString() => "[ChatApplication]";
}