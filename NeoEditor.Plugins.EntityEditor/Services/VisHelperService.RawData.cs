using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.EntityEditor.Services;

/// <summary>
/// Raw-data audit view (R31): full-field table grouped by FieldGroupMetadata
/// sections, with typed rendering — bools color-coded, reference columns resolved
/// to clickable badges, unresolved segments amber-highlighted.
///
/// Key property: reference segments resolve through the SAME context-aware path
/// as the Detail badges (<see cref="IReferenceResolver.LookupRef{T}"/>), so the
/// raw table and the curated view can never disagree (round-30 user feedback).
/// </summary>
public partial class VisHelperService
{
    /// <summary>Audit statistics for the raw-data view.</summary>
    public readonly record struct RawDataStats(int TotalFields, int FieldsWithValue, int UnresolvedRefSegments);

    private static readonly MethodInfo LookupRefMethod =
        typeof(IReferenceResolver).GetMethod(nameof(IReferenceResolver.LookupRef))
        ?? throw new InvalidOperationException("IReferenceResolver.LookupRef not found");

    /// <summary>
    /// Combined collapsible raw-data section: header (with audit stats) + body.
    /// Replaces the former two-child "BuildExpander + Border" pattern.
    /// </summary>
    public Control BuildRawData(IEntity entity)
    {
        var table = BuildRawDataTableInternal(entity, out var stats);
        var body = new Border { IsVisible = false, Child = table, Padding = new Thickness(8) };
        var header = BuildExpander(BuildRawDataLabel(stats), body);
        return new StackPanel { Children = { header, body } };
    }

    /// <summary>Expander label with audit stats: "Raw Data (24 fields · 12 set · 2 unresolved refs)".</summary>
    public string BuildRawDataLabel(RawDataStats stats)
    {
        var unresolved = stats.UnresolvedRefSegments > 0
            ? Loc("Vis.RawUnresolved", stats.UnresolvedRefSegments)
            : "";
        return $"{Loc("Vis.RawData")}  ({Loc("Vis.RawFields", stats.TotalFields, stats.FieldsWithValue)}{unresolved})";
    }

    /// <summary>Compute audit stats without building the UI (tests / other callers).</summary>
    public RawDataStats ComputeRawDataStats(IEntity entity)
    {
        var rows = CollectRawRows(entity);
        return new RawDataStats(rows.Count, rows.Count(r => r.HasValue), CountUnresolvedSegments(entity, rows));
    }

    /// <summary>Full-field table grouped by FieldGroupMetadata sections.</summary>
    public Control BuildRawDataTable(IEntity entity)
        => BuildRawDataTableInternal(entity, out _);

    // ═══════════════ internals ═══════════════

    private sealed record RawRow(
        string ColumnName,
        string Section,
        PropertyInfo Property,
        string RawValue,
        bool HasValue,
        ReferenceFieldAttribute? RefAttr,
        string? Description);

