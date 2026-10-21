using AIAgent.Cli.Cli;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class InteractionTests
{
    [TestMethod]
    public async Task AC006_ConfirmationOnlyAcceptsYesAndDisplaysRequiredNotice()
    {
        foreach (var answer in new[] { "y", "Y", " yes ", "YeS", "", " ", "n", "true", "y extra" })
        {
            var terminal = new FakeTerminal(); terminal.Text(answer); terminal.Enter();
            var interaction = new ConsoleInteraction(terminal);
            interaction.EnsureAvailable();
            var accepted = await interaction.ConfirmWorkspaceAsync(new("/actual workspace"), new(null), default);
            Assert.IsTrue(accepted == (answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)), "AC-006");
            Assert.IsTrue(terminal.Output.Contains("/actual workspace", StringComparison.Ordinal) && terminal.Output.Contains("尚未設定模型", StringComparison.Ordinal) && terminal.Output.Contains("本機聊天與選取的檔案內容可能傳送至 Gemini；支援受限列檔與 UTF-8 讀檔", StringComparison.Ordinal) && terminal.Output.Contains("使用此工作區？[y/N]", StringComparison.Ordinal), "AC-006");
            Assert.IsTrue(terminal.ScopeCount == 0, "AC-009");
        }
    }

    [TestMethod]
    public async Task AC006_ConfirmationEofAndControlCRemainDistinct()
    {
        var terminal = new FakeTerminal(); var interaction = new ConsoleInteraction(terminal);
        try { await interaction.ConfirmWorkspaceAsync(new("/root"), new("test-model"), default); Assert.Fail("AC-006"); } catch (BootstrapEndOfInputException) { }
        Assert.IsTrue(terminal.ScopeCount == 0 && terminal.Output.Contains("test-model", StringComparison.Ordinal), "AC-006");
        terminal.Add(new ConsoleKeyInfo('\u0003', ConsoleKey.C, false, false, true));
        try { await interaction.ConfirmWorkspaceAsync(new("/root"), new(null), default); Assert.Fail("AC-006"); } catch (OperationCanceledException) { }
        Assert.IsTrue(terminal.ScopeCount == 0, "AC-009");
    }

    [TestMethod]
    public void AC009_CapabilityFailurePrecedesInteractionAndStatusIsRedacted()
    {
        var terminal = new FakeTerminal { IsInteractive = false };
        var interaction = new ConsoleInteraction(terminal);
        try { interaction.EnsureAvailable(); Assert.Fail("AC-009"); } catch (BootstrapFailureException ex) { Assert.IsTrue(ex.Code == BootstrapErrorCode.TerminalUnavailable, "AC-009"); }
        Assert.IsTrue(terminal.Output.Length == 0 && terminal.ReadCount == 0, "AC-009");
        using var secrets = new SessionSecrets(SecurityTests.FakeKey.ToCharArray());
        interaction.Policy = new SensitiveDataPolicy(secrets);
        interaction.ShowStatus("before" + SecurityTests.FakeKey + "after");
        Assert.IsFalse(terminal.Output.Contains(SecurityTests.FakeKey, StringComparison.Ordinal), "AC-012");
    }

    [TestMethod]
    public async Task AC012_ReenteredKeyPromptUsesExistingSessionPolicy()
    {
        var terminal = new FakeTerminal(); terminal.Text(SecurityTests.FakeKey); terminal.Enter();
        using var oldSecrets = new SessionSecrets("API".ToCharArray());
        var interaction = new ConsoleInteraction(terminal) { Policy = new SensitiveDataPolicy(oldSecrets) };
        using var input = await interaction.ReadKeyAsync(default);
        Assert.IsTrue(input.Status == KeyInputStatus.Accepted && !terminal.Output.Contains("API", StringComparison.Ordinal), "AC-012");
    }
}