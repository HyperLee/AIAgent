using System.Text.Json;
using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GeminiProtocolTests
{
    [TestMethod]
    public async Task GEMAC016_PartWithMultipleDataKindsIsRejected()
    {
        using var session = GeminiFixtures.Session();
        var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response("[{\"text\":\"partial\",\"functionCall\":{\"name\":\"sample\",\"args\":{}}}]"))) };
        await using var client = new GeminiClient(session, handler);
        var result = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
        Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidResponse && result.ModelMessage is null && result.ToolCalls.Count == 0 && result.RequiresContextReset, "GEM-AC-016");
    }

    [TestMethod]
    public async Task GEMAC016_UnsupportedOrPartialFunctionCallIsRejectedBeforePublication()
    {
        foreach (var field in new[] { "\"willContinue\":true", "\"willContinue\":false", "\"partialArgs\":[]", "\"unknownProtocolField\":true" })
        {
            using var session = GeminiFixtures.Session();
            var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"sample\",\"args\":{}," + field + "}}]"))) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
            Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidResponse && result.ModelMessage is null && result.ToolCalls.Count == 0 && result.RequiresContextReset, "GEM-AC-016");
        }
    }

    [TestMethod]
    public async Task GEMAC016_UnknownPromptBlockReasonCannotBecomeKnownRefusal()
    {
        using var session = GeminiFixtures.Session();
        var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json("{\"promptFeedback\":{\"blockReason\":\"NEW_UNKNOWN\"}}")) };
        await using var client = new GeminiClient(session, handler);
        var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
        Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidResponse && result.ModelMessage is null && result.RequiresContextReset, "GEM-AC-016");
    }

    [TestMethod]
    public async Task GEMAC010_AllCallsPreserveOriginalPartIndexIdsAndArguments()
    {
        foreach (var ids in new[] { true, false })
        {
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Calls(ids))) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
            Assert.IsTrue(result.Status == LlmStatus.ToolCalls && result.SafeText == "working" && result.ModelMessage is not null && result.ToolCalls.Count == 2, "GEM-AC-010");
            Assert.IsTrue(result.ToolCalls[0].PartIndex == 1 && result.ToolCalls[1].PartIndex == 2 && result.ToolCalls[0].Arguments.GetProperty("value").GetInt32() == 1 && result.ToolCalls[1].Arguments.GetProperty("value").GetInt32() == 2, "GEM-AC-010");
            Assert.IsTrue(result.ToolCalls[0].CallId == (ids ? "call-a" : null), "GEM-AC-010");
        }
    }

    [TestMethod]
    public async Task GEMAC011_TwoExplicitToolStepsKeepSignaturesAndCompleteContents()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls <= 2 ? GeminiFixtures.Calls() : GeminiFixtures.Text()));
        await using var client = new GeminiClient(session, handler);
        var messages = new List<LlmMessage> { LlmMessage.FromUserText("question") };
        for (var step = 0; step < 2; step++)
        {
            var result = await client.GenerateAsync(new("instruction", messages, [GeminiFixtures.Tool()]), default);
            Assert.IsTrue(result.Status == LlmStatus.ToolCalls, "GEM-AC-011");
            var results = result.ToolCalls.Select(GeminiFixtures.Result).Reverse().ToArray();
            messages.Add(result.ModelMessage!); messages.Add(LlmMessage.FromToolResults(results));
        }
        var completed = await client.GenerateAsync(new("instruction", messages, [GeminiFixtures.Tool()]), default);
        Assert.IsTrue(completed.Status == LlmStatus.Completed && handler.Calls == 3, "GEM-AC-011");
        using var body = JsonDocument.Parse(handler.Bodies[^1]); var contents = body.RootElement.GetProperty("contents");
        Assert.IsTrue(contents.GetArrayLength() == 5, "GEM-AC-011");
        foreach (var index in new[] { 1, 3 })
        {
            var parts = contents[index].GetProperty("parts"); var results = contents[index + 1].GetProperty("parts");
            Assert.IsTrue(parts.GetArrayLength() == 3 && parts[1].GetProperty("thoughtSignature").GetString() == "c2lnbmF0dXJl" && results.GetArrayLength() == 2, "GEM-AC-011");
            Assert.IsTrue(results[0].GetProperty("functionResponse").GetProperty("id").GetString() == "call-a" && results[1].GetProperty("functionResponse").GetProperty("id").GetString() == "call-b" && !results[0].GetProperty("functionResponse").TryGetProperty("partIndex", out _), "GEM-AC-011");
        }
    }

    [TestMethod]
    public async Task GEMAC012_SameNameNoIdCallsRequireEveryMatchingResult()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Calls(false))) };
        await using var client = new GeminiClient(session, handler);
        var first = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
        var a = GeminiFixtures.Result(first.ToolCalls[0]); var b = GeminiFixtures.Result(first.ToolCalls[1]);
        var invalid = new IReadOnlyList<LlmToolResult>[] { [a], [a,a], [a,new(b.PartIndex,null,"wrong",b.Response)], [a,new(b.PartIndex,"wrong",b.Name,b.Response)], [a,new(8,null,b.Name,b.Response)] };
        foreach (var results in invalid)
        {
            var result = await client.GenerateAsync(new("instruction", [LlmMessage.FromUserText("question"), first.ModelMessage!, LlmMessage.FromToolResults(results)], [GeminiFixtures.Tool()]), default);
            Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidRequest && !result.RequestDispatched && handler.Calls == 1, "GEM-AC-012");
        }
        var valid = await client.GenerateAsync(new("instruction", [LlmMessage.FromUserText("question"), first.ModelMessage!, LlmMessage.FromToolResults([b,a])], [GeminiFixtures.Tool()]), default);
        Assert.IsTrue(valid.Status == LlmStatus.ToolCalls && handler.Calls == 2, "GEM-AC-012");
    }

    [TestMethod]
    public async Task GEMAC013_UnexpectedAndMalformedCallsNeverPublishCalls()
    {
        foreach (var data in new[] { GeminiFixtures.Calls(), GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"\",\"args\":{}}}]"), GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"sample\",\"args\":[]}}]") })
        {
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(data)) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: data == GeminiFixtures.Calls() ? [] : [GeminiFixtures.Tool()]), default);
            Assert.IsTrue(result.Status == LlmStatus.Failed && result.ToolCalls.Count == 0 && result.ModelMessage is null && result.RequiresContextReset, "GEM-AC-013");
        }
        using var session2 = GeminiFixtures.Session(); var plain = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Text("```json {functionCall:sample} ```"))) };
        await using var client2 = new GeminiClient(session2, plain);
        Assert.IsTrue((await client2.GenerateAsync(GeminiFixtures.Request(), default)).Status == LlmStatus.Completed, "GEM-AC-013");
    }

    [TestMethod]
    public async Task GEMAC017_ToolResultKeyIsBlockedBeforeNextTransmission()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Calls())) };
        await using var client = new GeminiClient(session, handler);
        var first = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
        var results = first.ToolCalls.Select(GeminiFixtures.Result).ToArray();
        results[0] = new(results[0].PartIndex, results[0].CallId, results[0].Name, GeminiFixtures.Element("{\"output\":\"" + GeminiFixtures.Key + "\"}"));
        var rejected = await client.GenerateAsync(new("instruction", [LlmMessage.FromUserText("question"), first.ModelMessage!, LlmMessage.FromToolResults(results)], [GeminiFixtures.Tool()]), default);
        Assert.IsTrue(rejected.ErrorCode == LlmErrorCode.SensitiveInput && !rejected.RequestDispatched && handler.Calls == 1, "GEM-AC-017");
    }

    [TestMethod]
    public async Task GEMAC022_OpaqueContentCannotCrossClientOrSession()
    {
        using var session = GeminiFixtures.Session(); var firstHandler = new GeminiHandler();
        var first = new GeminiClient(session, firstHandler);
        var message = (await first.GenerateAsync(GeminiFixtures.Request(), default)).ModelMessage!;
        await first.DisposeAsync(); await first.DisposeAsync();
        Assert.IsTrue(firstHandler.Disposed && (await first.GenerateAsync(GeminiFixtures.Request(), default)).ErrorCode == LlmErrorCode.ClientUnavailable && firstHandler.Calls == 1, "GEM-AC-022");
        using var session2 = GeminiFixtures.Session(); var secondHandler = new GeminiHandler();
        await using var second = new GeminiClient(session2, secondHandler);
        var result = await second.GenerateAsync(new("instruction", [LlmMessage.FromUserText("question"), message, LlmMessage.FromUserText("again")], []), default);
        Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidRequest && secondHandler.Calls == 0 && session.Workspace.RootPath == "/unused-workspace", "GEM-AC-022");
    }
}
