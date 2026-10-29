using System.Net;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class OpenAiBoundaryTests
{
    [TestMethod]
    public async Task MODELAC015_CompletedResponseCannotPublishIncompleteNativeItem()
    {
        using var session = OpenAiTestData.Session();
        var handler = new OpenAiTestHandler { Response = OpenAiTestData.Response("[{\"type\":\"function_call\",\"id\":\"i\",\"call_id\":\"c\",\"name\":\"sample\",\"arguments\":\"{}\",\"status\":\"in_progress\"}]") };
        await using var client = OpenAiTestData.Client(session, handler);
        var result = await client.GenerateAsync(OpenAiTestData.Request([GeminiFixtures.Tool()]), default);
        Assert.AreEqual(LlmErrorCode.InvalidResponse, result.ErrorCode);
        Assert.AreEqual(0, result.ToolCalls.Count); Assert.IsNull(result.ModelMessage);
    }
    [TestMethod]
    public async Task MODELAC001_UnsupportedFullToolSchemaStopsBeforeGenerationAdmission()
    {
        using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler();
        await using var client = OpenAiTestData.Client(session, handler);
        var budget = new GenerationBudget();
        var registry = TestRulesRegistry.Create([new UnsupportedSchemaTool()], session.Policy);
        var runner = new AgentRunner(client, registry, session.Policy, "synthetic instruction", new(), _ => { }, budget);
        var result = await runner.RunAsync([LlmMessage.FromUserText("question")], default);
        Assert.AreEqual("ModelCapabilityUnavailable", result.StopReason.ToString());
        Assert.AreEqual(0, budget.Snapshot().ProgramGenerationsStarted);
        Assert.AreEqual(0, handler.Bodies.Count);
    }

    private sealed class UnsupportedSchemaTool : ITool
    {
        public LlmToolDefinition Definition { get; } = new("unsupported_schema", "description", GeminiFixtures.Element("{\"type\":\"object\",\"$ref\":\"unknown\"}"));
        public ToolCapability Capability => ToolCapability.WorkspaceRead;
        public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken token) => throw new InvalidOperationException("unexpected execution");
    }
    [TestMethod]
    public async Task MODELAC018_ResponseByteAndDepthBoundsRejectWholeDocument()
    {
        foreach (var size in new[] { 1048576, 1048577 })
        {
            using var session = OpenAiTestData.Session(); var text = OpenAiTestData.TextResponse;
            var handler = new OpenAiTestHandler { Response = text + new string(' ', size - Encoding.UTF8.GetByteCount(text)) };
            await using var client = OpenAiTestData.Client(session, handler);
            var result = await client.GenerateAsync(OpenAiTestData.Request(), default);
            Assert.AreEqual(size == 1048576 ? LlmStatus.Completed : LlmStatus.Failed, result.Status);
            if (size > 1048576) { Assert.AreEqual(LlmErrorCode.InvalidResponse, result.ErrorCode); Assert.IsTrue(result.RequiresContextReset); }
        }
        foreach (var depth in new[] { 64, 65 })
        {
            using var session = OpenAiTestData.Session();
            var metadata = new string('[', depth - 1) + "0" + new string(']', depth - 1);
            var handler = new OpenAiTestHandler { Response = OpenAiTestData.TextResponse[..^1] + ",\"extra\":" + metadata + "}" };
            await using var client = OpenAiTestData.Client(session, handler);
            Assert.AreEqual(depth == 64 ? LlmStatus.Completed : LlmStatus.Failed, (await client.GenerateAsync(OpenAiTestData.Request(), default)).Status);
        }
    }

    [TestMethod]
    public async Task MODELAC019_SplitVisibleSecretAndEscapedArgumentsRequireReset()
    {
        foreach (var output in new[]
        {
            "[{\"type\":\"message\",\"id\":\"m\",\"role\":\"assistant\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":\"FAKE_OPENAI_\",\"annotations\":[]},{\"type\":\"output_text\",\"text\":\"AUTH_ONLY\",\"annotations\":[]}]}]",
            "[{\"type\":\"function_call\",\"id\":\"i\",\"call_id\":\"c\",\"name\":\"sample\",\"arguments\":\"{\\\"x\\\":\\\"\\\\u0046AKE_OPENAI_AUTH_ONLY\\\"}\"}]"
        })
        {
            using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler { Response = OpenAiTestData.Response(output) };
            await using var client = OpenAiTestData.Client(session, handler);
            var result = await client.GenerateAsync(OpenAiTestData.Request([GeminiFixtures.Tool()]), default);
            Assert.AreEqual(LlmErrorCode.SensitiveOutput, result.ErrorCode);
            Assert.IsTrue(result.RequiresContextReset); Assert.AreEqual(0, result.ToolCalls.Count); Assert.IsNull(result.ModelMessage);
            Assert.IsFalse(result.SafeText.Contains(OpenAiTestData.Key, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task MODELAC015_DuplicateIdsRefusalMixedWithCallsAndUnpromptedCallsAreRejected()
    {
        var call = "{\"type\":\"function_call\",\"id\":\"i\",\"call_id\":\"c\",\"name\":\"sample\",\"arguments\":\"{}\"}";
        var refusal = "{\"type\":\"message\",\"id\":\"m\",\"role\":\"assistant\",\"status\":\"completed\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"no\"}]}";
        foreach (var output in new[] { "[" + call + "," + call + "]", "[" + refusal + "," + call + "]" })
        {
            using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler { Response = OpenAiTestData.Response(output) };
            await using var client = OpenAiTestData.Client(session, handler);
            var result = await client.GenerateAsync(OpenAiTestData.Request([GeminiFixtures.Tool()]), default);
            Assert.AreEqual(LlmErrorCode.InvalidResponse, result.ErrorCode); Assert.AreEqual(0, result.ToolCalls.Count);
        }
        using var emptySession = OpenAiTestData.Session(); var unsolicited = new OpenAiTestHandler { Response = OpenAiTestData.Response("[" + call + "]") };
        await using var noTools = OpenAiTestData.Client(emptySession, unsolicited);
        Assert.AreEqual(LlmErrorCode.UnexpectedToolCall, (await noTools.GenerateAsync(OpenAiTestData.Request(), default)).ErrorCode);
    }

    [TestMethod]
    public async Task MODELAC018_InvalidUtf8UnpairedSurrogateAndDuplicateFieldsAreRejected()
    {
        foreach (var bytes in new[]
        {
            Encoding.UTF8.GetBytes(OpenAiTestData.TextResponse.Replace("answer", "\\ud800", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(OpenAiTestData.TextResponse.Replace("\"status\":\"completed\"", "\"status\":\"completed\",\"status\":\"completed\"", StringComparison.Ordinal)),
            new byte[] { 0xff, 0xfe, 0xff }
        })
        {
            using var session = OpenAiTestData.Session(); var handler = new OpenAiTestHandler
            { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }) };
            await using var client = OpenAiTestData.Client(session, handler);
            Assert.AreEqual(LlmErrorCode.InvalidResponse, (await client.GenerateAsync(OpenAiTestData.Request(), default)).ErrorCode);
        }
    }

    [TestMethod]
    public async Task MODELAC017_DeadlineCancellationAndDisposalNeverAdoptLateSuccess()
    {
        using var session = OpenAiTestData.Session(); var clock = new DeadlineClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new OpenAiTestHandler
        {
            Respond = async (_, token) =>
        {
            entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
        };
        await using var client = OpenAiTestData.Client(session, handler, clock);
        var run = client.GenerateAsync(OpenAiTestData.Request(), default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); clock.Fire();
        var timeout = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(TimeSpan.FromSeconds(120), clock.DueTime);
        Assert.AreEqual(LlmErrorCode.Timeout, timeout.ErrorCode); Assert.IsTrue(timeout.RequestDispatched);
        Assert.AreEqual(1, handler.Bodies.Count);
        var before = await client.GenerateAsync(OpenAiTestData.Request(), new CancellationToken(true));
        Assert.AreEqual(LlmStatus.Cancelled, before.Status); Assert.IsFalse(before.RequestDispatched);
        await client.DisposeAsync(); await client.DisposeAsync();
        Assert.AreEqual(LlmErrorCode.ClientUnavailable, (await client.GenerateAsync(OpenAiTestData.Request(), default)).ErrorCode);
    }

    [TestMethod]
    public async Task MODELAC026_ContextLengthWhitelistAndOversizedErrorKeepStatusClassification()
    {
        using var session = OpenAiTestData.Session();
        var handler = new OpenAiTestHandler { Status = HttpStatusCode.BadRequest, Response = "{\"error\":{\"code\":\"context_length_exceeded\"}}" };
        await using var client = OpenAiTestData.Client(session, handler);
        Assert.AreEqual(LlmErrorCode.InputTooLarge, (await client.GenerateAsync(OpenAiTestData.Request(), default)).ErrorCode);
        handler.Status = HttpStatusCode.Unauthorized; handler.Response = new string('x', 65537);
        Assert.AreEqual(LlmErrorCode.AuthenticationFailed, (await client.GenerateAsync(OpenAiTestData.Request(), default)).ErrorCode);
        Assert.AreEqual(2, handler.Bodies.Count);
    }
}