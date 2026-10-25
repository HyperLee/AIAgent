using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ContextSdkTests
{
    [DataRow("/absolute")]
    [DataRow("../a")]
    [DataRow(".env")]
    [DataRow("a//b")]
    [TestMethod]
    public void CTXAC013_ProjectionRejectsNoncanonicalOrSensitiveMetadataPaths(string path)
    {
        var response = JsonSerializer.SerializeToElement(new { status = "success", error_code = "none", data = new { path } });
        Assert.ThrowsExactly<ContextInspectionException>(() => GeminiContextTools.Project(new(0, null, "read_file", response)));
    }
    [TestMethod]
    public async Task CTXAC011_RealCompactionRetainsMultiCallNativeTurnsAndSignatures()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(Path.Combine(root, "a"), "RAW_A"); await File.WriteAllTextAsync(Path.Combine(root, "b"), "RAW_B");
        var interaction = new ScriptInteraction();
        for (var i = 0; i < 6; i++) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("question"));
        var handler = new GeminiHandler(); var compacted = false;
        handler.Respond = async (request, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsByteArrayAsync(token));
            var contents = body.RootElement.GetProperty("contents");
            if (!body.RootElement.TryGetProperty("tools", out _))
            {
                compacted = true; Assert.AreEqual(1, contents.GetArrayLength());
                var source = contents[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
                using var projected = JsonDocument.Parse(source); Assert.AreEqual(3, projected.RootElement.GetProperty("turns").GetArrayLength());
                Assert.IsFalse(source.Contains("RAW_A", StringComparison.Ordinal)); Assert.IsFalse(source.Contains("RAW_B", StringComparison.Ordinal));
                Assert.IsFalse(source.Contains("THOUGHT", StringComparison.Ordinal)); Assert.IsFalse(source.Contains("thoughtSignature", StringComparison.Ordinal));
                Assert.AreEqual(2, projected.RootElement.GetProperty("turns")[0].GetProperty("tools").GetArrayLength());
                return GeminiHandler.Json(GeminiFixtures.Text(ContextTestData.Summary));
            }
            if (compacted)
            {
                Assert.AreEqual("c2lnLWE=", contents[1].GetProperty("parts")[1].GetProperty("thoughtSignature").GetString());
                Assert.AreEqual("c2lnLWI=", contents[1].GetProperty("parts")[2].GetProperty("thoughtSignature").GetString());
                var responses = contents[2].GetProperty("parts"); Assert.AreEqual(2, responses.GetArrayLength());
                Assert.AreEqual("call-a", responses[0].GetProperty("functionResponse").GetProperty("id").GetString());
                Assert.AreEqual("call-b", responses[1].GetProperty("functionResponse").GetProperty("id").GetString());
            }
            var parts = contents[contents.GetArrayLength() - 1].GetProperty("parts");
            if (parts[0].TryGetProperty("functionResponse", out _)) return GeminiHandler.Json(GeminiFixtures.Text("final"));
            return GeminiHandler.Json(GeminiFixtures.Response("[{\"thought\":true,\"text\":\"THOUGHT\"},{\"functionCall\":{\"name\":\"read_file\",\"id\":\"call-a\",\"args\":{\"path\":\"a\"}},\"thoughtSignature\":\"c2lnLWE=\"},{\"functionCall\":{\"name\":\"read_file\",\"id\":\"call-b\",\"args\":{\"path\":\"b\"}},\"thoughtSignature\":\"c2lnLWI=\"}]"));
        };
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "test", new Workspace.WorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default));
        Assert.IsTrue(compacted); Assert.AreEqual(13, handler.Calls);
    }

    [TestMethod]
    public async Task CTXAC020_ActualBodyBoundaryReportsUndispatchedOversize()
    {
        async Task<(int Calls, long Bytes, bool Dispatched, LlmResult Result, long? Estimate)> Run(int padding)
        {
            using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
            await using var client = new GeminiClient(session, handler);
            var inspector = client.CreateContextInspector(); long bytes = 0; var dispatched = false;
            using var report = ((ILlmContextTelemetry)inspector).ObserveRequest((size, sent) => { bytes = size; dispatched = sent; });
            var memory = System.Text.Encoding.UTF8.GetString(ContextDocuments.Memory(new(1, 1, ["goal"], [], [], []), [new("n1", "原文")]));
            var request = new LlmRequest(new string('x', padding) + memory, [LlmMessage.FromUserText("question")], [GeminiFixtures.Tool()]);
            var estimate = inspector.Measure(request);
            var result = await client.GenerateAsync(request, default);
            return (handler.Calls, bytes, dispatched, result, estimate.EstimatedJsonBytes);
        }
        var calibration = await Run(1); var padding = checked(262144 - (int)calibration.Bytes + 1);
        var accepted = await Run(padding); var rejected = await Run(padding + 1);
        Assert.AreEqual(262144L, accepted.Bytes); Assert.AreEqual(1, accepted.Calls); Assert.IsTrue(accepted.Dispatched);
        Assert.AreEqual(262145L, rejected.Bytes); Assert.AreEqual(0, rejected.Calls); Assert.IsFalse(rejected.Dispatched);
        Assert.AreEqual(LlmErrorCode.InputTooLarge, rejected.Result.ErrorCode); Assert.IsFalse(rejected.Result.RequestDispatched);
        Assert.IsTrue(accepted.Estimate > 0); Assert.IsNull(rejected.Result.Usage);
    }

    [TestMethod]
    public async Task CTXAC011_ProjectionKeepsOnlyMetadataAndRetainedProtocolStillResends()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1 ? BudgetFixture.Calls("read_file", new { path = "a" }) : GeminiFixtures.Text("final")));
        await using var client = new GeminiClient(session, handler);
        var user = LlmMessage.FromUserText("question"); var tools = new[] { GeminiFixtures.Tool("read_file") };
        var calls = await client.GenerateAsync(new("test", [user], tools), default);
        var call = calls.ToolCalls.Single();
        var response = LlmMessage.FromToolResults([new(call.PartIndex, call.CallId, call.Name,
            GeminiFixtures.Element("{\"status\":\"success\",\"error_code\":\"none\",\"data\":{\"path\":\"a\",\"content\":\"RAW_FILE_TEXT\",\"byte_count\":13}}"))]);
        var answer = await client.GenerateAsync(new("test", [user, calls.ModelMessage!, response], tools), default);
        var turn = new[] { user, calls.ModelMessage!, response, answer.ModelMessage! };
        var projected = client.CreateContextInspector().ProjectCompletedTurn(1, turn);
        Assert.AreEqual("final", projected.AssistantText); Assert.AreEqual(13, projected.Tools.Single().GetProperty("byte_count").GetInt32());
        var source = System.Text.Encoding.UTF8.GetString(ContextDocuments.Source(null, [projected]));
        Assert.IsFalse(source.Contains("RAW_FILE_TEXT", StringComparison.Ordinal)); Assert.IsFalse(source.Contains("c2ln", StringComparison.Ordinal));
        await client.GenerateAsync(new("test", turn.Append(LlmMessage.FromUserText("next")).ToArray(), tools), default);
        using var body = JsonDocument.Parse(handler.Bodies[^1]);
        Assert.AreEqual("c2ln", body.RootElement.GetProperty("contents")[1].GetProperty("parts")[1].GetProperty("thoughtSignature").GetString());
        Assert.AreEqual("local-call", body.RootElement.GetProperty("contents")[2].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("id").GetString());
    }

    [TestMethod]
    public async Task CTXAC019_InspectorMeasuresLocallyAndRejectsForeignNativeOwnership()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        await using var client = new GeminiClient(session, handler);
        var inspector = client.CreateContextInspector();
        var measured = inspector.Measure(new("test", [LlmMessage.FromUserText("question")], []));
        Assert.AreEqual(ContextMeasureStatus.Measured, measured.Status);
        Assert.IsTrue(measured.EstimatedJsonBytes > 0); Assert.AreEqual(0, handler.Calls);
        var invalid = inspector.Measure(new("test", [LlmMessage.FromUserText("q"), LlmMessage.FromModel(new object()), LlmMessage.FromUserText("next")], []));
        Assert.AreEqual(ContextMeasureStatus.Unavailable, invalid.Status); Assert.IsTrue(invalid.RequiresContextReset);
        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    public async Task CTXAC013_ProjectionExcludesThoughtAndToolPayload()
    {
        using var session = GeminiFixtures.Session(); var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response("[{\"thought\":true,\"text\":\"private reasoning\"},{\"text\":\"visible\",\"thoughtSignature\":\"c2ln\"}]")));
        await using var client = new GeminiClient(session, handler);
        var user = LlmMessage.FromUserText("question");
        var result = await client.GenerateAsync(new("test", [user], []), default);
        var projected = client.CreateContextInspector().ProjectCompletedTurn(1, [user, result.ModelMessage!]);
        Assert.AreEqual("visible", projected.AssistantText); Assert.AreEqual("question", projected.UserText);
        Assert.AreEqual(0, projected.Tools.Count); Assert.AreEqual(1, handler.Calls);
        Assert.IsFalse(JsonSerializer.Serialize(projected).Contains("visible", StringComparison.Ordinal));
    }
}