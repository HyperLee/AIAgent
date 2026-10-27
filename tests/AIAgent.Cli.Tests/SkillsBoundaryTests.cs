using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SkillsBoundaryTests
{
    [DataRow(1, true)]
    [DataRow(64, true)]
    [DataRow(65, false)]
    [TestMethod]
    public void SKILLAC004_IdLengthBoundary(int count, bool accepted) => Assert.AreEqual(accepted, SkillFormat.ValidId(new string('a', count)));

    [DataRow(512, true)]
    [DataRow(513, false)]
    [TestMethod]
    public void SKILLAC004_DescriptionCodeUnitsAndLiteralSyntax(int length, bool accepted)
    {
        Assert.AreEqual(accepted, SkillFormat.TryParse("a", "---\ndescription: " + new string('中', length) + "\nname: a\n---\nbody\n", out _, out _));
        Assert.IsTrue(SkillFormat.TryParse("a", "---\nname: a\ndescription: Literal # and \"quotes\".\n---\n\t原文😀\r\n", out var description, out var body));
        Assert.AreEqual("Literal # and \"quotes\".", description); Assert.AreEqual("\t原文😀\r\n", body);
    }

    [DataRow("'quoted'")]
    [DataRow("\"quoted\"")]
    [DataRow("|block")]
    [DataRow(">block")]
    [DataRow("&anchor")]
    [DataRow("*alias")]
    [DataRow("!tag")]
    [DataRow("{object}")]
    [DataRow("[array]")]
    [DataRow(" space")]
    [DataRow("space ")]
    [DataRow("tab\tinside")]
    [TestMethod]
    public void SKILLAC004_UnsupportedValuesNeverBecomeYaml(string description) =>
        Assert.IsFalse(SkillFormat.TryParse("a", "---\nname: a\ndescription: " + description + "\n---\nbody", out _, out _));

    [DataRow(16384, "none")]
    [DataRow(16385, "file_too_large")]
    [TestMethod]
    public async Task SKILLAC005_RawBytesCheckedBeforeOpen(int bytes, string expected)
    {
        await using var f = new SkillsFixture(); f.Add("a", "x"); var node = f.Fs.Nodes["/ws/skills/a/SKILL.md"];
        var prefix = Encoding.UTF8.GetString(node.Bytes); node.Bytes = Encoding.UTF8.GetBytes(prefix + new string('x', bytes - node.Bytes.Length)); node.State = node.State with { Length = bytes };
        Assert.AreEqual(expected, (await f.Skills.TryUseAsync("a", default)).ErrorCode); Assert.IsTrue(f.Fs.Handles.All(h => h.Disposed));
        if (bytes > 16384) Assert.AreEqual(0, f.Fs.Opened);
    }

    [DataRow(32, "none")]
    [DataRow(33, "skills_limit")]
    [TestMethod]
    public async Task SKILLAC003_CatalogSourceLimitDoesNotGrantOrRestrictDirectUse(int count, string expected)
    {
        await using var f = new SkillsFixture(); for (var i = 0; i < count; i++) f.Add("s" + i);
        Assert.AreEqual(expected, (await f.Skills.ListAsync(default)).ErrorCode);
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("s32"[..(count == 32 ? 2 : 3)], default)).ErrorCode);
    }

    [DataRow(256, "none")]
    [DataRow(257, "skills_limit")]
    [TestMethod]
    public async Task SKILLAC003_EnumerationsCountEveryEntryAndStopAt257(int count, string expected)
    {
        await using var f = new SkillsFixture(); f.Fs.Directory("skills"); f.Fs.Names = Enumerable.Range(0, count).Select(i => "_ignored" + i).ToList();
        Assert.AreEqual(expected, (await f.Skills.ListAsync(default)).ErrorCode); Assert.IsTrue(f.Fs.Scanned <= (count == 257 ? 257 : 1024)); Assert.AreEqual(0, f.Fs.Opened);
    }

    [TestMethod]
    public async Task SKILLAC005_FullCatalogReadsExactEofAndGrowthIsBounded()
    {
        await using var f = new SkillsFixture();
        for (var i = 0; i < 32; i++)
        {
            var id = "s" + i.ToString("D2"); f.Add(id); var node = f.Fs.Nodes["/ws/skills/" + id + "/SKILL.md"];
            node.Bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(node.Bytes) + new string('x', 16384 - node.Bytes.Length)); node.State = node.State with { Length = 16384 };
        }
        Assert.AreEqual("none", (await f.Skills.ListAsync(default)).ErrorCode); Assert.AreEqual(1048576, f.Fs.TotalReadBytes);
        var before = f.Fs.TotalReadBytes; var growing = f.Fs.Nodes["/ws/skills/s31/SKILL.md"]; growing.Bytes = growing.Bytes.Append((byte)120).ToArray();
        Assert.AreEqual("file_too_large", (await f.Skills.ListAsync(default)).ErrorCode); Assert.AreEqual(524289, f.Fs.TotalReadBytes - before);
        Assert.IsTrue(f.Fs.Handles.All(h => h.Disposed));
    }

    [DataRow("link")]
    [DataRow("special")]
    [DataRow("directory")]
    [TestMethod]
    public async Task SKILLAC002_NonRegularSourceIsNeverOpened(string kind)
    {
        await using var f = new SkillsFixture(); f.Add("a"); f.Fs.Nodes["/ws/skills/a/SKILL.md"].State = new(kind == "link" ? WorkspaceNodeKind.Link : kind == "special" ? WorkspaceNodeKind.Other : WorkspaceNodeKind.Directory);
        Assert.AreNotEqual("none", (await f.Skills.ShowAsync("a", default)).ErrorCode); Assert.AreEqual(0, f.Fs.Opened);
    }

    [DataRow("1")]
    [DataRow("scope")]
    [DataRow("skill")]
    [DataRow("sha256")]
    [TestMethod]
    public async Task SKILLAC014_ShortKeysRejectFixedProjectionFieldsWithoutDisclosure(string key)
    {
        await using var f = new SkillsFixture(); f.Add("demo"); await f.Skills.TryUseAsync("demo", default);
        using var session = WorkspaceFixtures.Session(key: key);
        Assert.Throws<SkillsSensitiveException>(() => SkillsDocuments.Projection(1, ".", f.Skills.Capture().Snapshot!.Source, session.Policy));
    }

    [TestMethod]
    public async Task SKILLAC006_BomCrLfAndRawFingerprintPreserveOriginalBody()
    {
        await using var f = new SkillsFixture(); f.Add("a");
        var text = "---\r\nname: a\r\ndescription: 中文😀\r\n---\r\n  body\r\n";
        var bytes = new byte[] { 239, 187, 191 }.Concat(Encoding.UTF8.GetBytes(text)).ToArray(); f.Fs.File("skills/a/SKILL.md", bytes);
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("a", default)).ErrorCode);
        var source = f.Skills.Capture().Snapshot!.Source;
        Assert.AreEqual("  body\r\n", source.Body); Assert.AreEqual(text, source.Text); Assert.AreEqual(bytes.LongLength, source.Descriptor.RawByteCount);
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(bytes)), source.Descriptor.Sha256);
    }

    [TestMethod]
    public async Task SKILLAC003_MissingRootIsEmptyButErrorsAndBadCandidatesAreNot()
    {
        await using var f = new SkillsFixture(); Assert.AreEqual("none", (await f.Skills.ListAsync(default)).ErrorCode);
        f.Add("a"); f.Fs.File("skills/a/SKILL.md", [255]);
        var result = await f.Skills.ListAsync(default); Assert.AreEqual("invalid_utf8", result.ErrorCode); Assert.IsNull(result.DisplayBlock);
        f.Fs.Nodes["/ws/skills/a"].State = new(WorkspaceNodeKind.Link);
        Assert.AreEqual("symbolic_link", (await f.Skills.ListAsync(default)).ErrorCode);
    }

    [TestMethod]
    public async Task SKILLAC003_CatalogMetadataCapacityRejectsWholeList()
    {
        await using var f = new SkillsFixture(); for (var i = 0; i < 32; i++) f.Add("s" + i, description: new string('中', 512));
        var result = await f.Skills.ListAsync(default); Assert.AreEqual("skills_limit", result.ErrorCode); Assert.IsNull(result.DisplayBlock);
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("s0", default)).ErrorCode);
    }

    [TestMethod]
    public async Task SKILLAC015_CatalogMustObserveModeChangesOfCurrentSource()
    {
        await using var f = new SkillsFixture(); f.Add("a"); await f.Skills.TryUseAsync("a", default);
        f.Fs.Nodes["/ws/skills/a/SKILL.md"].Mode = UnixFileMode.UserRead;
        Assert.AreEqual("none", (await f.Skills.ListAsync(default)).ErrorCode);
        Assert.AreEqual(SkillsStatus.Stale, f.Skills.Capture().Status);
    }

    [TestMethod]
    public async Task SKILLAC002_NativeCaseAliasIsNotAValidSkillIdDirectory()
    {
        var root = WorkspaceFixtures.NewDirectory(); Directory.CreateDirectory(root + "/skills/Upper");
        File.WriteAllText(root + "/skills/Upper/SKILL.md", "---\nname: upper\ndescription: Literal source.\n---\nbody");
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), executionPolicy: execution);
        Assert.AreEqual("skill_not_found", (await execution.Skills!.TryUseAsync("upper", default)).ErrorCode);
    }
}