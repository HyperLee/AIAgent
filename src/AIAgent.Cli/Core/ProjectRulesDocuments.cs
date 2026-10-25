using System.Globalization;
using System.Text;
using System.Text.Json;

using AIAgent.Cli.Security;
using AIAgent.Cli.Workspace;

namespace AIAgent.Cli.Core;

internal static class ProjectRulesDocuments
{
    internal static string? Projection(long revision, ProjectRulesChain chain, ISensitiveDataPolicy policy)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject(); writer.WriteNumber("version", 1); writer.WriteNumber("rules_revision", revision);
            writer.WriteString("scope", chain.Scope); writer.WriteStartArray("sources");
            foreach (var source in chain.Sources)
            {
                writer.WriteStartObject(); writer.WriteString("kind", source.Kind); writer.WriteString("path", source.Path);
                writer.WriteString("scope", source.Scope); writer.WriteNumber("byte_count", source.RawByteCount);
                writer.WriteString("sha256", source.Sha256); writer.WriteString("text", source.Text); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        var bytes = output.ToArray();
        if (bytes.Length > ProjectRulesLimits.MaxProjectRulesProjectionBytes) return null;
        if (!PlanProjection.SafeJson(bytes, policy, ContentBoundary.ModelInput)) throw new ProjectRulesSensitiveException();
        return Encoding.UTF8.GetString(bytes);
    }

    internal static string? Display(ProjectRulesStamp stamp, bool show, ISensitiveDataPolicy policy)
    {
        var output = new StringBuilder("Scope：").Append(FileChangePreviewBuilder.EscapePath(stamp.Scope))
            .Append("；狀態：").Append(stamp.Status).Append("；RulesRevision：")
            .Append(stamp.RulesRevision.ToString(CultureInfo.InvariantCulture)).Append('\n');
        if (stamp.Snapshot is not { } snapshot) output.Append("尚無安全規則快照。\n");
        else
        {
            if (stamp.Status != ProjectRulesStatus.Valid) output.Append("過時規則：不適用於目前操作。\n");
            output.Append("完整規則投影 bytes：").Append(Encoding.UTF8.GetByteCount(snapshot.Projection).ToString(CultureInfo.InvariantCulture)).Append('\n');
            for (var i = 0; i < snapshot.Sources.Count; i++)
            {
                var source = snapshot.Sources[i];
                output.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(". ").Append(FileChangePreviewBuilder.EscapePath(source.Path))
                    .Append("；Scope：").Append(FileChangePreviewBuilder.EscapePath(source.Scope)).Append("；bytes：")
                    .Append(source.RawByteCount.ToString(CultureInfo.InvariantCulture)).Append("；SHA-256：").Append(source.Sha256).Append('\n');
                if (show) output.Append(FileChangePreviewBuilder.EscapePath(source.Text)).Append('\n');
            }
        }
        var text = output.ToString();
        return Encoding.UTF8.GetByteCount(text) <= ProjectRulesLimits.MaxProjectRulesDisplayBytes &&
            policy.CanSendOrUse(text, ContentBoundary.WriteCandidate) ? text : null;
    }
}

internal sealed class ProjectRulesSensitiveException : Exception
{
    internal ProjectRulesSensitiveException() : base("規則未通過敏感檢查") { }
}