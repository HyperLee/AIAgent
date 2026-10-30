using AIAgent.Cli.Git;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal sealed class DelegatedReadRunner(DelegationChildSpec spec, DelegationRuntime runtime, IReadOnlyList<ITool> tools,
    DelegationReadSession? reads, CodeReviewCoordinator? review, DelegationBudgetLease lease, int index, GenerationGateway gateway,
    ISensitiveDataPolicy policy, Func<bool> isCurrent, Func<CancellationToken, Task> requireCurrent, Action<DelegationEventKind, int> progress, IMcpContentGuard? reportGuard = null)
{
    private readonly UsageAccumulator _usage = new();
    private readonly IMcpContentGuard _instructionGuard = new McpContentGuard(policy, [DelegationInstructions.Common, spec.Instruction]);
    private bool SafeReport(string value) => _instructionGuard.IsSafe(value) && (reportGuard?.IsSafe(value) ?? true);
    private bool SafeReport(System.Text.Json.JsonElement value) => _instructionGuard.IsSafe(value) && (reportGuard?.IsSafe(value) ?? true);
    private int _started;
    private int _calls;
    internal ContextRequestMeasure? LastMeasure { get; private set; }
    internal long? LastBodyBytes { get; private set; }
    internal bool LastDispatched { get; private set; }
    internal LlmRequest FirstRequest() => new(spec.Instruction + (review?.Appendix() ?? ""), [LlmMessage.FromUserText(spec.Handoff)], tools.Select(t => t.Definition).ToArray());
    internal void ValidateRuntime()
    {
        var request = FirstRequest();
        if (runtime.Client is ILlmRuntimePolicyValidator validator && !validator.ValidatePolicy(request.SystemInstruction, request.Tools)) throw new DelegationFailure("sensitive_content", reset: true);
        Measure(request);
    }
    private void Measure(LlmRequest request)
    {
        if (runtime.Inspector is ILlmToolCapabilityInspector capabilities && !capabilities.CanRepresentTools(request.Tools)) throw new DelegationFailure("context_capacity");
        LastMeasure = runtime.Inspector.Measure(request);
        if (LastMeasure.RequiresContextReset) throw new DelegationFailure("invalid_protocol", reset: true);
        if (LastMeasure.Status != ContextMeasureStatus.Measured || LastMeasure.EstimatedJsonBytes is not >= 0 || LastMeasure.EstimatedJsonBytes >= ContextDocuments.AutomaticThreshold) throw new DelegationFailure("context_capacity");
    }
    internal async Task<DelegationChildResult> RunAsync(CancellationToken token)
    {
        var messages = new List<LlmMessage> { LlmMessage.FromUserText(spec.Handoff) };
        while (true)
        {
            token.ThrowIfCancellationRequested(); await requireCurrent(token);
            var remaining = lease.Remaining(index); if (remaining == 0) return End("delegation_limit");
            var request = new LlmRequest(spec.Instruction + (review?.Appendix() ?? ""), messages, remaining == 1 ? [] : tools.Select(t => t.Definition).ToArray());
            Measure(request);
            using var telemetry = runtime.Inspector is ILlmContextTelemetry meter ? meter.ObserveRequest((bytes, sent) => { LastBodyBytes = bytes; LastDispatched = sent; }) : null;
            var result = await gateway.GenerateAsync(runtime.Client, request, GenerationPurpose.DelegatedRead, token,
                () => { if (!lease.TryBeginChild(index, token, isCurrent)) return false; _started++; return true; }, _usage);
            if (result?.RequestDispatched == true) { reads?.Ledger.MarkDispatched(); review?.Snapshots.MarkDispatched(); }
            token.ThrowIfCancellationRequested();
            if (result is null || !Valid(result)) throw new DelegationFailure("invalid_protocol", reset: true);
            if (result.RequiresContextReset || result.ErrorCode is LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall)
                throw new DelegationFailure("sensitive_content", reset: true);
            if (result.ErrorCode == LlmErrorCode.AuthenticationFailed) throw new DelegationFailure("delegation_model_failed", authenticationFailed: true);
            if (!policy.CanSendOrUse(result.SafeText, ContentBoundary.ModelInput) || !SafeReport(result.SafeText)) throw new DelegationFailure("sensitive_content", reset: true);
            await requireCurrent(token);
            if (result.Status == LlmStatus.Completed)
            {
                var report = spec.Task.Role == DelegationRole.Explore ? DelegationReports.Explore(result.SafeText, reads!.Ledger) :
                    review!.Validate(result.SafeText) is not null ? Parse(result.SafeText) : null;
                if (report is not null && !SafeReport(report.Value)) throw new DelegationFailure("sensitive_content", reset: true);
                if (report is null) return End(spec.Task.Role == DelegationRole.Explore ? "delegation_report_invalid" : "review_invalid");
                if (reads is not null && await reads.ValidateAsync(token) is { } code && code != "none") throw DelegationErrors.Source(code);
                if (review is not null && await review.Snapshots.ValidateAsync(token) is { } git && git != "none") throw DelegationErrors.Source(git);
                var child = new DelegationChildResult(spec.Stamp.AgentId, spec.Task, DelegationTaskState.Completed, "none", report, Coverage(), _started, gateway.Snapshot(_usage), LastMeasure, LastBodyBytes, LastDispatched);
                try { _ = DelegationJson.Write(w => DelegationJson.Child(w, child), 8192); }
                catch (DelegationFailure) { return End("result_too_large"); }
                return child;
            }
            if (result.Status != LlmStatus.ToolCalls) return End("delegation_model_failed");
            if (lease.Remaining(index) == 0) throw new DelegationFailure("invalid_protocol", reset: true);
            if (result.ToolCalls.Any(c => SensitiveJson.Contains(c.Arguments, policy, ContentBoundary.ToolArguments) || !policy.CanSendOrUse(c.Name, ContentBoundary.ToolArguments) || c.CallId is { } id && !policy.CanSendOrUse(id, ContentBoundary.ToolArguments))) throw new DelegationFailure("sensitive_arguments", reset: true);
            if (_calls + result.ToolCalls.Count > 16) return End("delegation_limit");
            if (result.ToolCalls.Any(c => !tools.Any(t => t.Definition.Name == c.Name))) return End("mode_restricted");
            if (reads is not null && !reads.Ledger.CanStartBatch(result.ToolCalls)) return End("delegation_limit");
            var results = new List<LlmToolResult>();
            foreach (var call in result.ToolCalls)
            {
                token.ThrowIfCancellationRequested(); await requireCurrent(token);
                if (reads is not null && !reads.Ledger.CanStartBatch([call])) return End("delegation_limit");
                var tool = tools.Single(t => t.Definition.Name == call.Name); _calls++;
                var output = await tool.ExecuteAsync(call.Arguments, token);
                if (output.ErrorCode is "process_unconfirmed" or "process_cleanup_failed") throw new DelegationFailure(output.ErrorCode, fatal: true);
                if (output.Response is not { } response || SensitiveJson.Contains(response, policy, ContentBoundary.ToolResult) || !SafeReport(response)) throw new DelegationFailure("sensitive_content", reset: true);
                if (output.ErrorCode is "sensitive_content" or "sensitive_arguments") throw new DelegationFailure(output.ErrorCode, reset: true);
                token.ThrowIfCancellationRequested();
                if (output.ErrorCode.StartsWith("rules_", StringComparison.Ordinal) || output.ErrorCode.StartsWith("skills_", StringComparison.Ordinal) || output.ErrorCode is "mcp_changed" or "git_snapshot_stale") throw DelegationErrors.Source(output.ErrorCode);
                if (output.Status == ToolStatus.Cancelled) throw new DelegationFailure("delegation_source_changed");
                if (output.Status != ToolStatus.Success) return End(output.ErrorCode);
                reads?.Ledger.AddPending(call.Name, response);
                results.Add(new(call.PartIndex, call.CallId, call.Name, response)); progress(DelegationEventKind.ToolCompleted, _calls);
            }
            if (!ToolCallPairing.MatchesResults(result.ToolCalls, results)) throw new DelegationFailure("invalid_protocol", reset: true);
            messages.Add(result.ModelMessage!); messages.Add(LlmMessage.FromToolResults(results));
        }
    }
    private static System.Text.Json.JsonElement Parse(string text) { using var doc = System.Text.Json.JsonDocument.Parse(text); return doc.RootElement.Clone(); }
    internal DelegationChildResult End(string code, DelegationTaskState state = DelegationTaskState.Failed) =>
        new(spec.Stamp.AgentId, spec.Task, state, code, null, Coverage(), _started, gateway.Snapshot(_usage), LastMeasure, LastBodyBytes, LastDispatched);
    private System.Text.Json.JsonElement Coverage() => reads is not null ? DelegationJson.Coverage(spec.Task.Role, reads.Ledger.SourceCount, reads.Ledger.SourceCount, truncated: reads.Ledger.Truncated,
        limitations: reads.Ledger.SourceCount == 0 ? ["observation_only", "no_source"] : reads.Ledger.Truncated ? ["observation_only", "truncated", "safety_filtered"] : ["observation_only", "safety_filtered"]) :
        DelegationJson.Coverage(DelegationRole.Review, review!.Snapshots.EligibleChangeCount, review.Snapshots.DeliveredChangeCount,
            review.Candidate?.Complete == true, limitations: review.Candidate?.Complete == true ? [] : ["review_incomplete"]);
    internal static bool Valid(LlmResult result)
    {
        if (!Enum.IsDefined(result.Status) || !Enum.IsDefined(result.ErrorCode) || result.SafeText is null || (result.Status == LlmStatus.Failed) == (result.ErrorCode == LlmErrorCode.None)) return false;
        return result.Status is LlmStatus.Completed or LlmStatus.ToolCalls ? !result.RequiresContextReset && result.ModelMessage is { } model && ToolCallPairing.MatchesProjection(model, result.ToolCalls) &&
            (result.Status == LlmStatus.Completed ? result.ToolCalls.Count == 0 && !string.IsNullOrWhiteSpace(result.SafeText) : result.ToolCalls.Count > 0) : result.ModelMessage is null && result.ToolCalls.Count == 0;
    }
    public override string ToString() => "[DelegatedReadRunner]";
}