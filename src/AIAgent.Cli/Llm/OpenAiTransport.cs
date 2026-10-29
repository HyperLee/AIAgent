using System.Net.Http.Headers;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Llm;

internal sealed class OpenAiTransport(HttpMessageHandler inner, string model, Func<ISensitiveDataPolicy?> policy,
    Func<AuthenticationHeaderValue?> authentication, Func<bool> isCurrent) : DelegatingHandler(inner)
{
    internal const string Destination = "https://api.openai.com/v1/responses";
    internal Action? OnDispatch { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var currentPolicy = policy(); var credential = authentication();
        if (OnDispatch is null || currentPolicy is null || !isCurrent()) throw new OpenAiBoundaryException(LlmErrorCode.ClientUnavailable);
        if (request.Method != HttpMethod.Post || request.RequestUri?.OriginalString != Destination || request.Headers.Authorization is not { Scheme: "Bearer" } authorization ||
            credential is null || authorization.Parameter != credential.Parameter || request.Headers.Any(h => h.Key != "Authorization") || request.Content is null)
            throw new OpenAiBoundaryException(LlmErrorCode.InvalidRequest);
        var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length > 262144) throw new OpenAiBoundaryException(LlmErrorCode.InputTooLarge);
        using var parsed = OpenAiJson.Parse(bytes);
        if (OpenAiJson.String(parsed.RootElement, "model") != model) throw new OpenAiBoundaryException(LlmErrorCode.InvalidRequest);
        OpenAiJson.Validate(parsed.RootElement, cancellationToken);
        OpenAiJson.Check(parsed.RootElement, currentPolicy, ContentBoundary.ModelInput, LlmErrorCode.SensitiveInput);
        cancellationToken.ThrowIfCancellationRequested();
        if (!isCurrent()) throw new OpenAiBoundaryException(LlmErrorCode.ClientUnavailable);
        OnDispatch();
        return await base.SendAsync(request, cancellationToken);
    }
    protected override void Dispose(bool disposing) { OnDispatch = null; base.Dispose(disposing); }
}