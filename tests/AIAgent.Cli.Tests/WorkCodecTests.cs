using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkCodecTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";
    private const string Minimal = "{\"format_version\":1,\"work_id\":\"0123456789abcdef0123456789abcdef\",\"title\":\" retained title \",\"created_utc\":\"2026-10-26T08:00:00Z\",\"workspace\":{\"root\":\"/Users/example/Project\",\"device\":\"0\",\"identity\":\"12345\"},\"scope_hint\":\".\",\"rules_refs\":[],\"summary\":null,\"pins\":[\" retained pin \"],\"plan\":null,\"file_refs\":[]}";

    [TestMethod]
    public void SAVEAC007_ExplicitWriterPreservesTextAndExactWhitelist()
    {
        using var secrets = new SessionSecrets("synthetic-current-key".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        var value = WorkSaveCodec.Parse(Encoding.UTF8.GetBytes(Minimal), Id, policy);
        Assert.AreEqual(" retained title ", value.Title);
        Assert.AreEqual(" retained pin ", value.Pins.Single());
        Assert.AreEqual(0UL, value.Workspace.Device);
        Assert.AreEqual(Minimal, Encoding.UTF8.GetString(WorkSaveCodec.Write(value, policy)));
        Assert.AreEqual("[WorkSaveDocument]", value.ToString());
    }

    [TestMethod]
    public void SAVEAC007_ParserRejectsMalformedStructureAndNumericSpelling()
    {
        foreach (var invalid in new[]
        {
            Minimal.Replace("\"format_version\":1", "\"format_version\":1.0"),
            Minimal.Replace("\"format_version\":1", "\"format_version\":1e0"),
            Minimal.Replace("\"format_version\":1", "\"format_version\":-0"),
            Minimal.Replace("\"format_version\":1", "\"format_version\":\"1\""),
            Minimal.Replace("\"format_version\":1", "\"format_version\":1,\"format_version\":1"),
            Minimal.Replace("\"pins\":[", "\"unknown\":null,\"pins\":["),
            Minimal.Replace("\"identity\":\"12345\"", "\"identity\":\"0\""),
            Minimal.Replace("\"device\":\"0\"", "\"device\":\"00\""),
            Minimal.Replace("\"device\":\"0\"", "\"device\":\"18446744073709551616\""),
            Minimal.Replace("2026-10-26T08:00:00Z", "2026-02-30T08:00:00Z"),
            Minimal.Replace("2026-10-26T08:00:00Z", "2026-10-26T08:00:00+00:00"),
            Minimal.Replace(" retained title ", "\\uD800"),
            Minimal.Replace(" retained title ", "\\u2028"),
            Minimal.Replace(" retained title ", "   "),
            Minimal + "{}", "```json\n" + Minimal + "\n```", Minimal[..^1] + ",}"
        }) Reject(invalid, "work_format_invalid");
        Reject("\uFEFF" + Minimal, "work_format_invalid");
        Reject(Minimal.Replace("\"format_version\":1", "\"format_version\":2"), "work_version_unsupported");
        Reject(Minimal, "work_format_invalid", new string('a', 32));
        using var secrets = new SessionSecrets("synthetic-current-key".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        var bytes = new byte[] { 255 }.Concat(Encoding.UTF8.GetBytes(Minimal)).ToArray();
        Assert.AreEqual("work_format_invalid", Assert.ThrowsExactly<WorkDocumentException>(() => WorkSaveCodec.Parse(bytes, Id, policy)).Code);
    }

    [TestMethod]
    public void SAVEAC008_PathsRuleAncestorsAndFileReferencesAreExact()
    {
        foreach (var root in new[] { "relative", "/Users/example/../Project", "/Users//example/Project", "/Users/example/Project/" })
            Reject(Edit(x => x["workspace"]!["root"] = root), "work_format_invalid");
        foreach (var path in new[] { "../outside", "./src", "src//", ".env", ".aiagent/work" })
            Reject(Edit(x => x["scope_hint"] = path), "work_format_invalid");
        var hash = new string('a', 64);
        Reject(Edit(x => x["rules_refs"] = JsonNode.Parse("[{\"path\":\"other/AGENTS.md\",\"sha256\":\"" + hash + "\"}]")), "work_format_invalid");
        var valid = WithPlan("read", "[{\"tool\":\"read_file\",\"path\":\"quoted \\\"file\\\".cs\"}]");
        using var secrets = new SessionSecrets("synthetic-current-key".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        Assert.AreEqual("quoted \"file\".cs", WorkSaveCodec.Parse(Encoding.UTF8.GetBytes(valid), Id, policy).FileRefs.Single().Path);
        Reject(valid.Replace("\"file_refs\":[{", "\"file_refs\":[{\"extra\":0,"), "work_format_invalid");
        Reject(Edit(x => x["file_refs"] = JsonNode.Parse("[{\"path\":\"extra.cs\",\"observation\":\"missing\",\"byte_count\":null,\"sha256\":null}]")), "work_format_invalid");
        Reject(valid.Replace("\"observation\":\"missing\"", "\"observation\":\"file\""), "work_format_invalid");
    }

    [TestMethod]
    public void SAVEAC009_PlanHistoryRejectsNormalizationAndInvalidEvidenceCombinations()
    {
        var analysis = WithPlan("analysis", "[]");
        Reject(analysis.Replace("\"objective\":\"objective\"", "\"objective\":\" objective \""), "work_plan_invalid");
        Reject(analysis.Replace("\"title\":\"task\"", "\"title\":\" task \""), "work_plan_invalid");
        Reject(analysis.Replace("\"reported_basis\":\"none\"", "\"reported_basis\":\"model_assessment\""), "work_plan_invalid");
        Reject(analysis.Replace("\"reported_state\":\"pending\"", "\"reported_state\":\"completed\""), "work_plan_invalid");
        Reject(analysis.Replace("\"block_reason\":null", "\"block_reason\":\"model_reported\""), "work_plan_invalid");
        Reject(analysis.Replace("\"reported_state\":\"pending\"", "\"reported_state\":\"blocked\""), "work_plan_invalid");
        Reject(analysis.Replace("\"requirements_version\":1", "\"requirements_version\":2"), "work_plan_invalid");
        Reject(WithPlan("read", "[{\"tool\":\"read_file\",\"path\":\"./a.cs\"}]"), "work_plan_invalid");
        Reject(WithPlan("read", "[{\"tool\":\"read_file_range\",\"path\":\"a.cs\",\"start_line\":2,\"end_line\":1}]"), "work_plan_invalid");
        Reject(WithPlan("write", "[{\"tool\":\"read_file\",\"path\":\"a.cs\"}]"), "work_plan_invalid");
        Reject(WithPlan("read", "[{\"tool\":\"read_file\",\"path\":\"a.cs\"},{\"tool\":\"read_file\",\"path\":\"a.cs\"}]"), "work_plan_invalid");
    }

    [TestMethod]
    public void SAVEAC009_CollectionAndEscapedPinBudgetsRejectWithoutTruncation()
    {
        Reject(Edit(x => x["pins"] = new JsonArray(Enumerable.Range(0, 17).Select(_ => JsonValue.Create("pin")).ToArray())), "work_capacity");
        Reject(Edit(x => x["pins"] = new JsonArray(Enumerable.Range(0, 8).Select(_ => JsonValue.Create(new string('x', 1024))).ToArray())), "work_capacity");
        Reject(Edit(x => x["summary"] = JsonNode.Parse("{\"version\":2,\"goals\":[\"goal\"],\"decisions\":[],\"open_questions\":[],\"background\":[]}")), "work_version_unsupported");
        Reject(Edit(x => x["summary"] = JsonNode.Parse("{\"version\":1,\"goals\":[],\"decisions\":[],\"open_questions\":[],\"background\":[]}")), "work_format_invalid");
        Reject(Edit(x => x["pins"] = new JsonArray(new string('x', 1025))), "work_format_invalid");
        var plan = JsonNode.Parse(WithPlan("analysis", "[]"))!;
        var task = plan["plan"]!["tasks"]![0]!.DeepClone();
        plan["plan"]!["tasks"] = new JsonArray(Enumerable.Range(0, 21).Select(_ => task.DeepClone()).ToArray());
        Reject(plan.ToJsonString(), "work_plan_invalid");
    }

    [TestMethod]
    public void SAVEAC026_DocumentChecksRawDecodedAndCanonicalContent()
    {
        Reject(Minimal.Replace(" retained title ", "synthetic-current-key"), "work_sensitive_content");
        Reject(Minimal.Replace(" retained title ", "synthetic-current-\\u006bey"), "work_sensitive_content");
        Reject(Minimal.Replace(" retained pin ", "ApiKey = forbidden-value"), "work_sensitive_content");
        Reject(Minimal.Replace(" retained pin ", "AI" + "za" + new string('Q', 35)), "work_sensitive_content");
        using var secrets = new SessionSecrets("format_version".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        Assert.AreEqual("work_sensitive_content", Assert.ThrowsExactly<WorkDocumentException>(() => WorkSaveCodec.Parse(Encoding.UTF8.GetBytes(Minimal), Id, policy)).Code);
    }

    private static string Edit(Action<JsonNode> change)
    {
        var node = JsonNode.Parse(Minimal)!;
        change(node);
        return node.ToJsonString();
    }

    private static string WithPlan(string kind, string requirements)
    {
        return Edit(x =>
        {
            x["plan"] = JsonNode.Parse("{\"objective\":\"objective\",\"tasks\":[{\"kind\":\"" + kind + "\",\"title\":\"task\",\"acceptance\":\"acceptance\",\"requirements_version\":1,\"requirements\":" + requirements + ",\"reported_state\":\"pending\",\"reported_basis\":\"none\",\"summary\":null,\"block_reason\":null}]}");
            var paths = JsonNode.Parse(requirements)!.AsArray().Select(r => r!["path"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            x["file_refs"] = new JsonArray(paths.Select(path => (JsonNode)new JsonObject { ["path"] = path, ["observation"] = "missing", ["byte_count"] = null, ["sha256"] = null }).ToArray());
        });
    }

    private static void Reject(string text, string code, string expectedId = Id)
    {
        using var secrets = new SessionSecrets("synthetic-current-key".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        var error = Assert.ThrowsExactly<WorkDocumentException>(() => WorkSaveCodec.Parse(Encoding.UTF8.GetBytes(text), expectedId, policy));
        Assert.AreEqual(code, error.Code);
        Assert.IsFalse(error.Message.Contains("synthetic-current-key", StringComparison.Ordinal));
    }
}
