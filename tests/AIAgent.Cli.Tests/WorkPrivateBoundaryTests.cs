using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkPrivateBoundaryTests
{
    private const string TempName = ".aiagent-work-0123456789abcdef0123456789abcdef.tmp";

    [TestMethod]
    public void SAVEAC024_PrivateNamesAreSensitiveInEverySegmentIncludingActualStoreRoot()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var name in new[] { ".aiagent", ".store.lock", TempName })
        {
            Assert.IsTrue(WorkspacePathRules.IsSensitive(name), name);
            Assert.IsTrue(WorkspacePathRules.IsSensitive("parent/" + name.ToUpperInvariant() + "/child"), name);
        }
        foreach (var path in new[] { home + "/.aiagent/work", home + "/.aiagent/work/id.json", "/.aiagent/work/id.json" })
            Assert.IsTrue(WorkspacePathRules.IsSensitive(path), path);
        foreach (var name in new[] { "aiagent", ".aiagent-other", ".store.locked", TempName + "x", ".aiagent-work-abc.tmp", ".gitignore", "public.json" })
            Assert.IsFalse(WorkspacePathRules.IsSensitive(name), name);
    }

    [TestMethod]
    public async Task SAVEAC024_PrivateReadSearchAndRulesRejectBeforeInspectingContents()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        foreach (var name in new[] { ".aiagent/work/id.json", ".store.lock", TempName })
        {
            Assert.AreEqual(WorkspaceErrorCode.SensitivePath, (await files.ReadFileAsync(name, default)).ErrorCode);
            Assert.AreEqual(WorkspaceErrorCode.SensitivePath, (await files.ListFilesAsync(name, default)).ErrorCode);
            Assert.AreEqual(WorkspaceErrorCode.SensitivePath, (await files.ReadFileRangeAsync(name, 1, 1, default)).ErrorCode);
            Assert.AreEqual(WorkspaceErrorCode.SensitivePath, (await files.SearchFilesAsync(name, "*", default)).ErrorCode);
            Assert.AreEqual(WorkspaceErrorCode.SensitivePath, (await files.SearchTextAsync(name, "fixture", "*", true, 0, default)).ErrorCode);
            Assert.AreEqual("sensitive_path", await files.ExecutionPolicy.Rules!.TryChangeScopeAsync(name, default));
        }
        Assert.AreEqual(0, fs.Opened); Assert.AreEqual(0, fs.Enumerated.Count);
        Assert.IsFalse(fs.Inspected.Any(path => path.Contains(".aiagent", StringComparison.Ordinal) || path.EndsWith(".store.lock", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SAVEAC024_HomeAncestorAndPrivateWorkspaceRootsCannotReadStore()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('/');
        foreach (var pair in new[] { (home, ".aiagent/work/id.json"), (Path.GetDirectoryName(home)!, Path.GetFileName(home) + "/.aiagent/work/id.json"), (home + "/.aiagent/work", "id.json") })
        {
            using var session = WorkspaceFixtures.Session(pair.Item1); var fs = new ControlledWorkspaceFileSystem();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
            Assert.AreEqual(WorkspaceErrorCode.SensitivePath, (await files.ReadFileAsync(pair.Item2, default)).ErrorCode);
            Assert.AreEqual(0, fs.Opened); Assert.AreEqual(0, fs.Inspected.Count);
        }
    }

    [TestMethod]
    public async Task SAVEAC024_DiscoveryOmitsPrivateEntriesAndDoesNotTraverseStore()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        fs.Directory(".aiagent"); fs.Directory(".aiagent/work"); fs.File(".aiagent/work/id.json", Encoding.UTF8.GetBytes("fixture"));
        fs.File(".store.lock", []); fs.File(TempName, Encoding.UTF8.GetBytes("fixture")); fs.File("public.txt", Encoding.UTF8.GetBytes("fixture"));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        var listed = await files.ListFilesAsync(".", default);
        CollectionAssert.AreEqual(new[] { "public.txt" }, listed.Data!.Entries.Select(entry => entry.Path).ToArray());
        var found = await files.SearchFilesAsync(".", "*", default);
        Assert.AreEqual(WorkspaceStatus.Success, found.Status);
        CollectionAssert.AreEqual(new[] { "public.txt" }, found.Data!.Matches.Select(match => match.Path).ToArray());
        var searched = await files.SearchTextAsync(".", "fixture", "*", true, 0, default);
        Assert.AreEqual(WorkspaceStatus.Success, searched.Status);
        CollectionAssert.AreEqual(new[] { "public.txt" }, searched.Data!.Matches.Select(match => match.Path).ToArray());
        Assert.IsFalse(fs.Enumerated.Any(path => path.Contains(".aiagent", StringComparison.Ordinal)));
        Assert.AreEqual(0, files.Snapshots.Count);
    }

    [TestMethod]
    public async Task SAVEAC024_GitProjectionFiltersPrivateTrackedAndUntrackedSources()
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files();
        fs.File("public.cs", Encoding.UTF8.GetBytes("B\n")); fs.Directory(".aiagent"); fs.Directory(".aiagent/work");
        fs.File(".aiagent/work/id.json", Encoding.UTF8.GetBytes("fixture")); fs.File(".store.lock", []); fs.File(TempName, []);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs);
        Assert.AreEqual("none", await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default));
        await using var git = new GitReadWorkspace(files, () => new PrivateManifestAdapter());
        var result = await git.CaptureAsync(default); Assert.AreEqual("none", result.ErrorCode);
        Assert.IsTrue(result.Snapshot!.Changes.Any(change => change.Path == "public.cs"));
        Assert.IsTrue(result.Snapshot.Changes.All(change => change.Path == "public.cs"));
        Assert.IsTrue(result.Snapshot.Exclusions.Any(exclusion => exclusion.Reason == "safety_filtered"));
        Assert.IsFalse(fs.Enumerated.Any(path => path.Contains(".aiagent", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task SAVEAC024_ControlledBuildRejectsPrivateTargetsAndRootBeforeAdmission()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var pair in new[] { ("/ws", ".aiagent/work/a.csproj"), ("/ws", ".store.lock/a.csproj"), ("/ws", TempName + "/a.csproj"), (home + "/.aiagent/work", "a.csproj") })
        {
            using var session = WorkspaceFixtures.Session(pair.Item1); var execution = new AgentExecutionPolicy(new(session.Policy));
            var fs = new ControlledWorkspaceFileSystem();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, executionPolicy: execution);
            await using var tasks = new ControlledDotnetTasksFactory().Create(session.Workspace, session.Policy, execution, files,
                (_, _) => { Assert.Fail("Private targets cannot reach approval."); return ValueTask.FromResult(ProcessApprovalDecision.Declined); }, _ => Assert.Fail());
            Assert.AreEqual("sensitive_path", (await tasks.RunAsync(DotnetTaskKind.Build, pair.Item2, default)).ErrorCode);
            Assert.AreEqual(0, fs.Inspected.Count); Assert.AreEqual(0, fs.Opened);
        }
    }

    [TestMethod]
    public async Task SAVEAC024_BuildAdmissionDeniesPrivateSubtreesWithoutReadingTheirFiles()
    {
        using var session = WorkspaceFixtures.Session(); var fs = AdmissionFileSystem();
        fs.Directory(".aiagent"); fs.Directory(".aiagent/work"); fs.File(".aiagent/work/Directory.Build.props", Encoding.UTF8.GetBytes("<Project />"));
        fs.File(".store.lock", [0]); fs.File(TempName, [0]);
        var snapshot = await new DotnetProjectAdmission(fs, session.Policy).InspectAsync("/ws", "a.csproj", DotnetTaskKind.Build,
            new("/sdk", "10.0.100", "/product", "/packages"), default);
        CollectionAssert.IsSubsetOf(new[] { "/ws/.aiagent", "/ws/.store.lock", "/ws/" + TempName }, snapshot.Denied.ToArray());
        Assert.IsFalse(snapshot.Contents.Keys.Any(path => path.Contains(".aiagent", StringComparison.Ordinal) || path.EndsWith(".store.lock", StringComparison.Ordinal)));
        Assert.IsFalse(fs.Enumerated.Contains("/ws/.aiagent"));
    }

    [TestMethod]
    public void SAVEAC024_DotnetNativePatternMatchesPrivateSegmentsWithoutSubstringOverreach()
    {
        foreach (var path in new[] { "/ws/.aiagent/work/id.json", "/ws/.AIAGENT/WORK/id.json", "/ws/bin/.store.lock", "/ws/bin/" + TempName, "/ws/bin/" + TempName.ToUpperInvariant() })
            Assert.IsTrue(Regex.IsMatch(path, MacSandboxPolicy.SensitivePattern), path);
        foreach (var path in new[] { "/ws/.aiagent-other/id.json", "/ws/bin/" + TempName + "x", "/ws/.store.locked", "/ws/public.json" })
            Assert.IsFalse(Regex.IsMatch(path, MacSandboxPolicy.SensitivePattern), path);
    }

    [TestMethod]
    public async Task SAVEAC028_RealNativePoliciesDenyPrivateFilesEvenWhenSourcesAreAllowed()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native private boundary fixture requires macOS."); return; }
        var installation = GitInstallation.Discover().FirstOrDefault();
        if (installation is null) { Assert.Inconclusive("Trusted native Git installation is unavailable."); return; }
        var root = WorkspaceFixtures.NewDirectory(); Directory.CreateDirectory(root + "/bin/.aiagent/work"); Directory.CreateDirectory(root + "/.git");
        var publicPath = root + "/bin/public.txt"; await File.WriteAllTextAsync(publicPath, "public fixture");
        await File.WriteAllTextAsync(root + "/.git/config", "public git metadata");
        var paths = new[] { root + "/bin/.aiagent/work/id.json", root + "/bin/.store.lock", root + "/bin/" + TempName };
        foreach (var path in paths) await File.WriteAllTextAsync(path, "private fixture");
        var snapshot = new ProcessAdmissionSnapshot(root, root + "/a.csproj", new("/sdk", "10.0.100", "/product", "/packages"), [root + "/bin"], [],
            new Dictionary<string, WorkspaceNodeState>(), new Dictionary<string, byte[]>());
        var dotnet = MacSandboxPolicy.Create(snapshot, root + "/run");
        var git = GitSandboxPolicy.Create(root, installation, new(ignoreReadFiles: paths.Append(root + "/.git/config").ToArray())) + "(allow process-exec (literal \"/bin/cat\"))\n";
        Assert.AreEqual(0, await RunSandbox(dotnet, "/bin/cat", publicPath));
        Assert.AreEqual(0, await RunSandbox(git, "/bin/cat", root + "/.git/config"));
        Assert.AreEqual(0, await RunSandbox(dotnet, "/usr/bin/touch", publicPath));
        foreach (var path in paths)
        {
            Assert.AreNotEqual(0, await RunSandbox(dotnet, "/bin/cat", path), "dotnet private read");
            Assert.AreNotEqual(0, await RunSandbox(dotnet, "/usr/bin/touch", path), "dotnet private write");
            Assert.AreNotEqual(0, await RunSandbox(git, "/bin/cat", path), "Git explicit private read deny");
            Assert.AreEqual("private fixture", await File.ReadAllTextAsync(path));
        }
        var actualRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aiagent", "work");
        StringAssert.Contains(dotnet, "(deny file-read* file-write* (subpath \"" + actualRoot + "\"))");
        StringAssert.Contains(git, "(deny file-read* file-write* (subpath \"" + actualRoot + "\"))");
    }

    private static async Task<int> RunSandbox(string policy, string executable, string path)
    {
        var start = new ProcessStartInfo("/usr/bin/sandbox-exec")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Clear(); start.ArgumentList.Add("-p"); start.ArgumentList.Add(policy); start.ArgumentList.Add(executable); start.ArgumentList.Add(path);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); await output; await error;
        return process.ExitCode;
    }

    private static ControlledWorkspaceFileSystem AdmissionFileSystem()
    {
        var fs = new ControlledWorkspaceFileSystem();
        foreach (var directory in new[] { "/product", "/sdk", "/packages", "/ws/obj" }) fs.Nodes[directory] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)fs.Nodes.Count + 20));
        var current = "";
        foreach (var segment in AppContext.BaseDirectory.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        { current += "/" + segment; fs.Nodes[current] = new(new(WorkspaceNodeKind.Directory, Identity: (ulong)fs.Nodes.Count + 100)); }
        fs.File("a.csproj", Encoding.UTF8.GetBytes("<Project />"));
        fs.File("obj/project.assets.json", Encoding.UTF8.GetBytes("{\"packageFolders\":{\"/packages/\":{}},\"targets\":{\"net10.0\":{}}}"));
        foreach (var full in new[] { "/sdk/dotnet", "/product/AIAgent.ProcessHost", "/product/AIAgent.ProcessHost.dll", "/product/AIAgent.ProcessHost.deps.json", "/product/AIAgent.ProcessHost.runtimeconfig.json", typeof(IControlledDotnetTasks).Assembly.Location })
            fs.Nodes[full] = new(new(WorkspaceNodeKind.File, 5, Identity: (ulong)fs.Nodes.Count + 300), Encoding.ASCII.GetBytes("hostA"));
        return fs;
    }

    private sealed class PrivateManifestAdapter : IGitReadAdapter
    {
        private readonly ScriptedGitAdapter _inner = new();
        public string? ExecutableIdentity => _inner.ExecutableIdentity;
        public ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken)
        {
            var paths = new[] { ".aiagent/work/id.json", ".store.lock", TempName, "public.cs" };
            var text = recipe switch
            {
                GitReadRecipe.HeadManifest => string.Concat(paths.Select(path => "100644 blob " + ScriptedGitAdapter.Old + "\t" + path + "\0")),
                GitReadRecipe.IndexManifest => string.Concat(paths.Select(path => "H 100644 " + ScriptedGitAdapter.Next + " 0\t" + path + "\0")),
                _ => null
            };
            return text is null ? _inner.RunAsync(recipe, request, cancellationToken) : ValueTask.FromResult(new GitReadResult("none", 0, Encoding.UTF8.GetBytes(text), true));
        }
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}