using System.Reflection;
using System.Text;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SkillsLifecycleTests
{
    [TestMethod]
    public async Task SKILLAC025_DisposeWaitsForActualReadClosureAndRejectsLatePublication()
    {
        await using var f = new SkillsFixture(); f.Add("a"); await f.Rules.TryReloadAsync(default);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Fs.OnRead = async _ => { entered.TrySetResult(); await release.Task; };
        var selection = f.Skills.TryUseAsync("a", default); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var disposal = f.Skills.DisposeAsync().AsTask(); Assert.IsFalse(disposal.IsCompleted);
        release.TrySetResult(); await Assert.ThrowsAsync<OperationCanceledException>(async () => await selection); await disposal;
        Assert.IsTrue(f.Fs.Handles.All(h => h.Disposed)); Assert.IsNull(f.Skills.Capture().Snapshot);
        Assert.AreEqual("skills_unavailable", (await f.Skills.TryUseAsync("a", default)).ErrorCode);
    }

    [TestMethod]
    public async Task SKILLAC024_ClearDuringInitialRuleReadCannotRebaseSkillCandidate()
    {
        await using var f = new SkillsFixture(); f.Add("a"); f.Fs.File("AGENTS.md", Encoding.UTF8.GetBytes("rule"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Fs.OnRead = async _ => { entered.TrySetResult(); await release.Task; };
        var selection = f.Skills.TryUseAsync("a", default); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        f.Skills.Clear(); release.TrySetResult();
        Assert.AreEqual("skills_changed", (await selection).ErrorCode); Assert.IsNull(f.Skills.Capture().Snapshot);
    }

    [TestMethod]
    public async Task SKILLAC025_CancelBeforeAndDuringReadKeepsOldVersionAndLease()
    {
        await using var f = new SkillsFixture(); f.Add("a"); await f.Skills.TryUseAsync("a", default); var original = f.Skills.Capture();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var opened = f.Fs.Opened;
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await f.Skills.TryReloadAsync(cancellation.Token)); Assert.AreEqual(opened, f.Fs.Opened);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Fs.OnRead = async _ => { entered.TrySetResult(); await release.Task; };
        using var pendingCancellation = new CancellationTokenSource(); var read = f.Skills.TryReloadAsync(pendingCancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); pendingCancellation.Cancel(); Assert.IsFalse(read.IsCompleted);
        Assert.IsFalse(f.Execution.WorkspaceOperations.TryAcquire(default, out _)); release.TrySetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await read); Assert.IsTrue(f.Skills.IsCurrent(original));
        Assert.IsTrue(f.Execution.WorkspaceOperations.TryAcquire(default, out var lease)); lease!.Dispose();
    }

    [TestMethod]
    public async Task SKILLAC016_FailedSecondCandidateAllowsExactlyOneOldSourceRecheck()
    {
        await using var f = new SkillsFixture(); f.Add("a"); f.Add("b"); await f.Skills.TryUseAsync("a", default);
        var baseline = f.Fs.Opened; var bReads = 0;
        f.Fs.OnOpen = path => { if (path.EndsWith("/b/SKILL.md", StringComparison.Ordinal) && ++bReads == 2) f.Fs.Nodes[path].Bytes[0] = 255; };
        var result = await f.Skills.TryUseAsync("b", default);
        Assert.AreEqual("invalid_utf8", result.ErrorCode); Assert.AreEqual(3, f.Fs.Opened - baseline); Assert.AreEqual(SkillsStatus.Valid, f.Skills.Capture().Status);
    }

    [TestMethod]
    public async Task SKILLAC017_StaleRestoredBytesAndClearsNeverReuseRevisions()
    {
        await using var f = new SkillsFixture(); f.Add("a"); await f.Skills.TryUseAsync("a", default); var original = f.Skills.Capture();
        f.Skills.MarkStale(); f.Skills.Settle(); await f.Skills.TryReloadAsync(default);
        Assert.AreEqual(2L, f.Skills.Capture().SkillsRevision); Assert.IsFalse(f.Skills.IsCurrent(original));
        f.Skills.Clear(); var cleared = f.Skills.Capture(); Assert.AreEqual(3L, cleared.SkillsRevision);
        Assert.AreEqual("none", (await f.Skills.TryUseAsync("a", default)).ErrorCode); Assert.AreEqual(4L, f.Skills.Capture().SkillsRevision);
    }

    [TestMethod]
    public async Task SKILLAC023_SameScopeRuleRevisionStalesButNoChangePreserves()
    {
        await using var f = new SkillsFixture(); f.Add("a"); f.Fs.File("AGENTS.md", [65]); await f.Skills.TryUseAsync("a", default);
        Assert.AreEqual("no_change", await f.Rules.TryReloadAsync(default)); Assert.AreEqual(SkillsStatus.Valid, f.Skills.Capture().Status);
        f.Fs.Nodes["/ws/AGENTS.md"].Bytes = [66]; Assert.AreEqual("none", await f.Rules.TryReloadAsync(default));
        Assert.AreEqual(SkillsStatus.Stale, f.Skills.Capture().Status); Assert.AreEqual("none", (await f.Skills.TryReloadAsync(default)).ErrorCode);
    }

    [TestMethod]
    public async Task SKILLAC017_OverflowFailsClosedWithoutWrapping()
    {
        await using var f = new SkillsFixture(); f.Add("a");
        typeof(SkillsCoordinator).GetField("_revision", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Skills, long.MaxValue);
        await Assert.ThrowsAsync<OverflowException>(async () => await f.Skills.TryUseAsync("a", default));
        Assert.AreEqual(long.MaxValue, f.Skills.Capture().SkillsRevision); Assert.AreEqual("skills_unavailable", await f.Skills.ValidateAsync(default)); Assert.IsNull(f.Skills.Capture().Snapshot);
    }

    [TestMethod]
    public async Task SKILLAC008_CatalogMembershipMutationPublishesNoPartialResult()
    {
        await using var f = new SkillsFixture(); f.Add("a");
        var scans = 0; f.Fs.OnEnumerate = _ => { if (++scans == 1) f.Add("b"); };
        var result = await f.Skills.ListAsync(default); Assert.AreEqual("path_changed", result.ErrorCode); Assert.IsNull(result.DisplayBlock);
    }

    [TestMethod]
    public async Task SKILLAC017_PlanGenerationOverflowStillCompletesOwnedOperationForDispose()
    {
        var f = new SkillsFixture(); f.Add("a"); await f.Rules.TryReloadAsync(default);
        typeof(PlanStore).GetField("_generation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Plans, long.MaxValue);
        try
        {
            await Assert.ThrowsAsync<OverflowException>(async () => await f.Skills.TryUseAsync("a", default));
            await f.Skills.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            // Keep a failing lifetime regression from leaving its fixture waiting forever.
            var pending = (HashSet<TaskCompletionSource>)typeof(SkillsCoordinator).GetField("_operations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Skills)!;
            foreach (var completion in pending) completion.TrySetResult();
            await f.DisposeAsync();
        }
    }
}