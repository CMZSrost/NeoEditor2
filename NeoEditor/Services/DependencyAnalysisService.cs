using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public record DependencyIssue(string SourceEntity, string SourceMod, string Field,
    string TargetDesc, string Issue);

public class DependencyAnalysisService
{
    /// <summary>Scan all entities for unresolvable references.</summary>
    public List<DependencyIssue> Analyze(IReadOnlyList<IEntity> entities, HashSet<string> loadedModNames)
    {
        var issues = new List<DependencyIssue>();

        foreach (var entity in entities)
        {
            var type = entity.GetType();
            var modName = GenericDataGridHelper.EntityModNames.GetValueOrDefault(entity.EntityId, "?");
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
                    var match = GenericDataGridHelper.FindBestMatch(
                        refAttr.TargetEntityType, rawId, refAttr.TargetKey);

                    // Try secondary if primary fails
                    if (match is null && refAttr.SecondaryTargetEntityType is not null)
                        match = GenericDataGridHelper.FindBestMatch(
                            refAttr.SecondaryTargetEntityType, rawId, refAttr.SecondaryTargetKey);

                    if (match is not null) continue; // reference resolves OK

                    // Unresolvable — check if namespace exists but entity doesn't
                    var colonIdx = rawId.IndexOf(':');
                    var nsPrefix = colonIdx > 0 ? rawId[..colonIdx] : null;
                    var idPart = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;

                    string issue;
                    if (nsPrefix is not null && !loadedModNames.Contains(nsPrefix)
                        && !GenericDataGridHelper.NamespaceToModName.ContainsKey(nsPrefix))
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
