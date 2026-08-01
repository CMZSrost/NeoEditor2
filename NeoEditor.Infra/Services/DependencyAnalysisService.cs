using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public record DependencyIssue(string SourceEntity, string SourceMod, string Field,
    string TargetDesc, string Issue);

public class DependencyAnalysisService
{
    private readonly IWorkspaceSession _session;
    private readonly IReferenceResolver _resolver;
    public DependencyAnalysisService(IWorkspaceSession session, IReferenceResolver resolver)
    { _session = session; _resolver = resolver; }

    /// <summary>Scan all entities for unresolvable references.</summary>
    public List<DependencyIssue> Analyze(IReadOnlyList<IEntity> entities, HashSet<string> loadedModNames)
    {
        var issues = new List<DependencyIssue>();
        var emn = _session.Store?.EntityModNames;
        var nsm = _session.Store?.NamespaceToModName;

        foreach (var entity in entities)
        {
            var type = entity.GetType();
            var modName = emn?.GetValueOrDefault(entity.EntityId, "?") ?? "?";
            var sourceLabel = entity.Subject;

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() != null))
            {
                var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>()!;
                var raw = prop.GetValue(entity)?.ToString();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

                // Split multi-value references
                var separators = new List<char>();
                if (refAttr.Separator is not null) separators.Add(refAttr.Separator[0]);
                if (refAttr.Separator == "|" || raw.Contains('|')) separators.Add('|');
                if (refAttr.Separator == "," || raw.Contains(',')) separators.Add(',');
                separators = separators.Distinct().ToList();

                var segments = separators.Count > 0
                    ? raw.Split(separators.ToArray(), StringSplitOptions.RemoveEmptyEntries)
                    : [raw];

                foreach (var seg in segments)
                {
                    var trimmed = seg.Trim('[', ']', ' ');
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    var pat = ReferencePattern.FromName(refAttr.Pattern);
                    var rawId = pat.ExtractRawId(trimmed);
                    if (string.IsNullOrEmpty(rawId)) continue;

                    // Try primary lookup
                    var match = _resolver.LookupRefByRawId(
                        entity, rawId, refAttr.TargetEntityType);

                    // Try secondary if primary fails
                    if (match is null && refAttr.SecondaryTargetEntityType is not null)
                        match = _resolver.LookupRefByRawId(
                            entity, rawId, refAttr.SecondaryTargetEntityType);

                    if (match is not null) continue; // reference resolves OK

                    // Unresolvable — check if namespace exists but entity doesn't
                    var colonIdx = rawId.IndexOf(':');
                    var nsPrefix = colonIdx > 0 ? rawId[..colonIdx] : null;
                    var idPart = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;

                    string issue;
                    if (nsPrefix is not null && !loadedModNames.Contains(nsPrefix)
                        && nsm is not null && !nsm.ContainsKey(nsPrefix))
                    {
                        issue = $"Namespace '{nsPrefix}' not loaded in this profile";
                    }
                    else
                    {
                        issue = idPart.StartsWith('-')
                            ? $"Condition negation (invert) — target {idPart} may be valid at runtime"
                            : $"Target not found in loaded data";
                    }

                    issues.Add(new DependencyIssue(sourceLabel, modName, colName,
                        $"{refAttr.TargetEntityType.Name}:{rawId}", issue));
                }
            }
        }

        return issues;
    }
}
