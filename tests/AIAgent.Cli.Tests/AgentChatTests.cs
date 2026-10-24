using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class AgentChatTests
{
    [TestMethod]
    public async Task LOOPAC007_RealChatSdkAndWorkspaceListReadThenAnswer()
    {
        var root = WorkspaceFixtures.NewDirectory();
        var bytes = new byte[] { 239, 187, 191 }.Concat(Encoding.UTF8.GetBytes("Hello\n")).ToArray();
        await File.WriteAllBytesAsync(Path.Combine(root, "notes.md"), bytes);
        var interaction = new ScriptInteraction();
        foreach (var text in new[] { "read notes", "/clear", "/exit" }) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls switch
        {
            1 => GeminiFixtures.Response("[{\"text\":\"private\",\"thought\":true},{\"functionCall\":{\"name\":\"list_files\",\"args\":{\"path\":\".\"}},\"thoughtSignature\":\"c2ln\"}]"),
            2 => GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"read_file\",\"id\":\"read-id\",\"args\":{\"path\":\"notes.md\"}}}]"),
            _ => GeminiFixtures.Text("final answer")
        }));
        var usage = new UsageAccumulator();
        var result = await new ChatApplication(interaction, new SdkChatFactory(handler), new(), usage,
            "test instruction", new WorkspaceFilesFactory()).RunAsync(WorkspaceFixtures.Session(root), default);
        Assert.IsTrue(result == 0 && handler.Calls == 3 && usage.Snapshot().DispatchedRequests == 3, "LOOP-AC-007");
        using var final = JsonDocument.Parse(handler.Bodies[2]);
        var declarations = final.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations");
        Assert.AreEqual(9, declarations.GetArrayLength(), "LOOP-AC-003");
        for (var index = 0; index < 2; index++)
        {
            var declaration = declarations[index];
            Assert.AreEqual(index == 0 ? "list_files" : "read_file", declaration.GetProperty("name").GetString(), "LOOP-AC-003");
            Assert.IsFalse(declaration.TryGetProperty("parameters", out _), "LOOP-AC-003");
            var schema = declaration.GetProperty("parametersJsonSchema");
            Assert.IsTrue(schema.GetProperty("required")[0].GetString() == "path" && !schema.GetProperty("additionalProperties").GetBoolean() &&
                schema.GetProperty("properties").GetProperty("path").GetProperty("type").GetString() == "string", "LOOP-AC-003");
        }
        var contents = final.RootElement.GetProperty("contents");
        Assert.AreEqual(5, contents.GetArrayLength(), "LOOP-AC-009");
        var data = contents[4].GetProperty("parts")[0].GetProperty("functionResponse").GetProperty("response").GetProperty("data");
        Assert.IsTrue(data.GetProperty("content").GetString() == "Hello\n" && data.GetProperty("byte_count").GetInt32() == 9 && data.GetProperty("has_utf8_bom").GetBoolean(), "LOOP-AC-007");
        Assert.IsTrue(contents[1].GetProperty("parts")[1].GetProperty("thoughtSignature").GetString() == "c2ln", "LOOP-AC-009");
        Assert.IsTrue((await File.ReadAllBytesAsync(Path.Combine(root, "notes.md"))).SequenceEqual(bytes), "LOOP-AC-035");
        Assert.AreEqual(1, interaction.Events.Count(e => e.Kind == ChatEventKind.Answer), "LOOP-AC-007");
        Assert.IsFalse(interaction.Events.Any(e => e.Text == "private"), "LOOP-AC-029");
    }
    [TestMethod]
    public async Task LOOPAC034_ActualSdkBodyBoundaryIncludesToolsHistorySignatureAndRealRead()
    {
        var root = WorkspaceFixtures.NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "中文\"\\\nHello");
        async Task<(int Bytes, int Calls, AgentTurnResult Result)> Run(int padding)
        {
            using var session = WorkspaceFixtures.Session(root);
            await using var workspace = new WorkspaceFilesFactory().Create(session);
            var registry = new Tools.ToolRegistry([new Tools.ListFilesTool(workspace, session.Policy), new Tools.ReadFileTool(workspace, session.Policy)], session.Policy);
            var handler = new GeminiHandler();
            handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls switch
            {
                1 => GeminiFixtures.Response("[{\"text\":\"old\",\"thoughtSignature\":\"c2ln\"}]"),
                2 => GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"read_file\",\"args\":{\"path\":\"notes.md\"}},\"thoughtSignature\":\"c2ln\"}]"),
                _ => GeminiFixtures.Text()
            }));
            await using var client = new GeminiClient(session, handler);
            var old = await client.GenerateAsync(new("instruction", [LlmMessage.FromUserText("old")], registry.Definitions), default);
            var result = await new AgentRunner(client, registry, session.Policy, new string('x', padding), new(), _ => { })
                .RunAsync([LlmMessage.FromUserText("old"), old.ModelMessage!, LlmMessage.FromUserText("read")], default);
            return (handler.Bodies[^1].Length, handler.Calls, result);
        }
        var calibration = await Run(1);
        var padding = 262144 - calibration.Bytes + 1;
        var allowed = await Run(padding);
        Assert.IsTrue(allowed.Bytes == 262144 && allowed.Calls == 3 && allowed.Result.Status == AgentTurnStatus.Completed, "LOOP-AC-034");
        var rejected = await Run(padding + 1);
        Assert.IsTrue(rejected.Calls == 2 && rejected.Result.ModelErrorCode == LlmErrorCode.InputTooLarge &&
            rejected.Result.GenerationsStarted == 2 && rejected.Result.TurnUsage.DispatchedRequests == 1 &&
            rejected.Result.Operations.Single().Status == Tools.ToolStatus.Success && rejected.Result.CompletedTurn.Count == 0, "LOOP-AC-034");
    }

    [TestMethod]
    public async Task LOOPAC026_RealSdkSensitiveToolCallDoesNotReachRealWorkspace()
    {
        var root = WorkspaceFixtures.NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), "safe");
        using var session = WorkspaceFixtures.Session(root);
        await using var workspace = new WorkspaceFilesFactory().Create(session);
        var registry = new Tools.ToolRegistry([new Tools.ReadFileTool(workspace, session.Policy)], session.Policy);
        var handler = new GeminiHandler
        {
            Respond = (_, _) => Task.FromResult(GeminiHandler.Json(
            GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"read_file\",\"id\":\"" + GeminiFixtures.Key + "\",\"args\":{\"path\":\"notes.md\"}}}]", usage: "{\"totalTokenCount\":17}")))
        };
        await using var client = new GeminiClient(session, handler);
        var result = await new AgentRunner(client, registry, session.Policy, "instruction", new(), _ => { })
            .RunAsync([LlmMessage.FromUserText("read")], default);
        Assert.IsTrue(result.ModelErrorCode == LlmErrorCode.SensitiveOutput && result.RequiresContextReset && result.Operations.Count == 0 &&
            workspace.Snapshots.Count == 0 && result.TurnUsage.Total.KnownTotal == 17 && handler.Calls == 1, "LOOP-AC-026");
    }

    [TestMethod]
    public async Task LOOPAC027_SensitiveFileStopsChainAndKeepsPriorSuccessfulHistory()
    {
        var root = WorkspaceFixtures.NewDirectory();
        await File.WriteAllTextAsync(Path.Combine(root, "notes.md"), GeminiFixtures.Key);
        var interaction = new ScriptInteraction();
        foreach (var text in new[] { "one", "read", "next", "/exit" }) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(text));
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 2
            ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"read_file\",\"args\":{\"path\":\"notes.md\"}}},{\"functionCall\":{\"name\":\"list_files\",\"args\":{\"path\":\".\"}}}]")
            : GeminiFixtures.Text()));
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "instruction", new WorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default), "LOOP-AC-027");
        using var next = JsonDocument.Parse(handler.Bodies[2]);
        Assert.IsTrue(handler.Calls == 3 && next.RootElement.GetProperty("contents").GetArrayLength() == 3, "LOOP-AC-027");
        Assert.IsFalse(interaction.Events.Any(e => e.Kind == ChatEventKind.ContextCleared || e.Text?.Contains(GeminiFixtures.Key, StringComparison.Ordinal) == true), "LOOP-AC-027");
    }

}