using System.Text.Json;

using AIAgent.Cli.Tools;

namespace AIAgent.Cli.Core;

internal enum McpStatus { Disconnected, Ready, Stale }
internal enum McpTransmission { NotSent, SentUnconfirmed, ResponseReceived }
internal enum McpApprovalDecision { Approved, Declined, Cancelled, EndOfInput, Unavailable }

internal static class McpProfile
{
    internal const string ServerId = "learn";
    internal const string Endpoint = "https://learn.microsoft.com/api/mcp";
    internal const string ProtocolVersion = "2026-07-28";
    internal const string DocsTool = "microsoft_docs_search";
    internal const string CodeTool = "microsoft_code_sample_search";
    internal const string DocsName = "mcp__learn__microsoft_docs_search";
    internal const string CodeName = "mcp__learn__microsoft_code_sample_search";
    internal const int MaxWireBytes = 262144;
    internal const int MaxProjectionBytes = 32768;
    internal const int MaxDisplayBytes = 65536;
    internal const int MaxArgumentsBytes = 8192;
    internal static string? LocalName(string remote) => remote == DocsTool ? DocsName : remote == CodeTool ? CodeName : null;
}

internal interface IMcpContentGuard
{
    bool IsSafe(string value);
    bool IsSafe(JsonElement value);
}

internal interface IMcpReadClient : IAsyncDisposable
{
    long ReceivedBytes => 0;
    Task DiscoverAsync(CancellationToken token);
    Task<McpCatalogObservation> ListToolsAsync(CancellationToken token);
    Task<McpCallOutcome> CallAsync(McpCallPermit permit, CancellationToken token);
}

internal sealed class McpToolDescriptor(string remoteName, string description, JsonElement schema, string fingerprint)
{
    internal string RemoteName { get; } = remoteName;
    internal string LocalName { get; } = McpProfile.LocalName(remoteName) ?? throw new McpFailure("mcp_catalog_invalid");
    internal string Description { get; } = description;
    internal JsonElement Schema { get; } = schema.Clone();
    internal string Fingerprint { get; } = fingerprint;
    internal LlmToolDefinition Definition => new(LocalName, "[external_mcp / learn] " + Description, Schema);
    internal bool Same(McpToolDescriptor other) => RemoteName == other.RemoteName && Fingerprint == other.Fingerprint &&
        Description == other.Description && Schema.GetRawText() == other.Schema.GetRawText();
    public override string ToString() => "[McpToolDescriptor]";
}

internal sealed class McpCatalogObservation(IReadOnlyList<McpToolDescriptor> tools, int excluded, long bytes)
{
    internal IReadOnlyList<McpToolDescriptor> Tools { get; } = Array.AsReadOnly(tools.ToArray());
    internal int Excluded { get; } = excluded;
    internal long Bytes { get; } = bytes;
    internal bool Same(McpCatalogObservation other) => Tools.Count == other.Tools.Count && Tools.Zip(other.Tools).All(t => t.First.Same(t.Second));
    public override string ToString() => "[McpCatalogObservation]";
}

internal sealed class McpCatalogSnapshot(long revision, McpCatalogObservation catalog, ProjectRulesStamp? rules)
{
    internal long Revision { get; } = revision;
    internal McpCatalogObservation Catalog { get; } = catalog;
    internal long RulesRevision { get; } = rules?.RulesRevision ?? 0;
    internal long RulesGeneration { get; } = rules?.RulesGeneration ?? 0;
    internal string Scope { get; } = rules?.Scope ?? ".";
    public override string ToString() => "[McpCatalogSnapshot]";
}

internal sealed class McpOperationStamp(long session, long generation, long revision, McpStatus status, McpCatalogSnapshot? snapshot)
{
    internal long SessionGeneration { get; } = session;
    internal long McpGeneration { get; } = generation;
    internal long CatalogRevision { get; } = revision;
    internal McpStatus Status { get; } = status;
    internal McpCatalogSnapshot? Snapshot { get; } = snapshot;
    internal bool Same(McpOperationStamp other) => SessionGeneration == other.SessionGeneration && McpGeneration == other.McpGeneration &&
        CatalogRevision == other.CatalogRevision && Status == other.Status && ReferenceEquals(Snapshot, other.Snapshot);
    public override string ToString() => "[McpOperationStamp]";
}

internal sealed class McpApprovalPreview(string displayBlock)
{
    internal string DisplayBlock { get; } = displayBlock;
    public override string ToString() => "[McpApprovalPreview]";
}

internal sealed class McpCallPermit
{
    private readonly McpCoordinator _issuer;
    internal McpToolDescriptor Tool { get; }
    internal JsonElement Arguments { get; }
    internal long CatalogRevision { get; }
    internal McpCallPermit(McpCoordinator issuer, McpToolDescriptor tool, JsonElement arguments, long revision)
    { _issuer = issuer; Tool = tool; Arguments = arguments.Clone(); CatalogRevision = revision; }
    internal bool Consume(object client) => _issuer.TryConsumePermit(this, client);
    internal bool IsCurrent => _issuer.IsPermitCurrent(this);
    public override string ToString() => "[McpCallPermit]";
}

internal sealed class McpCallOutcome(string errorCode, McpTransmission transmission, JsonElement? response = null,
    bool safeResultAccepted = false, long bytes = 0)
{
    internal string ErrorCode { get; } = errorCode;
    internal McpTransmission Transmission { get; } = transmission;
    internal JsonElement? Response { get; } = response?.Clone();
    internal bool SafeResultAccepted { get; } = safeResultAccepted;
    internal long Bytes { get; } = bytes;
    public override string ToString() => "[McpCallOutcome]";
}

internal sealed class McpOperationResult(string errorCode, string? display = null)
{
    internal string ErrorCode { get; } = errorCode;
    internal string? Display { get; } = display;
    public override string ToString() => "[McpOperationResult]";
}

internal sealed class McpFailure(string code) : Exception("MCP 操作未通過安全閘門")
{
    internal string Code { get; } = code;
}