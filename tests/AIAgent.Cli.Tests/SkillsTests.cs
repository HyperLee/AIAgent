using System.Security.Cryptography;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SkillsTests
{
    [TestMethod]
    public void SKILLAC004_ExactExamplePreservesBodyAndRejectsUnsupportedHeaders()
    {
        const string text = "---\nname: review-change\ndescription: Review current changes.\n---\nReport findings from current changes.";
        Assert.IsTrue(SkillFormat.TryParse("review-change", text, out var description, out var body));
        Assert.AreEqual("Review current changes.", description); Assert.AreEqual("Report findings from current changes.", body);
        Assert.AreEqual(102, Encoding.UTF8.GetByteCount(text));
        Assert.AreEqual("2656293af027bea3b084c39cfb343f37d446cd5c475bb412443fe92cfd2d3889", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
        foreach (var invalid in new[] { text.Replace("name:", "other:"), text.Replace("name: review-change", "name: another"), text.Replace("description: Review", "description:  Review"), text.Replace("description: Review", "description: \"Review"), text.Replace("\n", "\r"), text[..text.LastIndexOf('\n')] })
            Assert.IsFalse(SkillFormat.TryParse("review-change", invalid, out _, out _));
    }

    [DataRow("a", true)]
    [DataRow("a-b2", true)]
    [DataRow("A", false)]
    [DataRow("a--b", false)]
    [DataRow("../a", false)]
    [DataRow("a-", false)]
    [TestMethod]
    public void SKILLAC004_IdsAreExactAscii(string id, bool accepted) => Assert.AreEqual(accepted, SkillFormat.ValidId(id));

    [TestMethod]
    public async Task SKILLAC003_CatalogIsSortedBoundedAndDoesNotPublishFactsOrSnapshots()
    {
        await using var f = new SkillsFixture(); f.Add("z"); f.Add("a"); f.Fs.Directory("skills/secrets"); f.Fs.Directory("skills/missing");
        var listed = await f.Skills.ListAsync(default);
        Assert.AreEqual("none", listed.ErrorCode); StringAssert.Contains(listed.DisplayBlock!, "\"excluded_count\":1");
        Assert.IsTrue(listed.DisplayBlock!.IndexOf("\"id\":\"a\"", StringComparison.Ordinal) < listed.DisplayBlock.IndexOf("\"id\":\"z\"", StringComparison.Ordinal));
        Assert.AreEqual(SkillsStatus.Disabled, f.Skills.Capture().Status); Assert.AreEqual(0, f.Files.Snapshots.Count); Assert.AreEqual(0, f.Plans.Facts.Count);
        StringAssert.Contains((await f.Files.Checkpoints.ListAsync(default)).DisplayBlock!, "0/32");
    }

    [TestMethod]
    public async Task SKILLAC010_UseShowReloadAndOffHaveExplicitVersionSemantics()
    {
        await using var f = new SkillsFixture(); f.Add("a");
        Assert.AreEqual(0, f.Fs.Opened); Assert.AreEqual("skill_not_active", (await f.Skills.TryReloadAsync(default)).ErrorCode);
        Assert.AreEqual("none", (await f.Skills.ShowAsync("a", default)).ErrorCode); Assert.AreEqual(SkillsStatus.Disabled, f.Skills.Capture().Status);
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("a", default)).ErrorCode); var stamp = f.Skills.Capture();
        Assert.AreEqual(1L, stamp.SkillsRevision); Assert.AreEqual("no_change", (await f.Skills.TryReloadAsync(default)).ErrorCode);
        Assert.IsTrue(f.Skills.IsCurrent(stamp));
        f.Add("a", "Changed body."); Assert.AreEqual("skills_changed", await f.Skills.ValidateAsync(default));
        Assert.AreEqual(SkillsStatus.Stale, f.Skills.Capture().Status);
        Assert.AreEqual("none", (await f.Skills.TryReloadAsync(default)).ErrorCode); Assert.AreEqual(2L, f.Skills.Capture().SkillsRevision);
        var reads = f.Fs.Opened; Assert.AreEqual("none", f.Skills.Disable().ErrorCode); Assert.AreEqual(reads, f.Fs.Opened);
        Assert.AreEqual(3L, f.Skills.Capture().SkillsRevision); Assert.AreEqual("no_change", f.Skills.Disable().ErrorCode);
    }

    [TestMethod]
    public async Task SKILLAC016_FailedCandidateRechecksOldSourceAndScopeChangesDisable()
    {
        await using var f = new SkillsFixture(); f.Add("a"); f.Add("bad");
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("a", default)).ErrorCode);
        f.Fs.File("skills/bad/SKILL.md", [65]);
        Assert.AreEqual("invalid_skill_format", (await f.Skills.TryUseAsync("bad", default)).ErrorCode);
        Assert.AreEqual(SkillsStatus.Valid, f.Skills.Capture().Status);
        f.Add("a", "Different body.");
        Assert.AreEqual("invalid_skill_format", (await f.Skills.TryUseAsync("bad", default)).ErrorCode);
        Assert.AreEqual(SkillsStatus.Stale, f.Skills.Capture().Status);
        f.Fs.Directory("sub"); Assert.AreEqual("none", await f.Rules.TryChangeScopeAsync("sub", default));
        Assert.AreEqual(SkillsStatus.Disabled, f.Skills.Capture().Status);
    }

    [TestMethod]
    public async Task SKILLAC008_NestedRulesAndUnrelatedChangesRespectSourceOwnership()
    {
        await using var f = new SkillsFixture(); f.Add("a");
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("a", default)).ErrorCode);
        f.Add("b"); f.Fs.Nodes["/ws/skills"].State = f.Fs.Nodes["/ws/skills"].State with { ModifiedSeconds = 100 };
        Assert.AreEqual("none", await f.Skills.ValidateAsync(default));
        f.Fs.File("skills/AGENTS.md", []);
        Assert.AreEqual("skills_changed", await f.Skills.ValidateAsync(default));
        Assert.AreEqual("rules_scope_mismatch", (await f.Skills.TryReloadAsync(default)).ErrorCode);
    }
}

internal sealed class SkillsFixture : IAsyncDisposable
{
    internal BootstrapSession Session { get; }
    internal ControlledWorkspaceFileSystem Fs { get; } = new();
    internal PlanStore Plans { get; }
    internal AgentExecutionPolicy Execution { get; }
    internal WorkspaceFiles Files { get; }
    internal ProjectRulesCoordinator Rules => Execution.Rules!;
    internal SkillsCoordinator Skills => Execution.Skills!;
    internal SkillsFixture(string key = GeminiFixtures.Key)
    {
        Session = WorkspaceFixtures.Session(key: key); Plans = new(Session.Policy); Execution = new(Plans);
        Files = new(Session.Workspace, Session.Policy, Fs, executionPolicy: Execution);
    }
    internal void Add(string id, string body = "Follow the current user request.", string description = "Local workflow.")
    {
        if (!Fs.Nodes.ContainsKey("/ws/skills")) Fs.Directory("skills");
        if (!Fs.Nodes.ContainsKey("/ws/skills/" + id)) Fs.Directory("skills/" + id);
        Fs.File("skills/" + id + "/SKILL.md", Encoding.UTF8.GetBytes("---\nname: " + id + "\ndescription: " + description + "\n---\n" + body));
    }
    public async ValueTask DisposeAsync() { await Skills.DisposeAsync(); await Rules.DisposeAsync(); await Files.DisposeAsync(); Session.Dispose(); }
}