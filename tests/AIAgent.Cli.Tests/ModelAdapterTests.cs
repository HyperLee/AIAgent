using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ModelAdapterTests
{
    [TestMethod]
    public async Task MODELAC013_BothGeminiProfilesUseNativeSdkAndSameOwnerOnly()
    {
        foreach (var model in new[] { "gemini-3.5-flash-lite", "gemini-3.6-flash" })
        {
            using var session = new BootstrapSession(new("/unused-workspace"), new(model), new SessionSecrets(GeminiFixtures.Key.ToCharArray()));
            var observed = false;
            var handler = new GeminiHandler
            {
                Respond = (request, _) =>
            {
                observed = request.RequestUri!.AbsoluteUri == "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent";
                return Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response("[{\"text\":\"answer\",\"thoughtSignature\":\"c2ln\"}]")));
            }
            };
            await using var client = new GeminiClient(session, handler);
            var first = await client.GenerateAsync(GeminiFixtures.Request(), default);
            Assert.AreEqual(LlmStatus.Completed, first.Status); Assert.IsTrue(observed);
            var request = new LlmRequest("synthetic instruction", [LlmMessage.FromUserText("one"), first.ModelMessage!, LlmMessage.FromUserText("two")], []);
            Assert.AreEqual(LlmStatus.Completed, (await client.GenerateAsync(request, default)).Status);
            var inspector = client.CreateContextInspector(); var count = handler.Calls;
            Assert.AreEqual(ContextMeasureStatus.Measured, (await ((ILlmRequestCapacityInspector)inspector).MeasureActualRequestAsync(request, default)).Status);
            Assert.AreEqual(count, handler.Calls);
            var otherHandler = new GeminiHandler(); await using var other = new GeminiClient(session, otherHandler);
            Assert.AreEqual(LlmErrorCode.InvalidRequest, (await other.GenerateAsync(request, default)).ErrorCode);
            Assert.AreEqual(0, otherHandler.Calls);
        }
    }

    [TestMethod]
    public async Task MODELAC019_SplitCredentialShapeRejectsWholeGeminiCandidateAndTools()
    {
        foreach (var model in new[] { "gemini-3.5-flash-lite", "gemini-3.6-flash" })
        {
            using var session = new BootstrapSession(new("/unused-workspace"), new(model), new SessionSecrets(GeminiFixtures.Key.ToCharArray()));
            var first = "s" + "k-synthetic_";
            var second = "credential_fragment";
            var parts = "[{\"text\":\"" + first + "\"},{\"text\":\"" + second + "\"},{\"functionCall\":{\"name\":\"sample\",\"args\":{}}}]";
            var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(parts, usage: "{\"totalTokenCount\":17}"))) };
            await using var client = new GeminiClient(session, handler);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: [GeminiFixtures.Tool()]), default);
            Assert.AreEqual(LlmErrorCode.SensitiveOutput, result.ErrorCode);
            Assert.AreEqual(LlmStatus.Failed, result.Status);
            Assert.AreEqual(0, result.ToolCalls.Count);
            Assert.IsNull(result.ModelMessage);
            Assert.IsTrue(result.RequiresContextReset);
            Assert.IsTrue(result.RequestDispatched);
            Assert.AreEqual(17L, result.Usage?.TotalTokens);
            Assert.AreEqual(1, handler.Calls);
            Assert.IsFalse(result.SafeText.Contains(first + second, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task MODELAC009_RevokedModelSessionNeverDispatchesOldClient()
    {
        using var session = GeminiFixtures.Session();
        var models = new ModelSessionCoordinator(new(), ModelCatalog.Find(session.Settings)!);
        session.ModelSession = models.Capture();
        var handler = new GeminiHandler(); await using var client = new GeminiClient(session, handler);
        models.BeginSwitch(); models.Publish(ModelCatalog.Find("gemini", "gemini-3.6-flash")!);
        var result = await client.GenerateAsync(GeminiFixtures.Request(), default);
        Assert.AreEqual(LlmErrorCode.ClientUnavailable, result.ErrorCode);
        Assert.IsFalse(result.RequestDispatched); Assert.AreEqual(0, handler.Calls);
    }
}