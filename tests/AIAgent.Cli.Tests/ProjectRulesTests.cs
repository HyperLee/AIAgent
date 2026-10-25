using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ProjectRulesTests
{
    [TestMethod]
    public async Task RULEAC003_RootAncestorLeafAndAbsencesAreOrderedWithoutFacts()
    {
        await using var f = new RulesFixture(); f.Fs.Directory("src"); f.Fs.Directory("src/App");
        f.Fs.File("AGENTS.md", [239, 187, 191, 65, 13, 10]); f.Fs.File("src/App/AGENTS.md", []);
        Assert.AreEqual("none", await f.Rules.TryChangeScopeAsync("src/App", default));
        var snapshot = f.Rules.Capture().Snapshot!;
        CollectionAssert.AreEqual(new[] { "AGENTS.md", "src/App/AGENTS.md" }, snapshot.Sources.Select(s => s.Path).ToArray());
        Assert.AreEqual(3, snapshot.Chain.Observations.Count); Assert.AreEqual("A\r\n", snapshot.Sources[0].Text);
        Assert.AreEqual(6L, snapshot.Sources[0].RawByteCount);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(new byte[] { 239, 187, 191, 65, 13, 10 })), snapshot.Sources[0].Sha256);
        Assert.AreEqual(0, f.Files.Snapshots.Count); Assert.AreEqual(0, f.Plans.Facts.Count);
        Assert.AreEqual(1L, snapshot.RulesRevision); Assert.AreEqual(0L, snapshot.Sources[1].RawByteCount);
    }

    [DataRow(16384, "none")]
    [DataRow(16385, "file_too_large")]
    [TestMethod]
    public async Task RULEAC008_RawFileLimitIsBoundedBeforeOpening(int length, string expected)
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", Enumerable.Repeat((byte)65, length).ToArray());
        Assert.AreEqual(expected, await f.Rules.TryReloadAsync(default));
        Assert.IsTrue(f.Fs.Handles.All(h => h.Disposed));
        if (length > 16384) Assert.AreEqual(0, f.Fs.Opened);
    }

    [DataRow(8, "none")]
    [DataRow(9, "rules_limit")]
    [DataRow(32, "none")]
    [DataRow(33, "rules_limit")]
    [TestMethod]
    public async Task RULEAC008_SourceAndDepthLimitsAreIndependent(int count, string expected)
    {
        await using var f = new RulesFixture(); var scope = ".";
        for (var i = 0; i < count; i++)
        {
            if (count < 10) f.Fs.File(scope == "." ? "AGENTS.md" : scope + "/AGENTS.md", []);
            if (count < 10 && i == count - 1) break;
            scope = scope == "." ? "d" : scope + "/d"; f.Fs.Directory(scope);
        }
        Assert.AreEqual(expected, await f.Rules.TryChangeScopeAsync(scope, default));
        if (expected != "none") Assert.IsNull(f.Rules.Capture().Snapshot);
        if (count == 33) Assert.AreEqual(0, f.Fs.Inspected.Count);
    }

    [DataRow("same_length")]
    [DataRow("identity")]
    [DataRow("missing")]
    [DataRow("timestamp")]
    [DataRow("new_root")]
    [TestMethod]
    public async Task RULEAC018_SourceMutationInvalidatesAndRestoredBytesDoNotRestoreAuthority(string change)
    {
        await using var f = new RulesFixture();
        if (change != "new_root") f.Fs.File("AGENTS.md", [65]);
        Assert.AreEqual("none", await f.Rules.TryReloadAsync(default)); var old = f.Rules.Capture();
        var original = f.Fs.Nodes.GetValueOrDefault("/ws/AGENTS.md");
        if (change == "new_root") f.Fs.File("AGENTS.md", []);
        else if (change == "missing") f.Fs.Nodes.Remove("/ws/AGENTS.md");
        else if (change == "same_length") original!.Bytes = [66];
        else original!.State = change == "identity" ? original.State with { Identity = 300 } : original.State with { ModifiedSeconds = 123 };
        Assert.AreEqual("rules_changed", await f.Rules.ValidateAsync(default));
        Assert.AreEqual(ProjectRulesStatus.Stale, f.Rules.Capture().Status);
        if (change == "new_root") f.Fs.Nodes.Remove("/ws/AGENTS.md");
        else { f.Fs.Nodes["/ws/AGENTS.md"] = original!; original!.Bytes = [65]; }
        Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
        Assert.IsTrue(f.Rules.Capture().RulesRevision > old.RulesRevision); Assert.IsFalse(f.Rules.IsCurrent(old));
    }

    [TestMethod]
    public async Task RULEAC016_ValidEqualReloadPreservesRevisionAndPlanApproval()
    {
        await using var f = new RulesFixture(); Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
        var generation = f.Plans.Generation; var stamp = f.Rules.Capture();
        Assert.AreEqual("no_change", await f.Rules.TryReloadAsync(default));
        Assert.IsTrue(f.Rules.IsCurrent(stamp)); Assert.AreEqual(generation, f.Plans.Generation);
        f.Fs.Nodes["/ws"].State = f.Fs.Nodes["/ws"].State with { ModifiedSeconds = 456, Length = 999 };
        Assert.AreEqual("none", await f.Rules.ValidateAsync(default));
    }

    [TestMethod]
    public async Task RULEAC017_FailedScopeCandidatePreservesValidOldChainButDetectsConcurrentOldChange()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); f.Fs.Directory("bad");
        f.Fs.File("bad/AGENTS.md", [255]); Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
        Assert.AreEqual("invalid_utf8", await f.Rules.TryChangeScopeAsync("bad", default));
        Assert.AreEqual(".", f.Rules.Capture().Scope); Assert.AreEqual(ProjectRulesStatus.Valid, f.Rules.Capture().Status);
        f.Fs.Nodes["/ws/AGENTS.md"].Bytes = [66];
        Assert.AreEqual("invalid_utf8", await f.Rules.TryChangeScopeAsync("bad", default));
        Assert.AreEqual(ProjectRulesStatus.Stale, f.Rules.Capture().Status);
    }

    [DataRow("1")]
    [DataRow("scope")]
    [DataRow("project_rule")]
    [DataRow("sha256")]
    [TestMethod]
    public async Task RULEAC026_ShortKeysMatchingFixedFieldsRejectTheWholeProjection(string key)
    {
        await using var f = new RulesFixture(key); f.Fs.File("AGENTS.md", [65]);
        Assert.AreEqual("sensitive_content", await f.Rules.TryReloadAsync(default));
        Assert.AreEqual(ProjectRulesStatus.Unavailable, f.Rules.Capture().Status); Assert.IsNull(f.Rules.Capture().Snapshot);
    }

    [DataRow("../outside")]
    [DataRow("/ws")]
    [DataRow(".codex")]
    [DataRow(".agents")]
    [TestMethod]
    public async Task RULEAC006_InvalidOrSensitiveScopeDoesNotReadContents(string scope)
    {
        await using var f = new RulesFixture();
        Assert.AreNotEqual("none", await f.Rules.TryChangeScopeAsync(scope, default));
        Assert.AreEqual(0, f.Fs.Opened); Assert.AreEqual(0L, f.Rules.Capture().RulesRevision);
    }

    [DataRow((int)WorkspaceNodeKind.Link)]
    [DataRow((int)WorkspaceNodeKind.Other)]
    [DataRow((int)WorkspaceNodeKind.Directory)]
    [TestMethod]
    public async Task RULEAC006_NonRegularCandidatesNeverOpen(int kind)
    {
        await using var f = new RulesFixture(); f.Fs.Nodes["/ws/AGENTS.md"] = new(new((WorkspaceNodeKind)kind));
        Assert.AreNotEqual("none", await f.Rules.TryReloadAsync(default)); Assert.AreEqual(0, f.Fs.Opened);
    }

    [TestMethod]
    public async Task RULEAC010_DirectEntriesEnforceScopeAndMissingCreateParentBoundary()
    {
        await using var f = new RulesFixture(); f.Fs.Directory("src"); f.Fs.Directory("src/feature"); f.Fs.Directory("tests");
        f.Fs.File("src/a", [65]); f.Fs.File("tests/a", [66]); f.Fs.File("src/feature/AGENTS.md", []);
        Assert.AreEqual("none", await f.Rules.TryChangeScopeAsync("src", default));
        Assert.AreEqual(WorkspaceStatus.Success, (await f.Files.ReadFileAsync("src/a", default)).Status);
        Assert.AreEqual(WorkspaceErrorCode.RulesScopeMismatch, (await f.Files.ReadFileAsync("tests/a", default)).ErrorCode);
        Assert.AreEqual(WorkspaceErrorCode.RulesScopeMismatch, (await f.Files.CreateFileAsync("src/feature/new", "a", default)).ErrorCode);
        Assert.AreEqual(1, f.Files.Snapshots.Count);
    }

    [TestMethod]
    public async Task RULEAC011_SearchRejectsWholeResultAtNewBoundaryButListDoesNotExploreChildren()
    {
        await using var f = new RulesFixture(); f.Fs.File("a", [65]); f.Fs.Directory("sub"); f.Fs.File("sub/AGENTS.md", []);
        f.Fs.File("sub/hidden", [65]);
        Assert.AreEqual(WorkspaceStatus.Success, (await f.Files.ListFilesAsync(".", default)).Status);
        var found = await f.Files.SearchFilesAsync(".", "*", default);
        Assert.AreEqual(WorkspaceErrorCode.RulesScopeMismatch, found.ErrorCode); Assert.IsNull(found.Data);
        var text = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.AreEqual(WorkspaceErrorCode.RulesScopeMismatch, text.ErrorCode); Assert.IsNull(text.Data);
        Assert.IsFalse(f.Fs.Enumerated.Contains("/ws/sub"));
    }

    [TestMethod]
    public async Task RULEAC011_NewBoundaryBeforeResultPublicationDiscardsResults()
    {
        await using var f = new RulesFixture(); f.Fs.Directory("sub"); f.Fs.File("sub/a", [65]);
        f.Fs.OnRead = _ => { f.Fs.File("sub/AGENTS.md", []); return Task.CompletedTask; };
        var result = await f.Files.SearchTextAsync(".", "A", "*", true, 0, default);
        Assert.AreEqual(WorkspaceErrorCode.RulesScopeMismatch, result.ErrorCode); Assert.IsNull(result.Data);
    }

    [TestMethod]
    public async Task RULEAC008_JsonEscapingCanExceedProjectionWithoutExceedingRawFile()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", Encoding.UTF8.GetBytes(new string('中', 5400)));
        Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
        f.Fs.Nodes["/ws/AGENTS.md"].Bytes = Encoding.UTF8.GetBytes(new string('中', 5461));
        f.Fs.Nodes["/ws/AGENTS.md"].State = f.Fs.Nodes["/ws/AGENTS.md"].State with { Length = 16383 };
        Assert.AreEqual("rules_limit", await f.Rules.TryReloadAsync(default));
        Assert.AreEqual(ProjectRulesStatus.Stale, f.Rules.Capture().Status);
    }

    [TestMethod]
    public async Task RULEAC024_CancellationWaitsForBoundedReadAndPublishesNoCandidate()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Fs.OnRead = async _ => { started.TrySetResult(); await release.Task; };
        using var cancellation = new CancellationTokenSource();
        var work = f.Rules.TryReloadAsync(cancellation.Token); await started.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancellation.Cancel();
        Assert.IsFalse(work.IsCompleted); release.SetResult(); await Assert.ThrowsAsync<OperationCanceledException>(async () => await work);
        Assert.IsNull(f.Rules.Capture().Snapshot); Assert.IsTrue(f.Fs.Handles.All(h => h.Disposed));
        f.Fs.OnRead = null; Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
    }

    [TestMethod]
    public async Task RULEAC009_RulesLeaseCannotOverlapAnotherOperation()
    {
        await using var f = new RulesFixture(); Assert.IsTrue(f.Execution.WorkspaceOperations.TryAcquire(default, out var lease));
        using (lease) Assert.AreEqual("rules_unavailable", await f.Rules.TryReloadAsync(default));
        Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
    }

    [TestMethod]
    public async Task RULEAC028_RuleErrorsUseExistingEnvelopeAndOpaqueTypesDoNotPrintContents()
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); await f.Rules.TryReloadAsync(default);
        Assert.AreEqual("[ProjectRulesSnapshot]", f.Rules.Capture().Snapshot!.ToString());
        Assert.AreEqual("[ProjectRuleSource]", f.Rules.Capture().Snapshot!.Sources[0].ToString());
        foreach (var code in new[] { "rules_unavailable", "rules_changed", "rules_scope_mismatch", "rules_limit" })
        {
            var result = ToolResponseWriter.Create(ToolStatus.Rejected, code, f.Session.Policy);
            Assert.AreEqual(JsonValueKind.Null, result.Response!.Value.GetProperty("data").ValueKind);
        }
    }
}

internal sealed class RulesFixture : IAsyncDisposable
{
    internal BootstrapSession Session { get; }
    internal ControlledWorkspaceFileSystem Fs { get; } = new();
    internal PlanStore Plans { get; }
    internal AgentExecutionPolicy Execution { get; }
    internal WorkspaceFiles Files { get; }
    internal ProjectRulesCoordinator Rules => Execution.Rules!;
    internal RulesFixture(string key = GeminiFixtures.Key)
    {
        Session = WorkspaceFixtures.Session(key: key); Plans = new(Session.Policy); Execution = new(Plans);
        Files = new(Session.Workspace, Session.Policy, Fs, executionPolicy: Execution);
    }
    public async ValueTask DisposeAsync() { await Rules.DisposeAsync(); await Files.DisposeAsync(); Session.Dispose(); }
}