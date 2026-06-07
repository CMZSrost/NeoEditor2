using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

/// <summary>
/// Centralized reference resolution helper. Resolves entity IDs to Subjects
/// using GenericDataGridHelper.ReferenceLookups, with dedup support.
/// </summary>
public static class ReferenceResolver
{
    /// <summary>Get deduped lookup dict for an entity type (highest ModId wins).</summary>
    public static Dictionary<int, T> GetDedupedInt<T>() where T : IEntity
    {
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null)
            return [];
        return list.OfType<T>()
            .GroupBy(e =>
            {
                var keyProp = e.GetType().GetProperty("Id") ?? e.GetType().GetProperty("nID");
                return keyProp?.GetValue(e) is int id ? id : 0;
            })
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.ModId).First());
    }

    /// <summary>Get deduped lookup dict keyed by composite string key.</summary>
    public static Dictionary<string, T> GetDedupedComposite<T>(Func<T, string> keySelector) where T : IEntity
    {
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null)
            return [];
        return list.OfType<T>()
            .GroupBy(keySelector)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.ModId).First());
    }

    /// <summary>Get all entities of a type (deduped: highest ModId per key).</summary>
    public static List<T> GetDedupedList<T>() where T : IEntity
    {
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null)
            return [];
        return list.OfType<T>()
            .GroupBy(e =>
            {
                var keyProp = e.GetType().GetProperty("Id") ?? e.GetType().GetProperty("nID");
                return keyProp?.GetValue(e)?.ToString() ?? e.EntityId;
            })
            .Select(g => g.OrderByDescending(e => e.ModId).First())
            .ToList();
    }

    /// <summary>Resolve a raw ID string to Subject using the given entity type's lookup.</summary>
    public static string ResolveSubject<T>(string rawId, Dictionary<int, T> lookup, string? pattern = null) where T : IEntity
    {
        var actualId = ReferenceHelper.ExtractRawId(rawId, pattern);
        if (int.TryParse(actualId, out var id) && lookup.TryGetValue(id, out var entity))
            return entity.Subject;
        return rawId;
    }

    /// <summary>Parse raw string (separator + pattern), resolve to Subjects.</summary>
    public static List<string> ResolveMultiRef<T>(string raw, string? separator, string? pattern,
        Dictionary<int, T> lookup) where T : IEntity
    {
        if (string.IsNullOrWhiteSpace(raw) || separator is null) return [];
        return raw.Split(separator)
            .Select(s => ResolveSubject(s.Trim(), lookup, pattern))
            .ToList();
    }

    /// <summary>Build a TreeViewItem. If onCtrlClick is set, Ctrl+Click triggers navigation.</summary>
    public static Avalonia.Controls.TreeViewItem CreateNavItem(string text, Avalonia.Media.IBrush? foreground = null,
        Action? onCtrlClick = null)
    {
        var item = new Avalonia.Controls.TreeViewItem
        {
            IsExpanded = true,
            Header = new Avalonia.Controls.TextBlock
            {
                Text = text,
                Foreground = foreground ?? Avalonia.Media.Brushes.Black
            }
        };
        if (onCtrlClick is not null)
        {
            item.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            item.PointerPressed += (_, e) =>
            {
                var keyMod = e.KeyModifiers;
                if ((keyMod & Avalonia.Input.KeyModifiers.Control) != 0)
                    onCtrlClick();
            };
        }
        return item;
    }

    /// <summary>Navigate to an entity by type and ID.</summary>
    public static void NavigateTo(Type entityType, string entityId)
    {
        GenericDataGridHelper.NavigateToByEntityId(entityType, entityId);
    }

    /// <summary>Navigate to an entity by type and int key.</summary>
    public static void NavigateToByKey<T>(int key) where T : IEntity
    {
        var lookup = GetDedupedInt<T>();
        if (lookup.TryGetValue(key, out var entity))
            NavigateTo(typeof(T), entity.EntityId);
    }

    /// <summary>Wire Ctrl+Click navigation on a TreeViewItem.</summary>
    public static void WireNavOnCtrlClick(Avalonia.Controls.TreeViewItem item, Action navigate)
    {
        item.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        item.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0)
                navigate();
        };
    }

    /// <summary>Find all entities that reference a given target entity via [ReferenceField] attributes.</summary>
    public static List<(Type SourceType, string PropName, IEntity SourceEntity)> FindReverseReferences(Type targetType, object targetKeyValue)
    {
        var results = new List<(Type, string, IEntity)>();
        foreach (var (_, srcType) in NeoEditor.Data.Constants.GameTypes)
        {
            var refProps = srcType.GetProperties()
                .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() is not null);
            foreach (var rp in refProps)
            {
                var refAttr = rp.GetCustomAttribute<ReferenceFieldAttribute>()!;
                if (refAttr.TargetEntityType != targetType) continue;
                if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(srcType, out var list) || list is null) continue;

                foreach (var obj in list)
                {
                    if (obj is not IEntity entity) continue;
                    var rawVal = rp.GetValue(entity)?.ToString();
                    if (string.IsNullOrWhiteSpace(rawVal)) continue;

                    var separator = refAttr.Separator;
                    var parts = separator is not null ? rawVal.Split(separator) : [rawVal];
                    foreach (var seg in parts)
                    {
                        var actualId = ReferenceHelper.ExtractRawId(seg.Trim(), refAttr.Pattern);
                        var keyInfo = ReferenceHelper.ParseTargetKey(refAttr.TargetKey);
                        var decomposed = ReferenceHelper.DecomposeId(actualId, keyInfo);
                        if (decomposed.Count == 0) continue;
                        if (decomposed.Values.Any(v => v.ToString() == targetKeyValue.ToString()))
                            results.Add((srcType, rp.Name, entity));
                    }
                }
            }
        }
        return results;
    }
}
