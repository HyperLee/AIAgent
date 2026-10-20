using System.Text;
using System.Text.Json;

using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkspaceContractTests
{
    [TestMethod]
    public void WSAC002_ResultsCopyInputsAndExposeOnlyCompleteData()
    {
        var entries = new[] { new WorkspaceEntry("a", WorkspaceEntryKind.File) };
        var data = new WorkspaceListData(".", entries, false);
        entries[0] = new("b", WorkspaceEntryKind.Directory);
        Assert.IsTrue(data.Entries[0].Path == "a", "WS-AC-002");
        Assert.IsFalse(data.Entries is WorkspaceEntry[], "WS-AC-002");
        var result = WorkspaceListResult.Success(data);
        Assert.IsTrue(result.Status == WorkspaceStatus.Success && result.ErrorCode == WorkspaceErrorCode.None && result.Data == data, "WS-AC-002");
        foreach (var status in new[] { WorkspaceStatus.Rejected, WorkspaceStatus.Cancelled, WorkspaceStatus.Failed })
        {
            var failed = WorkspaceReadResult.End(status, status == WorkspaceStatus.Cancelled ? WorkspaceErrorCode.None : WorkspaceErrorCode.IoFailure);
            Assert.IsTrue(failed.Data is null, "WS-AC-002");
        }
        Assert.IsTrue(result.ToString() == "[WorkspaceListResult]" && data.ToString() == "[WorkspaceListData]", "WS-AC-002");
        var bytes = Encoding.UTF8.GetBytes("abc");
        var snapshot = new WorkspaceReadSnapshot("a", bytes, false);
        bytes[0] = 0;
        var copy = snapshot.CopyOriginalBytes(); copy[1] = 0;
        Assert.IsTrue(snapshot.CopyOriginalBytes().SequenceEqual(new byte[] { 97, 98, 99 }), "WS-AC-002");
        Assert.IsTrue(snapshot.Sha256Hex == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "WS-AC-002");
    }

    [TestMethod]
    public void WSAC002_ExplicitProjectionHasExactFieldsAndNoLocalSnapshotData()
    {
        using var session = GeminiFixtures.Session();
        var result = WorkspaceReadResult.Success(new("notes.md", "Hello\n", 6, false));
        Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(result, session.Policy, out var response, out _), "WS-AC-002");
        Assert.IsTrue(response.GetRawText() == "{\"status\":\"success\",\"error_code\":\"none\",\"data\":{\"path\":\"notes.md\",\"content\":\"Hello\\n\",\"byte_count\":6,\"has_utf8_bom\":false}}", "WS-AC-002");
        var failed = WorkspaceListResult.End(WorkspaceStatus.Rejected, WorkspaceErrorCode.SensitivePath);
        Assert.IsTrue(WorkspaceResponseWriter.TryCreateResponse(failed, session.Policy, out response, out _), "WS-AC-002");
        Assert.IsTrue(response.GetRawText() == "{\"status\":\"rejected\",\"error_code\":\"sensitive_path\"}", "WS-AC-002");
    }

    [TestMethod]
    public void WSAC020_ShortKeysBlockActualProjectionFieldsAndEscapedValues()
    {
        using var secrets = new SessionSecrets("status".ToCharArray());
        var policy = new SensitiveDataPolicy(secrets);
        var result = WorkspaceReadResult.Success(new("a", "safe", 4, false));
        Assert.IsFalse(WorkspaceResponseWriter.TryCreateResponse(result, policy, out _, out var error), "WS-AC-020");
        Assert.IsTrue(error == WorkspaceErrorCode.SensitiveContent, "WS-AC-020");
        Assert.IsFalse(WorkspaceResponseWriter.TryCreateResponse(WorkspaceReadResult.End(WorkspaceStatus.Rejected, error), policy, out _, out _), "WS-AC-020");
        using var escaped = new SessionSecrets("a\"b\\c".ToCharArray());
        Assert.IsFalse(WorkspaceResponseWriter.TryCreateResponse(WorkspaceReadResult.Success(new("a", "prefixa\"b\\c", 12, false)), new SensitiveDataPolicy(escaped), out _, out _), "WS-AC-020");
    }
}