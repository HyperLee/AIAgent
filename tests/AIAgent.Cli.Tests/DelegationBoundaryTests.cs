using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationBoundaryTests
{
    [TestMethod]
    public async Task MULTIAC017_FullSourceLedgerRejectsNewFileBeforeReadingIt()
    {
        await using var f = await DelegationTestFixture.Create();
        for (var i = 0; i < 16; i++) f.FileSystem.File("a" + i + ".cs", [65]);
        f.FileSystem.File("b.cs", [66]);
        f.Factory.Respond = (_, request, _) => Task.FromResult(request.Messages.Count == 1 ?
            RunnerFixture.Calls(new LlmToolCall(0, null, "search_files", GeminiFixtures.Element("{\"path\":\".\",\"pattern\":\"a*\"}"))) :
            RunnerFixture.Calls(new LlmToolCall(0, null, "read_file", GeminiFixtures.Element("{\"path\":\"b.cs\"}"))));
        var result = await f.Run(1); Assert.AreEqual("delegation_limit", result.Children[0].ErrorCode); Assert.AreEqual(0, f.FileSystem.Opened);
    }

    [TestMethod]
    public void MULTIAC005_TasksJsonAccepts16384BytesAndRejects16385()
    {
        static object Tasks(string first, string second) => new
        {
            tasks = new[] {
            new { role = "explore", objective = new string('x', 2048), scope_path = first, acceptance = new string('x', 2048) },
            new { role = "review", objective = new string('x', 2048), scope_path = second, acceptance = new string('x', 2048) } }
        };
        var overhead = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Tasks("", "")));
        var secondLength = 16384 - overhead - 4000; Assert.IsTrue(secondLength is > 0 and < 4096);
        var valid = JsonSerializer.Serialize(Tasks(new string('s', 4000), new string('s', secondLength)));
        Assert.AreEqual(16384, Encoding.UTF8.GetByteCount(valid)); using var exact = JsonDocument.Parse(valid);
        Assert.AreEqual("none", DelegationJson.ParseTasks(exact.RootElement, out _));
        using var over = JsonDocument.Parse(JsonSerializer.Serialize(Tasks(new string('s', 4000), new string('s', secondLength + 1))));
        Assert.AreEqual("invalid_arguments", DelegationJson.ParseTasks(over.RootElement, out _));
    }

    [TestMethod]
    public async Task MULTIAC015_ChildCannotPublishItsFixedInstructionsAsAnalysis()
    {
        await using var f = await DelegationTestFixture.Create(); string? protectedText = null;
        f.Factory.ObserveSpec = spec => protectedText = spec.Instruction.Split('\n')[0];
        f.Factory.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(JsonSerializer.Serialize(new { version = 1, summary = protectedText, observations = Array.Empty<object>(), limitations = Array.Empty<string>() })));
        var result = await f.Run(1); Assert.IsTrue(result.RequiresContextReset); Assert.IsNull(result.Data);
    }

    [TestMethod]
    public async Task MULTIAC006_MixedNativeBatchNeverExecutesPrecedingReadAndPreservesEveryPair()
    {
        await using var f = new RunnerFixture();
        f.Client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(RunnerFixture.Read(0), new(1, null, "delegate_readonly", GeminiFixtures.Element("{}"))) : ChatFixture.Completed());
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("inspect")], default);
        Assert.AreEqual(0, f.FileSystem.Opened); Assert.AreEqual(2, f.Client.Requests.Count); Assert.AreEqual(0, f.Client.Requests[1].Tools.Count);
        Assert.AreEqual("turn_stopped", result.Operations[0].ErrorCode); Assert.AreEqual("delegation_batch_invalid", result.Operations[1].ErrorCode);
        Assert.AreEqual(2, f.Client.Requests[1].Messages[^1].Results.Count); Assert.IsTrue(ToolCallPairing.ValidTurn(result.CompletedTurn));
    }
    [TestMethod]
    public async Task MULTIAC007_ExplanationCannotReopenParentToolStage()
    {
        await using var f = new RunnerFixture();
        f.Client.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "delegate_readonly", GeminiFixtures.Element("{}"))) : RunnerFixture.Calls(RunnerFixture.Read(0)));
        var result = await f.Runner.RunAsync([LlmMessage.FromUserText("inspect")], default);
        Assert.AreEqual(0, f.FileSystem.Opened); Assert.AreEqual(2, f.Client.Requests.Count); Assert.IsTrue(result.RequiresContextReset);
    }
    [TestMethod]
    public async Task MULTIAC008_ChildForgedCapabilitiesStopOnlyThatChildWithoutApprovalOrWrites()
    {
        foreach (var name in new[] { "create_file", "run_dotnet_task", "delegate_readonly", "submit_plan", "mcp__learn__microsoft_docs_search" })
        {
            await using var f = await DelegationTestFixture.Create();
            f.Factory.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, null, name, GeminiFixtures.Element("{}"))));
            var result = await f.Run(1); var child = result.Data!.Value.GetProperty("children")[0];
            Assert.AreEqual("mode_restricted", child.GetProperty("error_code").GetString()); Assert.AreEqual(1, f.Factory.Clients[0].Requests.Count);
            Assert.AreEqual(0, f.Files.Snapshots.Count);
        }
    }
    [TestMethod]
    public async Task MULTIAC025_InvalidExploreReportIsOrdinaryFailureAndHealthySiblingCompletes()
    {
        await using var f = await DelegationTestFixture.Create();
        f.Factory.Respond = (i, _, _) => Task.FromResult(ChatFixture.Completed(i == 0 ? "invalid report" : DelegationTestFixture.EmptyReport));
        var result = await f.Run(2); Assert.AreEqual("partial", result.Data!.Value.GetProperty("state").GetString());
        Assert.AreEqual("delegation_report_invalid", result.Children[0].ErrorCode); Assert.AreEqual(DelegationTaskState.Completed, result.Children[1].State);
    }
    [TestMethod]
    public async Task MULTIAC017_SeventeenCallBatchExecutesNoTool()
    {
        await using var f = await DelegationTestFixture.Create();
        f.Factory.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(Enumerable.Range(0, 17).Select(i => new LlmToolCall(i, null, "read_file", GeminiFixtures.Element("{\"path\":\"a.cs\"}"))).ToArray()));
        var result = await f.Run(1); Assert.AreEqual("delegation_limit", result.Children[0].ErrorCode); Assert.AreEqual(1, f.Factory.Clients[0].Requests.Count);
    }
    [TestMethod]
    public async Task MULTIAC024_ValidInnerReportCannotBypassWholeChildEnvelopeLimit()
    {
        await using var f = await DelegationTestFixture.Create();
        var text = JsonSerializer.Serialize(new { version = 1, summary = new string('s', 2048), observations = Array.Empty<object>(), limitations = Enumerable.Repeat(new string('x', 1024), 8).ToArray() });
        f.Factory.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(text));
        var result = await f.Run(1); Assert.AreEqual("result_too_large", result.Children[0].ErrorCode); Assert.IsNull(result.Children[0].Report);
    }
    [TestMethod]
    public void MULTIAC024_JsonByteLimitsIncludeEnvelopeAndUnicodeEscaping()
    {
        foreach (var limit in new[] { 8192, 32768, 65536 })
        {
            Assert.AreEqual(limit, DelegationJson.Bytes(DelegationJson.Write(w => w.WriteStringValue(new string('x', limit - 2)), limit)));
            Assert.ThrowsExactly<DelegationFailure>(() => DelegationJson.Write(w => w.WriteStringValue(new string('x', limit - 1)), limit));
        }
        Assert.AreEqual(5, DelegationJson.Bytes(DelegationJson.Write(w => w.WriteStringValue("中"))));
    }
    [TestMethod]
    public async Task MULTIAC023_ExistenceObservationsNeverReadLargeOrNonUtf8Contents()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("large.bin", new byte[200000]); fs.File("invalid.txt", [255]);
        var execution = new AgentExecutionPolicy(new PlanStore(session.Policy)); await using var parent = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
        await execution.Rules!.EnsureInitializedAsync(default); await using var child = parent.CreateDelegationReadSession(".");
        var result = await child.Files.ListFilesAsync(".", default); Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(result, session.Policy, out var response, out _));
        child.Ledger.AddPending("list_files", response); child.Ledger.MarkDispatched(); Assert.AreEqual(0, fs.Opened);
        Assert.IsTrue(child.Ledger.Delivered("large.bin", null, null)); Assert.IsFalse(child.Ledger.Delivered("large.bin", 1, 1));
        Assert.AreEqual("none", await child.ValidateAsync(default)); Assert.AreEqual(0, fs.Opened);
    }
    [TestMethod]
    public async Task MULTIAC023_TextSearchContextLinesAreDeliveredOnlyAfterDispatch()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File("a.cs", Encoding.UTF8.GetBytes("before\nmatch\nafter"));
        var execution = new AgentExecutionPolicy(new PlanStore(session.Policy)); await using var parent = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
        await execution.Rules!.EnsureInitializedAsync(default); await using var child = parent.CreateDelegationReadSession(".");
        var result = await child.Files.SearchTextAsync(".", "match", "**/*", true, 1, default);
        Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(result, session.Policy, out var response, out _)); child.Ledger.AddPending("search_text", response);
        Assert.IsFalse(child.Ledger.Delivered("a.cs", 1, 3)); child.Ledger.MarkDispatched(); Assert.IsTrue(child.Ledger.Delivered("a.cs", 1, 3));
    }
}