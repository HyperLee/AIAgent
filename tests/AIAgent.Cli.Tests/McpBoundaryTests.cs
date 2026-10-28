using System.Text.Json;

using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class McpBoundaryTests
{
    [TestMethod]
    [DataRow(8192, true)]
    [DataRow(8193, false)]
    public void MCPAC014_SchemaByteLimitIncludesAnnotationsAndFixedFields(int size, bool accepted)
    {
        const string prefix = "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"additionalProperties\":false,\"title\":\"";
        using var document = JsonDocument.Parse(prefix + new string('x', size - prefix.Length - 2) + "\"}");
        if (accepted) Assert.AreEqual(8192, System.Text.Encoding.UTF8.GetByteCount(McpSchema.Admit(document.RootElement).GetRawText()));
        else Assert.ThrowsExactly<McpFailure>(() => McpSchema.Admit(document.RootElement));
    }

    [TestMethod]
    [DataRow(8192, true)]
    [DataRow(8193, false)]
    public void MCPAC014_ArgumentsHaveIndependentCompactJsonByteLimit(int size, bool accepted)
    {
        var properties = Enumerable.Range(0, 8).ToDictionary(i => "q" + i, _ => new { type = "string" });
        var schema = McpSchema.Admit(JsonSerializer.SerializeToElement(new { type = "object", properties }));
        var arguments = Enumerable.Range(0, 8).ToDictionary(i => "q" + i, i => i == 7 ? "" : new string('x', 1024));
        arguments["q7"] = new string('x', size - JsonSerializer.SerializeToUtf8Bytes(arguments).Length);
        var value = JsonSerializer.SerializeToElement(arguments);
        Assert.AreEqual(size, System.Text.Encoding.UTF8.GetByteCount(value.GetRawText()));
        Assert.AreEqual(accepted, McpSchema.ValidArguments(schema, value));
    }

    [TestMethod]
    [DataRow(1024, false, true)]
    [DataRow(1025, false, false)]
    [DataRow(512, true, true)]
    [DataRow(513, true, false)]
    public void MCPAC007_Utf16LimitIsSeparateFromUnicodeScalarLength(int count, bool emoji, bool accepted)
    {
        var schema = McpSchema.Admit(GeminiFixtures.Element("{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\",\"maxLength\":2048}}}"));
        var value = string.Concat(Enumerable.Repeat(emoji ? "😀" : "中", count));
        Assert.AreEqual(accepted, McpSchema.ValidArguments(schema, JsonSerializer.SerializeToElement(new { query = value })));
    }

    [TestMethod]
    [DataRow("{\"type\":1,\"properties\":{\"q\":{\"type\":\"string\"}}}")]
    [DataRow("{\"type\":\"object\",\"properties\":{\"q\":{\"type\":\"string\",\"minLength\":\"1\"}}}")]
    [DataRow("{\"type\":\"object\",\"properties\":{\"q\":{\"type\":\"string\",\"enum\":[\"a\",\"\\u0061\"]}}}")]
    public void MCPAC007_MalformedSchemaAndSemanticallyDuplicateEnumsAreControlled(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.ThrowsExactly<McpFailure>(() => McpSchema.Admit(document.RootElement));
    }

    [TestMethod]
    public void MCPAC024_SensitiveValuesCannotEscapeThroughNumbersOrJoinedFields()
    {
        using var session = GeminiFixtures.Session();
        var guard = new McpContentGuard(session.Policy, ["SYNTHETIC_PROTECTED_MARKER"]);
        using var instruction = JsonDocument.Parse("{\"a\":\"SYNTHETIC_PROTECTED_MARKER\"}");
        Assert.IsFalse(guard.IsSafe(instruction.RootElement));
        Assert.IsFalse(guard.IsSafe("prefix" + GeminiFixtures.Key + "suffix"));
        using var split = JsonDocument.Parse(JsonSerializer.Serialize(new[] { GeminiFixtures.Key[..5], GeminiFixtures.Key[5..] }));
        Assert.IsFalse(guard.IsSafe(split.RootElement));
        using var splitObject = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, string> { ["a"] = GeminiFixtures.Key[..5], ["b"] = GeminiFixtures.Key[5..] }));
        Assert.IsFalse(guard.IsSafe(splitObject.RootElement));
        Assert.IsTrue(guard.IsSafe("public documentation query"));
    }

    [TestMethod]
    [DataRow("12", "{\"number\":12}")]
    [DataRow("12", "{\"a\":1,\"b\":2}")]
    [DataRow("xy", "{\"x\":\"y\"}")]
    public void MCPAC024_ShortKeysRejectDecodedNumericAndJoinedNameValueProjections(string key, string json)
    {
        using var session = WorkspaceFixtures.Session(key: key);
        var guard = new McpContentGuard(session.Policy, []);
        using var document = JsonDocument.Parse(json);
        Assert.IsFalse(guard.IsSafe(document.RootElement));
    }

    [TestMethod]
    public void MCPAC007_SchemaRetainsNullableEnumAndRejectsUnknownOrInvalidArguments()
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"query\":{\"type\":[\"string\",\"null\"],\"minLength\":1,\"enum\":[\"😀\",null]}},\"required\":[\"query\"],\"additionalProperties\":true}");
        var accepted = McpSchema.Admit(schema.RootElement);
        Assert.IsFalse(accepted.GetProperty("additionalProperties").GetBoolean());
        using var valid = JsonDocument.Parse("{\"query\":\"😀\"}");
        using var invalid = JsonDocument.Parse("{\"query\":\"x\"}");
        using var unknown = JsonDocument.Parse("{\"query\":null,\"extra\":\"x\"}");
        Assert.IsTrue(McpSchema.ValidArguments(accepted, valid.RootElement));
        Assert.IsFalse(McpSchema.ValidArguments(accepted, invalid.RootElement));
        Assert.IsFalse(McpSchema.ValidArguments(accepted, unknown.RootElement));
    }

    [TestMethod]
    [DataRow("pattern", "\".*\"")]
    [DataRow("x-mcp-header", "\"Custom\"")]
    [DataRow("$ref", "\"#/x\"")]
    [DataRow("minimum", "0")]
    public void MCPAC007_UnsupportedSchemaIsNeverSilentlyWeakened(string keyword, string value)
    {
        using var schema = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\",\"" + keyword + "\":" + value + "}}}");
        Assert.ThrowsExactly<McpFailure>(() => McpSchema.Admit(schema.RootElement));
    }

    [TestMethod]
    public void MCPAC008_FingerprintIgnoresObjectOrderButPreservesArrayAndMetadataValues()
    {
        using var a = JsonDocument.Parse("{\"a\":1,\"b\":[\"x\",\"y\"]}");
        using var b = JsonDocument.Parse("{\"b\":[\"x\",\"y\"],\"a\":1}");
        using var c = JsonDocument.Parse("{\"a\":1,\"b\":[\"y\",\"x\"]}");
        Assert.AreEqual(McpJson.Fingerprint(a.RootElement), McpJson.Fingerprint(b.RootElement));
        Assert.AreNotEqual(McpJson.Fingerprint(a.RootElement), McpJson.Fingerprint(c.RootElement));
    }
}