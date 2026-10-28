using System.Globalization;
using System.Text.Json;

namespace AIAgent.Cli.Core;

/// <summary>Local observation of one accepted provider payload; grants no authority.</summary>
internal sealed class WebDocumentSource(string requestedUrl, DateTimeOffset retrievedAtUtc, string safePayloadSha256, string? requestedView)
{
    internal string Observation => "provider_response";
    internal string RequestedUrl { get; } = requestedUrl;
    internal DateTimeOffset RetrievedAtUtc { get; } = retrievedAtUtc.ToUniversalTime();
    internal string SafePayloadSha256 { get; } = safePayloadSha256;
    internal string? RequestedView { get; } = requestedView;
    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject(); writer.WriteString("observation", Observation); writer.WriteString("requested_url", RequestedUrl);
        writer.WriteString("retrieved_at_utc", RetrievedAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
        writer.WriteString("safe_payload_sha256", SafePayloadSha256); writer.WriteString("requested_view", RequestedView); writer.WriteEndObject();
    }
    public override string ToString() => "[WebDocumentSource]";
}