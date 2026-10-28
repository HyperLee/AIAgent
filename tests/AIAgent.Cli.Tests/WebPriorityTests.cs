using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WebPriorityTests
{
    [TestMethod]
    [DataRow("stale")]
    [DataRow("deadline")]
    public async Task WEBAC022_MetadataSensitivitySurvivesStaleAndDeadlineRaces(string race)
    {
        var clock = new McpClock(); await using var f = new McpFixture(clock, key: "provider_response",
            guardFactory: guard => new SourceRaceGuard(guard, () => { if (race == "deadline") clock.Advance(31); }));
        await f.Initialize(); Assert.AreEqual("none", (await f.Mcp.ConnectAsync(default)).ErrorCode);
        f.Observe = method =>
        {
            if (method != "tools/call") return;
            if (race == "stale") f.Mcp.MarkStale();
        };
        var result = await WebFixtures.Fetch(f);
        Assert.AreEqual("sensitive_content", result.ErrorCode); Assert.IsTrue(f.Mcp.RequiresContextReset);
        Assert.AreEqual(McpTransmission.ResponseReceived, result.McpOutcome!.Transmission);
        Assert.IsFalse(result.McpOutcome.SafeResultAccepted); Assert.IsNull(result.McpOutcome.Response);
        Assert.AreEqual(1, f.Handlers[0].Calls.Count);
    }

    [TestMethod]
    public async Task WEBAC022_FatalResponseCleanupCannotBeHiddenByCatalogInvalidation()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var handler = f.Handlers[0];
        f.Observe = method => { if (method == "tools/call") f.Mcp.MarkStale(); };
        handler.MutateResponse = response =>
        {
            if (handler.Methods.Last() != "tools/call") return;
            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            response.Content.Dispose(); response.Content = new FailingContent(bytes);
            response.Content.Headers.ContentType = new("application/json");
        };
        var result = await WebFixtures.Fetch(f);
        Assert.AreEqual("unavailable", result.ErrorCode); Assert.IsTrue(f.Mcp.FatalFailure);
        Assert.IsFalse(result.McpOutcome!.SafeResultAccepted); Assert.AreEqual(1, handler.Calls.Count);
    }

    [TestMethod]
    public async Task WEBAC007_DuplicateArgumentsStillScanEveryDecodedValueBeforeStructuralRejection()
    {
        await using var f = new McpFixture(); await f.Initialize(); await f.Mcp.ConnectAsync(default);
        var result = await f.Mcp.InvokeAsync(WebFixtures.FetchName,
            GeminiFixtures.Element("{\"url\":\"x\",\"url\":\"SYNTHETIC_PROTECTED_INSTRUCTION\"}"), f.Mcp.Capture(), default);
        Assert.AreEqual("sensitive_arguments", result.ErrorCode); Assert.IsTrue(f.Mcp.RequiresContextReset);
        Assert.AreEqual(0, f.Previews.Count); Assert.AreEqual(0, f.Handlers[0].Calls.Count);
    }

    private sealed class SourceRaceGuard(IMcpContentGuard inner, Action duringProjection) : IMcpContentGuard
    {
        private bool _fired;
        public bool IsSafe(string value)
        {
            if (!_fired && value.Contains("\"observation\":\"provider_response\"", StringComparison.Ordinal))
            { _fired = true; duringProjection(); }
            return inner.IsSafe(value);
        }
        public bool IsSafe(JsonElement value) => inner.IsSafe(value);
    }
    private sealed class FailingContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        protected override void Dispose(bool disposing)
        { base.Dispose(disposing); if (disposing) throw new IOException("SYNTHETIC_RESPONSE_CLOSE_FAILURE"); }
    }
}