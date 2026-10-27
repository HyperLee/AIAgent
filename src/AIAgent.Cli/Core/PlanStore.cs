using System.Globalization;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

/// <summary>獨立於對話歷史的有界帳本；鎖內只有同步、純記憶體決策。</summary>
internal sealed partial class PlanStore(ISensitiveDataPolicy policy, object? decisionGate = null)
{
    private readonly object _gate = decisionGate ?? new();
    private readonly List<TaskOperationFact> _facts = [];
    private readonly HashSet<TaskEvidenceReservation> _reservations = [];
    private readonly AsyncLocal<long?> _borrowedGeneration = new();
    private ISensitiveDataPolicy? _policy = policy;
    private PlanSnapshot? _plan;
    private AgentMode _mode;
    private long _generation;
    private long _planSequence;
    private long _taskSequence;
    private long _operationSequence;
    private long _evidenceEpoch;
    private CheckpointRecoveryState? _recovery;
    internal CheckpointRecoveryState Recovery { get { lock (_gate) return _recovery ??= new(this, () => _policy); } }
    internal long EvidenceEpoch { get { lock (_gate) return _evidenceEpoch; } }
    internal bool CanRecordRecovery { get { lock (_gate) return _policy is not null && _evidenceEpoch < long.MaxValue && _generation < long.MaxValue && _reservations.Count == 0; } }
    internal void RecordRecovery()
    {
        lock (_gate)
        {
            var epoch = checked(_evidenceEpoch + 1); var generation = checked(_generation + 1);
            _evidenceEpoch = epoch; _generation = generation;
            BlockCore("workspace_restored");
            if (_plan is not null) _plan = new(_plan.PlanId, _plan.Revision, _plan.Objective, PlanApprovalState.Draft, _plan.Tasks);
            _mode = AgentMode.Plan;
        }
    }
    internal CheckpointSequence CheckpointSequence { get; } = new();
    internal object DecisionGate => _gate;
    internal long CurrentRulesRevision { get; set; }
    internal long CurrentSkillsRevision { get; set; }
    internal SkillsCoordinator? Skills { get; set; }
    internal void RevokeForSkills()
    {
        lock (_gate)
        {
            if (_reservations.Count != 0) throw new InvalidOperationException("操作仍在收尾");
            _generation = checked(_generation + 1);
            BlockCore("skills_changed");
            if (_plan is not null) _plan = new(_plan.PlanId, _plan.Revision, _plan.Objective, PlanApprovalState.Draft, _plan.Tasks);
        }
    }
    internal bool HasReservations { get { lock (_gate) return _reservations.Count != 0; } }
    internal void RevokeForRules()
    {
        lock (_gate)
        {
            if (_reservations.Count != 0) throw new InvalidOperationException("操作仍在收尾");
            _generation = checked(_generation + 1);
            if (_plan is not null)
            {
                BlockCore("rules_changed");
                _plan = new(_plan.PlanId, _plan.Revision, _plan.Objective, PlanApprovalState.Draft, _plan.Tasks);
            }
        }
    }
    internal bool ProcessCapabilityAvailable { get; set; }
    internal bool ProcessTestCapabilityAvailable { get; set; } = true;
    internal AgentMode Mode { get { lock (_gate) return _mode; } }
    internal long Generation { get { lock (_gate) return _generation; } }
    internal long ExecutionGeneration { get { lock (_gate) return _borrowedGeneration.Value ?? _generation; } }
    internal bool IsExecutionCurrent { get { lock (_gate) return _policy is not null && Current; } }
    internal PlanSnapshot? Snapshot { get { lock (_gate) return _plan; } }
    internal IReadOnlyList<TaskOperationFact> Facts { get { lock (_gate) return Array.AsReadOnly(_facts.ToArray()); } }
    internal bool HasInProgress { get { lock (_gate) return _plan?.Tasks.Any(t => t.State == PlanTaskState.InProgress) == true; } }
    internal IDisposable BorrowGeneration(long? generation = null)
    {
        var previous = _borrowedGeneration.Value;
        _borrowedGeneration.Value = previous ?? generation ?? Generation;
        return new GenerationLease(this, previous);
    }
    private bool Current => _borrowedGeneration.Value is not { } borrowed || borrowed == _generation;

