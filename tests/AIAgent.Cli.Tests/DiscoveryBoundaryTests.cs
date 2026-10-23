using System.Globalization;
using System.Text;

using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DiscoveryBoundaryTests
{
    [TestMethod]
    public async Task SEARCHAC010_EntryBudgetNeverObtainsTheNextItem()
    {
        foreach (var count in new[] { 8191, 8192, 8193 })
        {
            await using var f = new DiscoveryFixture();
            f.Fs.Names = Enumerable.Range(0, count).Select(i => ".env." + i).ToList();
            var result = await f.Files.SearchFilesAsync(".", "*", default);
            Assert.IsTrue(result.Data?.Matches.Count == 0 && result.Data.Truncated == (count >= 8192) && f.Fs.Scanned == Math.Min(count, 8192), "SEARCH-AC-010");
        }
    }

    [TestMethod]
    public async Task SEARCHAC011_CandidateBudgetIncludesUnsupportedFiles()
    {
        foreach (var count in new[] { 511, 512, 513 })
        {
            await using var f = new DiscoveryFixture();
            for (var i = 0; i < count; i++) f.Fs.File(i.ToString("D4", CultureInfo.InvariantCulture), [255]);
            f.Fs.File("z.skip", [65]);
            var result = await f.Files.SearchTextAsync(".", "A", "????", true, 0, default);
            Assert.IsTrue(result.Data?.Matches.Count == 0 && result.Data.Truncated == (count >= 512) && f.Fs.Opened == Math.Min(count, 512), "SEARCH-AC-011");
        }
    }

    [TestMethod]
    public async Task SEARCHAC012_ReadBudgetNeverUsesPartialCandidatesOrOverreads()
    {
        await using var f = new DiscoveryFixture();
        for (var i = 0; i < 65; i++) f.Fs.File(i.ToString("D3", CultureInfo.InvariantCulture), Enumerable.Repeat((byte)65, 131072).ToArray());
        var result = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.IsTrue(result.Data is { Truncated: true } && f.Fs.TotalReadBytes == 8388608 && f.Fs.Opened == 64 &&
            result.Data.Matches.All(m => m.Path != "063" && m.Path != "064") && f.Fs.Handles.All(h => h.Disposed), "SEARCH-AC-012");
    }

    [TestMethod]
    public async Task SEARCHAC013_Depth32FilesAreVisibleAndDepth33IsNotEntered()
    {
        await using var f = new DiscoveryFixture(); var path = "";
        for (var i = 1; i <= 33; i++)
        {
            path = path.Length == 0 ? "d" : path + "/d"; f.Fs.Directory(path); f.Fs.File(path + "/a", [65]);
        }
        var result = await f.Files.SearchFilesAsync(".", "a", default);
        Assert.IsTrue(result.Data?.Matches.Count == 32 && result.Data.Truncated && !f.Fs.Enumerated.Contains("/ws/" + path), "SEARCH-AC-013");
    }

    [TestMethod]
    public async Task SEARCHAC014_OnlyAnOmittedMatchMarksTheResultCountAsTruncated()
    {
        foreach (var count in new[] { 199, 200, 201 })
        {
            await using var f = new DiscoveryFixture();
            for (var i = count - 1; i >= 0; i--) f.Fs.File(i.ToString("D3", CultureInfo.InvariantCulture), [65]);
            var result = await f.Files.SearchFilesAsync(".", "*", default);
            Assert.IsTrue(result.Data!.Matches.Count == Math.Min(count, 200) && result.Data.Truncated == (count > 200) &&
                result.Data.Matches[0].Path == "000" && result.Data.Matches[^1].Path == (Math.Min(count, 200) - 1).ToString("D3", CultureInfo.InvariantCulture), "SEARCH-AC-014");
        }
        await using var text = new DiscoveryFixture(); text.Fs.File("a", Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("A\n", 201))));
        var found = await text.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.IsTrue(found.Data!.Matches.Count == 200 && found.Data.Truncated && found.Data.Matches[^1].LineNumber == 200, "SEARCH-AC-014-lines");
        text.Fs.File("z", Encoding.UTF8.GetBytes(GeminiFixtures.Key));
        Assert.IsTrue((await text.Files.SearchTextAsync(".", "A", "*", true, 0, default)).ErrorCode == WorkspaceErrorCode.SensitiveContent, "SEARCH-AC-018-full-result");
    }

    [TestMethod]
    public async Task SEARCHAC015_ExactJsonEnvelopeBoundariesDoNotCutText()
    {
        foreach (var extra in new[] { -1, 0, 1 })
        {
            await using var f = new DiscoveryFixture();
            f.Fs.File("a", Encoding.UTF8.GetBytes(new string('x', 65330 + extra)));
            var range = await f.Files.ReadFileRangeAsync("a", 1, 1, default);
            if (extra <= 0)
            {
                Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(range, f.Session.Policy, out var json, out _) &&
                    Encoding.UTF8.GetByteCount(json.GetRawText()) == 65536 + extra, "SEARCH-AC-015-range");
            }
            else Assert.IsTrue(range.ErrorCode == WorkspaceErrorCode.ResultTooLarge && range.Data is null, "SEARCH-AC-015-range-reject");
            f.Fs.File("a", Encoding.UTF8.GetBytes(new string('x', 65346 + extra)));
            var search = await f.Files.SearchTextAsync(".", "x", "*", true, 0, default);
            Assert.IsTrue(search.Data!.Matches.Count == (extra <= 0 ? 1 : 0) && search.Data.Truncated == (extra > 0), "SEARCH-AC-015-search");
            if (extra <= 0) Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(search, f.Session.Policy, out var json, out _) &&
                Encoding.UTF8.GetByteCount(json.GetRawText()) == 65536 + extra, "SEARCH-AC-015-search-bytes");
        }
        await using var escaped = new DiscoveryFixture(); escaped.Fs.File("a", Encoding.UTF8.GetBytes(new string('"', 33000)));
        Assert.IsTrue((await escaped.Files.ReadFileRangeAsync("a", 1, 1, default)).ErrorCode == WorkspaceErrorCode.ResultTooLarge, "SEARCH-AC-015-escape");
    }

    [TestMethod]
    public async Task SEARCHAC016_InvalidQueriesRejectBeforeIoAndLiteralsRemainLiteral()
    {
        await using var f = new DiscoveryFixture();
        foreach (var query in new[] { "", "a\r", "a\n", "\u2028", "\u2029", "\u0000", "\ud800", new string('x', 1025) })
            Assert.IsTrue((await f.Files.SearchTextAsync(".", query, "*", true, 0, default)).ErrorCode == WorkspaceErrorCode.InvalidQuery, "SEARCH-AC-016-invalid");
        Assert.IsTrue(f.Fs.Inspected.Count == 0 && f.Fs.Opened == 0, "SEARCH-AC-016-io");
        f.Fs.File("a", Encoding.UTF8.GetBytes(" \t.*[x]é"));
        foreach (var query in new[] { " ", "\t", ".*", "[x]", "é" })
            Assert.IsTrue((await f.Files.SearchTextAsync(".", query, "*", true, 0, default)).Data!.Matches.Count == 1, "SEARCH-AC-016-literal");
        Assert.IsTrue((await f.Files.SearchTextAsync(".", "e\u0301", "*", false, 0, default)).Data!.Matches.Count == 0, "SEARCH-AC-016-normalization");
    }

    [TestMethod]
    public async Task SEARCHAC004_InvalidAndSensitivePathsNeverOpenContent()
    {
        await using var f = new DiscoveryFixture();
        foreach (var path in new[] { "/ws", "../a", "a\\b", "C:a", "\ud800", new string('a', 4097), ".git", "x/.env.example" })
        {
            Assert.IsTrue((await f.Files.SearchFilesAsync(path, "*", default)).Data is null, "SEARCH-AC-004-files");
            Assert.IsTrue((await f.Files.SearchTextAsync(path, "x", "*", true, 0, default)).Data is null, "SEARCH-AC-004-text");
            Assert.IsTrue((await f.Files.ReadFileRangeAsync(path, 1, 1, default)).Data is null, "SEARCH-AC-004-range");
        }
        Assert.IsTrue(f.Fs.Opened == 0 && f.Fs.Inspected.Count == 0, "SEARCH-AC-004-io");
    }

    [TestMethod]
    public async Task SEARCHAC025_UnstartedOperationsPreserveBaselines()
    {
        await using var f = new DiscoveryFixture(); f.Fs.File("a", [65]); await f.Files.ReadFileAsync("a", default);
        await f.Files.ReadFileRangeAsync("a", 0, 1, default);
        await f.Files.SearchTextAsync(".", "A", "*", true, 0, new CancellationToken(true));
        await f.Files.SearchTextAsync(".", "A", "b", true, 0, default);
        Assert.IsTrue(f.Files.Snapshots.Count == 1, "SEARCH-AC-025-preserved");
        f.Fs.File("a", [255]); await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.IsTrue(f.Files.Snapshots.Count == 0, "SEARCH-AC-025-format");
    }
}