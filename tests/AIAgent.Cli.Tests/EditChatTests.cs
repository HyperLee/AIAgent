using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class EditChatTests
{
    [TestMethod]
    public async Task EDITAC032_RealChatSdkWorkspaceCreatesThenAnswers()
    {
        var root = WorkspaceFixtures.NewDirectory(); var interaction = new EditChatInteraction();
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("create"));
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1
            ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"create_file\",\"id\":\"write-id\",\"args\":{\"path\":\"new.md\",\"content\":\"Hello\\n\"}},\"thoughtSignature\":\"c2ln\"}]")
            : GeminiFixtures.Text("done")));
        var usage = new UsageAccumulator();
        var code = await new ChatApplication(interaction, new SdkChatFactory(handler), new(), usage, "instruction", new WorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default);
        Assert.AreEqual(0, code, "EDIT-AC-032-exit");
        Assert.IsTrue(File.Exists(Path.Combine(root, "new.md")), "EDIT-AC-032-real-write");
        Assert.AreEqual("Hello\n", await File.ReadAllTextAsync(Path.Combine(root, "new.md")), "EDIT-AC-032-bytes");
        Assert.IsTrue(interaction.Approvals == 1 && handler.Calls == 2 && usage.Snapshot().DispatchedRequests == 2, "EDIT-AC-032-counts");
        using var body = JsonDocument.Parse(handler.Bodies[1]);
        var declarations = body.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations");
        CollectionAssert.AreEqual(new[] { "list_files", "read_file", "create_file", "replace_text", "search_files", "search_text", "read_file_range", "update_task", "run_dotnet_task" }, declarations.EnumerateArray().Select(d => d.GetProperty("name").GetString()).ToArray(), "EDIT-AC-003-seven-tools");
        var contents = body.RootElement.GetProperty("contents");
        Assert.AreEqual("c2ln", contents[1].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString(), "EDIT-AC-032-signature");
        var response = contents[2].GetProperty("parts")[0].GetProperty("functionResponse");
        Assert.IsTrue(response.GetProperty("id").GetString() == "write-id" && response.GetProperty("response").GetProperty("data").GetProperty("byte_count").GetInt32() == 6, "EDIT-AC-039-response");
        Assert.IsTrue(interaction.Inner.Events.Any(e => e.Agent?.Operation is { } op && op.GetType().GetProperty("WriteState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(op) is FileWriteState.Verified), "EDIT-AC-061-local-fact");
    }

    [TestMethod]
    public async Task EDITAC015_DeclineStopsEntireBatchWithoutGeneration()
    {
        var root = WorkspaceFixtures.NewDirectory(); var interaction = new EditChatInteraction { Decision = FileApprovalDecision.Declined };
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("create"));
        interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit"));
        var handler = new GeminiHandler
        {
            Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(
            "[{\"functionCall\":{\"name\":\"create_file\",\"args\":{\"path\":\"a\",\"content\":\"x\"}}},{\"functionCall\":{\"name\":\"read_file\",\"args\":{\"path\":\"a\"}}}]")))
        };
        var code = await new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "instruction", new WorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default);
        Assert.IsTrue(code == 0 && handler.Calls == 1 && interaction.Approvals == 1 && Directory.GetFileSystemEntries(root).Length == 0, "EDIT-AC-015");
        var operations = interaction.Inner.Events.Where(e => e.Agent?.Operation is not null).Select(e => e.Agent!.Operation!).ToArray();
        Assert.IsTrue(operations.Length == 2 && operations[0].ErrorCode == "approval_declined" && operations[1].ErrorCode == "turn_stopped", "EDIT-AC-015-remainder");
    }
    [TestMethod]
    public async Task EDITAC030_ApprovalAndBusyRecoveryFailureRemainLocalFailure()
    {
        foreach (var recovery in new[] { false, true })
        {
            var root = WorkspaceFixtures.NewDirectory();
            var interaction = new EditChatInteraction { Decision = recovery ? FileApprovalDecision.Approved : FileApprovalDecision.Unavailable };
            if (recovery) interaction.OnConfirm = _ => interaction.Inner.ObserverFails = true;
            interaction.Inner.Inputs.Enqueue(() => ChatInputResult.Submitted("create"));
            var handler = new GeminiHandler
            {
                Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(
                "[{\"functionCall\":{\"name\":\"create_file\",\"args\":{\"path\":\"a\",\"content\":\"x\"}}}]")))
            };
            var exit = await new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "instruction", new WorkspaceFilesFactory()).RunAsync(WorkspaceFixtures.Session(root), default);
            var operation = interaction.Inner.Events.Single(e => e.Agent?.Operation is not null).Agent!.Operation!;
            Assert.IsTrue(exit == 1 && operation.Status == Tools.ToolStatus.Failed && operation.ErrorCode == "unavailable" &&
                operation.WriteState == FileWriteState.NotApplied && Directory.GetFileSystemEntries(root).Length == 0, "EDIT-AC-030-local-failure");
            Assert.IsTrue(interaction.Inner.Events.Any(e => e.StopReason == AgentStopReason.LocalFailure), "EDIT-AC-043-priority");
        }
    }

}

internal sealed class EditChatInteraction : IChatInteraction
{
    internal ScriptInteraction Inner { get; } = new();
    internal int Approvals { get; private set; }
    internal FileApprovalDecision Decision { get; set; } = FileApprovalDecision.Approved;
    internal Action<FileChangePreview>? OnConfirm { get; set; }
    public ValueTask<FileApprovalDecision> ConfirmAsync(FileChangePreview preview, CancellationToken token)
    { Approvals++; OnConfirm?.Invoke(preview); return new(Decision); }
    public ValueTask<ChatInputResult> ReadInputAsync(CancellationToken token) => Inner.ReadInputAsync(token);
    public ValueTask<KeyReplacementChoice> ChooseKeyReplacementAsync(CancellationToken token) => Inner.ChooseKeyReplacementAsync(token);
    public ValueTask<KeyInputResult> ReadKeyAsync(CancellationToken token) => Inner.ReadKeyAsync(token);
    public IDisposable ObserveBusyInput(Action<ChatControlSignal> signal) => Inner.ObserveBusyInput(signal);
    public void SetPolicy(Security.ISensitiveDataPolicy policy) => Inner.SetPolicy(policy);
    public void Show(ChatEvent message) => Inner.Show(message);
}