using System.Globalization;

using AIAgent.Cli.Security;

namespace AIAgent.Cli.Core;

internal sealed class WorkMemoryCandidate(ContextOperationStamp stamp, ContextSummary? summary, List<ContextPin> pins,
    long nextPinSequence, long nextRevision, long nextPinsRevision, PlanWorkCandidate plan, WorkSavedPlan? history, string workId)
{
    internal ContextOperationStamp Stamp { get; } = stamp;
    internal ContextSummary? Summary { get; } = summary;
    internal List<ContextPin> Pins { get; } = pins;
    internal long NextPinSequence { get; } = nextPinSequence;
    internal long NextRevision { get; } = nextRevision;
    internal long NextPinsRevision { get; } = nextPinsRevision;
    internal PlanWorkCandidate Plan { get; } = plan;
    internal WorkSavedPlan? History { get; } = history;
    internal string WorkId { get; } = workId;
    public override string ToString() => "[WorkMemoryCandidate]";
}

internal sealed partial class ContextCoordinator
{
    internal WorkSavedPlan? LoadedWorkPlan { get; private set; }
    internal string? LoadedWorkId { get; private set; }

    internal WorkMemoryCandidate PrepareWorkImport(WorkSaveDocument document)
    {
        lock (DecisionGate)
        {
            var stamp = Capture();
            var summary = document.Summary is { } s ? new ContextSummary(0, 0, s.Goals, s.Decisions, s.OpenQuestions, s.Background, document.WorkId) : null;
            var prepared = context.PrepareWorkPins(document.Pins, document.WorkId);
            var policy = new WorkPersistencePolicy(_policy);
            var pinsBytes = ContextDocuments.Pins(prepared.Pins); var memory = ContextDocuments.Memory(summary, prepared.Pins);
            if (!policy.IsSafe(pinsBytes) || !policy.IsSafe(memory)) throw new WorkDocumentException("work_sensitive_content");
            var plan = plans.PrepareWorkImport(document.Plan);
            return new(stamp, summary, prepared.Pins, prepared.Sequence, checked(context.Revision + 1), checked(context.PinsRevision + 1), plan, document.Plan, document.WorkId);
        }
    }

    internal bool TryPublishWorkImport(WorkMemoryCandidate candidate, CancellationToken token)
    {
        lock (DecisionGate)
        {
            if (token.IsCancellationRequested || !IsCurrent(candidate.Stamp) || !plans.IsCurrent(candidate.Plan)) return false;
            // Everything that can fail is prepared first. These commits only assign owned values.
            plans.PublishWorkImport(candidate.Plan);
            context.PublishWorkImport(candidate);
            LoadedWorkPlan = candidate.History; LoadedWorkId = candidate.WorkId;
            return true;
        }
    }
}

internal sealed partial class ConversationContext
{
    internal (List<ContextPin> Pins, long Sequence) PrepareWorkPins(IReadOnlyList<string> texts, string workId)
    {
        var sequence = _pinSequence; var pins = new List<ContextPin>(texts.Count);
        foreach (var text in texts) pins.Add(new("n" + checked(++sequence).ToString(CultureInfo.InvariantCulture), text, workId));
        return (pins, sequence);
    }
    internal void PublishWorkImport(WorkMemoryCandidate candidate)
    {
        _turns.Clear(); _pins = candidate.Pins; Summary = candidate.Summary;
        _pinSequence = candidate.NextPinSequence; Revision = candidate.NextRevision; PinsRevision = candidate.NextPinsRevision;
    }
}

internal sealed class PlanWorkCandidate(long generation, long nextGeneration, long nextPlanSequence, long nextTaskSequence, PlanSnapshot? plan)
{
    internal long Generation { get; } = generation;
    internal long NextGeneration { get; } = nextGeneration;
    internal long NextPlanSequence { get; } = nextPlanSequence;
    internal long NextTaskSequence { get; } = nextTaskSequence;
    internal PlanSnapshot? Plan { get; } = plan;
    public override string ToString() => "[PlanWorkCandidate]";
}

internal sealed partial class PlanStore
{
    internal PlanWorkCandidate PrepareWorkImport(WorkSavedPlan? history)
    {
        lock (_gate)
        {
            if (_policy is null || _reservations.Count != 0 || HasInProgress) throw new WorkDocumentException("work_revision_conflict");
            var nextPlan = _planSequence; var nextTask = _taskSequence; PlanSnapshot? plan = null;
            var pending = history?.Tasks.Where(t => t.ReportedState != PlanTaskState.Completed).ToArray() ?? [];
            if (pending.Length > 0)
            {
                var tasks = pending.Select(t => new PlanTaskSnapshot(Id("t", checked(++nextTask)), t.Kind, t.Title, t.Acceptance, t.Requirements, requirementsVersion: t.RequirementsVersion)).ToArray();
                plan = new(Id("p", checked(++nextPlan)), 1, history!.Objective, PlanApprovalState.Draft, tasks);
                var persistence = new WorkPersistencePolicy(_policy);
                foreach (var upper in new[] { false, true })
                {
                    var json = PlanProjection.State(AgentMode.Plan, plan, upper, CurrentRulesRevision);
                    var display = PlanProjection.Display(AgentMode.Plan, plan, 256, upper: upper);
                    if (json.Length > PlanLimits.MaxProjectionBytes || System.Text.Encoding.UTF8.GetByteCount(display) > PlanLimits.MaxDisplayBytes)
                        throw new WorkDocumentException("work_capacity");
                    if (!persistence.IsSafe(json) || !persistence.IsSafe(display)) throw new WorkDocumentException("work_sensitive_content");
                }
            }
            return new(_generation, checked(_generation + 1), nextPlan, nextTask, plan);
        }
    }
    internal bool IsCurrent(PlanWorkCandidate candidate) => _policy is not null && _generation == candidate.Generation && _reservations.Count == 0 && !HasInProgress;
    internal void PublishWorkImport(PlanWorkCandidate candidate)
    {
        _planSequence = candidate.NextPlanSequence; _taskSequence = candidate.NextTaskSequence;
        _generation = candidate.NextGeneration; _plan = candidate.Plan; _mode = AgentMode.Plan; _facts.Clear();
    }
}
