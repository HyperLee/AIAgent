using System.Text.Json;

using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

using Google.GenAI;
using Google.GenAI.Types;

namespace AIAgent.Cli.Llm;

/// <summary>一次生成最多一次傳輸；只持有本 session 的模型協定，不管理聊天或執行工具。</summary>
public sealed class GeminiClient : ILlmClient
{
    private readonly object gate = new();
    private readonly object owner = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly TimeProvider timeProvider;
    private readonly string model;
    private ISensitiveDataPolicy? policy;
    private Client? sdk;
    private GeminiTransport? transport;
    private HttpClient? http;
    private TaskCompletionSource? inFlight;
    private Task? disposal;

    public GeminiClient(BootstrapSession session) : this(session, new SocketsHttpHandler { AllowAutoRedirect = false }) { }

    internal GeminiClient(BootstrapSession session, HttpMessageHandler handler, TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
        model = session.Settings.Model ?? "";
        policy = session.Policy;
        if (!ModelIdentifier.IsValid(model)) { handler.Dispose(); return; }
        try
        {
            transport = new GeminiTransport(handler, model, policy);
            http = new HttpClient(transport) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            // The SDK owns the necessary string copy. Never retain a second application-level string key.
            sdk = new Client(enterprise: false, vertexAI: false, apiKey: new string(session.Secrets.AuthenticationKey),
                httpOptions: new HttpOptions { BaseUrl = GeminiLimits.BaseUrl, ApiVersion = "v1beta", Timeout = GeminiLimits.RequestTimeoutSeconds * 1000, RetryOptions = new HttpRetryOptions { Attempts = 1 } },
                clientOptions: new ClientOptions { HttpClientFactory = () => http! });
        }
        catch
        {
            try { if (http is not null) http.Dispose(); else handler.Dispose(); }
            finally { lifetime.Dispose(); }
            throw;
        }
    }

    public Task<LlmResult> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (disposal is not null) return Task.FromResult(Failure(LlmErrorCode.ClientUnavailable, new()));
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(Cancelled(new()));
            if (inFlight is not null) return Task.FromResult(Failure(LlmErrorCode.InvalidRequest, new()));
            inFlight = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        return GenerateCoreAsync(request, cancellationToken);
    }

    private async Task<LlmResult> GenerateCoreAsync(LlmRequest request, CancellationToken external)
    {
        var state = new GeminiOperation();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(GeminiLimits.RequestTimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(external, lifetime.Token, deadline.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (sdk is null || !ModelIdentifier.IsValid(model)) return Failure(LlmErrorCode.InvalidRequest, state);
            var contents = GeminiProtocol.Convert(request, owner, policy!);
            var config = GeminiProtocol.Config(request);
            transport!.Operation = state;
            var response = await sdk.Models.GenerateContentAsync(model, contents, config, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            var parsed = GeminiResults.Parse(response, request.Tools.Count > 0, state.InvalidParts);
            var message = parsed.Content is null ? null : LlmMessage.FromModel(new GeminiModelContent(parsed.Content, owner, parsed.Calls), parsed.Calls);
            linked.Token.ThrowIfCancellationRequested();
            return new(parsed.Status, LlmErrorCode.None, policy!.RedactForDisplay(parsed.Text), parsed.Calls, message, state.Usage, state.RequestDispatched, false);
        }
        catch (Exception ex)
        {
            if (external.IsCancellationRequested) return Cancelled(state);
            if (lifetime.IsCancellationRequested) return Failure(LlmErrorCode.ClientUnavailable, state);
            if (deadline.IsCancellationRequested || ex is OperationCanceledException) return Failure(LlmErrorCode.Timeout, state);
            if (ex is GeminiBoundaryException boundary) return Failure(boundary.Code, state, boundary.Remote);
            return Failure(ex is HttpRequestException ? LlmErrorCode.NetworkFailure : LlmErrorCode.InvalidResponse, state);
        }
        finally
        {
            state.Response?.Dispose();
            if (transport is not null) transport.Operation = null;
            TaskCompletionSource? finished;
            lock (gate) { finished = inFlight; inFlight = null; }
            finished?.TrySetResult();
        }
    }

    private LlmResult Failure(LlmErrorCode code, GeminiOperation state, bool remote = false) => new(LlmStatus.Failed, code, Display(GeminiResults.ErrorText(code, remote)), [], null, state.Usage, state.RequestDispatched, code is LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall);
    private LlmResult Cancelled(GeminiOperation state) => new(LlmStatus.Cancelled, LlmErrorCode.None, Display("已取消本次請求。"), [], null, state.Usage, state.RequestDispatched, false);
    private string Display(string text) => policy?.RedactForDisplay(text) ?? text;

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completed;
        Task wait;
        lock (gate)
        {
            if (disposal is not null) return new(disposal);
            completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            disposal = completed.Task;
            wait = inFlight?.Task ?? Task.CompletedTask;
        }
        _ = DisposeCoreAsync(wait, completed);
        return new(completed.Task);
    }

    private async Task DisposeCoreAsync(Task wait, TaskCompletionSource completed)
    {
        try
        {
            lifetime.Cancel();
            await wait;
            if (sdk is not null) await sdk.DisposeAsync();
            http?.Dispose();
            sdk = null; http = null; transport = null; policy = null;
            lifetime.Dispose();
            completed.TrySetResult();
        }
        catch (Exception) { completed.TrySetException(new InvalidOperationException("模型用戶端資源釋放失敗")); }
    }

    public override string ToString() => "[GeminiClient]";
}