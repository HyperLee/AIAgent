using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>單一回合的依序協調器；不擁有借用資源，也不提交對話。</summary>
internal sealed partial class AgentRunner(ILlmClient client, ToolRegistry registry, ISensitiveDataPolicy policy,
    string systemInstruction, UsageAccumulator programUsage, Action<AgentEvent> onEvent,
    GenerationBudget? generationBudget = null, ContextCoordinator? contextCoordinator = null, ILlmContextInspector? contextInspector = null)
{
    internal const int MaxGenerationRequestsPerTurn = 5;
    private int _running;
    private readonly GenerationBudget _budget = generationBudget ?? new();
    private readonly ILlmContextInspector? _inspector = contextInspector ?? client as ILlmContextInspector;

    internal async Task<AgentTurnResult> RunAsync(IReadOnlyList<LlmMessage> initialMessages, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return Unavailable(AgentStopReason.RunnerUnavailable);
        try
        {
            using var generation = registry.ExecutionPolicy.Plans.BorrowGeneration();
            if (initialMessages is null || initialMessages.Count == 0 || initialMessages.Any(message => message is null) ||
                initialMessages[^1] is not { Kind: LlmMessageKind.User } user || string.IsNullOrWhiteSpace(user.UserText))
                return Unavailable(AgentStopReason.LocalFailure);
            var turn = new Turn(client, registry, policy, systemInstruction, programUsage, onEvent,
                initialMessages.ToArray(), cancellationToken, _budget, contextCoordinator, _inspector);
            _budget.BeginConversation();
            return await turn.RunAsync();
        }
        catch (Exception) { return Unavailable(AgentStopReason.LocalFailure); }
        finally { Volatile.Write(ref _running, 0); }
    }

    private static AgentTurnResult Unavailable(AgentStopReason reason) => new(AgentTurnStatus.Failed, reason,
        null, null, "", [], [], 0, new UsageAccumulator().Snapshot(), false);
    public override string ToString() => "[AgentRunner]";

    internal async Task<AgentTurnResult> RunMaintenanceAsync(CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return Unavailable(AgentStopReason.RunnerUnavailable);
        try
        {
            using var generation = registry.ExecutionPolicy.Plans.BorrowGeneration();
            return await new Turn(client, registry, policy, systemInstruction, programUsage, onEvent, [], token,
                _budget, contextCoordinator, _inspector, true).RunMaintenanceAsync();
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private sealed partial class Turn(ILlmClient client, ToolRegistry registry, ISensitiveDataPolicy policy,
        string instruction, UsageAccumulator programUsage, Action<AgentEvent> onEvent,
        IReadOnlyList<LlmMessage> initial, CancellationToken token, GenerationBudget budget,
        ContextCoordinator? coordinator, ILlmContextInspector? inspector, bool maintenance = false)
    {
        private readonly object _gate = coordinator?.DecisionGate ?? new();
        private readonly List<LlmMessage> _messages = initial.ToList();
        private readonly List<LlmMessage> _current = initial.Count == 0 ? [] : [initial[^1]];
        private readonly List<LocalToolOutcome> _operations = [];
        private readonly UsageAccumulator _usage = new();
        private bool _cancelled;
        private bool _reset;
        private bool _eventFailed;
        private int _generations;
        private LlmStatus? _modelStatus;
        private LlmErrorCode? _modelError;
        private readonly long _planGeneration = registry.ExecutionPolicy.Plans.ExecutionGeneration;

        internal async Task<AgentTurnResult> RunAsync()
        {
            using var registration = token.Register(() => { lock (_gate) _cancelled = true; });
            try
            {
                await PrepareConversationAsync();
                while (true)
                {
                    if (Cancelled) return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                    var result = await GenerateAsync(Request(), GenerationPurpose.Conversation);
                    if (result is null) return Invalid();
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
                        if (LimitReason() is { } limit)
                        {
                            AddRemaining(result.ToolCalls, 0, LimitCode(limit));
                            return Finish(AgentTurnStatus.Stopped, limit);
                        }
                        if (result.ToolCalls.Any(call => registry.IsModeRestricted(call.Name)))
                        {
                            foreach (var call in result.ToolCalls)
                                AddOperation(call, ToolResponseWriter.Create(registry.IsModeRestricted(call.Name) ? ToolStatus.Rejected : ToolStatus.NotExecuted,
                                    registry.IsModeRestricted(call.Name) ? "mode_restricted" : "turn_stopped", policy));
                            return Finish(AgentTurnStatus.Stopped, AgentStopReason.ModeRestricted);
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
            catch (OperationCanceledException) when (token.IsCancellationRequested || Cancelled)
            { return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled); }
            catch (ContextFlowException failure) { return Finish(failure.Status, failure.Reason); }
            catch (ContextCapacityException) { return Finish(AgentTurnStatus.Stopped, AgentStopReason.ContextCapacity); }
            catch (ContextInspectionException) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.InvalidProtocol); }
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
                if (LimitReason() is { } limit)
                {
                    AddRemaining(calls, index, LimitCode(limit));
                    return Finish(AgentTurnStatus.Stopped, limit);
                }
                try { Emit(new(AgentEventKind.ExecutingTool, _generations, call.PartIndex, registry.SafeName(call.Name))); }
                catch (Exception)
                {
                    AddRemaining(calls, index, "turn_stopped");
                    return Finish(AgentTurnStatus.Failed, AgentStopReason.LocalFailure);
                }
                if (!TryBeginTool(out var startReason))
                {
                    AddRemaining(calls, index, startReason == AgentStopReason.TurnCancelled ? "turn_cancelled" : LimitCode(startReason));
                    return Finish(startReason == AgentStopReason.TurnCancelled ? AgentTurnStatus.Cancelled : AgentTurnStatus.Stopped, startReason);
                }
                ToolExecutionResult result;
                var taskBound = registry.IsTaskOperation(call.Name) && registry.ExecutionPolicy.Plans.HasInProgress;
                var localFailure = false;
                try { result = await registry.DispatchAsync(call, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                { result = ToolResponseWriter.Create(ToolStatus.Cancelled, "none", policy); }
                catch (Exception)
                { result = ToolResponseWriter.Create(ToolStatus.Failed, "unavailable", policy); localFailure = true; }

                AddOperation(call, result);
                if (result.Response is null) _reset = true;
                var reason = result.ProcessCleanupFailed || result.ProcessSummary?.EndState == ProcessEndState.Unconfirmed ||
                    result.ErrorCode is "process_unconfirmed" or "process_cleanup_failed" ? AgentStopReason.ProcessUnconfirmed
                    : localFailure || _eventFailed || result.CleanupFailed || result.ErrorCode == "unavailable" ? AgentStopReason.LocalFailure
                    : result.WriteState == FileWriteState.Unconfirmed ? AgentStopReason.WriteUnconfirmed
                    : result.Response is null || result.ErrorCode is "sensitive_arguments" or "sensitive_content" ? AgentStopReason.SensitiveData
                    : result.ErrorCode == "process_limit" ? AgentStopReason.ProcessResourceLimit
                    : result.ErrorCode == "workspace_unavailable" ? AgentStopReason.WorkspaceUnavailable
                    : result.Status == ToolStatus.Cancelled || Cancelled ? AgentStopReason.TurnCancelled
                    : result.ErrorCode == "approval_declined" ? AgentStopReason.ApprovalDeclined
                    : result.ErrorCode == "conflict" || result.StopAfterOperation && result.ErrorCode is "path_changed" or "symbolic_link" or "sensitive_path" ? AgentStopReason.FileConflict
                    : result.WriteState is not null && (result.Status == ToolStatus.Failed || result.StopAfterOperation || result.ErrorCode == "publish_unsupported") ? AgentStopReason.FileWriteFailed
                    : result.ErrorCode == "mode_restricted" ? AgentStopReason.ModeRestricted
                    : registry.IsProcessTool(call.Name) && (result.StopAfterOperation || result.Status != ToolStatus.Success) ? AgentStopReason.ProcessFailed
                    : result.ErrorCode == "plan_required" ? AgentStopReason.PlanRequired
                    : result.ErrorCode == "plan_not_approved" ? AgentStopReason.PlanNotApproved
                    : result.ErrorCode is "evidence_limit" or "plan_too_large" ? AgentStopReason.TaskResourceLimit
                    : result.ErrorCode is "task_required" or "task_scope_mismatch" || taskBound && result.Status != ToolStatus.Success ? AgentStopReason.TaskBlocked
                    : AgentStopReason.None;
                if (reason != AgentStopReason.None)
                {
                    if (result.ErrorCode == "sensitive_arguments") _reset = true;
                    AddRemaining(calls, index + 1, reason == AgentStopReason.TurnCancelled ? "turn_cancelled" : "turn_stopped");
                    return Finish(reason == AgentStopReason.TurnCancelled ? AgentTurnStatus.Cancelled :
                        reason is AgentStopReason.WorkspaceUnavailable or AgentStopReason.ApprovalDeclined or AgentStopReason.FileConflict or AgentStopReason.WriteUnconfirmed or
                            AgentStopReason.ModeRestricted or AgentStopReason.PlanRequired or AgentStopReason.PlanNotApproved or AgentStopReason.TaskBlocked or AgentStopReason.TaskResourceLimit ||
                        (reason is AgentStopReason.FileWriteFailed or AgentStopReason.ProcessFailed) && result.Status == ToolStatus.Rejected || reason == AgentStopReason.ProcessResourceLimit ? AgentTurnStatus.Stopped : AgentTurnStatus.Failed, reason);
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

        private bool TryBeginTool(out AgentStopReason reason)
        {
            // The permission point precedes invocation; dependencies receive the same token
            // and decide cancellation versus I/O/publication at their own boundaries.
            lock (_gate)
            {
                reason = Cancelled ? AgentStopReason.TurnCancelled : LimitReason() ?? AgentStopReason.None;
                return reason == AgentStopReason.None;
            }
        }
        private bool Cancelled { get { lock (_gate) return _cancelled || token.IsCancellationRequested || _planGeneration != registry.ExecutionPolicy.Plans.Generation; } }

        private AgentTurnResult Invalid()
        {
            _reset = true; _modelStatus = null; _modelError = null;
            return Finish(AgentTurnStatus.Failed, AgentStopReason.InvalidProtocol);
        }

        private AgentTurnResult Finish(AgentTurnStatus status, AgentStopReason reason, string text = "",
            IReadOnlyList<LlmMessage>? completed = null)
        {
            if (Cancelled && reason is not (AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable or AgentStopReason.WriteUnconfirmed or AgentStopReason.ProcessUnconfirmed))
            { status = AgentTurnStatus.Cancelled; reason = AgentStopReason.TurnCancelled; text = ""; completed = null; }
            if (_eventFailed) { status = AgentTurnStatus.Failed; reason = AgentStopReason.LocalFailure; text = ""; completed = null; }
            if (!maintenance) registry.ExecutionPolicy.Plans.BlockInProgress(reason switch
            {
                AgentStopReason.None when status == AgentTurnStatus.Completed => "turn_paused",
                AgentStopReason.TurnCancelled => "turn_cancelled",
                AgentStopReason.GenerationLimit => "generation_limit",
                AgentStopReason.ProgramGenerationLimit or AgentStopReason.ContextCapacity or AgentStopReason.ContextCompactionFailed => "resource_limit",
                AgentStopReason.ApprovalDeclined => "approval_declined",
                AgentStopReason.FileConflict => "file_conflict",
                AgentStopReason.WriteUnconfirmed => "write_unconfirmed",
                AgentStopReason.WorkspaceUnavailable => "workspace_unavailable",
                AgentStopReason.TaskResourceLimit => "resource_limit",
                AgentStopReason.TaskBlocked => "operation_failed",
                AgentStopReason.ProcessResourceLimit => "resource_limit",
                AgentStopReason.ProcessUnconfirmed => "process_unconfirmed",
                AgentStopReason.ProcessFailed => ProcessBlockReason(),
                AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable => "local_failure",
                _ => "model_outcome"
            });
            return new(status, reason, _modelStatus, _modelError, text,
                status == AgentTurnStatus.Completed ? completed ?? [] : [], _operations, _generations, _usage.Snapshot(), _reset);
        }

        private string ProcessBlockReason() => _operations.LastOrDefault(o => o.ToolName == "run_dotnet_task")?.ErrorCode switch
        {
            "process_start_failed" => "process_start_failed",
            "process_timed_out" => "process_timed_out",
            "process_output_limit" => "process_output_limit",
            "validation_inconclusive" => "validation_inconclusive",
            "capability_unavailable" => "capability_unavailable",
            _ => "process_failed"
        };

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