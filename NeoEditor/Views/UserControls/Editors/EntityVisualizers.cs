using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Views.UserControls.Editors;

// ══════════════════════════════════════════════════════════════════════════════
// Shared helpers
// ══════════════════════════════════════════════════════════════════════════════

file static class VisHelper
{
    private static IImageService? _imgSvc;
    public static IImageService ImageService =>
        _imgSvc ??= App.ServiceProvider!.GetRequiredService<IImageService>();

    /// <summary>Localization shortcut.</summary>
    public static string Loc(string key) => App.Localizor[key];

    public static TreeViewItem Section(string text, IBrush? fg = null)
    {
        var tb = new TextBlock { Text = text, FontWeight = FontWeight.Bold, Foreground = fg ?? Brushes.DodgerBlue };
        return new TreeViewItem { IsExpanded = true, Header = tb };
    }

    public static TreeViewItem Leaf(string text, IBrush? fg = null)
    {
        var tb = new TextBlock { Text = text, Foreground = fg ?? Brushes.Black, TextWrapping = TextWrapping.Wrap };
        return new TreeViewItem { IsExpanded = true, Header = tb };
    }

    public static TreeViewItem NavLeaf(string text, Action nav, IBrush? fg = null)
    {
        var item = Leaf(text, fg);
        item.Cursor = new Cursor(StandardCursorType.Hand);
        item.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) nav(); };
        return item;
    }

    public static TreeViewItem RefNode<T>(string raw, string? separator, string? pattern, string? targetKey,
        string label, IBrush fg) where T : IEntity
    {
        var node = Section(label, fg);
        if (string.IsNullOrWhiteSpace(raw)) { node.Items.Add(Leaf("(None)", Brushes.Gray)); return node; }

        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null)
        { node.Items.Add(Leaf(raw, Brushes.Gray)); return node; }

        var parts = separator is not null ? raw.Split(separator) : [raw];
        foreach (var seg in parts)
        {
            var s = seg.Trim(); if (string.IsNullOrEmpty(s)) continue;
            var idStr = ReferenceParser.ExtractRawId(s, pattern);
            var match = GenericDataGridHelper.FindBestMatch(typeof(T), idStr, targetKey);
            var display = match?.Subject ?? idStr;
            var extra = ReferencePattern.FromName(pattern).FormatExtraInfo(s);
            if (!string.IsNullOrEmpty(extra)) display += $" ({extra})";
            var leaf = match is not null
                ? NavLeaf(display, () => ReferenceResolver.Instance.NavigateTo(typeof(T), match.EntityId), fg)
                : Leaf(display, Brushes.Gray);
            node.Items.Add(leaf);
        }
        return node;
    }

    /// <summary>Try to find and load an image, return null if not found.</summary>
    public static Bitmap? LoadImage(string? imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName)) return null;
        var name = StripNs(imageName.Trim());
        var path = ImageService.FindImage(name);
        if (path is null) return null;
        try { return new Bitmap(path); } catch { return null; }
    }

    public static string StripNs(string name)
    {
        var c = name.IndexOf(':'); return c > 0 ? name[(c + 1)..] : name;
    }

    /// <summary>Build a compact overview header with optional image thumbnail.</summary>
    public static StackPanel OverviewHeader(IEntity entity, Bitmap? thumb = null, string? subtitle = null)
    {
        var sp = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (thumb is not null)
            header.Children.Add(new Image { Source = thumb, MaxWidth = 48, MaxHeight = 48, Stretch = Stretch.Uniform });

        var textCol = new StackPanel { Spacing = 2 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        titleRow.Children.Add(new TextBlock { Text = entity.Subject ?? $"[{entity.GetType().Name}]", FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        // Mod badge: "mid:modName"
        var modName = Helper.GenericDataGridHelper.EntityModNames.TryGetValue(entity.EntityId, out var mn)
            ? mn : $"mod_{entity.ModId}";
        titleRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#20000000"),
            Padding = new Thickness(5, 1),
            Child = new TextBlock { Text = $"{entity.ModId}:{modName}", FontSize = 9, Foreground = Brush.Parse("#888") }
        });
        textCol.Children.Add(titleRow);
        if (subtitle is not null)
            textCol.Children.Add(new TextBlock { Text = subtitle, FontSize = 10, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
        header.Children.Add(textCol);
        sp.Children.Add(header);
        return sp;
    }

    public static TextBlock Kv(string key, string value, int keyWidth = 90)
        => new()
        {
            Text = $"{key}: {value}",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1)
        };

    public static ScrollViewer Wrap(Control content)
        => new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

    // ═══════════════ Shared layout primitives ═══════════════

    public static Border Card(Control content) => new()
    {
        CornerRadius = new CornerRadius(8),
        Background = Brush.Parse("#08000000"),
        BorderBrush = Brush.Parse("#18000000"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(14),
        Child = content
    };

    public static TextBlock SectionLabel(string text) => new()
    {
        Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold,
        Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 8)
    };

    public static Border Separator() => new()
    {
        Height = 1, Background = Brush.Parse("#18000000"), Margin = new Thickness(4, 2)
    };

    public static Border MiniBadge(string text, string bg, string fg, Action? onClick = null)
    {
        var tb = new TextBlock { Text = text, FontSize = 10, Foreground = Brush.Parse(fg), Padding = new Thickness(7, 2) };
        var badge = new Border { CornerRadius = new CornerRadius(9), Background = Brush.Parse(bg), Child = tb };
        if (onClick is not null)
        {
            badge.Cursor = new Cursor(StandardCursorType.Hand);
            badge.PointerPressed += (_, e) =>
            { if ((e.KeyModifiers & KeyModifiers.Control) != 0) onClick(); };
        }
        return badge;
    }

    /// <summary>Build a compact key-value table Grid for any entity's raw column data.</summary>
    public static Control BuildRawDataTable(IEntity entity)
    {
        var entityType = entity.GetType();
        var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>() != null
                && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => p.MetadataToken)
            .ToList();

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new(130, GridUnitType.Pixel),
                new(1, GridUnitType.Star)
            }
        };

        int row = 0;
        foreach (var p in props)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));

            var val = p.GetValue(entity);
            var colName = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ?? p.Name;
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            var strVal = val is bool b ? (b ? "1" : "0") : val?.ToString() ?? "";

            var isRef = refAttr is not null && !string.IsNullOrWhiteSpace(strVal);
            var display = strVal.Length > 100 ? strVal[..100] + "..." : strVal;
            if (string.IsNullOrWhiteSpace(strVal)) display = "(empty)";

            var keyTb = new TextBlock
            {
                Text = colName,
                FontSize = 10,
                Foreground = Brush.Parse("#888888"),
                Margin = new Thickness(4, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Top,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(keyTb, row); Grid.SetColumn(keyTb, 0);
            grid.Children.Add(keyTb);

            var valTb = new TextBlock
            {
                Text = display,
                FontSize = 10,
                Foreground = isRef ? Brush.Parse("#00796B") : string.IsNullOrWhiteSpace(strVal) ? Brush.Parse("#CCC") : Brush.Parse("#333"),
                Margin = new Thickness(0, 2, 4, 2),
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = isRef ? FontWeight.Medium : FontWeight.Normal
            };
            Grid.SetRow(valTb, row); Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            row++;
        }

        return grid;
    }

    // ═══════════════ Shared layout primitives (moved from AttackMode) ═══════════════

    public static Control StatBar(string label, string valueText, double fillRatio, string colorHex)
    {
        fillRatio = Math.Clamp(fillRatio, 0.05, 1.0);
        var grid = new Grid { Height = 26 };
        grid.ColumnDefinitions.Add(new(80, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(2, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new(3, GridUnitType.Star));

        var labelTb = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(labelTb, 0); grid.Children.Add(labelTb);

        var fillStar = Math.Max((int)(fillRatio * 100), 6);
        var emptyStar = Math.Max(100 - fillStar, 0);
        grid.ColumnDefinitions[1] = new(fillStar, GridUnitType.Star);
        grid.ColumnDefinitions[2] = new(emptyStar, GridUnitType.Star);

        var fill = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = Brush.Parse(colorHex),
            Margin = new Thickness(0, 1),
            Child = new TextBlock
            {
                Text = valueText, FontSize = 10, Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0)
            }
        };
        Grid.SetColumn(fill, 1); grid.Children.Add(fill);
        return grid;
    }

    /// <summary>StatBar with 0 at center — fills left for negative values, right for positive.</summary>
    public static Control CenteredStatBar(string label, string valueText, double value, double maxAbs,
        string? posColor = null, string? negColor = null)
    {
        posColor ??= "#2E7D32"; negColor ??= "#C62828";
        var absRatio = Math.Clamp(Math.Abs(value) / Math.Max(maxAbs, 0.01), 0.08, 1.0);
        var isNeg = value < 0;

        var grid = new Grid { Height = 26 };
        grid.ColumnDefinitions.Add(new(80, GridUnitType.Pixel));   // label
        grid.ColumnDefinitions.Add(new(56, GridUnitType.Pixel));   // value text
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));     // left fill
        grid.ColumnDefinitions.Add(new(3, GridUnitType.Pixel));    // center zero line
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));     // right fill

        var labelTb = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(labelTb, 0); grid.Children.Add(labelTb);

        // Value text — always visible, between label and bar
        var valTb = new TextBlock
        {
            Text = valueText, FontSize = 10, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(isNeg ? negColor : value > 0 ? posColor : "#999"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0)
        };
        Grid.SetColumn(valTb, 1); grid.Children.Add(valTb);

        // Center zero marker
        var center = new Border { Background = Brush.Parse("#20000000"), Margin = new Thickness(0, 4) };
        Grid.SetColumn(center, 3); grid.Children.Add(center);

        if (isNeg)
        {
            var fill = new Border
            {
                CornerRadius = new CornerRadius(4, 0, 0, 4),
                Background = Brush.Parse(negColor),
                Margin = new Thickness(0, 1, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = absRatio * 160, MaxWidth = 160
            };
            Grid.SetColumn(fill, 2); grid.Children.Add(fill);
        }
        else if (value > 0)
        {
            var fill = new Border
            {
                CornerRadius = new CornerRadius(0, 4, 4, 0),
                Background = Brush.Parse(posColor),
                Margin = new Thickness(0, 1, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = absRatio * 160, MaxWidth = 160
            };
            Grid.SetColumn(fill, 4); grid.Children.Add(fill);
        }

        return grid;
    }

    /// <summary>Shared reverse-references panel — shows all entities that reference the given entity.</summary>
    public static Control BuildReverseRefsPanel(string entityId)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return new StackPanel();
        var rawRefs = store.Index.ReverseLookup(entityId);
        if (rawRefs.Count == 0) return new StackPanel();

        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid)>();
        foreach (var (srcEid, _, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var m = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (m != null) { resolved.Add((t, m.Subject, srcEid)); break; }
            }
        }
        if (resolved.Count == 0) return new StackPanel();

        var sp = new StackPanel();
        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(SectionLabel($"{Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var wp = new WrapPanel();
        foreach (var (srcType, srcSubject, srcEid) in resolved)
        {
            wp.Children.Add(MiniBadge($"{srcType.Name}: {srcSubject}", "#E8F5E9", "#2E7D32",
                () => ReferenceResolver.Instance.NavigateTo(srcType, srcEid)));
        }
        sp.Children.Add(Card(wp));
        return sp;
    }

    public static Control BuildExpander(string label, Border body)
    {
        var arrow = new TextBlock { Text = "▶", FontSize = 10, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center };
        var labelTb = new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#888"), VerticalAlignment = VerticalAlignment.Center };
        var expanded = false;
        var header = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#06000000"),
            Padding = new Thickness(12, 6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { arrow, labelTb } }
        };
        header.PointerPressed += (_, _) =>
        {
            expanded = !expanded;
            arrow.Text = expanded ? "▼" : "▶";
            body.IsVisible = expanded;
        };
        return header;
    }

    public static TextBlock OvSectionLabel(string text) => new()
    {
        Text = text, FontSize = 10, FontWeight = FontWeight.SemiBold,
        Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 4)
    };

    /// <summary>Build a compact stat grid with key-value pairs in a Card.</summary>
    public static Control BuildStatCard(List<(string label, string value, string? color)> rows)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(90, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(4, 0)
        };
        for (int i = 0; i < rows.Count; i++)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));
            var (label, value, color) = rows[i];
            var lbl = new TextBlock { Text = label, FontSize = 10, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 1) };
            var val = new TextBlock { Text = value, FontSize = 10, FontWeight = FontWeight.Medium, Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1) };
            Grid.SetRow(lbl, i); Grid.SetColumn(lbl, 0);
            Grid.SetRow(val, i); Grid.SetColumn(val, 1);
            grid.Children.Add(lbl); grid.Children.Add(val);
        }
        return Card(grid);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Default — fallback for any unregistered entity type
// ══════════════════════════════════════════════════════════════════════════════

public class DefaultEntityVisualizer : IEntityVisualizer
{
    public Type EntityType { get; }
    public DefaultEntityVisualizer(Type type) => EntityType = type;

    public Control BuildDetail(IEntity entity)
    {
        var tree = new TreeView();
        var root = VisHelper.Section(entity.Subject ?? $"[{entity.GetType().Name}]", Brushes.DodgerBlue);

        var props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>() != null
                && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => p.MetadataToken);

        foreach (var p in props)
        {
            var val = p.GetValue(entity);
            var colName = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ?? p.Name;
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            var strVal = val is bool b ? (b ? "1" : "0") : val?.ToString() ?? "";

            if (refAttr is not null && !string.IsNullOrWhiteSpace(strVal))
            {
                var display = strVal.Length > 100 ? strVal[..100] + "..." : strVal;
                root.Items.Add(VisHelper.Leaf($"→ {colName}: {display}", Brushes.Teal));
            }
            else if (!string.IsNullOrWhiteSpace(strVal))
            {
                var display = strVal.Length > 100 ? strVal[..100] + "..." : strVal;
                root.Items.Add(VisHelper.Leaf($"{colName}: {display}"));
            }
        }
        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        var sp = VisHelper.OverviewHeader(entity);
        var props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>() != null
                && p.DeclaringType != typeof(IEntity))
            .Take(8);
        foreach (var p in props)
        {
            var val = p.GetValue(entity)?.ToString() ?? "";
            if (val.Length > 50) val = val[..50] + "...";
            if (string.IsNullOrEmpty(val)) continue;
            var col = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ?? p.Name;
            sp.Children.Add(VisHelper.Kv(col, val));
        }
        return VisHelper.Wrap(sp);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ItemType — image-first card layout with gallery, bar stats, resolved refs
// ══════════════════════════════════════════════════════════════════════════════

