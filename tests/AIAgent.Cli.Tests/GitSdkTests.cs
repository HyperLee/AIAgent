using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Git;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class GitSdkTests
{
    [TestMethod]
    public async Task GITAC001_RealSdkPreservesReviewToolPairingAndValidatedFinding()
    {
        var interaction = new ScriptInteraction();
        foreach (var input in new[] { "/review staged", "/review show", "/exit" }) interaction.Inputs.Enqueue(() => ChatInputResult.Submitted(input));
        var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1
            ? GeminiFixtures.Response("[{\"functionCall\":{\"name\":\"git_status\",\"id\":\"s\",\"args\":{}},\"thoughtSignature\":\"c2ln\"},{\"functionCall\":{\"name\":\"git_diff\",\"id\":\"d\",\"args\":{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\"}}},{\"functionCall\":{\"name\":\"read_git_file_range\",\"id\":\"r\",\"args\":{\"snapshot_id\":\"g1\",\"layer\":\"staged\",\"path\":\"a.cs\",\"side\":\"new\",\"start_line\":1,\"end_line\":2}}}]")
            : GeminiFixtures.Text("{\"version\":1,\"snapshot_id\":\"g1\",\"selection\":\"staged\",\"summary\":\"reviewed\",\"findings\":[{\"severity\":\"P2\",\"title\":\"Concrete issue\",\"body\":\"Trigger and impact.\",\"path\":\"a.cs\",\"layer\":\"staged\",\"side\":\"new\",\"start_line\":2,\"end_line\":2}]}")));
        var usage = new UsageAccumulator();
        Assert.AreEqual(0, await new ChatApplication(interaction, new SdkChatFactory(handler), new(), usage, "test instruction", new TestWorkspaceFilesFactory(), gitReadWorkspaceFactory: new Factory())
            .RunAsync(WorkspaceFixtures.Session(), default));
        Assert.AreEqual(2, handler.Calls); Assert.AreEqual(2L, usage.Snapshot().DispatchedRequests);
        using var body = JsonDocument.Parse(handler.Bodies[1]); var root = body.RootElement;
        CollectionAssert.AreEqual(new[] { "git_status", "git_diff", "read_git_file_range" }, root.GetProperty("tools")[0].GetProperty("functionDeclarations").EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray());
        var contents = root.GetProperty("contents"); Assert.AreEqual("c2ln", contents[1].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
        var responses = contents[2].GetProperty("parts"); Assert.AreEqual(3, responses.GetArrayLength());
        CollectionAssert.AreEqual(new[] { "s", "d", "r" }, responses.EnumerateArray().Select(x => x.GetProperty("functionResponse").GetProperty("id").GetString()).ToArray());
        Assert.AreEqual("context\nnew\n", responses[2].GetProperty("functionResponse").GetProperty("response").GetProperty("data").GetProperty("content").GetString());
        var report = interaction.Events.Single(e => e.Kind == ChatEventKind.Answer).Text!;
        StringAssert.Contains(report, "完整差異提供：1/1"); StringAssert.Contains(report, "Concrete issue"); StringAssert.Contains(report, "staged / new / 2-2");
    }

    private sealed class Factory : IGitReadWorkspaceFactory
    { public IGitReadWorkspace Create(BootstrapSession session, IWorkspaceFiles workspace) => new ReviewGitWorkspace(); }
}