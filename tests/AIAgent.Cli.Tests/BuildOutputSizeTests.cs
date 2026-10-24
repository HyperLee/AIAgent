using System.Text;

using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;
using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildOutputSizeTests
{
    [TestMethod]
    public void BUILDAC025_EscapedControlOutputIsReducedWithoutDiscardingFacts()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray()); var policy = new SensitiveDataPolicy(secrets);
        var output = new ProcessOutputCollector(policy, _ => { });
        for (var i = 0; i < 4; i++) output.Feed(false, Enumerable.Repeat((byte)27, 4096).ToArray());
        output.Complete(false); output.Complete(true);
        var summary = new ProcessExecutionSummary(DotnetTaskKind.Build, "a.csproj", ProcessStartState.Started,
            ProcessEndState.Exited, 0, 10, true, false, ValidationOutcome.BuildSucceeded, stdoutBytes: 16384);
        var result = ProcessResultWriter.Create(ToolStatus.Success, "none", summary, output, policy);
        Assert.IsNotNull(result.Response);
        Assert.IsTrue(Encoding.UTF8.GetByteCount(result.Response.Value.GetRawText()) <= 65536);
        var data = result.Response.Value.GetProperty("data");
        Assert.AreEqual(16384L, data.GetProperty("stdout_bytes").GetInt64());
        Assert.IsTrue(data.GetProperty("stdout_truncated").GetBoolean());
        Assert.AreEqual("build_succeeded", data.GetProperty("validation").GetString());
        Assert.AreSame(summary, result.ProcessSummary);
        Assert.IsFalse(data.GetProperty("stdout").GetString()!.Contains('\u001b'));
    }
    [TestMethod]
    public void BUILDAC026_ShortKeyMatchingFixedFieldsBlocksJsonWithoutLosingLocalFact()
    {
        using var secrets = new SessionSecrets("task".ToCharArray()); var policy = new SensitiveDataPolicy(secrets);
        var summary = new ProcessExecutionSummary(DotnetTaskKind.Build, "a.csproj", ProcessStartState.Started,
            ProcessEndState.Exited, 0, 10, true, false, ValidationOutcome.BuildSucceeded);
        var result = ProcessResultWriter.Create(ToolStatus.Success, "none", summary, null, policy);
        Assert.IsNull(result.Response); Assert.AreSame(summary, result.WithoutResponse().ProcessSummary);
    }
    [TestMethod]
    public void BUILDAC019_SecretsAcrossDroppedWindowsAndCombinedStreamsAreBlocked()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray()); var policy = new SensitiveDataPolicy(secrets);
        var output = new ProcessOutputCollector(policy, _ => { });
        for (var i = 0; i < 10; i++) output.Feed(false, Encoding.UTF8.GetBytes(new string('x', 4096)));
        output.Feed(false, Encoding.UTF8.GetBytes("SYNTHETIC_PRO"));
        output.Feed(true, Encoding.UTF8.GetBytes("CESS_SECRET"));
        Assert.AreEqual("sensitive", output.State); Assert.AreEqual("", output.Stdout); Assert.AreEqual("", output.Stderr);
    }
    [TestMethod]
    public void BUILDAC024_ScalarAlignedEvictionIsMarkedEvenAtExactWindowByteLimit()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray()); var policy = new SensitiveDataPolicy(secrets);
        var output = new ProcessOutputCollector(policy, _ => { });
        var bytes = Encoding.UTF8.GetBytes(new string('a', 4095) + "中" + new string('b', 12286));
        for (var i = 0; i < bytes.Length; i += 4096) output.Feed(false, bytes.AsSpan(i, Math.Min(4096, bytes.Length - i)));
        output.Complete(false);
        Assert.AreEqual(16384L, output.StdoutBytes);
        Assert.IsTrue(output.StdoutTruncated);
    }
    [TestMethod]
    public void BUILDAC017_CompleteSummaryAllowsWhitespaceOnlyTailWithoutFinalNewline()
    {
        var parser = new MtpSummaryParser();
        parser.Feed("Test run summary: Passed!\n total: 1\n failed: 0\n succeeded: 1\n skipped: 0\n \t");
        parser.Complete(); Assert.IsNotNull(parser.Counts);
    }

    [TestMethod]
    public void BUILDAC017_DroppedOversizedFailedCandidateCannotBeHiddenByLaterPassedSummary()
    {
        var parser = new MtpSummaryParser();
        parser.Feed("Test run summary: Failed!" + new string(' ', 8193) + "\nTest run summary: Passed!\n total: 1\n failed: 0\n succeeded: 1\n skipped: 0\n");
        parser.Complete();
        Assert.IsNull(parser.Counts);
    }

    [TestMethod]
    public void BUILDAC017_OverlongSummaryAndOverflowNeverVerifyEvenWhenTextSaysPassed()
    {
        foreach (var total in new[] { "9223372036854775808", "1,000", "-1", "1.0" })
        {
            var parser = new MtpSummaryParser();
            parser.Feed("Test run summary: Passed!\n total: " + total + "\n failed: 0\n succeeded: 1\n skipped: 0\n");
            parser.Complete(); Assert.IsNull(parser.Counts);
        }
        var overlong = new MtpSummaryParser();
        overlong.Feed("Test run summary: Passed!\n" + new string(' ', 8193) + "\n total: 1\n failed: 0\n succeeded: 1\n skipped: 0\n");
        overlong.Complete(); Assert.IsNull(overlong.Counts);
    }
}