using System.Globalization;
using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DiscoverySafetyTests
{
    [TestMethod]
    public async Task SEARCHAC005_AllRestrictedKindsAndSensitiveNamesAreHiddenWithoutOpening()
    {
        await using var f = new DiscoveryFixture();
        foreach (var name in new[] { ".git", ".agents", ".codex", ".aws", ".ssh", ".secrets", "secrets", ".env.example", "a.key", "a.secrets.json" })
        { f.Fs.Directory(name); f.Fs.File(name + "/inside", [65]); }
        f.Fs.Nodes["/ws/link"] = new(new(WorkspaceNodeKind.Link)); f.Fs.Nodes["/ws/fifo"] = new(new(WorkspaceNodeKind.Other));
        f.Fs.File("ordinary", [65]);
        var result = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.IsTrue(result.Data?.Matches.Count == 1 && result.Data.Matches[0].Path == "ordinary" && !result.Data.Truncated &&
            f.Fs.Opened == 1 && f.Fs.Enumerated.Count == 1, "SEARCH-AC-005");
        Assert.IsTrue((await f.Files.SearchFilesAsync("link", "*", default)).ErrorCode == WorkspaceErrorCode.SymbolicLink &&
            (await f.Files.SearchFilesAsync("ordinary", "*", default)).ErrorCode == WorkspaceErrorCode.WrongEntryType, "SEARCH-AC-005-start");
    }

    [TestMethod]
    public async Task SEARCHAC008_EveryIgnoredNameIsCaseInsensitiveAndOnlyHidesDirectories()
    {
        foreach (var name in new[] { "bin", "obj", "node_modules", ".vs", ".idea", "dist", "coverage", "TestResults", "logs", ".cache" })
        {
            await using var f = new DiscoveryFixture(); var upper = name.ToUpperInvariant(); f.Fs.Directory(upper); f.Fs.File(upper + "/a", [65]);
            Assert.IsTrue((await f.Files.SearchFilesAsync(".", "*", default)).Data?.Matches.Count == 0 && f.Fs.Enumerated.Count == 1, "SEARCH-AC-008-ignore");
            Assert.IsTrue((await f.Files.SearchFilesAsync(upper, "*", default)).Data?.Matches.Count == 1, "SEARCH-AC-008-explicit");
        }
    }

    [TestMethod]
    public async Task SEARCHAC007_ComparisonIsCultureIndependentAndLimitsUseUtf16()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "tr-TR", "zh-TW" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                await using var f = new DiscoveryFixture(); f.Fs.File("I.cs", Encoding.UTF8.GetBytes("I")); f.Fs.File("i.cs", [65]);
                Assert.IsTrue((await f.Files.SearchFilesAsync(".", "I.cs", default)).Data?.Matches.Count == 1 &&
                    (await f.Files.SearchTextAsync(".", "i", "*", false, 0, default)).Data?.Matches.Count == 1, "SEARCH-AC-007-culture");
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
        await using var limit = new DiscoveryFixture(); var path = new string('a', 4094) + "😀"; limit.Fs.File(path, [65]);
        Assert.IsTrue((await limit.Files.SearchFilesAsync(".", new string('*', 1), default)).Data?.Matches.Count == 1, "SEARCH-AC-004-path4096");
        var pattern = new string('a', 1022) + "😀"; limit.Fs.File(pattern, [65]);
        Assert.IsTrue((await limit.Files.SearchFilesAsync(".", pattern, default)).Data?.Matches.Count == 1, "SEARCH-AC-006-pattern1024");
        var query = new string('x', 1022) + "😀"; limit.Fs.File("q", Encoding.UTF8.GetBytes(query));
        Assert.IsTrue((await limit.Files.SearchTextAsync(".", query, "q", true, 0, default)).Data?.Matches.Count == 1, "SEARCH-AC-016-query1024");
    }

    [TestMethod]
    public async Task SEARCHAC018_SecretNamesAreCheckedBeforeIgnoredOrSensitiveFiltering()
    {
        await using var f = new DiscoveryFixture(); f.Fs.Directory(".env." + GeminiFixtures.Key);
        var result = await f.Files.SearchFilesAsync(".", "*", default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.SensitiveContent && result.Data is null && f.Fs.Opened == 0, "SEARCH-AC-018-name");
    }

    [TestMethod]
    public async Task SEARCHAC022_ModeChangesOnUnsupportedCandidatesStillRejectTheSearch()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new DiscoveryModeFileSystem(); fs.Inner.File("a", [255]); fs.Inner.File("b", [65]);
        fs.Inner.OnOpen = path => { if (path == "/ws/b") fs.Inner.Nodes["/ws/a"].Mode = UnixFileMode.UserRead; };
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var result = await files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.PathChanged && result.Data is null, "SEARCH-AC-022-mode");
    }
}

internal sealed class DiscoveryModeFileSystem : IWorkspaceWriteFileSystem
{
    internal ControlledWorkspaceFileSystem Inner { get; } = new();
    public WorkspaceNodeState Inspect(string path) => Inner.Inspect(path);
    public IEnumerable<string> EnumerateNames(string path) => Inner.EnumerateNames(path);
    public IWorkspaceReadFile OpenRead(string path) => Inner.OpenRead(path);
    public UnixFileMode GetMode(string path) => Inner.Nodes[path].Mode;
    public IWorkspaceStagedFile CreateStagedFile(string path) => throw new InvalidOperationException("FAKE_ONLY");
    public void PublishNew(string staged, string target) => throw new InvalidOperationException("FAKE_ONLY");
    public void PublishReplacement(string staged, string target) => throw new InvalidOperationException("FAKE_ONLY");
    public void DeleteOwnedStagedFile(string path) => throw new InvalidOperationException("FAKE_ONLY");
}