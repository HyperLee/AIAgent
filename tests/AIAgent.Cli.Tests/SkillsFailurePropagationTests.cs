using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SkillsFailurePropagationTests
{
    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task SKILLAC019_CheckpointCliConsumesSensitiveAndFatalSkillsSignalsAfterVerified(bool sensitive)
    {
        using var session = WorkspaceFixtures.Session(); var f = new ChatFixture("/checkpoint restore c1");
        f.Factory.Sessions.Add(session); f.Client.Session = session;
        f.Context.CommitCompletedTurn([LlmMessage.FromUserText("historical"), LlmMessage.FromModel(new object())]);
        var checkpoints = new CheckpointChatWorkspace(); var interaction = new CheckpointInteraction(f.Interaction);
        var factory = new CheckpointChatFactory(checkpoints, execution => checkpoints.AfterPublication = () =>
        { if (sensitive) execution.Skills!.MarkSensitive(); else execution.Skills!.MarkFatalFailure(); });
        if (sensitive) f.Interaction.Inputs.Enqueue(() => { Assert.AreEqual(0, f.Context.CompletedTurnCount); return ChatInputResult.Submitted("/exit"); });
        var app = new ChatApplication(interaction, f.Factory, f.Context, f.Usage, "fixture", factory); interaction.Application = app;
        Assert.AreEqual(sensitive ? 0 : 1, await app.RunAsync(session, default)); Assert.AreEqual(1, checkpoints.Publications);
        Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("Verified", StringComparison.Ordinal) == true));
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task SKILLAC026_ReviewCaptureCliConsumesSensitiveAndFatalSkillsSignals(bool sensitive)
    {
        using var session = WorkspaceFixtures.Session(); var f = new ChatFixture("/review"); f.Factory.Sessions.Add(session); f.Client.Session = session;
        f.Context.CommitCompletedTurn([LlmMessage.FromUserText("historical"), LlmMessage.FromModel(new object())]);
        if (sensitive) f.Interaction.Inputs.Enqueue(() => { Assert.AreEqual(0, f.Context.CompletedTurnCount); return ChatInputResult.Submitted("/exit"); });
        var app = new ChatApplication(f.Interaction, f.Factory, f.Context, f.Usage, "fixture", new TestWorkspaceFilesFactory(), gitReadWorkspaceFactory: new FaultGitFactory(sensitive));
        Assert.AreEqual(sensitive ? 0 : 1, await app.RunAsync(session, default)); Assert.AreEqual(0, f.Client.Requests.Count);
        if (sensitive) Assert.IsTrue(f.Interaction.Events.Any(e => e.Text?.Contains("sensitive_content", StringComparison.Ordinal) == true));
    }

    [DataRow(true, "sensitive_content")]
    [DataRow(false, "io_failure")]
    [TestMethod]
    public async Task SKILLAC026_RealGitCapturePreservesSevereSkillsClassification(bool sensitive, string expected)
    {
        using var session = WorkspaceFixtures.Session(); var fs = GitSnapshotTests.Files(); fs.File("a.cs", Encoding.UTF8.GetBytes("A\n"));
        fs.Directory("skills"); fs.Directory("skills/demo"); fs.File("skills/demo/SKILL.md", Encoding.UTF8.GetBytes(SkillsIntegrationTests.Source()));
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs); var skills = files.ExecutionPolicy.Skills!;
        await skills.TryUseAsync("demo", default);
        var adapter = new FaultSkillAdapter(fs, sensitive); await using var git = new GitReadWorkspace(files, () => adapter);
        var result = await git.CaptureAsync(default);
        Assert.AreEqual(expected, result.ErrorCode); Assert.IsNull(result.Snapshot);
        Assert.IsTrue(sensitive ? skills.RequiresContextReset : skills.FatalFailure);
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task SKILLAC019_RealRestoreRetainsVerifiedWhileSurfacingFatalSkillSettlement(bool sensitive)
    {
        var root = SkillsIntegrationTests.SkillRoot(); File.WriteAllText(root + "/notes.md", "before");
        using var session = WorkspaceFixtures.Session(root); var execution = new AgentExecutionPolicy(new(session.Policy));
        var fs = new RestoreFaultFileSystem(root + "/skills/demo/SKILL.md", sensitive);
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval(), execution);
        await execution.Skills!.TryUseAsync("demo", default); await files.ReadFileAsync("notes.md", default);
        Assert.AreEqual(FileWriteState.Verified, (await files.ReplaceTextAsync("notes.md", "before", "after", default)).WriteState);
        fs.Trigger = true;
        var result = await files.Checkpoints.RestoreAsync(1, new CheckpointTestApproval(), default);
        Assert.AreEqual(FileWriteState.Verified, result.WriteState); Assert.AreEqual("before", File.ReadAllText(root + "/notes.md"));
        Assert.AreEqual(1L, execution.Recovery.Revision);
        Assert.IsTrue(sensitive ? execution.Skills.RequiresContextReset : execution.Skills.FatalFailure);
        if (!sensitive) Assert.IsTrue(result.Fatal);
    }

    private sealed class FaultGitFactory(bool sensitive) : IGitReadWorkspaceFactory
    {
        public IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace) => new FaultGitWorkspace(workspace.ProjectRulesWorkspace!.ExecutionPolicy.Skills!, sensitive);
    }
    private sealed class FaultGitWorkspace(SkillsCoordinator skills, bool sensitive) : IGitReadWorkspace
    {
        public Task<GitCaptureResult> CaptureAsync(CancellationToken token)
        { if (sensitive) skills.MarkSensitive(); else skills.MarkFatalFailure(); return Task.FromResult(new GitCaptureResult("git_failed")); }
        public Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken token) => Task.FromResult("git_failed");
        public Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken token) => Task.FromResult("git_failed");
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FaultSkillAdapter(ControlledWorkspaceFileSystem fs, bool sensitive) : IGitReadAdapter
    {
        private readonly ScriptedGitAdapter _inner = new();
        private bool _changed;
        public string? ExecutableIdentity => _inner.ExecutableIdentity;
        public ValueTask<GitReadResult> RunAsync(GitReadRecipe recipe, GitReadRequest request, CancellationToken token)
        {
            if (recipe == GitReadRecipe.CheckIgnore)
                return ValueTask.FromResult(new GitReadResult("none", 0, Encoding.UTF8.GetBytes(string.Concat(request.Paths.Select(p => p.StartsWith("skills/", StringComparison.Ordinal)
                    ? ".gitignore\0" + "1\0skills/\0" + p + "\0" : "\0\0\0" + p + "\0"))), true));
            if (recipe == GitReadRecipe.Batch && !_changed)
            {
                _changed = true;
                if (sensitive) fs.File("skills/demo/SKILL.md", Encoding.UTF8.GetBytes(SkillsIntegrationTests.Source(GeminiFixtures.Key)));
                else fs.OnOpen = path => { if (path.EndsWith("/SKILL.md", StringComparison.Ordinal)) fs.FailDispose = true; };
            }
            return _inner.RunAsync(recipe, request, token);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RestoreFaultFileSystem(string skill, bool sensitive) : IWorkspaceWriteFileSystem
    {
        private readonly MacWorkspaceFileSystem _inner = new();
        private bool _failClose;
        internal bool Trigger { get; set; }
        public WorkspaceNodeState Inspect(string path) => _inner.Inspect(path);
        public bool HasExactName(string path) => _inner.HasExactName(path);
        public IEnumerable<string> EnumerateNames(string path) => _inner.EnumerateNames(path);
        public IWorkspaceReadFile OpenRead(string path) => new FaultFile(_inner.OpenRead(path), () => _failClose && path == skill);
        public UnixFileMode GetMode(string path) => _inner.GetMode(path);
        public IWorkspaceStagedFile CreateStagedFile(string path) => _inner.CreateStagedFile(path);
        public void PublishNew(string staged, string target) => _inner.PublishNew(staged, target);
        public void PublishReplacement(string staged, string target)
        {
            _inner.PublishReplacement(staged, target);
            if (!Trigger) return;
            if (sensitive) File.WriteAllText(skill, SkillsIntegrationTests.Source(GeminiFixtures.Key)); else _failClose = true;
        }
        public void DeleteOwnedStagedFile(string path) => _inner.DeleteOwnedStagedFile(path);
        private sealed class FaultFile(IWorkspaceReadFile inner, Func<bool> fail) : IWorkspaceReadFile
        {
            public Stream Stream => inner.Stream;
            public WorkspaceNodeState GetState() => inner.GetState();
            public UnixFileMode GetMode() => inner.GetMode();
            public async ValueTask DisposeAsync() { await inner.DisposeAsync(); if (fail()) throw new IOException("Synthetic close failure."); }
        }
    }
}