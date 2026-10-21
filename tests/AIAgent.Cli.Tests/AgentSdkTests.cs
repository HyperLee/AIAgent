using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class AgentSdkTests
{
    [TestMethod]
    public async Task LOOPAC004_RawDuplicateCallFieldsAreRejectedBeforeDictionaryConversion()
    {
        foreach (var call in new[] {
            "{\"name\":\"sample\",\"name\":\"sample\",\"args\":{}}",
            "{\"name\":\"sample\",\"args\":{\"path\":\"a\",\"p\\u0061th\":\"b\"}}",
            "{\"name\":\"sample\",\"args\":{\"nested\":{\"v\":1,\"v\":2}}}" })
        {
            using var session = GeminiFixtures.Session();
            var handler = new GeminiHandler
            {
                Respond = (_, _) => Task.FromResult(GeminiHandler.Json(
                GeminiFixtures.Response("[{\"functionCall\":" + call + "}]", usage: "{\"totalTokenCount\":17}")))
            };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
            Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidResponse && result.ModelMessage is null &&
                result.ToolCalls.Count == 0 && result.RequiresContextReset && result.Usage?.TotalTokens == 17, "LOOP-AC-004");
        }
    }

    [TestMethod]
    public async Task LOOPAC009_ModelProjectionAndResultOrderPreserveNativeCalls()
    {
        using var session = GeminiFixtures.Session();
        var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Calls())) };
        await using var client = new GeminiClient(session, handler);
        var first = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
        Assert.AreEqual(2, first.ModelMessage!.ModelCalls.Count, "LOOP-AC-009");
        var reversed = first.ToolCalls.Select(GeminiFixtures.Result).Reverse().ToArray();
        var rejected = await client.GenerateAsync(new("instruction", [LlmMessage.FromUserText("q"), first.ModelMessage,
            LlmMessage.FromToolResults(reversed)], [GeminiFixtures.Tool()]), default);
        Assert.IsTrue(rejected.ErrorCode == LlmErrorCode.InvalidRequest && !rejected.RequestDispatched && handler.Calls == 1, "LOOP-AC-010");
    }
}