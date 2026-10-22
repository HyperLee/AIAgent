using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>單一回合的依序協調器；不擁有借用資源，也不提交對話。</summary>
internal sealed class AgentRunner(ILlmClient client, ToolRegistry registry, ISensitiveDataPolicy policy,
    string systemInstruction, UsageAccumulator programUsage, Action<AgentEvent> onEvent)
{
    internal const int MaxGenerationRequestsPerTurn = 5;
    private int _running;

    internal async Task<AgentTurnResult> RunAsync(IReadOnlyList<LlmMessage> initialMessages, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return Unavailable(AgentStopReason.RunnerUnavailable);
        try
        {
            if (initialMessages is null || initialMessages.Count == 0 || initialMessages.Any(message => message is null) ||
                initialMessages[^1] is not { Kind: LlmMessageKind.User } user || string.IsNullOrWhiteSpace(user.UserText))
                return Unavailable(AgentStopReason.LocalFailure);
            var turn = new Turn(client, registry, policy, systemInstruction, programUsage, onEvent,
                initialMessages.ToArray(), cancellationToken);
            return await turn.RunAsync();
        }
        catch (Exception) { return Unavailable(AgentStopReason.LocalFailure); }
        finally { Volatile.Write(ref _running, 0); }
    }

    private static AgentTurnResult Unavailable(AgentStopReason reason) => new(AgentTurnStatus.Failed, reason,
        null, null, "", [], [], 0, new UsageAccumulator().Snapshot(), false);
    public override string ToString() => "[AgentRunner]";

    private sealed class Turn(ILlmClient client, ToolRegistry registry, ISensitiveDataPolicy policy,
        string instruction, UsageAccumulator programUsage, Action<AgentEvent> onEvent,
        IReadOnlyList<LlmMessage> initial, CancellationToken token)
    {
        private readonly object _gate = new();
        private readonly List<LlmMessage> _messages = initial.ToList();
        private readonly List<LlmMessage> _current = [initial[^1]];
        private readonly List<LocalToolOutcome> _operations = [];
        private readonly UsageAccumulator _usage = new();
        private bool _cancelled;
        private bool _reset;
        private bool _eventFailed;
        private int _generations;
        private LlmStatus? _modelStatus;
        private LlmErrorCode? _modelError;

        internal async Task<AgentTurnResult> RunAsync()
        {
            using var registration = token.Register(() => { lock (_gate) _cancelled = true; });
            try
            {
                while (true)
                {
                    if (Cancelled) return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                    Emit(new(AgentEventKind.Generating, _generations + 1));
                    if (!TryBegin()) return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                    _generations++;
                    var result = await client.GenerateAsync(new(instruction, _messages, registry.Definitions), token);
                    if (result is null) return Invalid();
                    programUsage.Record(result); _usage.Record(result);
                    _reset |= result.RequiresContextReset;
                    if (!ValidResult(result)) return Invalid();
                    _modelStatus = result.Status; _modelError = result.ErrorCode;
                    _reset |= result.ErrorCode is LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall;
                    if (policy.ContainsCurrentKey(result.SafeText))
                    {
                        _reset = true;
                        if (result.Status == LlmStatus.ToolCalls) AddRemaining(result.ToolCalls, 0, "turn_stopped");
                        return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData);
                    }
                    if (result.Status == LlmStatus.ToolCalls)
                    {
                        // Check the entire native batch before acquiring any tool start permission.
                        if (result.ToolCalls.Any(registry.IsSensitive))
                        {
                            _reset = true;
                            foreach (var call in result.ToolCalls)
                                AddOperation(call, registry.IsSensitive(call)
                                    ? ToolResponseWriter.Create(ToolStatus.Rejected, "sensitive_arguments", policy)
                                    : ToolResponseWriter.Create(ToolStatus.NotExecuted, "turn_stopped", policy));
                            return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData);
                        }
                        if (Cancelled)
                        {
                            AddRemaining(result.ToolCalls, 0, "turn_cancelled");
                            return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                        }
                        if (_generations == MaxGenerationRequestsPerTurn)
                        {
                            AddRemaining(result.ToolCalls, 0, "generation_limit");
                            return Finish(AgentTurnStatus.Stopped, AgentStopReason.GenerationLimit);
                        }
                        var stopped = await ExecuteBatchAsync(result);
                        if (stopped is not null) return stopped;
                        continue;
                    }
                    if (Cancelled || result.Status == LlmStatus.Cancelled)
                        return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                    if (result.Status == LlmStatus.Completed)
                    {
                        _current.Add(result.ModelMessage!);
                        if (!ToolCallPairing.ValidTurn(_current)) return Invalid();
                        return Finish(AgentTurnStatus.Completed, AgentStopReason.None, result.SafeText, _current);
                    }
                    return Finish(result.Status == LlmStatus.Failed ? AgentTurnStatus.Failed : AgentTurnStatus.Stopped,
                        AgentStopReason.ModelOutcome, result.Status == LlmStatus.Truncated ? result.SafeText : "");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled); }
            catch (Exception) { return Finish(AgentTurnStatus.Failed, AgentStopReason.LocalFailure); }
        }

        private async Task<AgentTurnResult?> ExecuteBatchAsync(LlmResult model)
        {
            var results = new List<LlmToolResult>();
            var calls = model.ToolCalls;
            for (var index = 0; index < calls.Count; index++)
            {
                var call = calls[index];
                if (Cancelled)
                {
                    AddRemaining(calls, index, "turn_cancelled");
                    return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                }
                try { Emit(new(AgentEventKind.ExecutingTool, _generations, call.PartIndex, registry.SafeName(call.Name))); }
                catch (Exception)
                {
                    AddRemaining(calls, index, "turn_stopped");
                    return Finish(AgentTurnStatus.Failed, AgentStopReason.LocalFailure);
                }
                if (!TryBegin())
                {
                    AddRemaining(calls, index, "turn_cancelled");
                    return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                }
                ToolExecutionResult result;
                var localFailure = false;
                try { result = await registry.DispatchAsync(call, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                { result = ToolResponseWriter.Create(ToolStatus.Cancelled, "none", policy); }
                catch (Exception)
                { result = ToolResponseWriter.Create(ToolStatus.Failed, "unavailable", policy); localFailure = true; }

                AddOperation(call, result);
                if (result.Response is null) _reset = true;
                var reason = localFailure || _eventFailed || result.CleanupFailed || result.ErrorCode == "unavailable" ? AgentStopReason.LocalFailure
                    : result.WriteState == FileWriteState.Unconfirmed ? AgentStopReason.WriteUnconfirmed
                    : result.Response is null || result.ErrorCode is "sensitive_arguments" or "sensitive_content" ? AgentStopReason.SensitiveData
                    : result.ErrorCode == "workspace_unavailable" ? AgentStopReason.WorkspaceUnavailable
                    : result.Status == ToolStatus.Cancelled || Cancelled ? AgentStopReason.TurnCancelled
                    : result.ErrorCode == "approval_declined" ? AgentStopReason.ApprovalDeclined
                    : result.ErrorCode == "conflict" || result.StopAfterOperation && result.ErrorCode is "path_changed" or "symbolic_link" or "sensitive_path" ? AgentStopReason.FileConflict
                    : result.WriteState is not null && (result.Status == ToolStatus.Failed || result.StopAfterOperation || result.ErrorCode == "publish_unsupported") ? AgentStopReason.FileWriteFailed
                    : AgentStopReason.None;
                if (reason != AgentStopReason.None)
                {
                    if (result.ErrorCode == "sensitive_arguments") _reset = true;
                    AddRemaining(calls, index + 1, reason == AgentStopReason.TurnCancelled ? "turn_cancelled" : "turn_stopped");
                    return Finish(reason == AgentStopReason.TurnCancelled ? AgentTurnStatus.Cancelled :
                        reason is AgentStopReason.WorkspaceUnavailable or AgentStopReason.ApprovalDeclined or AgentStopReason.FileConflict or AgentStopReason.WriteUnconfirmed ||
                        reason == AgentStopReason.FileWriteFailed && result.Status == ToolStatus.Rejected ? AgentTurnStatus.Stopped : AgentTurnStatus.Failed, reason);
                }
                results.Add(new(call.PartIndex, call.CallId, call.Name, result.Response!.Value));
            }
            if (!ToolCallPairing.MatchesResults(calls, results)) return Invalid();
            _messages.Add(model.ModelMessage!); _current.Add(model.ModelMessage!);
            var responseMessage = LlmMessage.FromToolResults(results);
            _messages.Add(responseMessage); _current.Add(responseMessage);
            return null;
        }

        private void AddRemaining(IReadOnlyList<LlmToolCall> calls, int start, string code)
        {
            for (var index = start; index < calls.Count; index++)
            {
                var result = ToolResponseWriter.Create(ToolStatus.NotExecuted, code, policy);
                if (result.Response is null) _reset = true;
                AddOperation(calls[index], result);
            }
        }

        private void AddOperation(LlmToolCall call, ToolExecutionResult result)
        {
            result = registry.PreserveWriteKind(call, result);
            var operation = new LocalToolOutcome(_generations, call.PartIndex, registry.SafeName(call.Name), result);
            _operations.Add(operation);
            if (_eventFailed) return;
            try { Emit(new(AgentEventKind.ToolCompleted, _generations, call.PartIndex, operation.ToolName, operation)); }
            catch (Exception) { /* Keep the published result; stop further events and operations. */ }
        }

        private void Emit(AgentEvent message)
        {
            try { onEvent(message); }
            catch (Exception) { _eventFailed = true; throw; }
        }

        private bool TryBegin()
        {
            // The permission point precedes invocation; dependencies receive the same token
            // and decide cancellation versus I/O/publication at their own boundaries.
            lock (_gate) return !_cancelled && !token.IsCancellationRequested;
        }
        private bool Cancelled { get { lock (_gate) return _cancelled || token.IsCancellationRequested; } }

        private AgentTurnResult Invalid()
        {
            _reset = true; _modelStatus = null; _modelError = null;
            return Finish(AgentTurnStatus.Failed, AgentStopReason.InvalidProtocol);
        }

        private AgentTurnResult Finish(AgentTurnStatus status, AgentStopReason reason, string text = "",
            IReadOnlyList<LlmMessage>? completed = null)
        {
            if (Cancelled && reason is not (AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable or AgentStopReason.WriteUnconfirmed))
            { status = AgentTurnStatus.Cancelled; reason = AgentStopReason.TurnCancelled; text = ""; completed = null; }
            if (_eventFailed) { status = AgentTurnStatus.Failed; reason = AgentStopReason.LocalFailure; text = ""; completed = null; }
            return new(status, reason, _modelStatus, _modelError, text,
                status == AgentTurnStatus.Completed ? completed ?? [] : [], _operations, _generations, _usage.Snapshot(), _reset);
        }

        private static bool ValidResult(LlmResult result)
        {
            if (!Enum.IsDefined(result.Status) || !Enum.IsDefined(result.ErrorCode) || result.SafeText is null ||
                (result.Status == LlmStatus.Failed) == (result.ErrorCode == LlmErrorCode.None)) return false;
            if (result.Status is LlmStatus.Completed or LlmStatus.ToolCalls)
            {
                if (result.RequiresContextReset || result.ModelMessage is not { } model ||
                    !ToolCallPairing.MatchesProjection(model, result.ToolCalls)) return false;
                return result.Status == LlmStatus.Completed
                    ? result.ToolCalls.Count == 0 && !string.IsNullOrWhiteSpace(result.SafeText)
                    : result.ToolCalls.Count > 0;
            }
            return result.ModelMessage is null && result.ToolCalls.Count == 0;
        }
    }
}