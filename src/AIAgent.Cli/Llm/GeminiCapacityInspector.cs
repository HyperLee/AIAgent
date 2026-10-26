using AIAgent.Cli.Core;

namespace AIAgent.Cli.Llm;

public sealed partial class GeminiClient
{
    private Task<ContextRequestMeasure> MeasureActualRequestAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (disposal is not null || sdk is null || policy is null || inFlight is not null)
                return Task.FromResult(new ContextRequestMeasure(ContextMeasureStatus.Unavailable));
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<ContextRequestMeasure>(cancellationToken);
            inFlight = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        return MeasureActualCoreAsync(request, cancellationToken);
    }

    /// <summary>使用相同 SDK 序列化；傳輸閘門在呼叫網路處理常式之前固定停止。</summary>
    private async Task<ContextRequestMeasure> MeasureActualCoreAsync(LlmRequest request, CancellationToken external)
    {
        var state = new GeminiOperation { MeasureOnly = true };
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(GeminiLimits.RequestTimeoutSeconds), timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(external, lifetime.Token, deadline.Token);
            linked.Token.ThrowIfCancellationRequested();
            var contents = GeminiProtocol.Convert(request, owner, policy!);
            var config = GeminiProtocol.Config(request);
            linked.Token.ThrowIfCancellationRequested();
            transport!.Operation = state;
            await sdk!.Models.GenerateContentAsync(model, contents, config, linked.Token);
            return new(ContextMeasureStatus.Unavailable, requiresContextReset: true);
        }
        catch (Exception ex)
        {
            if (external.IsCancellationRequested) throw new OperationCanceledException(external);
            if (lifetime.IsCancellationRequested || ex is OperationCanceledException)
                return new(ContextMeasureStatus.Unavailable);
            if (state.BodyBytes is { } bytes && (ex is GeminiRequestMeasuredException ||
                ex is GeminiBoundaryException { Code: LlmErrorCode.InputTooLarge }))
                return new(ContextMeasureStatus.Measured, bytes);
            return new(ContextMeasureStatus.Unavailable, requiresContextReset: ex is GeminiBoundaryException
            { Code: LlmErrorCode.SensitiveInput or LlmErrorCode.InvalidRequest or LlmErrorCode.InvalidResponse });
        }
        finally
        {
            // The measurement never observes API usage or publishes generation/body telemetry.
            state.Response?.Dispose();
            if (transport is not null) transport.Operation = null;
            TaskCompletionSource? finished;
            lock (gate) { finished = inFlight; inFlight = null; }
            finished?.TrySetResult();
        }
    }
}