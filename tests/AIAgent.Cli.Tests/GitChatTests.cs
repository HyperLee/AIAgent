using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitChatTests
{
    [TestMethod]
    public async Task GITAC012_ReviewUsesThreeToolsSameRunnerAndShowDoesNotGenerate()
    {
        var fixture = new ChatFixture("/review staged", "/review show", "/exit");
        var reader = new ReviewGitWorkspace();
        fixture.Client.Respond = (i, request, _) =>
        {
            CollectionAssert.AreEqual(new[] { "git_status", "git_diff", "read_git_file_range" }, request.Tools.Select(x => x.Name).ToArray());
            return Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "git_diff", GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}"))) : ChatFixture.Completed(Report("g1")));
        };
        Assert.AreEqual(0, await Run(fixture, reader));
        Assert.AreEqual(2, fixture.Client.Requests.Count);
        var answer = fixture.Interaction.Events.Single(x => x.Kind == ChatEventKind.Answer).Text!;
        StringAssert.Contains(answer, "完整差異提供：1/1");
        StringAssert.Contains(answer, "本回合未執行建置或測試");
        Assert.IsFalse(answer.StartsWith('{'));
        Assert.AreEqual(2L, fixture.Usage.Snapshot().DispatchedRequests);
    }

    [TestMethod]
    public async Task GITAC015_InvalidFinalJsonNeverEntersSuccessHistoryOrDisplay()
    {
        var fixture = new ChatFixture("/review", "/review show", "/exit");
        fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed("UNVALIDATED_REPORT_SENTINEL"));
        Assert.AreEqual(0, await Run(fixture, new()));
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Text?.Contains("UNVALIDATED_REPORT_SENTINEL", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.StopReason == AgentStopReason.ReviewInvalid));
    }

    [TestMethod]
    public async Task GITAC012_MixedForbiddenBatchExecutesNoGitOrWriteTools()
    {
        var fixture = new ChatFixture("/review", "/exit"); var reader = new ReviewGitWorkspace();
        fixture.Client.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(
            new(0, null, "git_diff", GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}")),
            new(1, null, "create_file", GeminiFixtures.Element("{\"path\":\"must-not-exist\",\"content\":\"bad\"}"))));
        Assert.AreEqual(0, await Run(fixture, reader));
        var operations = fixture.Interaction.Events.Where(e => e.Agent?.Operation is not null).Select(e => e.Agent!.Operation!).ToArray();
        Assert.AreEqual(2, operations.Length); Assert.AreEqual(ToolStatus.NotExecuted, operations[0].Status);
        Assert.AreEqual("mode_restricted", operations[1].ErrorCode);
        Assert.AreEqual(1, fixture.Client.Requests.Count);
    }

    [TestMethod]
    public async Task GITAC018_ClearDoesNotReuseSnapshotIdsAndFailedAttemptKeepsOldReport()
    {
        var fixture = new ChatFixture("/review", "/review", "/review show", "/clear", "/review", "/exit");
        fixture.Client.Respond = (i, _, _) => Task.FromResult(i == 1 ? ChatFixture.Completed("bad") : ChatFixture.Completed(Report(i == 0 ? "g1" : "g3", "all")));
        Assert.AreEqual(0, await Run(fixture, new()));
        Assert.AreEqual(3, fixture.Client.Requests.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("最近審查嘗試：review_invalid", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Where(e => e.Kind == ChatEventKind.Answer).Any(e => e.Text?.Contains("g3", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task GITAC012_ExactCommandsAndLocalAdmissionFailuresDoNotGenerate()
    {
        var fixture = new ChatFixture("/REVIEW", "/review staged extra", "/review show", "/review", "/exit");
        Assert.AreEqual(0, await Run(fixture, new() { Error = "not_git_repository" }));
        Assert.AreEqual(0, fixture.Client.Requests.Count);
    }

    [TestMethod]
    public async Task GITAC018_InterruptDuringCaptureCancelsOnlyTheReview()
    {
        var fixture = new ChatFixture("/review", "/exit");
        var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        var app = new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "test instruction", new TestWorkspaceFilesFactory(), gitReadWorkspaceFactory: new Factory(new()));
        fixture.Interaction.OnShow = message => { if (message.Text?.StartsWith("正在捕捉", StringComparison.Ordinal) == true) app.Interrupt(); };
        Assert.AreEqual(0, await app.RunAsync(session, default));
        Assert.AreEqual(0, fixture.Client.Requests.Count);
    }

    [TestMethod]
    public async Task GITAC018_SensitiveSourceAtFinalPublicationUsesCommonSafetyReset()
    {
        var fixture = new ChatFixture("/review", "/exit");
        fixture.Client.Respond = (_, _, _) => Task.FromResult(ChatFixture.Completed(Report("g1", "all")));
        var reader = new ReviewGitWorkspace { ValidationResult = count => count == 4 ? "sensitive_content" : "none" };
        Assert.AreEqual(0, await Run(fixture, reader));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.ContextCleared));
        Assert.IsFalse(fixture.Interaction.Events.Any(e => e.Kind == ChatEventKind.Answer));
    }

    [TestMethod]
    public async Task GITAC017_OversizedReviewAppendixStopsBeforeGeneration()
    {
        var fixture = new ChatFixture("/review", "/exit");
        Assert.AreEqual(0, await Run(fixture, new ReviewGitWorkspace { SourcePath = new string('a', 4092), ChangeCount = 8 }));
        Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.StopReason == AgentStopReason.ContextCapacity));
    }

    internal static string Report(string id, string selection = "staged") => "{\"version\":1,\"snapshot_id\":\"" + id + "\",\"selection\":\"" + selection + "\",\"summary\":\"provided differences examined\",\"findings\":[]}";
    private static Task<int> Run(ChatFixture fixture, ReviewGitWorkspace reader)
    {
        var session = GeminiFixtures.Session(); fixture.Factory.Sessions.Add(session); fixture.Client.Session = session;
        return new ChatApplication(fixture.Interaction, fixture.Factory, fixture.Context, fixture.Usage, "test instruction", new TestWorkspaceFilesFactory(), gitReadWorkspaceFactory: new Factory(reader)).RunAsync(session, default);
    }
    private sealed class Factory(ReviewGitWorkspace reader) : IGitReadWorkspaceFactory
    { public IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace) => reader; }
}