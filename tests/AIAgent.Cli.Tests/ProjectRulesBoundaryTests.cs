using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ProjectRulesBoundaryTests
{
    [DataRow(32768, true)]
    [DataRow(32769, false)]
    [TestMethod]
    public async Task RULEAC008_ExactEscapedProjectionLimit(int target, bool accepted)
    {
        await using var f = new RulesFixture(); f.Fs.Directory("sub"); var first = new string('x', 16384); var second = "";
        object Source(string path, string scope, string text) => new { kind = "project_rule", path, scope, byte_count = Encoding.UTF8.GetByteCount(text), sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))), text };
        int Bytes() => JsonSerializer.SerializeToUtf8Bytes(new { version = 1, rules_revision = 1, scope = "sub", sources = new[] { Source("AGENTS.md", ".", first), Source("sub/AGENTS.md", "sub", second) } }).Length;
        for (var i = 0; i < 5 && Bytes() != target; i++) second = new string('x', second.Length + target - Bytes());
        Assert.AreEqual(target, Bytes()); Assert.IsTrue(second.Length <= 16384);
        f.Fs.File("AGENTS.md", Encoding.UTF8.GetBytes(first)); f.Fs.File("sub/AGENTS.md", Encoding.UTF8.GetBytes(second));
        Assert.AreEqual(accepted ? "none" : "rules_limit", await f.Rules.TryChangeScopeAsync("sub", default));
        if (accepted) Assert.AreEqual(target, Encoding.UTF8.GetByteCount(f.Rules.Capture().Snapshot!.Projection));
        else Assert.IsNull(f.Rules.Capture().Snapshot);
    }

    [DataRow(65536, true)]
    [DataRow(65537, false)]
    [TestMethod]
    public void RULEAC005_FullDisplayHasIndependentExactByteLimit(int target, bool accepted)
    {
        using var session = WorkspaceFixtures.Session();
        ProjectRulesStamp Snapshot(string text) => new(0, 0, 1, ".", ProjectRulesStatus.Valid,
            new(1, new(".", [new("AGENTS.md", ".", 0, new string('a', 64), text)], []), "{}"));
        var baseLength = Encoding.UTF8.GetByteCount(ProjectRulesDocuments.Display(Snapshot(""), true, session.Policy)!);
        var display = ProjectRulesDocuments.Display(Snapshot(new string('x', target - baseLength)), true, session.Policy);
        Assert.AreEqual(accepted, display is not null);
        if (accepted) Assert.AreEqual(target, Encoding.UTF8.GetByteCount(display!));
    }

    [DataRow(new byte[] { 255 }, "invalid_utf8")]
    [DataRow(new byte[] { 0 }, "binary_content")]
    [DataRow(new byte[] { 27 }, "binary_content")]
    [TestMethod]
    public async Task RULEAC007_InvalidBytesNeverPublishPartialRules(byte[] content, string code)
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", content);
        Assert.AreEqual(code, await f.Rules.TryReloadAsync(default)); Assert.IsNull(f.Rules.Capture().Snapshot);
        Assert.AreEqual(0, f.Files.Snapshots.Count);
    }

    [DataRow("_revision")]
    [DataRow("_generation")]
    [TestMethod]
    public async Task RULEAC024_CounterOverflowCannotPartiallyPublishOrLeaveAuthorityValid(string field)
    {
        await using var f = new RulesFixture(); f.Fs.File("AGENTS.md", [65]); await f.Rules.TryReloadAsync(default);
        typeof(ProjectRulesCoordinator).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(f.Rules, long.MaxValue);
        var before = f.Rules.Capture().Snapshot; f.Fs.Nodes["/ws/AGENTS.md"].Bytes = [66];
        try { await f.Rules.TryReloadAsync(default); } catch (OverflowException) { }
        Assert.AreSame(before, f.Rules.Capture().Snapshot);
        Assert.AreNotEqual(ProjectRulesStatus.Valid, f.Rules.Capture().Status);
    }

    [TestMethod]
    public async Task RULEAC012_ProcessScopeRejectsBeforeAdmissionOrApproval()
    {
        var root = WorkspaceFixtures.NewDirectory(); Directory.CreateDirectory(root + "/sub");
        await File.WriteAllTextAsync(root + "/Outside.csproj", "must not be parsed");
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        await execution.Rules!.TryChangeScopeAsync("sub", default); var approvals = 0;
        await using var tasks = new ControlledDotnetTasks(session.Workspace, session.Policy, execution, files,
            (_, _) => { approvals++; return ValueTask.FromResult(ProcessApprovalDecision.Approved); }, _ => { });
        var result = await tasks.RunAsync(DotnetTaskKind.Build, "Outside.csproj", default);
        Assert.AreEqual("rules_scope_mismatch", result.ErrorCode); Assert.AreEqual(0, approvals);
        Assert.IsFalse(Directory.Exists(root + "/obj")); Assert.IsFalse(Directory.Exists(root + "/bin"));
    }
}