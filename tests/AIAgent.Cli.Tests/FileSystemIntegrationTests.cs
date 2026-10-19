using System.Diagnostics;
using AIAgent.Cli.Cli;
using AIAgent.Cli.Configuration;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class FileSystemIntegrationTests
{
    // Intentionally retain isolated temp fixtures. No recursive deletion or bulk cleanup.
    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aiagent-bootstrap-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [TestMethod]
    public void AC005_RealDirectoryAncestorLinksBrokenLinksFilesAndCycles()
    {
        var temp = NewDirectory();
        Directory.CreateDirectory(Path.Combine(temp, "real", "nested"));
        Directory.CreateDirectory(Path.Combine(temp, "real", "target"));
        Directory.CreateDirectory(Path.Combine(temp, "other", "target"));
        Directory.CreateDirectory(Path.Combine(temp, "other", "child"));
        Directory.CreateSymbolicLink(Path.Combine(temp, "real", "link"), "../other/child");
        Directory.CreateSymbolicLink(Path.Combine(temp, "complex"), "real/link/../target");
        Directory.CreateSymbolicLink(Path.Combine(temp, "alias"), "real");
        Directory.CreateSymbolicLink(Path.Combine(temp, "final"), "alias/nested");
        Directory.CreateSymbolicLink(Path.Combine(temp, "broken"), "missing");
        Directory.CreateSymbolicLink(Path.Combine(temp, "cycle1"), "cycle2");
        Directory.CreateSymbolicLink(Path.Combine(temp, "cycle2"), "cycle1");
        File.WriteAllText(Path.Combine(temp, "file.txt"), "FAKE_FIXTURE_ONLY");
        var resolver = new WorkspaceResolver(new WorkspaceFileSystem());
        var expected = resolver.Resolve(new(temp, "real/nested"));
        foreach (var path in new[] { "alias/nested", "final" })
            Assert.IsTrue(resolver.Resolve(new(temp, path)) == expected, "AC-005");
        Assert.IsTrue(resolver.Resolve(new(temp, "complex")) == resolver.Resolve(new(temp, "other/target")), "AC-005");
        foreach (var path in new[] { "missing", "file.txt", "broken", "cycle1" })
        {
            try { resolver.Resolve(new(temp, path)); Assert.Fail("AC-005"); }
            catch (BootstrapFailureException ex) { Assert.IsTrue(ex.Code == BootstrapErrorCode.WorkspaceUnavailable, "AC-005"); }
        }
    }

    [TestMethod]
    public void AC004_WorkspaceSettingsNeverOverrideApplicationSettings()
    {
        var temp = NewDirectory();
        var app = Directory.CreateDirectory(Path.Combine(temp, "app")).FullName;
        var workspace = Directory.CreateDirectory(Path.Combine(temp, "workspace")).FullName;
        File.WriteAllText(Path.Combine(app, "appsettings.json"), "{\"Gemini\":{\"Model\":null}}");
        File.WriteAllText(Path.Combine(workspace, "appsettings.json"), "{\"ApiKey\":\"FAKE_CONFIG_ONLY\"}");
        var before = Directory.GetFiles(workspace);
        Assert.IsNull(new SettingsLoader(app).Load().Model, "AC-004");
        Assert.IsTrue(Directory.GetFiles(workspace).SequenceEqual(before), "AC-010");
        try { new SettingsLoader(Path.Combine(temp, "missing")).Load(); Assert.Fail("AC-004"); }
        catch (BootstrapFailureException ex) { Assert.IsTrue(ex.Code == BootstrapErrorCode.InvalidSettings, "AC-004"); }
    }

    [TestMethod]
    public void AC013_IgnoreRulesNestedExceptionsAndAlreadyTrackedFiles()
    {
        var temp = NewDirectory();
        Git(temp, "init", "--quiet");
        var names = new[] { ".env", ".env.local", "gemini-api-key.txt", "api-key.txt", "test.secrets.json", "appsettings.Local.json", ".secrets/value.txt", "secrets/value.txt", "logs/value.txt", "value.log", "value.dmp", "value.dump", "bin/value.txt", "obj/value.txt" };
        var tracked = Path.Combine(temp, "tracked.log");
        File.WriteAllText(tracked, "FAKE_FIXTURE_ONLY");
        Git(temp, "add", "tracked.log");
        var repoRoot = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        File.Copy(Path.Combine(repoRoot, ".gitignore"), Path.Combine(temp, ".gitignore"));
        foreach (var prefix in new[] { "", "nested/" })
        {
            foreach (var name in names)
            {
                var relative = prefix + name;
                var path = Path.Combine(temp, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "FAKE_FIXTURE_ONLY");
                Assert.IsTrue(Git(temp, "check-ignore", "-q", "--", relative).Exit == 0, "AC-013");
            }
            foreach (var name in new[] { ".env.example", "notes.md", "Program.cs" })
            {
                var relative = prefix + name;
                File.WriteAllText(Path.Combine(temp, relative), "PLACEHOLDER_ONLY");
                Assert.IsTrue(Git(temp, "check-ignore", "-q", "--", relative).Exit == 1, "AC-013");
            }
        }
        Assert.IsTrue(Git(temp, "ls-files", "--error-unmatch", "tracked.log").Exit == 0, "AC-013");
        Assert.IsTrue(Git(temp, "check-ignore", "-q", "tracked.log").Exit == 1, "AC-013");
    }

    private static (int Exit, string Output) Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
