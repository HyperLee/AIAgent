using System.Text;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SkillsCapacityTests
{
    [TestMethod]
    public void SKILLAC005_ActiveProjectionCountsLabelAndEscapesAtExactBoundary()
    {
        using var session = WorkspaceFixtures.Session();
        var descriptor = new SkillDescriptor("a", "Synthetic fixture.", "skills/a/SKILL.md", ".", 16384, new string('0', 64));
        SkillSource Source(string body) => new(descriptor, "Synthetic fixture text.", body, 0, [], []);
        var low = 0; var high = 16384;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (SkillsDocuments.Projection(1, ".", Source(new string('\t', middle) + "x"), session.Policy) is null) high = middle - 1; else low = middle;
        }
        var body = new string('\t', low) + "x"; var json = SkillsDocuments.Projection(1, ".", Source(body), session.Policy)!;
        body += new string('x', 32768 - Encoding.UTF8.GetByteCount(SkillsDocuments.AppendixLabel + json));
        Assert.IsTrue(Encoding.UTF8.GetByteCount(body) < 16384);
        var exact = SkillsDocuments.Projection(1, ".", Source(body), session.Policy)!;
        Assert.AreEqual(32768, Encoding.UTF8.GetByteCount(SkillsDocuments.AppendixLabel + exact));
        Assert.IsNull(SkillsDocuments.Projection(1, ".", Source(body + "x"), session.Policy));
    }

    [TestMethod]
    public void SKILLAC005_CatalogAndDisplayHaveIndependentExactLimits()
    {
        using var session = WorkspaceFixtures.Session();
        SkillCatalog Catalog(int count, int padding = 0) => new(".", Enumerable.Range(0, 32).Select(i =>
            new SkillDescriptor("s" + i, new string('中', count) + (i == 31 ? new string('x', padding) : ""), "skills/s" + i + "/SKILL.md", ".", 16384, new string('0', 64))).ToArray(), 0, [], []);
        var count = 1;
        while (SkillsDocuments.Catalog(Catalog(count + 1), session.Policy) is not null) count++;
        var measured = SkillsDocuments.Catalog(Catalog(count), session.Policy)!; var remainder = 32768 - Encoding.UTF8.GetByteCount(measured);
        Assert.IsTrue(count + remainder <= 512);
        Assert.AreEqual(32768, Encoding.UTF8.GetByteCount(SkillsDocuments.Catalog(Catalog(count, remainder), session.Policy)!));
        Assert.IsNull(SkillsDocuments.Catalog(Catalog(count, remainder + 1), session.Policy));
        Assert.IsNotNull(SkillsDocuments.DisplayBlock(new string('x', 65536), session.Policy)); Assert.IsNull(SkillsDocuments.DisplayBlock(new string('x', 65537), session.Policy));
    }

    [DataRow("/skills", (int)SkillsCommandKind.Status)]
    [DataRow(" /skills list ", (int)SkillsCommandKind.List)]
    [DataRow("/skills show a", (int)SkillsCommandKind.Show)]
    [DataRow("/skills use a", (int)SkillsCommandKind.Use)]
    [DataRow("/skills reload", (int)SkillsCommandKind.Reload)]
    [DataRow("/skills off", (int)SkillsCommandKind.Off)]
    [DataRow("/Skills", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills  list", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills\tlist", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills use a b", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills use ../a", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills use /a", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills use a*", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills use 中文", (int)SkillsCommandKind.Invalid)]
    [DataRow("/skills show a\n/skills off", (int)SkillsCommandKind.Invalid)]
    [TestMethod]
    public void SKILLAC009_CommandGrammarDoesNotInterpretPathsOrShell(string command, int kind) =>
        Assert.AreEqual((SkillsCommandKind)kind, SkillsCommands.Parse(command).Kind);
}