public class ItemTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ItemType);

    private static readonly Dictionary<int, string> SlotNames = new()
    {
        [20] = "L-Hand", [21] = "R-Hand", [22] = "Back", [23] = "Head",
        [14] = "R-Shoulder", [17] = "Face", [13] = "L-Back",
        [11] = "Torso", [4] = "Legs", [2] = "L-Foot", [3] = "R-Foot"
    };

    // ═══════════════ Detail ═══════════════

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ItemType it) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(it), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(it));

        var hasStats = it.Weight > 0 || it.StackLimit > 0 || it.Durability > 0 || it.MonetaryValue > 0 || it.Mirrored || it.SlotDepth > 0;
        if (hasStats)
            root.Children.Add(BuildStatsPanel(it));

        if (!string.IsNullOrWhiteSpace(it.Properties))
            root.Children.Add(BuildPropertiesPanel(it));

        if (!string.IsNullOrWhiteSpace(it.AttackModes))
            root.Children.Add(BuildAttackModesPanel(it));

        var hasEquip = !string.IsNullOrWhiteSpace(it.EquipSlots) ||
            !string.IsNullOrWhiteSpace(it.UseSlots) ||
            it.SocketLocked ||
            !string.IsNullOrWhiteSpace(it.EquipConditions) ||
            !string.IsNullOrWhiteSpace(it.UseConditions) ||
            !string.IsNullOrWhiteSpace(it.PossessConditions);
        if (hasEquip)
            root.Children.Add(BuildEquipmentCard(it));

        if (!string.IsNullOrWhiteSpace(it.ChargeProfiles))
            root.Children.Add(BuildChargeCard(it));

        var hasContainer = !string.IsNullOrWhiteSpace(it.Capacities) ||
            (!string.IsNullOrWhiteSpace(it.FormatId) && it.FormatId != "3") ||
            !string.IsNullOrWhiteSpace(it.ContentIds);
        if (hasContainer)
            root.Children.Add(BuildContainerCard(it));

        if (it.DegradePerHour > 0 || it.EquipDegradePerHour > 0 || it.DegradePerUse > 0 ||
            (!string.IsNullOrWhiteSpace(it.DegradeTreasureIds) && it.DegradeTreasureIds != "3,3"))
            root.Children.Add(BuildDegradeCard(it));

        if (!string.IsNullOrWhiteSpace(it.SwitchIds))
            root.Children.Add(BuildSwitchesPanel(it));

        root.Children.Add(BuildRefsPanel(it));
        root.Children.Add(BuildReverseRefsPanel(it));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Hero header: switchable image gallery (left) + identity (right) ═══════════════

    private static Control BuildHeroHeader(ItemType it)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var imageNames = (it.ImageList ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        var isImageList = (it.ImageList ?? "").Contains(',');

        // ── Image area (top-left) ──
        var imageArea = new Border
        {
            Width = 132, Height = 132,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Background = Brush.Parse("#0A000000"),
            VerticalAlignment = VerticalAlignment.Top
        };
        if (!isImageList && imageNames.Count == 1)
        {
            var bmp = VisHelper.LoadImage(imageNames[0]);
            if (bmp is not null)
                imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        }
        else if (imageNames.Count > 0)
            imageArea.Child = BuildImageGallery(imageNames);
        Grid.SetColumn(imageArea, 0); Grid.SetRowSpan(imageArea, 2);
        grid.Children.Add(imageArea);

        // ── Identity (right) ──
        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = $"{it.GroupId}.{it.SubgroupId}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") }
        });
        identity.Children.Add(new TextBlock { Text = it.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(it.Description) && it.Description != it.Name)
            identity.Children.Add(new TextBlock { Text = it.Description, FontSize = 12, Foreground = Brush.Parse("#666666"), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(it.DescriptionAlt))
            identity.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#FFF3E0"),
                Padding = new Thickness(8, 3),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new StackPanel { Spacing = 1, Children = {
                    new TextBlock { Text = $"✦ {VisHelper.Loc("Vis.Identified")}", FontSize = 9, Foreground = Brush.Parse("#E65100") },
                    new TextBlock { Text = it.DescriptionAlt, FontSize = 11, Foreground = Brush.Parse("#BF360C"), TextWrapping = TextWrapping.Wrap }
                }}
            });
        Grid.SetColumn(identity, 1); Grid.SetRow(identity, 0);
        Grid.SetRowSpan(identity, 2);
        grid.Children.Add(identity);

        return VisHelper.Card(grid);
    }

    private static Control BuildImageGallery(List<string> names)
    {
        var idx = 0;
        var bmps = names.Select(VisHelper.LoadImage).Where(b => b is not null).Cast<Bitmap>().ToList();
        if (bmps.Count == 0) return new TextBlock { Text = "No images", FontSize = 10, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

        var imageView = new Image { Source = bmps[0], Stretch = Stretch.Uniform, Width = 132, Height = 106 };

        // Navigation dots + prev/next
        var nav = new DockPanel { Height = 26, Background = Brush.Parse("#14000000"), LastChildFill = true };
        var prevBtn = new Button { Content = "◀", FontSize = 9, Padding = new Thickness(4, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        var nextBtn = new Button { Content = "▶", FontSize = 9, Padding = new Thickness(4, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        var dotPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
        var dots = new List<Border>();
        for (int i = 0; i < bmps.Count; i++)
        {
            var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = i == 0 ? Brush.Parse("#666") : Brush.Parse("#CCC") };
            dots.Add(dot); dotPanel.Children.Add(dot);
        }

        void UpdateView(int newIdx)
        {
            idx = ((newIdx % bmps.Count) + bmps.Count) % bmps.Count;
            imageView.Source = bmps[idx];
            for (int i = 0; i < dots.Count; i++) dots[i].Background = Brush.Parse(i == idx ? "#666" : "#CCC");
        }

        prevBtn.Click += (_, _) => UpdateView(idx - 1);
        nextBtn.Click += (_, _) => UpdateView(idx + 1);
        DockPanel.SetDock(prevBtn, Avalonia.Controls.Dock.Left);
        DockPanel.SetDock(nextBtn, Avalonia.Controls.Dock.Right);
        nav.Children.Add(prevBtn);
        nav.Children.Add(nextBtn);
        nav.Children.Add(dotPanel);

        var gallery = new DockPanel();
        var imgCapture = new Avalonia.Controls.DockPanel();
        imgCapture.Children.Add(imageView);
        gallery.Children.Add(nav);
        DockPanel.SetDock(nav, Avalonia.Controls.Dock.Bottom);
        gallery.Children.Add(imgCapture);

        return gallery;
    }

    // ═══════════════ Stats ═══════════════

    private static Control BuildStatsPanel(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Stats")));

        var bars = new StackPanel { Spacing = 5 };
        if (it.Weight > 0)
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Weight"), $"{it.Weight:F1} kg", Math.Min(it.Weight / 50.0, 1.0), "#4CAF50"));
        if (it.StackLimit > 0)
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.StackLimit"), $"×{it.StackLimit}", Math.Min(it.StackLimit / 100.0, 1.0), "#2196F3"));
        if (it.Durability > 0)
        {
            var durPct = it.Durability >= 999 ? 1.0 : it.Durability;
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Durability"), it.Durability >= 999 ? "Infinite" : $"{it.Durability * 100:F0}%", durPct, durPct < 0.3 ? "#C62828" : "#FF9800"));
        }
        if (it.MonetaryValue > 0)
        {
            var valText = it.MonetaryValueAlt != it.MonetaryValue
                ? $"${it.MonetaryValue:F2} → ${it.MonetaryValueAlt:F2} (real)"
                : $"${it.MonetaryValue:F2}";
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Value"), valText, Math.Min(it.MonetaryValue / 500.0, 1.0), "#9C27B0"));
        }
        if (it.Mirrored)
            bars.Children.Add(VisHelper.StatBar("", VisHelper.Loc("Vis.MirroredDesc"), 0.3, "#607D8B"));
        if (it.SlotDepth > 0)
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.SlotDepth"), $"{it.SlotDepth}", Math.Min(it.SlotDepth / 10.0, 1.0), "#546E7A"));

        sp.Children.Add(VisHelper.Card(bars));
        return sp;
    }

    // ═══════════════ Properties → ItemProp ═══════════════

    private static Control BuildPropertiesPanel(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.Properties")} (→ ItemProp)"));
        var wp = new WrapPanel();
        foreach (var s in it.Properties.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var p = ReferenceResolver.Instance.LookupRef<ItemProp>(it, nameof(ItemType.Properties), s);
            if (p is not null)
                wp.Children.Add(VisHelper.MiniBadge(p.PropertyName, "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ItemProp), p.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(s, "#F5F5F5", "#9E9E9E"));
        }
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    // ═══════════════ AttackModes → AttackMode  (format: {slot}={id}) ═══════════════

    private static Control BuildAttackModesPanel(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.AttackModes")} (→ AttackMode)"));
        var wp = new WrapPanel();
        foreach (var seg in it.AttackModes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var eqIdx = seg.IndexOf('=');
            var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
            var amId = eqIdx > 0 ? seg[(eqIdx + 1)..].Trim() : seg;

            var slotName = int.TryParse(slotPart, out var sn) && SlotNames.TryGetValue(sn, out var snv) ? snv : slotPart;
            var am = ReferenceResolver.Instance.LookupRef<AttackMode>(it, nameof(ItemType.AttackModes), seg);
            if (am is not null)
            {
                var label = string.IsNullOrEmpty(slotName) ? am.Subject : $"{slotName}: {am.Subject}";
                var dmg = am.DamageCut + am.DamageBlunt;
                if (dmg > 0) label += $" ({dmg:F1})";
                wp.Children.Add(VisHelper.MiniBadge(label, "#FFEBEE", "#C62828",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(AttackMode), am.EntityId)));
            }
            else
                wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
        }
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    // ═══════════════ Equipment card ═══════════════

    private static Control BuildEquipmentCard(ItemType it)
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Equipment")));

        // Equip slots
        if (!string.IsNullOrWhiteSpace(it.EquipSlots))
        {
            var wp = new WrapPanel();
            foreach (var s in it.EquipSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                var name = int.TryParse(s, out var sn) && SlotNames.TryGetValue(sn, out var snv) ? snv : s;
                wp.Children.Add(VisHelper.MiniBadge(name, "#E3F2FD", "#1565C0"));
            }
            sp.Children.Add(new StackPanel { Spacing = 3, Children = {
                new TextBlock { Text = VisHelper.Loc("Vis.EquipSlots"), FontSize = 10, Foreground = Brushes.Gray },
                wp
            }});
        }

        // Use slots
        if (!string.IsNullOrWhiteSpace(it.UseSlots))
        {
            var wp = new WrapPanel();
            foreach (var s in it.UseSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                var label = s == "211" ? "Self-Use" : s;
                wp.Children.Add(VisHelper.MiniBadge(label, "#E8EAF6", "#283593"));
            }
            sp.Children.Add(new StackPanel { Spacing = 3, Children = {
                new TextBlock { Text = VisHelper.Loc("Vis.UseSlots"), FontSize = 10, Foreground = Brushes.Gray },
                wp
            }});
        }

        // SocketLocked
        if (it.SocketLocked)
        {
            sp.Children.Add(new StackPanel { Spacing = 2, Children = {
                new TextBlock { Text = VisHelper.Loc("Vis.SocketLocked"), FontSize = 10, Foreground = Brushes.Gray },
                VisHelper.MiniBadge(VisHelper.Loc("Vis.SocketLockedDesc"), "#FFEBEE", "#C62828")
            }});
        }

        // Condition references
        if (!string.IsNullOrWhiteSpace(it.EquipConditions))
            sp.Children.Add(ConditionRow(VisHelper.Loc("Vis.WhenEquipped"), it.EquipConditions, it, nameof(ItemType.EquipConditions)));
        if (!string.IsNullOrWhiteSpace(it.UseConditions))
            sp.Children.Add(ConditionRow(VisHelper.Loc("Vis.WhenUsed"), it.UseConditions, it, nameof(ItemType.UseConditions)));
        if (!string.IsNullOrWhiteSpace(it.PossessConditions))
            sp.Children.Add(ConditionRow(VisHelper.Loc("Vis.WhenCarried"), it.PossessConditions, it, nameof(ItemType.PossessConditions)));

        return VisHelper.Card(sp);
    }

    private static Control ConditionRow(string label, string raw, IEntity sourceEntity, string propName)
    {
        var wp = new WrapPanel();
        var pattern = ReferencePattern.FromName("{id}x{mult}");
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var c = ReferenceResolver.Instance.LookupRef<Condition>(sourceEntity, propName, seg);
            if (c is not null)
            {
                var extra = pattern.FormatExtraInfo(seg);
                var text = string.IsNullOrEmpty(extra) ? c.Subject : $"{c.Subject} ×{extra}";
                wp.Children.Add(VisHelper.MiniBadge(text, "#FCE4EC", "#C62828",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), c.EntityId)));
            }
        }
        return wp.Children.Count > 0
            ? new StackPanel { Spacing = 3, Children = { new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.Gray }, wp } }
            : new TextBlock();
    }

    // ═══════════════ Container card ═══════════════

    private static Control BuildContainerCard(ItemType it)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.AsContainer")));

        if (!string.IsNullOrWhiteSpace(it.Capacities))
            sp.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.Capacity")}: {it.Capacities}", FontSize = 11 });

        if (!string.IsNullOrWhiteSpace(it.FormatId) && it.FormatId != "3")
            sp.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.Format"), it.FormatId, typeof(ContainerType)));
        if (!string.IsNullOrWhiteSpace(it.ContentIds))
        {
            var wp = new WrapPanel();
            foreach (var seg in it.ContentIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var ct = ReferenceResolver.Instance.LookupRef<ContainerType>(it, nameof(ItemType.ContentIds), seg);
                if (ct is not null)
                    wp.Children.Add(VisHelper.MiniBadge(ct.Name, "#E8EAF6", "#283593",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(ContainerType), ct.EntityId)));
            }
            if (wp.Children.Count > 0)
                sp.Children.Add(new StackPanel { Spacing = 3, Children = { new TextBlock { Text = VisHelper.Loc("Vis.AcceptsContent"), FontSize = 10, Foreground = Brushes.Gray }, wp } });
        }

        return VisHelper.Card(sp);
    }

    // ═══════════════ Degrade card ═══════════════

    private static Control BuildDegradeCard(ItemType it)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Degradation")));
        if (it.DegradePerHour > 0)
            sp.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.PerHour")}: {it.DegradePerHour:F3}", FontSize = 11 });
        if (it.EquipDegradePerHour > 0)
            sp.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.PerHourEquipped")}: {it.EquipDegradePerHour:F3}", FontSize = 11, Foreground = Brush.Parse("#E65100") });
        if (it.DegradePerUse > 0)
            sp.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.PerUse")}: {it.DegradePerUse:F3}", FontSize = 11 });
        if (!string.IsNullOrWhiteSpace(it.DegradeTreasureIds) && it.DegradeTreasureIds != "3,3")
        {
            var wp = new WrapPanel();
            foreach (var seg in it.DegradeTreasureIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0 && s != "3"))
            {
                var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(it, nameof(ItemType.DegradeTreasureIds), seg);
                if (tt is not null)
                    wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#FFF8E1", "#F57F17",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            }
            if (wp.Children.Count > 0)
                sp.Children.Add(new StackPanel { Spacing = 3, Children = { new TextBlock { Text = VisHelper.Loc("Vis.BreakParts"), FontSize = 10, Foreground = Brushes.Gray }, wp } });
        }
        return VisHelper.Card(sp);
    }

    // ═══════════════ Charge card ═══════════════

    private static Control BuildChargeCard(ItemType it)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.ChargeAmmo")));
        var wp = new WrapPanel();
        foreach (var seg in it.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cp = ReferenceResolver.Instance.LookupRef<ChargeProfile>(it, nameof(ItemType.ChargeProfiles), seg);
            if (cp is not null)
                wp.Children.Add(VisHelper.MiniBadge(cp.Name, "#E0F7FA", "#006064",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ChargeProfile), cp.EntityId)));
        }
        sp.Children.Add(wp);
        return VisHelper.Card(sp);
    }

    // ═══════════════ Switches → ItemType (toggle states) ═══════════════

    private static Control BuildSwitchesPanel(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.SwitchStates")} (→ ItemType)"));
        var wp = new WrapPanel();
        foreach (var seg in it.SwitchIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var sw = ReferenceResolver.Instance.LookupRef<ItemType>(it, nameof(ItemType.SwitchIds), seg);
            if (sw is not null)
                wp.Children.Add(VisHelper.MiniBadge($"{sw.GroupId}.{sw.SubgroupId} {sw.Name}", "#F3E5F5", "#6A1B9A",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), sw.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
        }
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    // ═══════════════ Reference bars (resolved subjects) ═══════════════

    private static Control BuildRefsPanel(ItemType it)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.References")));
        var added = false;

        if (!string.IsNullOrWhiteSpace(it.TreasureId) && it.TreasureId != "3")
        { sp.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.TreasureTable"), it.TreasureId, typeof(TreasureTable))); added = true; }
        if (!string.IsNullOrWhiteSpace(it.CondId) && it.CondId != "1")
        { sp.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.RequiredCondition"), it.CondId, typeof(Condition))); added = true; }
        if (!string.IsNullOrWhiteSpace(it.ComponentId) && it.ComponentId != "0")
        { sp.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.Component"), it.ComponentId, typeof(TreasureTable))); added = true; }

        if (!added)
            sp.Children.Add(new TextBlock { Text = "—", FontSize = 11, Foreground = Brushes.Gray, FontStyle = FontStyle.Italic });

        return VisHelper.Card(sp);
    }

    private static Control ResolvedRefRow(string label, string raw, Type targetType, string? targetKey = null)
    {
        IEntity? match = GenericDataGridHelper.FindBestMatch(targetType, raw, targetKey);
        var subject = match?.Subject ?? raw;

        var grid = new Grid { Margin = new Thickness(0, 2) };
        grid.ColumnDefinitions.Add(new(110, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));

        grid.Children.Add(new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999999"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var linkBar = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#0D000000"),
            Padding = new Thickness(8, 4),
            Cursor = match is not null ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Child = new TextBlock
            {
                Text = subject,
                FontSize = 11,
                Foreground = match is not null ? Brush.Parse("#1565C0") : Brushes.Gray
            }
        };
        if (match is not null)
        {
            var m = match;
            var tt = targetType;
            linkBar.PointerPressed += (_, e) =>
            { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(tt, m.EntityId); };
        }
        Grid.SetColumn(linkBar, 1);
        grid.Children.Add(linkBar);

        return grid;
    }

    // ═══════════════ Reverse references ═══════════════

    private static Control BuildReverseRefsPanel(ItemType it)
        => VisHelper.BuildReverseRefsPanel(it.EntityId);

    // ═══════════════ Overview ═══════════════

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not ItemType it) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        // Type badge
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E8EAF6"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = "ItemType", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#283593") }
        });

        // Thumbnail image
        var imageNames = (it.ImageList ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        Bitmap? thumb = null;
        if (imageNames.Count > 0)
        {
            thumb = VisHelper.LoadImage(imageNames[0]);
        }

        // Identity badge
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = $"{it.GroupId}.{it.SubgroupId}", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") }
        });

        // Title with optional thumb
        if (thumb is not null)
        {
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            headerRow.Children.Add(new Image { Source = thumb, MaxWidth = 48, MaxHeight = 48, Stretch = Stretch.Uniform });
            headerRow.Children.Add(new TextBlock { Text = it.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            root.Children.Add(headerRow);
        }
        else
        {
            root.Children.Add(new TextBlock { Text = it.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        }

        // Flags
        var flags = new List<string>();
        if (it.Mirrored) flags.Add(VisHelper.Loc("Vis.Mirrored"));
        if (it.SocketLocked) flags.Add(VisHelper.Loc("Vis.SocketLocked"));
        if (!string.IsNullOrWhiteSpace(it.SwitchIds)) flags.Add(VisHelper.Loc("Vis.Toggleable"));
        if (flags.Count > 0)
            root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") } });

        // Core stats
        var statRows = new List<(string, string, string?)>();
        if (it.Weight > 0)
            statRows.Add((VisHelper.Loc("Vis.Weight"), $"{it.Weight:F1} kg", "#4CAF50"));
        if (it.StackLimit > 0)
            statRows.Add((VisHelper.Loc("Vis.StackLimit"), $"×{it.StackLimit}", "#2196F3"));
        if (it.Durability > 0)
            statRows.Add((VisHelper.Loc("Vis.Durability"), it.Durability >= 999 ? "Infinite" : $"{it.Durability * 100:F0}%", it.Durability >= 999 ? "#607D8B" : "#FF9800"));
        if (it.MonetaryValue > 0)
            statRows.Add((VisHelper.Loc("Vis.Value"), $"${it.MonetaryValue:F2}", "#9C27B0"));
        if (it.SlotDepth > 0)
            statRows.Add((VisHelper.Loc("Vis.SlotDepth"), $"{it.SlotDepth}", "#546E7A"));

        if (statRows.Count > 0)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
            root.Children.Add(VisHelper.BuildStatCard(statRows));
        }

        // Property & attack mode counts
        var propCount = string.IsNullOrWhiteSpace(it.Properties) ? 0 : it.Properties.Split(',').Length;
        var atkCount = string.IsNullOrWhiteSpace(it.AttackModes) ? 0 : it.AttackModes.Split(',').Length;
        var switchCount = string.IsNullOrWhiteSpace(it.SwitchIds) ? 0 : it.SwitchIds.Split(',').Length;
        var refRows = new List<(string, string, string?)>();
        if (propCount > 0)
            refRows.Add((VisHelper.Loc("Vis.Properties"), $"→ {propCount} ItemProp", "#2E7D32"));
        if (atkCount > 0)
            refRows.Add((VisHelper.Loc("Vis.AttackModes"), $"→ {atkCount} AttackMode", "#C62828"));
        if (switchCount > 0)
            refRows.Add((VisHelper.Loc("Vis.SwitchStates"), $"→ {switchCount} ItemType", "#6A1B9A"));
        if (!string.IsNullOrWhiteSpace(it.ComponentId) && it.ComponentId != "0")
            refRows.Add((VisHelper.Loc("Vis.Component"), $"→ TreasureTable", "#1565C0"));

        if (refRows.Count > 0)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.References")));
            root.Children.Add(VisHelper.BuildStatCard(refRows));
        }

        // Equipment summary
        if (!string.IsNullOrWhiteSpace(it.EquipSlots))
        {
            var slots = it.EquipSlots.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)
                .Select(s => int.TryParse(s, out var sn) && SlotNames.TryGetValue(sn, out var snv) ? snv : s).ToList();
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Equipment")));
            root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
            {
                (VisHelper.Loc("Vis.EquipSlots"), string.Join(", ", slots), "#1565C0")
            }));
        }

        return root;
    }
}


