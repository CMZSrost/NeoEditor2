using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Services;

/// <summary>
/// Encapsulates entity row filtering logic extracted from ModGameDataTabsView.
/// Supports ShowAll toggle, mod filter, and full-text search with col:value syntax.
/// </summary>
public class FilterService : IFilterService
{
    private static readonly Dictionary<Type, PropertyInfo[]> StringPropsCache = new();

    public ObservableCollection<object> ApplyFilters(
        ObservableCollection<object> source,
        Type entityType,
        bool isMergeView,
        bool showAll,
        HashSet<string> overriddenEntityIds,
        int? selectedModId,
        string? filterText)
    {
        var query = source.AsEnumerable();

        // Filter 1: ShowAll (merge view only)
        if (isMergeView && !showAll)
            query = query.Where(item => item is IEntity e && !overriddenEntityIds.Contains(e.EntityId));

        // Filter 2: Mod filter (merge view only)
        if (isMergeView && selectedModId.HasValue)
            query = query.Where(item => item is IEntity e && e.ModId == selectedModId.Value);

        // Filter 3: Text filter (both modes, supports col:value syntax)
        var text = filterText?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var tokens = ParseFilterTokens(text);
            if (tokens.Count > 0)
            {
                var stringProps = GetStringProperties(entityType);
                query = query.Where(item => MatchesAllTokens(item, tokens, stringProps, entityType));
            }
        }

        return new ObservableCollection<object>(query);
    }

    // ── Token parsing ──────────────────────────────────────────────────────

    private static List<FilterToken> ParseFilterTokens(string filterText)
    {
        var tokens = new List<FilterToken>();
        var parts = SplitFilterText(filterText);
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            var colonIdx = part.IndexOf(':');
            if (colonIdx > 0 && colonIdx < part.Length - 1)
            {
                var col = part[..colonIdx].Trim();
                var val = part[(colonIdx + 1)..].Trim();
                if (col.Length > 0 && val.Length > 0)
                    tokens.Add(new FilterToken(col, val, IsColumn: true));
            }
            else
            {
                tokens.Add(new FilterToken(null, part.Trim(), IsColumn: false));
            }
        }
        return tokens;
    }

    private static List<string> SplitFilterText(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    // ── Matching ───────────────────────────────────────────────────────────

    private static bool MatchesAllTokens(object item, List<FilterToken> tokens,
        PropertyInfo[] stringProps, Type entityType)
    {
        foreach (var token in tokens)
        {
            if (token.IsColumn)
            {
                var prop = FindColumnProperty(entityType, token.Column!);
                if (prop is null) return false;
                var val = prop.GetValue(item)?.ToString() ?? "";
                if (!val.Contains(token.Value, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                var found = false;
                foreach (var sp in stringProps)
                {
                    var val = sp.GetValue(item)?.ToString() ?? "";
                    if (val.Contains(token.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
        }
        return true;
    }

    public static PropertyInfo? FindColumnProperty(Type entityType, string columnName)
    {
        foreach (var prop in entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.DeclaringType == typeof(IEntity)) continue;
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr is null) continue;
            if (string.Equals(colAttr.Name, columnName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(prop.Name, columnName, StringComparison.OrdinalIgnoreCase))
                return prop;
        }
        return null;
    }

    public static PropertyInfo[] GetStringProperties(Type entityType)
    {
        if (!StringPropsCache.TryGetValue(entityType, out var props))
        {
            props = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.DeclaringType != typeof(IEntity)
                    && p.GetCustomAttribute<ColumnAttribute>() != null
                    && p.PropertyType == typeof(string))
                .ToArray();
            StringPropsCache[entityType] = props;
        }
        return props;
    }

    private record struct FilterToken(string? Column, string Value, bool IsColumn);
}
