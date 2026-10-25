using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitReviewTests
{
    [TestMethod]
    public async Task GITAC014_DeliveryRequiresActualRequestDispatch()
    {
        using var session = WorkspaceFixtures.Session();
        await using var coordinator = new GitSnapshotCoordinator(new ReviewGitWorkspace(), session.Policy, NextId());
        Assert.AreEqual("none", await coordinator.CaptureAsync(default)); coordinator.Bind(GitSelection.Staged);
        var tool = new GitDiffTool(coordinator, session.Policy);
        var result = await tool.ExecuteAsync(GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}"), default);
        Assert.AreEqual(ToolStatus.Success, result.Status);
        Assert.AreEqual(0, coordinator.DeliveredChangeCount);
        coordinator.MarkDispatched(); Assert.AreEqual(1, coordinator.DeliveredChangeCount);
    }

    [TestMethod]
    public async Task GITAC015_ReportsRequireDeliveredChangedLineOnCorrectSide()
    {
        using var session = WorkspaceFixtures.Session();
        await using var coordinator = new GitSnapshotCoordinator(new ReviewGitWorkspace(), session.Policy, NextId());
        await coordinator.CaptureAsync(default); coordinator.Bind(GitSelection.Staged);
        var review = new CodeReviewCoordinator(coordinator, session.Policy);
        var json = "{\"version\":1,\"snapshot_id\":\"g1\",\"selection\":\"staged\",\"summary\":\"checked\",\"findings\":[{\"severity\":\"P2\",\"title\":\"bug\",\"body\":\"trigger and impact\",\"path\":\"a.cs\",\"layer\":\"staged\",\"side\":\"new\",\"start_line\":2,\"end_line\":2}]}";
        Assert.IsNull(review.Validate(json));
        await new GitDiffTool(coordinator, session.Policy).ExecuteAsync(GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}"), default);
        Assert.IsNull(review.Validate(json)); coordinator.MarkDispatched();
        Assert.IsNotNull(review.Validate(json));
        Assert.IsNull(review.Validate(json.Replace("\"start_line\":2,\"end_line\":2", "\"start_line\":1,\"end_line\":1", StringComparison.Ordinal)));
        Assert.IsNull(review.Validate(json.Replace("\"version\":1", "\"version\":1,\"approved\":true", StringComparison.Ordinal)));
        Assert.IsNull(review.Validate(json.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task GITAC013_FakeIdExtraFieldsAndNonCanonicalPathsNeverValidateSources()
    {
        using var session = WorkspaceFixtures.Session(); var reader = new ReviewGitWorkspace();
        await using var coordinator = new GitSnapshotCoordinator(reader, session.Policy, NextId()); await coordinator.CaptureAsync(default);
        foreach (var input in new[] { "{\"snapshot_id\":\"g0\",\"layer\":\"staged\",\"path\":\"a.cs\"}", "{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"./a.cs\"}", "{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\",\"argv\":\"bad\"}" })
        {
            var result = await new GitDiffTool(coordinator, session.Policy).ExecuteAsync(GeminiFixtures.Element(input), default);
            Assert.AreEqual(ToolStatus.Rejected, result.Status); Assert.AreEqual(JsonValueKind.Null, result.Response!.Value.GetProperty("data").ValueKind);
        }
        Assert.AreEqual(0, reader.Validations);
    }

    [TestMethod]
    public void GITAC012_ReviewCapabilityIntersectionBlocksDirectPlanMutations()
    {
        using var session = WorkspaceFixtures.Session(); var plans = new PlanStore(session.Policy); plans.SetMode(AgentMode.Plan);
        var execution = new AgentExecutionPolicy(plans);
        using (execution.EnterReview())
        {
            Assert.IsTrue(execution.Allows(ToolCapability.GitRead));
            Assert.IsFalse(execution.Allows(ToolCapability.WorkspaceRead));
            Assert.AreEqual("mode_restricted", plans.Submit(GeminiFixtures.Element("{}")).ErrorCode);
        }
        Assert.IsTrue(execution.Allows(ToolCapability.WorkspaceRead));
        Assert.AreEqual(AgentMode.Plan, plans.Mode);
    }

    [TestMethod]
    public async Task GITAC012_DirectRunnerReviewEntryEnforcesItsOwnCapabilityOverlay()
    {
        await using var fixture = new RunnerFixture();
        await using var snapshots = new GitSnapshotCoordinator(new ReviewGitWorkspace(), fixture.Session.Policy, NextId());
        await snapshots.CaptureAsync(default); snapshots.Bind(GitSelection.Staged);
        fixture.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(RunnerFixture.Read(0)));
        var result = await fixture.Runner.RunAsync([LlmMessage.FromUserText("review")], default, new(snapshots, fixture.Session.Policy));
        Assert.AreEqual(AgentStopReason.ModeRestricted, result.StopReason);
        Assert.AreEqual(0, fixture.FileSystem.Opened);
        Assert.IsFalse(fixture.Registry.ExecutionPolicy.Plans.ReviewTurnActive);
    }

    internal static Func<string> NextId() { long id = 0; return () => "g" + checked(++id); }
}

internal sealed class ReviewGitWorkspace : IGitReadWorkspace
{
    internal int Validations { get; private set; }
    internal string Error { get; set; } = "none";
    internal Func<int, string>? ValidationResult { get; set; }
    internal string SourcePath { get; set; } = "a.cs";
    internal int ChangeCount { get; set; } = 1;
    internal string OldText { get; set; } = "context\nold\n";
    internal string NewText { get; set; } = "context\nnew\n";
    public Task<GitCaptureResult> CaptureAsync(CancellationToken cancellationToken)
    {
        var changes = Enumerable.Range(0, ChangeCount).Select(i =>
        {
            var path = ChangeCount == 1 ? SourcePath : i + "_" + SourcePath;
            var old = new GitSource(path, GitLayer.Staged, GitSide.Old, Encoding.UTF8.GetBytes(OldText), "100644");
            var next = new GitSource(path, GitLayer.Staged, GitSide.New, Encoding.UTF8.GetBytes(NewText), "100644");
            return new GitChange(path, GitLayer.Staged, old, next);
        });
        var snapshot = new GitSnapshot("", ".", ScriptedGitAdapter.Old, "sha1", changes, [], new(".", []), new(0, 0, 0, ".", ProjectRulesStatus.Valid, null));
        return Task.FromResult(new GitCaptureResult(Error, snapshot));
    }
    public Task<string> ValidateAsync(GitSnapshot snapshot, CancellationToken cancellationToken) { Validations++; return Task.FromResult(ValidationResult?.Invoke(Validations) ?? Error); }
    public Task<string> ValidateAsync(GitValidationManifest manifest, CancellationToken cancellationToken) { Validations++; return Task.FromResult(Error); }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}