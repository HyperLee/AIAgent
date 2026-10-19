using System.Text.Json;
using AIAgent.Cli.Core;
using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class SecurityTests
{
    internal const string FakeKey = "FAKE_KEY_FOR_TESTS_ONLY";

    [TestMethod]
    public void AC011_ExactDetectionAllBoundariesAndDisplay()
    {
        using var secrets = new SessionSecrets(FakeKey.ToCharArray());
        var policy = new SensitiveDataPolicy(secrets);
        foreach (var content in new[] { FakeKey, "before" + FakeKey + "after", FakeKey + FakeKey })
        {
            Assert.IsTrue(policy.ContainsCurrentKey(content), "AC-011");
            foreach (var boundary in Enum.GetValues<ContentBoundary>()) Assert.IsFalse(policy.CanSendOrUse(content, boundary), "AC-011");
            Assert.IsFalse(policy.RedactForDisplay(content).Contains(FakeKey, StringComparison.Ordinal), "AC-011");
        }
        Assert.IsTrue(policy.RedactForDisplay(FakeKey + FakeKey) == "[REDACTED][REDACTED]", "AC-011");
        Assert.IsFalse(policy.ContainsCurrentKey(FakeKey.ToLowerInvariant()), "AC-011");
        Assert.IsTrue(policy.CanSendOrUse("normal", ContentBoundary.ModelInput), "AC-011");
        Assert.IsFalse(policy.CanSendOrUse("normal", (ContentBoundary)999), "AC-011");
    }

    [TestMethod]
    public void AC012_StructuredCredentialValuesAreEntirelyRedacted()
    {
        using var secrets = new SessionSecrets(FakeKey.ToCharArray());
        var policy = new SensitiveDataPolicy(secrets);
        foreach (var field in new[] { "Authorization", "Proxy-Authorization", "x-goog-api-key", "ApiKey", "api_key", "key", "access_token", "refresh_token", "client_secret" })
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { nested = new[] { new Dictionary<string, object> { [field.ToUpperInvariant()] = new { value = "FAKE_OTHER_CREDENTIAL_ONLY" }, ["text"] = FakeKey } } }));
            var redacted = policy.RedactDiagnostic(json.RootElement);
            using var parsed = JsonDocument.Parse(redacted);
            Assert.IsTrue(parsed.RootElement.GetProperty("nested")[0].GetProperty(field.ToUpperInvariant()).GetString() == "[REDACTED]", "AC-012");
            Assert.IsFalse(redacted.Contains("FAKE_", StringComparison.Ordinal), "AC-012");
        }
    }

    [TestMethod]
    public void AC010_OwnedMemoryClearedSerializationRejectedAndDisposedAccessFails()
    {
        var owned = FakeKey.ToCharArray();
        var secrets = new SessionSecrets(owned);
        Assert.IsTrue(secrets.AuthenticationKey.SequenceEqual(FakeKey), "AC-010");
        Assert.IsTrue(secrets.ToString() == "[REDACTED]", "AC-010");
        try { JsonSerializer.Serialize(secrets); Assert.Fail("AC-010"); }
        catch (NotSupportedException) { }
        secrets.Dispose();
        secrets.Dispose();
        Assert.IsTrue(owned.All(c => c == '\0'), "AC-010");
        try { _ = secrets.AuthenticationKey.Length; Assert.Fail("AC-010"); }
        catch (ObjectDisposedException) { }
    }

    [TestMethod]
    public void AC011_EmptyKeyCannotCreateSecrets()
    {
        try { using var secrets = new SessionSecrets([]); Assert.Fail("AC-011"); }
        catch (ArgumentException) { }
    }
}
