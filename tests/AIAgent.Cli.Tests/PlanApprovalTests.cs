using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanApprovalTests
{
    [TestMethod]
    public async Task PLANAC003_ConfirmationUsesFreshHiddenChoicesAndOneReader()
    {
        foreach (var (input, expected) in new[] { ("y\r", PlanApprovalDecision.Approved), (" YeS \r", PlanApprovalDecision.Approved),
            ("n\r", PlanApprovalDecision.Declined), ("NO\r", PlanApprovalDecision.Declined), (" \r", PlanApprovalDecision.Declined),
            ("\u0003", PlanApprovalDecision.Cancelled), ("\u0004", PlanApprovalDecision.EndOfInput) })
        {
            using var f = new PlanFixture(); f.Submit();
            var terminal = new ApprovalTerminal(); terminal.Buffered("yes\r\n"); terminal.Bursts.Enqueue(input);
            using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(f.Session.Policy);
            Assert.AreEqual(expected, await interaction.ConfirmPlanAsync(f.Store.ApprovalPreview()!, default));
            Assert.AreEqual(1, terminal.MaximumReaders);
            Assert.IsFalse(terminal.Output.Contains("YeS", StringComparison.Ordinal));
            Assert.IsTrue(terminal.Output.Contains(PlanProjection.Prompt, StringComparison.Ordinal));
            Assert.AreEqual(PlanApprovalState.Draft, f.Store.Snapshot!.ApprovalState);
        }
    }

    [TestMethod]
    public async Task PLANAC004_BufferedYesAloneCannotApproveAndEofOutranksCancel()
    {
        foreach (var buffered in new[] { "yes\r", "\u0004\u0003" })
        {
            using var f = new PlanFixture(); f.Submit(); var terminal = new ApprovalTerminal(); terminal.Buffered(buffered);
            using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(f.Session.Policy);
            Assert.AreEqual(PlanApprovalDecision.EndOfInput, await interaction.ConfirmPlanAsync(f.Store.ApprovalPreview()!, default));
        }
    }

    [TestMethod]
    public async Task PLANAC004_InvalidConfirmationStringsAreNotCommands()
    {
        using var f = new PlanFixture(); f.Submit(); var terminal = new ApprovalTerminal();
        foreach (var input in new[] { "/clear\r", "/exit\r", "bad\t\r", "yes\r" }) terminal.Bursts.Enqueue(input);
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(f.Session.Policy);
        Assert.AreEqual(PlanApprovalDecision.Approved, await interaction.ConfirmPlanAsync(f.Store.ApprovalPreview()!, default));
        Assert.AreEqual(3, terminal.Output.Split("請輸入 y／yes 或 n／no。", StringSplitOptions.None).Length - 1);
        Assert.IsNotNull(f.Store.Snapshot); Assert.IsFalse(terminal.Output.Contains("/exit", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PLANAC004_StaleOrAlteredPreviewCannotApproveNewGenerationOrVersion()
    {
        using var f = new PlanFixture(); f.Submit(); var old = f.Store.ApprovalPreview()!;
        Assert.IsFalse(f.Store.Approve(new(old.PlanId, old.Revision, old.Generation, old.DisplayBlock + "altered")));
        f.Submit(1, "t1", title: "changed"); Assert.IsFalse(f.Store.Approve(old));
        var current = f.Store.ApprovalPreview()!; f.Store.Clear(); Assert.IsFalse(f.Store.Approve(current));
        f.Submit(); Assert.IsFalse(f.Store.Approve(old)); Assert.AreEqual(PlanApprovalState.Draft, f.Store.Snapshot!.ApprovalState);
    }

    [TestMethod]
    public void PLANAC005_FixedFieldsAndIdentifiersMatchingShortKeyAreNeverExempted()
    {
        foreach (var key in new[] { "plan_id", "t1", "p1", "Execute" })
        {
            using var session = WorkspaceFixtures.Session(key: key); var store = new PlanStore(session.Policy); store.SetMode(AgentMode.Plan);
            Assert.AreEqual("sensitive_content", store.Submit(PlanFixture.Submission()).ErrorCode);
            Assert.IsNull(store.Snapshot); Assert.IsNull(store.ApprovalPreview());
        }
    }
}