    private Control BuildRawDataTableInternal(IEntity entity, out RawDataStats stats)
    {
        var rows = CollectRawRows(entity);
        stats = new RawDataStats(rows.Count, rows.Count(r => r.HasValue), CountUnresolvedSegments(entity, rows));
        var sections = FieldGroupMetadata.GetSections(entity.GetType());

        var outer = new StackPanel { Spacing = 8 };
        foreach (var section in sections)
        {
            var sectionRows = rows.Where(r => r.Section == section).ToList();
            if (sectionRows.Count == 0) continue;

            var setCount = sectionRows.Count(r => r.HasValue);
            var header = new Border
            {
                Tag = section,
                CornerRadius = new CornerRadius(6),
                Background = Brush.Parse("#0A000000"),
                Padding = new Thickness(10, 5),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = section, FontSize = 11, FontWeight = FontWeight.SemiBold,
                            Foreground = Brush.Parse("#555"), VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = Loc("Vis.RawFields", sectionRows.Count, setCount), FontSize = 10,
                            Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            outer.Children.Add(header);

            var grid = new Grid
            {
                ColumnDefinitions = { new(130, GridUnitType.Pixel), new(1, GridUnitType.Star) }
            };
            int row = 0;
            foreach (var r in sectionRows)
            {
                grid.RowDefinitions.Add(new(GridLength.Auto));

                var keyTb = EditorUIFactory.SelectableText(r.ColumnName, fontSize: 10,
                    foreground: Brush.Parse("#888888"));
                keyTb.Margin = new Thickness(4, 2, 8, 2);
                keyTb.VerticalAlignment = VerticalAlignment.Top;
                // R30: field explanation tooltip (embedded Docs/38 authoritative meaning).
                if (r.Description is not null)
                    ToolTip.SetTip(keyTb, r.Description);
                Grid.SetRow(keyTb, row);
                Grid.SetColumn(keyTb, 0);
                grid.Children.Add(keyTb);

                var valueCell = BuildRawValueCell(entity, r);
                Grid.SetRow(valueCell, row);
                Grid.SetColumn(valueCell, 1);
                grid.Children.Add(valueCell);
                row++;
            }
            outer.Children.Add(grid);
        }
        return outer;
    }

    private Control BuildRawValueCell(IEntity entity, RawRow r)
    {
        if (!r.HasValue)
            return new TextBlock
            {
                Text = "(empty)", FontSize = 10, Foreground = Brush.Parse("#CCC"),
                Margin = new Thickness(0, 2, 4, 2)
            };

        // bool: keep raw "0"/"1" (audit fidelity) but color-code the meaning.
        if (r.Property.PropertyType == typeof(bool))
            return EditorUIFactory.SelectableText(r.RawValue, fontSize: 10,
                fontWeight: FontWeight.Medium,
                foreground: Brush.Parse(r.RawValue == "1" ? "#2E7D32" : "#999"));

        // reference column: resolve each segment to a clickable badge (same path as Detail).
        if (r.RefAttr is not null)
            return BuildRawRefCell(entity, r);

        // plain text: truncate at 100 chars, full value on hover.
        var display = r.RawValue.Length > 100 ? r.RawValue[..100] + "…" : r.RawValue;
        var tb = EditorUIFactory.SelectableText(display, fontSize: 10, foreground: Brush.Parse("#333"));
        if (r.RawValue.Length > 100)
            ToolTip.SetTip(tb, r.RawValue);
        return tb;
    }

    private Control BuildRawRefCell(IEntity entity, RawRow r)
    {
        var wp = new WrapPanel();
        var sep = r.RefAttr!.Separator;
        var segments = sep is null
            ? new[] { r.RawValue }
            : r.RawValue.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        foreach (var seg in segments)
        {
            var s = seg.Trim();
            if (s.Length == 0) continue;

            var rawId = ReferenceParser.ExtractRawId(s, r.RefAttr.Pattern);
            if (string.IsNullOrEmpty(rawId))
            {
                wp.Children.Add(BuildPlainBadge(s, "#F5F5F5", "#999"));
                continue;
            }

            var targetType = r.RefAttr.TargetEntityType;
            // R38: non-entity target types (ImageAsset — file-name refs) are raw text
            // by design — render plainly, do NOT mark them amber "unresolved".
            if (!typeof(IEntity).IsAssignableFrom(targetType))
            {
                wp.Children.Add(BuildPlainBadge(s, "#F5F5F5", "#999"));
                continue;
            }

            var target = ResolveRawSegment(entity, r.Property.Name, targetType, rawId);
            if (target is null && r.RefAttr.SecondaryTargetEntityType is not null)
            {
                targetType = r.RefAttr.SecondaryTargetEntityType;
                target = ResolveRawSegment(entity, r.Property.Name, targetType, rawId);
            }

            if (target is not null)
            {
                // same label augmentation as RefNode.RefNode<T>: append pattern extra info.
                var label = target.Subject ?? rawId;
                var extra = ReferencePattern.FromName(r.RefAttr.Pattern).FormatExtraInfo(s);
                if (!string.IsNullOrEmpty(extra)) label += $" ({extra})";
                wp.Children.Add(BuildResolvedRefBadge(target, targetType, label, s));
            }
            else
            {
                // amber = unresolved reference (audit signal, not an error style).
                wp.Children.Add(BuildPlainBadge(s, "#FFF8E1", "#B45309"));
            }
        }
        return wp;
    }

    /// <summary>Resolve through IReferenceResolver.LookupRef&lt;T&gt; — the canonical,
    /// context-aware path used by RefNode badges (R31 unification).
    /// R38: target types that are NOT IEntity (e.g. ImageAsset — plain file-name
    /// references) violate LookupRef&lt;T&gt;'s generic constraint; they are resolved
    /// by the caller as raw text only, never here.</summary>
    private IEntity? ResolveRawSegment(IEntity source, string propertyName, Type targetType, string rawId)
    {
        if (!typeof(IEntity).IsAssignableFrom(targetType)) return null;
        try
        {
            var m = LookupRefMethod.MakeGenericMethod(targetType);
            return m.Invoke(_resolver, new object?[] { source, propertyName, rawId }) as IEntity;
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Warning(ex,
                "[RawData] LookupRef<{Type}> failed for {Eid}.{Prop} id={RawId}",
                targetType.Name, source.EntityId, propertyName, rawId);
            return null;
        }
    }

    private Control BuildResolvedRefBadge(IEntity target, Type targetType, string label, string rawSegment)
    {
        var badge = BuildPlainBadge(label, "#E8F5E9", "#2E7D32");
        var eid = target.EntityId;
        var src = target;
        badge.Cursor = new Cursor(StandardCursorType.Hand);
        badge.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
            e.Handled = true;
            if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
                _router.RequestPeek(targetType, eid, src);
            else
                _router.NavigateToEntity(targetType, eid, src);
        };

        // hover preview (Doc 21 §7 P6) + raw segment for audit fidelity.
        var tip = new StackPanel { Spacing = 4 };
        tip.Children.Add(BuildRefTooltip(target));
        tip.Children.Add(new TextBlock
        {
            Text = Loc("Vis.RawOriginal", rawSegment),
            FontSize = 9, Foreground = Brush.Parse("#999999")
        });
        ToolTip.SetTip(badge, tip);
        return badge;
    }

