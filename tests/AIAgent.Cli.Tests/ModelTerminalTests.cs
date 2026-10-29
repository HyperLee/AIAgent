using System.Text;

using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class ModelTerminalTests
{
    [TestMethod]
    public async Task MODELAC003_BootstrapShowsProviderAndFixedDestination()
    {
        var terminal = new FakeTerminal(); terminal.Text("n"); terminal.Enter();
        Assert.IsFalse(await new ConsoleInteraction(terminal).ConfirmWorkspaceAsync(new("/workspace"), new("gemini-3.6-flash"), default));
        Assert.IsTrue(terminal.Output.Contains("Provider：gemini", StringComparison.Ordinal));
        Assert.IsTrue(terminal.Output.Contains("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MODELAC005_PreviewLimitAndOtherTextDeclinesWithoutRetry()
    {
        foreach (var length in new[] { 65536, 65537 })
        {
            var terminal = new FakeTerminal(); terminal.Text("yes"); terminal.Enter();
            using var session = GeminiFixtures.Session();
            using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
            var result = await interaction.ConfirmModelAsync(new(new string('x', length)), default);
            Assert.AreEqual(length == 65536 ? ModelApprovalDecision.Approved : ModelApprovalDecision.Unavailable, result);
            Assert.IsTrue(length == 65536 ? Encoding.UTF8.GetByteCount(terminal.Output) >= length : terminal.Output.Length == 0);
        }
        var other = new FakeTerminal(); other.Text("anything else"); other.Enter();
        using var initial = GeminiFixtures.Session(); using var reader = new ConsoleChatInteraction(other); reader.SetPolicy(initial.Policy);
        Assert.AreEqual(ModelApprovalDecision.Declined, await reader.ConfirmModelAsync(new("safe preview"), default));
    }

    [TestMethod]
    public async Task MODELAC010_HiddenKeyGapDoesNotUseDisposedPolicy()
    {
        var terminal = new FakeTerminal(); terminal.Text("NEW_SYNTHETIC_INPUT"); terminal.Enter();
        using var session = GeminiFixtures.Session();
        using var interaction = new ConsoleChatInteraction(terminal); interaction.SetPolicy(session.Policy);
        Assert.IsTrue(interaction.PrepareModelKeyInput());
        interaction.DetachPolicy(); session.Dispose();
        using var input = await interaction.ReadKeyAsync(default);
        Assert.AreEqual(KeyInputStatus.Accepted, input.Status);
        Assert.IsFalse(terminal.Output.Contains("NEW_SYNTHETIC_INPUT", StringComparison.Ordinal));
        Assert.ThrowsExactly<InvalidOperationException>(() => interaction.Show(new(ChatEventKind.Ready, "dynamic data")));
    }
}