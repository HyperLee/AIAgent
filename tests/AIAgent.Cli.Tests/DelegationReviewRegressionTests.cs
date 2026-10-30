using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DelegationReviewRegressionTests
{
    [TestMethod]
    public async Task MULTIAC029_CancelCannotMaskDisposalThatOutlivesCleanupWindow()
    {
        await using var f = await DelegationTestFixture.Create(); using var cancellation = new CancellationTokenSource();
        DelayedDelegationDisposal? delayed = null; f.Factory.Decorate = (_, client) => delayed = new(client);
        var run = f.Run(1, cancellation.Token); await delayed!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancellation.Cancel(); f.Clock.Advance(10);
        try
        {
            var result = await run.WaitAsync(TimeSpan.FromSeconds(3)); Assert.IsTrue(result.Fatal); Assert.AreEqual("delegation_cleanup_failed", result.ErrorCode);
        }
        finally { delayed.Release.TrySetResult(); await delayed.Finished.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
    [TestMethod]
    [DataRow(1, "process_cleanup_failed")]
    [DataRow(2, "process_unconfirmed")]
    [DataRow(3, "process_cleanup_failed")]
    public async Task MULTIAC029_GitFatalDuringToolOrEitherRevalidationStaysFatal(int validation, string error)
    {
        await using var f = await DelegationTestFixture.Create(); f.Git.ValidationResult = n => n == validation ? error : "none";
        f.Factory.Respond = (_, request, _) => Task.FromResult(request.Messages.Count == 1 ? RunnerFixture.Calls(new LlmToolCall(0, null, "git_diff", GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}"))) : ChatFixture.Completed(GitChatTests.Report("g1", "all")));
        var result = await f.Run(1, role: DelegationRole.Review);
        Assert.IsTrue(result.Fatal, result.ErrorCode + "|" + string.Join(",", result.Children.Select(c => c.ErrorCode)) + "|validations=" + f.Git.Validations + "|clients=" + f.Factory.Clients.Count); Assert.AreEqual("delegation_cleanup_failed", result.ErrorCode); Assert.IsNull(result.Data);
    }
    [TestMethod]
    public async Task MULTIAC029_SimultaneousCancelCannotMaskStoredGitFatal()
    {
        await using var f = await DelegationTestFixture.Create(); using var cancellation = new CancellationTokenSource();
        f.Git.ValidationResult = _ => { cancellation.Cancel(); return "process_cleanup_failed"; };
        f.Factory.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, null, "git_diff", GeminiFixtures.Element("{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}"))));
        var result = await f.Run(1, cancellation.Token, DelegationRole.Review); Assert.IsTrue(result.Fatal, result.ErrorCode + "|" + string.Join(",", result.Children.Select(c => c.ErrorCode)) + "|validations=" + f.Git.Validations); Assert.AreEqual("delegation_cleanup_failed", result.ErrorCode);
    }
    [TestMethod]
    public async Task MULTIAC027_DeadlineInsideWorkspaceReadProducesTimedOutInsteadOfFailedNone()
    {
        await using var f = await DelegationTestFixture.Create(); f.FileSystem.File("a.cs", [65]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.FileSystem.OnRead = token => { entered.TrySetResult(); return Task.Delay(Timeout.Infinite, token); };
        f.Factory.Respond = (_, _, _) => Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, null, "read_file", GeminiFixtures.Element("{\"path\":\"a.cs\"}"))));
        var run = f.Run(1); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); f.Clock.Advance(120);
        var result = await run.WaitAsync(TimeSpan.FromSeconds(3)); Assert.AreEqual("timed_out", result.Data!.Value.GetProperty("state").GetString());
        Assert.AreEqual(DelegationTaskState.TimedOut, result.Children[0].State); Assert.AreEqual("delegation_timed_out", result.Children[0].ErrorCode);
    }
    [TestMethod]
    public async Task MULTIAC026_SensitiveContentAtCandidateRevalidationRequiresSharedReset()
    {
        await using var f = await DelegationTestFixture.Create(); f.FileSystem.File("a.cs", Encoding.UTF8.GetBytes(new string('x', GeminiFixtures.Key.Length)));
        f.Factory.Respond = (_, request, _) =>
        {
            if (request.Messages.Count == 1) return Task.FromResult(RunnerFixture.Calls(new LlmToolCall(0, null, "read_file", GeminiFixtures.Element("{\"path\":\"a.cs\"}"))));
            f.FileSystem.Nodes["/ws/a.cs"].Bytes = Encoding.UTF8.GetBytes(GeminiFixtures.Key);
            return Task.FromResult(ChatFixture.Completed("""{"version":1,"summary":"Observed source.","observations":[{"path":"a.cs","start_line":1,"end_line":1,"note":"Provided line."}],"limitations":[]}"""));
        };
        var result = await f.Run(1); Assert.IsTrue(result.RequiresContextReset); Assert.IsNull(result.Data);
    }
    [TestMethod]
    public async Task MULTIAC030_RejectedNewTurnDoesNotReusePreviousCancelledBatchOutcome()
    {
        await using var f = await DelegationTestFixture.Create(); using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Factory.Respond = async (_, _, token) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return ChatFixture.Completed(); };
        var parent = new ScriptClient { Session = f.Session, Inspect = _ => new(ContextMeasureStatus.Measured, 1024) };
        parent.Respond = (i, _, _) => Task.FromResult(i == 0 ? RunnerFixture.Calls(new LlmToolCall(0, null, "delegate_readonly", GeminiFixtures.Element("{\"tasks\":[{\"role\":\"explore\",\"objective\":\"inspect\",\"scope_path\":\".\",\"acceptance\":\"cite\"}]}"))) : i == 1 ? RunnerFixture.Calls(new LlmToolCall(0, null, "delegate_readonly", GeminiFixtures.Element("{}"))) : ChatFixture.Completed());
        var registry = new ToolRegistry([new DelegateReadonlyTool(f.Coordinator, f.Session.Policy)], f.Session.Policy, f.Execution);
        var runner = new AgentRunner(parent, registry, f.Session.Policy, "synthetic instruction", new(), _ => { }, f.Budget, f.Contexts, parent);
        var first = runner.RunAsync([LlmMessage.FromUserText("first")], cancellation.Token); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancellation.Cancel();
        Assert.AreEqual(AgentTurnStatus.Cancelled, (await first).Status);
        var second = await runner.RunAsync([LlmMessage.FromUserText("second")], default);
        Assert.AreEqual(AgentTurnStatus.Completed, second.Status); Assert.AreEqual(3, parent.Requests.Count); Assert.AreEqual(0, parent.Requests[^1].Tools.Count);
        await parent.DisposeAsync();
    }
    [TestMethod]
    public async Task MULTIAC029_BlockedEventProjectionNeverHoldsDecisionGateAndHasBoundedCleanup()
    {
        await using var f = await DelegationTestFixture.Create(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.OnEvent = _ => { entered.TrySetResult(); release.Task.GetAwaiter().GetResult(); };
        var run = Task.Run(() => f.Run(1));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Run(() => { lock (f.Contexts.DecisionGate) { } }).WaitAsync(TimeSpan.FromSeconds(3));
            await f.Clock.CleanupCreated.Task.WaitAsync(TimeSpan.FromSeconds(3)); f.Clock.Advance(10);
            Assert.IsTrue((await run.WaitAsync(TimeSpan.FromSeconds(3))).Fatal);
        }
        finally { release.TrySetResult(); await run.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
    [TestMethod]
    public async Task MULTIAC033_EnableVersionDoesNotRetainCompactedNativeHistory()
    {
        await using var f = await DelegationTestFixture.Create(); var weak = AddAndCompact(f);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.IsFalse(weak.IsAlive); Assert.AreEqual(DelegationMode.Enabled, f.Coordinator.Mode);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AddAndCompact(DelegationTestFixture f)
    {
        var payload = new object(); f.RawContext.CommitCompletedTurn([LlmMessage.FromUserText("old"), LlmMessage.FromModel(payload)]);
        f.Coordinator.SetEnabled(false); f.Coordinator.SetEnabled(true);
        f.RawContext.Publish(new(1, 1, ["goal"], [], [], []), 1); return new(payload);
    }
    [TestMethod]
    public void MULTIAC025_FindingDedupUsesDecodedOrdinalValues()
    {
        using var a = JsonDocument.Parse("""{"snapshot_id":"g1","findings":[{"severity":"P2","title":"a","body":"issue","path":"a.cs","layer":"staged","side":"new","start_line":1,"end_line":1}]}""");
        using var b = JsonDocument.Parse("""{"snapshot_id":"g1","findings":[{"severity":"P2","title":"\u0061","body":"issue","path":"a.cs","layer":"staged","side":"new","start_line":1,"end_line":1}]}""");
        var task = new DelegationTask(DelegationRole.Review, "inspect", ".", "cite"); var coverage = DelegationJson.Coverage(DelegationRole.Review);
        var result = DelegationReports.Batch("d1", [new("a1", task, DelegationTaskState.Completed, "none", a.RootElement, coverage), new("a2", task, DelegationTaskState.Completed, "none", b.RootElement, coverage)]);
        Assert.AreEqual(1, result.GetProperty("merged_findings").GetArrayLength());
    }
}
internal sealed class DelayedDelegationDisposal(ScriptClient inner) : ILlmClient
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<LlmResult> GenerateAsync(LlmRequest request, CancellationToken token) => inner.GenerateAsync(request, token);
    public async ValueTask DisposeAsync() { Entered.TrySetResult(); await Release.Task; await inner.DisposeAsync(); Finished.TrySetResult(); }
}