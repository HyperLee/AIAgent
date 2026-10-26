using System.Diagnostics;
using System.Reflection;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class CheckpointGitTests
{
    [TestMethod]
    public async Task CKPTAC009_AbsentRootGitIsStableWithoutSearchingParentsOrRunningGit()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem();
        var adapter = new TargetAdapter(); await using var files = Create(session, fs, adapter);
        var first = await Capture(files); var second = await Capture(files);
        Assert.IsTrue(Same(first, second)); Assert.AreEqual(0, adapter.Calls.Count);
        Assert.AreEqual(0, fs.Enumerated.Count); Assert.AreEqual(0, fs.TotalReadBytes);
        fs.Directory(".git");
        Assert.IsFalse(Same(first, await Capture(files)));
    }

    [TestMethod]
    public async Task CKPTAC010_GuardUsesOnlyTargetMetadataAndIgnoresUnrelatedIndexIdentity()
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files();
        var adapter = new TargetAdapter(); await using var files = Create(session, fs, adapter);
        var first = await Capture(files);
        fs.Nodes["/ws/.git/index"].State = fs.Nodes["/ws/.git/index"].State with { Identity = 900, ModifiedSeconds = 10 };
        var second = await Capture(files);
        Assert.IsTrue(Same(first, second)); Assert.AreEqual(4, adapter.Calls.Count(x => x.Recipe == GitReadRecipe.Version));
        Assert.AreEqual(0, fs.Enumerated.Count); Assert.AreEqual(0, fs.TotalReadBytes);
        foreach (var call in adapter.Calls.Where(x => x.Recipe is GitReadRecipe.HeadManifest or GitReadRecipe.IndexManifest or GitReadRecipe.IntentToAddVisible or GitReadRecipe.IntentToAddInvisible))
        {
            Assert.AreEqual("a.cs", call.Request.Scope);
            Assert.AreEqual(0, call.Request.IgnoreReadFiles.Count);
            var arguments = GitProcessRecipe.Arguments("/ws", call.Recipe, call.Request);
            Assert.AreEqual("a.cs", arguments[^1]); Assert.IsTrue(arguments.Contains("--literal-pathspecs", StringComparer.Ordinal));
        }
        Assert.IsFalse(adapter.Calls.Any(x => x.Recipe is GitReadRecipe.Batch or GitReadRecipe.BatchCheck or GitReadRecipe.CheckIgnore));
        Assert.IsFalse(adapter.Disposed, "Injected adapters remain owned by their caller.");
    }

    [TestMethod]
    [DataRow("index")]
    [DataRow("head")]
    [DataRow("symbolic")]
    [DataRow("executable")]
    [DataRow("gitdir")]
    [DataRow("ita")]
    [DataRow("absent-index")]
    [DataRow("absent-head")]
    public async Task CKPTAC010_GuardDetectsTargetTrackingAndAdmittedIdentityChanges(string change)
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files();
        var adapter = new TargetAdapter(); await using var files = Create(session, fs, adapter);
        var first = await Capture(files);
        switch (change)
        {
            case "index": adapter.IndexOid = TargetAdapter.Other; break;
            case "head": adapter.Head = TargetAdapter.Other; break;
            case "symbolic": adapter.Reference = "refs/heads/other"; break;
            case "executable": adapter.Identity = "changed-installation"; break;
            case "gitdir": fs.Nodes["/ws/.git"].State = fs.Nodes["/ws/.git"].State with { Identity = 99 }; break;
            case "ita": adapter.IntentToAdd = true; break;
            case "absent-index": adapter.IndexAbsent = true; break;
            case "absent-head": adapter.HeadAbsent = true; break;
        }
        Assert.IsFalse(Same(first, await Capture(files)));
    }

    [TestMethod]
    public async Task CKPTAC010_UnstableCompleteObservationsRefuseRatherThanAcceptingEitherVersion()
    {
        using var session = WorkspaceFixtures.Session(); var adapter = new TargetAdapter();
        adapter.OnRun = recipe => { if (recipe == GitReadRecipe.Version && adapter.Observation == 2) adapter.IndexOid = TargetAdapter.Other; };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        var failure = await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files));
        Assert.AreEqual("checkpoint_git_conflict", failure.Code);
    }

    [TestMethod]
    [DataRow("h", "100644", 0)]
    [DataRow("S", "100644", 0)]
    [DataRow("H", "160000", 0)]
    [DataRow("H", "120000", 0)]
    [DataRow("H", "100644", 1)]
    [DataRow("H", "100644", 2)]
    [DataRow("H", "100644", 3)]
    public async Task CKPTAC010_UnsupportedTargetEntriesRequireManualRecovery(string tag, string mode, int stage)
    {
        using var session = WorkspaceFixtures.Session();
        var adapter = new TargetAdapter { Tag = tag, Mode = mode, Stage = stage };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        Assert.AreEqual("checkpoint_manual_required", (await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files))).Code);
    }

    [TestMethod]
    public async Task CKPTAC009_GitfileAndAlternateLayoutsAreManualRatherThanAbsent()
    {
        using var session = WorkspaceFixtures.Session(); var fs = new ControlledWorkspaceFileSystem(); fs.File(".git", []);
        var adapter = new TargetAdapter(); await using var files = Create(session, fs, adapter);
        Assert.AreEqual("checkpoint_manual_required", (await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files))).Code);
        Assert.AreEqual(0, adapter.Calls.Count);
        fs.Directory(".git"); fs.File(".git/commondir", []);
        Assert.AreEqual("checkpoint_manual_required", (await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files))).Code);
    }

    [TestMethod]
    public async Task CKPTAC009_UnbornHeadHasExplicitAbsenceAndNeverRequestsHeadManifest()
    {
        using var session = WorkspaceFixtures.Session(); var adapter = new TargetAdapter { Unborn = true };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        Assert.IsTrue(Same(await Capture(files), await Capture(files)));
        Assert.IsFalse(adapter.Calls.Any(x => x.Recipe == GitReadRecipe.HeadManifest));
        Assert.AreEqual(4, adapter.Calls.Count(x => x.Recipe == GitReadRecipe.VerifyHeadRef && x.Request.HeadReference == "refs/heads/main"));
    }

    [TestMethod]
    [DataRow("process_cleanup_failed", false)]
    [DataRow("process_unconfirmed", true)]
    [DataRow("process_unconfirmed", false)]
    [DataRow("sensitive_content", true)]
    [DataRow("sensitive_path", true)]
    [DataRow("git_unavailable", true)]
    [DataRow("git_timed_out", true)]
    public async Task CKPTAC016_FailuresPreserveStrictSensitiveAndCleanupClassification(string code, bool cleanup)
    {
        using var session = WorkspaceFixtures.Session(); var adapter = new TargetAdapter { Failure = new(code, cleanupConfirmed: cleanup) };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        var failure = await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files));
        Assert.AreEqual(code is "git_unavailable" or "git_timed_out" ? "checkpoint_manual_required" : code, failure.Code);
    }

    [TestMethod]
    public async Task CKPTAC025_SensitiveMetadataIsRejectedWithoutReturningGuard()
    {
        using var session = WorkspaceFixtures.Session(key: "private-fixture-value");
        var adapter = new TargetAdapter { Reference = "refs/heads/private-fixture-value" };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        var failure = await Assert.ThrowsAsync<Exception>(() => Capture(files));
        Assert.AreEqual("SensitiveContent", failure.GetType().GetProperty("Code", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(failure)!.ToString());
        Assert.IsFalse(failure.Message.Contains("private-fixture-value", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CKPTAC028_CancelledGuardDoesNotBecomeAnAbsentRepository()
    {
        using var session = WorkspaceFixtures.Session(); var adapter = new TargetAdapter();
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Capture(files, stop.Token));
        Assert.AreEqual(0, adapter.Calls.Count);
    }

    [TestMethod]
    public async Task CKPTAC028_AdapterCancellationStopsTheWriteEvenBeforeCallerTokenChanges()
    {
        using var session = WorkspaceFixtures.Session(); var adapter = new TargetAdapter { Failure = new("turn_cancelled") };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Capture(files));
    }

    [TestMethod]
    public async Task CKPTAC016_CleanupUncertaintyWinsOverConcurrentCancellation()
    {
        using var session = WorkspaceFixtures.Session(); using var stop = new CancellationTokenSource();
        var adapter = new TargetAdapter { Failure = new("process_unconfirmed", cleanupConfirmed: false) };
        adapter.OnRun = _ => stop.Cancel();
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        Assert.AreEqual("process_unconfirmed", (await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files, stop.Token))).Code);
    }

    [TestMethod]
    public async Task CKPTAC010_UnexpectedPathAndOversizedMetadataCannotProduceGuard()
    {
        using var session = WorkspaceFixtures.Session(); var adapter = new TargetAdapter { ReturnedPath = "other.cs" };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        Assert.AreEqual("checkpoint_manual_required", (await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files))).Code);
        adapter.Failure = new("none", 0, new byte[1048577]);
        Assert.AreEqual("checkpoint_manual_required", (await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files))).Code);
    }

    [TestMethod]
    [DataRow("tree")]
    [DataRow("commit")]
    public async Task CKPTAC010_TargetHeadMustActuallyBeABlob(string objectType)
    {
        using var session = WorkspaceFixtures.Session(); var adapter = new TargetAdapter { HeadType = objectType };
        await using var files = Create(session, GitSnapshotTests.Files(), adapter);
        Assert.AreEqual("checkpoint_manual_required", (await Assert.ThrowsExactlyAsync<GitFailure>(() => Capture(files))).Code);
    }

    [TestMethod]
    public async Task CKPTAC009_ReplacedRootCannotInheritTheOldRulesAndGuard()
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files(); var adapter = new TargetAdapter();
        await using var files = Create(session, fs, adapter); _ = await Capture(files);
        fs.Nodes["/ws"].State = fs.Nodes["/ws"].State with { Identity = 99 };
        var failure = await Assert.ThrowsAsync<Exception>(() => Capture(files));
        Assert.AreEqual("RulesChanged", failure.GetType().GetProperty("Code", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(failure)!.ToString());
    }

    [TestMethod]
    public async Task CKPTAC010_ObservedRootAndGitDirectoryReplacementRefuseBeforeReadmission()
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files(); var adapter = new TargetAdapter();
        await using var files = Create(session, fs, adapter); var guard = await Capture(files);
        var method = guard.GetType().GetMethod("MatchesRootObservation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        Assert.IsTrue((bool)method.Invoke(guard, ["Directory:0:1:0", "Directory:0:2:0"])!);
        Assert.IsFalse((bool)method.Invoke(guard, ["Directory:0:99:0", "Directory:0:2:0"])!);
        Assert.IsFalse((bool)method.Invoke(guard, ["Directory:0:1:0", "Directory:0:99:0"])!);
        Assert.IsFalse((bool)method.Invoke(guard, ["Directory:0:1:0", "Absent"])!);
        using var absentSession = WorkspaceFixtures.Session();
        await using var absentFiles = Create(absentSession, new ControlledWorkspaceFileSystem(), new TargetAdapter());
        var absent = await Capture(absentFiles);
        Assert.IsTrue((bool)method.Invoke(absent, ["Directory:0:1:0", "Absent"])!);
        Assert.IsFalse((bool)method.Invoke(absent, ["Directory:0:1:0", "Directory:0:2:0"])!);
    }

    [TestMethod]
    public async Task CKPTAC010_NativeGuardPreservesIndexAndDetectsGitAddWithUnchangedOpposingWorkBytes()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native checkpoint Git fixture requires macOS."); return; }
        var root = await CreateNativeFixture("sha1");
        await File.WriteAllTextAsync(root + "/a.cs", "A\n"); await FixtureGit(root, ["add", "--", "a.cs"]);
        await FixtureGit(root, ["-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "--no-verify", "-qm", "Synthetic baseline"]);
        await File.WriteAllTextAsync(root + "/a.cs", "B\n"); await FixtureGit(root, ["add", "--", "a.cs"]);
        await File.WriteAllTextAsync(root + "/a.cs", "A\n");
        using var session = WorkspaceFixtures.Session(root);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem());
        var beforeIndex = await File.ReadAllBytesAsync(root + "/.git/index");
        var opposing = await Capture(files);
        CollectionAssert.AreEqual(beforeIndex, await File.ReadAllBytesAsync(root + "/.git/index"));
        Assert.AreEqual("A\n", await File.ReadAllTextAsync(root + "/a.cs"));
        await File.WriteAllTextAsync(root + "/unrelated.cs", "unrelated\n"); await FixtureGit(root, ["add", "--", "unrelated.cs"]);
        var unrelatedIndex = await File.ReadAllBytesAsync(root + "/.git/index");
        Assert.IsTrue(Same(opposing, await Capture(files)), "Another target's index change cannot conflict with this guard.");
        CollectionAssert.AreEqual(unrelatedIndex, await File.ReadAllBytesAsync(root + "/.git/index"));
        await FixtureGit(root, ["add", "--", "a.cs"]);
        var addedIndex = await File.ReadAllBytesAsync(root + "/.git/index");
        var added = await Capture(files);
        Assert.IsFalse(Same(opposing, added), "git add changes target index metadata even though work bytes stay A.");
        CollectionAssert.AreEqual(addedIndex, await File.ReadAllBytesAsync(root + "/.git/index"));
        Assert.AreEqual("A\n", await File.ReadAllTextAsync(root + "/a.cs"));
        await File.WriteAllTextAsync(root + "/a.cs", "dirty\n");
        Assert.IsTrue(Same(added, await Capture(files)), "Work content belongs to the separate exact-byte source check.");
        Assert.AreEqual(0, files.Snapshots.Count);
    }

    [TestMethod]
    [DataRow("sha1")]
    [DataRow("sha256")]
    public async Task CKPTAC010_NativeUnbornUntrackedAndIntentToAddRemainDistinctWithoutIndexWrites(string format)
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native checkpoint Git fixture requires macOS."); return; }
        var root = await CreateNativeFixture(format); await File.WriteAllBytesAsync(root + "/a.cs", []);
        using var session = WorkspaceFixtures.Session(root);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem());
        Assert.IsFalse(File.Exists(root + "/.git/index"));
        var untracked = await Capture(files);
        Assert.IsFalse(File.Exists(root + "/.git/index"), "Read-only admission must not create an index for an untracked unborn target.");
        await FixtureGit(root, ["add", "-N", "--", "a.cs"]);
        var intentIndex = await File.ReadAllBytesAsync(root + "/.git/index");
        var intent = await Capture(files); Assert.IsFalse(Same(untracked, intent));
        CollectionAssert.AreEqual(intentIndex, await File.ReadAllBytesAsync(root + "/.git/index"));
        var intentStage = await FixtureGit(root, ["ls-files", "--stage", "--", "a.cs"]);
        await FixtureGit(root, ["add", "--", "a.cs"]);
        Assert.AreEqual(intentStage, await FixtureGit(root, ["ls-files", "--stage", "--", "a.cs"]), "The empty-blob oid, mode and stage stay identical when ITA is consumed.");
        var addedIndex = await File.ReadAllBytesAsync(root + "/.git/index");
        var added = await Capture(files);
        Assert.IsFalse(Same(intent, added), "ITA visible/invisible metadata must detect the changed flag even when object metadata and work bytes agree.");
        Assert.IsTrue(Same(added, await Capture(files)));
        CollectionAssert.AreEqual(addedIndex, await File.ReadAllBytesAsync(root + "/.git/index"));
        Assert.AreEqual(0L, new FileInfo(root + "/a.cs").Length); Assert.AreEqual(0, files.Snapshots.Count);
    }

    private static async Task<string> CreateNativeFixture(string format)
    {
        var root = "/private/tmp/aia-checkpoint-git-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        await FixtureGit(root, ["init", "--initial-branch=main", "--object-format=" + format]);
        return root;
    }

    [TestMethod]
    public async Task CKPTAC013_NativeCheckpointRestoresDirtyBeforeBytesAndPreservesIndexAndHead()
    {
        if (!OperatingSystem.IsMacOS()) { Assert.Inconclusive("Native checkpoint restoration requires macOS."); return; }
        var root = await CreateNativeFixture("sha1"); var target = root + "/a.cs";
        await File.WriteAllTextAsync(target, "HEAD\n"); await FixtureGit(root, ["add", "--", "a.cs"]);
        await FixtureGit(root, ["-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "--no-verify", "-qm", "Synthetic baseline"]);
        await File.WriteAllTextAsync(target, "staged\n"); await FixtureGit(root, ["add", "--", "a.cs"]);
        var original = Encoding.UTF8.GetBytes("\uFEFFdirty 中文\r\n"); await File.WriteAllBytesAsync(target, original);
        var index = await File.ReadAllBytesAsync(root + "/.git/index"); var head = await File.ReadAllBytesAsync(root + "/.git/HEAD");
        using var session = WorkspaceFixtures.Session(root);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval());
        Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("a.cs", default)).Status);
        Assert.AreEqual(FileWriteState.Verified, (await files.ReplaceTextAsync("a.cs", "dirty", "agent", default)).WriteState);
        Assert.IsTrue((await files.Checkpoints.ListAsync(default)).DisplayBlock!.Contains("Available", StringComparison.Ordinal));
        var result = await files.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual("none", result.ErrorCode);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(target));
        CollectionAssert.AreEqual(index, await File.ReadAllBytesAsync(root + "/.git/index"));
        CollectionAssert.AreEqual(head, await File.ReadAllBytesAsync(root + "/.git/HEAD"));
        Assert.AreEqual(0, files.Snapshots.Count);
        Assert.AreEqual(AgentMode.Plan, files.ExecutionPolicy.Mode);
    }
    private static async Task<string> FixtureGit(string root, string[] arguments)
    {
        var start = new ProcessStartInfo(GitInstallation.Discover().First().Executable)
        { WorkingDirectory = root, UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment.Clear(); foreach (var pair in GitProcessRecipe.Environment()) start.Environment.Add(pair.Key, pair.Value);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); process.StandardInput.Close();
        await process.WaitForExitAsync(); Assert.AreEqual(0, process.ExitCode, await error); return await output;
    }

    private static WorkspaceFiles Create(BootstrapSession session, ControlledWorkspaceFileSystem fs, IGitReadAdapter adapter)
    {
        return new WorkspaceFiles(session.Workspace, session.Policy, fs, checkpointGitAdapter: adapter);
    }
    private static bool Same(object first, object second) => (bool)first.GetType().GetMethod("SameAs", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(first, [second])!;
    private static async Task<object> Capture(WorkspaceFiles files, CancellationToken token = default)
    {
        var method = typeof(WorkspaceFiles).GetMethod("CaptureCheckpointGitGuardAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Checkpoint Git observations must run under the existing workspace lease.");
        await files.ExecutionPolicy.Rules!.EnsureInitializedAsync(default);
        Assert.IsTrue(files.ExecutionPolicy.WorkspaceOperations.TryAcquire(default, out var lease));
        using var owned = lease!;
        var operationType = typeof(WorkspaceFiles).GetNestedType("WorkspaceOperation", BindingFlags.NonPublic)!;
        var operation = Activator.CreateInstance(operationType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
            [((ReadSnapshotStore)files.Snapshots).Generation, files.ExecutionPolicy.Plans.ExecutionGeneration], null)!;
        operationType.GetProperty("Lease", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, lease);
        operationType.GetProperty("RulesStamp", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(operation, files.ExecutionPolicy.Rules.Capture());
        typeof(WorkspaceFiles).GetField("_activePlanGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(files, files.ExecutionPolicy.Plans.ExecutionGeneration);
        var task = (Task)method.Invoke(files, ["a.cs", operation, token])!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private sealed class TargetAdapter : IGitReadAdapter
    {
        internal const string Oid = "1111111111111111111111111111111111111111";
        internal const string Other = "2222222222222222222222222222222222222222";
        internal string IndexOid { get; set; } = Oid;
        internal string Head { get; set; } = Oid;
        internal string Reference { get; set; } = "refs/heads/main";
        internal string Identity { get; set; } = "synthetic-checkpoint-git";
        internal string Tag { get; set; } = "H";
        internal string Mode { get; set; } = "100644";
        internal string HeadType { get; set; } = "blob";
        internal int Stage { get; set; }
        internal bool IntentToAdd { get; set; }
        internal bool IndexAbsent { get; set; }
        internal bool HeadAbsent { get; set; }
        internal bool Unborn { get; set; }
        internal string ReturnedPath { get; set; } = "a.cs";
        internal int Observation { get; private set; }
        internal bool Disposed { get; private set; }
        internal GitReadResult? Failure { get; set; }
        internal Action<GitReadRecipe>? OnRun { get; set; }
        internal List<(GitReadRecipe Recipe, GitReadRequest Request)> Calls { get; } = [];
        public string? ExecutableIdentity => Identity;
        public ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Calls.Add((recipe, request));
            if (recipe == GitReadRecipe.Version) Observation++;
            OnRun?.Invoke(recipe);
            if (Failure is not null) return ValueTask.FromResult(Failure);
            var text = recipe switch
            {
                GitReadRecipe.Version => "git version 2.54.0\n",
                GitReadRecipe.InsideWorkTree => "true\n",
                GitReadRecipe.BareRepository => "false\n",
                GitReadRecipe.ObjectFormat => "sha1\n",
                GitReadRecipe.SymbolicHead => Reference + "\n",
                GitReadRecipe.VerifyHead => Unborn ? "" : Head + "\n",
                GitReadRecipe.HeadManifest => HeadAbsent ? "" : "100644 " + HeadType + " " + Oid + "\t" + ReturnedPath + "\0",
                GitReadRecipe.IndexManifest => IndexAbsent ? "" : Tag + " " + Mode + " " + IndexOid + " " + Stage + "\t" + ReturnedPath + "\0",
                GitReadRecipe.IntentToAddVisible when IntentToAdd => ":000000 100644 " + new string('0', 40) + " " + Oid + " A\0a.cs\0",
                _ => ""
            };
            var exit = recipe is GitReadRecipe.SparseCheckoutConfig or GitReadRecipe.SparseIndexConfig or GitReadRecipe.WorktreeConfig or GitReadRecipe.BareConfig or GitReadRecipe.VerifyHeadRef ? 1 :
                recipe == GitReadRecipe.VerifyHead && Unborn ? 128 : 0;
            return ValueTask.FromResult(new GitReadResult("none", exit, Encoding.UTF8.GetBytes(text)));
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}