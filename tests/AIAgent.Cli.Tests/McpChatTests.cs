using AIAgent.Cli.Core;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpChatTests
{
    [TestMethod]
    public async Task MCPAC009_MultilineMcpCommandHasFixedRejectionAndNeverGenerates()
    {
        var fixture = new ChatFixture("/mcp connect learn\n/mcp tools");
        Assert.AreEqual(0, await fixture.Run()); Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("invalid_mcp_command", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task MCPAC009_DisconnectedStatusIsLocalAndNeverBecomesConversation()
    {
        var fixture = new ChatFixture("/mcp", "/mcp tools", "/mcp reload", "/mcp disconnect", "/Mcp", "/mcp connect  learn");
        Assert.AreEqual(0, await fixture.Run());
        Assert.AreEqual(0, fixture.Client.Requests.Count);
        Assert.AreEqual(0, fixture.Interaction.Remembered.Count);
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("Disconnected", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("mcp_not_connected", StringComparison.Ordinal) == true));
        Assert.IsTrue(fixture.Interaction.Events.Any(e => e.Text?.Contains("invalid_mcp_command", StringComparison.Ordinal) == true));
    }
}