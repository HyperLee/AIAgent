using AIAgent.Cli.Processes;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class RvaContractTests
{
    [TestMethod]
    public void RVAAC008_ConsentExplainsThatTheIdentityLimitIncludesRoot()
    {
        var snapshot = new ProcessAdmissionSnapshot("/fixture", "/fixture/Fixture.csproj",
            new("/sdk", "10.0.403", "/runtime", "/packages"), [], [],
            new Dictionary<string, WorkspaceNodeState>(), new Dictionary<string, byte[]>());
        var preview = DotnetRecipe.Preview(snapshot, DotnetTaskKind.Build, "/private/tmp/aia-fixture", TimeSpan.FromSeconds(120), null);
        Assert.IsTrue(preview.Contains("256 個程序身分，包含根程序", StringComparison.Ordinal), "Consent must not promise 256 descendants plus a root.");
        Assert.IsFalse(preview.Contains("256 個後代", StringComparison.Ordinal));
    }
}