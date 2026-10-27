using System.Security.Cryptography;

using AIAgent.Cli.Core;

namespace AIAgent.Cli.Workspace;

internal sealed partial class WorkspaceFiles : ISkillsWorkspace
{
    public ISkillsWorkspace SkillsWorkspace => this;
    public void ClearSkillSnapshots() { lock (_gate) _snapshots.InvalidateAllForRootFailure(); }

    public Task<SkillReadResult> ReadSkillAsync(string scope, string id, WorkspaceOperationLease lease, CancellationToken token) =>
        ReadSkillsAsync(scope, id, lease, token);
    public Task<SkillReadResult> ReadSkillCatalogAsync(string scope, WorkspaceOperationLease lease, CancellationToken token) =>
        ReadSkillsAsync(scope, null, lease, token);
    public Task<SkillReadResult> ValidateSkillSourceAsync(SkillSource source, WorkspaceOperationLease lease, CancellationToken token) =>
        ReadSkillAsync(source.Descriptor.Scope, source.Descriptor.Id, lease, token);

    private async Task<SkillReadResult> ReadSkillsAsync(string scope, string? id, WorkspaceOperationLease lease, CancellationToken token)
    {
        if (id is not null && !SkillFormat.ValidId(id)) return new("invalid_skills_command");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lease.CancellationToken, _lifetime.Token);
        var operation = new WorkspaceOperation(_snapshots.Generation, _executionPolicy.Plans.Generation) { Lease = lease };
        try
        {
            RequireRulesLease(lease); _activePlanGeneration = operation.PlanGeneration;
            var rules = _executionPolicy.Rules!;
            var error = await rules.ValidateAsync(linked.Token, lease);
            if (error != "none") return new(error);
            var stamp = rules.Capture(); if (scope != stamp.Scope) return new("rules_scope_mismatch");
            var root = scope == "." ? "skills" : scope + "/skills";
            root = ValidateArgument(root, false);
            var parents = InspectPath(scope, WorkspaceNodeKind.Directory, operation);
            var rootState = _fileSystem.Inspect(Path.Combine(_root, root));
            var observations = new List<SkillPathObservation>();
            observations.AddRange(parents.Select(p => new SkillPathObservation(Path.GetRelativePath(_root, p.Path), p.State)));
            observations.Add(new(root, rootState));
            if (rootState.Kind == WorkspaceNodeKind.Missing || !_fileSystem.HasExactName(Path.Combine(_root, root)))
            {
                Revalidate(parents); RevalidateSkillObservations(observations);
                return id is null ? new("none", catalog: new(scope, [], 0, observations, [])) : new("skill_not_found");
            }
            if (rootState.Kind == WorkspaceNodeKind.Link) throw Rejected(WorkspaceErrorCode.SymbolicLink);
            if (rootState.Kind != WorkspaceNodeKind.Directory) throw Rejected(WorkspaceErrorCode.WrongEntryType);
            var boundary = await CheckProjectRulesPathAsync(scope, root, true, operation.RuleBoundaries, lease, linked.Token);
            if (boundary != WorkspaceErrorCode.None) throw Rejected(boundary);
            if (id is not null)
            {
                var source = await ReadSkillCoreAsync(scope, id, operation, observations, linked.Token);
                RevalidateSkillObservations(observations); RevalidateSkillRules(stamp, operation);
                return source is null ? new("skill_not_found") : new("none", source);
            }
            var members = ReadSkillMembers(root, linked.Token);
            var skills = new List<SkillDescriptor>(); var excluded = 0;
            foreach (var name in members)
            {
                linked.Token.ThrowIfCancellationRequested(); RequireRulesLease(lease);
                var path = root + "/" + name;
                if (WorkspacePathRules.IsSensitive(path)) { excluded++; continue; }
                if (!SkillFormat.ValidId(name)) continue;
                _ = ValidateArgument(path, false);
                var entry = _fileSystem.Inspect(Path.Combine(_root, path)); observations.Add(new(path, entry));
                if (entry.Kind is WorkspaceNodeKind.Link or WorkspaceNodeKind.Other) throw Rejected(entry.Kind == WorkspaceNodeKind.Link ? WorkspaceErrorCode.SymbolicLink : WorkspaceErrorCode.WrongEntryType);
                if (entry.Kind != WorkspaceNodeKind.Directory) continue;
                var source = await ReadSkillCoreAsync(scope, name, operation, observations, linked.Token, skills.Count == SkillsLimits.MaxSkillCatalogSources);
                if (source is not null) skills.Add(source.Descriptor);
            }
            // Single-operation observations stay strict, including directory timestamps and absences.
            RevalidateSkillObservations(observations); RevalidateSkillRules(stamp, operation);
            if (!members.SequenceEqual(ReadSkillMembers(root, linked.Token), StringComparer.Ordinal)) throw Rejected(WorkspaceErrorCode.PathChanged);
            CheckActive(operation.Generation, linked.Token);
            return new("none", catalog: new(scope, skills, excluded, observations, members));
        }
        catch (OperationCanceledException) { throw; }
        catch (WorkspaceFailure failure) { return new(WorkspaceToolCode(failure.Code)); }
        catch (SkillFormatException) { return new("invalid_skill_format"); }
        catch (Exception exception)
        {
            return new(exception switch
            {
                PlatformNotSupportedException => "skills_unavailable",
                UnauthorizedAccessException => "access_denied",
                FileNotFoundException or DirectoryNotFoundException => "path_changed",
                ArgumentException or NotSupportedException => "invalid_path",
                _ => "io_failure"
            });
        }
    }

    private string[] ReadSkillMembers(string root, CancellationToken token)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var iterator = _fileSystem.EnumerateNames(Path.Combine(_root, root)).GetEnumerator();
        var count = 0;
        while (iterator.MoveNext())
        {
            token.ThrowIfCancellationRequested();
            if (++count > SkillsLimits.MaxSkillDirectoryEntries) throw Rejected(WorkspaceErrorCode.SkillsLimit);
            var name = iterator.Current;
            if (name.Contains('/') || !names.Add(name)) throw Rejected(WorkspaceErrorCode.InvalidPath);
        }
        return names.Order(StringComparer.Ordinal).ToArray();
    }

    private async Task<SkillSource?> ReadSkillCoreAsync(string scope, string id, WorkspaceOperation operation,
        List<SkillPathObservation> observations, CancellationToken token, bool sourceLimit = false)
    {
        var directory = (scope == "." ? "skills/" : scope + "/skills/") + id;
        var path = ValidateArgument(directory + "/SKILL.md", true);
        var directoryState = _fileSystem.Inspect(Path.Combine(_root, directory)); observations.Add(new(directory, directoryState));
        if (directoryState.Kind == WorkspaceNodeKind.Missing || !_fileSystem.HasExactName(Path.Combine(_root, directory))) return null;
        var boundaries = new List<ProjectRuleObservation>();
        var boundary = await CheckProjectRulesPathAsync(scope, directory, true, boundaries, operation.Lease!, token);
        if (boundary != WorkspaceErrorCode.None) throw Rejected(boundary);
        var candidate = _fileSystem.Inspect(Path.Combine(_root, path)); observations.Add(new(path, candidate));
        if (candidate.Kind == WorkspaceNodeKind.Missing || !_fileSystem.HasExactName(Path.Combine(_root, path))) return null;
        if (sourceLimit) throw Rejected(WorkspaceErrorCode.SkillsLimit);
        var file = await ReadVerifiedFileAsync(path, operation, token, expected: candidate, maxBytes: SkillsLimits.MaxSkillFileBytes);
        if (!SkillFormat.TryParse(id, file.Content, out var description, out var body)) throw new SkillFormatException();
        var descriptor = new SkillDescriptor(id, description, path, scope, file.Bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(file.Bytes)));
        if (RevalidateProjectRulesPaths(boundaries, operation.Lease!) != WorkspaceErrorCode.None) throw Rejected(WorkspaceErrorCode.RulesScopeMismatch);
        foreach (var item in boundaries) operation.RuleBoundaries.Add(item);
        var sourceObservations = file.Observations.Select(p => new SkillPathObservation(Path.GetRelativePath(_root, p.Path), p.State, p.State.Kind == WorkspaceNodeKind.File ? file.Mode : null)).ToArray();
        observations.AddRange(sourceObservations);
        return new(descriptor, file.Content, body, file.Mode, sourceObservations, boundaries);
    }

    private void RevalidateSkillObservations(IEnumerable<SkillPathObservation> observations)
    {
        foreach (var item in observations)
            if (_fileSystem.Inspect(item.Path == "." ? _root : Path.Combine(_root, item.Path)) != item.State) throw Rejected(WorkspaceErrorCode.PathChanged);
    }
    private void RevalidateSkillRules(ProjectRulesStamp stamp, WorkspaceOperation operation)
    {
        if (!_executionPolicy.Rules!.IsCurrent(stamp)) throw Rejected(WorkspaceErrorCode.RulesChanged);
        var error = RevalidateProjectRulesPaths(operation.RuleBoundaries, operation.Lease!);
        if (error != WorkspaceErrorCode.None) throw Rejected(error);
    }
    private static string WorkspaceToolCode(WorkspaceErrorCode code) => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(code.ToString());
    private sealed class SkillFormatException : IOException { }
}