using System.Net;
using System.Text;
using System.Text.Json;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

internal sealed class GeminiHandler : HttpMessageHandler
{
    internal int Calls { get; private set; }
    internal List<byte[]> Bodies { get; } = [];
    internal bool AuthenticationCorrect { get; private set; } = true;
    internal bool EndpointCorrect { get; private set; } = true;
    internal bool Disposed { get; private set; }
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } = (_, _) => Task.FromResult(Json(GeminiFixtures.Text()));
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        var uri = request.RequestUri!;
        EndpointCorrect &= request.Method == HttpMethod.Post && uri.Scheme == "https" && uri.Host == "generativelanguage.googleapis.com" && uri.AbsolutePath == "/v1beta/models/gemini-3.5-flash-lite:generateContent" && uri.Query == "" && uri.Fragment == "";
        AuthenticationCorrect &= request.Headers.TryGetValues("x-goog-api-key", out var values) && values.Single() == GeminiFixtures.Key && request.Headers.Authorization is null;
        Bodies.Add(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
        return await Respond(request, cancellationToken);
    }
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    internal static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

internal static class GeminiFixtures
{
    internal const string Key = "FAKE_KEY_FOR_TESTS_ONLY";
    internal static BootstrapSession Session() => new(new("/unused-workspace"), new("gemini-3.5-flash-lite"), new SessionSecrets(Key.ToCharArray()));
    internal static JsonElement Element(string json) { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
    internal static LlmRequest Request(string text = "question", IReadOnlyList<LlmToolDefinition>? tools = null) => new("instruction", [LlmMessage.FromUserText(text)], tools ?? []);
    internal static LlmToolDefinition Tool(string name = "sample") => new(name, "fake tool", Element("{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"integer\"}}}"));
    internal static string Text(string text = "answer", string finish = "STOP", string? usage = null) => Response("[{\"text\":" + JsonSerializer.Serialize(text) + "}]", finish, usage);
    internal static string Response(string parts, string finish = "STOP", string? usage = null) => "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":" + parts + "},\"finishReason\":\"" + finish + "\"}]" + (usage is null ? "" : ",\"usageMetadata\":" + usage) + "}";
    internal static string Calls(bool withIds = true) => Response("[{\"text\":\"working\"},{\"functionCall\":{\"name\":\"sample\",\"args\":{\"value\":1}" + (withIds ? ",\"id\":\"call-a\"" : "") + "},\"thoughtSignature\":\"c2lnbmF0dXJl\"},{\"functionCall\":{\"name\":\"sample\",\"args\":{\"value\":2}" + (withIds ? ",\"id\":\"call-b\"" : "") + "}}]");
    internal static LlmToolResult Result(LlmToolCall call) => new(call.PartIndex, call.CallId, call.Name, Element("{\"output\":\"fake result\"}"));
}
