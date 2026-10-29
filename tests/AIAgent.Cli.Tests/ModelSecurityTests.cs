using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ModelSecurityTests
{
    [TestMethod]
    public async Task MODELAC019_CoreRejectsCredentialShapeBeforePublishingAnswer()
    {
        var fixture = new ChatFixture("question", "/exit");
        fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed("s" + "k-" + new string('a', 16)));
        Assert.AreEqual(0, await fixture.Run());
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.ContextCleared));
    }
    [TestMethod]
    public void MODELAC020_OpenAiShapeIsRejectedAcrossActiveAndPersistencePolicies()
    {
        using var session = GeminiFixtures.Session();
        var shape = "s" + "k-" + new string('a', 16);
        foreach (var boundary in Enum.GetValues<ContentBoundary>())
            Assert.IsFalse(session.Policy.CanSendOrUse("prefix" + shape + "suffix", boundary));
        var persistence = new WorkPersistencePolicy(session.Policy);
        foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode })
            foreach (var offset in new[] { 0, 1 })
                Assert.IsFalse(persistence.IsSafeRaw(new byte[offset].Concat(encoding.GetBytes(shape)).ToArray()));
        var escaped = "{\"text\":\"\\u0073\\u006b\\u002d" + new string('a', 16) + "\"}";
        Assert.IsFalse(persistence.IsSafe(Encoding.UTF8.GetBytes(escaped)));
        Assert.IsTrue(session.Policy.CanSendOrUse("s" + "k-" + new string('a', 15), ContentBoundary.ModelInput));
        Assert.IsTrue(session.Policy.CanSendOrUse("S" + "K-" + new string('a', 16), ContentBoundary.ModelInput));
        Assert.IsTrue(session.Policy.CanSendOrUse("s" + "k-" + new string('a', 15) + "中", ContentBoundary.ModelInput));
    }

    [TestMethod]
    public void MODELAC020_OpenAiPlaceholderIsAnExactPersistenceException()
    {
        using var session = GeminiFixtures.Session(); var persistence = new WorkPersistencePolicy(session.Policy);
        Assert.IsTrue(persistence.IsSafe("ApiKey=YOUR_OPENAI_API_KEY"));
        Assert.IsFalse(persistence.IsSafe("ApiKey=your_openai_api_key"));
    }
}