using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WebCatalogTests
{
    [TestMethod]
    public async Task WEBAC002_ThreeToolsAreRequiredOrderedAndModeRestricted()
    {
        await using var f = new McpFixture(); await f.Initialize();
        Assert.AreEqual("none", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        CollectionAssert.AreEqual(new[] { McpProfile.DocsName, McpProfile.CodeName, WebFixtures.FetchName }, f.Mcp.GetTools().Select(t => t.LocalName).ToArray());
        var registry = new ToolRegistry([], f.Session.Policy, f.Execution);
        Assert.IsTrue(registry.IsExternalTool(WebFixtures.FetchName));
        Assert.AreEqual(WebFixtures.FetchName, registry.SafeName(WebFixtures.FetchName));
        Assert.IsFalse(registry.IsTaskOperation(WebFixtures.FetchName));
        Assert.AreEqual(3, registry.Definitions.Count);
        f.Plans.SetMode(AgentMode.Plan); Assert.AreEqual(3, registry.Definitions.Count);
        using (f.Execution.EnterReview()) Assert.AreEqual(0, registry.Definitions.Count);
        f.Mcp.MarkStale(); Assert.AreEqual(0, registry.Definitions.Count);
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("duplicate")]
    public async Task WEBAC002_IncompleteCatalogNeverFallsBackToSearchOnly(string kind)
    {
        await using var f = new McpFixture(); await f.Initialize();
        using var catalog = JsonDocument.Parse(McpHttpFixture.Catalog);
        var tools = catalog.RootElement.GetProperty("tools").EnumerateArray().Where(t => t.GetProperty("name").GetString() != "microsoft_docs_fetch").ToList();
        if (kind == "duplicate") { var fetch = catalog.RootElement.GetProperty("tools")[2]; tools.Add(fetch); tools.Add(fetch); }
        f.Catalog = JsonSerializer.Serialize(new { tools });
        Assert.AreEqual("mcp_catalog_invalid", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        Assert.AreEqual(McpStatus.Disconnected, f.Mcp.Capture().Status); Assert.AreEqual(0, f.Mcp.GetTools().Count);
    }

    [TestMethod]
    [DataRow("{\"type\":\"object\",\"properties\":{\"url\":{\"type\":[\"string\",\"null\"]}},\"required\":[\"url\"]}")]
    [DataRow("{\"type\":\"object\",\"properties\":{\"uri\":{\"type\":\"string\"}},\"required\":[\"uri\"]}")]
    [DataRow("{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}}}")]
    [DataRow("{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"extra\":{\"type\":\"string\"}},\"required\":[\"url\"]}")]
    [DataRow("{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\",\"format\":\"uri\"}},\"required\":[\"url\"]}")]
    public async Task WEBAC003_FetchSchemaCannotBeWeakened(string schema)
    {
        await using var f = new McpFixture(); await f.Initialize(); f.Catalog = WebFixtures.WithFetchSchema(schema);
        Assert.AreEqual("mcp_schema_unsupported", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        Assert.AreEqual(0, f.Mcp.GetTools().Count);
    }
}

internal static class WebFixtures
{
    internal const string FetchName = "mcp__learn__microsoft_docs_fetch";
    internal const string Url = "https://learn.microsoft.com/en-us/dotnet/api/system.string";
    internal static string WithFetchSchema(string schema)
    {
        using var catalog = JsonDocument.Parse(McpHttpFixture.Catalog); using var definition = JsonDocument.Parse(schema);
        var tools = catalog.RootElement.GetProperty("tools").EnumerateArray().Take(2).ToList();
        tools.Add(JsonSerializer.SerializeToElement(new { name = "microsoft_docs_fetch", description = "Fetch docs", inputSchema = definition.RootElement }));
        return JsonSerializer.Serialize(new { tools });
    }
    internal static Task<ToolExecutionResult> Fetch(McpFixture f, string url = Url) => f.Mcp.InvokeAsync(FetchName, JsonSerializer.SerializeToElement(new { url }), f.Mcp.Capture(), default);
}