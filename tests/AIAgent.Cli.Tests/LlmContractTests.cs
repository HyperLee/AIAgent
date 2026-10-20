using System.Text.Json;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class LlmContractTests
{
    [TestMethod]
    public void GEMAC008_RequestSnapshotsListsAndJsonOwnership()
    {
        var document = JsonDocument.Parse("{\"value\":1}");
        var tool = new LlmToolDefinition("sample", "description", document.RootElement);
        var call = new LlmToolCall(2, null, "sample", document.RootElement);
        var result = new LlmToolResult(2, null, "sample", document.RootElement);
        var messages = new List<LlmMessage> { LlmMessage.FromUserText("question") };
        var tools = new List<LlmToolDefinition> { tool };
        var results = new List<LlmToolResult> { result };
        var toolMessage = LlmMessage.FromToolResults(results);
        var request = new LlmRequest("instruction", messages, tools);
        messages.Clear(); tools.Clear(); results.Clear(); document.Dispose();
        Assert.IsTrue(request.Messages.Count == 1 && request.Tools.Count == 1 && toolMessage.Results.Count == 1, "GEM-AC-008");
        Assert.IsTrue(tool.Parameters.GetProperty("value").GetInt32() == 1 && call.Arguments.GetProperty("value").GetInt32() == 1 && result.Response.GetProperty("value").GetInt32() == 1, "GEM-AC-008");
        foreach (var content in new object[] { request, tool, call, result, toolMessage })
            Assert.IsFalse(content.ToString()!.Contains("description", StringComparison.Ordinal) || content.ToString()!.Contains("question", StringComparison.Ordinal), "GEM-AC-008");
        try { _ = JsonSerializer.Serialize(toolMessage); Assert.Fail("GEM-AC-008"); } catch (NotSupportedException) { }
    }

    [TestMethod]
    public void GEMAC001_CorePublicInterfacesDoNotExposeSdkTypes()
    {
        Type[] types = [typeof(ILlmClient), typeof(LlmRequest), typeof(LlmMessage), typeof(LlmToolDefinition), typeof(LlmToolCall), typeof(LlmToolResult), typeof(LlmResult), typeof(LlmUsage)];
        foreach (var type in types)
        {
            var signatures = type.GetProperties().Select(p => p.PropertyType.ToString()).Concat(type.GetMethods().Select(m => m.ReturnType.ToString()));
            Assert.IsFalse(signatures.Any(s => s.Contains("Google.GenAI", StringComparison.Ordinal)), "GEM-AC-001");
        }
    }

    [TestMethod]
    public void GEMAC024_SyntheticConsumerDistinguishesLongValuesMissingAndUnsent()
    {
        // This is an executable handoff example, not a product accumulator or CLI implementation.
        LlmResult Result(bool sent, LlmUsage? usage) => new(LlmStatus.Empty, LlmErrorCode.None, "", [], null, usage, sent, false);
        var sequence = new[] { Result(true, new(3_000_000_000,0,null,7,23)), Result(false,null), Result(true,new(4,2,null,null,null)), Result(true,null) };
        var dispatched = sequence.Where(r => r.RequestDispatched).ToArray();
        var known = dispatched.Select(r => r.Usage?.PromptTokens).Where(v => v.HasValue).Sum(v => v!.Value);
        Assert.IsTrue(dispatched.Length == 3 && known == 3_000_000_004L && dispatched.Count(r => r.Usage?.PromptTokens is null) == 1 && dispatched.Count(r => r.Usage?.ThoughtTokens is null) == 3, "GEM-AC-024");
        Assert.IsTrue(dispatched[0].Usage?.TotalTokens == 23 && dispatched[0].Usage?.OutputTokens == 0 && dispatched[0].Usage?.CachedTokens == 7, "GEM-AC-024");
        var overflow = new[] { Result(true,new(null,null,null,null,long.MaxValue)), Result(true,new(null,null,null,null,1)) };
        try { _ = checked(overflow[0].Usage!.TotalTokens!.Value + overflow[1].Usage!.TotalTokens!.Value); Assert.Fail("GEM-AC-024"); }
        catch (OverflowException) { /* Stage 03 must flag incomplete accumulation rather than wrap or resend. */ }
    }
}