    internal void BindPolicy(ISensitiveDataPolicy value)
    {
        lock (_gate)
        {
            if (_plan is not null || _facts.Count != 0 || _reservations.Count != 0) throw new InvalidOperationException("計畫狀態尚未清除");
            _policy = value;
        }
    }

    internal void SetMode(AgentMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("模式無效");
        lock (_gate)
        {
            if (_policy is null) return;
            if (mode == AgentMode.Plan) BlockCore("mode_changed");
            _mode = mode;
        }
    }

    internal PlanMutationResult Submit(JsonElement arguments, CancellationToken token = default)
    {
        lock (_gate)
        {
            if (_policy is null || !Current) return new("unavailable");
            if (token.IsCancellationRequested) return new("turn_cancelled");
            if (ReviewTurnActive || _mode != AgentMode.Plan) return new("mode_restricted");
            if (!PlanArguments.Submit(arguments, _policy, out var expected, out var objective, out var tasks, out var error)) return new(error);
            if (expected != (_plan?.Revision ?? 0)) return new("plan_revision_conflict");
            var newPlan = _plan is null || _plan.Objective != objective;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var task in tasks.Where(t => t.TaskId.Length != 0))
            {
                if (newPlan || !seen.Add(task.TaskId) || _plan!.Tasks.All(t => t.TaskId != task.TaskId)) return new("task_not_found");
            }
            var nextPlan = _planSequence; var nextTask = _taskSequence;
            string planId; long revision;
            try
            {
                planId = newPlan ? Id("p", checked(++nextPlan)) : _plan!.PlanId;
                var revised = new List<PlanTaskSnapshot>();
                foreach (var task in tasks)
                {
                    var prior = _plan?.Tasks.SingleOrDefault(t => t.TaskId == task.TaskId);
                    revised.Add(!newPlan && prior is not null && prior.SameDefinition(task)
                        ? new(prior.TaskId, prior.Kind, task.Title, task.Acceptance, task.Requirements,
                            prior.State, prior.Summary, prior.BlockReason, prior.CompletionBasis, prior.CompletionFactIds, task.RequirementsVersion, prior.CompletionRulesRevision, prior.CompletionEvidenceEpoch)
                        : new(Id("t", checked(++nextTask)), task.Kind, task.Title, task.Acceptance, task.Requirements, requirementsVersion: task.RequirementsVersion));
                }
                if (!newPlan && revised.Count == _plan!.Tasks.Count && revised.Zip(_plan.Tasks).All(pair =>
                    pair.First.TaskId == pair.Second.TaskId && pair.First.Requirements.Zip(pair.Second.Requirements).All(r => r.First.Matches(r.Second))))
                    return new("no_change");
                revision = newPlan ? 1 : checked(_plan!.Revision + 1);
                var candidate = new PlanSnapshot(planId, revision, objective, PlanApprovalState.Draft, revised);
                var result = new PlanMutationResult("none", candidate);
                error = ValidateCandidate(candidate, result, true);
                if (error != "none") return new(error);
                if (token.IsCancellationRequested) return new("turn_cancelled");
                _planSequence = nextPlan; _taskSequence = nextTask; _plan = candidate;
                return result;
            }
            catch (OverflowException) { return new("unavailable"); }
        }
    }

    internal PlanMutationResult Update(JsonElement arguments, CancellationToken token = default)
    {
        lock (_gate)
        {
            if (_policy is null || !Current) return new("unavailable");
            if (token.IsCancellationRequested) return new("turn_cancelled");
            if (ReviewTurnActive || _mode != AgentMode.Execute) return new("mode_restricted");
            if (!PlanArguments.Update(arguments, _policy, out var planId, out var revision, out var taskId, out var state, out var summary, out var error)) return new(error);
            if (_plan is null) return new("plan_required");
            if (_plan.PlanId != planId || _plan.Revision != revision) return new("plan_revision_conflict");
            var task = _plan.Tasks.SingleOrDefault(t => t.TaskId == taskId);
            if (task is null) return new("task_not_found");
            if (_plan.ApprovalState != PlanApprovalState.Approved) return new("plan_not_approved");
            if (task.Kind == PlanTaskKind.Validation && (task.RequirementsVersion == 1 || !ProcessCapabilityAvailable || !ProcessTestCapabilityAvailable && task.Requirements.Any(r => r.Task == DotnetTaskKind.Test)) && state == PlanTaskState.Completed) return new("capability_unavailable");
            TaskOperationFact? analysis = null;
            PlanTaskSnapshot updated;
            if (state == PlanTaskState.InProgress)
            {
                if (task.State is not (PlanTaskState.Pending or PlanTaskState.Blocked)) return new("invalid_task_transition");
                if (_plan.Tasks.Any(t => t.State == PlanTaskState.InProgress)) return new("task_in_progress");
                updated = task.Kind == PlanTaskKind.Validation && (task.RequirementsVersion == 1 || !ProcessCapabilityAvailable || !ProcessTestCapabilityAvailable && task.Requirements.Any(r => r.Task == DotnetTaskKind.Test)) ? task.WithState(PlanTaskState.Blocked, summary, "capability_unavailable") : task.WithState(state, summary);
            }
            else
            {
                if (task.State != PlanTaskState.InProgress) return new("invalid_task_transition");
                if (state == PlanTaskState.Blocked) updated = task.WithState(state, summary, "model_reported");
                else if (task.Kind == PlanTaskKind.Analysis)
                {
                    if (_facts.Count + _reservations.Count >= PlanLimits.MaxFacts) return new("evidence_limit");
                    if (_operationSequence >= long.MaxValue - _reservations.Count) return new("unavailable");
                    if (!_policy.CanSendOrUse(Id("op", _operationSequence + _reservations.Count + 1), ContentBoundary.ToolResult)) return new("sensitive_content");
                    var operationId = Id("op", _operationSequence + 1);
                    analysis = new(operationId, new(_plan.PlanId, _plan.Revision, task.TaskId, CurrentRulesRevision), TaskCompletionBasis.ModelAssessment,
                        null, ToolStatus.Success, "none", summary: summary, evidenceEpoch: _evidenceEpoch);
                    updated = task.WithState(state, summary, basis: TaskCompletionBasis.ModelAssessment, facts: [operationId], rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch);
                }
                else
                {
                    var selected = new List<string>();
                    foreach (var requirement in task.Requirements)
                    {
                        var fact = _facts.LastOrDefault(f => f.Context.PlanId == _plan.PlanId && f.Context.TaskId == task.TaskId && f.Context.RulesRevision == CurrentRulesRevision && f.EvidenceEpoch == _evidenceEpoch &&
                            f.Kind == TaskCompletionBasis.LocalOperation && f.Request is { } request && request.Matches(requirement));
                        if (fact is null || fact.Status != ToolStatus.Success || fact.CleanupFailed ||
                            (task.Kind == PlanTaskKind.Validation ? !SuccessfulProcess(fact, requirement) : fact.Truncated) ||
                            task.Kind == PlanTaskKind.Write && fact.WriteState != FileWriteState.Verified || selected.Contains(fact.OperationId, StringComparer.Ordinal))
                            return new("evidence_required");
                        selected.Add(fact.OperationId);
                    }
                    updated = task.WithState(state, summary, basis: TaskCompletionBasis.LocalOperation, facts: selected, rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch);
                }
            }
            var candidate = _plan.WithTasks(_plan.Tasks.Select(t => t.TaskId == taskId ? updated : t).ToArray());
            var result = new PlanMutationResult("none", candidate, updated);
            error = ValidateCandidate(candidate, result, false);
            if (error != "none") return new(error);
            if (token.IsCancellationRequested) return new("turn_cancelled");
            _plan = candidate;
            if (analysis is not null) { _operationSequence++; _facts.Add(analysis); }
            return result;
        }
    }

    private string ValidateCandidate(PlanSnapshot plan, PlanMutationResult result, bool submit)
    {
        var actual = PlanProjection.State(_mode, plan, rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision); var upper = PlanProjection.State(_mode, plan, true, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision);
        if (actual.Length > PlanLimits.MaxProjectionBytes || upper.Length > PlanLimits.MaxProjectionBytes ||
            Encoding.UTF8.GetByteCount(PlanProjection.Display(_mode, plan, 256, upper: true, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision)) > PlanLimits.MaxDisplayBytes ||
            Encoding.UTF8.GetByteCount(PlanProjection.Display(_mode, plan, 256, true, true, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision)) > PlanLimits.MaxDisplayBytes)
            return "plan_too_large";
        var resultBytes = PlanProjection.ResultBytes(result, submit);
        if (resultBytes.Length > PlanLimits.MaxDisplayBytes) return "plan_too_large";
        return PlanProjection.SafeJson(actual, _policy!, ContentBoundary.ModelInput) &&
            PlanProjection.SafeJson(resultBytes, _policy!, ContentBoundary.ToolResult) &&
            PlanProjection.SafeDisplay(PlanProjection.Display(_mode, plan, OldFactCount(plan), evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision), _policy!) &&
            PlanProjection.SafeDisplay(PlanProjection.Display(_mode, plan, OldFactCount(plan), true, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision), _policy!) ? "none" : "sensitive_content";
    }

    internal PlanApprovalPreview? ApprovalPreview()
    {
        lock (_gate)
        {
            if (_policy is null || _plan is null) return null;
            var block = PlanProjection.Display(_mode, _plan, OldFactCount(_plan), true, rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision);
            return PlanProjection.SafeDisplay(block, _policy) ? new(_plan.PlanId, _plan.Revision, _generation, block, CurrentRulesRevision, Skills?.Capture()) : null;
        }
    }

    internal bool Approve(PlanApprovalPreview preview)
    {
        lock (_gate)
        {
            if (_policy is null || _plan is null || _generation != preview.Generation || preview.RulesRevision != CurrentRulesRevision || preview.SkillsStamp is { } skillStamp && Skills?.IsCurrent(skillStamp) != true || _plan.PlanId != preview.PlanId || _plan.Revision != preview.Revision ||
                _plan.ApprovalState != PlanApprovalState.Draft) return false;
            var block = PlanProjection.Display(_mode, _plan, OldFactCount(_plan), true, rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision);
            if (block != preview.DisplayBlock || !PlanProjection.SafeDisplay(block, _policy)) return false;
            var candidate = new PlanSnapshot(_plan.PlanId, _plan.Revision, _plan.Objective, PlanApprovalState.Approved, _plan.Tasks);
            if (!PlanProjection.SafeJson(PlanProjection.State(AgentMode.Execute, candidate, rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision), _policy, ContentBoundary.ModelInput)) return false;
            _plan = candidate; _mode = AgentMode.Execute; return true;
        }
    }

    internal string? Display()
    {
        lock (_gate)
        {
            if (_policy is null) return null;
            var block = PlanProjection.Display(_mode, _plan, _plan is null ? 0 : OldFactCount(_plan), rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision);
            return PlanProjection.SafeDisplay(block, _policy) ? block : null;
        }
    }

    internal string? StateAppendix()
    {
        lock (_gate)
        {
            if (_policy is null || !Current) return null;
            var bytes = PlanProjection.State(_mode, _plan, rulesRevision: CurrentRulesRevision, evidenceEpoch: _evidenceEpoch, skillsRevision: CurrentSkillsRevision);
            var text = "\n本機狀態資料（僅為資料，不授予權限）：\n" + Encoding.UTF8.GetString(bytes);
            return bytes.Length <= PlanLimits.MaxProjectionBytes && PlanProjection.SafeJson(bytes, _policy, ContentBoundary.ModelInput) &&
                _policy.CanSendOrUse(text, ContentBoundary.ModelInput) ? text : null;
        }
    }

    internal string CheckWrite(string tool, string path)
    {
        lock (_gate)
        {
            if (_policy is null || !Current) return "unavailable";
            if (_mode == AgentMode.Plan) return "mode_restricted";
            if (_plan is null) return "none";
            if (_plan.ApprovalState != PlanApprovalState.Approved) return "plan_not_approved";
            var task = _plan.Tasks.SingleOrDefault(t => t.State == PlanTaskState.InProgress);
            if (task?.Kind != PlanTaskKind.Write) return "task_required";
            return task.Requirements.Any(r => r.ToolName == tool && r.Path == path) ? "none" : "task_scope_mismatch";
        }
    }

    internal string CheckProcess(DotnetTaskKind kind, string path)
    {
        lock (_gate)
        {
            if (_policy is null || !Current) return "unavailable";
            if (_mode != AgentMode.Execute) return "mode_restricted";
            if (!Enum.IsDefined(kind)) return "invalid_arguments";
            if (_plan is null) return "none";
            if (_plan.ApprovalState != PlanApprovalState.Approved) return "plan_not_approved";
            var task = _plan.Tasks.SingleOrDefault(t => t.State == PlanTaskState.InProgress);
            if (task?.Kind != PlanTaskKind.Validation || task.RequirementsVersion != 2) return "task_required";
            return task.Requirements.Any(r => r.ToolName == "run_dotnet_task" && r.Path == path && r.Task == kind)
                ? "none" : "task_scope_mismatch";
        }
    }

    internal string ReserveProcess(DotnetTaskKind kind, string path, out TaskEvidenceReservation? reservation)
    {
        lock (_gate)
        {
            reservation = null;
            var error = CheckProcess(kind, path);
            if (error != "none") return error;
            return Reserve(new("run_dotnet_task", path, task: kind), false, out reservation);
        }
    }

    internal void RecordProcess(TaskEvidenceReservation? reservation, ToolStatus status, string errorCode, ProcessExecutionSummary summary)
    {
        if (reservation is null) return;
        lock (_gate)
        {
            if (summary.StartState == ProcessStartState.NotStarted && summary.EndState != ProcessEndState.Unconfirmed)
            {
                if (_reservations.Remove(reservation) && _policy is not null && reservation.Generation == _generation &&
                    _plan?.PlanId == reservation.Context.PlanId && _plan.Revision == reservation.Context.PlanRevision &&
                    _plan.Tasks.Any(t => t.TaskId == reservation.Context.TaskId)) BlockCore(ProcessReason(status, errorCode, summary));
                return;
            }
            if (!_reservations.Remove(reservation) || _policy is null || reservation.Generation != _generation ||
                _plan?.PlanId != reservation.Context.PlanId || _plan.Revision != reservation.Context.PlanRevision ||
                _plan.Tasks.All(t => t.TaskId != reservation.Context.TaskId) || reservation.Request.ToolName != "run_dotnet_task" ||
                reservation.Request.Task != summary.Task || reservation.Request.Path != summary.ProjectPath) return;
            var safe = SafeProcess(summary, _policy);
            var safeCode = _policy.CanSendOrUse(errorCode, ContentBoundary.ToolResult) ? errorCode : "";
            _facts.Add(new(Id("op", checked(++_operationSequence)), reservation.Context, TaskCompletionBasis.LocalOperation,
                reservation.Request, safe ? status : ToolStatus.Failed, safeCode,
                processSummary: safe ? summary : null, evidenceEpoch: reservation.EvidenceEpoch));
            if (status != ToolStatus.Success || !safe || !SuccessfulProcess(_facts[^1], reservation.Request))
                BlockCore(ProcessReason(status, errorCode, summary));
        }
    }

    private static bool SafeProcess(ProcessExecutionSummary value, ISensitiveDataPolicy policy)
    {
        string[] values = [value.ProjectPath, PlanProjection.Name(value.Task), PlanProjection.Name(value.StartState),
            PlanProjection.Name(value.EndState), PlanProjection.Name(value.ValidationOutcome), value.OutputState,
            value.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture), value.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "null",
            value.StdoutBytes.ToString(CultureInfo.InvariantCulture), value.StderrBytes.ToString(CultureInfo.InvariantCulture)];
        return values.All(v => policy.CanSendOrUse(v, ContentBoundary.ToolResult)) &&
            (value.Tests is not { } tests || new[] { tests.Total, tests.Passed, tests.Failed, tests.Skipped, tests.Executed }
                .All(n => policy.CanSendOrUse(n.ToString(CultureInfo.InvariantCulture), ContentBoundary.ToolResult)));
    }

    private static bool SuccessfulProcess(TaskOperationFact fact, TaskRequirement request) =>
        fact.ErrorCode == "none" && fact.ProcessSummary is
        {
            StartState: ProcessStartState.Started, EndState: ProcessEndState.Exited, ExitCode: 0,
            CleanupConfirmed: true, CleanupFailed: false, OutputState: "safe"
        } summary &&
        summary.Task == request.Task && summary.ProjectPath == request.Path &&
        (request.Task == DotnetTaskKind.Build ? summary.ValidationOutcome == ValidationOutcome.BuildSucceeded :
            summary.ValidationOutcome == ValidationOutcome.TestsPassed && summary.Tests is { Executed: > 0, Failed: 0 });

    private static string ProcessReason(ToolStatus status, string error, ProcessExecutionSummary summary) =>
        summary.CleanupFailed || summary.EndState == ProcessEndState.Unconfirmed ? "process_unconfirmed" : error switch
        {
            "process_start_failed" or "process_failed" or "process_timed_out" or "process_output_limit" or "validation_inconclusive" => error,
            "approval_declined" => "approval_declined",
            "capability_unavailable" => "capability_unavailable",
            "process_limit" => "resource_limit",
            _ => status == ToolStatus.Cancelled ? "turn_cancelled" : "process_failed"
        };

    internal string Reserve(TaskRequirement request, bool write, out TaskEvidenceReservation? reservation)
    {
        lock (_gate)
        {
            reservation = null;
            if (_policy is null || !Current) return "unavailable";
            if (write && CheckWrite(request.ToolName, request.Path) is { } error && error != "none") return error;
            var task = _plan?.Tasks.SingleOrDefault(t => t.State == PlanTaskState.InProgress);
            if (task is null) return "none";
            if (_facts.Count + _reservations.Count >= PlanLimits.MaxFacts) { BlockCore("resource_limit"); return "evidence_limit"; }
            if (_operationSequence >= long.MaxValue - _reservations.Count) { BlockCore("local_failure"); return "unavailable"; }
            if (!_policy.CanSendOrUse(Id("op", _operationSequence + _reservations.Count + 1), ContentBoundary.ToolResult)) return "sensitive_content";
            reservation = new(_generation, new(_plan!.PlanId, _plan.Revision, task.TaskId, CurrentRulesRevision), request, _evidenceEpoch);
            _reservations.Add(reservation); return "none";
        }
    }

    internal void Release(TaskEvidenceReservation? reservation)
    { if (reservation is not null) lock (_gate) _reservations.Remove(reservation); }

    internal void Record(TaskEvidenceReservation? reservation, ToolStatus status, string errorCode, bool truncated = false,
        FileWriteState? writeState = null, bool cleanupFailed = false, long? bytes = null, int? count = null, WorkspaceDiscoverySummary? discovery = null)
    {
        if (reservation is null) return;
        lock (_gate)
        {
            if (!_reservations.Remove(reservation) || _policy is null || reservation.Generation != _generation ||
                _plan?.PlanId != reservation.Context.PlanId || _plan.Revision != reservation.Context.PlanRevision ||
                _plan.Tasks.All(t => t.TaskId != reservation.Context.TaskId)) return;
            // All strings are validated before reservation; never store arguments or file contents.
            var safeCode = _policy.CanSendOrUse(errorCode, ContentBoundary.ToolResult) ? errorCode :
                _policy.CanSendOrUse("sensitive_content", ContentBoundary.ToolResult) ? "sensitive_content" : "";
            _facts.Add(new(Id("op", checked(++_operationSequence)), reservation.Context, TaskCompletionBasis.LocalOperation,
                reservation.Request, status, safeCode, truncated, writeState, cleanupFailed, bytes, count, discovery, evidenceEpoch: reservation.EvidenceEpoch));
            if (status != ToolStatus.Success) BlockCore(errorCode switch
            {
                "checkpoint_limit" => "resource_limit",
                "approval_declined" => "approval_declined",
                "conflict" => "file_conflict",
                "write_unconfirmed" => "write_unconfirmed",
                "workspace_unavailable" => "workspace_unavailable",
                "rules_changed" or "rules_unavailable" or "rules_scope_mismatch" or "rules_limit" or "skills_changed" => errorCode,
                _ => status == ToolStatus.Cancelled ? "turn_cancelled" : "operation_failed"
            });
        }
    }

    internal bool ReviewTurnActive { get; set; }
    internal void BlockInProgress(string reason) { lock (_gate) { if (Current && _policy is not null && (!ReviewTurnActive || reason is "rules_changed" or "rules_unavailable" or "rules_scope_mismatch" or "rules_limit" or "skills_changed")) BlockCore(reason); } }
    private void BlockCore(string reason)
    {
        if (_plan is null) return;
        _plan = _plan.WithTasks(_plan.Tasks.Select(t => t.State == PlanTaskState.InProgress ? t.WithState(PlanTaskState.Blocked, t.Summary, reason) : t).ToArray());
    }
    internal void Clear()
    {
        lock (_gate)
        {
            _generation = checked(_generation + 1); _plan = null; _facts.Clear(); _reservations.Clear(); _recovery?.Clear();
        }
    }
    internal void Stop() { lock (_gate) { Clear(); _policy = null; } }
    private int OldFactCount(PlanSnapshot plan) => _facts.Count(f => f.EvidenceEpoch != _evidenceEpoch || f.Context.RulesRevision != CurrentRulesRevision || f.Context.PlanId != plan.PlanId || f.Context.PlanRevision != plan.Revision ||
        plan.Tasks.All(t => t.TaskId != f.Context.TaskId));
    private static string Id(string prefix, long number) => prefix + number.ToString(CultureInfo.InvariantCulture);
    private sealed class GenerationLease(PlanStore owner, long? previous) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) owner._borrowedGeneration.Value = previous; }
    }
    public override string ToString() => "[PlanStore]";
}

