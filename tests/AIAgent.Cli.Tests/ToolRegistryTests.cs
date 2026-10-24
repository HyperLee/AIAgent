using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ToolRegistryTests
{
    [TestMethod]
    public async Task LOOPAC004_InvalidArgumentsAndUnknownNamesNeverReadWorkspace()
    {
        using var session = WorkspaceFixtures.Session();
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65]);
        await using var workspace = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var registry = new ToolRegistry([new ListFilesTool(workspace, session.Policy), new ReadFileTool(workspace, session.Policy)], session.Policy);
        foreach (var json in new[] { "{}", "null", "[]", "{\"path\":null}", "{\"path\":0}", "{\"path\":true}",
            "{\"path\":{}}", "{\"path\":[]}", "{\"Path\":\"a\"}", "{\"path\":\"a\",\"extra\":1}", "{\"path\":\"a\",\"path\":\"b\"}" })
        {
            var result = await registry.DispatchAsync(new(0, null, "read_file", GeminiFixtures.Element(json)), default);
            Assert.IsTrue(result.Status == ToolStatus.Rejected && result.ErrorCode == "invalid_arguments" && result.RelativePath is null, "LOOP-AC-004");
        }
        foreach (var name in new[] { "Read_file", "create_file", "shell" })
            Assert.IsTrue((await registry.DispatchAsync(new(0, null, name, GeminiFixtures.Element("{\"path\":\"a\"}")), default)).ErrorCode == "unknown_tool", "LOOP-AC-011");
        Assert.AreEqual(0, fs.Opened, "LOOP-AC-004");
        Assert.AreEqual(0, fs.Inspected.Count, "LOOP-AC-004");
        Assert.Throws<ArgumentException>(() => new ToolRegistry([new ReadFileTool(workspace, session.Policy), new ReadFileTool(workspace, session.Policy)], session.Policy), "LOOP-AC-002");
    }

    [TestMethod]
    public async Task LOOPAC013_RealReadAndListKeepWorkspaceProjectionAndMetadata()
    {
        using var session = WorkspaceFixtures.Session();
        var fs = new ControlledWorkspaceFileSystem(); fs.File("a", [65, 10]);
        await using var workspace = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var registry = new ToolRegistry([new ListFilesTool(workspace, session.Policy), new ReadFileTool(workspace, session.Policy)], session.Policy);
        var result = await registry.DispatchAsync(new(3, null, "read_file", GeminiFixtures.Element("{\"path\":\"a\"}")), default);
        Assert.IsTrue(result.Status == ToolStatus.Success && result.ByteCount == 2 && result.RelativePath == "a" && workspace.Snapshots.Count == 1, "LOOP-AC-013");
        Assert.IsTrue(result.Response!.Value.GetProperty("data").GetProperty("content").GetString() == "A\n", "LOOP-AC-013");
        var list = await registry.DispatchAsync(new(4, null, "list_files", GeminiFixtures.Element("{\"path\":\".\"}")), default);
        Assert.IsTrue(list.ReturnedEntries == 1 && !list.Truncated && list.Response!.Value.GetProperty("data").GetProperty("safety_filtered").GetBoolean(), "LOOP-AC-031");
    }

    [TestMethod]
    public async Task LOOPAC026_RegistryChecksNameIdAndAllDecodedArguments()
    {
        using var session = WorkspaceFixtures.Session();
        var fs = new ControlledWorkspaceFileSystem();
        await using var workspace = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var registry = new ToolRegistry([new ReadFileTool(workspace, session.Policy)], session.Policy);
        foreach (var call in new[] {
            new LlmToolCall(0, null, GeminiFixtures.Key, GeminiFixtures.Element("{}")),
            new LlmToolCall(0, GeminiFixtures.Key, "read_file", GeminiFixtures.Element("{}")),
            new LlmToolCall(0, null, "unknown", GeminiFixtures.Element("{\"extra\":\"" + GeminiFixtures.Key + "\"}")) })
            Assert.IsTrue((await registry.DispatchAsync(call, default)).ErrorCode == "sensitive_arguments", "LOOP-AC-026");
        Assert.AreEqual(0, fs.Inspected.Count, "LOOP-AC-026");
    }
    [TestMethod]
    public async Task LOOPAC005_RealToolsPreserveLiteralWhitespaceUnicodeAndVariableNames()
    {
        var root = WorkspaceFixtures.NewDirectory();
        var paths = new[] { " ", " a ", "中文😀.txt", "$NAME" };
        foreach (var path in paths) await File.WriteAllTextAsync(Path.Combine(root, path), "safe");
        File.CreateSymbolicLink(Path.Combine(root, "link"), Path.Combine(root, paths[0]));
        using var session = WorkspaceFixtures.Session(root);
        await using var workspace = new WorkspaceFilesFactory().Create(session);
        var registry = new ToolRegistry([new ListFilesTool(workspace, session.Policy), new ReadFileTool(workspace, session.Policy)], session.Policy);
        foreach (var path in paths)
        {
            var result = await registry.DispatchAsync(new(0, null, "read_file", System.Text.Json.JsonSerializer.SerializeToElement(new { path })), default);
            Assert.IsTrue(result.Status == ToolStatus.Success && result.RelativePath == path && result.ByteCount == 4, "LOOP-AC-005");
        }
        foreach (var (path, code) in new[] { ("", "invalid_path"), ("../a", "invalid_path"), ("/absolute", "invalid_path"),
            (".env", "sensitive_path"), ("link", "symbolic_link"), (new string('a', 4097), "invalid_path") })
            Assert.AreEqual(code, (await registry.DispatchAsync(new(0, null, "read_file", System.Text.Json.JsonSerializer.SerializeToElement(new { path })), default)).ErrorCode, "LOOP-AC-005");
        Assert.AreEqual(4, workspace.Snapshots.Count, "LOOP-AC-005");
    }

    [TestMethod]
    public async Task LOOPAC031_RealListingLimitKeepsTruncationAndSafeFiltering()
    {
        var root = WorkspaceFixtures.NewDirectory();
        for (var i = 0; i < 205; i++) await File.WriteAllTextAsync(Path.Combine(root, "safe" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)), "x");
        await File.WriteAllTextAsync(Path.Combine(root, ".env"), "synthetic hidden data");
        using var session = WorkspaceFixtures.Session(root);
        await using var workspace = new WorkspaceFilesFactory().Create(session);
        var result = await new ListFilesTool(workspace, session.Policy).ExecuteAsync(GeminiFixtures.Element("{\"path\":\".\"}"), default);
        Assert.IsTrue(result.Status == ToolStatus.Success && result.ReturnedEntries == 200 && result.Truncated, "LOOP-AC-031");
        var data = result.Response!.Value.GetProperty("data");
        Assert.IsTrue(data.GetProperty("truncated").GetBoolean() && data.GetProperty("safety_filtered").GetBoolean() &&
            data.GetProperty("entries").GetArrayLength() == 200, "LOOP-AC-031");
        Assert.IsFalse(result.Response.Value.GetRawText().Contains(".env", StringComparison.Ordinal), "LOOP-AC-031");
    }

    [TestMethod]
    public void LOOPAC002_RegistryCapturesEachValidatedDefinitionExactlyOnce()
    {
        using var session = WorkspaceFixtures.Session();
        var registry = new ToolRegistry([new ChangingDefinitionTool()], session.Policy);
        Assert.IsTrue(registry.Definitions.Single().Name == "read_file", "LOOP-AC-002");
    }


    [TestMethod]
    public async Task LOOPAC028_UnsafeResultProjectionDoesNotRetainRawSuccessMetadata()
    {
        using var session = WorkspaceFixtures.Session();
        var registry = new ToolRegistry([new UnsafeMetadataTool()], session.Policy);
        var result = await registry.DispatchAsync(RunnerFixture.Read(0), default);
        Assert.IsTrue(result.Response is null && result.RelativePath is null && result.ByteCount is null, "LOOP-AC-028");
    }
}


internal sealed class ChangingDefinitionTool : ITool
{
    private int _reads;
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition => WorkspaceToolProjection.Definition(++_reads == 1 ? "read_file" : "changed", "test definition");
    public Task<ToolExecutionResult> ExecuteAsync(System.Text.Json.JsonElement arguments, CancellationToken cancellationToken) => throw new InvalidOperationException("not executed");
}

internal sealed class UnsafeMetadataTool : ITool
{
    public ToolCapability Capability => ToolCapability.WorkspaceRead;
    public LlmToolDefinition Definition { get; } = WorkspaceToolProjection.Definition("read_file", "test definition");
    public Task<ToolExecutionResult> ExecuteAsync(System.Text.Json.JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(new ToolExecutionResult(ToolStatus.Success, "none", null, GeminiFixtures.Key, 1));
}