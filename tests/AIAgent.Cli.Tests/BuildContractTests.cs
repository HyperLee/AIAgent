using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildContractTests
{
    [TestMethod]
    public void BUILDAC026_WithoutResponsePreservesIndependentProcessFacts()
    {
        var summary = new ProcessExecutionSummary(DotnetTaskKind.Build, "a.csproj", ProcessStartState.Started,
            ProcessEndState.Unconfirmed, null, 1, false, true, ValidationOutcome.NotVerified);
        var value = new ToolExecutionResult(ToolStatus.Failed, "process_cleanup_failed", null,
            processSummary: summary, processCleanupFailed: true, stopAfterOperation: true).WithoutResponse();
        Assert.AreSame(summary, value.ProcessSummary);
        Assert.IsTrue(value.ProcessCleanupFailed);
        Assert.IsFalse(value.CleanupFailed);
        Assert.IsNull(value.WriteState);
        Assert.IsTrue(value.StopAfterOperation);
    }

    [TestMethod]
    public void BUILDAC004_StrictArgumentsRejectBeforeServiceAndKeepPathWhitespace()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray());
        var policy = new SensitiveDataPolicy(secrets);
        foreach (var text in new[] { "{}", "{\"task\":\"Build\",\"project_path\":\"a.csproj\"}",
            "{\"task\":\"test\",\"task\":\"build\",\"project_path\":\"a.csproj\"}",
            "{\"task\":\"build\",\"project_path\":null}",
            "{\"task\":\"build\",\"project_path\":\"a.sln\"}",
            "{\"task\":\"build\",\"project_path\":\"a.csproj\",\"approved\":true}" })
        {
            using var json = JsonDocument.Parse(text);
            Assert.IsFalse(DotnetTaskArguments.TryParse(json.RootElement, policy, out _, out _, out _));
        }
        using var valid = JsonDocument.Parse("{\"task\":\"build\",\"project_path\":\" ./ folder /a.csproj\"}");
        Assert.IsTrue(DotnetTaskArguments.TryParse(valid.RootElement, policy, out var task, out var path, out _));
        Assert.AreEqual(" ./ folder /a.csproj", path);
        Assert.AreEqual(DotnetTaskKind.Build, task);
    }

    [TestMethod]
    public async Task BUILDAC002_RegistryNeverDispatchesProcessInPlanMode()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray());
        var policy = new SensitiveDataPolicy(secrets);
        var plans = new PlanStore(policy); plans.SetMode(AgentMode.Plan);
        await using var tasks = new CountingTasks();
        var registry = TestRulesRegistry.Create([new RunDotnetTaskTool(tasks, policy)], policy, new(plans));
        Assert.AreEqual(0, registry.Definitions.Count);
        var result = await registry.DispatchAsync(new LlmToolCall(0, "call", "run_dotnet_task",
            JsonSerializer.SerializeToElement(new { task = "build", project_path = "a.csproj" })), default);
        Assert.AreEqual("mode_restricted", result.ErrorCode);
        Assert.AreEqual(0, tasks.Calls);
    }
    private sealed class CountingTasks : IControlledDotnetTasks
    {
        internal int Calls;
        public Task<DotnetTaskResult> RunAsync(DotnetTaskKind task, string projectPath, CancellationToken token)
        { Calls++; throw new InvalidOperationException(); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}