// ══════════════════════════════════════════════════════════════════════════════
// Recipe
// ══════════════════════════════════════════════════════════════════════════════

public class RecipeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Recipe);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Recipe r) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(r), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(r));
        root.Children.Add(BuildIngredientsPanel(r));
        root.Children.Add(BuildProductPanel(r));
        if (!string.IsNullOrWhiteSpace(r.AlsoTry))
            root.Children.Add(BuildAlsoTryPanel(r));
        root.Children.Add(BuildReverseRefsPanel(r));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Recipe r) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var typeLabel = string.IsNullOrWhiteSpace(r.Type) ? "Misc" : r.Type;
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E8F5E9"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") }
        });
        root.Children.Add(new TextBlock { Text = r.Subject ?? r.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        var statRows = new List<(string, string, string?)>();
        statRows.Add(("Hours", $"{r.Hours:F1}", null));
        statRows.Add(("Reverse", r.Reverse > 0 ? "Yes" : "No", null));
        statRows.Add(("Hidden", r.HiddenId != "0" ? $"#{r.HiddenId}" : "No", null));
        var toolCount = string.IsNullOrWhiteSpace(r.Tools) ? 0 : r.Tools.Split('+').Length;
        var consCount = string.IsNullOrWhiteSpace(r.Consumed) ? 0 : r.Consumed.Split('+').Length;
        statRows.Add(("Tools", $"{toolCount}", null));
        statRows.Add(("Consumed", $"{consCount}", null));
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel("Stats"));
        root.Children.Add(VisHelper.BuildStatCard(statRows));

        return root;
    }

    private static Control BuildHeroHeader(Recipe r)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = $"ID: {r.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") }
        });
        if (!string.IsNullOrWhiteSpace(r.Type))
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"),
                Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = r.Type, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") }
            });
        var flags = new List<string>();
        if (r.Scrap) flags.Add("Scrap");
        if (r.Identify) flags.Add("Identify");
        if (r.DegradeOutput) flags.Add("DegradeOutput");
        if (r.TransferComponents) flags.Add("TransferComponents");
        if (flags.Count > 0)
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"),
                Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = r.Subject ?? r.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(r.SecretName))
            identity.Children.Add(new TextBlock { Text = $"Secret: {r.SecretName}", FontSize = 12, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888") });
        var statRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock { Text = $"Hours: {r.Hours:F1}", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = $"Reverse: {(r.Reverse > 0 ? "Yes" : "No")}", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = r.DegradeOutput ? "DegradeOutput: On" : "DegradeOutput: Off", FontSize = 11, Foreground = r.DegradeOutput ? Brush.Parse("#2E7D32") : Brush.Parse("#999") });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildIngredientsPanel(Recipe r)
    {
        var sp = new StackPanel();
        var hasAny = false;

        void AddGroup(string label, string raw, string propName, string bg, string fg)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            hasAny = true;
            sp.Children.Add(VisHelper.SectionLabel(label));
            var wp = new WrapPanel();
            var pattern = ReferencePattern.FromName("{mult}x{id}");
            foreach (var part in raw.Split('+'))
            {
                var seg = part.Trim();
                var ing = ReferenceResolver.Instance.LookupRef<Ingredient>(r, propName, seg);
                var extra = pattern.FormatExtraInfo(seg);
                var qty = string.IsNullOrEmpty(extra) ? "1" : extra;
                if (ing is not null)
                    wp.Children.Add(VisHelper.MiniBadge($"{ing.Name} x{qty}", bg, fg,
                        () => ReferenceResolver.Instance.NavigateTo(typeof(Ingredient), ing.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge($"{seg} x{qty}", bg, fg));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        AddGroup("Tools", r.Tools, nameof(Recipe.Tools), "#FFF3E0", "#E65100");
        AddGroup("Consumed", r.Consumed, nameof(Recipe.Consumed), "#FFEBEE", "#C62828");
        AddGroup("Destroyed", r.Destroyed, nameof(Recipe.Destroyed), "#FCE4EC", "#880E4F");

        if (!hasAny) sp.Children.Add(new TextBlock { Text = "(No ingredients)", FontSize = 11, Foreground = Brush.Parse("#999") });
        return sp;
    }

    private static Control BuildProductPanel(Recipe r)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Product"));
        var wp = new WrapPanel();
        var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(r, nameof(Recipe.TreasureId), r.TreasureId);
        if (tt is not null)
        {
            wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32",
                () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            if (!string.IsNullOrWhiteSpace(tt.Treasures))
            {
                var itemTypes = GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}");
                foreach (var seg in tt.Treasures.Split(',').Take(6))
                {
                    var parts = seg.Trim().Split('x');
                    if (parts.Length < 2) continue;
                    var itemId = parts[0];
                    var it = itemTypes.GetValueOrDefault(itemId);
                    if (it is not null)
                        wp.Children.Add(VisHelper.MiniBadge(it.Name, "#E0F2F1", "#00695C",
                            () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), it.EntityId)));
                }
            }
        }
        else
            wp.Children.Add(VisHelper.MiniBadge($"TT #{r.TreasureId}", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));

        if (r.TempTreasureId != "3" && r.TempTreasureId != r.TreasureId)
        {
            sp.Children.Add(VisHelper.SectionLabel("Temp Product Preview"));
            var wp2 = new WrapPanel();
            var tmpTt = ReferenceResolver.Instance.LookupRef<TreasureTable>(r, nameof(Recipe.TempTreasureId), r.TempTreasureId);
            if (tmpTt is not null)
                wp2.Children.Add(VisHelper.MiniBadge(tmpTt.Name, "#E3F2FD", "#1565C0",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tmpTt.EntityId)));
            else
                wp2.Children.Add(VisHelper.MiniBadge($"TT #{r.TempTreasureId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp2));
        }
        return sp;
    }

    private static Control BuildAlsoTryPanel(Recipe r)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Also Try (Alternative Recipes)"));
        var wp = new WrapPanel();
        foreach (var seg in r.AlsoTry.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var rr = ReferenceResolver.Instance.LookupRef<Recipe>(r, nameof(Recipe.AlsoTry), seg);
            if (rr is not null)
                wp.Children.Add(VisHelper.MiniBadge(rr.Name, "#F3E5F5", "#6A1B9A",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(Recipe), rr.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{seg}", "#F5F5F5", "#999"));
        }
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    private static Control BuildReverseRefsPanel(Recipe r)
        => VisHelper.BuildReverseRefsPanel(r.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// TreasureTable
// ══════════════════════════════════════════════════════════════════════════════

public class TreasureTableEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(TreasureTable);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not TreasureTable tt) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(tt), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(tt));
        if (!string.IsNullOrWhiteSpace(tt.Treasures))
            root.Children.Add(BuildLootPanel(tt));
        else
            root.Children.Add(VisHelper.Card(new TextBlock { Text = "(Empty)", FontSize = 11, Foreground = Brush.Parse("#999") }));
        root.Children.Add(BuildReverseRefsPanel(tt));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not TreasureTable tt) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock { Text = tt.Subject ?? tt.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        var flags = new List<string>();
        if (tt.Nested) flags.Add("Nested");
        if (tt.Suppress) flags.Add("Suppress");
        if (tt.Identify) flags.Add("Identify");
        if (flags.Count > 0)
            root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") } });

        if (!string.IsNullOrWhiteSpace(tt.Treasures))
        {
            var orGroups = tt.Treasures.Split('|');
            var totalItems = orGroups.Sum(g => g.Split(',').Count(s => s.Trim().Length > 0));
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Loot")));
            root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
            {
                (VisHelper.Loc("Vis.ORGroups"), $"{orGroups.Length}", null),
                (VisHelper.Loc("Vis.TotalItems"), $"{totalItems}", null)
            }));
        }

        return root;
    }

    private static Control BuildHeroHeader(TreasureTable tt)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {tt.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        var flags = new List<string>();
        if (tt.Nested) flags.Add("Nested");
        if (tt.Suppress) flags.Add("Suppress");
        if (tt.Identify) flags.Add("Identify");
        if (flags.Count > 0)
            badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = tt.Subject ?? tt.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildLootPanel(TreasureTable tt)
    {
        var sp = new StackPanel();
        var itemTypes = GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}");

        var orGroups = tt.Treasures.Split('|');
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.Loot")} ({orGroups.Length} {VisHelper.Loc("Vis.ORGroup").ToLowerInvariant()}{(orGroups.Length > 1 ? "s" : "")})"));

        foreach (var orSeg in orGroups)
        {
            var items = orSeg.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0 && s.Contains('x')).ToList();
            if (items.Count == 0) continue;

            var cardStack = new StackPanel { Spacing = 4 };
            if (items.Count > 1)
                cardStack.Children.Add(new TextBlock { Text = VisHelper.Loc("Vis.ORGroup"), FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#5C6BC0"), Margin = new Thickness(0, 0, 0, 4) });

            foreach (var seg in items)
            {
                var parts = seg.Split('x');
                if (parts.Length < 2) continue;
                var itemId = parts[0].Trim();
                var probStr = parts.Length > 1 ? parts[1].Trim() : "1";
                var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
                var prob = double.TryParse(probStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 1.0;
                var probColor = prob >= 0.5 ? "#2E7D32" : prob >= 0.1 ? "#E65100" : "#999";

                string itemName;
                Action? nav = null;
                if (itemTypes.TryGetValue(itemId, out var matched))
                {
                    itemName = matched.Name;
                    nav = () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), matched.EntityId);
                }
                else
                {
                    var nested = ReferenceResolver.Instance.LookupRef<TreasureTable>(tt, nameof(TreasureTable.Treasures), itemId);
                    if (nested is not null)
                    {
                        itemName = $"[TT] {nested.Name}";
                        nav = () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), nested.EntityId);
                    }
                    else
                        itemName = itemId;
                }

                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2) };
                row.Children.Add(VisHelper.MiniBadge(itemName, "#F5F5F5", "#333", nav));
                row.Children.Add(VisHelper.MiniBadge($"{prob:P0}", prob >= 0.5 ? "#E8F5E9" : "#FFF3E0", probColor));
                row.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.Quantity")} {qtyRange}", FontSize = 10, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center });
                cardStack.Children.Add(row);
            }
            sp.Children.Add(VisHelper.Card(cardStack));
        }
        return sp;
    }

    private static Control BuildReverseRefsPanel(TreasureTable tt)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return new StackPanel();
        var rawRefs = store.Index.ReverseLookup(tt.EntityId);
        if (rawRefs.Count == 0) return new StackPanel();

        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var match = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (match != null) { resolved.Add((t, match.Subject, srcEid, propName)); break; }
            }
        }
        if (resolved.Count == 0) return new StackPanel();

        var sp = new StackPanel();
        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var list = new StackPanel { Spacing = 3 };
        foreach (var (srcType, srcSubject, srcEid, _) in resolved.Take(15))
        {
            var tc = srcType == typeof(CampType) ? ("#FFF3E0", "#E65100") : ("#F5F5F5", "#666");
            var row = new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#0D000000"), Padding = new Thickness(8, 3), Cursor = new Cursor(StandardCursorType.Hand), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { CornerRadius = new CornerRadius(3), Background = Brush.Parse(tc.Item1), Padding = new Thickness(5, 1), Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(tc.Item2) } }, new TextBlock { Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center } } } };
            var ct = srcType; var ci = srcEid;
            row.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct, ci); };
            list.Children.Add(row);
        }
        if (resolved.Count > 15) list.Children.Add(new TextBlock { Text = $"+ {resolved.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 2) });
        sp.Children.Add(VisHelper.Card(list));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Encounter
// ══════════════════════════════════════════════════════════════════════════════

public class EncounterEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Encounter);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Encounter enc) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(enc), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(enc));
        if (!string.IsNullOrWhiteSpace(enc.Description))
            root.Children.Add(BuildStoryPanel(enc));
        if (!string.IsNullOrWhiteSpace(enc.Responses))
            root.Children.Add(BuildResponsesPanel(enc));
        root.Children.Add(BuildRefsPanel(enc));
        var triggers = FindTriggers(enc.Id);
        if (triggers.Count > 0)
            root.Children.Add(BuildTriggersPanel(triggers));
        root.Children.Add(BuildReverseRefsPanel(enc));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Encounter enc) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var bmp = VisHelper.LoadImage(enc.Image);
        var imgStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
        if (bmp is not null)
            imgStack.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 } });
        root.Children.Add(imgStack);

        var typeLabel = enc.Type == EncounterType.Scavenge ? "Scavenge" : "Normal";
        var typeBg = enc.Type == EncounterType.Scavenge ? "#FFF3E0" : "#E3F2FD";
        var typeFg = enc.Type == EncounterType.Scavenge ? "#E65100" : "#1565C0";
        root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) } });
        root.Children.Add(new TextBlock { Text = enc.Subject ?? enc.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        if (!string.IsNullOrWhiteSpace(enc.Description))
        {
            var desc = enc.Description.Length > 150 ? enc.Description[..150] + "..." : enc.Description;
            root.Children.Add(new TextBlock { Text = desc, FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        }

        var statRows = new List<(string, string, string?)>();
        if (enc.Price != 0) statRows.Add(("Price", $"${enc.Price:F2}", null));
        statRows.Add(("Type", enc.Type.ToString(), null));
        if (enc.LootChance > 0) statRows.Add(("Loot Chance", $"{enc.LootChance:P0}", null));
        if (enc.AccidentChance > 0) statRows.Add(("Accident", $"{enc.AccidentChance:P0}", "#C62828"));
        if (enc.CreatureId != "0") statRows.Add(("Creature", $"#{enc.CreatureId}", null));
        if (statRows.Count > 0) { root.Children.Add(VisHelper.Separator()); root.Children.Add(VisHelper.OvSectionLabel("Stats")); root.Children.Add(VisHelper.BuildStatCard(statRows)); }

        return root;
    }

    private static Control BuildHeroHeader(Encounter enc)
    {
        var grid = new Grid { ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var bmp = VisHelper.LoadImage(enc.Image);
        var imageArea = new Border { Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon { Symbol = Symbol.BookOpen, FontSize = 40, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(imageArea, 0); grid.Children.Add(imageArea);

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {enc.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        var typeLabel = enc.Type == EncounterType.Scavenge ? "Scavenge" : "Normal";
        var typeBg = enc.Type == EncounterType.Scavenge ? "#FFF3E0" : "#E3F2FD";
        var typeFg = enc.Type == EncounterType.Scavenge ? "#E65100" : "#1565C0";
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(8, 2), Child = new TextBlock { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) } });
        if (enc.RemoveCreatures) badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = "RemoveCreatures", FontSize = 10, Foreground = Brush.Parse("#C62828") } });
        if (enc.RemoveUsed) badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = "RemoveUsed", FontSize = 10, Foreground = Brush.Parse("#C62828") } });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = enc.Subject ?? enc.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        var chanceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        if (enc.Price != 0) chanceRow.Children.Add(new TextBlock { Text = $"Price: ${enc.Price:F2}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (enc.LootChance > 0) chanceRow.Children.Add(new TextBlock { Text = $"Loot: {enc.LootChance:P0}", FontSize = 11, Foreground = Brush.Parse("#2E7D32") });
        if (enc.AccidentChance > 0) chanceRow.Children.Add(new TextBlock { Text = $"Accident: {enc.AccidentChance:P0}", FontSize = 11, Foreground = Brush.Parse("#C62828") });
        if (chanceRow.Children.Count > 0) identity.Children.Add(chanceRow);
        Grid.SetColumn(identity, 1); grid.Children.Add(identity);

        return VisHelper.Card(grid);
    }

    private static Control BuildStoryPanel(Encounter enc)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Story Text"));
        var desc = enc.Description.Length > 2000 ? enc.Description[..2000] + "..." : enc.Description;
        sp.Children.Add(VisHelper.Card(new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
        return sp;
    }

    private static Control BuildResponsesPanel(Encounter enc)
    {
        var sp = new StackPanel();
        var lines = enc.Responses.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        sp.Children.Add(VisHelper.SectionLabel($"Responses ({lines.Length} option{(lines.Length > 1 ? "s" : "")})"));
        var text = enc.Responses.Length > 1500 ? enc.Responses[..1500] + "..." : enc.Responses;
        sp.Children.Add(VisHelper.Card(new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333"), FontFamily = "Consolas, monospace" }));
        return sp;
    }

    private static Control BuildRefsPanel(Encounter enc)
    {
        var sp = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(enc.TreasureId) && enc.TreasureId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel("Loot Table"));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(enc, nameof(Encounter.TreasureId), enc.TreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32", () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"TT #{enc.TreasureId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.RemoveTreasureId) && enc.RemoveTreasureId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel("Remove (submit/destroy)"));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(enc, nameof(Encounter.RemoveTreasureId), enc.RemoveTreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#FFEBEE", "#C62828", () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"TT #{enc.RemoveTreasureId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.Conditions) && enc.Conditions != "1")
        {
            sp.Children.Add(VisHelper.SectionLabel("Conditions"));
            var wp = new WrapPanel();
            foreach (var seg in enc.Conditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(enc, nameof(Encounter.Conditions), seg);
                if (cond is not null)
                    wp.Children.Add(VisHelper.MiniBadge(cond.Subject, "#FCE4EC", "#C62828", () => ReferenceResolver.Instance.NavigateToByKeyFor<Condition>(cond.Id, enc)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.PreConditions))
        {
            sp.Children.Add(VisHelper.SectionLabel("Pre-Conditions"));
            var wp = new WrapPanel();
            foreach (var seg in enc.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var isNeg = seg.StartsWith("-");
                var rawId = isNeg ? seg[1..] : seg;
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), seg);
                if (cond is not null)
                    wp.Children.Add(VisHelper.MiniBadge((isNeg ? "NOT " : "") + cond.Subject, isNeg ? "#FFEBEE" : "#E8F5E9", isNeg ? "#C62828" : "#2E7D32", () => ReferenceResolver.Instance.NavigateToByKeyFor<Condition>(cond.Id, enc)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (enc.CreatureId != "0")
        {
            sp.Children.Add(VisHelper.SectionLabel("Spawn Creature"));
            var wp = new WrapPanel();
            var creature = ReferenceResolver.Instance.LookupRef<Creature>(enc, nameof(Encounter.CreatureId), enc.CreatureId);
            if (creature is not null)
            {
                wp.Children.Add(VisHelper.MiniBadge(creature.Subject, "#E8EAF6", "#283593", () => ReferenceResolver.Instance.NavigateTo(typeof(Creature), creature.EntityId)));
                if (!string.IsNullOrWhiteSpace(enc.CreatureHex) && enc.CreatureHex != "0,0")
                    wp.Children.Add(new TextBlock { Text = $" at {enc.CreatureHex}", FontSize = 10, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center });
            }
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{enc.CreatureId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.Teleport) && enc.Teleport != "0,0")
        {
            sp.Children.Add(VisHelper.SectionLabel("Teleport"));
            sp.Children.Add(VisHelper.Card(new TextBlock { Text = $"Destination: ({enc.Teleport})", FontSize = 11, Foreground = Brush.Parse("#6A1B9A") }));
        }

        if (!string.IsNullOrWhiteSpace(enc.Accidents) && enc.Accidents != "1")
        {
            sp.Children.Add(VisHelper.SectionLabel("Accidents"));
            var wp = new WrapPanel();
            foreach (var seg in enc.Accidents.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var accident = ReferenceResolver.Instance.LookupRef<Encounter>(enc, nameof(Encounter.Accidents), seg);
                if (accident is not null)
                    wp.Children.Add(VisHelper.MiniBadge(accident.Subject, "#FFEBEE", "#C62828", () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), accident.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        return sp;
    }

    private static Control BuildTriggersPanel(List<EncounterTrigger> triggers)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel($"Triggered By ({triggers.Count})"));
        var wp = new WrapPanel();
        foreach (var trigger in triggers)
            wp.Children.Add(VisHelper.MiniBadge($"{trigger.Name}", "#F3E5F5", "#6A1B9A", () => ReferenceResolver.Instance.NavigateTo(typeof(EncounterTrigger), trigger.EntityId)));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    private static List<EncounterTrigger> FindTriggers(int encounterId)
    {
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(EncounterTrigger), out var list) || list is null)
            return [];
        return list.OfType<EncounterTrigger>().Where(t => t.EncounterId == encounterId).ToList();
    }

    private static Control BuildReverseRefsPanel(Encounter enc)
        => VisHelper.BuildReverseRefsPanel(enc.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// Creature
// ══════════════════════════════════════════════════════════════════════════════

public class CreatureEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Creature);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Creature c) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(c), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(c));
        root.Children.Add(BuildRefsPanel(c));
        root.Children.Add(BuildReverseRefsPanel(c));
        if (!string.IsNullOrWhiteSpace(c.Activities))
            root.Children.Add(BuildActivitiesPanel(c));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Creature c) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var bmp = VisHelper.LoadImage(c.Image);
        var imgStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
        if (bmp is not null)
            imgStack.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 } });
        else
            imgStack.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = "??", FontSize = 24, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        root.Children.Add(imgStack);

        root.Children.Add(new TextBlock { Text = c.Subject ?? c.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        if (!string.IsNullOrWhiteSpace(c.NamePublic) && c.NamePublic != c.Name)
            root.Children.Add(new TextBlock { Text = $"\"{c.NamePublic}\"", FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel("Stats"));
        var factionName = ReferenceResolver.Instance.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction)?.Subject;
        var statRows = new List<(string, string, string?)>
        {
            ("Moves/Turn", $"{c.MovesPerTurn}", null),
            ("Faction", factionName ?? $"#{c.Faction}", null)
        };
        var atkCount = string.IsNullOrWhiteSpace(c.AttackModes) ? 0 : c.AttackModes.Split(',').Length;
        if (atkCount > 0) statRows.Add(("Attacks", $"{atkCount} modes", null));
        root.Children.Add(VisHelper.BuildStatCard(statRows));

        return root;
    }

    private static Control BuildHeroHeader(Creature c)
    {
        var grid = new Grid { ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var bmp = VisHelper.LoadImage(c.Image);
        var imageArea = new Border { Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new TextBlock { Text = "Creature", FontSize = 14, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(imageArea, 0); grid.Children.Add(imageArea);

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {c.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"{c.MovesPerTurn} moves/turn", FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        var factionName = ReferenceResolver.Instance.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction)?.Subject;
        if (!string.IsNullOrWhiteSpace(factionName) && c.Faction != "0")
            badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = factionName, FontSize = 10, Foreground = Brush.Parse("#283593") } });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = c.Subject ?? c.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(c.NamePublic) && c.NamePublic != c.Name)
            identity.Children.Add(new TextBlock { Text = $"Public: {c.NamePublic}", FontSize = 12, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888") });
        if (!string.IsNullOrWhiteSpace(c.Notes))
            identity.Children.Add(new TextBlock { Text = c.Notes, FontSize = 11, Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 1); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildRefsPanel(Creature c)
    {
        var sp = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(c.Faction) && c.Faction != "0")
        {
            sp.Children.Add(VisHelper.SectionLabel("Faction"));
            var wp = new WrapPanel();
            var faction = ReferenceResolver.Instance.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction);
            if (faction is not null)
                wp.Children.Add(VisHelper.MiniBadge(faction.Subject, "#FFF3E0", "#E65100", () => ReferenceResolver.Instance.NavigateTo(typeof(Faction), faction.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{c.Faction}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.AttackModes))
        {
            sp.Children.Add(VisHelper.SectionLabel("Attack Modes"));
            var wp = new WrapPanel();
            foreach (var seg in c.AttackModes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var am = ReferenceResolver.Instance.LookupRef<AttackMode>(c, nameof(Creature.AttackModes), seg);
                if (am is not null)
                    wp.Children.Add(VisHelper.MiniBadge(am.Subject, "#FFEBEE", "#C62828", () => ReferenceResolver.Instance.NavigateTo(typeof(AttackMode), am.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.BaseConditions))
        {
            sp.Children.Add(VisHelper.SectionLabel("Base Conditions"));
            var eqPattern = ReferencePattern.FromName("{id}={value}");
            var wp = new WrapPanel();
            foreach (var seg in c.BaseConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(c, nameof(Creature.BaseConditions), seg);
                if (cond is not null)
                {
                    var extra = eqPattern.FormatExtraInfo(seg);
                    var label = string.IsNullOrEmpty(extra) ? cond.Subject : $"{cond.Subject} ={extra}";
                    wp.Children.Add(VisHelper.MiniBadge(label, "#FCE4EC", "#C62828", () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), cond.EntityId)));
                    continue;
                }
                wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.EncounterIds))
        {
            sp.Children.Add(VisHelper.SectionLabel("On-Encounter Conditions"));
            var wp = new WrapPanel();
            foreach (var seg in c.EncounterIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(c, nameof(Creature.EncounterIds), seg);
                if (cond is not null)
                    wp.Children.Add(VisHelper.MiniBadge(cond.Subject, "#E8EAF6", "#283593", () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), cond.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.TreasureId) && c.TreasureId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel("Loot Table"));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(c, nameof(Creature.TreasureId), c.TreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32", () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(c.TreasureId, "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.CorpseId) && c.CorpseId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel("Corpse Loot"));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(c, nameof(Creature.CorpseId), c.CorpseId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#FCE4EC", "#880E4F", () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(c.CorpseId, "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        return sp;
    }

    private static Control BuildActivitiesPanel(Creature c)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Activities"));
        var act = c.Activities.Length > 500 ? c.Activities[..500] + "..." : c.Activities;
        sp.Children.Add(VisHelper.Card(new TextBlock { Text = act, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#555") }));
        return sp;
    }

    private static Control BuildReverseRefsPanel(Creature c)
        => VisHelper.BuildReverseRefsPanel(c.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// Condition
// ══════════════════════════════════════════════════════════════════════════════

public class ConditionEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Condition);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Condition cond) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(cond), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(cond));
        if (!string.IsNullOrWhiteSpace(cond.Description))
            root.Children.Add(BuildDescriptionPanel(cond));
        root.Children.Add(BuildModifiersPanel(cond));
        if (!string.IsNullOrWhiteSpace(cond.Effects))
            root.Children.Add(BuildEffectsPanel(cond));
        if (!string.IsNullOrWhiteSpace(cond.IdNext) && cond.IdNext != "0")
            root.Children.Add(BuildNextPanel(cond));
        root.Children.Add(BuildReverseRefsPanel(cond));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Condition cond) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var colorIcon = cond.Color switch
        {
            ConditionColor.Red => "🔴",
            ConditionColor.Green => "🟢",
            ConditionColor.Yellow => "🟡",
            _ => "⚪"
        };
        var severityLabel = cond.Fatal ? "FATAL" : cond.Permanent ? "Instant" : cond.Stackable ? "Stackable" : "Duration";
        var sevBg = cond.Fatal ? "#FFEBEE" : cond.Permanent ? "#FFF3E0" : cond.Stackable ? "#E8F5E9" : "#E3F2FD";
        var sevFg = cond.Fatal ? "#C62828" : cond.Permanent ? "#E65100" : cond.Stackable ? "#2E7D32" : "#1565C0";
        root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse(sevBg), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = $"{colorIcon} {severityLabel}", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(sevFg) } });
        root.Children.Add(new TextBlock { Text = cond.Subject ?? cond.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel("Stats"));
        var statRows = new List<(string, string, string?)>
        {
            ("Duration", cond.Permanent ? "Instant" : $"{cond.Duration}h", null),
            ("Color", cond.Color switch { ConditionColor.Red => "Red (-)", ConditionColor.Green => "Green (+)", ConditionColor.Yellow => "Yellow", _ => "White" }, null),
            ("Transfer", cond.TransferRange >= 0 ? $"{cond.TransferRange}" : "None", null)
        };
        root.Children.Add(VisHelper.BuildStatCard(statRows));

        if (!string.IsNullOrWhiteSpace(cond.IdNext) && cond.IdNext != "0")
        {
            var nextCount = cond.IdNext.Split(',').Length;
            root.Children.Add(VisHelper.Kv("Next Stages", $"{nextCount} condition(s)", 85));
        }

        return root;
    }

    private static Control BuildHeroHeader(Condition cond)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {cond.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });

        var sevBg = cond.Fatal ? "#FFEBEE" : cond.Permanent ? "#FFF3E0" : "#E8F5E9";
        var sevFg = cond.Fatal ? "#C62828" : cond.Permanent ? "#E65100" : "#2E7D32";
        var sevLabel = cond.Fatal ? "FATAL" : cond.Permanent ? "Instant" : "Duration";
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse(sevBg), Padding = new Thickness(8, 2), Child = new TextBlock { Text = sevLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(sevFg) } });

        if (cond.Stackable) badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = "Stackable", FontSize = 10, Foreground = Brush.Parse("#2E7D32") } });
        if (!cond.Display) badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#F5F5F5"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = "Hidden", FontSize = 10, Foreground = Brush.Parse("#999") } });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = cond.Subject ?? cond.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        var statRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock { Text = cond.Permanent ? "Instant" : $"{cond.Duration}h", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = cond.Color switch { ConditionColor.Red => "Red (-)", ConditionColor.Green => "Green (+)", ConditionColor.Yellow => "Yellow", _ => "White" }, FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = $"Transfer: {cond.TransferRange}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.ResetTimer) statRow.Children.Add(new TextBlock { Text = "ResetTimer", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.DisplayOther) statRow.Children.Add(new TextBlock { Text = "Visible to Others", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.DisplayGameOver) statRow.Children.Add(new TextBlock { Text = "GameOver Log", FontSize = 11, Foreground = Brush.Parse("#666") });
        identity.Children.Add(statRow);

        if (!string.IsNullOrWhiteSpace(cond.Thresholds))
            identity.Children.Add(new TextBlock { Text = $"Thresholds: {cond.Thresholds}", FontSize = 11, Foreground = Brush.Parse("#6A1B9A") });
        if (!string.IsNullOrWhiteSpace(cond.ChanceNext) && cond.ChanceNext != "0")
            identity.Children.Add(new TextBlock { Text = $"Chance Next: {cond.ChanceNext}", FontSize = 11, Foreground = Brush.Parse("#666") });

        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildDescriptionPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Description"));
        var desc = cond.Description.Length > 800 ? cond.Description[..800] + "..." : cond.Description;
        sp.Children.Add(VisHelper.Card(new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
        return sp;
    }

    private static Control BuildModifiersPanel(Condition cond)
    {
        var names = (cond.FieldNames ?? "").Split(',').Select(s => s.Trim()).ToList();
        var mods = (cond.Modifiers ?? "").Split(',').Select(s => s.Trim()).ToList();
        if (names.Count == 0 || names.All(string.IsNullOrEmpty)) return new StackPanel();

        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("FieldNames → Modifiers"));
        var grid = new Grid
        {
            ColumnDefinitions = { new(1, GridUnitType.Star), new(60, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(4, 0)
        };
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        for (int i = 0; i < Math.Max(names.Count, mods.Count); i++)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));
            var name = i < names.Count ? names[i] : "";
            var mod = i < mods.Count ? mods[i] : "";
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(mod)) continue;

            var nameTb = new TextBlock { Text = name, FontSize = 10, Foreground = Brush.Parse("#1565C0"), Margin = new Thickness(2, 1, 4, 1), TextTrimming = TextTrimming.CharacterEllipsis };
            var arrow = new TextBlock { Text = "→", FontSize = 10, Foreground = Brush.Parse("#999"), Margin = new Thickness(2, 1), TextAlignment = TextAlignment.Center };
            var modTb = new TextBlock { Text = mod, FontSize = 10, FontWeight = FontWeight.Medium, Foreground = Brush.Parse("#2E7D32"), Margin = new Thickness(4, 1, 2, 1) };
            Grid.SetRow(nameTb, i); Grid.SetColumn(nameTb, 0);
            Grid.SetRow(arrow, i); Grid.SetColumn(arrow, 1);
            Grid.SetRow(modTb, i); Grid.SetColumn(modTb, 2);
            grid.Children.Add(nameTb); grid.Children.Add(arrow); grid.Children.Add(modTb);
        }
        sp.Children.Add(VisHelper.Card(grid));
        return sp;
    }

    private static Control BuildEffectsPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Effects"));
        var eff = cond.Effects.Length > 800 ? cond.Effects[..800] + "..." : cond.Effects;
        sp.Children.Add(VisHelper.Card(new TextBlock { Text = eff, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#00695C"), FontFamily = "Consolas, monospace" }));
        return sp;
    }

    private static Control BuildNextPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Condition Chain"));
        var wp = new WrapPanel();
        foreach (var seg in cond.IdNext.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var next = ReferenceResolver.Instance.LookupRef<Condition>(cond, nameof(Condition.IdNext), seg);
            if (next is not null)
                wp.Children.Add(VisHelper.MiniBadge(next.Subject, "#F3E5F5", "#6A1B9A", () => ReferenceResolver.Instance.NavigateToByKeyFor<Condition>(next.Id, cond)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{seg}", "#F5F5F5", "#999"));
        }
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    private static Control BuildReverseRefsPanel(Condition cond)
        => VisHelper.BuildReverseRefsPanel(cond.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// AttackMode
// ══════════════════════════════════════════════════════════════════════════════

public class AttackModeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(AttackMode);

    // ═══════════════ Detail ═══════════════

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not AttackMode am) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawContent = VisHelper.BuildRawDataTable(am);
        var rawBody = new Border { IsVisible = false, Child = rawContent, Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(am));
        root.Children.Add(BuildCombatPanel(am));

        if (!string.IsNullOrWhiteSpace(am.ChargeProfiles))
            root.Children.Add(BuildChargePanel(am));

        if (!string.IsNullOrWhiteSpace(am.AttackerConditions))
            root.Children.Add(BuildConditionsPanel(am));

        if (!string.IsNullOrWhiteSpace(am.AttackPhrases))
            root.Children.Add(BuildAttackPhrasesPanel(am));

        root.Children.Add(BuildReverseRefsPanel(am));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Overview ═══════════════

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not AttackMode am) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var bmp = VisHelper.LoadImage(am.Image);
        var imgStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
        if (bmp is not null)
        {
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8),
                ClipToBounds = true, Background = Brush.Parse("#0A000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 }
            });
        }
        else
        {
            var iconSymbol = GetSoundSymbol(am.Sound) ?? (am.Type == AttackType.Ranged ? Symbol.Target : Symbol.Flash);
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8),
                Background = Brush.Parse("#0A000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new SymbolIcon { Symbol = iconSymbol, FontSize = 32, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });
        }
        root.Children.Add(imgStack);

        var isRanged = am.Type == AttackType.Ranged;
        var rangeStr = am.Range <= 1 ? "1 tile" : $"{am.Range} tiles";
        var typeLabel = isRanged ? $"Ranged ({rangeStr})" : $"Melee ({rangeStr})";
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(isRanged ? "#FCE4EC" : "#E8F5E9"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(isRanged ? "#C62828" : "#2E7D32") }
        });
        root.Children.Add(new TextBlock { Text = am.Subject ?? am.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
        {
            var quote = am.WieldPhrase.Length > 80 ? am.WieldPhrase[..80] + "..." : am.WieldPhrase;
            root.Children.Add(new TextBlock { Text = $"\"{quote}\"", FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        }

        var hasStats = am.DamageCut > 0 || am.DamageBlunt > 0 || am.Range > 1 || am.Morale != 0.25;
        if (hasStats)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
            var statRows = new List<(string label, string value, string? color)>();
            var totalDmg = am.DamageCut + am.DamageBlunt;
            statRows.Add((VisHelper.Loc("Range"), $"{am.Range} {VisHelper.Loc("Vis.Tiles")}", null));
            if (am.DamageCut > 0) statRows.Add((VisHelper.Loc("Vis.Cut"), $"{am.DamageCut:F1}", "#E53935"));
            if (am.DamageBlunt > 0) statRows.Add((VisHelper.Loc("Vis.Blunt"), $"{am.DamageBlunt:F1}", "#FB8C00"));
            statRows.Add((VisHelper.Loc("Vis.Total"), $"{totalDmg:F1}", null));
            var moralePct = (int)(am.Morale * 100);
            var moraleLabel = moralePct == 25 ? $"{moralePct}% ({VisHelper.Loc("Vis.Base")})" : $"{moralePct}%";
            var moraleColor = am.Morale > 0.25 ? "#2E7D32" : am.Morale < 0.25 ? "#C62828" : null;
            statRows.Add((VisHelper.Loc("Morale"), moraleLabel, moraleColor));
            if (am.Penetration > 0)
                statRows.Add((VisHelper.Loc("Penetration"), $"Lv.{am.Penetration}", "#6A1B9A"));
            root.Children.Add(VisHelper.BuildStatCard(statRows));
        }

        if (!string.IsNullOrWhiteSpace(am.ChargeProfiles))
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Ammo")));
            var wp = new WrapPanel();
            foreach (var seg in am.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cp = ReferenceResolver.Instance.LookupRef<ChargeProfile>(am, nameof(AttackMode.ChargeProfiles), seg);
                if (cp is not null)
                    wp.Children.Add(VisHelper.MiniBadge(cp.Name, "#E0F7FA", "#006064",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(ChargeProfile), cp.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            if (wp.Children.Count > 0) root.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(am.Sound))
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Sound")));
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#F3E5F5"),
                Padding = new Thickness(8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new TextBlock { Text = "▶", FontSize = 9, Foreground = Brush.Parse("#7B1FA2") }, new TextBlock { Text = am.Sound, FontSize = 10, Foreground = Brush.Parse("#7B1FA2") } } }
            });
        }

        return root;
    }

    // ═══════════════ Hero Header ═══════════════

    private static Control BuildHeroHeader(AttackMode am)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var bmp = VisHelper.LoadImage(am.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Background = Brush.Parse("#0A000000"),
            VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
        {
            var iconSymbol = GetSoundSymbol(am.Sound) ?? (am.Type == AttackType.Ranged ? Symbol.Target : Symbol.Flash);
            imageArea.Child = new SymbolIcon { Symbol = iconSymbol, FontSize = 40, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = $"ID: {am.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") }
        });
        var rangeStr = am.Range <= 1 ? "1 tile" : $"{am.Range} tiles";
        var typeLabel = am.Type == AttackType.Ranged ? $"Ranged ({rangeStr})" : $"Melee ({rangeStr})";
        var typeBg = am.Type == AttackType.Ranged ? "#FCE4EC" : "#E8F5E9";
        var typeFg = am.Type == AttackType.Ranged ? "#C62828" : "#2E7D32";
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(typeBg),
            Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = am.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
        {
            var quote = am.WieldPhrase.Length > 120 ? am.WieldPhrase[..120] + "..." : am.WieldPhrase;
            identity.Children.Add(new TextBlock { Text = $"\"{quote}\"", FontSize = 12, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap });
        }

        if (!string.IsNullOrWhiteSpace(am.Notes))
            identity.Children.Add(new TextBlock { Text = am.Notes, FontSize = 12, Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap });

        Grid.SetColumn(identity, 1);
        Grid.SetRow(identity, 0);
        grid.Children.Add(identity);

        return VisHelper.Card(grid);
    }

    // ═══════════════ Combat Panel ═══════════════

    private static Control BuildCombatPanel(AttackMode am)
    {
        var sp = new StackPanel();

        var isRanged = am.Type == AttackType.Ranged;
        var iconSymbol = isRanged ? Symbol.Target : Symbol.Flash;
        var iconBg = isRanged ? "#FCE4EC" : "#E8F5E9";
        var iconFg = isRanged ? "#C62828" : "#2E7D32";
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        headerRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12), Width = 24, Height = 24,
            Background = Brush.Parse(iconBg),
            Child = new SymbolIcon { Symbol = iconSymbol, FontSize = 14, Foreground = Brush.Parse(iconFg), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        headerRow.Children.Add(new TextBlock { Text = isRanged ? VisHelper.Loc("Vis.CombatRanged") : VisHelper.Loc("Vis.CombatMelee"), FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#555"), VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(headerRow);

        var bars = new StackPanel { Spacing = 6 };

        var rangeMax = Math.Max(am.Range, 10);
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Range"), $"{am.Range} {VisHelper.Loc("Vis.Tiles")}", am.Range / (double)rangeMax, "#607D8B"));

        var maxDmg = Math.Max(am.DamageCut, Math.Max(am.DamageBlunt, 2.0));
        if (am.DamageCut > 0)
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Cut"), $"{am.DamageCut:F1}", am.DamageCut / maxDmg, "#E53935"));
        if (am.DamageBlunt > 0)
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Blunt"), $"{am.DamageBlunt:F1}", am.DamageBlunt / maxDmg, "#FB8C00"));

        var totalDmg = am.DamageCut + am.DamageBlunt;
        var moralePct = (int)(am.Morale * 100);
        var moraleLabel = moralePct == 25 ? $"{moralePct}% (base)" : $"{moralePct}%";
        var moraleColor = am.Morale > 0.25 ? "#2E7D32" : am.Morale < 0.25 ? "#C62828" : "#78909C";
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Morale"), moraleLabel, am.Morale, moraleColor));

        if (totalDmg > 0)
        {
            var effectiveDmg = totalDmg * (1 + am.Morale);
            var effLabel = $"{effectiveDmg:F1} ({1 + am.Morale:F2} × {totalDmg:F1})";
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Effective"), effLabel,
                Math.Clamp(effectiveDmg / 8.0, 0.05, 1.0), "#6A1B9A"));
        }

        if (am.Penetration > 0)
        {
            var penRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            penRow.Children.Add(new TextBlock { Text = VisHelper.Loc("Penetration"), FontSize = 11, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center });
            var penDots = new string('●', am.Penetration) + new string('○', Math.Max(0, 4 - am.Penetration));
            penRow.Children.Add(new TextBlock { Text = $"{penDots}  Lv.{am.Penetration}", FontSize = 11, Foreground = Brush.Parse("#6A1B9A"), VerticalAlignment = VerticalAlignment.Center });
            bars.Children.Add(penRow);
        }

        if (!string.IsNullOrWhiteSpace(am.Sound))
        {
            var sndRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            sndRow.Children.Add(new TextBlock { Text = VisHelper.Loc("Sound"), FontSize = 11, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center });
            sndRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#F3E5F5"),
                Padding = new Thickness(8, 2),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new TextBlock { Text = "▶", FontSize = 9, Foreground = Brush.Parse("#7B1FA2") }, new TextBlock { Text = am.Sound, FontSize = 10, Foreground = Brush.Parse("#7B1FA2") } } }
            });
            bars.Children.Add(sndRow);
        }

        if (am.Transfer)
        {
            var tRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            tRow.Children.Add(new TextBlock { Text = VisHelper.Loc("Transfer"), FontSize = 11, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center });
            tRow.Children.Add(new TextBlock { Text = VisHelper.Loc("Vis.TransferDesc"), FontSize = 11, Foreground = Brush.Parse("#558B2F"), VerticalAlignment = VerticalAlignment.Center });
            bars.Children.Add(tRow);
        }

        sp.Children.Add(VisHelper.Card(bars));
        return sp;
    }

    // ═══════════════ Charge Profiles ═══════════════

    private static Control BuildChargePanel(AttackMode am)
    {
        var sp = new StackPanel();
        var parts = am.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.Ammo")} ({parts.Count} type{(parts.Count > 1 ? "s" : "")})"));

        var wp = new WrapPanel();
        foreach (var raw in parts)
        {
            var cp = ReferenceResolver.Instance.LookupRef<ChargeProfile>(am, nameof(AttackMode.ChargeProfiles), raw);
            if (cp is not null)
                wp.Children.Add(VisHelper.MiniBadge(cp.Name, "#E0F7FA", "#006064",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ChargeProfile), cp.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(raw, "#F5F5F5", "#999"));
        }

        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    // ═══════════════ Attacker Conditions ═══════════════

    private static Control BuildConditionsPanel(AttackMode am)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.AttackerConditions")));

        var pattern = ReferencePattern.FromName("{id}x{mult}");
        var wp = new WrapPanel();
        foreach (var seg in am.AttackerConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cond = ReferenceResolver.Instance.LookupRef<Condition>(am, nameof(AttackMode.AttackerConditions), seg);
            if (cond is not null)
            {
                var extra = pattern.FormatExtraInfo(seg);
                var display = string.IsNullOrEmpty(extra) ? cond.Subject : $"{cond.Subject} x{extra}";
                wp.Children.Add(VisHelper.MiniBadge(display, "#FCE4EC", "#C62828",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), cond.EntityId)));
            }
            else
            {
                wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
        }

        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    // ═══════════════ Attack Phrases ═══════════════

    private static Control BuildAttackPhrasesPanel(AttackMode am)
    {
        var sp = new StackPanel();
        var phrases = am.AttackPhrases.Split(',', '，').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.AttackPhrases")} ({phrases.Count})"));

        var wp = new WrapPanel();
        foreach (var p in phrases)
        {
            var display = p.Length > 60 ? p[..60] + "..." : p;
            wp.Children.Add(VisHelper.MiniBadge(display, "#E3F2FD", "#1565C0"));
        }

        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    // ═══════════════ Reverse References (via store's pre-built ReferenceIndex) ═══════════════

    private static Control BuildReverseRefsPanel(AttackMode am)
    {
        var sp = new StackPanel();

        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return sp;

        // Reuse the store's pre-built ReferenceIndex reverse lookup — O(1)
        var rawRefs = store.Index.ReverseLookup(am.EntityId);
        if (rawRefs.Count == 0) return sp;

        // Resolve source entity IDs → (type, subject, entityId, propName)
        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var match = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (match != null)
                {
                    resolved.Add((t, match.Subject, srcEid, propName));
                    break;
                }
            }
        }

        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var list = new StackPanel { Spacing = 3 };
        foreach (var (srcType, srcSubject, srcEid, propName) in resolved.Take(15))
        {
            var typeColors = srcType == typeof(Creature) ? ("#E8EAF6", "#283593")
                           : srcType == typeof(ItemType) ? ("#E3F2FD", "#1565C0")
                           : ("#F5F5F5", "#666");

            var row = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#0D000000"),
                Padding = new Thickness(8, 3),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    new Border { CornerRadius = new CornerRadius(3), Background = Brush.Parse(typeColors.Item1), Padding = new Thickness(5, 1), Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(typeColors.Item2) } },
                    new TextBlock { Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center }
                }}
            };
            var ct = srcType; var ci = srcEid;
            row.PointerPressed += (_, e) =>
            { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct, ci); };
            list.Children.Add(row);
        }
        if (resolved.Count > 15)
            list.Children.Add(new TextBlock { Text = $"+ {resolved.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 2) });
        sp.Children.Add(VisHelper.Card(list));
        return sp;
    }

    // ═══════════════ Sound icon helpers ═══════════════

    private static Symbol? GetSoundSymbol(string? sound) => sound?.ToLowerInvariant() switch
    {
        "punch" or "claws" or "grasp" => Symbol.Flash,
        "club" => Symbol.Flash,
        "blade" => Symbol.Cut,
        "rifle" or "pistol" => Symbol.Target,
        "bow" or "throw" => Symbol.Target,
        "laser" => Symbol.Flash,
        "bite" => Symbol.Warning,
        "choke" => Symbol.Dismiss,
        _ => null
    };
}

