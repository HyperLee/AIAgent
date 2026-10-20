using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Llm;

internal sealed class GeminiOperation
{
    internal bool RequestDispatched { get; set; }
    internal bool InvalidParts { get; set; }
    internal LlmUsage? Usage { get; set; }
    internal HttpResponseMessage? Response { get; set; }
}

internal sealed class GeminiBoundaryException(LlmErrorCode code, bool remote = false) : Exception("模型邊界已停止本次操作")
{
    internal LlmErrorCode Code { get; } = code;
    internal bool Remote { get; } = remote;
}

internal sealed class GeminiTransport(HttpMessageHandler inner, string model, ISensitiveDataPolicy policy) : DelegatingHandler(inner)
{
    internal GeminiOperation? Operation { get; set; }
    private ISensitiveDataPolicy? currentPolicy = policy;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var state = Operation ?? throw new GeminiBoundaryException(LlmErrorCode.ClientUnavailable);
        var uri = request.RequestUri;
        if (request.Method != HttpMethod.Post || uri is null || uri.Scheme != "https" || uri.Host != "generativelanguage.googleapis.com" || !uri.IsDefaultPort || uri.UserInfo != "" || uri.AbsolutePath != $"/v1beta/models/{model}:generateContent" || uri.Query != "" || uri.Fragment != "") throw new GeminiBoundaryException(LlmErrorCode.InvalidRequest);
        var body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
        if (body.Length > GeminiLimits.MaxRequestBodyBytes) throw new GeminiBoundaryException(LlmErrorCode.InputTooLarge);
        using (var document = JsonDocument.Parse(body))
            if (GeminiJson.IsSensitive(document.RootElement, currentPolicy!, ContentBoundary.ModelInput)) throw new GeminiBoundaryException(LlmErrorCode.SensitiveInput);
        cancellationToken.ThrowIfCancellationRequested();
        state.RequestDispatched = true;
        var response = await base.SendAsync(request, cancellationToken);
        state.Response = response;
        if (!response.IsSuccessStatusCode) throw new GeminiBoundaryException(await ClassifyErrorAsync(response, cancellationToken), true);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        using var parsed = JsonDocument.Parse(bytes);
        state.Usage = GeminiJson.ReadUsage(parsed.RootElement);
        if (GeminiJson.IsSensitive(parsed.RootElement, currentPolicy!, ContentBoundary.ModelInput) || GeminiJson.HasSensitiveCombinedText(parsed.RootElement, currentPolicy!)) throw new GeminiBoundaryException(LlmErrorCode.SensitiveOutput);
        state.InvalidParts = GeminiJson.HasInvalidParts(parsed.RootElement);
        return response;
    }

    private static async Task<LlmErrorCode> ClassifyErrorAsync(HttpResponseMessage response, CancellationToken token)
    {
        var status = (int)response.StatusCode;
        if (status is 400 or 403)
        {
            // Read at most the allowance plus one sentinel byte; never buffer an unbounded error body.
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var bytes = new byte[GeminiLimits.MaxErrorBodyBytes + 1];
            var length = 0;
            while (length < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(length), token);
                if (read == 0) break;
                length += read;
            }
            if (length <= GeminiLimits.MaxErrorBodyBytes)
            {
                try
                {
                    using var document = JsonDocument.Parse(bytes.AsMemory(0, length));
                    var root = document.RootElement;
                    if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                        foreach (var detail in details.EnumerateArray())
                            if (detail.ValueKind == JsonValueKind.Object && String(detail, "@type") == "type.googleapis.com/google.rpc.ErrorInfo" && String(detail, "domain") == "googleapis.com" && String(detail, "reason") is "API_KEY_INVALID" or "API_KEY_EXPIRED") return LlmErrorCode.AuthenticationFailed;
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException) { /* Only safe HTTP classification survives. */ }
            }
        }
        return status switch
        {
            401 => LlmErrorCode.AuthenticationFailed,
            404 => LlmErrorCode.ModelUnavailable,
            403 => LlmErrorCode.PermissionDenied,
            429 => LlmErrorCode.QuotaOrRateLimited,
            400 or 422 => LlmErrorCode.InvalidRequest,
            408 or 504 => LlmErrorCode.Timeout,
            >= 500 and <= 599 => LlmErrorCode.ServiceUnavailable,
            _ => LlmErrorCode.InvalidResponse
        };
    }

    private static string? String(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    protected override void Dispose(bool disposing) { currentPolicy = null; Operation = null; base.Dispose(disposing); }
}