using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.DataViewer.Services;

namespace NeoEditor.Data.Validation;

/// <summary>
/// Checks that [ReferenceField] values resolve to entities present in the currently loaded data.
/// Only checks when the target table IS loaded in ReferenceLookups (skip otherwise).
/// Always issues Warning (never Error) because cross-mod/game-base references are valid.
/// </summary>
public class ReferenceIntegrityRule : IValidationRule
{
    private readonly DataTableService? _dataTableService;

    public ReferenceIntegrityRule(DataTableService? dataTableService)
    {
        _dataTableService = dataTableService;
    }

    public void Validate(IReadOnlyList<IEntity> entities, ValidationReport report)
    {
        if (entities.Count == 0) return;

        foreach (var entity in entities)
        {
            var type = entity.GetType();
            var refProps = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() != null);

            foreach (var prop in refProps)
            {
                var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>()!;
                // R30: ReferenceList must be read as raw text ("3,14"), not "[3, 14]".
                var raw = ReferenceText.GetRawString(prop.GetValue(entity), refAttr);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var targetType = refAttr.TargetEntityType;

                // Skip if target table not loaded in current view
                if (!(_dataTableService?.ReferenceLookups.ContainsKey(targetType) ?? false))
                    continue;

                var separator = refAttr.Separator;
                var pattern = refAttr.Pattern;
                var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

                // R30: split by the FULL separator string — multi-char separators ("],[" for
                // bracket conditions) must not be split char-by-char.
                var segments = separator is not null
                    ? raw.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).Where(s => s.Length > 0)
                    : new[] { raw.Trim() };
                foreach (var seg in segments)
                {
                    CheckSegment(seg, entity, colName, targetType, pattern, refAttr.TargetKey, report);
                }
            }
        }
    }

    private void CheckSegment(string segment, IEntity entity, string colName,
        Type targetType, string? pattern, string? targetKey, ValidationReport report)
    {
        var rawId = ReferenceParser.ExtractRawId(segment, pattern);
        if (string.IsNullOrWhiteSpace(rawId) || rawId == "0") return;

        // Simple int ID: resolve via FindBestMatch
        if (int.TryParse(rawId, out var intId))
        {
            var found = _dataTableService?.ResolveEntityIdByTargetKey(targetType, rawId, targetKey);
            if (found is null && intId > 0)
            {
                report.Warning(entity.Subject, colName,
                    $"Reference '{rawId}' → {targetType.Name}: entity not found in loaded data");
            }
            return;
        }

        // Composite key or namespaced ref: try TargetKey resolution
        var resolved = _dataTableService?.ResolveEntityIdByTargetKey(targetType, rawId, targetKey);
        if (resolved is null)
        {
            report.Warning(entity.Subject, colName,
                $"Reference '{segment}' → {targetType.Name}: target not found in loaded data");
        }
    }
}
