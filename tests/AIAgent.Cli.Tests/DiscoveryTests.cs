using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DiscoveryTests
{
    [TestMethod]
    public async Task SEARCHAC007_GlobMatchesUnicodeScalarsAndZeroDirectories()
    {
        await using var f = new DiscoveryFixture();
        f.Fs.Directory("src"); f.Fs.Directory("src/Core");
        foreach (var name in new[] { "A.cs", "😀.cs", "e\u0301.cs", "[AB].cs", "Core/A.cs", "Core/B.txt" }) f.Fs.File("src/" + name, [65]);
        foreach (var (pattern, count) in new[] { ("*.cs", 5), ("**/*.cs", 5), ("Core/*.cs", 1), ("?.cs", 3), ("[AB].cs", 1), ("**", 6), ("*.CS", 0) })
        {
            var result = await f.Files.SearchFilesAsync("src", pattern, default);
            Assert.IsTrue(result.Data?.Matches.Count == count && !result.Data.Truncated && result.Data.SafetyFiltered, "SEARCH-AC-007");
        }
        Assert.IsTrue(f.Fs.Opened == 0 && f.Files.Snapshots.Count == 0, "SEARCH-AC-009-no-content");
    }

    [TestMethod]
    public async Task SEARCHAC006_InvalidPatternsNeverEnumerate()
    {
        await using var f = new DiscoveryFixture();
        foreach (var pattern in new[] { "", "/a", "a/", "a//b", "a/./b", "../a", "C:a", "a\\b", "a**b", "***", "a\n", "\ud800", new string('a', 1025) })
        {
            var result = await f.Files.SearchFilesAsync(".", pattern, default);
            Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.InvalidPattern && result.Data is null, "SEARCH-AC-006");
        }
        Assert.IsTrue(f.Fs.ProductInspectionCount == 0 && f.Fs.Opened == 0 && f.Fs.Scanned == 0, "SEARCH-AC-006-io");
    }

    [TestMethod]
    public async Task SEARCHAC008_IgnoredDirectoriesHaveAnExplicitStartException()
    {
        await using var f = new DiscoveryFixture();
        f.Fs.Directory("OBJ"); f.Fs.File("OBJ/a", [65]); f.Fs.File("bin", [65]);
        f.Fs.File(".normal", [65]); f.Fs.Directory(".git"); f.Fs.File(".git/a", [65]);
        var result = await f.Files.SearchFilesAsync(".", "**/*", default);
        Assert.IsTrue(result.Data!.Matches.Select(m => m.Path).SequenceEqual(new[] { ".normal", "bin" }) && !result.Data.Truncated, "SEARCH-AC-008");
        Assert.IsTrue(!f.Fs.Enumerated.Contains("/ws/OBJ") && !f.Fs.Enumerated.Contains("/ws/.git"), "SEARCH-AC-008-hidden");
        Assert.IsTrue((await f.Files.SearchFilesAsync("OBJ", "*", default)).Data!.Matches.Count == 1, "SEARCH-AC-008-start");
    }

    [TestMethod]
    public async Task SEARCHAC019_TextMatchesLinesAndPreservesEmptyContext()
    {
        await using var f = new DiscoveryFixture();
        f.Fs.File("a", Encoding.UTF8.GetBytes("x\r\n\rneedle needle\nNEEDLE\n尾"));
        var result = await f.Files.SearchTextAsync(".", "needle", "**/*", true, 2, default);
        var match = result.Data!.Matches.Single();
        Assert.IsTrue(match.LineNumber == 3 && match.Content == "needle needle" && match.BeforeLines.Count == 2 &&
            match.BeforeLines[1].Content == "" && match.AfterLines.Count == 2 && match.AfterLines[1].LineNumber == 5, "SEARCH-AC-019");
        Assert.IsTrue((await f.Files.SearchTextAsync(".", "needle", "**/*", false, 0, default)).Data!.Matches.Count == 2, "SEARCH-AC-016-case");
    }

    [TestMethod]
    public async Task SEARCHAC020_RangesPreserveLineEndingsAndIndependentByteCounts()
    {
        await using var f = new DiscoveryFixture();
        f.Fs.File("a", Encoding.UTF8.GetBytes("one\r\n二\nlast"));
        var data = (await f.Files.ReadFileRangeAsync("a", 2, 20, default)).Data!;
        Assert.IsTrue(data.StartLine == 2 && data.EndLine == 3 && data.TotalLines == 3 && data.Content == "二\nlast" &&
            data.FileByteCount == 13 && data.ContentByteCount == 8 && !data.HasMore && !data.HasUtf8Bom, "SEARCH-AC-020");
        foreach (var (text, lines) in new[] { ("", 0), ("\uFEFF", 0), ("a\n", 1), ("\n", 1), ("a\n\n", 2), ("\uFEFF😀\r\n\uFEFF", 2) })
        {
            f.Fs.File("a", Encoding.UTF8.GetBytes(text));
            data = (await f.Files.ReadFileRangeAsync("a", 1, 200, default)).Data!;
            Assert.IsTrue(data.TotalLines == lines && data.Content == (text.StartsWith('\uFEFF') ? text[1..] : text) &&
                (lines == 0 ? data.StartLine is null && data.EndLine is null : data.StartLine == 1 && data.EndLine == lines), "SEARCH-AC-020-lines");
        }
    }

    [TestMethod]
    public async Task SEARCHAC021_InvalidRangePrecedesIoAndEofIsClamped()
    {
        await using var f = new DiscoveryFixture(); f.Fs.File("a", [65]);
        foreach (var (start, end) in new[] { (0, 1), (-1, 1), (2, 1), (1, 201), (int.MinValue, int.MaxValue) })
            Assert.IsTrue((await f.Files.ReadFileRangeAsync("a", start, end, default)).ErrorCode == WorkspaceErrorCode.InvalidLineRange, "SEARCH-AC-021");
        Assert.IsTrue(f.Fs.Opened == 0 && f.Fs.ProductInspectionCount == 0, "SEARCH-AC-021-io");
        Assert.IsTrue((await f.Files.ReadFileRangeAsync("a", 2, 2, default)).ErrorCode == WorkspaceErrorCode.InvalidLineRange, "SEARCH-AC-021-eof");
    }

    [TestMethod]
    public async Task SEARCHAC024_PartialReadsInvalidateOnlyTheirExactBaseline()
    {
        await using var f = new DiscoveryFixture(); f.Fs.File("a", [65]); f.Fs.File("b", [66]);
        await f.Files.ReadFileAsync("a", default); await f.Files.ReadFileAsync("b", default);
        await f.Files.SearchFilesAsync(".", "*", default);
        Assert.IsTrue(f.Files.Snapshots.Count == 2, "SEARCH-AC-025-files");
        await f.Files.SearchTextAsync(".", "A", "a", true, 0, default);
        Assert.IsTrue(!f.Files.Snapshots.TryGet("a", out _) && f.Files.Snapshots.TryGet("b", out _), "SEARCH-AC-024-text");
        await f.Files.ReadFileRangeAsync("b", 1, 1, default);
        Assert.IsTrue(f.Files.Snapshots.Count == 0, "SEARCH-AC-024-range");
    }

    [TestMethod]
    public async Task SEARCHAC018_SecretsOutsideReturnedMatchesRejectEverything()
    {
        await using var f = new DiscoveryFixture();
        f.Fs.File("a", Encoding.UTF8.GetBytes("hit")); f.Fs.File("z", Encoding.UTF8.GetBytes(GeminiFixtures.Key));
        var result = await f.Files.SearchTextAsync(".", "hit", "*", true, 0, default);
        Assert.IsTrue(result.ErrorCode == WorkspaceErrorCode.SensitiveContent && result.Data is null, "SEARCH-AC-018");
        Assert.IsTrue((await f.Files.ReadFileRangeAsync("z", 1, 1, default)).ErrorCode == WorkspaceErrorCode.SensitiveContent, "SEARCH-AC-018-range");
    }

    [TestMethod]
    public async Task SEARCHAC017_OnlyThreeUnsupportedFileClassesAreSkipped()
    {
        await using var f = new DiscoveryFixture();
        f.Fs.File("a", [65]); f.Fs.File("binary", [0]); f.Fs.File("encoding", [255]); f.Fs.File("large", new byte[131073]);
        var result = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.IsTrue(result.Data?.Matches.Count == 1 && !result.Data.Truncated, "SEARCH-AC-017");
        Assert.IsTrue((await f.Files.ReadFileRangeAsync("binary", 1, 1, default)).ErrorCode == WorkspaceErrorCode.BinaryContent, "SEARCH-AC-017-range");
        f.Fs.OnOpen = path => { if (path == "/ws/a") throw new UnauthorizedAccessException("FAKE_ONLY"); };
        result = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.IsTrue(result.Status == WorkspaceStatus.Failed && result.ErrorCode == WorkspaceErrorCode.AccessDenied && result.Data is null, "SEARCH-AC-022-access");
    }
}

internal sealed class DiscoveryFixture : IAsyncDisposable
{
    internal AIAgent.Cli.Core.BootstrapSession Session { get; } = WorkspaceFixtures.Session();
    internal ControlledWorkspaceFileSystem Fs { get; } = new();
    internal WorkspaceFiles Files { get; }
    internal DiscoveryFixture() => Files = new(Session.Workspace, Session.Policy, Fs);
    public async ValueTask DisposeAsync() { await Files.DisposeAsync(); Session.Dispose(); }
}