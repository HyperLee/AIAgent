using System.Text;

using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ModelSettingsTests
{
    [TestMethod]
    public void MODELAC002_NewSettingsAndLegacyAcceptOnlyRegisteredGeminiModels()
    {
        foreach (var model in new[] { "gemini-3.5-flash-lite", "gemini-3.6-flash" })
        {
            Assert.AreEqual(model, Load("{\"Llm\":{\"Model\":\" " + model + " \",\"Provider\":\" gemini \"}}").Model);
            Assert.AreEqual(model, Load("{\"Gemini\":{\"Model\":\"" + model + "\"}}").Model);
        }
        foreach (var json in new[]
        {
            "{\"Gemini\":{\"Model\":\"model-name\"}}",
            "{\"Llm\":{\"Provider\":\"Gemini\",\"Model\":\"gemini-3.6-flash\"}}",
            "{\"Llm\":{\"Provider\":\"gemini\",\"Model\":\"gemini-3.6-flash\",\"Model\":\"gemini-3.6-flash\"}}",
            "{\"Llm\":{\"Provider\":\"gemini\",\"Model\":\"gemini-3.6-flash\"},\"Gemini\":{\"Model\":\"gemini-3.6-flash\"}}",
            "{\"Llm\":{\"Provider\":null,\"Model\":\"gemini-3.6-flash\"}}"
        }) Assert.ThrowsExactly<BootstrapFailureException>(() => Load(json));
        var openai = Load("{\"Llm\":{\"Provider\":\"openai\",\"Model\":\"gpt-6.1-terra\"}}");
        Assert.AreEqual("openai", openai.Provider);
        Assert.AreEqual("gpt-6.1-terra", openai.Model);
    }

    [TestMethod]
    public async Task MODELAC002_UnknownLegacyModelStopsBeforeAnyInteraction()
    {
        var fake = new BootstrapFake();
        var loader = new SettingsLoader("/application", _ => new MemoryStream(Encoding.UTF8.GetBytes("{\"Gemini\":{\"Model\":\"model-name\"}}")));
        using var result = await new Bootstrapper(loader, fake, fake, fake, () => "/startup").StartAsync([], default);
        Assert.AreEqual(2, result.ExitCode);
        Assert.AreEqual(BootstrapErrorCode.InvalidSettings, result.ErrorCode);
        Assert.AreEqual(0, fake.Calls.Count);
    }

    private static PublicSettings Load(string json) => new SettingsLoader("/application", _ => new MemoryStream(Encoding.UTF8.GetBytes(json))).Load();
}