// ══════════════════════════════════════════════════════════════════════════════
// BattleMove
// ══════════════════════════════════════════════════════════════════════════════

public class BattleMoveEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(BattleMove);

    // ═══════════════ Detail ═══════════════

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not BattleMove bm) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(bm), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(bm));
        root.Children.Add(BuildStatsPanel(bm));

        if (!string.IsNullOrWhiteSpace(bm.PopUp))
            root.Children.Add(BuildTextPanel(VisHelper.Loc("Vis.Description"), bm.PopUp, 800));
        if (!string.IsNullOrWhiteSpace(bm.Success))
            root.Children.Add(BuildTextPanel(VisHelper.Loc("Vis.OnSuccess"), bm.Success, 400, "#2E7D32"));
        if (!string.IsNullOrWhiteSpace(bm.Fail))
            root.Children.Add(BuildTextPanel(VisHelper.Loc("Vis.OnFail"), bm.Fail, 400, "#C62828"));

        root.Children.Add(BuildConditionsPanel(bm));
        root.Children.Add(BuildReverseRefsPanel(bm));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Overview ═══════════════

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not BattleMove bm) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var (typeLabel, typeBg, typeFg) = GetTypeBadge(bm);
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(typeBg),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });

        root.Children.Add(new TextBlock
        {
            Text = bm.Subject ?? bm.Name,
            FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
        });

        if (!string.IsNullOrWhiteSpace(bm.PopUp))
        {
            var quote = bm.PopUp.Length > 80 ? bm.PopUp[..80] + "..." : bm.PopUp;
            root.Children.Add(new TextBlock { Text = $"\"{quote}\"", FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        }

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
        var rangeText = bm.MinRange == -1 && bm.MaxRange == -1 ? "All"
            : bm.MinRange == 0 ? $"0–{bm.MaxRange}" : $"{bm.MinRange}–{bm.MaxRange}";
        var statRows = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Type"), GetAttackTypeLabel(bm), null),
            (VisHelper.Loc("Vis.Chance"), $"{bm.Chance:P0}", bm.Chance < 1 ? "#E65100" : null),
            (VisHelper.Loc("Vis.Priority"), $"{bm.Priority:F2}", null),
            (VisHelper.Loc("Vis.Fatigue"), $"{bm.Fatigue:F1}", bm.Fatigue > 0 ? "#C62828" : null),
            (VisHelper.Loc("Vis.Detect"), $"{bm.Detect:P0}", null),
            (VisHelper.Loc("Vis.Order"), $"{bm.Order:F2}", null),
            (VisHelper.Loc("Vis.Range"), rangeText, null),
            (VisHelper.Loc("Vis.Exposure"), $"them {FmtExp(bm.SeeThem)} / us {FmtExp(bm.SeeUs)}", null),
        };
        if (bm.MinCharges > 0)
            statRows.Add((VisHelper.Loc("Vis.MinCharges"), $"{bm.MinCharges}", null));
        root.Children.Add(VisHelper.BuildStatCard(statRows));

        var condCounts = GetConditionCounts(bm);
        if (condCounts.Count > 0)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.BattleMove.Conditions")));
            var wp = new WrapPanel();
            foreach (var (label, count) in condCounts)
                wp.Children.Add(VisHelper.MiniBadge($"{label}: {count}", "#E8EAF6", "#283593"));
            root.Children.Add(VisHelper.Card(wp));
        }

        return root;
    }

    // ═══════════════ Hero Header ═══════════════

    private static Control BuildHeroHeader(BattleMove bm)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        // --- badge row ---
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = $"ID: {bm.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") }
        });

        var (typeLabel, typeBg, typeFg) = GetTypeBadge(bm);
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(typeBg),
            Padding = new Thickness(8, 2),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 5,
                Children =
                {
                    new SymbolIcon { Symbol = GetTypeIconSymbol(bm), FontSize = 11, Foreground = Brush.Parse(typeFg) },
                    new TextBlock { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
                }
            }
        });

        if (!string.IsNullOrWhiteSpace(bm.StrId))
            badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#F3E5F5"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = bm.StrId, FontSize = 10, Foreground = Brush.Parse("#6A1B9A") } });

        var flags = new List<string>();
        if (bm.Offense) flags.Add(VisHelper.Loc("Vis.Offensive"));
        if (bm.Approach) flags.Add("Approach");
        if (bm.FallBack) flags.Add("FallBack");
        if (bm.Retreat) flags.Add(VisHelper.Loc("Vis.Retreat"));
        if (bm.Position) flags.Add("Position");
        if (bm.Passive) flags.Add(VisHelper.Loc("Vis.Passive"));
        if (bm.AllOutOfRange) flags.Add("AllOutOfRange");
        if (bm.InAttackRange) flags.Add("InAttackRange");
        if (flags.Count > 0)
            badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"{VisHelper.Loc("Vis.BattleMove.Flags")}: {string.Join(" · ", flags)}", FontSize = 10, Foreground = Brush.Parse("#283593") } });
        identity.Children.Add(badgeRow);

        // --- name ---
        identity.Children.Add(new TextBlock { Text = bm.Subject ?? bm.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(bm.Notes))
            identity.Children.Add(new TextBlock { Text = bm.Notes, FontSize = 12, Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap });

        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    // ═══════════════ Stats Panel ═══════════════

    private static Control BuildStatsPanel(BattleMove bm)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Stats")));

        var bars = new StackPanel { Spacing = 6 };

        // Chance — 0–1 probability, normalized
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Chance"), $"{bm.Chance:P0}", bm.Chance,
            bm.Chance >= 1 ? "#2E7D32" : "#E65100"));

        // Detect — 0–1 probability, normalized
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Detect"), $"{bm.Detect:P0}", bm.Detect,
            bm.Detect <= 0 ? "#2E7D32" : bm.Detect >= 0.5 ? "#C62828" : "#FB8C00"));

        // Priority — bot-only, default 0
        var priorityFill = Math.Clamp(bm.Priority / 1.0, 0.05, 1.0);
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Priority"), $"{bm.Priority:F2}", priorityFill,
            bm.Priority > 0 ? "#1565C0" : "#78909C"));

        // Key-value rows for non-normalized stats
        var grid = new Grid
        {
            ColumnDefinitions = { new(90, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(4, 0)
        };
        void AddKv(int i, string label, string value, string? color = null)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));
            var lbl = new TextBlock { Text = label, FontSize = 10, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 1) };
            var val = new TextBlock { Text = value, FontSize = 10, FontWeight = FontWeight.Medium, Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1) };
            Grid.SetRow(lbl, i); Grid.SetColumn(lbl, 0);
            Grid.SetRow(val, i); Grid.SetColumn(val, 1);
            grid.Children.Add(lbl); grid.Children.Add(val);
        }
        int row = 0;
        AddKv(row++, VisHelper.Loc("Vis.Fatigue"), $"{bm.Fatigue:F1}", bm.Fatigue > 0 ? "#C62828" : "#2E7D32");
        AddKv(row++, VisHelper.Loc("Vis.Order"), $"{bm.Order:F2}");
        var rangeText = bm.MinRange == -1 && bm.MaxRange == -1 ? "All"
            : bm.MinRange == 0 ? $"0–{bm.MaxRange}" : $"{bm.MinRange}–{bm.MaxRange}";
        AddKv(row++, VisHelper.Loc("Vis.Range"), rangeText);
        AddKv(row++, VisHelper.Loc("Vis.Exposure"), $"them {FmtExp(bm.SeeThem)} / us {FmtExp(bm.SeeUs)}");
        if (bm.MinCharges > 0)
            AddKv(row++, VisHelper.Loc("Vis.MinCharges"), $"{bm.MinCharges}");
        if (!string.IsNullOrWhiteSpace(bm.ChanceType) && bm.ChanceType != "0,0,0")
            AddKv(row++, "Chance Type", bm.ChanceType);

        bars.Children.Add(grid);
        sp.Children.Add(VisHelper.Card(bars));
        return sp;
    }

    // ═══════════════ Text panels ═══════════════

    private static Control BuildTextPanel(string label, string text, int maxLen, string? color = null)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(label));
        var display = text.Length > maxLen ? text[..maxLen] + "..." : text;
        sp.Children.Add(VisHelper.Card(new TextBlock { Text = display, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(color ?? "#333") }));
        return sp;
    }

    // ═══════════════ Conditions Panel ═══════════════

    private static Control BuildConditionsPanel(BattleMove bm)
    {
        var sp = new StackPanel { Spacing = 8 };
        var hasAny = false;

        void AddCondGroup(string label, string raw, string separator, string propName, string bg, string fg)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            hasAny = true;
            sp.Children.Add(VisHelper.SectionLabel(label));
            var wp = new WrapPanel();
            foreach (var seg in raw.Split(separator).Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var clean = seg.Trim('[', ']');
                var isNegative = clean.StartsWith("-");
                var lookupId = isNegative ? clean[1..] : seg;

                var cond = ReferenceResolver.Instance.LookupRef<Condition>(bm, propName, lookupId);
                if (cond is not null)
                {
                    var display = isNegative ? $"NOT {cond.Subject}" : cond.Subject;
                    var (cbg, cfg) = isNegative ? ("#FFEBEE", "#C62828") : (bg, fg);
                    wp.Children.Add(VisHelper.MiniBadge(display, cbg, cfg,
                        () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), cond.EntityId)));
                }
                else
                {
                    wp.Children.Add(VisHelper.MiniBadge(clean, "#F5F5F5", "#999"));
                }
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        // Pre-conditions — must have / must NOT have (negative IDs = "NOT")
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.UsPreCond"), bm.UsPreConditions, ",", nameof(BattleMove.UsPreConditions), "#FFF3E0", "#E65100");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.ThemPreCond"), bm.ThemPreConditions, ",", nameof(BattleMove.ThemPreConditions), "#FFF3E0", "#E65100");
        // Applied on success
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.UsRequired"), bm.UsConditions, "],[", nameof(BattleMove.UsConditions), "#FCE4EC", "#C62828");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.ThemRequired"), bm.ThemConditions, "],[", nameof(BattleMove.ThemConditions), "#FCE4EC", "#C62828");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.SelfEffects"), bm.PairConditions, "],[", nameof(BattleMove.PairConditions), "#E8EAF6", "#283593");
        // Applied on fail
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.UsFail"), bm.UsFailConditions, "],[", nameof(BattleMove.UsFailConditions), "#F5F5F5", "#999");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.ThemFail"), bm.ThemFailConditions, "],[", nameof(BattleMove.ThemFailConditions), "#F5F5F5", "#999");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.PairFail"), bm.PairFailConditions, "],[", nameof(BattleMove.PairFailConditions), "#F5F5F5", "#999");

        if (!hasAny) sp.Children.Add(new TextBlock { Text = "(No conditions)", FontSize = 11, Foreground = Brush.Parse("#999") });
        return sp;
    }

    // ═══════════════ Reverse References ═══════════════

    private static Control BuildReverseRefsPanel(BattleMove bm)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return new StackPanel();

        var rawRefs = store.Index.ReverseLookup(bm.EntityId);
        if (rawRefs.Count == 0) return new StackPanel();

        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var match = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (match != null)
                {
                    resolved.Add((t, match.Subject, srcEid, propName));
                    break;
                }
            }
        }

        if (resolved.Count == 0) return new StackPanel();

        var sp = new StackPanel();
        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var list = new StackPanel { Spacing = 3 };
        foreach (var (srcType, srcSubject, srcEid, _) in resolved.Take(15))
        {
            var typeColors = srcType == typeof(Creature) ? ("#E8EAF6", "#283593")
                           : srcType == typeof(ItemType) ? ("#E3F2FD", "#1565C0")
                           : ("#F5F5F5", "#666");

            var row = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#0D000000"),
                Padding = new Thickness(8, 3),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    Children =
                    {
                        new Border { CornerRadius = new CornerRadius(3), Background = Brush.Parse(typeColors.Item1), Padding = new Thickness(5, 1), Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(typeColors.Item2) } },
                        new TextBlock { Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            };
            var ct = srcType; var ci = srcEid;
            row.PointerPressed += (_, e) =>
            { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct, ci); };
            list.Children.Add(row);
        }
        if (resolved.Count > 15)
            list.Children.Add(new TextBlock { Text = $"+ {resolved.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 2) });
        sp.Children.Add(VisHelper.Card(list));
        return sp;
    }

    // ═══════════════ Helpers ═══════════════

    private static (string label, string bg, string fg) GetTypeBadge(BattleMove bm)
    {
        var kind = bm.Offense ? VisHelper.Loc("Vis.Offensive")
                 : bm.Retreat ? VisHelper.Loc("Vis.Retreat")
                 : bm.Passive ? VisHelper.Loc("Vis.Passive")
                 : VisHelper.Loc("Vis.Action");
        var attackLabel = GetAttackTypeLabel(bm);
        var label = $"{attackLabel} · {kind}";
        var bg = bm.Offense ? "#FFEBEE" : bm.Retreat ? "#E3F2FD" : bm.Passive ? "#F5F5F5" : "#FFF3E0";
        var fg = bm.Offense ? "#C62828" : bm.Retreat ? "#1565C0" : bm.Passive ? "#999" : "#E65100";
        return (label, bg, fg);
    }

    private static string GetAttackTypeLabel(BattleMove bm) => bm.AttackModeType switch
    {
        BattleMoveType.NonAttack => VisHelper.Loc("Vis.NonAttack"),
        BattleMoveType.Melee => VisHelper.Loc("Vis.CombatMelee"),
        BattleMoveType.Ranged => VisHelper.Loc("Vis.CombatRanged"),
        _ => "?"
    };

    private static Symbol GetTypeIconSymbol(BattleMove bm) => bm.AttackModeType switch
    {
        BattleMoveType.NonAttack => Symbol.Question,
        BattleMoveType.Melee => Symbol.Flash,
        BattleMoveType.Ranged => Symbol.Target,
        _ => Symbol.Question
    };

    private static List<(string label, int count)> GetConditionCounts(BattleMove bm)
    {
        var counts = new List<(string, int)>();
        int Count(string raw, string sep) => string.IsNullOrWhiteSpace(raw) ? 0
            : raw.Split(sep).Select(s => s.Trim()).Count(s => s.Length > 0);

        void Add(string label, int n) { if (n > 0) counts.Add((label, n)); }
        Add(VisHelper.Loc("Vis.BattleMove.UsPreCond"), Count(bm.UsPreConditions, ","));
        Add(VisHelper.Loc("Vis.BattleMove.ThemPreCond"), Count(bm.ThemPreConditions, ","));
        Add(VisHelper.Loc("Vis.BattleMove.UsRequired"), Count(bm.UsConditions, "],["));
        Add(VisHelper.Loc("Vis.BattleMove.ThemRequired"), Count(bm.ThemConditions, "],["));
        Add(VisHelper.Loc("Vis.BattleMove.SelfEffects"), Count(bm.PairConditions, "],["));
        Add(VisHelper.Loc("Vis.BattleMove.UsFail"), Count(bm.UsFailConditions, "],["));
        Add(VisHelper.Loc("Vis.BattleMove.ThemFail"), Count(bm.ThemFailConditions, "],["));
        Add(VisHelper.Loc("Vis.BattleMove.PairFail"), Count(bm.PairFailConditions, "],["));
        return counts;
    }

    private static string FmtExp(int level) => level switch
    {
        0 => $"{VisHelper.Loc("Vis.Exposure.Hidden")} (0)",
        1 => $"{VisHelper.Loc("Vis.Exposure.Seen")} (1)",
        _ => $"{VisHelper.Loc("Vis.Exposure.Any")} (2)"
    };
}

