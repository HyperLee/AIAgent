using System.Text;
using System.Text.Json;

using AIAgent.Cli.Security;

namespace AIAgent.Cli.Core;

internal sealed partial class ContextCoordinator
{
    internal string PrepareWorkSource(ContextOperationStamp stamp, ILlmContextInspector inspector)
    {
        var projections = stamp.Context.CompletedTurns.Select(turn => inspector.ProjectCompletedTurn(turn.TurnId, turn.Messages)).Select(turn =>
            new ContextTurnProjection(turn.TurnId, turn.UserText, turn.AssistantText, turn.Tools.Select(WorkToolObservation).ToArray())).ToArray();
        var policy = new WorkPersistencePolicy(_policy);
        if (projections.Any(turn => !policy.IsSafe(turn.UserText) || !policy.IsSafe(turn.AssistantText) || turn.Tools.Any(tool => !policy.IsSafeJson(tool))))
            throw new WorkSensitiveSourceException();
        var source = ContextDocuments.Source(stamp.Context.Summary, projections);
        if (!new WorkPersistencePolicy(_policy).IsSafe(source)) throw new WorkDocumentException("work_sensitive_content");
        return Encoding.UTF8.GetString(source);
    }
    private static JsonElement WorkToolObservation(JsonElement observation)
    {
        var allowed = new[] { "name", "status", "error_code", "path", "byte_count", "returned_entries", "truncated", "task", "start_state", "end_state", "exit_code", "cleanup_confirmed", "validation" };
        var bytes = ContextDocuments.Write(ContextDocuments.MaxSourceBytes, writer =>
        {
            writer.WriteStartObject();
            foreach (var field in observation.EnumerateObject()) if (allowed.Contains(field.Name, StringComparer.Ordinal)) field.WriteTo(writer);
            writer.WriteEndObject();
        });
        using var document = JsonDocument.Parse(bytes); return document.RootElement.Clone();
    }
}

internal sealed class WorkSensitiveSourceException : Exception
{
    internal WorkSensitiveSourceException() : base("目前工作來源未通過安全檢查") { }
    public override string ToString() => "[WorkSensitiveSourceException]";
}

internal sealed partial class AgentRunner
{
    internal async Task<AgentTurnResult> RunWorkSaveSummaryAsync(ContextOperationStamp stamp, string source, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return Unavailable(AgentStopReason.RunnerUnavailable);
        try
        {
            using var generation = registry.ExecutionPolicy.Plans.BorrowGeneration();
            return await new Turn(client, registry, policy, systemInstruction, programUsage, onEvent, [], token,
                _budget, contextCoordinator, _inspector, true).RunWorkSummaryAsync(stamp, source);
        }
        finally { Volatile.Write(ref _running, 0); }
    }
    private sealed partial class Turn
    {
        internal async Task<AgentTurnResult> RunWorkSummaryAsync(ContextOperationStamp stamp, string source)
        {
            using var registration = token.Register(() => { lock (_gate) _cancelled = true; });
            try
            {
                await RequireRulesAsync();
                if (coordinator is null || inspector is null) return Finish(AgentTurnStatus.Stopped, AgentStopReason.ContextCapacity);
                if (LimitReason() is { } limit) return Finish(AgentTurnStatus.Stopped, limit);
                if (!new WorkPersistencePolicy(policy).IsSafe(source)) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
                var request = new LlmRequest(CompactionInstruction, [LlmMessage.FromUserText(source)], []);
                if (Measure(request, GenerationPurpose.WorkSaveSummary, stamp).EstimatedJsonBytes >= ContextDocuments.AutomaticThreshold)
                    return Finish(AgentTurnStatus.Stopped, AgentStopReason.ContextCapacity);
                var result = await GenerateAsync(request, GenerationPurpose.WorkSaveSummary, stamp);
                if (result is null) return Invalid();
                _modelStatus = result.Status; _modelError = result.ErrorCode;
                _reset |= result.RequiresContextReset || result.ErrorCode is LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall;
                if (!ValidResult(result) || result.ToolCalls.Count != 0) return Invalid();
                if (_reset || !new WorkPersistencePolicy(policy).IsSafe(result.SafeText))
                { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
                if (Cancelled || result.Status == LlmStatus.Cancelled) return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                if (result.Status != LlmStatus.Completed) return Finish(result.Status == LlmStatus.Failed ? AgentTurnStatus.Failed : AgentTurnStatus.Stopped, AgentStopReason.ModelOutcome);
                await RequireRulesAsync();
                if (!coordinator.IsCurrent(stamp)) return Finish(AgentTurnStatus.Stopped, AgentStopReason.ContextCompactionFailed);
                return Finish(AgentTurnStatus.Completed, AgentStopReason.None, result.SafeText);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested || Cancelled) { return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled); }
            catch (ProjectRulesFlowException ex) { return Finish(AgentTurnStatus.Stopped, ex.Reason); }
            catch (ProjectRulesSensitiveException) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
            catch (ContextFlowException ex) { return Finish(ex.Status, ex.Reason); }
            catch (ContextInspectionException) { return Invalid(); }
            catch (ContextCapacityException) { return Finish(AgentTurnStatus.Stopped, AgentStopReason.ContextCapacity); }
            catch (Exception) { return Finish(AgentTurnStatus.Failed, AgentStopReason.LocalFailure); }
        }
    }
}