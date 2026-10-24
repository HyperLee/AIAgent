using System.Text;

using AIAgent.Cli.Processes;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class BuildOutputTests
{
    [TestMethod]
    public void BUILDAC019_SecretAcrossChunksAndAnsiIsNeverRetained()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray());
        var output = new ProcessOutputCollector(new SensitiveDataPolicy(secrets), _ => { });
        output.Feed(false, Encoding.UTF8.GetBytes("SYNTHETIC_PRO"));
        output.Feed(false, Encoding.UTF8.GetBytes("CESS_SECRET"));
        output.Complete(false);
        Assert.AreEqual("sensitive", output.State);
        Assert.AreEqual("", output.Stdout);
        var visible = new ProcessOutputCollector(new SensitiveDataPolicy(secrets), _ => { });
        visible.Feed(false, Encoding.UTF8.GetBytes("SYNTHETIC_\u001b[31mPROCESS_\u001b[0mSECRET"));
        Assert.AreEqual("sensitive", visible.State);
        Assert.AreEqual("", visible.Stdout);
    }

    [TestMethod]
    public void BUILDAC024_ExactOutputLimitStopsAfterOneAdditionalByte()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray());
        var stops = new List<string>();
        var output = new ProcessOutputCollector(new SensitiveDataPolicy(secrets), stops.Add);
        var block = Enumerable.Repeat((byte)'x', 4096).ToArray();
        for (var i = 0; i < 256; i++) output.Feed(i % 2 != 0, block);
        Assert.IsFalse(output.OutputLimit);
        output.Feed(false, [120]);
        Assert.IsTrue(output.OutputLimit);
        Assert.AreEqual(1048577L, output.StdoutBytes + output.StderrBytes);
        CollectionAssert.AreEqual(new[] { "process_output_limit" }, stops);
        Assert.IsTrue(output.StdoutTruncated);
    }

    [TestMethod]
    public void BUILDAC017_OnlyCompleteFinalSingleSummaryCanVerifyTests()
    {
        const string good = "Test run summary: Passed!\n  total: 4\n  failed: 0\n  succeeded: 3\n  skipped: 1\n  duration: 10ms\n";
        foreach (var text in new[] { good, "build output\n" + good })
        {
            var parser = new MtpSummaryParser(); parser.Feed(text); parser.Complete();
            Assert.AreEqual(3L, parser.Counts!.Passed);
            Assert.AreEqual(1L, parser.Counts.Skipped);
        }
        foreach (var text in new[] { good + "after\n", good + good, good.Replace("total: 4", "total: 5"),
            good.Replace("succeeded: 3", "succeeded: 0").Replace("total: 4", "total: 1"),
            good.Replace("failed: 0\n", ""), good.TrimEnd(), good.Replace("total: 4", "total: 4e0"),
            good.Replace("skipped: 1", "skipped: 1\n  skipped: 1"),
            good.Replace("duration: 10ms", "assembly: other.dll") })
        {
            var parser = new MtpSummaryParser(); parser.Feed(text); parser.Complete();
            Assert.IsNull(parser.Counts);
        }
    }

    [TestMethod]
    public void BUILDAC024_Utf8SplitIsValidButIncompleteScalarIsRejected()
    {
        using var secrets = new SessionSecrets("SYNTHETIC_PROCESS_SECRET".ToCharArray());
        var output = new ProcessOutputCollector(new SensitiveDataPolicy(secrets), _ => { });
        var bytes = Encoding.UTF8.GetBytes("中文");
        foreach (var b in bytes) output.Feed(false, [b]);
        output.Complete(false);
        Assert.AreEqual("中文", output.Stdout);
        var invalid = new ProcessOutputCollector(new SensitiveDataPolicy(secrets), _ => { });
        invalid.Feed(false, [0xe4]); invalid.Complete(false);
        Assert.AreEqual("invalid_utf8", invalid.State);
        Assert.AreEqual("", invalid.Stdout);
    }
}