using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Core;

internal sealed partial class AgentRunner
{
    private sealed partial class Turn
    {
        private async Task<AgentTurnResult?> ExecuteDelegationBatchAsync(LlmResult model)
        {
            _toolPhaseClosed = true;
            var delegation = registry.ExecutionPolicy.Delegation;
            var mixed = model.ToolCalls.Count != 1;
            var results = new List<LlmToolResult>();
            DelegationBatchResult? currentOutcome = null;
            foreach (var call in model.ToolCalls)
            {
                ToolExecutionResult output;
                if (call.Name != DelegateReadonlyTool.Name) output = ToolResponseWriter.Create(ToolStatus.NotExecuted, "turn_stopped", policy);
                else if (mixed) output = DelegationJson.Response(new(ToolStatus.Rejected, "delegation_batch_invalid", null), policy);
                else if (review is not null) output = DelegationJson.Response(new(ToolStatus.Rejected, "mode_restricted", null), policy);
                else if (delegation is null || delegation.Mode != DelegationMode.Enabled) output = DelegationJson.Response(new(ToolStatus.Rejected, "delegation_disabled", null), policy);
                else
                {
                    delegation.BindGateway(Gateway);
                    output = await registry.DispatchAsync(call, _requestRegistry ?? registry.CaptureSnapshot(), token);
                    _aggregationLease = delegation.AggregationLease;
                }
                if (!mixed && call.Name == DelegateReadonlyTool.Name) currentOutcome = output.DelegationOutcome;
                if (output.ErrorCode == "delegation_budget_insufficient") _delegationResourceBlocked = true;
                AddOperation(call, output);
                if (output.Response is not { } response) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
                results.Add(new(call.PartIndex, call.CallId, call.Name, response));
            }
            if (delegation?.FatalFailure == true) return Finish(AgentTurnStatus.Failed, AgentStopReason.DelegationUnconfirmed);
            if (currentOutcome is { } batch && !mixed)
            {
                if (batch.RequiresContextReset) { _reset = true; return Finish(AgentTurnStatus.Failed, AgentStopReason.SensitiveData); }
                if (batch.AuthenticationFailed) { _modelStatus = LlmStatus.Failed; _modelError = LlmErrorCode.AuthenticationFailed; return Finish(AgentTurnStatus.Failed, AgentStopReason.ModelOutcome); }
                if (batch.Status == ToolStatus.Cancelled || Cancelled) return Finish(AgentTurnStatus.Cancelled, AgentStopReason.TurnCancelled);
                if (batch.Status == ToolStatus.Failed) return Finish(AgentTurnStatus.Stopped, batch.ErrorCode is "context_capacity" or "result_too_large" ? AgentStopReason.ContextCapacity : AgentStopReason.DelegationStopped);
            }
            if (!ToolCallPairing.MatchesResults(model.ToolCalls, results)) return Invalid();
            _messages.Add(model.ModelMessage!); _current.Add(model.ModelMessage!);
            var message = LlmMessage.FromToolResults(results); _messages.Add(message); _current.Add(message);
            if (LimitReason() is { } reason) return Finish(AgentTurnStatus.Stopped, reason);
            return null;
        }
    }
}