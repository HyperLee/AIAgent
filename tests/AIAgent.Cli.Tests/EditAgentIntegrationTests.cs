using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditAgentIntegrationTests
{
    [TestMethod]
    public async Task EDITAC032_RealSdkReadAndReplaceInSameBatchKeepPairing()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(Path.Combine(root, "a"), "draft\r\n");
        var interaction = new EditChatInteraction(); var context = new ConversationContext();
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("edit"));
        interaction.Inner.Inputs.Enqueue(() => { Assert.AreEqual(1, context.CompletedTurnCount, "EDIT-AC-032-commit"); return ChatInputResult.Submitted("/exit"); });
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1 ? GeminiFixtures.Response(
            "[{\"functionCall\":{\"name\":\"read_file\",\"args\":{\"path\":\"a\"}},\"thoughtSignature\":\"c2ln\"},{\"functionCall\":{\"name\":\"replace_text\",\"args\":{\"path\":\"a\",\"old_text\":\"draft\",\"new_text\":\"final\"}}}]") : GeminiFixtures.Text()));
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), context, new(), "instruction", new TestWorkspaceFilesFactory()).RunAsync(WorkspaceFixtures.Session(root), default), "EDIT-AC-032");
        Assert.IsTrue(handler.Calls == 2 && interaction.Approvals == 1 && await File.ReadAllTextAsync(Path.Combine(root, "a")) == "final\r\n", "EDIT-AC-033-real-sequence");
        using var body = JsonDocument.Parse(handler.Bodies[1]);
        var responses = body.RootElement.GetProperty("contents")[2].GetProperty("parts");
        Assert.IsTrue(responses.GetArrayLength() == 2 && responses[0].GetProperty("functionResponse").GetProperty("name").GetString() == "read_file" &&
            responses[1].GetProperty("functionResponse").GetProperty("name").GetString() == "replace_text", "EDIT-AC-033-pairing");
    }

    [TestMethod]
    public async Task EDITAC033_SecondDeclineRetainsFirstVerifiedWrite()
    {
        var root = WorkspaceFixtures.NewDirectory(); var interaction = new EditChatInteraction();
        interaction.OnConfirm = preview => interaction.Decision = preview.RelativePath == "a" ? FileApprovalDecision.Approved : FileApprovalDecision.Declined;
        var context = new ConversationContext();
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("edit"));
        interaction.Inner.Inputs.Enqueue(() => { Assert.AreEqual(0, context.CompletedTurnCount, "EDIT-AC-062-no-commit"); return ChatInputResult.Submitted("/exit"); });
        var handler = new GeminiHandler
        {
            Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(
            "[{\"functionCall\":{\"name\":\"create_file\",\"args\":{\"path\":\"a\",\"content\":\"A\"}}},{\"functionCall\":{\"name\":\"create_file\",\"args\":{\"path\":\"b\",\"content\":\"B\"}}},{\"functionCall\":{\"name\":\"read_file\",\"args\":{\"path\":\"a\"}}}]")))
        };
        await new ChatApplication(interaction, new SdkChatFactory(handler), context, new(), "instruction", new TestWorkspaceFilesFactory()).RunAsync(WorkspaceFixtures.Session(root), default);
        var operations = interaction.Inner.Events.Where(e => e.Agent?.Operation is not null).Select(e => e.Agent!.Operation!).ToArray();
        Assert.IsTrue(handler.Calls == 1 && interaction.Approvals == 2 && operations.Length == 3 && operations[0].WriteState == FileWriteState.Verified &&
            operations[1].WriteState == FileWriteState.NotApplied && operations[2].Status == ToolStatus.NotExecuted, "EDIT-AC-033");
        Assert.IsTrue(await File.ReadAllTextAsync(Path.Combine(root, "a")) == "A" && !File.Exists(Path.Combine(root, "b")), "EDIT-AC-015-keep-first");
    }

    [TestMethod]
    public async Task EDITAC039_ExactSdkBodyLimitIncludesFourToolsAndRealWrite()
    {
        async Task<(int Bytes, int Calls, AgentTurnResult Result)> Run(int padding)
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root);
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), new EditApproval());
            var registry = Registry(files, session);
            var handler = new GeminiHandler();
            handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1 ? GeminiFixtures.Response(
                "[{\"functionCall\":{\"name\":\"create_file\",\"args\":{\"path\":\"a\",\"content\":\"中文\\n\"}},\"thoughtSignature\":\"c2ln\"}]") : GeminiFixtures.Text()));
            await using var client = new GeminiClient(session, handler);
            var result = await new AgentRunner(client, registry, session.Policy, new string('x', padding), new(), _ => { }).RunAsync([LlmMessage.FromUserText("create")], default);
            return (handler.Bodies[^1].Length, handler.Calls, result);
        }
        var calibration = await Run(1); var padding = 262144 - calibration.Bytes + 1;
        var accepted = await Run(padding);
        Assert.IsTrue(accepted.Bytes == 262144 && accepted.Calls == 2 && accepted.Result.Status == AgentTurnStatus.Completed, "EDIT-AC-039-262144");
        var rejected = await Run(padding + 1);
        Assert.IsTrue(rejected.Calls == 1 && rejected.Result.GenerationsStarted == 2 && rejected.Result.ModelErrorCode == LlmErrorCode.InputTooLarge &&
            rejected.Result.Operations.Single().WriteState == FileWriteState.Verified && rejected.Result.TurnUsage.DispatchedRequests == 1, "EDIT-AC-039-262145");
    }

    [TestMethod]
    public async Task EDITAC031_FifthGenerationCannotPromptOrWrite()
    {
        var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(handler.Calls < 5
            ? "[{\"functionCall\":{\"name\":\"list_files\",\"args\":{\"path\":\".\"}}}]"
            : "[{\"functionCall\":{\"name\":\"create_file\",\"args\":{\"path\":\"a\",\"content\":\"x\"}}}]")));
        await using var client = new GeminiClient(session, handler);
        var result = await new AgentRunner(client, Registry(files, session), session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("loop")], default);
        Assert.IsTrue(handler.Calls == 5 && result.StopReason == AgentStopReason.GenerationLimit && result.Operations[^1].ErrorCode == "generation_limit" &&
            result.Operations[^1].WriteState == FileWriteState.NotApplied && approval.Calls == 0 && Directory.GetFileSystemEntries(root).Length == 0, "EDIT-AC-031");
    }

    [TestMethod]
    public async Task EDITAC034_ModelFailureAfterWriteDoesNotReplayOrEraseFact()
    {
        foreach (var error in new[] { LlmErrorCode.NetworkFailure, LlmErrorCode.Timeout, LlmErrorCode.AuthenticationFailed })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
            using var args = JsonDocument.Parse("{\"path\":\"a\",\"content\":\"x\"}");
            var client = new ScriptClient { Session = session };
            client.Respond = (index, _, _) =>
            {
                LlmToolCall[] calls = [new(0, null, "create_file", args.RootElement)];
                return Task.FromResult(index == 0 ? new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), null, true, false)
                    : new LlmResult(LlmStatus.Failed, error, "", [], null, null, true, false));
            };
            var result = await new AgentRunner(client, Registry(files, session), session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("create")], default);
            Assert.IsTrue(result.ModelErrorCode == error && result.Operations.Single().WriteState == FileWriteState.Verified && approval.Calls == 1 &&
                client.Requests.Count == 2 && result.CompletedTurn.Count == 0 && await File.ReadAllTextAsync(Path.Combine(root, "a")) == "x", "EDIT-AC-034");
        }
    }

    [TestMethod]
    public async Task EDITAC029_UnconfirmedOutranksCancellationAndCleanupOutranksBoth()
    {
        foreach (var cleanup in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory(); using var session = WorkspaceFixtures.Session(root); using var cancel = new CancellationTokenSource();
            var fs = new EditFileSystem { Fault = cleanup ? "cleanup" : "publish-after", OnPublish = cancel.Cancel };
            await using var files = new WorkspaceFiles(session.Workspace, session.Policy, fs, new EditApproval());
            using var args = JsonDocument.Parse("{\"path\":\"a\",\"content\":\"x\"}");
            var client = new ScriptClient
            {
                Session = session,
                Respond = (_, _, _) =>
            {
                LlmToolCall[] calls = [new(0, null, "create_file", args.RootElement), new(1, null, "create_file", args.RootElement)];
                return Task.FromResult(new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), null, true, false));
            }
            };
            var result = await new AgentRunner(client, Registry(files, session), session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("create")], cancel.Token);
            Assert.IsTrue(result.StopReason == (cleanup ? AgentStopReason.LocalFailure : AgentStopReason.WriteUnconfirmed) && result.Operations.Count == 2 &&
                result.Operations[0].WriteState == FileWriteState.Unconfirmed && result.Operations[1].ErrorCode == "turn_stopped", "EDIT-AC-029-priority");
        }
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("file")]
    [DataRow("link")]
    public async Task EDITAC020_ChangedSnapshotParentStopsTheWholeBatch(string kind)
    {
        var root = WorkspaceFixtures.NewDirectory(); var parent = Path.Combine(root, "dir"); var moved = Path.Combine(root, "moved");
        Directory.CreateDirectory(parent); await File.WriteAllTextAsync(Path.Combine(parent, "a"), "abc");
        using var session = WorkspaceFixtures.Session(root); var approval = new EditApproval();
        await using var files = new WorkspaceFiles(session.Workspace, session.Policy, new MacWorkspaceFileSystem(), approval);
        Assert.AreEqual(WorkspaceStatus.Success, (await files.ReadFileAsync("dir/a", default)).Status, "EDIT-AC-020-seed");
        Directory.Move(parent, moved);
        if (kind == "file") await File.WriteAllTextAsync(parent, "external");
        else if (kind == "link") Directory.CreateSymbolicLink(parent, moved);
        using var replace = JsonDocument.Parse("{\"path\":\"dir/a\",\"old_text\":\"b\",\"new_text\":\"B\"}");
        using var create = JsonDocument.Parse("{\"path\":\"new\",\"content\":\"x\"}");
        var client = new ScriptClient
        {
            Session = session,
            Respond = (_, _, _) =>
        {
            LlmToolCall[] calls = [new(0, null, "replace_text", replace.RootElement), new(1, null, "create_file", create.RootElement)];
            return Task.FromResult(new LlmResult(LlmStatus.ToolCalls, LlmErrorCode.None, "", calls, LlmMessage.FromModel(new object(), calls), null, true, false));
        }
        };
        var result = await new AgentRunner(client, Registry(files, session), session.Policy, "instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("edit")], default);
        Assert.IsTrue(result.StopReason == AgentStopReason.FileConflict && client.Requests.Count == 1 && approval.Calls == 0 &&
            !files.Snapshots.TryGet("dir/a", out _) && !File.Exists(Path.Combine(root, "new")), "EDIT-AC-020-parent-" + kind);
        Assert.IsTrue(result.Operations.Count == 2 && result.Operations[1].Status == ToolStatus.NotExecuted && result.Operations[1].ErrorCode == "turn_stopped", "EDIT-AC-060-parent-remainder");
    }

    private static ToolRegistry Registry(IWorkspaceFiles files, BootstrapSession session) => TestRulesRegistry.Create(
        [new ListFilesTool(files, session.Policy), new ReadFileTool(files, session.Policy), new CreateFileTool(files, session.Policy), new ReplaceTextTool(files, session.Policy)], session.Policy);
}