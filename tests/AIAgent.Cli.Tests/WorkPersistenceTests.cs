using System.Text;
using System.Text.Json;

using AIAgent.Cli.Security;

namespace AIAgent.Cli.Tests;

[TestClass]
public sealed class WorkPersistenceTests
{
    [TestMethod]
    public void SAVEAC026_CurrentKeyAndDecodedJsonValuesAreRejected()
    {
        using var secrets = new SessionSecrets("current-synthetic".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        Assert.IsFalse(policy.IsSafe("prefix current-synthetic suffix"));
        using var json = JsonDocument.Parse("{\"text\":\"current-\\u0073ynthetic\"}");
        Assert.IsFalse(policy.IsSafeJson(json.RootElement));
        Assert.IsTrue(policy.IsSafe("ordinary historical note"));
    }

    [TestMethod]
    public void SAVEAC026_CredentialShapesAreRejectedAcrossRawEncodingsAndOffsets()
    {
        using var secrets = new SessionSecrets("current-synthetic".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        var shapes = new[] { "AI" + "za" + new string('Q', 35), new string('-', 5) + "BEGIN " + "RSA PRIVATE " + "KEY" + new string('-', 5) };
        foreach (var shape in shapes)
        {
            Assert.IsFalse(policy.IsSafe(shape));
            foreach (var encoding in new Encoding[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode })
            {
                var bytes = encoding.GetBytes("prefix " + shape + " suffix");
                Assert.IsFalse(policy.IsSafe(bytes));
                Assert.IsFalse(policy.IsSafe(new byte[] { 255 }.Concat(bytes).ToArray()));
            }
        }
        Assert.IsTrue(policy.IsSafe("AIza incomplete"));
    }

    [TestMethod]
    public void SAVEAC026_AssignmentNamesBoundariesAndPlaceholdersAreExact()
    {
        using var secrets = new SessionSecrets("current-synthetic".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        foreach (var name in new[] { "ApiKey", "api_key", "Authorization", "Proxy-Authorization", "x-goog-api-key", "access_token", "refresh_token", "client_secret" })
        {
            Assert.IsFalse(policy.IsSafe(name.ToUpperInvariant() + " = other-value"));
            Assert.IsFalse(policy.IsSafe("\"" + name + "\": \"YOUR_API_KEY\", trailing"));
            Assert.IsTrue(policy.IsSafe(name + " = \"YOUR_API_KEY\""));
            Assert.IsTrue(policy.IsSafe(name + " = \t "));
            Assert.IsTrue(policy.IsSafe("prefix_" + name + " = other-value"));
        }
        foreach (var placeholder in new[] { "YOUR_GEMINI_API_KEY", "YOUR_API_KEY", "YOUR_ACCESS_TOKEN", "YOUR_CLIENT_SECRET", "EXAMPLE_ONLY_NOT_A_SECRET", "[REDACTED]" })
            Assert.IsTrue(policy.IsSafe("ApiKey = '" + placeholder + "'"));
        Assert.IsFalse(policy.IsSafe("ApiKey = your_api_key"));
        Assert.IsFalse(policy.IsSafe("ApiKey = YOUR_API_KEY suffix"));
        Assert.IsFalse(policy.IsSafe("first line\nApiKey: value\nlast line"));
        Assert.IsTrue(policy.IsSafe("ApiKeyX = value"));
    }

    [TestMethod]
    public void SAVEAC026_FixedNamesNumbersAndCurrentShortKeyAreNotExempt()
    {
        using var secrets = new SessionSecrets("1".ToCharArray());
        var policy = new WorkPersistencePolicy(new SensitiveDataPolicy(secrets));
        using var number = JsonDocument.Parse("{\"value\":1}");
        Assert.IsFalse(policy.IsSafeJson(number.RootElement));
        using var safe = JsonDocument.Parse("{\"value\":2}");
        Assert.IsTrue(policy.IsSafeJson(safe.RootElement));
        using var fieldSecrets = new SessionSecrets("pins".ToCharArray());
        var fieldPolicy = new WorkPersistencePolicy(new SensitiveDataPolicy(fieldSecrets));
        using var fields = JsonDocument.Parse("{\"pins\":[]}");
        Assert.IsFalse(fieldPolicy.IsSafeJson(fields.RootElement));
    }
}
