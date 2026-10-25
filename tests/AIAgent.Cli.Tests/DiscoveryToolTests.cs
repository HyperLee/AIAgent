using System.Text.Json;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DiscoveryToolTests
{
    internal static ToolRegistry Registry(IWorkspaceFiles files, BootstrapSession session) => TestRulesRegistry.Create(
        [new ListFilesTool(files, session.Policy), new ReadFileTool(files, session.Policy),
            new CreateFileTool(files, session.Policy), new ReplaceTextTool(files, session.Policy),
            new SearchFilesTool(files, session.Policy), new SearchTextTool(files, session.Policy), new ReadFileRangeTool(files, session.Policy)], session.Policy);

    [TestMethod]
    public async Task SEARCHAC003_InvalidStructuresNeverReachWorkspace()
    {
        await using var f = new DiscoveryFixture(); var registry = Registry(f.Files, f.Session);
        foreach (var (name, args) in new[] {
            ("search_files", "{}"), ("search_files", "{\"path\":\".\",\"pattern\":null}"),
            ("search_files", "{\"path\":\".\",\"pattern\":\"*\",\"Path\":\".\"}"),
            ("search_files", "{\"path\":\".\",\"path\":\".\",\"pattern\":\"*\"}"),
            ("search_text", "{\"path\":\".\",\"query\":\"x\",\"file_pattern\":null}"),
            ("search_text", "{\"path\":\".\",\"query\":\"x\",\"case_sensitive\":1}"),
            ("search_text", "{\"path\":\".\",\"query\":\"x\",\"context_lines\":4}"),
            ("search_text", "{\"path\":\".\",\"query\":\"x\",\"context_lines\":-1}"),
            ("search_text", "{\"path\":\".\",\"query\":\"x\",\"context_lines\":1.0}"),
            ("read_file_range", "{\"path\":\"a\",\"start_line\":1e0,\"end_line\":2}"),
            ("read_file_range", "{\"path\":\"a\",\"start_line\":\"1\",\"end_line\":2}"),
            ("read_file_range", "{\"path\":\"a\",\"start_line\":2147483648,\"end_line\":2}"),
            ("read_file_range", "{\"path\":\"a\",\"start_line\":null,\"end_line\":2}") })
        {
            var result = await registry.DispatchAsync(new(0, null, name, GeminiFixtures.Element(args)), default);
            Assert.IsTrue(result.ErrorCode == "invalid_arguments" && result.Status == ToolStatus.Rejected && result.DiscoverySummary is null, "SEARCH-AC-003");
        }
        Assert.IsTrue(f.Fs.ProductInspectionCount == 0 && f.Fs.Scanned == 0 && f.Fs.Opened == 0, "SEARCH-AC-003-io");
    }

    [TestMethod]
    public async Task SEARCHAC001_SevenDefinitionsAndDefaultsReachTheSameWorkspace()
    {
        await using var f = new DiscoveryFixture(); f.Fs.File("a", [65, 10, 66]);
        var registry = Registry(f.Files, f.Session);
        Assert.IsTrue(registry.Definitions.Select(d => d.Name).SequenceEqual(new[] {
            "list_files", "read_file", "create_file", "replace_text", "search_files", "search_text", "read_file_range" }), "SEARCH-AC-001");
        Assert.IsTrue(f.Fs.ProductInspectionCount == 0, "SEARCH-AC-001-factory");
        var result = await registry.DispatchAsync(BudgetFixture.Call("search_text", new { path = ".", query = "A" }), default);
        var match = result.Response!.Value.GetProperty("data").GetProperty("matches")[0];
        Assert.IsTrue(match.GetProperty("after_lines").GetArrayLength() == 1 && result.DiscoverySummary?.ReturnedCount == 1 &&
            result.ByteCount is null && result.ReturnedEntries is null && result.DiscoverySummary.Kind == DiscoveryKind.TextSearch, "SEARCH-AC-001-defaults");
        Assert.IsTrue(!result.StopAfterOperation && result.WithoutResponse().DiscoverySummary is null, "SEARCH-AC-002-response");
    }

    [TestMethod]
    public async Task SEARCHAC030_ShortKeysCannotBypassProjectionChecks()
    {
        foreach (var key in new[] { "matches", "line_number", "status", "success", "content_byte_count" })
        {
            using var session = WorkspaceFixtures.Session(key: key); var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            var registry = Registry(files, session);
            var call = key == "content_byte_count" ? BudgetFixture.Call("read_file_range", new { path = "a", start_line = 1, end_line = 1 }) :
                BudgetFixture.Call("search_text", new { path = ".", query = "A" });
            var result = await registry.DispatchAsync(call, default);
            Assert.IsTrue(result.Status != ToolStatus.Success && result.DiscoverySummary is null && result.RelativePath is null, "SEARCH-AC-030");
        }
    }

    [TestMethod]
    public async Task SEARCHAC031_SummariesContainCountsAndRangeBytesWithoutContents()
    {
        await using var f = new DiscoveryFixture(); f.Fs.File("a", [65, 10, 66]); var registry = Registry(f.Files, f.Session);
        foreach (var (call, expected) in new[] {
            (BudgetFixture.Call("search_files", new { path = ".", pattern = "*" }), "找到安全檔案：1 筆"),
            (BudgetFixture.Call("search_text", new { path = ".", query = "A" }), "找到文字命中：1 行"),
            (BudgetFixture.Call("read_file_range", new { path = "a", start_line = 1, end_line = 1 }), "第 1～1 行；回傳 2 bytes") })
        {
            var result = await registry.DispatchAsync(call, default);
            var local = new LocalToolOutcome(1, 0, call.Name, result);
            var display = ChatDisplay.Format(new(ChatEventKind.AgentProgress, agentEvent: new(AgentEventKind.ToolCompleted, 1, 0, call.Name, local)));
            Assert.IsTrue(display.Contains(expected, StringComparison.Ordinal) && !display.Contains("已讀取", StringComparison.Ordinal) &&
                !display.Contains("已列出", StringComparison.Ordinal) && !display.Contains("A\nB", StringComparison.Ordinal), "SEARCH-AC-031");
        }
    }

    [TestMethod]
    public void SEARCHAC002_DefensiveCopiesAndSafeTypeMarkersProtectOwnedData()
    {
        var matches = new[] { new WorkspaceFileMatch("a") };
        var data = new WorkspaceFileSearchData(".", matches, false); matches[0] = new("b");
        var before = new[] { new WorkspaceContextLine(1, "private text") };
        var text = new WorkspaceTextMatch("a", 2, "text", before, []); before[0] = new(1, "changed");
        Assert.IsTrue(data.Matches[0].Path == "a" && text.BeforeLines[0].Content == "private text" &&
            text.ToString() == "[WorkspaceTextMatch]" && data.ToString() == "[WorkspaceFileSearchData]", "SEARCH-AC-002-copy");
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<WorkspaceFileMatch>)data.Matches)[0] = new("c"), "SEARCH-AC-002-readonly");
        Assert.ThrowsExactly<ArgumentException>(() => WorkspaceFileSearchResult.End(WorkspaceStatus.Success, WorkspaceErrorCode.None), "SEARCH-AC-002-state");
    }
}