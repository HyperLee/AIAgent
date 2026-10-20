using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkspaceTests
{
    [TestMethod]
    public void AC003_RelativeWhitespaceLiteralPathsAndStartupDirectory()
    {
        var fs = new FakeWorkspaceFileSystem(); var resolver = new WorkspaceResolver(fs);
        var cwd = Directory.GetCurrentDirectory();
        foreach (var path in new string?[] { null, "a b", "./--name", "~", "$HOME", "../other" })
        {
            var actual = resolver.Resolve(new("/startup", path));
            Assert.IsTrue(actual.RootPath == Path.GetFullPath(path ?? "/startup", "/startup"), "AC-003");
        }
        Assert.IsTrue(Directory.GetCurrentDirectory() == cwd, "AC-003");
    }

    [TestMethod]
    public void AC005_ResolvesAncestorFinalAndRelativeLinkTargets()
    {
        var fs = new FakeWorkspaceFileSystem();
        fs.Links["/alias"] = "/actual"; fs.Links["/actual/nested"] = "../target"; fs.Links["/target/end"] = "/final";
        var resolver = new WorkspaceResolver(fs);
        var root = resolver.Resolve(new("/startup", "/alias/nested/end"));
        Assert.IsTrue(root.RootPath == "/final", "AC-005");
        resolver.Revalidate(root);
        Assert.IsTrue(fs.Checked[^1] == "/final", "AC-005");
    }

    [TestMethod]
    public void AC005_CycleAndPermissionFailuresControlled()
    {
        var fs = new FakeWorkspaceFileSystem(); fs.Links["/a"] = "/b"; fs.Links["/b"] = "/a";
        ExpectFailure(() => new WorkspaceResolver(fs).Resolve(new("/", "/a")));
        foreach (var exception in new Exception[] { new UnauthorizedAccessException("FAKE_PERMISSION_ONLY"), new DirectoryNotFoundException("FAKE_MISSING_ONLY"), new IOException("FAKE_IO_ONLY") })
        {
            fs = new FakeWorkspaceFileSystem { Failure = exception };
            ExpectFailure(() => new WorkspaceResolver(fs).Resolve(new("/", "/target")));
            ExpectFailure(() => new WorkspaceResolver(fs).Revalidate(new("/target")));
        }
    }

    [TestMethod]
    public void AC005_TraversableAncestorDoesNotRequireDirectoryListingPermission()
    {
        var fs = new FakeWorkspaceFileSystem { OnlyListablePath = "/parent/workspace" };
        var root = new WorkspaceResolver(fs).Resolve(new("/", "/parent/workspace"));
        Assert.IsTrue(root.RootPath == "/parent/workspace" && fs.Checked.Count == 1, "AC-005");
    }

    [TestMethod]
    public void AC005_LinkTargetParentSegmentsFollowPhysicalLinkResolution()
    {
        var fs = new FakeWorkspaceFileSystem();
        fs.Links["/alias"] = "real/link/../target";
        fs.Links["/real/link"] = "/other/child";
        var root = new WorkspaceResolver(fs).Resolve(new("/", "/alias"));
        Assert.IsTrue(root.RootPath == "/other/target", "AC-005");
    }

    private static void ExpectFailure(Action action)
    {
        try { action(); Assert.Fail("AC-005"); }
        catch (BootstrapFailureException ex) { Assert.IsTrue(ex.Code == BootstrapErrorCode.WorkspaceUnavailable && !ex.ToString().Contains("FAKE_", StringComparison.Ordinal), "AC-005"); }
    }
}

internal sealed class FakeWorkspaceFileSystem : IWorkspaceFileSystem
{
    internal Dictionary<string, string> Links { get; } = new(StringComparer.Ordinal);
    internal List<string> Checked { get; } = [];
    internal Exception? Failure { get; set; }
    internal string? OnlyListablePath { get; set; }
    public string? GetLinkTarget(string path) => Links.GetValueOrDefault(path);
    public void EnsureDirectoryTraversable(string path) { if (Failure is not null) throw Failure; }
    public void EnsureDirectoryAccessible(string path)
    {
        if (Failure is not null) throw Failure;
        if (OnlyListablePath is not null && path != OnlyListablePath) throw new UnauthorizedAccessException("FAKE_ANCESTOR_PERMISSION_ONLY");
        Checked.Add(path);
    }
}
