using System.Text;
using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Llm;
using AIAgent.Cli.Tools;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class DiscoveryIntegrationTests
{
    private static string Batch(params (string Name, object Args)[] calls) => GeminiFixtures.Response(JsonSerializer.Serialize(
        calls.Select((call, index) => new { functionCall = new { name = call.Name, args = call.Args, id = "call-" + index }, thoughtSignature = "c2ln" })));

    [TestMethod]
    public async Task SEARCHAC028_RealApplicationAndSdkPreserveSevenToolsAndNativeBatchOrder()
    {
        var root = WorkspaceFixtures.NewDirectory(); await File.WriteAllTextAsync(Path.Combine(root, "a"), "one\r\n二\nlast");
        var interaction = new ScriptInteraction(); interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("locate"));
        interaction.Inputs.Enqueue(() => ChatInputResult.Submitted("/exit")); var handler = new GeminiHandler();
        handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(handler.Calls == 1 ? Batch(
            ("search_files", new { path = ".", pattern = "*" }), ("search_text", new { path = ".", query = "二" }),
            ("read_file_range", new { path = "a", start_line = 2, end_line = 20 })) : GeminiFixtures.Text()));
        Assert.IsTrue(await new ChatApplication(interaction, new SdkChatFactory(handler), new(), new(), "test instruction", new WorkspaceFilesFactory())
            .RunAsync(WorkspaceFixtures.Session(root), default) == 0 && handler.Calls == 2, "SEARCH-AC-028-app");
        using var first = JsonDocument.Parse(handler.Bodies[0]); using var second = JsonDocument.Parse(handler.Bodies[1]);
        var definitions = first.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations");
        Assert.IsTrue(definitions.GetArrayLength() == 9 && definitions[4].GetProperty("name").GetString() == "search_files" &&
            definitions[5].GetProperty("parametersJsonSchema").GetProperty("required").GetArrayLength() == 2, "SEARCH-AC-028-schema");
        var contents = second.RootElement.GetProperty("contents"); var parts = contents[2].GetProperty("parts");
        Assert.IsTrue(parts.GetArrayLength() == 3 && parts[0].GetProperty("functionResponse").GetProperty("id").GetString() == "call-0" &&
            parts[2].GetProperty("functionResponse").GetProperty("response").GetProperty("data").GetProperty("content_byte_count").GetInt32() == 8 &&
            contents[1].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString() == "c2ln" &&
            interaction.Events.Count(e => e.Agent?.Operation?.DiscoverySummary is not null) == 3, "SEARCH-AC-028-pairing");
    }

    [TestMethod]
    public async Task SEARCHAC003_RawSdkArgumentsPreserveDuplicateAndIntegerRejection()
    {
        foreach (var args in new[] { "{\"path\":\"a\",\"start_line\":1.0,\"end_line\":1}",
            "{\"path\":\"a\",\"start_line\":1e0,\"end_line\":1}",
            "{\"path\":\"a\",\"p\\u0061th\":\"a\",\"start_line\":1,\"end_line\":1}" })
        {
            await using var f = new DiscoveryFixture();
            var handler = new GeminiHandler { Respond = (_, _) => Task.FromResult(GeminiHandler.Json(GeminiFixtures.Response(
                "[{\"functionCall\":{\"name\":\"read_file_range\",\"args\":" + args + "}}]"))) };
            await using var client = new GeminiClient(f.Session, handler); var registry = DiscoveryToolTests.Registry(f.Files, f.Session);
            var result = await client.GenerateAsync(GeminiFixtures.Request(tools: registry.Definitions), default);
            if (args.Contains("p\\u0061th", StringComparison.Ordinal))
                Assert.IsTrue(result.ErrorCode == LlmErrorCode.InvalidResponse && result.ToolCalls.Count == 0, "SEARCH-AC-003-sdk-duplicate");
            else
                Assert.IsTrue((await registry.DispatchAsync(result.ToolCalls.Single(), default)).ErrorCode == "invalid_arguments", "SEARCH-AC-003-sdk-integer");
            Assert.IsTrue(f.Fs.Opened == 0 && f.Fs.Inspected.Count == 0, "SEARCH-AC-003-sdk-io");
        }
    }

    [TestMethod]
    public async Task SEARCHAC029_FifthGenerationMarksAllThreeToolsUnexecuted()
    {
        await using var f = new DiscoveryFixture(); f.Fs.File("a", [65]); var registry = DiscoveryToolTests.Registry(f.Files, f.Session);
        await using var client = new ScriptClient { Session = f.Session };
        client.Respond = (index, _, _) => Task.FromResult(index < 4 ? RunnerFixture.Calls(BudgetFixture.Call("search_files", new { path = ".", pattern = "*" })) :
            RunnerFixture.Calls(new(0, null, "search_files", JsonSerializer.SerializeToElement(new { path = ".", pattern = "*" })),
                new(1, null, "search_text", JsonSerializer.SerializeToElement(new { path = ".", query = "A" })),
                new(2, null, "read_file_range", JsonSerializer.SerializeToElement(new { path = "a", start_line = 1, end_line = 1 }))));
        var result = await new AgentRunner(client, registry, f.Session.Policy, "test instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("q")], default);
        Assert.IsTrue(result.StopReason == AgentStopReason.GenerationLimit && result.GenerationsStarted == 5 && client.Requests.Count == 5 &&
            f.Fs.Enumerated.Count == 4 && f.Fs.Opened == 0 && result.Operations.TakeLast(3).All(o => o.Status == ToolStatus.NotExecuted), "SEARCH-AC-029-fifth");
    }

    [TestMethod]
    public async Task SEARCHAC028_SafeRejectionsContinueWhileSensitiveContentStopsTheBatch()
    {
        foreach (var sensitive in new[] { false, true })
        {
            await using var f = new DiscoveryFixture(); f.Fs.File("a", Encoding.UTF8.GetBytes(sensitive ? GeminiFixtures.Key : "A"));
            var registry = DiscoveryToolTests.Registry(f.Files, f.Session); await using var client = new ScriptClient { Session = f.Session };
            var first = sensitive ? BudgetFixture.Call("search_text", new { path = ".", query = "A" }) : BudgetFixture.Call("search_files", new { path = ".", pattern = "***" });
            client.Respond = (index, _, _) => Task.FromResult(index == 0 ? RunnerFixture.Calls(first,
                new(1, null, "read_file_range", JsonSerializer.SerializeToElement(new { path = "a", start_line = 1, end_line = 1 }))) : ChatFixture.Completed());
            var result = await new AgentRunner(client, registry, f.Session.Policy, "test instruction", new(), _ => { }).RunAsync([LlmMessage.FromUserText("q")], default);
            Assert.IsTrue(sensitive ? result.StopReason == AgentStopReason.SensitiveData && client.Requests.Count == 1 && result.Operations[1].Status == ToolStatus.NotExecuted :
                result.Status == AgentTurnStatus.Completed && client.Requests.Count == 2 && result.Operations[0].ErrorCode == "invalid_pattern" && result.Operations[1].DiscoverySummary is not null, "SEARCH-AC-028-stop");
        }
    }

    [TestMethod]
    public async Task SEARCHAC029_SevenToolsMultipleResultsHistoryAndWritesHonorExactSdkBodyLimit()
    {
        async Task<(int Bytes, int Calls, AgentTurnResult Result, int Writes)> Run(int padding)
        {
            await using var f = new BudgetFixture(new string('x', padding)); await File.WriteAllTextAsync(Path.Combine(f.Root, "a"), "A");
            var registry = DiscoveryToolTests.Registry(f.Files, f.Session);
            f.Handler.Respond = (_, _) => Task.FromResult(GeminiHandler.Json(f.Handler.Calls == 2 ? Batch(
                ("search_files", new { path = ".", pattern = "*" }), ("search_text", new { path = ".", query = "A" }),
                ("read_file_range", new { path = "a", start_line = 1, end_line = 1 }), ("create_file", new { path = "b", content = "safe" })) : GeminiFixtures.Text()));
            var runner = new AgentRunner(f.Client, registry, f.Session.Policy, f.Instruction, f.Usage, f.Events.Add);
            var prior = await runner.RunAsync([LlmMessage.FromUserText("prior")], default);
            var result = await runner.RunAsync(prior.CompletedTurn.Append(LlmMessage.FromUserText("locate")).ToArray(), default);
            return (f.Handler.Bodies[^1].Length, f.Handler.Calls, result, f.FileSystem.PublishCalls);
        }
        var calibration = await Run(1); var padding = 262144 - calibration.Bytes + 1;
        var accepted = await Run(padding); var rejected = await Run(padding + 1);
        Assert.IsTrue(accepted.Bytes == 262144 && accepted.Calls == 3 && accepted.Result.Status == AgentTurnStatus.Completed && accepted.Writes == 1, "SEARCH-AC-029-262144");
        Assert.IsTrue(rejected.Calls == 2 && rejected.Result.ModelErrorCode == LlmErrorCode.InputTooLarge && rejected.Writes == 1 &&
            rejected.Result.Operations.Count == 4 && rejected.Result.Operations[^1].WriteState == FileWriteState.Verified && rejected.Result.CompletedTurn.Count == 0, "SEARCH-AC-029-262145");
    }
}