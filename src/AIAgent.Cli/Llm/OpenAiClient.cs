using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Llm;

[JsonConverter(typeof(OpenAiClientConverter))]
internal sealed partial class OpenAiClient : ILlmClient, ILlmContextInspector, ILlmRequestCapacityInspector, ILlmContextTelemetry, ILlmToolCapabilityInspector, ILlmRuntimePolicyValidator
{
    private const string Destination = OpenAiTransport.Destination;
    private readonly object _gate = new();
    private readonly object _owner = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TimeProvider _clock;
    private readonly HttpClient _http;
    private readonly OpenAiTransport _transport;
    private readonly BootstrapSession _session;
    private readonly ModelProfile? _profile;
    private ISensitiveDataPolicy? _policy;
    private AuthenticationHeaderValue? _authentication;
    private TaskCompletionSource? _inFlight;
    private Task? _disposal;
    private Action<long, bool>? _report;

    internal OpenAiClient(BootstrapSession session) : this(session, new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }) { }
    internal OpenAiClient(BootstrapSession session, HttpMessageHandler handler, TimeProvider? clock = null)
    {
        _session = session; _profile = ModelCatalog.Find(session.Settings); _policy = session.Policy; _clock = clock ?? TimeProvider.System;
        if (_profile?.ProtocolKind == ModelProtocolKind.OpenAiResponses)
            _authentication = new AuthenticationHeaderValue("Bearer", new string(session.Secrets.AuthenticationKey));
        _transport = new(handler, _profile?.Model ?? "", () => _policy, () => _authentication, () => _session.IsModelSessionCurrent);
        _http = new HttpClient(_transport, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public Task<LlmResult> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposal is not null || !_session.IsModelSessionCurrent) return Task.FromResult(Failure(LlmErrorCode.ClientUnavailable));
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(Cancelled());
            if (_inFlight is not null) return Task.FromResult(Failure(LlmErrorCode.InvalidRequest));
            _inFlight = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        return GenerateCoreAsync(request, cancellationToken);
    }

    private async Task<LlmResult> GenerateCoreAsync(LlmRequest request, CancellationToken external)
    {
        var dispatched = false; long? bodyBytes = null; LlmUsage? usage = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120), _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(external, _lifetime.Token, deadline.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (_profile?.ProtocolKind != ModelProtocolKind.OpenAiResponses || _authentication is null) return Failure(LlmErrorCode.InvalidRequest);
            var bytes = OpenAiProtocol.Serialize(request, _profile.Model, _owner, _policy!, linked.Token);
            bodyBytes = bytes.LongLength;
            if (bodyBytes > _profile.RequestBodyLimit) return Failure(LlmErrorCode.InputTooLarge);
            using var outgoing = new HttpRequestMessage(HttpMethod.Post, Destination) { Content = new ByteArrayContent(bytes) };
            outgoing.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json"); outgoing.Headers.Authorization = _authentication;
            // Final gate immediately before the only transport attempt. There is no alternate endpoint or retry path.
            if (outgoing.Method != HttpMethod.Post || outgoing.RequestUri!.AbsoluteUri != Destination || outgoing.Headers.Contains("x-goog-api-key") ||
                outgoing.Headers.Authorization != _authentication || !_session.IsModelSessionCurrent) return Failure(LlmErrorCode.InvalidRequest);
            using (var parsed = OpenAiJson.Parse(bytes))
                OpenAiJson.Check(parsed.RootElement, _policy!, ContentBoundary.ModelInput, LlmErrorCode.SensitiveInput);
            linked.Token.ThrowIfCancellationRequested();
            _transport.OnDispatch = () => dispatched = true;
            using var response = await _http.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            var payload = await ReadBoundedAsync(response.Content, response.IsSuccessStatusCode ? 1048576 : 65536, linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                JsonDocument? error = null;
                try { if (payload is not null) { error = OpenAiJson.Parse(payload); usage = OpenAiJson.ReadUsage(error.RootElement); OpenAiJson.Validate(error.RootElement, linked.Token); } }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or OpenAiBoundaryException or System.Text.DecoderFallbackException) { error?.Dispose(); error = null; }
                using (error) return Failure(Classify((int)response.StatusCode, error?.RootElement), dispatched, usage);
            }
            if (payload is null) throw OpenAiJson.Invalid();
            using var document = OpenAiJson.Parse(payload);
            usage = OpenAiJson.ReadUsage(document.RootElement);
            var result = OpenAiResults.Parse(document.RootElement, request.Tools.Count > 0, _owner, _policy!, usage, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!_session.IsModelSessionCurrent) return Failure(LlmErrorCode.ClientUnavailable, dispatched, usage);
            return result;
        }
        catch (Exception ex)
        {
            if (ex is OpenAiBoundaryException { Code: LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall } serious)
                return Failure(serious.Code, dispatched, usage);
            if (external.IsCancellationRequested) return Cancelled(dispatched, usage);
            if (_lifetime.IsCancellationRequested) return Failure(LlmErrorCode.ClientUnavailable, dispatched, usage);
            if (deadline.IsCancellationRequested || ex is OperationCanceledException) return Failure(LlmErrorCode.Timeout, dispatched, usage);
            if (ex is OpenAiBoundaryException boundary) return Failure(boundary.Code, dispatched, usage);
            return Failure(ex is HttpRequestException ? LlmErrorCode.NetworkFailure : dispatched ? LlmErrorCode.InvalidResponse : LlmErrorCode.InvalidRequest, dispatched, usage);
        }
        finally
        {
            _transport.OnDispatch = null;
            try { if (bodyBytes is { } length) _report?.Invoke(length, dispatched); } catch (Exception) { }
            TaskCompletionSource? finished;
            lock (_gate) { finished = _inFlight; _inFlight = null; }
            finished?.TrySetResult();
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, int limit, CancellationToken token)
    {
        await using var stream = await content.ReadAsStreamAsync(token);
        using var output = new MemoryStream(); var buffer = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, limit + 1 - (int)output.Length)), token);
            if (count == 0) return output.ToArray();
            output.Write(buffer, 0, count);
            if (output.Length > limit) return null;
        }
    }

    private static LlmErrorCode Classify(int status, JsonElement? root) => status switch
    {
        401 => LlmErrorCode.AuthenticationFailed,
        403 => LlmErrorCode.PermissionDenied,
        404 => LlmErrorCode.ModelUnavailable,
        408 => LlmErrorCode.Timeout,
        429 => LlmErrorCode.QuotaOrRateLimited,
        409 or >= 500 and <= 599 => LlmErrorCode.ServiceUnavailable,
        400 => root is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty("error", out var error) && OpenAiJson.String(error, "code") == "context_length_exceeded"
            ? LlmErrorCode.InputTooLarge : LlmErrorCode.InvalidRequest,
        _ => LlmErrorCode.InvalidResponse
    };
    private LlmResult Failure(LlmErrorCode code, bool dispatched = false, LlmUsage? usage = null) => new(LlmStatus.Failed, code,
        _policy?.RedactForDisplay(OpenAiResults.ErrorText(code)) ?? "模型用戶端已結束。", [], null, usage, dispatched,
        code is LlmErrorCode.SensitiveOutput or LlmErrorCode.InvalidResponse or LlmErrorCode.UnexpectedToolCall);
    private LlmResult Cancelled(bool dispatched = false, LlmUsage? usage = null) => new(LlmStatus.Cancelled, LlmErrorCode.None,
        _policy?.RedactForDisplay("已取消本次請求。") ?? "", [], null, usage, dispatched, false);

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion; Task wait;
        lock (_gate)
        {
            if (_disposal is not null) return new(_disposal);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _disposal = completion.Task; wait = _inFlight?.Task ?? Task.CompletedTask;
        }
        _ = DisposeCoreAsync(wait, completion); return new(completion.Task);
    }
    private async Task DisposeCoreAsync(Task wait, TaskCompletionSource completion)
    {
        var failed = false;
        try { _lifetime.Cancel(); } catch (Exception) { failed = true; }
        try { await wait; } catch (Exception) { failed = true; }
        try { _http.Dispose(); } catch (Exception) { failed = true; }
        lock (_gate) { _authentication = null; _policy = null; _report = null; }
        try { _lifetime.Dispose(); } catch (Exception) { failed = true; }
        if (failed) completion.TrySetException(new InvalidOperationException("模型用戶端資源釋放失敗")); else completion.TrySetResult();
    }
    public override string ToString() => "[OpenAiClient]";
}

internal sealed class OpenAiClientConverter : JsonConverter<OpenAiClient>
{
    public override OpenAiClient Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("模型用戶端不可序列化");
    public override void Write(Utf8JsonWriter writer, OpenAiClient value, JsonSerializerOptions options) => throw new NotSupportedException("模型用戶端不可序列化");
}