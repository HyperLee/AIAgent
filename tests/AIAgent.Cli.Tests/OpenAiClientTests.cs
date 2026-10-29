using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class OpenAiClientTests
{
    [TestMethod]
    public async Task MODELAC013_OfficialResponsesBodyAuthenticationAndOptionalSchema()
    {
        using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler();
        await using var client = OpenAiTestData.Client(session, handler);
        Assert.AreEqual(0, handler.Bodies.Count);
        var tool = GeminiFixtures.Tool();
        var result = await client.GenerateAsync(new("synthetic instruction", [LlmMessage.FromUserText("question")], [tool]), default);
        Assert.AreEqual(LlmStatus.Completed, result.Status);
        Assert.IsTrue(handler.OfficialDestination && handler.AuthenticationCorrect);
        using var json = JsonDocument.Parse(handler.Bodies.Single()); var root = json.RootElement;
        Assert.AreEqual("gpt-6.1-terra", root.GetProperty("model").GetString());
        Assert.AreEqual("synthetic instruction", root.GetProperty("instructions").GetString());
        foreach (var field in new[] { "stream", "store", "background", "parallel_tool_calls" }) Assert.IsFalse(root.GetProperty(field).GetBoolean());
        Assert.AreEqual(8192, root.GetProperty("max_output_tokens").GetInt32());
        Assert.AreEqual("none", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.AreEqual("reasoning.encrypted_content", root.GetProperty("include")[0].GetString());
        var function = root.GetProperty("tools")[0];
        Assert.IsFalse(function.GetProperty("strict").GetBoolean());
        Assert.IsTrue(JsonElement.DeepEquals(tool.Parameters, function.GetProperty("parameters")));
        Assert.AreEqual("function", function.GetProperty("type").GetString());
        Assert.AreEqual("auto", root.GetProperty("tool_choice").GetString());
        foreach (var field in new[] { "conversation", "previous_response_id", "metadata" }) Assert.IsFalse(root.TryGetProperty(field, out _));
        Assert.IsFalse(Encoding.UTF8.GetString(handler.Bodies[0]).Contains(OpenAiTestData.Key, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MODELAC014_NativeIndicesCallIdsReasoningAndOwnerArePreserved()
    {
        using var session = OpenAiTestData.Session();
        var handler = new OpenAiTestHandler { Response = OpenAiTestData.Response("""
        [{"type":"reasoning","id":"reason-one","summary":[],"encrypted_content":"c3ludGhldGlj"},
         {"type":"message","id":"message-one","role":"assistant","status":"completed","content":[{"type":"output_text","text":"checking","annotations":[]}]},
         {"type":"function_call","id":"item-one","call_id":"call-one","name":"sample","arguments":"{\"value\":\"hello\"}","status":"completed"}]
        """) };
        await using var client = OpenAiTestData.Client(session, handler);
        var first = await client.GenerateAsync(OpenAiTestData.Request([GeminiFixtures.Tool()]), default);
        Assert.AreEqual(LlmStatus.ToolCalls, first.Status);
        Assert.AreEqual(2, first.ToolCalls.Single().PartIndex);
        Assert.AreEqual("call-one", first.ToolCalls[0].CallId);
        var results = LlmMessage.FromToolResults([new(2, "call-one", "sample", GeminiFixtures.Element("{\"status\":\"success\",\"data\":\"complete result\"}"))]);
        var followup = new LlmRequest("synthetic instruction", [LlmMessage.FromUserText("question"), first.ModelMessage!, results], [GeminiFixtures.Tool()]);
        handler.Response = OpenAiTestData.TextResponse;
        Assert.AreEqual(LlmStatus.Completed, (await client.GenerateAsync(followup, default)).Status);
        using var sent = JsonDocument.Parse(handler.Bodies[1]); var input = sent.RootElement.GetProperty("input");
        Assert.AreEqual(5, input.GetArrayLength());
        Assert.AreEqual("reasoning", input[1].GetProperty("type").GetString());
        Assert.AreEqual("c3ludGhldGlj", input[1].GetProperty("encrypted_content").GetString());
        Assert.AreEqual("item-one", input[3].GetProperty("id").GetString());
        Assert.AreEqual("call-one", input[4].GetProperty("call_id").GetString());
        using var output = JsonDocument.Parse(input[4].GetProperty("output").GetString()!);
        Assert.AreEqual("complete result", output.RootElement.GetProperty("data").GetString());
        var otherHandler = new OpenAiTestHandler(); await using var other = OpenAiTestData.Client(session, otherHandler);
        Assert.AreEqual(LlmErrorCode.InvalidRequest, (await other.GenerateAsync(followup, default)).ErrorCode);
        Assert.AreEqual(0, otherHandler.Bodies.Count);
    }

    [DataRow("completed", "[]", null, LlmStatus.Empty)]
    [DataRow("incomplete", "[]", "max_output_tokens", LlmStatus.Truncated)]
    [DataRow("incomplete", "[]", "content_filter", LlmStatus.Refused)]
    [DataRow("completed", "[{\"type\":\"message\",\"id\":\"m\",\"role\":\"assistant\",\"status\":\"completed\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"no\"}]}]", null, LlmStatus.Refused)]
    [TestMethod]
    public async Task MODELAC015_ControlledStatusesDoNotCarryExecutableCalls(string status, string output, string? reason, LlmStatus expected)
    {
        using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler { Response = OpenAiTestData.Response(output, status, reason) };
        await using var client = OpenAiTestData.Client(session, handler);
        var result = await client.GenerateAsync(OpenAiTestData.Request(), default);
        Assert.AreEqual(expected, result.Status); Assert.IsNull(result.ModelMessage); Assert.AreEqual(0, result.ToolCalls.Count);
    }

    [DataRow("[{\"type\":\"web_search_call\",\"id\":\"hosted\"}]")]
    [DataRow("[{\"type\":\"reasoning\",\"id\":\"r\",\"summary\":[]}]")]
    [DataRow("[{\"type\":\"function_call\",\"id\":\"i\",\"name\":\"sample\",\"arguments\":\"{}\"}]")]
    [DataRow("[{\"type\":\"function_call\",\"id\":\"i\",\"call_id\":\"c\",\"name\":\"sample\",\"arguments\":\"[]\"}]")]
    [DataRow("[{\"type\":\"function_call\",\"id\":\"i\",\"call_id\":\"c\",\"name\":\"sample\",\"arguments\":\"{\\\"x\\\":1,\\\"x\\\":2}\"}]")]
    [TestMethod]
    public async Task MODELAC015_InvalidNativeBatchRequiresResetAndNeverPublishesPrefix(string output)
    {
        using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler { Response = OpenAiTestData.Response(output) };
        await using var client = OpenAiTestData.Client(session, handler);
        var result = await client.GenerateAsync(OpenAiTestData.Request([GeminiFixtures.Tool()]), default);
        Assert.AreEqual(LlmErrorCode.InvalidResponse, result.ErrorCode);
        Assert.IsTrue(result.RequiresContextReset); Assert.IsNull(result.ModelMessage); Assert.AreEqual(0, result.ToolCalls.Count);
    }

    [TestMethod]
    public async Task MODELAC017_ActualBodyBoundaryAndInspectionAreOffline()
    {
        using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler();
        await using var client = OpenAiTestData.Client(session, handler);
        var inspector = (ILlmContextInspector)client;
        Assert.AreEqual(ContextMeasureStatus.Measured, inspector.Measure(OpenAiTestData.Request()).Status);
        Assert.AreEqual(0, handler.Bodies.Count);
        await client.GenerateAsync(OpenAiTestData.Request(), default);
        var pad = 262144 - handler.Bodies[0].Length;
        LlmRequest Request(int count) => new("synthetic instruction", [LlmMessage.FromUserText("question" + new string('x', count))], []);
        Assert.AreEqual(LlmStatus.Completed, (await client.GenerateAsync(Request(pad), default)).Status);
        Assert.AreEqual(262144, handler.Bodies[^1].Length);
        var measured = await ((ILlmRequestCapacityInspector)client).MeasureActualRequestAsync(Request(pad + 1), default);
        Assert.AreEqual(262145L, measured.EstimatedJsonBytes);
        var denied = await client.GenerateAsync(Request(pad + 1), default);
        Assert.AreEqual(LlmErrorCode.InputTooLarge, denied.ErrorCode); Assert.IsFalse(denied.RequestDispatched);
        Assert.AreEqual(2, handler.Bodies.Count);
    }

    [DataRow(401, LlmErrorCode.AuthenticationFailed)]
    [DataRow(403, LlmErrorCode.PermissionDenied)]
    [DataRow(404, LlmErrorCode.ModelUnavailable)]
    [DataRow(408, LlmErrorCode.Timeout)]
    [DataRow(429, LlmErrorCode.QuotaOrRateLimited)]
    [DataRow(409, LlmErrorCode.ServiceUnavailable)]
    [DataRow(503, LlmErrorCode.ServiceUnavailable)]
    [DataRow(302, LlmErrorCode.InvalidResponse)]
    [DataRow(400, LlmErrorCode.InvalidRequest)]
    [TestMethod]
    public async Task MODELAC026_HttpErrorsNeverRetryOrExposeRemoteMessage(int status, LlmErrorCode expected)
    {
        using var session = OpenAiTestData.Session();
        var handler = new OpenAiTestHandler { Status = (HttpStatusCode)status, Response = "{\"error\":{\"message\":\"REMOTE_PRIVATE_MESSAGE\",\"code\":\"untrusted\"}}" };
        await using var client = OpenAiTestData.Client(session, handler);
        var result = await client.GenerateAsync(OpenAiTestData.Request(), default);
        Assert.AreEqual(expected, result.ErrorCode); Assert.AreEqual(1, handler.Bodies.Count);
        Assert.IsFalse(result.SafeText.Contains("REMOTE_PRIVATE_MESSAGE", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MODELAC024_UsagePreservesKnownZeroMissingAndSubsets()
    {
        using var session = OpenAiTestData.Session();
        var handler = new OpenAiTestHandler { Response = OpenAiTestData.TextResponse.Replace("\"usage\":{}", "\"usage\":{\"input_tokens\":0,\"output_tokens\":9,\"input_tokens_details\":{\"cached_tokens\":0},\"output_tokens_details\":{\"reasoning_tokens\":3},\"total_tokens\":9223372036854775808}", StringComparison.Ordinal) };
        await using var client = OpenAiTestData.Client(session, handler);
        var result = await client.GenerateAsync(OpenAiTestData.Request(), default);
        Assert.AreEqual(0L, result.Usage?.PromptTokens); Assert.AreEqual(9L, result.Usage?.OutputTokens);
        Assert.AreEqual(0L, result.Usage?.CachedTokens); Assert.AreEqual(3L, result.Usage?.ThoughtTokens);
        Assert.IsNull(result.Usage?.TotalTokens);
    }
}

internal static class OpenAiTestData
{
    internal const string Key = "FAKE_OPENAI_AUTH_ONLY";
    internal static BootstrapSession Session() => new(new("/unused-workspace"), new("openai", "gpt-6.1-terra"), new SessionSecrets(Key.ToCharArray()));
    internal static ILlmClient Client(BootstrapSession session, HttpMessageHandler handler, TimeProvider? clock = null)
    {
        var type = typeof(ILlmClient).Assembly.GetType("AIAgent.Cli.Llm.OpenAiClient");
        Assert.IsNotNull(type, "MODEL-AC-013: native adapter is required");
        return (ILlmClient)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [session, handler, clock], null)!;
    }
    internal static LlmRequest Request(IReadOnlyList<LlmToolDefinition>? tools = null) => new("synthetic instruction", [LlmMessage.FromUserText("question")], tools ?? []);
    internal static string TextResponse => Response("[{\"type\":\"message\",\"id\":\"message-one\",\"role\":\"assistant\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":\"answer\",\"annotations\":[]}]}]");
    internal static string Response(string output, string status = "completed", string? reason = null) => "{\"id\":\"synthetic-response\",\"object\":\"response\",\"status\":\"" + status + "\",\"output\":" + output + ",\"usage\":{}" + (reason is null ? "" : ",\"incomplete_details\":{\"reason\":\"" + reason + "\"}") + "}";
}

internal sealed class OpenAiTestHandler : HttpMessageHandler
{
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Respond { get; set; }
    internal List<byte[]> Bodies { get; } = [];
    internal string Response { get; set; } = OpenAiTestData.TextResponse;
    internal HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    internal bool OfficialDestination { get; private set; } = true;
    internal bool AuthenticationCorrect { get; private set; } = true;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Bodies.Add(await request.Content!.ReadAsByteArrayAsync(token));
        OfficialDestination &= request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri == "https://api.openai.com/v1/responses";
        AuthenticationCorrect &= request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == OpenAiTestData.Key && !request.Headers.Contains("x-goog-api-key");
        if (Respond is not null) return await Respond(request, token);
        return new(Status) { Content = new StringContent(Response, Encoding.UTF8, "application/json") };
    }
}