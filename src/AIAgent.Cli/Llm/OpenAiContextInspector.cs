using System.Text.Json;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Llm;

internal sealed partial class OpenAiClient
{
    public bool ValidatePolicy(string instruction, IReadOnlyList<LlmToolDefinition> tools)
    {
        lock (_gate)
        {
            if (_disposal is not null || _profile is null || _policy is null) return false;
            try { return OpenAiProtocol.Serialize(new(instruction, [LlmMessage.FromUserText("本機驗證")], tools), _profile.Model, _owner, _policy, default).Length <= _profile.RequestBodyLimit; }
            catch (Exception) { return false; }
        }
    }
    public bool CanRepresentTools(IReadOnlyList<LlmToolDefinition> tools) => OpenAiProtocol.CanRepresentTools(tools);
    public ContextRequestMeasure Measure(LlmRequest request)
    {
        lock (_gate)
        {
            if (_disposal is not null || _policy is null || _profile?.ProtocolKind != ModelProtocolKind.OpenAiResponses || !_session.IsModelSessionCurrent)
                return new(ContextMeasureStatus.Unavailable);
            try { return new(ContextMeasureStatus.Measured, OpenAiProtocol.Serialize(request, _profile.Model, _owner, _policy, default).LongLength); }
            catch (OpenAiBoundaryException ex) { return new(ContextMeasureStatus.Unavailable, requiresContextReset: ex.Code is LlmErrorCode.SensitiveInput or LlmErrorCode.InvalidRequest or LlmErrorCode.InvalidResponse); }
            catch (Exception) { return new(ContextMeasureStatus.Unavailable, requiresContextReset: true); }
        }
    }
    public Task<ContextRequestMeasure> MeasureActualRequestAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = Measure(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(result);
    }
    public ContextTurnProjection ProjectCompletedTurn(long turnId, IReadOnlyList<LlmMessage> messages)
    {
        lock (_gate)
        {
            if (_disposal is not null || _policy is null || !_session.IsModelSessionCurrent) throw new ContextInspectionException();
            try
            {
                _ = OpenAiProtocol.Convert(new("", messages, []), _owner, _policy, default, completedTurn: true);
                if (messages[^1].Protocol is not OpenAiModelContent final) throw new ContextInspectionException();
                var visible = string.Concat(final.Items.Where(i => OpenAiJson.String(i, "type") == "message")
                    .SelectMany(i => i.GetProperty("content").EnumerateArray()).Where(p => OpenAiJson.String(p, "type") == "output_text").Select(p => OpenAiJson.String(p, "text")));
                var tools = messages.SelectMany(m => m.Results).Select(GeminiContextTools.Project).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
                var projection = new ContextTurnProjection(turnId, messages[0].UserText!, visible, tools);
                if (!ContextDocuments.Safe(ContextDocuments.Source(null, [projection]), _policy)) throw new ContextInspectionException();
                return projection;
            }
            catch (Exception) { throw new ContextInspectionException(); }
        }
    }
    public IDisposable ObserveRequest(Action<long, bool> report)
    {
        lock (_gate)
        {
            if (_disposal is not null || _report is not null) throw new ContextInspectionException();
            _report = report; return new ReportLease(this);
        }
    }
    private sealed class ReportLease(OpenAiClient client) : IDisposable
    {
        public void Dispose() { lock (client._gate) client._report = null; }
    }
}