// ══════════════════════════════════════════════════════════════════════════════
// HexType
// ══════════════════════════════════════════════════════════════════════════════

public class HexTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(HexType);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not HexType ht) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(ht), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ht));
        root.Children.Add(BuildLightPanel(ht));
        root.Children.Add(BuildRefsPanel(ht));
        root.Children.Add(BuildReverseRefsPanel(ht));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not HexType ht) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var passLabel = ht.Passable == PassableType.Passable ? "Passable" : "Blocked";
        var passBg = ht.Passable == PassableType.Passable ? "#E8F5E9" : "#FFEBEE";
        var passFg = ht.Passable == PassableType.Passable ? "#2E7D32" : "#C62828";
        root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse(passBg), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = passLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(passFg) } });
        root.Children.Add(new TextBlock { Text = ht.Subject ?? ht.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel("Terrain"));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            ("Cost", $"{ht.TerrainCost}", null),
            ("Visibility", $"{ht.VizIncrease - ht.VizLimiter}", null),
            ("Enc Range", $"{ht.MinRange}–{ht.MaxRange}", null)
        }));

        return root;
    }

    private static Control BuildHeroHeader(HexType ht)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {ht.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        var passLabel = ht.Passable == PassableType.Passable ? "Passable" : "Blocked";
        var passBg = ht.Passable == PassableType.Passable ? "#E8F5E9" : "#FFEBEE";
        var passFg = ht.Passable == PassableType.Passable ? "#2E7D32" : "#C62828";
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse(passBg), Padding = new Thickness(8, 2), Child = new TextBlock { Text = passLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(passFg) } });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = ht.Subject ?? ht.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(ht.Description))
            identity.Children.Add(new TextBlock { Text = ht.Description, FontSize = 12, Foreground = Brush.Parse("#888") });

        var statRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock { Text = $"Cost: {ht.TerrainCost} AP", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = $"Visibility: {ht.VizIncrease - ht.VizLimiter} (+{ht.VizIncrease}, -{ht.VizLimiter})", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = $"Enc Range: {ht.MinRange}–{ht.MaxRange}", FontSize = 11, Foreground = Brush.Parse("#666") });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildLightPanel(HexType ht)
    {
        if (string.IsNullOrWhiteSpace(ht.LightLevels)) return new StackPanel();
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Light Levels"));
        var lightNames = new[] { "Dawn", "Morning", "Noon", "Afternoon", "Dusk", "Midnight" };
        var levels = ht.LightLevels.Split(',').Select(s => s.Trim()).ToList();
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star) }, Margin = new Thickness(4, 0) };
        grid.RowDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < lightNames.Length; i++)
        {
            var col = new StackPanel { Margin = new Thickness(2, 4) };
            col.Children.Add(new TextBlock { Text = lightNames[i], FontSize = 9, Foreground = Brush.Parse("#999"), TextAlignment = TextAlignment.Center });
            col.Children.Add(new TextBlock { Text = i < levels.Count ? levels[i] : "?", FontSize = 11, FontWeight = FontWeight.Medium, Foreground = Brush.Parse("#333"), TextAlignment = TextAlignment.Center });
            Grid.SetColumn(col, i); grid.Children.Add(col);
        }
        sp.Children.Add(VisHelper.Card(grid));
        return sp;
    }

    private static Control BuildRefsPanel(HexType ht)
    {
        var sp = new StackPanel { Spacing = 8 };
        var hasAny = false;

        void AddRef<T>(string label, string raw, string propName, string bg, string fg) where T : IEntity
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "3" || raw == "25") return;
            hasAny = true;
            sp.Children.Add(VisHelper.SectionLabel(label));
            var wp = new WrapPanel();
            foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var match = ReferenceResolver.Instance.LookupRef<T>(ht, propName, seg);
                if (match is not null)
                    wp.Children.Add(VisHelper.MiniBadge(match.Subject, bg, fg, () => ReferenceResolver.Instance.NavigateTo(typeof(T), match.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        AddRef<TreasureTable>("Scavenge Loot", ht.TreasureId, nameof(HexType.TreasureId), "#E8F5E9", "#2E7D32");
        AddRef<TreasureTable>("Initial Scavenge", ht.ScavengeInitialId, nameof(HexType.ScavengeInitialId), "#E0F2F1", "#00695C");
        AddRef<TreasureTable>("Hourly Scavenge", ht.ScavengeItemsIdPerHour, nameof(HexType.ScavengeItemsIdPerHour), "#B2DFDB", "#004D40");
        AddRef<Condition>("On-Enter Conditions", ht.ConditionIds, nameof(HexType.ConditionIds), "#FCE4EC", "#C62828");

        if (ht.DefaultCampId != 517)
        {
            hasAny = true;
            sp.Children.Add(VisHelper.SectionLabel("Default Camp"));
            var wp = new WrapPanel();
            var camp = ReferenceResolver.Instance.LookupRef<CampType>(ht, nameof(HexType.DefaultCampId), ht.DefaultCampId.ToString());
            if (camp is not null)
                wp.Children.Add(VisHelper.MiniBadge(camp.Subject, "#FFF3E0", "#E65100", () => ReferenceResolver.Instance.NavigateTo(typeof(CampType), camp.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{ht.DefaultCampId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!hasAny) sp.Children.Add(new TextBlock { Text = "(No references)", FontSize = 11, Foreground = Brush.Parse("#999") });
        return sp;
    }

    private static Control BuildReverseRefsPanel(HexType ht)
        => VisHelper.BuildReverseRefsPanel(ht.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// Faction
// ══════════════════════════════════════════════════════════════════════════════

public class FactionEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Faction);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Faction f) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(f), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(f));
        if (!string.IsNullOrWhiteSpace(f.DictFactions))
            root.Children.Add(BuildRelationsPanel(f));
        root.Children.Add(BuildMembersPanel(f));
        root.Children.Add(BuildReverseRefsPanel(f));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Faction f) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock { Text = f.Subject ?? f.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        var relationCount = string.IsNullOrWhiteSpace(f.DictFactions) ? 0 : f.DictFactions.Split(',').Length;
        var memberCount = 0;
        if (GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Creature), out var cl) && cl is not null)
            memberCount = cl.OfType<Creature>().Count(c => c.Faction == f.Id.ToString());

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Diplomacy")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            ("Relations", $"{relationCount}", null),
            (VisHelper.Loc("Vis.Members"), $"{memberCount}", null)
        }));

        return root;
    }

    private static Control BuildHeroHeader(Faction f)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {f.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = f.Subject ?? f.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildRelationsPanel(Faction f)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Diplomacy")));
        var relationsStack = new StackPanel { Spacing = 3 };

        foreach (var seg in f.DictFactions.Split(','))
        {
            var parts = seg.Trim().Split('=');
            if (parts.Length < 2) continue;
            var fid = parts[0].Trim();
            var relVal = int.TryParse(parts[1].Trim(), out var rv) ? rv : 0;
            var otherFaction = ReferenceResolver.Instance.LookupRef<Faction>(f, nameof(Faction.DictFactions), seg.Trim());
            var otherName = otherFaction?.Name ?? fid;
            var relDesc = relVal >= 100 ? "Allied" : relVal >= 50 ? "Friendly" : relVal >= 0 ? "Neutral" : relVal >= -50 ? "Hostile" : "Enemy";

            relationsStack.Children.Add(VisHelper.CenteredStatBar(otherName, $"{relVal:+#;-#;0} ({relDesc})",
                relVal, 100.0));
        }
        sp.Children.Add(VisHelper.Card(relationsStack));
        return sp;
    }

    private static Control BuildMembersPanel(Faction f)
    {
        var sp = new StackPanel();
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Creature), out var creatureList) || creatureList is null)
            return sp;
        var members = creatureList.OfType<Creature>().Where(c => c.Faction == f.Id.ToString()).ToList();
        if (members.Count == 0) return sp;

        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.Members")} ({members.Count})"));
        var wp = new WrapPanel();
        foreach (var m in members)
            wp.Children.Add(VisHelper.MiniBadge(m.Subject, "#E8EAF6", "#283593", () => ReferenceResolver.Instance.NavigateTo(typeof(Creature), m.EntityId)));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    private static Control BuildReverseRefsPanel(Faction f)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return new StackPanel();
        var rawRefs = store.Index.ReverseLookup(f.EntityId);
        if (rawRefs.Count == 0) return new StackPanel();

        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var match = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (match != null) { resolved.Add((t, match.Subject, srcEid, propName)); break; }
            }
        }
        if (resolved.Count == 0) return new StackPanel();

        var sp = new StackPanel();
        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var list = new StackPanel { Spacing = 3 };
        foreach (var (srcType, srcSubject, srcEid, _) in resolved.Take(15))
        {
            var tc = ("#F5F5F5", "#666");
            var row = new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#0D000000"), Padding = new Thickness(8, 3), Cursor = new Cursor(StandardCursorType.Hand), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { CornerRadius = new CornerRadius(3), Background = Brush.Parse(tc.Item1), Padding = new Thickness(5, 1), Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(tc.Item2) } }, new TextBlock { Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center } } } };
            var ct = srcType; var ci = srcEid;
            row.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct, ci); };
            list.Children.Add(row);
        }
        if (resolved.Count > 15) list.Children.Add(new TextBlock { Text = $"+ {resolved.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 2) });
        sp.Children.Add(VisHelper.Card(list));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Ingredient
// ══════════════════════════════════════════════════════════════════════════════

public class IngredientEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Ingredient);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Ingredient ing) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(ing), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ing));
        root.Children.Add(BuildPropsPanel(ing));
        root.Children.Add(BuildReversePanel(ing));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Ingredient ing) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock { Text = ing.Subject ?? ing.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        var reqCount = string.IsNullOrWhiteSpace(ing.RequiredProps) ? 0 : ing.RequiredProps.Split('&').Length;
        var forbCount = string.IsNullOrWhiteSpace(ing.ForbidProps) ? 0 : ing.ForbidProps.Split('&').Length;
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel("Properties"));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            ("Required", $"{reqCount} props", "#2E7D32"),
            ("Forbidden", $"{forbCount} props", "#C62828")
        }));

        return root;
    }

    private static Control BuildHeroHeader(Ingredient ing)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {ing.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = ing.Subject ?? ing.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildPropsPanel(Ingredient ing)
    {
        var sp = new StackPanel { Spacing = 8 };

        void AddProps(string label, string raw, string propName, string bg, string fg)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            sp.Children.Add(VisHelper.SectionLabel(label));
            var wp = new WrapPanel();
            foreach (var s in raw.Split('&').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                var p = ReferenceResolver.Instance.LookupRef<ItemProp>(ing, propName, s);
                if (p is not null)
                    wp.Children.Add(VisHelper.MiniBadge(p.PropertyName, bg, fg,
                        () => ReferenceResolver.Instance.NavigateTo(typeof(ItemProp), p.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(s, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }

        AddProps("Required Properties", ing.RequiredProps, nameof(Ingredient.RequiredProps), "#E8F5E9", "#2E7D32");
        AddProps("Forbidden Properties", ing.ForbidProps, nameof(Ingredient.ForbidProps), "#FFEBEE", "#C62828");
        return sp;
    }

    private static Control BuildReversePanel(Ingredient ing)
    {
        var sp = new StackPanel();
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        var revRefs = store is not null ? ReferenceResolver.ResolveReverseRefs(store, ing.EntityId) : [];
        if (revRefs.Count == 0) return sp;

        sp.Children.Add(VisHelper.SectionLabel($"Used in {revRefs.Count} Recipe(s)"));
        var wp = new WrapPanel();
        foreach (var (_, srcSubject, srcEid, _) in revRefs.Take(20))
            wp.Children.Add(VisHelper.MiniBadge(srcSubject, "#F3E5F5", "#6A1B9A", () => ReferenceResolver.Instance.NavigateTo(typeof(Recipe), srcEid)));
        if (revRefs.Count > 20)
            wp.Children.Add(VisHelper.MiniBadge($"+{revRefs.Count - 20} more", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ItemProp
// ══════════════════════════════════════════════════════════════════════════════

public class ItemPropEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ItemProp);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ItemProp ip) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(ip), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ip));
        root.Children.Add(BuildReversePanel(ip));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        // Same as Detail for this simple type — reverse refs are the main content
        return BuildDetail(entity);
    }

    private static Control BuildHeroHeader(ItemProp ip)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {ip.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = ip.PropertyName ?? ip.Subject, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildReversePanel(ItemProp ip)
    {
        var sp = new StackPanel();
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return sp;
        var rawRefs = store.Index.ReverseLookup(ip.EntityId);
        if (rawRefs.Count == 0) return sp;

        var resolved = new List<(Type, string, string)>();
        foreach (var (srcEid, _, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var m = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (m != null) { resolved.Add((t, m.Subject, srcEid)); break; }
            }
        }
        if (resolved.Count == 0) return sp;

        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.UsedBy")} ({resolved.Count})"));
        var wp = new WrapPanel();
        foreach (var (srcType, srcSubject, srcEid) in resolved.Take(30))
        {
            wp.Children.Add(VisHelper.MiniBadge($"{srcType.Name}: {srcSubject}", "#E8F5E9", "#2E7D32",
                () => ReferenceResolver.Instance.NavigateTo(srcType, srcEid)));
        }
        if (resolved.Count > 30)
            wp.Children.Add(VisHelper.MiniBadge($"+{resolved.Count - 30} more", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// EncounterTrigger
// ══════════════════════════════════════════════════════════════════════════════

public class EncounterTriggerEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(EncounterTrigger);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not EncounterTrigger et) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(et), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(et));
        root.Children.Add(BuildStatsPanel(et));
        root.Children.Add(BuildRefsPanel(et));
        root.Children.Add(BuildReverseRefsPanel(et));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not EncounterTrigger et) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var types = new List<string>();
        if (et.LocBased) types.Add("Location");
        if (et.DateBased) types.Add("Date");
        if (et.HexBased) types.Add("Hex");
        if (et.Unique) types.Add("Unique");
        root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = types.Count > 0 ? string.Join(" + ", types) : "Manual", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#283593") } });
        root.Children.Add(new TextBlock { Text = et.Subject ?? et.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel("Trigger"));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            ("Chance", $"{et.Chance:P0}", null),
            ("Encounter", $"#{et.EncounterId}", et.EncounterId != 0 ? null : "#999")
        }));

        return root;
    }

    private static Control BuildHeroHeader(EncounterTrigger et)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {et.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        var types = new List<string>();
        if (et.LocBased) types.Add("Location");
        if (et.DateBased) types.Add("Date");
        if (et.HexBased) types.Add("Hex");
        if (et.Unique) types.Add("Unique");
        if (et.AIPassable) types.Add("AI");
        if (types.Count > 0)
            badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = string.Join(" · ", types), FontSize = 10, Foreground = Brush.Parse("#283593") } });
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"Chance: {et.Chance:P0}", FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = et.Subject ?? et.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(et.Area))
            identity.Children.Add(new TextBlock { Text = $"Area: {et.Area}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (!string.IsNullOrWhiteSpace(et.DateMin) || !string.IsNullOrWhiteSpace(et.DateMax))
            identity.Children.Add(new TextBlock { Text = $"Date: {et.DateMin} – {et.DateMax}", FontSize = 11, Foreground = Brush.Parse("#666") });

        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildRefsPanel(EncounterTrigger et)
    {
        var sp = new StackPanel { Spacing = 8 };
        if (et.EncounterId != 0)
        {
            sp.Children.Add(VisHelper.SectionLabel("Encounter"));
            var wp = new WrapPanel();
            var enc = ReferenceResolver.Instance.LookupRef<Encounter>(et, nameof(EncounterTrigger.EncounterId), et.EncounterId.ToString());
            if (enc is not null)
                wp.Children.Add(VisHelper.MiniBadge(enc.Subject, "#E8F5E9", "#2E7D32", () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), enc.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{et.EncounterId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }
        if (!string.IsNullOrWhiteSpace(et.HexTypes))
        {
            sp.Children.Add(VisHelper.SectionLabel("Hex Types"));
            var wp = new WrapPanel();
            foreach (var seg in et.HexTypes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var ht = ReferenceResolver.Instance.LookupRef<HexType>(et, nameof(EncounterTrigger.HexTypes), seg);
                if (ht is not null)
                    wp.Children.Add(VisHelper.MiniBadge(ht.Subject, "#E0F2F1", "#00695C", () => ReferenceResolver.Instance.NavigateToByKeyFor<HexType>(ht.Id, et)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            sp.Children.Add(VisHelper.Card(wp));
        }
        return sp;
    }

    private static Control BuildReverseRefsPanel(EncounterTrigger et)
        => VisHelper.BuildReverseRefsPanel(et.EntityId);

    private static Control BuildStatsPanel(EncounterTrigger et)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.TriggerDetails")));
        var stats = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Chance"), $"{et.Chance:P0}", et.Chance > 0 ? "#1565C0" : "#999"),
            (VisHelper.Loc("Vis.Unique"), et.Unique ? "Yes (once)" : "No (repeatable)", et.Unique ? "#E65100" : "#999"),
            (VisHelper.Loc("Vis.AIPassable"), et.AIPassable ? "Yes" : "No", null),
        };
        if (!string.IsNullOrWhiteSpace(et.Area))
            stats.Add((VisHelper.Loc("Vis.Area"), et.Area, "#2E7D32"));
        if (!string.IsNullOrWhiteSpace(et.DateMin))
            stats.Add((VisHelper.Loc("Vis.DateRange"), $"{et.DateMin} – {et.DateMax}", "#6A1B9A"));
        sp.Children.Add(VisHelper.BuildStatCard(stats));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// CampType
// ══════════════════════════════════════════════════════════════════════════════

public class CampTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(CampType);

    // ═══════════════ Detail ═══════════════

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not CampType ct) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(ct), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ct));
        root.Children.Add(BuildStatsPanel(ct));
        if (!string.IsNullOrWhiteSpace(ct.TreasureId) && ct.TreasureId != "3")
            root.Children.Add(BuildLootPanel(ct));
        root.Children.Add(BuildReverseRefsPanel(ct));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Overview ═══════════════

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not CampType ct) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var bmp = VisHelper.LoadImage(ct.ImageList);
        var imgStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
        if (bmp is not null)
        {
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8),
                ClipToBounds = true, Background = Brush.Parse("#0A000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 }
            });
        }
        else
        {
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8),
                Background = Brush.Parse("#0A000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new SymbolIcon { Symbol = Symbol.Home, FontSize = 32, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });
        }
        root.Children.Add(imgStack);

        root.Children.Add(new TextBlock { Text = ct.Description ?? $"Camp #{ct.Id}", FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        if (!string.IsNullOrWhiteSpace(ct.Capacities))
            root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = ct.Capacities, FontSize = 10, Foreground = Brush.Parse("#E65100") } });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.CampStats")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.SleepQuality"), $"{ct.SleepQuality:P0}", ct.SleepQuality > 0 ? "#2E7D32" : ct.SleepQuality < 0 ? "#C62828" : null),
            (VisHelper.Loc("Vis.HealPerHour"), $"{ct.HealPerHourMod:P0}", ct.HealPerHourMod > 0 ? "#2E7D32" : null),
            (VisHelper.Loc("Vis.VisibilityMod"), $"{ct.Visibility:P0}", ct.Visibility < 0 ? "#2E7D32" : null),
            (VisHelper.Loc("Vis.Alertness"), $"{ct.Alertness:P0}", ct.Alertness > 0 ? "#C62828" : null),
            (VisHelper.Loc("Vis.TempAdjust"), $"{ct.WetTempAdjustMod:+#;-#;0}", null),
        }));

        return root;
    }

    // ═══════════════ Hero Header ═══════════════

    private static Control BuildHeroHeader(CampType ct)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var bmp = VisHelper.LoadImage(ct.ImageList);
        var imageArea = new Border
        {
            Width = 132, Height = 132,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Background = Brush.Parse("#0A000000"),
            VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon { Symbol = Symbol.Home, FontSize = 40, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(imageArea, 0); grid.Children.Add(imageArea);

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {ct.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = ct.Capacities, FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock { Text = ct.Description ?? $"Camp #{ct.Id}", FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        var statRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.SleepQuality")}: {ct.SleepQuality:P0}", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.HealPerHour")}: {ct.HealPerHourMod:P0}", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.Alertness")}: {ct.Alertness:P0}", FontSize = 11, Foreground = Brush.Parse("#666") });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 1); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    // ═══════════════ Stats Panel ═══════════════

    private static Control BuildStatsPanel(CampType ct)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.CampStats")));

        var bars = new StackPanel { Spacing = 6 };

        // Alertness — 0 to 1, higher = more dangerous guards
        bars.Children.Add(VisHelper.CenteredStatBar(VisHelper.Loc("Vis.Alertness"), $"{ct.Alertness:P0}",
            ct.Alertness, 1.0, negColor: "#78909C"));

        // Visibility — negative = stealth bonus, positive = exposed
        bars.Children.Add(VisHelper.CenteredStatBar(VisHelper.Loc("Vis.VisibilityMod"), $"{ct.Visibility:P0}",
            ct.Visibility, 1.0));

        // Sleep quality — -1 to 1, 0 = baseline
        bars.Children.Add(VisHelper.CenteredStatBar(VisHelper.Loc("Vis.SleepQuality"), $"{ct.SleepQuality:P0}",
            ct.SleepQuality, 1.0));

        // Temp adjust — degrees, ±5°C max range
        bars.Children.Add(VisHelper.CenteredStatBar(VisHelper.Loc("Vis.TempAdjust"), $"{ct.WetTempAdjustMod:+#;-#;0}",
            ct.WetTempAdjustMod, 5.0));

        // Heal per hour — positive = healing, bottom of the list
        bars.Children.Add(VisHelper.CenteredStatBar(VisHelper.Loc("Vis.HealPerHour"), $"{ct.HealPerHourMod:P0}",
            ct.HealPerHourMod, 1.0, negColor: "#78909C"));

        sp.Children.Add(VisHelper.Card(bars));
        return sp;
    }

    // ═══════════════ Loot Panel ═══════════════

    private static Control BuildLootPanel(CampType ct)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.LootTable")));
        var wp = new WrapPanel();
        var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(ct, nameof(CampType.TreasureId), ct.TreasureId);
        if (tt is not null)
            wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32",
                () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
        else
            wp.Children.Add(VisHelper.MiniBadge(ct.TreasureId, "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    // ═══════════════ Reverse References ═══════════════

    private static Control BuildReverseRefsPanel(CampType ct)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return new StackPanel();

        var rawRefs = store.Index.ReverseLookup(ct.EntityId);
        if (rawRefs.Count == 0) return new StackPanel();

        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var match = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (match != null)
                {
                    resolved.Add((t, match.Subject, srcEid, propName));
                    break;
                }
            }
        }

        if (resolved.Count == 0) return new StackPanel();

        var sp = new StackPanel();
        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var list = new StackPanel { Spacing = 3 };
        foreach (var (srcType, srcSubject, srcEid, _) in resolved.Take(15))
        {
            var typeColors = srcType == typeof(Creature) ? ("#E8EAF6", "#283593")
                           : srcType == typeof(ItemType) ? ("#E3F2FD", "#1565C0")
                           : ("#F5F5F5", "#666");

            var row = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#0D000000"),
                Padding = new Thickness(8, 3),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    new Border { CornerRadius = new CornerRadius(3), Background = Brush.Parse(typeColors.Item1), Padding = new Thickness(5, 1), Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(typeColors.Item2) } },
                    new TextBlock { Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center }
                }}
            };
            var ct2 = srcType; var ci = srcEid;
            row.PointerPressed += (_, e) =>
            { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct2, ci); };
            list.Children.Add(row);
        }
        if (resolved.Count > 15)
            list.Children.Add(new TextBlock { Text = $"+ {resolved.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 2) });
        sp.Children.Add(VisHelper.Card(list));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ChargeProfile
// ══════════════════════════════════════════════════════════════════════════════

public class ChargeProfileEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ChargeProfile);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ChargeProfile cp) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(cp), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(cp));
        root.Children.Add(BuildStatsPanel(cp));
        root.Children.Add(BuildReverseRefsPanel(cp));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not ChargeProfile cp) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock { Text = cp.Subject ?? cp.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        if (cp.Degrade)
            root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = "Degradable", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#E65100") } });

        var rates = new List<string>();
        if (cp.PerUse != 0) rates.Add($"Use: {cp.PerUse:F1}");
        if (cp.PerHour != 0) rates.Add($"Hr: {cp.PerHour:F1}");
        if (cp.PerHourEquipped != 0) rates.Add($"Eqp: {cp.PerHourEquipped:F1}");
        if (cp.PerHex != 0) rates.Add($"Hex: {cp.PerHex:F1}");
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.ConsumptionRates")));
        root.Children.Add(new TextBlock { Text = string.Join("  ·  ", rates.Count > 0 ? rates : ["(no consumption)"]), FontSize = 10, Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        root.Children.Add(VisHelper.Kv("Item", cp.ItemId, 40));

        return root;
    }

    private static Control BuildHeroHeader(ChargeProfile cp)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {cp.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        if (cp.Degrade) badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = "Degradable", FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = cp.Subject ?? cp.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        identity.Children.Add(new TextBlock { Text = $"Item: {cp.ItemId}", FontSize = 12, Foreground = Brush.Parse("#888") });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(ChargeProfile cp)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.ConsumptionRates")));
        sp.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            ("Per Use", $"{cp.PerUse:F2}", cp.PerUse > 0 ? "#C62828" : null),
            ("Per Hour", $"{cp.PerHour:F2}", cp.PerHour > 0 ? "#E65100" : null),
            ("Per Hr Equipped", $"{cp.PerHourEquipped:F2}", cp.PerHourEquipped > 0 ? "#FB8C00" : null),
            ("Per Hex", $"{cp.PerHex:F2}", cp.PerHex > 0 ? "#6A1B9A" : null)
        }));
        return sp;
    }

    private static Control BuildReverseRefsPanel(ChargeProfile cp)
        => VisHelper.BuildReverseRefsPanel(cp.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// ContainerType
// ══════════════════════════════════════════════════════════════════════════════

public class ContainerTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ContainerType);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ContainerType ct) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(ct), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ct));
        root.Children.Add(BuildReversePanel(ct));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        // Same as Detail — simple type with only reverse refs as content
        return BuildDetail(entity);
    }

    private static Control BuildHeroHeader(ContainerType ct)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {ct.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = ct.Subject ?? ct.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildReversePanel(ContainerType ct)
    {
        var sp = new StackPanel();
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return sp;
        var rawRefs = store.Index.ReverseLookup(ct.EntityId);
        if (rawRefs.Count == 0) return sp;

        var resolved = new List<(string, string)>();
        foreach (var (srcEid, _, _) in rawRefs)
        {
            if (store.ReferenceLookups.TryGetValue(typeof(ItemType), out var list) && list is not null)
            {
                var m = list.OfType<ItemType>().FirstOrDefault(e => e.EntityId == srcEid);
                if (m != null) resolved.Add((m.Subject, m.EntityId));
            }
        }
        if (resolved.Count == 0) return sp;

        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.UsedBy")} ({resolved.Count})"));
        var wp = new WrapPanel();
        foreach (var (subject, eid) in resolved.Take(20))
            wp.Children.Add(VisHelper.MiniBadge(subject, "#E3F2FD", "#1565C0", () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), eid)));
        if (resolved.Count > 20)
            wp.Children.Add(VisHelper.MiniBadge($"+{resolved.Count - 20} more", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// CreatureSource
// ══════════════════════════════════════════════════════════════════════════════

public class CreatureSourceEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(CreatureSource);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not CreatureSource cs) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(cs), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(cs));
        root.Children.Add(BuildStatsPanel(cs));
        if (!string.IsNullOrWhiteSpace(cs.CreatureId) && cs.CreatureId != "0")
            root.Children.Add(BuildCreaturePanel(cs));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not CreatureSource cs) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock { Text = cs.Subject ?? cs.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = $"({cs.X}, {cs.Y}) · {cs.Min}–{cs.Max}", FontSize = 10, Foreground = Brush.Parse("#E65100") } });

        var (totalW, proportion) = GetWeightInfo(cs);
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Spawn")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({cs.X}, {cs.Y})", null),
            (VisHelper.Loc("Vis.Count"), $"{cs.Min}–{cs.Max}", null),
            ("Weight", $"{cs.Weight:F2} ({proportion:P0})", null)
        }));

        return root;
    }

    private static (double TotalWeight, double Proportion) GetWeightInfo(CreatureSource cs)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store?.ReferenceLookups.TryGetValue(typeof(CreatureSource), out var list) != true || list is null)
            return (cs.Weight, 1.0);
        var atPos = list.OfType<CreatureSource>().Where(s => s.X == cs.X && s.Y == cs.Y).ToList();
        var total = atPos.Sum(s => s.Weight);
        return (total, total > 0 ? cs.Weight / total : 1.0);
    }

    private static Control BuildHeroHeader(CreatureSource cs)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {cs.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"({cs.X}, {cs.Y}) · {cs.Min}–{cs.Max}", FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = cs.Subject ?? cs.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        var (totalW, proportion) = GetWeightInfo(cs);
        identity.Children.Add(new TextBlock { Text = $"Weight: {cs.Weight:F2} ({proportion:P0} of total {totalW:F1} at this location)", FontSize = 12, Foreground = Brush.Parse("#888") });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(CreatureSource cs)
    {
        var (totalW, proportion) = GetWeightInfo(cs);
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Spawn")));
        sp.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({cs.X}, {cs.Y})", null),
            (VisHelper.Loc("Vis.Count"), $"{cs.Min}–{cs.Max}", cs.Max > 0 ? "#1565C0" : null),
            ("Weight", $"{cs.Weight:F2} ({proportion:P0} of position total)", null)
        }));
        return sp;
    }

    private static Control BuildCreaturePanel(CreatureSource cs)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Creature")));
        var wp = new WrapPanel();
        var creature = ReferenceResolver.Instance.LookupRef<Creature>(cs, nameof(CreatureSource.CreatureId), cs.CreatureId);
        if (creature is not null)
            wp.Children.Add(VisHelper.MiniBadge(creature.Subject, "#E8EAF6", "#283593", () => ReferenceResolver.Instance.NavigateTo(typeof(Creature), creature.EntityId)));
        else
            wp.Children.Add(VisHelper.MiniBadge($"#{cs.CreatureId}", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DmcPlace
// ══════════════════════════════════════════════════════════════════════════════

public class DmcPlaceEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(DmcPlace);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not DmcPlace dp) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(dp), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(dp));
        root.Children.Add(BuildRefsPanel(dp));
        root.Children.Add(BuildReverseRefsPanel(dp));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not DmcPlace dp) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var bmp = VisHelper.LoadImage(dp.Image);
        var imgStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 4 };
        if (bmp is not null)
            imgStack.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 } });
        root.Children.Add(imgStack);

        root.Children.Add(new TextBlock { Text = dp.Subject ?? $"DMC Place #{dp.Id}", FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel("Location"));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            ("Position", $"({dp.X}, {dp.Y})", null),
            ("Encounter", $"#{dp.EncounterId}", dp.EncounterId != 1 ? null : "#999")
        }));

        return root;
    }

    private static Control BuildHeroHeader(DmcPlace dp)
    {
        var grid = new Grid { ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var bmp = VisHelper.LoadImage(dp.Image);
        var imageArea = new Border { Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon { Symbol = Symbol.Building, FontSize = 40, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(imageArea, 0); grid.Children.Add(imageArea);

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {dp.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"({dp.X}, {dp.Y})", FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = dp.Subject ?? $"DMC Place #{dp.Id}", FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(dp.Image))
            identity.Children.Add(new TextBlock { Text = $"Icon: {dp.Image}", FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildRefsPanel(DmcPlace dp)
    {
        var sp = new StackPanel();
        if (dp.EncounterId == 1) return sp;
        sp.Children.Add(VisHelper.SectionLabel("Encounter"));
        var wp = new WrapPanel();
        var enc = ReferenceResolver.Instance.LookupRef<Encounter>(dp, nameof(DmcPlace.EncounterId), dp.EncounterId.ToString());
        if (enc is not null)
            wp.Children.Add(VisHelper.MiniBadge(enc.Subject, "#E8F5E9", "#2E7D32", () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), enc.EntityId)));
        else
            wp.Children.Add(VisHelper.MiniBadge($"#{dp.EncounterId}", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    private static Control BuildReverseRefsPanel(DmcPlace dp)
        => VisHelper.BuildReverseRefsPanel(dp.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// BarterHex (NEW)
// ══════════════════════════════════════════════════════════════════════════════

public class BarterHexEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(BarterHex);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not BarterHex bh) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(bh), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(bh));
        root.Children.Add(BuildStatsPanel(bh));
        if (bh.RestockTreasureId > 0 && bh.RestockTreasureId != 3)
            root.Children.Add(BuildRestockPanel(bh));
        root.Children.Add(BuildReverseRefsPanel(bh));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not BarterHex bh) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };
        var shopLabel = bh.Buys ? "Shop (Buys)" : "Shop (Sells Only)";
        root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = shopLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") } });
        root.Children.Add(new TextBlock { Text = $"Barter Hex #{bh.Id}", FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        
        // Resolve RestockTT name
        var restockLabel = $"#{bh.RestockTreasureId}";
        if (bh.RestockTreasureId > 0 && bh.RestockTreasureId != 3)
        {
            var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
            if (store?.ReferenceLookups.TryGetValue(typeof(TreasureTable), out var list) == true && list is not null)
            {
                var tt = list.OfType<TreasureTable>().FirstOrDefault(t => t.Id == bh.RestockTreasureId);
                if (tt is not null) restockLabel = tt.Subject;
            }
        }
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Location")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({bh.X}, {bh.Y})", null),
            ("Restock", restockLabel, bh.RestockTreasureId != 3 ? "#1565C0" : "#999")
        }));
        return root;
    }

    private static Control BuildHeroHeader(BarterHex bh)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {bh.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse(bh.Buys ? "#E8F5E9" : "#FCE4EC"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = bh.Buys ? "Buys Items" : "Sells Only", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(bh.Buys ? "#2E7D32" : "#C62828") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = $"Barter Hex #{bh.Id}", FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        identity.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.Position")}: ({bh.X}, {bh.Y})", FontSize = 12, Foreground = Brush.Parse("#888") });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(BarterHex bh)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.ShopInfo")));
        var stats = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({bh.X}, {bh.Y})", null),
            (VisHelper.Loc("Vis.Buys"), bh.Buys ? "Yes" : "No", bh.Buys ? "#2E7D32" : "#999"),
        };
        if (bh.RestockTreasureId > 0)
            stats.Add(("Restock TT", $"#{bh.RestockTreasureId}", bh.RestockTreasureId != 3 ? "#1565C0" : "#999"));
        sp.Children.Add(VisHelper.BuildStatCard(stats));
        return sp;
    }

    private static Control BuildRestockPanel(BarterHex bh)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel("Restock Treasure Table"));
        var wp = new WrapPanel();
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store?.ReferenceLookups.TryGetValue(typeof(TreasureTable), out var list) == true && list is not null)
        {
            var tt = list.OfType<TreasureTable>().FirstOrDefault(t => t.Id == bh.RestockTreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Subject, "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"TT #{bh.RestockTreasureId}", "#F5F5F5", "#999"));
        }
        else
            wp.Children.Add(VisHelper.MiniBadge($"TT #{bh.RestockTreasureId}", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    private static Control BuildReverseRefsPanel(BarterHex bh)
        => VisHelper.BuildReverseRefsPanel(bh.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// DataFile (NEW)
// ══════════════════════════════════════════════════════════════════════════════

public class DataFileEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(DataFile);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not DataFile df) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(df), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(df));
        if (!string.IsNullOrWhiteSpace(df.Description))
        {
            var sp = new StackPanel();
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Content")));
            var desc = df.Description.Length > 2000 ? df.Description[..2000] + "..." : df.Description;
            sp.Children.Add(VisHelper.Card(new TextBlock { Text = desc, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
            root.Children.Add(sp);
        }

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not DataFile df) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var bmp = VisHelper.LoadImage(df.Image);
        if (bmp is not null)
            root.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 } });
        else
            root.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new SymbolIcon { Symbol = Symbol.Document, FontSize = 32, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });

        root.Children.Add(new TextBlock { Text = df.Subject ?? df.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        if (df.Value > 0)
            root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = $"$ {df.Value:F2}", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") } });

        return root;
    }

    private static Control BuildHeroHeader(DataFile df)
    {
        var grid = new Grid { ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var bmp = VisHelper.LoadImage(df.Image);
        var imageArea = new Border { Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon { Symbol = Symbol.Document, FontSize = 40, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(imageArea, 0); grid.Children.Add(imageArea);

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {df.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        if (df.Value > 0) badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"$ {df.Value:F2}", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = df.Subject ?? df.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(df.Image))
            identity.Children.Add(new TextBlock { Text = df.Image, FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// GameVar (NEW)
// ══════════════════════════════════════════════════════════════════════════════

public class GameVarEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(GameVar);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not GameVar gv) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(gv), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(gv));
        root.Children.Add(BuildStatsPanel(gv));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not GameVar gv) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = gv.Type, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        root.Children.Add(new TextBlock { Text = gv.Subject ?? gv.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Type"), gv.Type, null),
            (VisHelper.Loc("Vis.Value"), gv.Value, "#2E7D32")
        }));
        return root;
    }

    private static Control BuildHeroHeader(GameVar gv)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = gv.Type, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = gv.Subject ?? gv.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        identity.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.Value")}: {gv.Value}", FontSize = 14, Foreground = Brush.Parse("#2E7D32") });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(GameVar gv)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Stats")));
        sp.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Type"), gv.Type, "#1565C0"),
            (VisHelper.Loc("Vis.Value"), gv.Value, "#2E7D32")
        }));
        return sp;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Headline (NEW)
