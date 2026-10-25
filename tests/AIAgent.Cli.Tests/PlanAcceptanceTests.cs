using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class PlanAcceptanceTests
{
    [TestMethod]
    public async Task PLANAC001_LocalModeCommandsDoNotGenerateOrRecordUsage()
    {
        var interaction = new ScriptInteraction();
        foreach (var text in new[] { "/plan", "/tasks", "/clear", "/execute", "/exit" })
            interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        var client = new ScriptClient(); var usage = new UsageAccumulator();
        var session = WorkspaceFixtures.Session(); client.Session = session;
        var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        var result = await new ChatApplication(interaction, factory, new(), usage,
            "test instruction", new TestWorkspaceFilesFactory()).RunAsync(session, default);
        Assert.AreEqual(0, result);
        Assert.AreEqual(0, client.Requests.Count);
        Assert.AreEqual(0L, usage.Snapshot().DispatchedRequests);
        Assert.IsFalse(interaction.Events.Any(e => e.Kind == ChatEventKind.InputRejected), "PLAN-AC-001-local-commands");
        Assert.IsTrue(interaction.Events.Any(e => e.Text?.Contains("[Plan]", StringComparison.Ordinal) == true));
        Assert.IsTrue(interaction.Events.Any(e => e.Text?.Contains("[Execute]", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task PLANAC002_PlanAdvertisesOnlyReadToolsAndSubmitPlan()
    {
        var interaction = new ScriptInteraction();
        foreach (var text in new[] { "/plan", "explore", "/exit" })
            interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        var session = WorkspaceFixtures.Session(); var client = new ScriptClient { Session = session };
        var factory = new ScriptFactory(client); factory.Sessions.Add(session);
        await new ChatApplication(interaction, factory, new(), new(),
            "test instruction", new TestWorkspaceFilesFactory()).RunAsync(session, default);
        CollectionAssert.AreEqual(new[] { "list_files", "read_file", "search_files", "search_text", "read_file_range", "submit_plan" },
            client.Requests.Single().Tools.Select(t => t.Name).ToArray());
        Assert.IsTrue(client.Requests.Single().SystemInstruction.Contains("\"mode\":\"plan\"", StringComparison.Ordinal));
    }
}