using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitBoundaryTests
{
    [TestMethod]
    [DataRow(2048, true)]
    [DataRow(2049, false)]
    public async Task GITAC015_SummaryUsesUtf16Limit(int length, bool accepted)
    {
        using var session = WorkspaceFixtures.Session();
        await using var snapshots = new GitSnapshotCoordinator(new ReviewGitWorkspace(), session.Policy, GitReviewTests.NextId());
        await snapshots.CaptureAsync(default); snapshots.Bind(GitSelection.Staged);
        var json = JsonSerializer.Serialize(new { version = 1, snapshot_id = "g1", selection = "staged", summary = new string('中', length), findings = Array.Empty<object>() });
        Assert.AreEqual(accepted, new CodeReviewCoordinator(snapshots, session.Policy).Validate(json) is not null);
    }

    [TestMethod]
    [DataRow(65536, true)]
    [DataRow(65537, false)]
    public async Task GITAC017_FinalJsonByteBoundaryIsIndependentOfStringLength(int bytes, bool accepted)
    {
        using var session = WorkspaceFixtures.Session();
        await using var snapshots = new GitSnapshotCoordinator(new ReviewGitWorkspace(), session.Policy, GitReviewTests.NextId());
        await snapshots.CaptureAsync(default); snapshots.Bind(GitSelection.Staged);
        var json = GitChatTests.Report("g1"); json += new string(' ', bytes - Encoding.UTF8.GetByteCount(json));
        Assert.AreEqual(accepted, new CodeReviewCoordinator(snapshots, session.Policy).Validate(json) is not null);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("\"1\"")]
    [DataRow("1.0")]
    [DataRow("0")]
    [DataRow("2")]
    public async Task GITAC015_VersionMustBeExactIntegerOne(string version)
    {
        using var session = WorkspaceFixtures.Session();
        await using var snapshots = new GitSnapshotCoordinator(new ReviewGitWorkspace(), session.Policy, GitReviewTests.NextId());
        await snapshots.CaptureAsync(default); snapshots.Bind(GitSelection.Staged);
        Assert.IsNull(new CodeReviewCoordinator(snapshots, session.Policy).Validate(GitChatTests.Report("g1").Replace("\"version\":1", "\"version\":" + version, StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("1", "200", true)]
    [DataRow("1", "201", false)]
    [DataRow("\"1\"", "2", false)]
    [DataRow("null", "2", false)]
    [DataRow("1.0", "2", false)]
    [DataRow("0", "2", false)]
    [DataRow("2", "1", false)]
    public async Task GITAC013_RangeTypesSpanAndEofAreChecked(string start, string end, bool accepted)
    {
        using var session = WorkspaceFixtures.Session(); var reader = new ReviewGitWorkspace();
        await using var snapshots = new GitSnapshotCoordinator(reader, session.Policy, GitReviewTests.NextId()); await snapshots.CaptureAsync(default);
        var args = GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\",\"side\":\"new\",\"start_line\":" + start + ",\"end_line\":" + end + "}");
        var result = await new ReadGitFileRangeTool(snapshots, session.Policy).ExecuteAsync(args, default);
        Assert.AreEqual(accepted ? ToolStatus.Success : ToolStatus.Rejected, result.Status);
        Assert.AreEqual(accepted ? 1 : 0, reader.Validations);
        if (accepted) Assert.AreEqual(2, result.Response!.Value.GetProperty("data").GetProperty("end_line").GetInt32());
    }

    [TestMethod]
    [DataRow(131072, true)]
    [DataRow(131073, false)]
    public void GITAC009_SourceLimitCountsBomAndRawBytes(int count, bool accepted)
    {
        var bytes = Enumerable.Repeat((byte)'a', count).ToArray(); bytes[0] = 239; bytes[1] = 187; bytes[2] = 191;
        if (accepted) { var source = new GitSource("a.cs", GitLayer.Staged, GitSide.New, bytes, "100644"); Assert.AreEqual((long)count, source.ByteCount); Assert.IsTrue(source.HasUtf8Bom); }
        else Assert.ThrowsExactly<GitFailure>(() => new GitSource("a.cs", GitLayer.Staged, GitSide.New, bytes, "100644"));
    }

    [TestMethod]
    [DataRow(200, "none")]
    [DataRow(201, "git_limit")]
    public async Task GITAC009_ChangedPathLimitDoesNotPublishPrefix(int count, string error)
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files();
        for (var i = 0; i < count; i++) fs.File("f" + i + ".cs", Encoding.UTF8.GetBytes("new\n"));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => new ScriptedGitAdapter(true));
        var captured = await git.CaptureAsync(default); Assert.AreEqual(error, captured.ErrorCode);
        Assert.AreEqual(error == "none", captured.Snapshot is not null);
        if (captured.Snapshot is { } snapshot) Assert.AreEqual(count, snapshot.Changes.Count);
    }

    [TestMethod]
    [DataRow(0, "none")]
    [DataRow(1, "git_limit")]
    public async Task GITAC009_TotalRawSourceBudgetIsEightMiB(int extra, string error)
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files();
        for (var i = 0; i < 64; i++) fs.File("f" + i + ".cs", Enumerable.Repeat((byte)'x', 131072).ToArray());
        if (extra > 0) fs.File("overflow.cs", [(byte)'x']);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => new ScriptedGitAdapter(true));
        var result = await git.CaptureAsync(default); Assert.AreEqual(error, result.ErrorCode); Assert.AreEqual(error == "none", result.Snapshot is not null);
    }

    [TestMethod]
    [DataRow(8192, true)]
    [DataRow(8193, false)]
    public void GITAC009_ManifestEnumerationBoundIsCheckedBeforePublication(int count, bool accepted)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(0, count).Select(i => "100644 blob " + ScriptedGitAdapter.Old + "\tf" + i + "\0")));
        if (accepted) Assert.AreEqual(count, GitManifestParser.Entries(bytes, "sha1", false).Count);
        else Assert.ThrowsExactly<GitFailure>(() => GitManifestParser.Entries(bytes, "sha1", false));
    }

    [TestMethod]
    public async Task GITAC011_CurrentKeyInSelectedSourceRejectsEntireCapture()
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files();
        fs.File("untracked.cs", Encoding.UTF8.GetBytes(GeminiFixtures.Key));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => new ScriptedGitAdapter(true));
        var result = await git.CaptureAsync(default); Assert.AreEqual("sensitive_content", result.ErrorCode); Assert.IsNull(result.Snapshot);
    }

    [TestMethod]
    public async Task GITAC009_CleanTrackedVersionsAreReleasedBetweenPaths()
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files();
        var text = new string('x', 131072);
        for (var i = 0; i < 100; i++) fs.File("f" + i + ".cs", Encoding.UTF8.GetBytes(text));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        var adapter = new ScriptedGitAdapter { RepeatedFiles = 100, RepeatedText = text };
        var baseline = GC.GetTotalMemory(true); var maximum = baseline; var read = 0;
        adapter.OnBlobRead = () => { if (++read % 10 == 0) maximum = Math.Max(maximum, GC.GetTotalMemory(true)); };
        await using var git = new GitReadWorkspace(files, () => adapter);
        var captured = await git.CaptureAsync(default);
        Assert.AreEqual("none", captured.ErrorCode); Assert.AreEqual(0, captured.Snapshot!.Changes.Count);
        Assert.IsTrue(maximum - baseline < 16777216, "Unchanged comparisons must not retain a collection-wide cache of raw bytes and decoded text.");
    }

    [TestMethod]
    [DataRow(30, 1, 1, 1, true)]
    [DataRow(31, 1, 1, 1, false)]
    [DataRow(1, 256, 1, 1, true)]
    [DataRow(1, 257, 1, 1, false)]
    [DataRow(1, 1, 2048, 1, true)]
    [DataRow(1, 1, 2049, 1, false)]
    [DataRow(1, 1, 1, 10, true)]
    [DataRow(1, 1, 1, 11, false)]
    public async Task GITAC015_FindingCountTextAndSpanLimits(int count, int titleLength, int bodyLength, int end, bool accepted)
    {
        using var session = WorkspaceFixtures.Session();
        var reader = new ReviewGitWorkspace { OldText = string.Concat(Enumerable.Range(1, 12).Select(i => "old" + i + "\n")), NewText = string.Concat(Enumerable.Range(1, 12).Select(i => "new" + i + "\n")) };
        await using var snapshots = new GitSnapshotCoordinator(reader, session.Policy, GitReviewTests.NextId()); await snapshots.CaptureAsync(default); snapshots.Bind(GitSelection.Staged);
        await new GitDiffTool(snapshots, session.Policy).ExecuteAsync(GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"path\":\"a.cs\",\"layer\":\"staged\"}"), default); snapshots.MarkDispatched();
        var findings = Enumerable.Range(0, count).Select(i => new { severity = "P2", title = count > 1 ? "problem" + i : new string('中', titleLength), body = new string('文', bodyLength), path = "a.cs", layer = "staged", side = "new", start_line = 1, end_line = end });
        var json = JsonSerializer.Serialize(new { version = 1, snapshot_id = "g1", selection = "staged", summary = "summary", findings });
        Assert.AreEqual(accepted, new CodeReviewCoordinator(snapshots, session.Policy).Validate(json) is not null);
    }

    [TestMethod]
    [DataRow(65536, true)]
    [DataRow(65537, false)]
    public void GITAC009_ToolEnvelopeByteLimitIncludesEveryField(int count, bool accepted)
    {
        using var session = WorkspaceFixtures.Session();
        const string envelope = "{\"status\":\"success\",\"error_code\":\"none\",\"data\":\"\"}";
        var result = GitProjection.Result(ToolStatus.Success, "none", session.Policy, data: w => w.WriteStringValue(new string('x', count - envelope.Length)));
        Assert.AreEqual(accepted ? ToolStatus.Success : ToolStatus.Rejected, result.Status);
        if (accepted) Assert.AreEqual(count, Encoding.UTF8.GetByteCount(result.Response!.Value.GetRawText()));
        else { Assert.AreEqual("result_too_large", result.ErrorCode); Assert.AreEqual(JsonValueKind.Null, result.Response!.Value.GetProperty("data").ValueKind); }
    }

    [TestMethod]
    [DataRow(32, "none")]
    [DataRow(33, "git_limit")]
    public async Task GITAC009_GitTraversalDepthIsRootRelative(int depth, string error)
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files(); var path = "";
        for (var i = 0; i < depth; i++) { path += path.Length == 0 ? "d" : "/d"; fs.Directory(path); }
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        await using var git = new GitReadWorkspace(files, () => new ScriptedGitAdapter(true));
        Assert.AreEqual(error, (await git.CaptureAsync(default)).ErrorCode);
    }

    [TestMethod]
    [DataRow(4096, true)]
    [DataRow(4097, false)]
    public async Task GITAC013_PathLengthIsCheckedBeforeSourceValidation(int length, bool accepted)
    {
        using var session = WorkspaceFixtures.Session(); var reader = new ReviewGitWorkspace { SourcePath = new string('a', 4096) };
        await using var snapshots = new GitSnapshotCoordinator(reader, session.Policy, GitReviewTests.NextId()); await snapshots.CaptureAsync(default);
        var arguments = JsonSerializer.Serialize(new { snapshot_id = "g1", path = new string('a', length), layer = "staged" });
        var result = await new GitDiffTool(snapshots, session.Policy).ExecuteAsync(GeminiFixtures.Element(arguments), default);
        Assert.AreEqual(accepted ? ToolStatus.Success : ToolStatus.Rejected, result.Status); Assert.AreEqual(accepted ? 1 : 0, reader.Validations);
    }

    [TestMethod]
    [DataRow("--ita-visible-in-index")]
    [DataRow("C")]
    public async Task GITAC011_CurrentKeyInFixedGitInputsIsRejectedBeforeAdapterCall(string key)
    {
        using var session = WorkspaceFixtures.Session(key: key); var fs = GitSnapshotTests.Files(); fs.File("a.cs", Encoding.UTF8.GetBytes("A\n"));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        var adapter = new ScriptedGitAdapter();
        await using var git = new GitReadWorkspace(files, () => adapter);
        Assert.AreEqual("sensitive_content", (await git.CaptureAsync(default)).ErrorCode); Assert.AreEqual(0, adapter.Calls);
    }

    [TestMethod]
    public async Task GITAC011_CurrentKeyInWorkingRootNeverBecomesAProcessArgument()
    {
        using var session = WorkspaceFixtures.Session("/ws/" + GeminiFixtures.Key); var fs = new ControlledWorkspaceFileSystem(); fs.Directory(GeminiFixtures.Key);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        var adapter = new ScriptedGitAdapter(); await using var git = new GitReadWorkspace(files, () => adapter);
        Assert.AreEqual("sensitive_content", (await git.CaptureAsync(default)).ErrorCode); Assert.AreEqual(0, adapter.Calls);
    }
}