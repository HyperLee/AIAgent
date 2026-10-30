using AIAgent.Cli.Git;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>單一回合的依序協調器；不擁有借用資源，也不提交對話。</summary>
internal sealed partial class AgentRunner(ILlmClient client, ToolRegistry registry, ISensitiveDataPolicy policy,
    string systemInstruction, UsageAccumulator programUsage, Action<AgentEvent> onEvent,
    GenerationBudget? generationBudget = null, ContextCoordinator? contextCoordinator = null, ILlmContextInspector? contextInspector = null, ModelProfile? modelProfile = null)
{
    internal const int MaxGenerationRequestsPerTurn = 5;
    private int _running;
    private readonly GenerationBudget _budget = generationBudget ?? new();
    private readonly ILlmContextInspector? _inspector = contextInspector ?? client as ILlmContextInspector;

    internal async Task<AgentTurnResult> RunAsync(IReadOnlyList<LlmMessage> initialMessages, CancellationToken cancellationToken, CodeReviewCoordinator? review = null)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return Unavailable(AgentStopReason.RunnerUnavailable);
        try
        {
            using var reviewScope = review is not null && !registry.ExecutionPolicy.Plans.ReviewTurnActive ? registry.ExecutionPolicy.EnterReview() : null;
            using var generation = registry.ExecutionPolicy.Plans.BorrowGeneration();
            if (initialMessages is null || initialMessages.Count == 0 || initialMessages.Any(message => message is null) ||
                initialMessages[^1] is not { Kind: LlmMessageKind.User } user || string.IsNullOrWhiteSpace(user.UserText))
                return Unavailable(AgentStopReason.LocalFailure);
            var turn = new Turn(client, registry, policy, systemInstruction, programUsage, onEvent,
                initialMessages.ToArray(), cancellationToken, _budget, contextCoordinator, _inspector, review: review, profile: modelProfile);
            _budget.BeginConversation();
            if (review is null) registry.ExecutionPolicy.Delegation?.BeginParentTurn(user.UserText!);
            if (review is null) registry.ExecutionPolicy.Mcp?.BeginTurn();
            return await turn.RunAsync();
        }
        catch (Exception) { return Unavailable(AgentStopReason.LocalFailure); }
        finally { registry.ExecutionPolicy.Delegation?.EndParentTurn(); Volatile.Write(ref _running, 0); }
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
                _budget, contextCoordinator, _inspector, true, profile: modelProfile).RunMaintenanceAsync();
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private sealed partial class Turn(ILlmClient client, ToolRegistry registry, ISensitiveDataPolicy policy,
        string instruction, UsageAccumulator programUsage, Action<AgentEvent> onEvent,
        IReadOnlyList<LlmMessage> initial, CancellationToken token, GenerationBudget budget,
        ContextCoordinator? coordinator, ILlmContextInspector? inspector, bool maintenance = false, CodeReviewCoordinator? review = null, ModelProfile? profile = null)
    {
        private readonly object _gate = coordinator?.DecisionGate ?? new();
        private readonly List<LlmMessage> _messages = initial.ToList();
        private readonly List<LlmMessage> _current = initial.Count == 0 ? [] : [initial[^1]];
        private readonly List<LocalToolOutcome> _operations = [];
        private McpOperationStamp? _mcpStamp;
        private static AgentStopReason McpReason(string code) => code is "mcp_changed" or "mcp_catalog_stale" ? AgentStopReason.McpChanged : code is "sensitive_content" or "sensitive_arguments" ? AgentStopReason.SensitiveData : AgentStopReason.McpStopped;
        private readonly UsageAccumulator _usage = new();
        private GenerationGateway? _gateway;
        private GenerationGateway Gateway => _gateway ??= new(_gate, programUsage, _usage, profile);
        private DelegationBudgetLease? _aggregationLease;
        private bool _toolPhaseClosed;
        private bool _delegationResourceBlocked;
        private bool _cancelled;
        private bool _reset;
        private bool _eventFailed;
        private int _generations;
        private ProjectRulesStamp? _rulesStamp;
        private SkillsOperationStamp? _skillsStamp;
        private LlmStatus? _modelStatus;
        private LlmErrorCode? _modelError;
        private readonly long _planGeneration = registry.ExecutionPolicy.Plans.ExecutionGeneration;

        internal async Task<AgentTurnResult> RunAsync()
        {
            using var registration = token.Register(() => { lock (_gate) _cancelled = true; });
            try
            {
                await RequireRulesAsync();
                await PrepareConversationAsync();
                while (true)
                {
                    if (Cancelled) return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                    var result = await GenerateAsync(Request(), review is null ? GenerationPurpose.Conversation : GenerationPurpose.Review);
                    if (result is null) return Invalid();
                    _reset |= result.RequiresContextReset;
                    if (!ValidResult(result)) return Invalid();
                    await RequireReviewAsync();
                    RequireMcpCurrent();
                    _modelStatus = result.Status; _modelError = result.ErrorCode;
                    _reset |= result.ErrorCode is LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall;
                    if (!policy.CanSendOrUse(result.SafeText, ContentBoundary.ModelInput))
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
                        try { if (result.ToolCalls[0].Name != McpProfile.FetchName) await RequireRulesAsync(); }
                        catch (ProjectRulesFlowException ex)
                        { AddRemaining(result.ToolCalls, 0, ex.Code); return Finish(AgentTurnStatus.Stopped, ex.Reason); }
                        catch (McpFailure ex)
                        { AddRemaining(result.ToolCalls, 0, "turn_stopped"); return Finish(AgentTurnStatus.Stopped, McpReason(ex.Code)); }
                        catch (SkillsFlowException ex)
                        { AddRemaining(result.ToolCalls, 0, ex.Code); return Finish(AgentTurnStatus.Stopped, ex.Reason); }
                        catch (SkillsSensitiveException)
                        { _reset = true; AddRemaining(result.ToolCalls, 0, "turn_stopped"); return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
                        catch (OperationCanceledException) when (token.IsCancellationRequested || Cancelled)
                        { AddRemaining(result.ToolCalls, 0, "turn_cancelled"); return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled); }
                        catch (Exception)
                        { AddRemaining(result.ToolCalls, 0, "turn_stopped"); return Finish(AgentTurnStatus.Failed, AgentStopReason.LocalFailure); }
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
                        if (_toolPhaseClosed) return Invalid();
                        if (result.ToolCalls.Any(call => call.Name == DelegateReadonlyTool.Name))
                        {
                            var delegationStopped = await ExecuteDelegationBatchAsync(result);
                            if (delegationStopped is not null) return delegationStopped;
                            continue;
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
                    if (!_reset) await RequireRulesAsync();
                    if (Cancelled || result.Status == LlmStatus.Cancelled)
                        return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                    if (result.Status == LlmStatus.Completed)
                    {
                        if (review is not null && review.Validate(result.SafeText) is null)
                            return Finish(AgentTurnStatus.Failed, AgentStopReason.ReviewInvalid);
                        _current.Add(result.ModelMessage!);
                        if (!ToolCallPairing.ValidTurn(_current)) return Invalid();
                        return Finish(AgentTurnStatus.Completed, AgentStopReason.None, review?.Candidate is { } report ? review.Display(report, false)! : result.SafeText, _current);
                    }
                    return Finish(result.Status == LlmStatus.Failed ? AgentTurnStatus.Failed : AgentTurnStatus.Stopped,
                        AgentStopReason.ModelOutcome, result.Status == LlmStatus.Truncated ? result.SafeText : "");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested || Cancelled)
            { return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled); }
            catch (ProjectRulesFlowException failure) { return Finish(AgentTurnStatus.Stopped, failure.Reason); }
            catch (McpFailure failure) { if (failure.Code is "sensitive_content" or "sensitive_arguments") _reset = true; return Finish(AgentTurnStatus.Stopped, McpReason(failure.Code)); }
            catch (SkillsFlowException failure) { return Finish(AgentTurnStatus.Stopped, failure.Reason); }
            catch (SkillsSensitiveException) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
            catch (ProjectRulesSensitiveException) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
            catch (ContextFlowException failure) { return Finish(failure.Status, failure.Reason); }
            catch (ContextCapacityException) { return Finish(AgentTurnStatus.Stopped, AgentStopReason.ContextCapacity); }
            catch (ContextInspectionException) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.InvalidProtocol); }
            catch (GitFailure failure)
            { if (failure.Code == "sensitive_content") _reset = true; return Finish(AgentTurnStatus.Stopped, failure.Code == "sensitive_content" ? AgentStopReason.SensitiveData : failure.Code is "process_cleanup_failed" or "process_unconfirmed" ? AgentStopReason.ProcessUnconfirmed : failure.Code is "result_too_large" or "context_capacity" ? AgentStopReason.ContextCapacity : AgentStopReason.GitStopped); }
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
                var preflight = registry.PreflightFetch(call, _requestRegistry ?? registry.CaptureSnapshot());
                try { if (preflight is null) await RequireRulesAsync(); }
                catch (ProjectRulesFlowException ex)
                { AddRemaining(calls, index, ex.Code); return Finish(AgentTurnStatus.Stopped, ex.Reason); }
                catch (McpFailure ex)
                { AddRemaining(calls, index, "turn_stopped"); return Finish(AgentTurnStatus.Stopped, McpReason(ex.Code)); }
                catch (SkillsFlowException ex)
                { AddRemaining(calls, index, ex.Code); return Finish(AgentTurnStatus.Stopped, ex.Reason); }
                catch (SkillsSensitiveException)
                { _reset = true; AddRemaining(calls, index, "turn_stopped"); return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
                catch (OperationCanceledException) when (token.IsCancellationRequested || Cancelled)
                { AddRemaining(calls, index, "turn_cancelled"); return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled); }
                catch (Exception)
                { AddRemaining(calls, index, "turn_stopped"); return Finish(AgentTurnStatus.Failed, AgentStopReason.LocalFailure); }
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
                try { result = preflight ?? await registry.DispatchAsync(call, _requestRegistry ?? registry.CaptureSnapshot(), token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                { result = ToolResponseWriter.Create(ToolStatus.Cancelled, "none", policy); }
                catch (Exception)
                { result = ToolResponseWriter.Create(ToolStatus.Failed, "unavailable", policy); localFailure = true; }

                AddOperation(call, result);
                if (result.Response is null || registry.ExecutionPolicy.Skills?.RequiresContextReset == true || registry.ExecutionPolicy.Mcp?.RequiresContextReset == true) _reset = true;
                var reason = result.ProcessCleanupFailed || result.ProcessSummary?.EndState == ProcessEndState.Unconfirmed ||
                    result.ErrorCode is "process_unconfirmed" or "process_cleanup_failed" ? AgentStopReason.ProcessUnconfirmed
                    : localFailure || registry.ExecutionPolicy.Skills?.FatalFailure == true || registry.ExecutionPolicy.Rules?.FatalFailure == true || _eventFailed || result.CleanupFailed || result.ErrorCode is "unavailable" or "checkpoint_unavailable" ? AgentStopReason.LocalFailure
                    : result.WriteState == FileWriteState.Unconfirmed ? AgentStopReason.WriteUnconfirmed
                    : registry.ExecutionPolicy.Skills?.RequiresContextReset == true || result.Response is null || result.ErrorCode is "sensitive_arguments" or "sensitive_content" ? AgentStopReason.SensitiveData
                    : registry.ExecutionPolicy.Mcp?.RequiresContextReset == true ? AgentStopReason.SensitiveData
                    : result.McpOutcome is not null && result.StopAfterOperation ? McpReason(result.ErrorCode)
                    : result.ErrorCode == "mcp_changed" ? AgentStopReason.McpChanged
                    : result.ErrorCode == "process_limit" ? AgentStopReason.ProcessResourceLimit
                    : result.ErrorCode == "workspace_unavailable" ? AgentStopReason.WorkspaceUnavailable
                    : result.ErrorCode is "rules_unavailable" or "rules_changed" or "rules_scope_mismatch" or "rules_limit" ? new ProjectRulesFlowException(result.ErrorCode).Reason
                    : result.ErrorCode is "skills_changed" or "skills_unavailable" or "skills_limit" ? new SkillsFlowException(result.ErrorCode).Reason
                    : registry.ExecutionPolicy.Skills?.Capture().Status == SkillsStatus.Stale ? AgentStopReason.SkillsChanged
                    : registry.ExecutionPolicy.Rules?.Capture().Status == ProjectRulesStatus.Stale ? AgentStopReason.RulesChanged
                    : result.Status == ToolStatus.Cancelled || Cancelled ? AgentStopReason.TurnCancelled
                    : result.ErrorCode == "approval_declined" ? AgentStopReason.ApprovalDeclined
                    : result.ErrorCode == "checkpoint_limit" ? AgentStopReason.TaskResourceLimit
                    : result.ErrorCode is "conflict" or "checkpoint_git_conflict" || result.StopAfterOperation && result.ErrorCode is "path_changed" or "symbolic_link" or "sensitive_path" ? AgentStopReason.FileConflict
                    : result.WriteState is not null && (result.Status == ToolStatus.Failed || result.StopAfterOperation || result.ErrorCode == "publish_unsupported") ? AgentStopReason.FileWriteFailed
                    : result.ErrorCode == "mode_restricted" ? AgentStopReason.ModeRestricted
                    : result.GitSummary is not null && result.StopAfterOperation ? AgentStopReason.GitStopped
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
                        reason is AgentStopReason.McpStopped or AgentStopReason.McpChanged or AgentStopReason.SkillsChanged or AgentStopReason.SkillsUnavailable or AgentStopReason.SkillsLimit or AgentStopReason.RulesUnavailable or AgentStopReason.RulesChanged or AgentStopReason.RulesScopeMismatch or AgentStopReason.RulesLimit or AgentStopReason.WorkspaceUnavailable or AgentStopReason.ApprovalDeclined or AgentStopReason.FileConflict or AgentStopReason.WriteUnconfirmed or
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

        private void RequireMcpCurrent()
        {
            var mcp = registry.ExecutionPolicy.Mcp;
            if (mcp is null) return;
            if (review is null) mcp.RequireReadyForGeneration();
            if (_mcpStamp is { } stamp && !mcp.IsCurrent(stamp)) throw new McpFailure("mcp_changed");
            _mcpStamp ??= mcp.Capture();
        }
        private async Task RequireRulesAsync()
        {
            RequireMcpCurrent();
            var rules = registry.ExecutionPolicy.Rules;
            if (registry.ExecutionPolicy.WorkspaceOperations.IsDisposed || rules?.FatalFailure == true) throw new InvalidOperationException("工作區來源服務無法使用");
            var error = rules is null ? "rules_unavailable" : await rules.ValidateAsync(token);
            if (error != "none") throw new ProjectRulesFlowException(error);
            var current = rules!.Capture();
            if (_rulesStamp is { } previous && (previous.RulesGeneration != current.RulesGeneration || previous.RulesRevision != current.RulesRevision || previous.SessionGeneration != current.SessionGeneration))
                throw new ProjectRulesFlowException("rules_changed");
            _rulesStamp ??= current;
            var skills = registry.ExecutionPolicy.Skills;
            if (skills?.FatalFailure == true) throw new InvalidOperationException("技能來源服務無法使用");
            var skillsError = skills is null ? "skills_unavailable" : await skills.ValidateAsync(token);
            if (skillsError != "none") throw new SkillsFlowException(skillsError);
            if (_skillsStamp is { } previousSkill && !skills!.IsCurrent(previousSkill)) throw new SkillsFlowException("skills_changed");
            _skillsStamp ??= skills!.Capture();
        }

        private async Task RequireReviewAsync()
        {
            if (review is null) return;
            await RequireRulesAsync();
            var error = await review.Snapshots.ValidateAsync(token);
            if (error is "rules_unavailable" or "rules_changed" or "rules_scope_mismatch" or "rules_limit") throw new ProjectRulesFlowException(error);
            if (error != "none") { review.Failed(error); throw new GitFailure(error); }
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
        private bool Cancelled
        {
            get
            {
                lock (_gate) return _cancelled || token.IsCancellationRequested || (_planGeneration != registry.ExecutionPolicy.Plans.Generation && (_mcpStamp is null || registry.ExecutionPolicy.Mcp?.IsCurrent(_mcpStamp) == true) && registry.ExecutionPolicy.Skills?.Capture().Status != SkillsStatus.Stale &&
            (_skillsStamp is null || registry.ExecutionPolicy.Skills?.Capture().SkillsRevision == _skillsStamp.SkillsRevision) && registry.ExecutionPolicy.Rules?.Capture().Status != ProjectRulesStatus.Stale &&
            (_rulesStamp is null || registry.ExecutionPolicy.Rules?.Capture().RulesRevision == _rulesStamp.RulesRevision));
            }
        }

        private AgentTurnResult Invalid()
        {
            _reset = true; _modelStatus = null; _modelError = null;
            return Finish(AgentTurnStatus.Failed, AgentStopReason.InvalidProtocol);
        }

        private AgentTurnResult Finish(AgentTurnStatus status, AgentStopReason reason, string text = "",
            IReadOnlyList<LlmMessage>? completed = null)
        {
            if (Cancelled && reason is not (AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable or AgentStopReason.WriteUnconfirmed or AgentStopReason.ProcessUnconfirmed or AgentStopReason.DelegationUnconfirmed))
            { status = AgentTurnStatus.Cancelled; reason = AgentStopReason.TurnCancelled; text = ""; completed = null; }
            if (_eventFailed) { status = AgentTurnStatus.Failed; reason = AgentStopReason.LocalFailure; text = ""; completed = null; }
            if (!maintenance) registry.ExecutionPolicy.Plans.BlockInProgress((_delegationResourceBlocked ? AgentStopReason.TaskResourceLimit : reason) switch
            {
                AgentStopReason.McpChanged => "mcp_changed",
                AgentStopReason.SkillsChanged or AgentStopReason.SkillsUnavailable or AgentStopReason.SkillsLimit => "skills_changed",
                AgentStopReason.RulesChanged => "rules_changed",
                AgentStopReason.RulesUnavailable => "rules_unavailable",
                AgentStopReason.RulesScopeMismatch => "rules_scope_mismatch",
                AgentStopReason.RulesLimit => "rules_limit",
                AgentStopReason.None when status == AgentTurnStatus.Completed => "turn_paused",
                AgentStopReason.TurnCancelled => "turn_cancelled",
                AgentStopReason.GenerationLimit => "generation_limit",
                AgentStopReason.ProgramGenerationLimit or AgentStopReason.ContextCapacity or AgentStopReason.ContextCompactionFailed => "resource_limit",
                AgentStopReason.ApprovalDeclined => "approval_declined",
                AgentStopReason.FileConflict => "file_conflict",
                AgentStopReason.WriteUnconfirmed => "write_unconfirmed",
                AgentStopReason.WorkspaceUnavailable => "workspace_unavailable",
                AgentStopReason.TaskResourceLimit => "resource_limit",
                AgentStopReason.TaskBlocked or AgentStopReason.DelegationStopped => "operation_failed",
                AgentStopReason.DelegationUnconfirmed => "local_failure",
                AgentStopReason.ProcessResourceLimit => "resource_limit",
                AgentStopReason.ProcessUnconfirmed => "process_unconfirmed",
                AgentStopReason.ProcessFailed => ProcessBlockReason(),
                AgentStopReason.LocalFailure or AgentStopReason.RunnerUnavailable => "local_failure",
                _ => "model_outcome"
            });
            return new(status, reason, _modelStatus, _modelError, text,
                status == AgentTurnStatus.Completed ? completed ?? [] : [], _operations, maintenance ? _generations : budget.Snapshot().TurnGenerationsStarted, Gateway.Snapshot(_usage), _reset, _rulesStamp, _skillsStamp, _mcpStamp);
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