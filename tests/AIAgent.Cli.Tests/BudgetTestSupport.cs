using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

internal sealed class BudgetFixture : IAsyncDisposable
{
    internal string Root { get; } = WorkspaceFixtures.NewDirectory();
    internal BootstrapSession Session { get; }
    internal EditApproval Approval { get; } = new();
    internal EditFileSystem FileSystem { get; } = new();
    internal WorkspaceFiles Files { get; }
    internal ToolRegistry Registry { get; }
    internal GeminiHandler Handler { get; } = new();
    internal GeminiClient Client { get; }
    internal UsageAccumulator Usage { get; } = new();
    internal List<AgentEvent> Events { get; } = [];
    internal string Instruction { get; }
    internal BudgetFixture(string instruction = "test instruction")
    {
        Instruction = instruction;
        Session = WorkspaceFixtures.Session(Root);
        Files = new(Session.Workspace, Session.Policy, FileSystem, Approval);
        Registry = TestRulesRegistry.Create([new ListFilesTool(Files, Session.Policy), new ReadFileTool(Files, Session.Policy),
            new CreateFileTool(Files, Session.Policy), new ReplaceTextTool(Files, Session.Policy)], Session.Policy);
        Client = new(Session, Handler);
    }
    internal Task<AgentTurnResult> Run(CancellationToken token = default, IReadOnlyList<LlmMessage>? messages = null) =>
        new AgentRunner(Client, Registry, Session.Policy, Instruction, Usage, Events.Add).RunAsync(messages ?? [LlmMessage.FromUserText("question")], token);
    internal Task<AgentTurnResult> RunWith(ILlmClient client, CancellationToken token = default) =>
        new AgentRunner(client, Registry, Session.Policy, Instruction, Usage, Events.Add).RunAsync([LlmMessage.FromUserText("question")], token);
    internal static LlmToolCall Call(string name, object args) => new(0, null, name, JsonSerializer.SerializeToElement(args));
    internal static string Calls(string name, object args, string? usage = null) => GeminiFixtures.Response(JsonSerializer.Serialize(new object[]
    {
        new { text = "preface" }, new { functionCall = new { name, args, id = "local-call" }, thoughtSignature = "c2ln" }
    }), usage: usage);
    public async ValueTask DisposeAsync() { await Files.DisposeAsync(); await Client.DisposeAsync(); Session.Dispose(); }
}