// ══════════════════════════════════════════════════════════════════════════════

public class HeadlineEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Headline);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Headline h) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(h), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(h));
        if (!string.IsNullOrWhiteSpace(h.HeadlineText))
        {
            var sp = new StackPanel();
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.HeadlineText")));
            var text = h.HeadlineText.Length > 2000 ? h.HeadlineText[..2000] + "..." : h.HeadlineText;
            sp.Children.Add(VisHelper.Card(new TextBlock { Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333333"), FontWeight = FontWeight.Medium }));
            root.Children.Add(sp);
        }

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Headline h) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock { Text = $"News #{h.Id}", FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        if (!string.IsNullOrWhiteSpace(h.HeadlineText))
        {
            var preview = h.HeadlineText.Length > 150 ? h.HeadlineText[..150] + "..." : h.HeadlineText;
            root.Children.Add(new TextBlock { Text = $"\"{preview}\"", FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        }
        return root;
    }

    private static Control BuildHeroHeader(Headline h)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { new SymbolIcon { Symbol = Symbol.News, FontSize = 11, Foreground = Brush.Parse("#1565C0") }, new TextBlock { Text = $"ID: {h.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } } } });
        var len = string.IsNullOrWhiteSpace(h.HeadlineText) ? 0 : h.HeadlineText.Length;
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"{len} chars", FontSize = 10, Foreground = Brush.Parse("#E65100") } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = $"News #{h.Id}", FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// ForbiddenHex (NEW)
// ══════════════════════════════════════════════════════════════════════════════

public class ForbiddenHexEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ForbiddenHex);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ForbiddenHex fh) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(fh), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(fh));
        root.Children.Add(BuildStatsPanel(fh));
        root.Children.Add(BuildReverseRefsPanel(fh));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not ForbiddenHex fh) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };
        root.Children.Add(new TextBlock { Text = fh.Subject ?? fh.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        if (!string.IsNullOrWhiteSpace(fh.Name))
            root.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Center, Child = new TextBlock { Text = fh.Name, FontSize = 10, Foreground = Brush.Parse("#C62828") } });
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Location")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)> { (VisHelper.Loc("Vis.Position"), $"({fh.X}, {fh.Y})", null) }));
        return root;
    }

    private static Control BuildHeroHeader(ForbiddenHex fh)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {fh.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { new SymbolIcon { Symbol = Symbol.Shield, FontSize = 10, Foreground = Brush.Parse("#C62828") }, new TextBlock { Text = "Forbidden", FontSize = 10, Foreground = Brush.Parse("#C62828") } } } });
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = fh.Subject ?? fh.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        identity.Children.Add(new TextBlock { Text = $"{VisHelper.Loc("Vis.Position")}: ({fh.X}, {fh.Y})", FontSize = 12, Foreground = Brush.Parse("#888") });
        Grid.SetColumn(identity, 0); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(ForbiddenHex fh)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Location")));
        var stats = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({fh.X}, {fh.Y})", null),
        };
        if (!string.IsNullOrWhiteSpace(fh.Name))
            stats.Add(("Name", fh.Name, "#C62828"));
        sp.Children.Add(VisHelper.BuildStatCard(stats));
        return sp;
    }

    private static Control BuildReverseRefsPanel(ForbiddenHex fh)
        => VisHelper.BuildReverseRefsPanel(fh.EntityId);
}