    private static Control BuildPlainBadge(string text, string bg, string fg)
        => new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(bg),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(1),
            Child = new TextBlock
            {
                Text = text, FontSize = 11, Foreground = Brush.Parse(fg), TextWrapping = TextWrapping.Wrap
            }
        };

    private List<RawRow> CollectRawRows(IEntity entity)
    {
        var entityType = entity.GetType();
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null
                        && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => p.MetadataToken)
            .ToList();

        var rows = new List<RawRow>(props.Count);
        foreach (var p in props)
        {
            var colName = p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name;
            var val = p.GetValue(entity);
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            // R30: ReferenceList must render as its raw text ("3,14"), not "[3, 14]".
            var strVal = val is bool b ? (b ? "1" : "0") : ReferenceText.GetRawString(val, refAttr);

            rows.Add(new RawRow(
                colName,
                FieldGroupMetadata.GetSection(entityType, colName),
                p,
                strVal,
                !string.IsNullOrWhiteSpace(strVal),
                refAttr,
                FieldDescriptions.GetDescription(entityType, p.Name)));
        }
        return rows;
    }

    private int CountUnresolvedSegments(IEntity entity, List<RawRow> rows)
    {
        int unresolved = 0;
        foreach (var r in rows)
        {
            if (r.RefAttr is null || !r.HasValue) continue;
            // R38: non-entity target types (ImageAsset) are raw-text refs — never "unresolved".
            if (!typeof(IEntity).IsAssignableFrom(r.RefAttr.TargetEntityType)
                && (r.RefAttr.SecondaryTargetEntityType is null
                    || !typeof(IEntity).IsAssignableFrom(r.RefAttr.SecondaryTargetEntityType)))
                continue;
            var sep = r.RefAttr.Separator;
            var segments = sep is null
                ? new[] { r.RawValue }
                : r.RawValue.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                var s = seg.Trim();
                if (s.Length == 0) continue;
                var rawId = ReferenceParser.ExtractRawId(s, r.RefAttr.Pattern);
                if (string.IsNullOrEmpty(rawId)) continue;
                if (ResolveRawSegment(entity, r.Property.Name, r.RefAttr.TargetEntityType, rawId) is null
                    && (r.RefAttr.SecondaryTargetEntityType is null
                        || ResolveRawSegment(entity, r.Property.Name, r.RefAttr.SecondaryTargetEntityType, rawId) is null))
                    unresolved++;
            }
        }
        return unresolved;
    }
}
