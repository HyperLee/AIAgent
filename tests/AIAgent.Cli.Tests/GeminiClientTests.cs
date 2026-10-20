using System.Net;
using System.Text;
using System.Text.Json;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GeminiClientTests
{
    [TestMethod]
    public async Task GEMAC003_OfficialEndpointAuthenticationAndGenerationConfig()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler);
        Assert.IsTrue(handler.Calls == 0, "GEM-AC-007");
        var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        var config = body.RootElement.GetProperty("generationConfig");
        Assert.IsTrue(result.Status == LlmStatus.Completed && result.SafeText == "answer" && result.RequestDispatched && result.ModelMessage is not null, "GEM-AC-003");
        Assert.IsTrue(handler.EndpointCorrect && handler.AuthenticationCorrect && config.GetProperty("candidateCount").GetInt32() == 1 && config.GetProperty("maxOutputTokens").GetInt32() == 8192, "GEM-AC-005");
        Assert.IsFalse(Encoding.UTF8.GetString(handler.Bodies.Single()).Contains(GeminiFixtures.Key, StringComparison.Ordinal), "GEM-AC-003");
    }

    [TestMethod]
    public async Task GEMAC004_ExplicitSettingsOverrideEnvironmentAndGlobalBaseUrl()
    {
        string[] names = ["GOOGLE_API_KEY", "GEMINI_API_KEY", "GOOGLE_GENAI_USE_VERTEXAI", "GOOGLE_GENAI_USE_ENTERPRISE", "GOOGLE_CLOUD_PROJECT", "GOOGLE_CLOUD_LOCATION", "GOOGLE_GEMINI_BASE_URL", "GOOGLE_VERTEX_BASE_URL"];
        var saved = names.Select(Environment.GetEnvironmentVariable).ToArray();
        // SDK has a write-only public setter; retain and restore its exact private state in this serialized test.
        var field = typeof(Google.GenAI.Client).GetField("geminiBaseUrl", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var oldBase = field.GetValue(null);
        var vertexField = typeof(Google.GenAI.Client).GetField("vertexBaseUrl", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var oldVertex = vertexField.GetValue(null);
        try
        {
            foreach (var name in names) Environment.SetEnvironmentVariable(name, name.Contains("USE_", StringComparison.Ordinal) ? "true" : "FAKE_CONFLICT_ONLY");
            Google.GenAI.Client.setDefaultBaseUrl(vertexBaseUrl: "https://invalid.example", geminiBaseUrl: "https://invalid.example");
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
            Assert.IsTrue(result.Status == LlmStatus.Completed && handler.EndpointCorrect && handler.AuthenticationCorrect && handler.Calls == 1, "GEM-AC-004");
        }
        finally
        {
            for (var i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], saved[i]);
            field.SetValue(null, oldBase);
            vertexField.SetValue(null, oldVertex);
        }
    }

    [TestMethod]
    public async Task GEMAC006_ActualUtf8BodyBoundaryIncludesSchemaEscapingAndSignatures()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler);
        var tools = new[] { GeminiFixtures.Tool() };
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response("[{\"text\":\"history\",\"thoughtSignature\":\"c2lnbmF0dXJl\"}]")));
        var history = (await client.GenerateAsync(GeminiFixtures.Request(), default)).ModelMessage!;
        LlmRequest Request(int padding) => new("中文\"\\\n", [LlmMessage.FromUserText("history"), history, LlmMessage.FromUserText("中文\"\\\n" + new string('x', padding))], tools);
        await client.GenerateAsync(Request(0), default);
        var padding = 262144 - handler.Bodies[^1].Length;
        var before = handler.Calls;
        var allowed = await client.GenerateAsync(Request(padding), default);
        Assert.IsTrue(allowed.Status == LlmStatus.Completed && handler.Calls == before + 1 && handler.Bodies[^1].Length == 262144, "GEM-AC-006");
        var rejected = await client.GenerateAsync(Request(padding + 1), default);
        Assert.IsTrue(rejected.ErrorCode == LlmErrorCode.InputTooLarge && !rejected.RequestDispatched && handler.Calls == before + 1, "GEM-AC-006");
    }

    [TestMethod]
    public async Task GEMAC007_TransientFailuresAndContinuationNeverRetry()
    {
        foreach (var code in new[] { 429, 500, 503, 302 })
        {
            using var session = GeminiFixtures.Session();
            var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json("{}", (HttpStatusCode)code)) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
            Assert.IsTrue(result.Status == LlmStatus.Failed && result.RequestDispatched && handler.Calls == 1, "GEM-AC-007");
        }
        using var session2 = GeminiFixtures.Session();
        var continuation = GeminiFixtures.Text("partial", "CONTINUATION").Replace("\"finishReason\"", "\"continuationToken\":\"dG9rZW4=\",\"finishReason\"", StringComparison.Ordinal);
        var handler2 = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(continuation)) };
        await using var client2 = new GeminiClient(session2, handler2);
        var truncated = await client2.GenerateAsync(GeminiFixtures.Request(), default);
        Assert.IsTrue(truncated.Status == LlmStatus.Truncated && handler2.Calls == 1 && truncated.ModelMessage is null, "GEM-AC-007");
    }

    [TestMethod]
    public async Task GEMAC008_InvalidRequestsNeverDispatch()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler);
        LlmRequest[] invalid = [null!, new("instruction", [], []), GeminiFixtures.Request(" "), GeminiFixtures.Request(null!), new("instruction", [null!], []), new("instruction", [LlmMessage.FromToolResults([])], []), new("instruction", [LlmMessage.FromUserText("question")], [new("sample", "description", GeminiFixtures.Element("[]"))])];
        foreach (var request in invalid)
        {
            var result = await client.GenerateAsync(request, default);
            Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidRequest && !result.RequestDispatched && handler.Calls == 0, "GEM-AC-008");
        }
    }

    [TestMethod]
    public async Task GEMAC009_TextProjectionExcludesThoughtAndPreservesOriginalParts()
    {
        using var session = GeminiFixtures.Session();
        var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response("[{\"text\":\"first\"},{\"text\":\"hidden\",\"thought\":true,\"thoughtSignature\":\"c2ln\"},{\"text\":\"last\"}]"))) };
        await using var client = new GeminiClient(session, handler);
        var first = await client.GenerateAsync(GeminiFixtures.Request(), default);
        Assert.IsTrue(first.SafeText == "firstlast", "GEM-AC-009");
        await client.GenerateAsync(new("instruction", [LlmMessage.FromUserText("question"), first.ModelMessage!, LlmMessage.FromUserText("again")], []), default);
        using var body = JsonDocument.Parse(handler.Bodies[1]);
        var parts = body.RootElement.GetProperty("contents")[1].GetProperty("parts");
        Assert.IsTrue(parts.GetArrayLength() == 3 && parts[1].GetProperty("text").GetString() == "hidden" && parts[1].GetProperty("thoughtSignature").GetString() == "c2ln", "GEM-AC-009");
    }

    [TestMethod]
    public async Task GEMAC014_ResponseStatusesNeverPublishPartialSuccess()
    {
        var cases = new (string Json, LlmStatus Status, LlmErrorCode Error)[]
        {
            ("{}", LlmStatus.Empty, LlmErrorCode.None),
            ("{\"promptFeedback\":{\"blockReason\":\"BLOCKED_REASON_UNSPECIFIED\"}}", LlmStatus.Empty, LlmErrorCode.None),
            ("{\"promptFeedback\":{\"blockReason\":\"BLOCK_REASON_UNSPECIFIED\"}}", LlmStatus.Empty, LlmErrorCode.None),
            (GeminiFixtures.Text(" "), LlmStatus.Empty, LlmErrorCode.None),
            (GeminiFixtures.Response("[{\"thought\":true,\"text\":\"hidden\"}]"), LlmStatus.Empty, LlmErrorCode.None),
            ("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}", LlmStatus.Refused, LlmErrorCode.None),
            ("{", LlmStatus.Failed, LlmErrorCode.InvalidResponse),
            ("[]", LlmStatus.Failed, LlmErrorCode.InvalidResponse),
            ("{\"candidates\":[{},{}]}", LlmStatus.Failed, LlmErrorCode.InvalidResponse),
            ("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"partial\"}]}}]}", LlmStatus.Failed, LlmErrorCode.InvalidResponse),
            (GeminiFixtures.Text("partial", "UNKNOWN_REASON"), LlmStatus.Failed, LlmErrorCode.InvalidResponse),
            (GeminiFixtures.Response("[{\"inlineData\":{\"mimeType\":\"image/png\",\"data\":\"YQ==\"}}]"), LlmStatus.Failed, LlmErrorCode.InvalidResponse)
        };
        var caseIndex = 0;
        foreach (var test in cases)
        {
            caseIndex++;
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(test.Json)) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
            Assert.IsTrue(result.Status == test.Status && result.ErrorCode == test.Error && result.ModelMessage is null && result.ToolCalls.Count == 0, $"GEM-AC-014/016-{caseIndex}: {result.Status}/{result.ErrorCode}");
            if (test.Error == LlmErrorCode.InvalidResponse) Assert.IsTrue(result.RequiresContextReset, "GEM-AC-016");
        }
        foreach (var reason in new[] { "SAFETY", "RECITATION", "BLOCKLIST", "PROHIBITED_CONTENT", "SPII", "MAX_TOKENS", "CONTINUATION", "MALFORMED_FUNCTION_CALL", "UNEXPECTED_TOOL_CALL", "TOO_MANY_TOOL_CALLS" })
        {
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Calls().Replace("STOP", reason, StringComparison.Ordinal))) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
            var expected = reason is "MAX_TOKENS" or "CONTINUATION" ? LlmStatus.Truncated : reason.Contains("CALL", StringComparison.Ordinal) ? LlmStatus.Failed : LlmStatus.Refused;
            Assert.IsTrue(result.Status == expected && result.ToolCalls.Count == 0 && result.ModelMessage is null, "GEM-AC-015/016");
        }
    }

    [TestMethod]
    public async Task GEMAC017_SensitiveInputIsRejectedAcrossAllBoundaries()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler);
        var escaped = "{\"description\":\"FAKE_KEY_FOR_TESTS_ONL\\u0059\",\"type\":\"object\"}";
        LlmRequest[] requests = [GeminiFixtures.Request(GeminiFixtures.Key), new(GeminiFixtures.Key, [LlmMessage.FromUserText("question")], []), new("instruction", [LlmMessage.FromUserText(GeminiFixtures.Key), LlmMessage.FromUserText("question")], []), GeminiFixtures.Request(tools: [new("sample", GeminiFixtures.Key, GeminiFixtures.Element("{}"))]), GeminiFixtures.Request(tools: [new(GeminiFixtures.Key, "description", GeminiFixtures.Element("{}"))]), GeminiFixtures.Request(tools: [new("sample", "description", GeminiFixtures.Element("{\"" + GeminiFixtures.Key + "\":{}}"))]), GeminiFixtures.Request(tools: [new("sample", "description", GeminiFixtures.Element(escaped))])];
        foreach (var request in requests)
        {
            var result = await client.GenerateAsync(request, default);
            Assert.IsTrue(result.ErrorCode == LlmErrorCode.SensitiveInput && !result.RequestDispatched && handler.Calls == 0, "GEM-AC-017");
        }
    }

    [TestMethod]
    public async Task GEMAC018_SensitiveOutputHasNoReusableContentButKeepsUsage()
    {
        var parts = new[] { "[{\"text\":\"" + GeminiFixtures.Key + "\"}]", "[{\"text\":\"FAKE_KEY_\"},{\"text\":\"FOR_TESTS_ONLY\"}]", "[{\"text\":\"answer\",\"partMetadata\":{\"nested\":\"" + GeminiFixtures.Key + "\"}}]", "[{\"functionCall\":{\"name\":\"" + GeminiFixtures.Key + "\",\"args\":{}}}]", "[{\"functionCall\":{\"name\":\"sample\",\"id\":\"" + GeminiFixtures.Key + "\",\"args\":{}}}]", "[{\"functionCall\":{\"name\":\"sample\",\"args\":{\"nested\":[\"" + GeminiFixtures.Key + "\"]}}}]" };
        foreach (var data in parts)
        {
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(data, usage: "{\"totalTokenCount\":17}"))) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
            Assert.IsTrue(result.ErrorCode == LlmErrorCode.SensitiveOutput && result.ToolCalls.Count == 0 && result.ModelMessage is null && result.RequiresContextReset && result.Usage?.TotalTokens == 17 && !result.SafeText.Contains(GeminiFixtures.Key, StringComparison.Ordinal), "GEM-AC-018");
        }
    }

    [TestMethod]
    public async Task GEMAC020_HttpAndTrustedErrorInfoClassifications()
    {
        var cases = new (int Code, LlmErrorCode Error)[] { (401,LlmErrorCode.AuthenticationFailed),(400,LlmErrorCode.InvalidRequest),(422,LlmErrorCode.InvalidRequest),(403,LlmErrorCode.PermissionDenied),(404,LlmErrorCode.ModelUnavailable),(429,LlmErrorCode.QuotaOrRateLimited),(408,LlmErrorCode.Timeout),(504,LlmErrorCode.Timeout),(500,LlmErrorCode.ServiceUnavailable),(503,LlmErrorCode.ServiceUnavailable),(302,LlmErrorCode.InvalidResponse) };
        foreach (var test in cases) await CheckError(test.Code, "{\"error\":{\"message\":\"" + GeminiFixtures.Key + "\"}}", test.Error);
        foreach (var reason in new[] { "API_KEY_INVALID", "API_KEY_EXPIRED" })
            foreach (var status in new[] { 400, 403 }) await CheckError(status, ErrorInfo("googleapis.com", reason), LlmErrorCode.AuthenticationFailed);
        await CheckError(403, ErrorInfo("unknown.example", "API_KEY_INVALID"), LlmErrorCode.PermissionDenied);
        await CheckError(400, ErrorInfo("googleapis.com", "SOMETHING_ELSE"), LlmErrorCode.InvalidRequest);
        await CheckError(500, ErrorInfo("googleapis.com", "API_KEY_INVALID"), LlmErrorCode.ServiceUnavailable);
        await CheckError(403, ErrorInfo("googleapis.com", "API_KEY_INVALID") + new string(' ', 65536), LlmErrorCode.PermissionDenied);
        var bounded = ErrorInfo("googleapis.com", "API_KEY_INVALID");
        await CheckError(403, bounded.PadRight(65536), LlmErrorCode.AuthenticationFailed);
        await CheckError(403, bounded.PadRight(65537), LlmErrorCode.PermissionDenied);
        await CheckError(403, "{" + GeminiFixtures.Key, LlmErrorCode.PermissionDenied);
    }

    [TestMethod]
    public async Task GEMAC019_ExceptionsAndMalformedResponsesNeverReachConsoleOrResults()
    {
        using var output = new StringWriter(); using var errors = new StringWriter();
        var oldOutput = Console.Out; var oldError = Console.Error;
        try
        {
            Console.SetOut(output); Console.SetError(errors);
            foreach (var mode in new[] { "network", "sdk", "json" })
            {
                using var session = GeminiFixtures.Session();
                var handler = new GeminiHandler { Respond = (_, _) => mode switch { "network" => throw new HttpRequestException(GeminiFixtures.Key), "sdk" => throw new InvalidOperationException(GeminiFixtures.Key), _ => Task.FromResult(GeminiHandler.Json("{" + GeminiFixtures.Key)) } };
                await using var client = new GeminiClient(session, handler);
                var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
                Assert.IsTrue(result.Status == LlmStatus.Failed && !JsonSerializer.Serialize(result).Contains(GeminiFixtures.Key, StringComparison.Ordinal) && handler.Calls == 1, "GEM-AC-019");
                if (mode == "network") Assert.IsTrue(result.ErrorCode == LlmErrorCode.NetworkFailure, "GEM-AC-020");
            }
            Assert.IsTrue(output.ToString().Length == 0 && errors.ToString().Length == 0, "GEM-AC-019");
        }
        finally { Console.SetOut(oldOutput); Console.SetError(oldError); }
    }

    [TestMethod]
    public async Task GEMAC023_UsagePreservesMissingZeroNegativeAndNonSuccessfulValues()
    {
        var values = new (string? Json, LlmUsage? Expected)[] { (null,null),("{}",new(null,null,null,null,null)),("{\"promptTokenCount\":0,\"candidatesTokenCount\":2,\"thoughtsTokenCount\":3,\"cachedContentTokenCount\":4,\"totalTokenCount\":19}",new(0,2,3,4,19)),("{\"promptTokenCount\":-1,\"totalTokenCount\":0}",new(null,null,null,null,0)) };
        foreach (var test in values)
        {
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Text(usage: test.Json))) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
            Assert.IsTrue(result.Usage == test.Expected, "GEM-AC-023");
        }
        foreach (var pair in new[] { ("", "STOP"), ("partial", "MAX_TOKENS"), ("blocked", "SAFETY") })
        {
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Text(pair.Item1, pair.Item2, "{\"promptTokenCount\":12}"))) };
            await using var client = new GeminiClient(session, handler);
            Assert.IsTrue((await client.GenerateAsync(GeminiFixtures.Request(), default)).Usage == new LlmUsage(12,null,null,null,null), "GEM-AC-023");
        }
    }

    private static string ErrorInfo(string domain, string reason) => "{\"error\":{\"status\":\"INVALID_ARGUMENT\",\"details\":[{\"@type\":\"type.googleapis.com/google.rpc.ErrorInfo\",\"domain\":\"" + domain + "\",\"reason\":\"" + reason + "\",\"metadata\":{\"ApiKey\":\"" + GeminiFixtures.Key + "\"}}]}}";
    private static async Task CheckError(int status, string body, LlmErrorCode expected)
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(body, (HttpStatusCode)status)) };
        await using var client = new GeminiClient(session, handler);
        var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
        Assert.IsTrue(result.ErrorCode == expected && handler.Calls == 1 && !result.SafeText.Contains(GeminiFixtures.Key, StringComparison.Ordinal), "GEM-AC-020");
    }
}
