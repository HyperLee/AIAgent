using System.Text;

using AIAgent.Cli.Git;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitDomainTests
{
    [TestMethod]
    public void GITAC014_DiffPreservesSideLinesAndRawEndings()
    {
        var oldSide = Source("one\r\ntwo\nlast", GitSide.Old);
        var newSide = Source("one\r\nchanged\rlast", GitSide.New);
        var diff = GitDiffBuilder.Create("g1", new("a.cs", GitLayer.Staged, oldSide, newSide), default);
        Assert.AreEqual(1, diff.Hunks.Count);
        StringAssert.Contains(diff.Text, "-two [LF]");
        StringAssert.Contains(diff.Text, "+changed [CR]");
        StringAssert.Contains(diff.Text, " last [EOF]");
        CollectionAssert.AreEqual(new[] { 2 }, diff.OldChangedLines.ToArray());
        CollectionAssert.AreEqual(new[] { 2 }, diff.NewChangedLines.ToArray());
        Assert.AreEqual(1, diff.Hunks[0].OldStart);
        Assert.AreEqual(3, diff.Hunks[0].NewCount);
    }

    [TestMethod]
    public void GITAC006_BomOnlyAndEmptyCreationHaveNoFindingAnchor()
    {
        var bytes = new byte[] { 239, 187, 191 }.Concat(Encoding.UTF8.GetBytes("same\n")).ToArray();
        var change = new GitChange("a.cs", GitLayer.Staged, Source("same\n", GitSide.Old), new("a.cs", GitLayer.Staged, GitSide.New, bytes, "100644"));
        Assert.IsFalse(change.ContentAvailable);
        Assert.AreEqual("metadata", change.ChangeKind);
        var empty = new GitChange("a.cs", GitLayer.Staged, GitSource.Absent("a.cs", GitLayer.Staged, GitSide.Old), Source("", GitSide.New));
        Assert.IsFalse(empty.ContentAvailable);
        Assert.AreEqual("added", empty.ChangeKind);
        Assert.AreEqual(0, empty.New.TotalLines);
    }

    [TestMethod]
    public void GITAC014_ThreeContextLinesAndAddedDeletedSidesAreExact()
    {
        var old = Source("1\n2\n3\n4\nold\n6\n7\n8\n9\n", GitSide.Old);
        var next = Source("1\n2\n3\n4\nnew\n6\n7\n8\n9\n", GitSide.New);
        var diff = GitDiffBuilder.Create("g2", new("a.cs", GitLayer.Staged, old, next), default);
        Assert.AreEqual(2, diff.Hunks[0].OldStart);
        Assert.AreEqual(7, diff.Hunks[0].OldCount);
        Assert.IsFalse(diff.Text.Contains(" 1 [LF]", StringComparison.Ordinal));
        Assert.IsFalse(diff.Text.Contains(" 9 [LF]", StringComparison.Ordinal));
        var deletion = GitDiffBuilder.Create("g3", new("a.cs", GitLayer.Staged, old, GitSource.Absent("a.cs", GitLayer.Staged, GitSide.New)), default);
        Assert.AreEqual(0, deletion.Hunks[0].NewStart);
        Assert.AreEqual(0, deletion.Hunks[0].NewCount);
        Assert.AreEqual(9, deletion.OldChangedLines.Count);
    }

    [TestMethod]
    public void GITAC011_SourceDefensivelyCopiesAndDoesNotPrintContent()
    {
        var bytes = Encoding.UTF8.GetBytes("private fixture");
        var source = new GitSource("a.cs", GitLayer.Staged, GitSide.New, bytes, "100644");
        bytes[0] = 0;
        var copy = source.CopyBytes(); copy[0] = 0;
        Assert.AreEqual("private fixture", source.Content);
        Assert.AreEqual("[GitSource]", source.ToString());
        Assert.AreEqual(64, source.Sha256!.Length);
    }

    private static GitSource Source(string text, GitSide side) => new("a.cs", GitLayer.Staged, side, Encoding.UTF8.GetBytes(text), "100644");
}