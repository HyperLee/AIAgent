using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkspacePathTests
{
    [TestMethod]
    public void WSAC003_PathGrammarRejectsUnsafeAndMalformedInputs()
    {
        foreach (var path in new string?[] { null, "", "/a", "//server/a", "C:a", "C:/a", "a\\b", "a\0b", "a\u007fb", "a\u0080b", "a\u2028b", "a\u2029b", "a\ud800b", "a\udc00b", "a/../b", "..", new('a', 4097) })
            Assert.IsFalse(WorkspacePathRules.TryNormalize(path, out _, out _), "WS-AC-003");
        Assert.IsTrue(WorkspacePathRules.TryNormalize(new('a', 4096), out _, out _), "WS-AC-003");
    }

    [TestMethod]
    public void WSAC004_NormalizationPreservesLiteralNamesAndDirectoryIntent()
    {
        foreach (var pair in new[] { (".", "."), ("docs//./notes.md", "docs/notes.md"), (" notes.md ", " notes.md "), ("   ", "   "), ("中文/😀", "中文/😀"), ("~/$HOME/%HOME%", "~/$HOME/%HOME%") })
        {
            Assert.IsTrue(WorkspacePathRules.TryNormalize(pair.Item1, out var normalized, out _), "WS-AC-004");
            Assert.IsTrue(normalized == pair.Item2, "WS-AC-004");
        }
        Assert.IsTrue(WorkspacePathRules.TryNormalize("a/", out var path, out var directory) && path == "a" && directory, "WS-AC-004");
    }

    [TestMethod]
    public void WSAC005_BoundaryRequiresAnEntireRootSegment()
    {
        Assert.IsTrue(WorkspacePathRules.IsWithinRoot("/work", "/work/a"), "WS-AC-005");
        Assert.IsFalse(WorkspacePathRules.IsWithinRoot("/work", "/work-other/a"), "WS-AC-005");
        Assert.IsFalse(WorkspacePathRules.IsWithinRoot("/work", "/outside/a"), "WS-AC-005");
    }

    [TestMethod]
    public void WSAC008_SensitiveNamesApplyToEverySegmentWithoutSubstringGuessing()
    {
        var names = new[] { ".git", ".agents", ".codex", ".aws", ".ssh", ".secrets", "secrets", ".env", ".env.example", "gemini-api-key.txt", "api-key.txt", "appsettings.Local.json", "credentials", "credentials.json", "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519", "identity", "a.secrets.json", "a.pem", "a.key", "a.p12", "a.pfx", "a.ppk", "a.keystore", "a.jks" };
        foreach (var name in names)
        {
            Assert.IsTrue(WorkspacePathRules.IsSensitive(name), "WS-AC-008");
            Assert.IsTrue(WorkspacePathRules.IsSensitive("nested/" + name.ToUpperInvariant() + "/a"), "WS-AC-008");
        }
        foreach (var name in new[] { "mysecrets", "secrets-notes.md", ".gitignore", "appsettings.json", "bin/a", "obj/a", "logs/a" })
            Assert.IsFalse(WorkspacePathRules.IsSensitive(name), "WS-AC-008");
    }
}