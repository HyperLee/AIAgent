using System.Text;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ConfigurationTests
{
    [TestMethod]
    public void AC002_HelpAndWorkspaceParse()
    {
        Assert.IsTrue(ArgumentParser.Parse(["--help"], "/startup").Help, "AC-002");
        Assert.IsNull(ArgumentParser.Parse([], "/startup").Options.WorkspaceArgument, "AC-003");
        Assert.IsTrue(ArgumentParser.Parse(["--workspace", "a b"], "/startup").Options == new BootstrapOptions("/startup", "a b"), "AC-003");
    }

    [TestMethod]
    public void AC002_InvalidArgumentsAreControlled()
    {
        string[][] cases = [["--help", "--workspace", "x"], ["--help", "--help"], ["--HELP"], ["-h"], ["--workspace=x"], ["--model", "x"], ["--key", "FAKE_INPUT_ONLY"], ["--workspace"], ["--workspace", " "], ["--workspace", "--x"], ["--workspace", "x", "--workspace", "y"], ["x"], ["--workspace", "x", "y"]];
        foreach (var args in cases)
        {
            try { ArgumentParser.Parse(args, "/startup"); Assert.Fail("AC-002"); }
            catch (BootstrapFailureException ex) { Assert.IsTrue(ex.Code == BootstrapErrorCode.InvalidArguments, "AC-002"); }
        }
    }

    [TestMethod]
    public void AC004_StrictJsonAndApplicationLocation()
    {
        foreach (var model in new[] { "\"gemini-3.5-flash-lite\"", "\" gemini-3.6-flash \"" })
        {
            var opened = "";
            var loader = new SettingsLoader("/application", path => { opened = path; return Json("{\"Gemini\":{\"Model\":" + model + "}}"); });
            var settings = loader.Load();
            Assert.IsTrue(opened == "/application/appsettings.json", "AC-004");
            Assert.IsTrue(settings.Model == model.Trim('"', ' '), "AC-004");
        }
        string[] invalid = ["", "[]", "{}", "{\"gemini\":{\"Model\":null}}", "{\"Gemini\":null}", "{\"Gemini\":{}}", "{\"Gemini\":{\"Model\":12}}", "{\"Gemini\":{\"Model\":\"\"}}", "{\"Gemini\":{\"Model\":\" \"}}", "{\"Gemini\":{\"Model\":\"x\\ny\"}}", "{\"Gemini\":{\"Model\":null,\"Model\":null}}", "{\"Gemini\":{\"Model\":null},\"Gemini\":{\"Model\":null}}", "{\"Gemini\":{\"Model\":null,\"ApiKey\":\"FAKE_CONFIG_ONLY\"}}", "{\"Gemini\":{\"Model\":null},\"extra\":0}", "{\"Gemini\":{\"Model\":null,}}", "{/*comment*/\"Gemini\":{\"Model\":null}}"];
        foreach (var json in invalid) ExpectSettingsFailure(() => new SettingsLoader("/application", _ => Json(json)).Load());
    }

    [TestMethod]
    public async Task GEMAC002_InvalidModelStopsBeforeWorkspaceAndKey()
    {
        string?[] values = [null, "", " ", "bad/model", "models/name", "https://host", "name?x", "name#x", "-name", "UPPER", "中文", "x\ny", "x\ty", new string('a', 129)];
        foreach (var value in values)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { Gemini = new { Model = value } });
            var loader = new SettingsLoader("/application", _ => Json(json));
            var fake = new BootstrapFake();
            using var result = await new Bootstrapper(loader, fake, fake, fake, () => "/startup").StartAsync([], default);
            Assert.IsTrue(result.ErrorCode == BootstrapErrorCode.InvalidSettings && result.ExitCode == 2 && fake.Calls.Count == 0, "GEM-AC-002");
        }
        foreach (var value in new[] { " gemini-3.6-flash ", "gemini-3.5-flash-lite" })
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { Gemini = new { Model = value } });
            Assert.IsTrue(new SettingsLoader("/application", _ => Json(json)).Load().Model == value.Trim(), "GEM-AC-002");
        }
    }

    [TestMethod]
    public void AC004_ReadFailuresDoNotExposeExceptions()
    {
        foreach (var exception in new Exception[] { new FileNotFoundException("FAKE_ERROR_ONLY"), new UnauthorizedAccessException("FAKE_ERROR_ONLY"), new IOException("FAKE_ERROR_ONLY") })
            ExpectSettingsFailure(() => new SettingsLoader("/application", _ => throw exception).Load());
    }

    [TestMethod]
    public void AC004_UnpairedUnicodeSurrogatesAreSettingsErrors()
    {
        foreach (var value in new[] { "\\ud800", "\\udc00", "\\ud800x" })
            ExpectSettingsFailure(() => new SettingsLoader("/application", _ => Json("{\"Gemini\":{\"Model\":\"" + value + "\"}}")).Load());
    }

    private static MemoryStream Json(string json) => new(Encoding.UTF8.GetBytes(json));
    private static void ExpectSettingsFailure(Action action)
    {
        try { action(); Assert.Fail("AC-004"); }
        catch (BootstrapFailureException ex) { Assert.IsTrue(ex.Code == BootstrapErrorCode.InvalidSettings && !ex.ToString().Contains("FAKE_", StringComparison.Ordinal), "AC-004"); }
    }
}