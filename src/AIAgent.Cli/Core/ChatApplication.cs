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
                    interaction.Show(new(ChatEventKind.Ready, text: "/help：操作說明；/clear：清除對話，保留累計；/exit：退出。\n" +
                        "目前只有文字聊天，沒有檔案工具。空白不送出；EOF／Ctrl+D 正常退出。\n" +
                        "等待輸入時 Ctrl+C 退出；生成中 Ctrl+C 只取消本回合，Ctrl+D 結束。\n" +
                        "單行上限 16384 個 UTF-16 單位。輸入期間不回顯；Enter 後顯示安全訊息。"));
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
        try
        {
            using var observer = interaction.ObserveBusyInput(HandleSignal);
            interaction.Show(new(ChatEventKind.Generating));
            var request = new LlmRequest(systemInstruction, context.BuildMessages(user), []);
            var generation = _client!.GenerateAsync(request, cancellation.Token);
            await Task.WhenAny(generation, _turnSignal.Task);
            if (cancellation.IsCancellationRequested) SetState(ChatState.Cancelling);
            var result = await generation;
            if (result is null) throw new InvalidOperationException("模型結果不存在");
            usage.Record(result);
            var (status, error, reset) = Validate(result);
            lock (_gate)
            {
                if (reset) ClearConversationState();
                // This lock is the commit point: signal callbacks only set flags under the same gate.
                if (_cancelTurn || _stopReason != StopReason.None)
                { status = LlmStatus.Cancelled; error = LlmErrorCode.None; }
                else if (status == LlmStatus.Completed && !reset)
                    context.CommitCompletedTurn([user, result.ModelMessage!]);
                LastOutcome = new(status, error, result.RequestDispatched, reset);
                _state = ChatState.AwaitingInput;
            }
            if (status == LlmStatus.Completed && !reset || status == LlmStatus.Truncated && !reset && !string.IsNullOrWhiteSpace(result.SafeText))
                interaction.Show(new(ChatEventKind.Answer, text: result.SafeText, status: status));
            else interaction.Show(new(ChatEventKind.TurnEnded, status: status, errorCode: error));
            if (reset) interaction.Show(new(ChatEventKind.ContextCleared, text: "模型 Context 已清除；請開始新的對話。"));
            interaction.Show(new(ChatEventKind.Usage, turnUsage: result.RequestDispatched ? result.Usage : null,
                sessionUsage: usage.Snapshot(), requestDispatched: result.RequestDispatched));
            if (error == LlmErrorCode.ClientUnavailable) RequestStop(StopReason.Failure);
            var authenticate = status == LlmStatus.Failed && error == LlmErrorCode.AuthenticationFailed && !Stopping;
            if (authenticate) await DisposeClientAsync();
            return authenticate;
        }
        finally
        {
            lock (_gate) { _turnCancellation = null; _turnSignal = null; }
        }
    }

    private (LlmStatus Status, LlmErrorCode Error, bool Reset) Validate(LlmResult result)
    {
        var reset = result.RequiresContextReset;
        if (result.SafeText is not null && _session!.Policy.ContainsCurrentKey(result.SafeText)) return (LlmStatus.Failed, LlmErrorCode.SensitiveOutput, true);
        if (!Enum.IsDefined(result.Status) || !Enum.IsDefined(result.ErrorCode) || result.SafeText is null)
            return (LlmStatus.Failed, LlmErrorCode.InvalidResponse, true);
        if (result.Status == LlmStatus.ToolCalls || result.ToolCalls.Count != 0) return (LlmStatus.Failed, LlmErrorCode.UnexpectedToolCall, true);
        if (result.Status == LlmStatus.Completed)
        {
            if (result.ErrorCode != LlmErrorCode.None || string.IsNullOrWhiteSpace(result.SafeText) || result.ModelMessage is not { Kind: LlmMessageKind.Model, Protocol: not null })
                return (LlmStatus.Failed, LlmErrorCode.InvalidResponse, true);
        }
        else if (result.ModelMessage is not null || (result.Status == LlmStatus.Failed) == (result.ErrorCode == LlmErrorCode.None))
            return (LlmStatus.Failed, LlmErrorCode.InvalidResponse, true);
        reset |= result.ErrorCode is LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall;
        if (reset && result.Status == LlmStatus.Completed) return (LlmStatus.Failed, LlmErrorCode.InvalidResponse, true);
        return (result.Status, result.ErrorCode, reset);
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
            interaction.SetPolicy(replacement.Policy);
            var old = _session;
            _session = replacement; _client = client; _workspace = workspace;
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
        _workspace?.Snapshots.Clear();
    }

    private async Task DisposeWorkspaceAsync()
    {
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
            else if (_state is ChatState.Generating or ChatState.Cancelling)
            {
                _cancelTurn = true;
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