internal sealed class AgentExecutionPolicy(PlanStore plans)
{
    private int _processStopped;
    internal bool ProcessSessionStopped => Volatile.Read(ref _processStopped) != 0;
    internal void StopProcesses() => Interlocked.Exchange(ref _processStopped, 1);
    internal PlanStore Plans { get; } = plans;
    internal CheckpointRecoveryState Recovery => Plans.Recovery;
    internal ProjectRulesCoordinator? Rules { get; set; }
    internal SkillsCoordinator? Skills { get; set; }
    internal AgentMode Mode => Plans.Mode;
    internal WorkspaceOperationCoordinator WorkspaceOperations { get; } = new();
    internal bool ProcessTestCapabilityAvailable
    {
        get => Plans.ProcessTestCapabilityAvailable;
        set => Plans.ProcessTestCapabilityAvailable = value;
    }
    internal bool ProcessCapabilityAvailable
    {
        get => Plans.ProcessCapabilityAvailable;
        set => Plans.ProcessCapabilityAvailable = value;
    }
    internal IDisposable EnterReview()
    {
        if (Plans.ReviewTurnActive) throw new InvalidOperationException("審查已在進行");
        Plans.ReviewTurnActive = true; return new ReviewLease(Plans);
    }
    private sealed class ReviewLease(PlanStore plans) : IDisposable
    { public void Dispose() => plans.ReviewTurnActive = false; }
    internal bool Allows(ToolCapability capability) => !ProcessSessionStopped && Plans.IsExecutionCurrent &&
        (!Plans.ReviewTurnActive || capability == ToolCapability.GitRead) && capability switch
        {
            ToolCapability.WorkspaceRead or ToolCapability.GitRead => true,
            ToolCapability.WorkspaceWrite or ToolCapability.UpdateTask or ToolCapability.DotnetTask => Mode == AgentMode.Execute,
            ToolCapability.SubmitPlan => Mode == AgentMode.Plan,
            _ => false
        };
    public override string ToString() => "[AgentExecutionPolicy]";
}