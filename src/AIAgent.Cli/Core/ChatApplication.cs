using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed class ChatApplication(IChatInteraction interaction, IChatRuntimeFactory runtimeFactory,
    ConversationContext context, UsageAccumulator usage, string systemInstruction, IWorkspaceFilesFactory workspaceFilesFactory)
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private CancellationTokenSource? _turnCancellation;
    private TaskCompletionSource? _turnSignal;
    private BootstrapSession? _session;
    private ILlmClient? _client;
    private IWorkspaceFiles? _workspace;
    private ToolRegistry? _registry;
    private AgentRunner? _runner;
    private IDisposable? _busyObserver;
    private ChatState _state = ChatState.AwaitingInput;
    private StopReason _stopReason;
    private bool _cancelTurn;
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
                _client = runtimeFactory.CreateClient(initialSession);
                _workspace = workspaceFilesFactory.Create(initialSession);
                (_registry, _runner) = CreateAgent(initialSession, _client, _workspace);
                interaction.Show(new(ChatEventKind.Ready, text: "聊天已就緒；輸入 /help 查看操作。輸入期間不回顯；Enter 後顯示安全訊息。\n" +
                    "本機累計不是正式帳單；傳輸嘗試不證明 Google 已收到或計費。"));
            }
            while (!Stopping)
            {
                SetState(ChatState.AwaitingInput);
                var input = await interaction.ReadInputAsync(_stop.Token);
                if (Stopping) break;
                if (input.Status != ChatInputStatus.Submitted)
                {
                    if (input.Status == ChatInputStatus.Invalid) ShowInvalidInput();
                    else RequestStop(input.Status switch { ChatInputStatus.EndOfInput => StopReason.Normal, ChatInputStatus.Cancelled => StopReason.Cancelled, _ => StopReason.Failure });
                    continue;
                }
                var text = input.Text!;
                var command = text.Trim();
                if (command.Length == 0) continue;
                if (command == "/exit") { RequestStop(StopReason.Normal); continue; }
                if (command == "/help")
                {
                    interaction.Show(new(ChatEventKind.Ready, text: "/help：操作說明；/clear：清除對話、快照與核准記憶體，保留累計；不復原已寫檔案；/exit：退出。\n" +
                        "支援 list_files、read_file、create_file、replace_text、search_files、search_text、read_file_range 七工具；文字搜尋與範圍讀取限 128 KiB UTF-8 文字檔，搜尋固定忽略產物與敏感目錄；每筆寫入先預覽並核准，拒絕即停止回合。內容可能送至 Gemini。空白不送出；EOF／Ctrl+D 正常退出。\n" +
                        "等待輸入時 Ctrl+C 退出；生成、工具或核准中 Ctrl+C 只取消本回合，Ctrl+D 結束。\n" +
                        "單行上限 16384 個 UTF-16 單位。輸入期間不回顯；Enter 後顯示安全訊息。\n" +
                        "替換保留普通權限位元，不保證 owner、ACL、xattr 或硬連結關係；取消不復原已寫內容。"));
                    continue;
                }
                if (command == "/clear")
                {
                    ClearConversationState(); LastOutcome = null;
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
                interaction.Show(new(ChatEventKind.UserMessage, text: text));
                if (await RunTurnAsync(LlmMessage.FromUserText(text)) && !Stopping) await ReplaceKeyAsync();
            }
        }
        catch (OperationCanceledException) { RequestStop(applicationCancellation.IsCancellationRequested ? StopReason.Cancelled : StopReason.Failure); }
        catch (Exception)
        {
            RequestStop(StopReason.Failure);
            if (policyReady) TryShow(new(ChatEventKind.Stopping, text: "本機流程無法完成；已安全停止。"));
        }
        finally
        {
            SetState(ChatState.Stopping);
            if (policyReady) TryShow(new(ChatEventKind.Stopping, text: "聊天已結束。"));
            try { ClearConversationState(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { await DisposeWorkspaceAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { await DisposeClientAsync(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            try { _session?.Dispose(); }
            catch (Exception) { RequestStop(StopReason.Failure); }
            _session = null;
            LastOutcome = null;
            registration.Dispose();
            lock (_gate) _finalized = true;
            _stop.Dispose();
        }
        return _stopReason switch { StopReason.Failure => 1, StopReason.Cancelled => 130, _ => 0 };
    }

    private async Task<bool> RunTurnAsync(LlmMessage user)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        lock (_gate)
        {
            if (_stopReason != StopReason.None) return false;
            _turnCancellation = cancellation;
            _turnSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _cancelTurn = false;
            _state = ChatState.Generating;
        }
        var keyHandoff = false;
        try
        {
            _busyObserver = interaction.ObserveBusyInput(HandleSignal);
            using var approvalBinding = workspaceFilesFactory.ApprovalBridge?.Bind(_session!, ConfirmChangeAsync);
            var result = await _runner!.RunAsync(context.BuildMessages(user), cancellation.Token);
            var status = result.Status;
            var reason = result.StopReason;
            lock (_gate)
            {
                if (result.RequiresContextReset) ClearConversationState();
                // Cancellation and final publication use the same CLI decision point.
                if ((_cancelTurn || _stopReason != StopReason.None) &&
                    reason is not (AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable or AgentStopReason.WriteUnconfirmed))
                { status = AgentTurnStatus.Cancelled; reason = AgentStopReason.TurnCancelled; }
                else if (status == AgentTurnStatus.Completed && !result.RequiresContextReset)
                {
                    context.CommitCompletedTurn(result.CompletedTurn);
                    interaction.Show(new(ChatEventKind.Answer, text: result.SafeText, status: LlmStatus.Completed));
                }
                LastOutcome = new(result, status, reason);
                if (status != AgentTurnStatus.Completed)
                {
                    if (status != AgentTurnStatus.Cancelled && result.ModelStatus == LlmStatus.Truncated &&
                        !result.RequiresContextReset && !string.IsNullOrWhiteSpace(result.SafeText))
                        interaction.Show(new(ChatEventKind.Answer, text: result.SafeText, status: LlmStatus.Truncated));
                    else interaction.Show(new(ChatEventKind.TurnEnded,
                        status: status == AgentTurnStatus.Cancelled ? LlmStatus.Cancelled : result.ModelStatus,
                        errorCode: result.ModelErrorCode, turnStatus: status, stopReason: reason));
                }
            }
            if (result.RequiresContextReset)
                interaction.Show(new(ChatEventKind.ContextCleared, text: "模型 Context 與讀取快照已清除；請開始新的對話。"));
            interaction.Show(new(ChatEventKind.Usage, sessionUsage: usage.Snapshot(),
                generationsStarted: result.GenerationsStarted, turnSummary: result.TurnUsage));
            if (result.ModelErrorCode == LlmErrorCode.ClientUnavailable ||
                result.StopReason is AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable) RequestStop(StopReason.Failure);
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
        var registry = new ToolRegistry([new ListFilesTool(workspace, session.Policy), new ReadFileTool(workspace, session.Policy),
            new CreateFileTool(workspace, session.Policy), new ReplaceTextTool(workspace, session.Policy),
            new SearchFilesTool(workspace, session.Policy), new SearchTextTool(workspace, session.Policy),
            new ReadFileRangeTool(workspace, session.Policy)], session.Policy);
        return (registry, new AgentRunner(client, registry, session.Policy, systemInstruction, usage, HandleAgentEvent));
    }

    private void HandleAgentEvent(AgentEvent message)
    {
        lock (_gate)
        {
            if (_cancelTurn || _stopReason != StopReason.None) _state = ChatState.Cancelling;
            else _state = message.Kind == AgentEventKind.Generating ? ChatState.Generating : ChatState.ExecutingTools;
        }
        interaction.Show(message.Kind == AgentEventKind.Generating ? new(ChatEventKind.Generating) :
            new(ChatEventKind.AgentProgress, agentEvent: message));
    }

    private async Task ReplaceKeyAsync()
    {
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
        ClearConversationState(); LastOutcome = null;
        await DisposeWorkspaceAsync();
        BootstrapSession? replacement = null;
        ILlmClient? client = null;
        IWorkspaceFiles? workspace = null;
        try
        {
            replacement = runtimeFactory.CreateReplacementSession(_session!.Workspace, _session.Settings, input);
            client = runtimeFactory.CreateClient(replacement);
            workspace = workspaceFilesFactory.Create(replacement);
            var agent = CreateAgent(replacement, client, workspace);
            interaction.SetPolicy(replacement.Policy);
            var old = _session;
            _session = replacement; _client = client; _workspace = workspace;
            _registry = agent.Registry; _runner = agent.Runner;
            replacement = null; client = null; workspace = null;
            old.Dispose();
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

    private void ClearConversationState()
    {
        context.Clear();
        LastOutcome = null;
        _workspace?.Snapshots.Clear();
    }

    private async Task DisposeWorkspaceAsync()
    {
        _runner = null; _registry = null;
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
        lock (_gate)
        {
            if (_finalized) return;
            if (signal == ChatControlSignal.EndOfInput) RequestStop(StopReason.Normal);
            else if (signal == ChatControlSignal.Unavailable) RequestStop(StopReason.Failure);
            else if (_state is ChatState.Generating or ChatState.ExecutingTools or ChatState.AwaitingApproval or ChatState.Cancelling)
            {
                _cancelTurn = true;
                _state = ChatState.Cancelling;
                try { _turnCancellation?.Cancel(); }
                catch (Exception) { RequestStop(StopReason.Failure); }
            }
            else RequestStop(StopReason.Cancelled);
            _turnSignal?.TrySetResult();
        }
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