// ══════════════════════════════════════════════════════════════════════════════
// Map (NEW)
// ══════════════════════════════════════════════════════════════════════════════

public class MapEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Map);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Map m) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border { IsVisible = false, Child = VisHelper.BuildRawDataTable(m), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(m));
        root.Children.Add(BuildMapImagePanel(m));
        if (!string.IsNullOrWhiteSpace(m.Definition))
            root.Children.Add(BuildDefinitionPanel(m));
        root.Children.Add(BuildReverseRefsPanel(m));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Map m) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var bmp = VisHelper.LoadImage(m.Name);
        if (bmp is not null)
        {
            root.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 } });
        }
        else
        {
            root.Children.Add(new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center, Child = new SymbolIcon { Symbol = Symbol.Map, FontSize = 32, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
        }

        root.Children.Add(new TextBlock { Text = m.Subject ?? m.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
        var defLen = string.IsNullOrWhiteSpace(m.Definition) ? 0 : m.Definition.Split(',').Length;
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.DataPoints"), $"{defLen} cells", null)
        }));

        return root;
    }

    private static Control BuildHeroHeader(Map m)
    {
        var grid = new Grid { ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };

        var bmp = VisHelper.LoadImage(m.Name);
        var imageArea = new Border { Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true, Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon { Symbol = Symbol.Map, FontSize = 40, Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(imageArea, 0); grid.Children.Add(imageArea);

        var identity = new StackPanel { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"ID: {m.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") } });
        if (!string.IsNullOrWhiteSpace(m.Definition))
        {
            var defLen = m.Definition.Split(',').Length;
            badgeRow.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2), Child = new TextBlock { Text = $"{defLen} cells", FontSize = 10, Foreground = Brush.Parse("#283593") } });
        }
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock { Text = m.Subject ?? m.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(m.Name))
            identity.Children.Add(new TextBlock { Text = m.Name, FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1); grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildMapImagePanel(Map m)
    {
        var sp = new StackPanel();
        var bmp = VisHelper.LoadImage(m.Name);
        if (bmp is not null)
        {
            sp.Children.Add(VisHelper.SectionLabel("Map Image"));
            const double maxW = 600;
            var scale = Math.Min(1.0, maxW / bmp.Size.Width);
            sp.Children.Add(VisHelper.Card(new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = bmp.Size.Width * scale, Height = bmp.Size.Height * scale }
            }));
        }

        if (!string.IsNullOrWhiteSpace(m.Definition))
        {
            var hexes = HexMapRenderer.ParseDefinition(m.Definition);
            var (gw, gh) = HexMapRenderer.GuessDimensions(hexes.Count);
            if (gw * gh < hexes.Count) gh = (int)Math.Ceiling((double)hexes.Count / gw);
            sp.Children.Add(VisHelper.SectionLabel($"Hex Data ({gw}×{gh}, {hexes.Count} cells)"));
        }
        return sp;
    }

    private static Control BuildDefinitionPanel(Map m)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.MapDefinition")));
        var def = m.Definition.Length > 3000 ? m.Definition[..3000] + "..." : m.Definition;
        sp.Children.Add(VisHelper.Card(new TextBlock { Text = def, FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#555555"), FontFamily = "Consolas, monospace" }));
        return sp;
    }

    private static Control BuildReverseRefsPanel(Map m)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return new StackPanel();
        var rawRefs = store.Index.ReverseLookup(m.EntityId);
        if (rawRefs.Count == 0) return new StackPanel();

        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var match = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (match != null) { resolved.Add((t, match.Subject, srcEid, propName)); break; }
            }
        }
        if (resolved.Count == 0) return new StackPanel();

        var sp = new StackPanel();
        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var list = new StackPanel { Spacing = 3 };
        foreach (var (srcType, srcSubject, srcEid, _) in resolved.Take(15))
        {
            var tc = srcType == typeof(Creature) ? ("#E8EAF6", "#283593") : ("#F5F5F5", "#666");
            var row = new Border { CornerRadius = new CornerRadius(4), Background = Brush.Parse("#0D000000"), Padding = new Thickness(8, 3), Cursor = new Cursor(StandardCursorType.Hand), Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { CornerRadius = new CornerRadius(3), Background = Brush.Parse(tc.Item1), Padding = new Thickness(5, 1), Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(tc.Item2) } }, new TextBlock { Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center } } } };
            var ct = srcType; var ci = srcEid;
            row.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct, ci); };
            list.Children.Add(row);
        }
        if (resolved.Count > 15) list.Children.Add(new TextBlock { Text = $"+ {resolved.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(4, 2) });
        sp.Children.Add(VisHelper.Card(list));
        return sp;
    }
}
