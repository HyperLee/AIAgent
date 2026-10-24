using AIAgent.Cli.Cli;
using AIAgent.Cli.Processes;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildApprovalTests
{
    [TestMethod]
    public async Task BUILDAC007_ProcessConsentRequiresFreshHiddenInputAndKeepsOneReader()
    {
        foreach (var (input, expected) in new[] { ("y\r", ProcessApprovalDecision.Approved), (" YeS \r", ProcessApprovalDecision.Approved),
            ("n\r", ProcessApprovalDecision.Declined), (" \r", ProcessApprovalDecision.Declined),
            ("\u0003", ProcessApprovalDecision.Cancelled), ("\u0004", ProcessApprovalDecision.EndOfInput) })
        {
            using var session = WorkspaceFixtures.Session();
            var terminal = new ApprovalTerminal(); terminal.Buffered("yes\r\n"); terminal.Bursts.Enqueue(input);
            using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var preview = new ProcessApprovalPreview("完整程序預覽\n確認此單次受控 .NET 任務？[y/N] ", new object(), 0, null, "once");
            Assert.AreEqual(expected, await interaction.ConfirmProcessAsync(preview, default));
            Assert.AreEqual(1, terminal.MaximumReaders);
            Assert.IsFalse(terminal.Output.Contains("YeS", StringComparison.Ordinal));
        }
    }
    [TestMethod]
    public async Task BUILDAC006_PrePastedYesAndIncompleteOrUnsafePreviewNeverAuthorize()
    {
        using var session = WorkspaceFixtures.Session();
        foreach (var text in new[] { "preview\n", new string('x', 65537), GeminiFixtures.Key })
        {
            var terminal = new ApprovalTerminal(); terminal.Buffered("yes\r");
            using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var preview = new ProcessApprovalPreview(text, new object(), 0, null, "once");
            Assert.AreNotEqual(ProcessApprovalDecision.Approved, await interaction.ConfirmProcessAsync(preview, default));
        }
    }
}