using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ChatSdkTests
{
    [TestMethod]
    public async Task CLIAC011_RealSdkCarriesOpaquePartsAndSignaturesThroughChat()
    {
        var interaction = Inputs("one", "two", "/exit");
        var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response("[{\"text\":\"private thought\",\"thought\":true},{\"text\":\"visible\",\"thoughtSignature\":\"c2lnbmF0dXJl\"}]"))) };
        var app = new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "test instruction");
        Assert.AreEqual(0, await app.RunAsync(GeminiFixtures.Session(), default));
        Assert.AreEqual(2, handler.Calls);
        using var body = JsonDocument.Parse(handler.Bodies[1]);
        var contents = body.RootElement.GetProperty("contents");
        Assert.AreEqual(3, contents.GetArrayLength());
        Assert.AreEqual("private thought", contents[1].GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.AreEqual("c2lnbmF0dXJl", contents[1].GetProperty("parts")[1].GetProperty("thoughtSignature").GetString());
        Assert.IsFalse(interaction.Events.Any(e => e.Text?.Contains("private thought", StringComparison.Ordinal) == true || e.Text?.Contains("c2lnbmF0dXJl", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task CLIAC011_SensitiveSdkResponseClearsAllOldHistoryBeforeNextRequest()
    {
        var interaction = Inputs("one", "sensitive", "next", "/exit");
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 2
            ? GeminiFixtures.Response("[{\"text\":\"FAKE_KEY_\"},{\"text\":\"FOR_TESTS_ONLY\"}]", usage: "{\"totalTokenCount\":17}") : GeminiFixtures.Text()));
        var usage = new UsageAccumulator();
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), usage, "test instruction").RunAsync(GeminiFixtures.Session(), default));
        Assert.AreEqual(3, handler.Calls);
        using var body = JsonDocument.Parse(handler.Bodies[2]);
        Assert.AreEqual(1, body.RootElement.GetProperty("contents").GetArrayLength());
        Assert.AreEqual(17L, usage.Snapshot().Total.KnownTotal);
        Assert.AreEqual(2L, usage.Snapshot().Total.MissingRequestCount);
        Assert.IsFalse(interaction.Events.Any(e => e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task CLIAC013_ChatUsesActualBodyBoundaryWithoutTrimmingOrRetrying()
    {
        async Task<(GeminiHandler Handler, ScriptInteraction Interaction, int Turns)> Run(string history)
        {
            var interaction = Inputs("one", "two", "/exit");
            var handler = new GeminiHandler();
            handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Text(handler.Calls == 1 ? history : "answer")));
            var context = new ConversationContext();
            var turns = 0;
            interaction.OnShow = e => { if (e.Kind == ChatEventKind.Usage) turns = context.CompletedTurnCount; };
            Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), context, new(), "instruction").RunAsync(GeminiFixtures.Session(), default));
            return (handler, interaction, turns);
        }
        var calibration = await Run("history");
        var padding = 262144 - calibration.Handler.Bodies[1].Length + 7;
        var allowed = await Run(new string('x', padding));
        Assert.AreEqual(2, allowed.Handler.Calls);
        Assert.AreEqual(262144, allowed.Handler.Bodies[1].Length);
        Assert.AreEqual(2, allowed.Turns);
        var rejected = await Run(new string('x', padding + 1));
        Assert.AreEqual(1, rejected.Handler.Calls);
        Assert.AreEqual(1, rejected.Turns);
        Assert.IsTrue(rejected.Interaction.Events.Any(e => e.ErrorCode == LlmErrorCode.InputTooLarge));
        Assert.IsFalse(rejected.Interaction.Events.Any(e => e.Kind == ChatEventKind.ContextCleared));
    }

    [TestMethod]
    public async Task CLIAC021_RealSdkAuthErrorIsHandledBeforePolicySwitchAndNewKeyOutputBlocked()
    {
        var interaction = Inputs("old question", "new question", "/exit");
        interaction.Choices.Enqueue(KeyReplacementChoice.Reenter);
        interaction.Keys.Enqueue(() => KeyInputResult.Accept("FAKE_NEW_KEY_ONLY"));
        var old = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json("{\"error\":{\"message\":\"" + GeminiFixtures.Key + "\"}}", System.Net.HttpStatusCode.Unauthorized)) };
        var replacement = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Text("prefixFAKE_NEW_KEY_ONLYsuffix"))) };
        var factory = new SdkChatFactory(old, replacement);
        Assert.AreEqual(0, await new ChatApplication(interaction, factory, new(), new(), "instruction").RunAsync(GeminiFixtures.Session(), default));
        Assert.AreEqual(1, old.Calls);
        Assert.AreEqual(1, replacement.Calls);
        using var body = JsonDocument.Parse(replacement.Bodies[0]);
        Assert.AreEqual(1, body.RootElement.GetProperty("contents").GetArrayLength());
        Assert.IsFalse(interaction.Events.Any(e => e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true || e.Text?.Contains("FAKE_NEW_KEY_ONLY", StringComparison.Ordinal) == true));
        Assert.IsTrue(interaction.Events.Any(e => e.ErrorCode == LlmErrorCode.AuthenticationFailed));
        Assert.IsTrue(interaction.Events.Any(e => e.ErrorCode == LlmErrorCode.SensitiveOutput));
    }

    private static ScriptInteraction Inputs(params string[] texts)
    {
        var interaction = new ScriptInteraction();
        foreach (var text in texts) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        return interaction;
    }
}

internal sealed class SdkChatFactory(params GeminiHandler[] handlers) : IChatRuntimeFactory
{
    private int _index;
    public BootstrapSession CreateReplacementSession(ConfirmedWorkspace workspace, PublicSettings settings, KeyInputResult acceptedInput) => new ChatRuntimeFactory().CreateReplacementSession(workspace, settings, acceptedInput);
    public ILlmClient CreateClient(BootstrapSession session) => new GeminiClient(session, handlers[_index++]);
}