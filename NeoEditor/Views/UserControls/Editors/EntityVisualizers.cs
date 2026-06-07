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
            var idStr = ReferenceHelper.ExtractRawId(s, pattern);
            var match = GenericDataGridHelper.FindBestMatch(typeof(T), idStr, targetKey);
            var display = match?.Subject ?? idStr;
            var extra = ReferencePattern.FromName(pattern).FormatExtraInfo(s);
            if (!string.IsNullOrEmpty(extra)) display += $" ({extra})";
            var leaf = match is not null
                ? NavLeaf(display, () => ReferenceResolver.NavigateTo(typeof(T), match.EntityId), fg)
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
        textCol.Children.Add(new TextBlock { Text = entity.Subject ?? $"[{entity.GetType().Name}]", FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
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

        root.Children.Add(BuildHeroHeader(it));

        var hasStats = it.Weight > 0 || it.StackLimit > 0 || it.Durability > 0 || it.MonetaryValue > 0;
        if (hasStats)
            root.Children.Add(BuildStatBars(it));

        if (!string.IsNullOrWhiteSpace(it.Properties))
            root.Children.Add(BuildPropertyTags(it));

        if (!string.IsNullOrWhiteSpace(it.EquipSlots) ||
            !string.IsNullOrWhiteSpace(it.EquipConditions) ||
            !string.IsNullOrWhiteSpace(it.UseConditions) ||
            !string.IsNullOrWhiteSpace(it.PossessConditions))
            root.Children.Add(BuildEquipmentCard(it));

        if (!string.IsNullOrWhiteSpace(it.Capacities) ||
            (!string.IsNullOrWhiteSpace(it.FormatId) && it.FormatId != "3") ||
            !string.IsNullOrWhiteSpace(it.ContentIds))
            root.Children.Add(BuildContainerCard(it));

        if (it.DegradePerHour > 0 || it.DegradePerUse > 0 ||
            (!string.IsNullOrWhiteSpace(it.DegradeTreasureIds) && it.DegradeTreasureIds != "3,3"))
            root.Children.Add(BuildDegradeCard(it));

        if (!string.IsNullOrWhiteSpace(it.ChargeProfiles))
            root.Children.Add(BuildChargeCard(it));

        root.Children.Add(BuildRefBars(it));
        root.Children.Add(BuildReverseRefBars(it));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Overview ═══════════════

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not ItemType it) return new TextBlock { Text = "..." };
        var bmp = !string.IsNullOrWhiteSpace(it.ImageList)
            ? VisHelper.LoadImage(it.ImageList.Split(',')[0].Trim())
            : null;

        var card = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#12000000"),
            Padding = new Thickness(10),
            Margin = new Thickness(8),
            Child = new StackPanel { Spacing = 4 }
        };
        var sp = (StackPanel)card.Child;

        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (bmp is not null)
            hdr.Children.Add(new Border
            {
                Width = 52, Height = 52, CornerRadius = new CornerRadius(4), ClipToBounds = true,
                Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill, Width = 52, Height = 52 }
            });
        var tc = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        tc.Children.Add(new TextBlock { Text = it.Subject, FontSize = 13, FontWeight = FontWeight.Bold });
        tc.Children.Add(new TextBlock { Text = $"{it.GroupId}.{it.SubgroupId}   {it.Weight:F1}kg   ×{it.StackLimit}   Dur {it.Durability:F1}", FontSize = 10, Foreground = Brushes.Gray });
        hdr.Children.Add(tc);
        sp.Children.Add(hdr);

        if (!string.IsNullOrWhiteSpace(it.Properties))
        {
            var propDict = ReferenceResolver.GetDedupedInt<ItemProp>();
            var tags = new WrapPanel();
            foreach (var seg in it.Properties.Split('&'))
            {
                var s = seg.Trim();
                if (int.TryParse(s, out var pid) && propDict.TryGetValue(pid, out var p))
                    tags.Children.Add(MiniBadge(p.PropertyName, "#E8F5E9", "#2E7D32"));
            }
            if (tags.Children.Count > 0) sp.Children.Add(tags);
        }

        return card;
    }

    // ═══════════════ Layout primitives ═══════════════

    private static Border Card(Control content) => new()
    {
        CornerRadius = new CornerRadius(8),
        Background = Brush.Parse("#08000000"),
        BorderBrush = Brush.Parse("#18000000"),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(14),
        Child = content
    };

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold,
        Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 8)
    };

    // Small tag badge
    private static Border MiniBadge(string text, string bg, string fg, Action? onClick = null)
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
                    new TextBlock { Text = "✦ Identified", FontSize = 9, Foreground = Brush.Parse("#E65100") },
                    new TextBlock { Text = it.DescriptionAlt, FontSize = 11, Foreground = Brush.Parse("#BF360C"), TextWrapping = TextWrapping.Wrap }
                }}
            });
        Grid.SetColumn(identity, 1); Grid.SetRow(identity, 0);
        Grid.SetRowSpan(identity, 2);
        grid.Children.Add(identity);

        return Card(grid);
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

    // ═══════════════ Horizontal stat bars ═══════════════

    private static Control BuildStatBars(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(SectionLabel("Stats"));

        var bars = new StackPanel { Spacing = 5 };
        if (it.Weight > 0)
            bars.Children.Add(StatBar("Weight", $"{it.Weight:F1} kg", it.Weight / 50.0, "#4CAF50"));
        if (it.StackLimit > 0)
            bars.Children.Add(StatBar("Stack Limit", $"×{it.StackLimit}", it.StackLimit / 100.0, "#2196F3"));
        if (it.Durability > 0)
            bars.Children.Add(StatBar("Durability", it.Durability >= 999 ? "Infinite" : $"{it.Durability * 100:F0}%", it.Durability >= 999 ? 1.0 : it.Durability, "#FF9800"));
        if (it.MonetaryValue > 0)
        {
            var valText = it.MonetaryValueAlt != it.MonetaryValue
                ? $"${it.MonetaryValue:F2} → ${it.MonetaryValueAlt:F2} (real)"
                : $"${it.MonetaryValue:F2}";
            bars.Children.Add(StatBar("Value", valText, it.MonetaryValue / 500.0, "#9C27B0"));
        }
        if (it.Mirrored)
            bars.Children.Add(StatBar("", "Mirrored — dual-wield", 0.3, "#607D8B"));

        sp.Children.Add(Card(bars));
        return sp;
    }

    private static Control StatBar(string label, string value, double fillRatio, string colorHex)
    {
        fillRatio = Math.Clamp(fillRatio, 0.05, 1.0);
        var starFill = (int)(fillRatio * 100);
        var starEmpty = 100 - starFill;

        var grid = new Grid { Height = 30 };
        grid.ColumnDefinitions.Add(new(95, GridUnitType.Pixel));  // label
        grid.ColumnDefinitions.Add(new(starFill, GridUnitType.Star));  // filled bar
        grid.ColumnDefinitions.Add(new(starEmpty, GridUnitType.Star)); // empty space

        var labelTb = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999999"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        var fill = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = Brush.Parse(colorHex),
            Margin = new Thickness(0, 1),
            Child = new TextBlock
            {
                Text = value, FontSize = 11, Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0)
            }
        };
        Grid.SetColumn(fill, 1);
        grid.Children.Add(fill);
        // empty space column (starEmpty) stays blank

        return grid;
    }

    // ═══════════════ Property tags ═══════════════

    private static Control BuildPropertyTags(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(SectionLabel("Properties"));
        var propDict = ReferenceResolver.GetDedupedInt<ItemProp>();
        var wp = new WrapPanel();
        foreach (var seg in it.Properties.Split('&'))
        {
            var s = seg.Trim(); if (string.IsNullOrWhiteSpace(s)) continue;
            if (int.TryParse(s, out var pid) && propDict.TryGetValue(pid, out var p))
                wp.Children.Add(MiniBadge(p.PropertyName, "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.NavigateToByKey<ItemProp>(pid)));
            else
                wp.Children.Add(MiniBadge(s, "#F5F5F5", "#9E9E9E"));
        }
        sp.Children.Add(Card(wp));
        return sp;
    }

    // ═══════════════ Equipment card ═══════════════

    private static Control BuildEquipmentCard(ItemType it)
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(SectionLabel("Equipment"));

        // Equip slots
        if (!string.IsNullOrWhiteSpace(it.EquipSlots))
        {
            var wp = new WrapPanel();
            foreach (var s in it.EquipSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                var name = int.TryParse(s, out var sn) && SlotNames.TryGetValue(sn, out var snv) ? snv : s;
                wp.Children.Add(MiniBadge(name, "#E3F2FD", "#1565C0"));
            }
            sp.Children.Add(new StackPanel { Spacing = 3, Children = {
                new TextBlock { Text = "Slots", FontSize = 10, Foreground = Brushes.Gray },
                wp
            }});
        }

        // Condition references
        if (!string.IsNullOrWhiteSpace(it.EquipConditions))
            sp.Children.Add(ConditionTagRow("When equipped", it.EquipConditions));
        if (!string.IsNullOrWhiteSpace(it.UseConditions))
            sp.Children.Add(ConditionTagRow("When used", it.UseConditions));
        if (!string.IsNullOrWhiteSpace(it.PossessConditions))
            sp.Children.Add(ConditionTagRow("When carried", it.PossessConditions));

        return Card(sp);
    }

    private static Control ConditionTagRow(string label, string raw)
    {
        var condDict = ReferenceResolver.GetDedupedInt<Condition>();
        var wp = new WrapPanel();
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var idStr = ReferenceHelper.ExtractRawId(seg, "{id}x{mult}");
            if (int.TryParse(idStr, out var cid) && condDict.TryGetValue(cid, out var c))
            {
                var extra = ReferencePattern.FromName("{id}x{mult}").FormatExtraInfo(seg);
                var text = string.IsNullOrEmpty(extra) ? c.Subject : $"{c.Subject} ×{extra}";
                wp.Children.Add(MiniBadge(text, "#FCE4EC", "#C62828",
                    () => ReferenceResolver.NavigateToByKey<Condition>(cid)));
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
        sp.Children.Add(SectionLabel("As Container"));

        if (!string.IsNullOrWhiteSpace(it.Capacities))
            sp.Children.Add(new TextBlock { Text = $"Capacity: {it.Capacities}", FontSize = 11 });

        if (!string.IsNullOrWhiteSpace(it.FormatId) && it.FormatId != "3")
            sp.Children.Add(ResolvedRef("Format", it.FormatId, typeof(ContainerType)));
        if (!string.IsNullOrWhiteSpace(it.ContentIds))
        {
            var containerDict = ReferenceResolver.GetDedupedInt<ContainerType>();
            var wp = new WrapPanel();
            foreach (var seg in it.ContentIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                if (int.TryParse(seg, out var cid) && containerDict.TryGetValue(cid, out var ct))
                    wp.Children.Add(MiniBadge(ct.Name, "#E8EAF6", "#283593",
                        () => ReferenceResolver.NavigateToByKey<ContainerType>(cid)));
            }
            if (wp.Children.Count > 0)
                sp.Children.Add(new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "Accepts content", FontSize = 10, Foreground = Brushes.Gray }, wp } });
        }

        return Card(sp);
    }

    // ═══════════════ Degrade card ═══════════════

    private static Control BuildDegradeCard(ItemType it)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(SectionLabel("Degradation"));
        if (it.DegradePerHour > 0)
            sp.Children.Add(new TextBlock { Text = $"Wear per hour: {it.DegradePerHour:F2}", FontSize = 11 });
        if (it.DegradePerUse > 0)
            sp.Children.Add(new TextBlock { Text = $"Wear per use: {it.DegradePerUse:F2}", FontSize = 11 });
        if (!string.IsNullOrWhiteSpace(it.DegradeTreasureIds) && it.DegradeTreasureIds != "3,3")
        {
            var ttDict = ReferenceResolver.GetDedupedInt<TreasureTable>();
            var wp = new WrapPanel();
            foreach (var seg in it.DegradeTreasureIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0 && s != "3"))
            {
                if (int.TryParse(seg, out var ttid) && ttDict.TryGetValue(ttid, out var tt))
                    wp.Children.Add(MiniBadge(tt.Name, "#FFF8E1", "#F57F17",
                        () => ReferenceResolver.NavigateToByKey<TreasureTable>(ttid)));
            }
            if (wp.Children.Count > 0)
                sp.Children.Add(new StackPanel { Spacing = 3, Children = { new TextBlock { Text = "Break parts", FontSize = 10, Foreground = Brushes.Gray }, wp } });
        }
        return Card(sp);
    }

    // ═══════════════ Charge card ═══════════════

    private static Control BuildChargeCard(ItemType it)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(SectionLabel("Charge / Ammo"));
        var cpDict = ReferenceResolver.GetDedupedInt<ChargeProfile>();
        var wp = new WrapPanel();
        foreach (var seg in it.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            if (int.TryParse(seg, out var cpid) && cpDict.TryGetValue(cpid, out var cp))
                wp.Children.Add(MiniBadge(cp.Name, "#E0F7FA", "#006064",
                    () => ReferenceResolver.NavigateToByKey<ChargeProfile>(cpid)));
        }
        sp.Children.Add(wp);
        return Card(sp);
    }

    // ═══════════════ Reference bars (resolved subjects) ═══════════════

    private static Control BuildRefBars(ItemType it)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(SectionLabel("References"));
        var added = false;

        if (!string.IsNullOrWhiteSpace(it.TreasureId) && it.TreasureId != "3")
        { sp.Children.Add(ResolvedRef("Treasure Table", it.TreasureId, typeof(TreasureTable))); added = true; }
        if (!string.IsNullOrWhiteSpace(it.CondId) && it.CondId != "1")
        { sp.Children.Add(ResolvedRef("Required Condition", it.CondId, typeof(Condition))); added = true; }
        if (!string.IsNullOrWhiteSpace(it.ComponentId) && it.ComponentId != "0")
        { sp.Children.Add(ResolvedRef("Component (craft)", it.ComponentId, typeof(ItemType), "{GroupId}.{SubgroupId}")); added = true; }

        if (!added)
            sp.Children.Add(new TextBlock { Text = "—", FontSize = 11, Foreground = Brushes.Gray, FontStyle = FontStyle.Italic });

        return Card(sp);
    }

    /// <summary>A horizontal bar showing the resolved subject of a reference, Ctrl+Click to navigate.</summary>
    private static Control ResolvedRef(string label, string raw, Type targetType, string? targetKey = null)
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
            { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.NavigateTo(tt, m.EntityId); };
        }
        Grid.SetColumn(linkBar, 1);
        grid.Children.Add(linkBar);

        return grid;
    }

    // ═══════════════ Reverse references ═══════════════

    private static Control BuildReverseRefBars(ItemType it)
    {
        var refs = ReferenceResolver.FindReverseReferences(typeof(ItemType), it.EntityId);
        if (refs.Count == 0) return new TextBlock();

        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(SectionLabel($"Referenced By ({refs.Count})"));
        var inner = new StackPanel { Spacing = 4 };
        foreach (var (srcType, _, srcEntity) in refs.Take(15))
        {
            var bar = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#0D000000"),
                Padding = new Thickness(8, 4),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    new Border {
                        CornerRadius = new CornerRadius(3), Background = Brush.Parse("#F3E5F5"),
                        Padding = new Thickness(6,1),
                        Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse("#6A1B9A") }
                    },
                    new TextBlock { Text = srcEntity.Subject, FontSize = 11, Foreground = Brush.Parse("#333333"), VerticalAlignment = VerticalAlignment.Center }
                }}
            };
            var capturedType = srcType;
            var capturedId = srcEntity.EntityId;
            bar.PointerPressed += (_, e) =>
            { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.NavigateTo(capturedType, capturedId); };
            inner.Children.Add(bar);
        }
        if (refs.Count > 15)
            inner.Children.Add(new TextBlock { Text = $"+ {refs.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray });
        sp.Children.Add(Card(inner));
        return sp;
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
        var tree = new TreeView();
        var title = string.IsNullOrWhiteSpace(r.Name) ? $"Recipe #{r.Id}" : $"{r.Name} (#{r.Id})";
        var root = VisHelper.Section(title, Brushes.DodgerBlue);

        var ingredients = ReferenceResolver.GetDedupedInt<Ingredient>();
        var itemProps = ReferenceResolver.GetDedupedInt<ItemProp>();
        var allTables = ReferenceResolver.GetDedupedInt<TreasureTable>();
        var itemTypes = ReferenceResolver.GetDedupedComposite<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}");

        // Tools
        if (!string.IsNullOrWhiteSpace(r.Tools))
            root.Items.Add(BuildIngredientGroup("Tools", r.Tools, "#FF8C00", ingredients, itemProps));
        // Consumed
        if (!string.IsNullOrWhiteSpace(r.Consumed))
            root.Items.Add(BuildIngredientGroup("Consumed", r.Consumed, "#DC143C", ingredients, itemProps));
        // Destroyed
        if (!string.IsNullOrWhiteSpace(r.Destroyed))
            root.Items.Add(BuildIngredientGroup("Destroyed", r.Destroyed, "#8B0000", ingredients, itemProps));
        // Product
        root.Items.Add(BuildProductNode(r.TreasureId, allTables, itemTypes));
        // AlsoTry
        if (!string.IsNullOrWhiteSpace(r.AlsoTry))
            root.Items.Add(BuildAlsoTry(r.AlsoTry));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Recipe r) return new TextBlock { Text = "..." };
        var sp = VisHelper.OverviewHeader(r);
        if (!string.IsNullOrWhiteSpace(r.Tools))
            sp.Children.Add(VisHelper.Kv("Tools", r.Tools, 65));
        if (!string.IsNullOrWhiteSpace(r.Consumed))
            sp.Children.Add(VisHelper.Kv("Consumed", r.Consumed, 65));
        if (!string.IsNullOrWhiteSpace(r.Destroyed))
            sp.Children.Add(VisHelper.Kv("Destroyed", r.Destroyed, 65));
        if (!string.IsNullOrWhiteSpace(r.TreasureId))
            sp.Children.Add(VisHelper.Kv("Product TT", r.TreasureId, 65));
        return VisHelper.Wrap(sp);
    }

    private static TreeViewItem BuildIngredientGroup(string label, string raw, string colorHex,
        Dictionary<int, Ingredient> ingredients, Dictionary<int, ItemProp> itemProps)
    {
        var g = VisHelper.Section(label, Brush.Parse(colorHex));
        foreach (var part in raw.Split('+'))
        {
            var parts = part.Trim().Split('x');
            var qty = parts.Length >= 2 ? parts[0] : "1";
            var idStr = parts.Length >= 2 ? parts[1] : parts[0];
            var id = int.TryParse(idStr, out var i) ? i : 0;
            var name = ingredients.TryGetValue(id, out var ing) ? ing.Name : $"#{idStr}";
            var node = VisHelper.Section($"{name} x{qty}", id > 0 ? Brushes.DarkOrange : Brushes.Gray);
            if (ing is not null)
            {
                node.Cursor = new Cursor(StandardCursorType.Hand);
                var capturedId = id;
                node.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.NavigateToByKey<Ingredient>(capturedId); };
                if (!string.IsNullOrWhiteSpace(ing.RequiredProps))
                    AddSingleProps(node, ing.RequiredProps, "Required", Brushes.DarkOrange, itemProps);
                if (!string.IsNullOrWhiteSpace(ing.ForbidProps))
                    AddSingleProps(node, ing.ForbidProps, "Forbidden", Brushes.IndianRed, itemProps);
            }
            g.Items.Add(node);
        }
        return g;
    }

    private static void AddSingleProps(TreeViewItem parent, string raw, string label, IBrush fg,
        Dictionary<int, ItemProp> itemProps)
    {
        var n = VisHelper.Section($"{label} Properties", fg);
        foreach (var s in raw.Split('&'))
        {
            if (int.TryParse(s.Trim(), out var pid) && itemProps.TryGetValue(pid, out var p))
            {
                var leaf = VisHelper.NavLeaf(p.PropertyName, () => ReferenceResolver.NavigateToByKey<ItemProp>(pid), fg);
                n.Items.Add(leaf);
            }
        }
        if (n.Items.Count > 0) parent.Items.Add(n);
    }

    private static TreeViewItem BuildProductNode(string ttId, Dictionary<int, TreasureTable> allTables,
        Dictionary<string, ItemType> itemTypes)
    {
        var n = VisHelper.Section("Product", Brushes.DarkGreen);
        if (!int.TryParse(ttId, out var id) || !allTables.TryGetValue(id, out var t))
        { n.Items.Add(VisHelper.Leaf($"TT #{ttId}")); return n; }
        if (string.IsNullOrWhiteSpace(t.Treasures))
        { n.Items.Add(VisHelper.Leaf(t.Name)); return n; }
        foreach (var seg in t.Treasures.Split(',').Take(12))
        {
            var parts = seg.Trim().Split('x');
            if (parts.Length < 2) continue;
            var itemId = parts[0]; var qty = parts.Length > 2 ? parts[2] : "1";
            var prob = parts.Length > 1 ? parts[1] : "1";
            var it = itemTypes.GetValueOrDefault(itemId);
            var name = it?.Name ?? itemId;
            var leaf = VisHelper.NavLeaf($"{name} ({itemId})  prob:{prob} qty:{qty}",
                () => { if (it is not null) ReferenceResolver.NavigateTo(typeof(ItemType), it.EntityId); },
                Brushes.DarkGreen);
            n.Items.Add(leaf);
        }
        if (t.Treasures.Split(',').Length > 12) n.Items.Add(VisHelper.Leaf("... more items"));
        return n;
    }

    private static TreeViewItem BuildAlsoTry(string raw)
    {
        var n = VisHelper.Section("Also Try", Brushes.Purple);
        var lookup = ReferenceResolver.GetDedupedInt<Recipe>();
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            if (int.TryParse(seg, out var id))
            {
                var name = lookup.TryGetValue(id, out var r) ? r.Name : $"Recipe #{id}";
                n.Items.Add(VisHelper.NavLeaf(name, () => ReferenceResolver.NavigateToByKey<Recipe>(id), Brushes.Purple));
            }
        }
        return n;
    }

    private static TreeViewItem BuildPropertyGroup(string label, string raw, Dictionary<int, ItemProp> itemProps)
    {
        var n = VisHelper.Section(label, Brushes.DarkKhaki);
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            if (int.TryParse(seg, out var pid) && itemProps.TryGetValue(pid, out var p))
                n.Items.Add(VisHelper.NavLeaf(p.PropertyName, () => ReferenceResolver.NavigateToByKey<ItemProp>(pid)));
            else
                n.Items.Add(VisHelper.Leaf(seg));
        }
        return n;
    }
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
        var tree = new TreeView();
        var title = string.IsNullOrWhiteSpace(tt.Name) ? $"TreasureTable #{tt.Id}" : $"{tt.Name} (#{tt.Id})";
        var root = VisHelper.Section(title, Brushes.DodgerBlue);

        if (!string.IsNullOrWhiteSpace(tt.Treasures))
        {
            var allTables = ReferenceResolver.GetDedupedInt<TreasureTable>();
            var itemTypes = ReferenceResolver.GetDedupedComposite<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}");
            var visited = new HashSet<int>();
            if (int.TryParse(tt.Id.ToString(), out var selfId)) visited.Add(selfId);
            root.Items.Add(BuildLootTree(tt.Treasures, allTables, itemTypes, visited, 0));
        }
        else
        {
            root.Items.Add(VisHelper.Leaf("(Empty - no loot)", Brushes.Gray));
        }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not TreasureTable tt) return new TextBlock { Text = "..." };
        var sp = VisHelper.OverviewHeader(tt);
        if (!string.IsNullOrWhiteSpace(tt.Treasures))
        {
            var parts = tt.Treasures.Split('|');
            var totalItems = parts.Sum(orGroup => orGroup.Split(',').Length);
            var firstItem = tt.Treasures.Split(',', '|').FirstOrDefault()?.Trim() ?? "";
            var display = firstItem.Length > 60 ? firstItem[..60] + "..." : firstItem;
            sp.Children.Add(VisHelper.Kv("Items", $"{totalItems} items across {parts.Length} groups"));
            sp.Children.Add(VisHelper.Kv("Sample", display, 50));
        }
        else
        {
            sp.Children.Add(VisHelper.Kv("Items", "(empty)"));
        }
        return VisHelper.Wrap(sp);
    }

    private static TreeViewItem BuildLootTree(string treasures, Dictionary<int, TreasureTable> allTables,
        Dictionary<string, ItemType> itemTypes, HashSet<int> visited, int depth)
    {
        var root = VisHelper.Section("Loot Table", Brushes.DodgerBlue);
        if (depth >= 5) { root.Items.Add(VisHelper.Leaf("(max depth)", Brushes.Gray)); return root; }

        foreach (var orSeg in treasures.Split('|'))
        {
            var orItems = orSeg.Split(',').Select(s => s.Trim())
                .Where(s => s.Length > 0 && s.Contains('x')).ToList();
            if (orItems.Count == 0) continue;

            TreeViewItem orNode;
            if (orItems.Count == 1)
            {
                orNode = root; // single item group, no OR wrapper
            }
            else
            {
                orNode = VisHelper.Section("OR Group", Brushes.CornflowerBlue);
            }

            foreach (var seg in orItems)
            {
                var parts = seg.Split('x');
                if (parts.Length < 2) continue;
                var itemId = parts[0].Trim();
                var probStr = parts.Length > 1 ? parts[1].Trim() : "1";
                var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
                var prob = double.TryParse(probStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 1.0;

                string itemName;
                IEntity? navTarget = null;
                Type? navType = null;
                TreasureTable? nestedTable = null;

                if (itemTypes.TryGetValue(itemId, out var matched))
                {
                    itemName = matched.Name;
                    navTarget = matched;
                    navType = typeof(ItemType);
                    if (!string.IsNullOrWhiteSpace(matched.TreasureId) &&
                        int.TryParse(matched.TreasureId, out var nid) &&
                        allTables.TryGetValue(nid, out var nt) &&
                        visited.Add(nid))
                        nestedTable = nt;
                }
                else if (int.TryParse(itemId, out var tid) &&
                         allTables.TryGetValue(tid, out var tt) &&
                         visited.Add(tid))
                {
                    nestedTable = tt;
                    itemName = $"[TT] {tt.Name}";
                }
                else
                {
                    itemName = $"#{itemId}";
                }

                // Probability bar text
                var probBar = prob >= 1.0 ? "100%" : $"{prob:P0}";
                var headerText = $"{itemName} ({itemId})  [{probBar}, qty {qtyRange}]";
                var fg = prob >= 0.5 ? Brushes.DarkGreen : prob >= 0.1 ? Brushes.DarkOrange : Brushes.Gray;

                var item = VisHelper.Section(headerText, fg);
                if (navTarget is not null && navType is not null)
                {
                    var captured = navTarget;
                    var capturedType = navType;
                    item.Cursor = new Cursor(StandardCursorType.Hand);
                    item.PointerPressed += (_, e) =>
                    { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.NavigateTo(capturedType, captured.EntityId); };
                }
                if (nestedTable is not null)
                    item.Items.Add(BuildLootTree(nestedTable.Treasures ?? "", allTables, itemTypes, visited, depth + 1));

                if (orItems.Count == 1)
                    root.Items.Add(item);
                else
                    orNode.Items.Add(item);
            }

            if (orItems.Count > 1) root.Items.Add(orNode);
        }

        return root;
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
        var tree = new TreeView();
        var root = VisHelper.Section(enc.Subject, Brushes.DodgerBlue);

        // Description
        if (!string.IsNullOrWhiteSpace(enc.Description))
        {
            var descSec = VisHelper.Section("Story Text", Brushes.SlateBlue);
            var desc = enc.Description;
            if (desc.Length > 2000) desc = desc[..2000] + "...";
            descSec.Items.Add(VisHelper.Leaf(desc, Brushes.Black));
            root.Items.Add(descSec);
        }

        // Responses
        if (!string.IsNullOrWhiteSpace(enc.Responses))
        {
            var respSec = VisHelper.Section("Responses", Brushes.DarkGreen);
            // Parse response format: each response is typically separated by newlines or specific format
            var text = enc.Responses;
            if (text.Length > 1500) text = text[..1500] + "...";
            respSec.Items.Add(VisHelper.Leaf(text, Brushes.Black));
            root.Items.Add(respSec);
        }

        // Linked data
        if (!string.IsNullOrWhiteSpace(enc.TreasureId) && enc.TreasureId != "3")
            root.Items.Add(VisHelper.RefNode<TreasureTable>(enc.TreasureId, null, null, null, "Treasure Table", Brushes.Teal));

        // Triggers (reverse reference)
        var triggers = FindTriggers(enc.Id);
        if (triggers.Count > 0)
        {
            var trigSec = VisHelper.Section("Triggered By", Brushes.DarkMagenta);
            foreach (var trigger in triggers)
            {
                var label = $"{trigger.Name} (id={trigger.Id})  Chance: {trigger.Chance:P0}";
                trigSec.Items.Add(VisHelper.NavLeaf(label,
                    () => ReferenceResolver.NavigateTo(typeof(EncounterTrigger), trigger.EntityId),
                    Brushes.Magenta));
            }
            root.Items.Add(trigSec);
        }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Encounter enc) return new TextBlock { Text = "..." };
        var sp = VisHelper.OverviewHeader(enc);
        var desc = enc.Description ?? "";
        if (desc.Length > 150) desc = desc[..150] + "...";
        sp.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray,
            Margin = new Thickness(8, 2, 8, 4)
        });
        if (!string.IsNullOrWhiteSpace(enc.Responses))
        {
            var respCount = enc.Responses.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            sp.Children.Add(VisHelper.Kv("Responses", $"{respCount} option(s)", 65));
        }
        return VisHelper.Wrap(sp);
    }

    private static List<EncounterTrigger> FindTriggers(int encounterId)
    {
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(EncounterTrigger), out var list) || list is null)
            return [];
        return list.OfType<EncounterTrigger>()
            .Where(t => t.EncounterId == encounterId)
            .ToList();
    }
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
        var tree = new TreeView();
        var root = VisHelper.Section(c.Subject, Brushes.DodgerBlue);

        // Image
        if (!string.IsNullOrWhiteSpace(c.Image))
        {
            var bmp = VisHelper.LoadImage(c.Image);
            if (bmp is not null)
            {
                var imgSp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
                imgSp.Children.Add(new TextBlock { Text = $"Map Icon: {c.Image}", FontSize = 9 });
                imgSp.Children.Add(new Image { Source = bmp, MaxWidth = 128, MaxHeight = 128 });
                root.Items.Add(new TreeViewItem { IsExpanded = true, Header = imgSp });
            }
        }

        // Identity
        var idSec = VisHelper.Section("Identity", Brushes.SteelBlue);
        if (!string.IsNullOrWhiteSpace(c.NamePublic))
            idSec.Items.Add(VisHelper.Leaf($"Public Name: {c.NamePublic}"));
        if (!string.IsNullOrWhiteSpace(c.Notes))
            idSec.Items.Add(VisHelper.Leaf($"Notes: {c.Notes}"));
        idSec.Items.Add(VisHelper.Leaf($"Moves/Turn: {c.MovesPerTurn}"));
        root.Items.Add(idSec);

        // Faction
        if (!string.IsNullOrWhiteSpace(c.Faction) && c.Faction != "0")
            root.Items.Add(VisHelper.RefNode<Faction>(c.Faction, null, null, null, "Faction", Brushes.Orange));

        // Attack Modes
        if (!string.IsNullOrWhiteSpace(c.AttackModes))
            root.Items.Add(VisHelper.RefNode<AttackMode>(c.AttackModes, ",", null, null, "Attack Modes", Brushes.Crimson));

        // Base Conditions
        if (!string.IsNullOrWhiteSpace(c.BaseConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(c.BaseConditions, ",", "{id}={value}", null, "Base Conditions", Brushes.DarkMagenta));

        // Encounter IDs (condition triggers on encounter)
        if (!string.IsNullOrWhiteSpace(c.EncounterIds))
            root.Items.Add(VisHelper.RefNode<Condition>(c.EncounterIds, ",", null, null, "On-Encounter Conditions", Brushes.DarkMagenta));

        // Loot
        if (!string.IsNullOrWhiteSpace(c.TreasureId) && c.TreasureId != "3")
            root.Items.Add(VisHelper.RefNode<TreasureTable>(c.TreasureId, null, null, null, "Loot Table", Brushes.Teal));

        // Corpse
        if (!string.IsNullOrWhiteSpace(c.CorpseId) && c.CorpseId != "3")
            root.Items.Add(VisHelper.RefNode<TreasureTable>(c.CorpseId, null, null, null, "Corpse Loot", Brushes.Teal));

        // Activities
        if (!string.IsNullOrWhiteSpace(c.Activities))
        {
            var act = c.Activities;
            if (act.Length > 200) act = act[..200] + "...";
            { var n = VisHelper.Section("Activities", Brushes.Gray); n.Items.Add(VisHelper.Leaf(act)); root.Items.Add(n); }
        }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Creature c) return new TextBlock { Text = "..." };
        var bmp = VisHelper.LoadImage(c.Image);
        var sub = $"Faction {c.Faction}  ·  {c.MovesPerTurn} moves/turn";
        var sp = VisHelper.OverviewHeader(c, bmp, sub);
        if (!string.IsNullOrWhiteSpace(c.AttackModes))
        {
            var atkCount = c.AttackModes.Split(',').Length;
            sp.Children.Add(VisHelper.Kv("Attacks", $"{atkCount} mode(s)", 65));
        }
        if (!string.IsNullOrWhiteSpace(c.BaseConditions))
            sp.Children.Add(VisHelper.Kv("Conditions", c.BaseConditions.Length > 50 ? c.BaseConditions[..50] + "..." : c.BaseConditions, 65));
        return VisHelper.Wrap(sp);
    }
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
        var tree = new TreeView();
        var root = VisHelper.Section(cond.Subject, Brushes.DodgerBlue);

        // Status flags
        var flagSec = VisHelper.Section("Status", Brushes.SteelBlue);
        var flags = new List<string>();
        if (cond.Fatal) flags.Add("FATAL");
        if (cond.Permanent) flags.Add("Permanent");
        if (cond.Stackable) flags.Add("Stackable");
        if (cond.ResetTimer) flags.Add("ResetTimer");
        if (!cond.Display) flags.Add("Hidden");
        if (cond.DisplayOther) flags.Add("Visible to Others");
        if (cond.DisplayGameOver) flags.Add("GameOver Log");
        flagSec.Items.Add(VisHelper.Leaf(string.Join(" · ", flags)));
        flagSec.Items.Add(VisHelper.Leaf($"Color: {cond.Color}"));
        flagSec.Items.Add(VisHelper.Leaf($"Duration: {cond.Duration}h"));
        flagSec.Items.Add(VisHelper.Leaf($"Transfer Range: {cond.TransferRange}"));
        if (cond.RemovePostCombat) flagSec.Items.Add(VisHelper.Leaf("Removed after combat"));
        if (cond.RemoveAll) flagSec.Items.Add(VisHelper.Leaf("RemoveAll"));
        root.Items.Add(flagSec);

        // Description
        if (!string.IsNullOrWhiteSpace(cond.Description))
        {
            var desc = cond.Description;
            if (desc.Length > 500) desc = desc[..500] + "...";
            { var n = VisHelper.Section("Description", Brushes.SlateBlue); n.Items.Add(VisHelper.Leaf(desc)); root.Items.Add(n); }
        }

        // FieldNames ↔ Modifiers paired table
        var names = (cond.FieldNames ?? "").Split(',').Select(s => s.Trim()).ToList();
        var mods = (cond.Modifiers ?? "").Split(',').Select(s => s.Trim()).ToList();
        if (names.Count > 0 && !names.All(string.IsNullOrEmpty))
        {
            var pairSec = VisHelper.Section("FieldNames → Modifiers", Brushes.Teal);
            for (int i = 0; i < Math.Max(names.Count, mods.Count); i++)
            {
                var name = i < names.Count ? names[i] : "?";
                var mod = i < mods.Count ? mods[i] : "?";
                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(mod)) continue;
                pairSec.Items.Add(VisHelper.Leaf($"{name}  →  {mod}"));
            }
            root.Items.Add(pairSec);
        }

        // Effects
        if (!string.IsNullOrWhiteSpace(cond.Effects))
        {
            var eff = cond.Effects;
            if (eff.Length > 500) eff = eff[..500] + "...";
            { var n = VisHelper.Section("Effects", Brushes.DarkCyan); n.Items.Add(VisHelper.Leaf(eff)); root.Items.Add(n); }
        }

        // Next conditions chain
        if (!string.IsNullOrWhiteSpace(cond.IdNext) && cond.IdNext != "0")
            root.Items.Add(VisHelper.RefNode<Condition>(cond.IdNext, ",", null, null, "Next Conditions", Brushes.DarkMagenta));

        // Chance to trigger next
        if (!string.IsNullOrWhiteSpace(cond.ChanceNext) && cond.ChanceNext != "0")
            { var n = VisHelper.Section("Chance Next", Brushes.Gray); n.Items.Add(VisHelper.Leaf(cond.ChanceNext)); root.Items.Add(n); }

        // Thresholds
        if (!string.IsNullOrWhiteSpace(cond.Thresholds))
            { var n = VisHelper.Section("Thresholds", Brushes.Gray); n.Items.Add(VisHelper.Leaf(cond.Thresholds)); root.Items.Add(n); }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Condition cond) return new TextBlock { Text = "..." };
        var flags = cond.Fatal ? "⚠ Fatal" : cond.Permanent ? "Perm" : "Temp";
        var sub = $"{flags}  ·  {cond.Duration}h  ·  {cond.Color}";
        var sp = VisHelper.OverviewHeader(cond, null, sub);
        if (!string.IsNullOrWhiteSpace(cond.IdNext) && cond.IdNext != "0")
            sp.Children.Add(VisHelper.Kv("Next", cond.IdNext, 40));
        if (!string.IsNullOrWhiteSpace(cond.FieldNames))
        {
            var fn = cond.FieldNames;
            if (fn.Length > 60) fn = fn[..60] + "...";
            sp.Children.Add(VisHelper.Kv("Fields", fn, 50));
        }
        return VisHelper.Wrap(sp);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// AttackMode
// ══════════════════════════════════════════════════════════════════════════════

public class AttackModeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(AttackMode);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not AttackMode am) return new TextBlock { Text = "Invalid" };
        var tree = new TreeView();
        var root = VisHelper.Section(am.Subject, Brushes.DodgerBlue);

        // Image
        if (!string.IsNullOrWhiteSpace(am.Image))
        {
            var bmp = VisHelper.LoadImage(am.Image);
            if (bmp is not null)
            {
                var imgSp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
                imgSp.Children.Add(new TextBlock { Text = $"Icon: {am.Image}", FontSize = 9 });
                imgSp.Children.Add(new Image { Source = bmp, MaxWidth = 96, MaxHeight = 96 });
                root.Items.Add(new TreeViewItem { IsExpanded = true, Header = imgSp });
            }
        }

        // Damage
        var dmgSec = VisHelper.Section("Damage", Brushes.Crimson);
        dmgSec.Items.Add(VisHelper.Leaf($"Cut: {am.DamageCut:F2}"));
        dmgSec.Items.Add(VisHelper.Leaf($"Blunt: {am.DamageBlunt:F2}"));
        dmgSec.Items.Add(VisHelper.Leaf($"Penetration: {am.Penetration}"));
        dmgSec.Items.Add(VisHelper.Leaf($"Morale: {am.Morale:P0}"));
        dmgSec.Items.Add(VisHelper.Leaf($"Range: {am.Range}  ·  Type: {am.Type}"));
        if (!string.IsNullOrWhiteSpace(am.Sound))
            dmgSec.Items.Add(VisHelper.Leaf($"Sound: {am.Sound}"));
        root.Items.Add(dmgSec);

        // Charge profiles
        if (!string.IsNullOrWhiteSpace(am.ChargeProfiles))
            root.Items.Add(VisHelper.RefNode<ChargeProfile>(am.ChargeProfiles, ",", null, null, "Charge Profiles", Brushes.Teal));

        // Attacker conditions
        if (!string.IsNullOrWhiteSpace(am.AttackerConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(am.AttackerConditions, ",", "{id}x{mult}", null, "Attacker Conditions", Brushes.DarkMagenta));

        // Flavor text
        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
        {
            var text = am.WieldPhrase.Length > 300 ? am.WieldPhrase[..300] + "..." : am.WieldPhrase;
            { var n = VisHelper.Section("Wield Phrase", Brushes.Gray); n.Items.Add(VisHelper.Leaf(text)); root.Items.Add(n); }
        }
        if (!string.IsNullOrWhiteSpace(am.AttackPhrases))
        {
            var text = am.AttackPhrases.Length > 300 ? am.AttackPhrases[..300] + "..." : am.AttackPhrases;
            { var n = VisHelper.Section("Attack Phrases", Brushes.Gray); n.Items.Add(VisHelper.Leaf(text)); root.Items.Add(n); }
        }

        // Flags
        if (am.Transfer)
            root.Items.Add(VisHelper.Leaf("Transferable", Brushes.SeaGreen));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not AttackMode am) return new TextBlock { Text = "..." };
        var totalDmg = am.DamageCut + am.DamageBlunt;
        var sub = $"Dmg: {totalDmg:F1} (C{am.DamageCut:F1}/B{am.DamageBlunt:F1})  ·  Range {am.Range}  ·  {am.Type}";
        var bmp = VisHelper.LoadImage(am.Image);
        var sp = VisHelper.OverviewHeader(am, bmp, sub);
        if (!string.IsNullOrWhiteSpace(am.ChargeProfiles))
            sp.Children.Add(VisHelper.Kv("Charges", am.ChargeProfiles, 60));
        return VisHelper.Wrap(sp);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// BattleMove
// ══════════════════════════════════════════════════════════════════════════════

public class BattleMoveEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(BattleMove);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not BattleMove bm) return new TextBlock { Text = "Invalid" };
        var tree = new TreeView();
        var root = VisHelper.Section(bm.Subject, Brushes.DodgerBlue);

        // Behavior flags
        var flags = new List<string>();
        if (bm.Offense) flags.Add("Offensive");
        if (bm.Approach) flags.Add("Approach");
        if (bm.FallBack) flags.Add("FallBack");
        if (bm.Retreat) flags.Add("Retreat");
        if (bm.Position) flags.Add("Position");
        if (bm.Passive) flags.Add("Passive");
        if (bm.AllOutOfRange) flags.Add("AllOutOfRange");
        if (bm.InAttackRange) flags.Add("InAttackRange");
        var flagSec = VisHelper.Section("Flags", Brushes.SteelBlue);
        flagSec.Items.Add(VisHelper.Leaf(string.Join(" · ", flags.Count > 0 ? flags : ["None"])));
        flagSec.Items.Add(VisHelper.Leaf($"Type: {bm.AttackModeType}  ·  Chance: {bm.Chance:P0}  ·  Priority: {bm.Priority:F2}"));
        flagSec.Items.Add(VisHelper.Leaf($"Fatigue: {bm.Fatigue:F2}  ·  Detect: {bm.Detect:P0}"));
        flagSec.Items.Add(VisHelper.Leaf($"Range: {bm.MinRange} to {bm.MaxRange}  ·  See Us: {bm.SeeUs} / Them: {bm.SeeThem}"));
        root.Items.Add(flagSec);

        // Success / Fail text
        if (!string.IsNullOrWhiteSpace(bm.Success))
        {
            var t = bm.Success.Length > 300 ? bm.Success[..300] + "..." : bm.Success;
            { var n = VisHelper.Section("On Success", Brushes.DarkGreen); n.Items.Add(VisHelper.Leaf(t)); root.Items.Add(n); }
        }
        if (!string.IsNullOrWhiteSpace(bm.Fail))
        {
            var t = bm.Fail.Length > 300 ? bm.Fail[..300] + "..." : bm.Fail;
            { var n = VisHelper.Section("On Fail", Brushes.DarkRed); n.Items.Add(VisHelper.Leaf(t)); root.Items.Add(n); }
        }
        if (!string.IsNullOrWhiteSpace(bm.PopUp))
        {
            var t = bm.PopUp.Length > 500 ? bm.PopUp[..500] + "..." : bm.PopUp;
            { var n = VisHelper.Section("Description", Brushes.SlateBlue); n.Items.Add(VisHelper.Leaf(t)); root.Items.Add(n); }
        }

        // Conditions
        if (!string.IsNullOrWhiteSpace(bm.UsPreConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.UsPreConditions, ",", null, null, "Us Pre-Conditions", Brushes.DarkOrange));
        if (!string.IsNullOrWhiteSpace(bm.ThemPreConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.ThemPreConditions, ",", null, null, "Them Pre-Conditions", Brushes.DarkOrange));
        if (!string.IsNullOrWhiteSpace(bm.UsConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.UsConditions, "],[", "[{id}", null, "Us Effect Conditions", Brushes.DarkMagenta));
        if (!string.IsNullOrWhiteSpace(bm.ThemConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.ThemConditions, "],[", "[{id}", null, "Them Effect Conditions", Brushes.DarkMagenta));
        if (!string.IsNullOrWhiteSpace(bm.PairConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.PairConditions, "],[", "[{id}", null, "Pair Conditions", Brushes.DarkMagenta));
        if (!string.IsNullOrWhiteSpace(bm.UsFailConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.UsFailConditions, "],[", "[{id}", null, "Us Fail Conditions", Brushes.Gray));
        if (!string.IsNullOrWhiteSpace(bm.ThemFailConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.ThemFailConditions, "],[", "[{id}", null, "Them Fail Conditions", Brushes.Gray));
        if (!string.IsNullOrWhiteSpace(bm.PairFailConditions))
            root.Items.Add(VisHelper.RefNode<Condition>(bm.PairFailConditions, "],[", "[{id}", null, "Pair Fail Conditions", Brushes.Gray));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not BattleMove bm) return new TextBlock { Text = "..." };
        var kind = bm.Offense ? "Offensive" : bm.Retreat ? "Retreat" : bm.Passive ? "Passive" : "Action";
        var sub = $"{kind}  ·  {bm.AttackModeType}  ·  {bm.Chance:P0} chance  ·  Fat {bm.Fatigue:F1}";
        var sp = VisHelper.OverviewHeader(bm, null, sub);
        if (!string.IsNullOrWhiteSpace(bm.PopUp))
        {
            var pop = bm.PopUp.Length > 100 ? bm.PopUp[..100] + "..." : bm.PopUp;
            sp.Children.Add(VisHelper.Kv("Desc", pop, 40));
        }
        return VisHelper.Wrap(sp);
    }
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
        var tree = new TreeView();
        var root = VisHelper.Section(ht.Subject, Brushes.DodgerBlue);

        if (!string.IsNullOrWhiteSpace(ht.Description))
            root.Items.Add(VisHelper.Leaf($"Display: {ht.Description}", Brushes.SlateBlue));

        // Terrain stats
        var terrainSec = VisHelper.Section("Terrain", Brushes.SeaGreen);
        terrainSec.Items.Add(VisHelper.Leaf($"Terrain Cost: {ht.TerrainCost}"));
        terrainSec.Items.Add(VisHelper.Leaf($"Visibility: {ht.VizIncrease - ht.VizLimiter} (base {ht.VizIncrease}, limit {ht.VizLimiter})"));
        terrainSec.Items.Add(VisHelper.Leaf($"Passable: {ht.Passable}"));
        terrainSec.Items.Add(VisHelper.Leaf($"Encounter Range: {ht.MinRange}–{ht.MaxRange}"));
        root.Items.Add(terrainSec);

        // Light levels
        if (!string.IsNullOrWhiteSpace(ht.LightLevels))
        {
            var lightNames = new[] { "Dawn", "Morning", "Noon", "Afternoon", "Dusk", "Midnight" };
            var levels = ht.LightLevels.Split(',').Select(s => s.Trim()).ToList();
            var lightSec = VisHelper.Section("Light Levels", Brushes.Goldenrod);
            for (int i = 0; i < Math.Min(levels.Count, lightNames.Length); i++)
                lightSec.Items.Add(VisHelper.Leaf($"{lightNames[i]}: {levels[i]}"));
            root.Items.Add(lightSec);
        }

        // Loot tables
        if (!string.IsNullOrWhiteSpace(ht.TreasureId) && ht.TreasureId != "3")
            root.Items.Add(VisHelper.RefNode<TreasureTable>(ht.TreasureId, null, null, null, "Scavenge Loot", Brushes.Teal));
        if (!string.IsNullOrWhiteSpace(ht.ScavengeInitialId) && ht.ScavengeInitialId != "3")
            root.Items.Add(VisHelper.RefNode<TreasureTable>(ht.ScavengeInitialId, null, null, null, "Initial Scavenge", Brushes.Teal));
        if (!string.IsNullOrWhiteSpace(ht.ScavengeItemsIdPerHour) && ht.ScavengeItemsIdPerHour != "25")
            root.Items.Add(VisHelper.RefNode<TreasureTable>(ht.ScavengeItemsIdPerHour, null, null, null, "Hourly Scavenge", Brushes.Teal));

        // Camp
        if (ht.DefaultCampId != 517)
            root.Items.Add(VisHelper.RefNode<CampType>(ht.DefaultCampId.ToString(), null, null, null, "Default Camp", Brushes.Orange));
        if (ht.CampItems != 5)
            root.Items.Add(VisHelper.Leaf($"Camp Items: {ht.CampItems}"));

        // Conditions on enter
        if (!string.IsNullOrWhiteSpace(ht.ConditionIds))
            root.Items.Add(VisHelper.RefNode<Condition>(ht.ConditionIds, ",", null, null, "On-Enter Conditions", Brushes.DarkMagenta));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not HexType ht) return new TextBlock { Text = "..." };
        var sub = $"{(ht.Passable == PassableType.Passable ? "Passable" : "Blocked")}  ·  Cost {ht.TerrainCost}  ·  Vis {ht.VizIncrease - ht.VizLimiter}";
        var sp = VisHelper.OverviewHeader(ht, null, sub);
        return VisHelper.Wrap(sp);
    }
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
        var tree = new TreeView();
        var root = VisHelper.Section(f.Subject, Brushes.DodgerBlue);

        if (!string.IsNullOrWhiteSpace(f.DictFactions))
        {
            var factions = ReferenceResolver.GetDedupedInt<Faction>();
            var relSec = VisHelper.Section("Diplomatic Relations", Brushes.Orange);
            foreach (var seg in f.DictFactions.Split(','))
            {
                var parts = seg.Trim().Split('=');
                if (parts.Length < 2) continue;
                var fid = parts[0].Trim();
                var relStr = parts[1].Trim();
                var relVal = int.TryParse(relStr, out var rv) ? rv : 0;
                var otherName = int.TryParse(fid, out var fi) && factions.TryGetValue(fi, out var of) ? of.Name : fid;
                var relDesc = relVal >= 100 ? "Allied" : relVal >= 50 ? "Friendly" : relVal >= 0 ? "Neutral" : relVal >= -50 ? "Hostile" : "Enemy";
                var fg = relVal >= 50 ? Brushes.DarkGreen : relVal >= 0 ? Brushes.Gray : relVal >= -50 ? Brushes.DarkOrange : Brushes.DarkRed;
                var leaf = VisHelper.Leaf($"{otherName}: {relStr} ({relDesc})", fg);
                if (int.TryParse(fid, out var fidInt))
                {
                    var capturedId = fidInt;
                    leaf.Cursor = new Cursor(StandardCursorType.Hand);
                    leaf.PointerPressed += (_, e) =>
                    { if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.NavigateToByKey<Faction>(capturedId); };
                }
                relSec.Items.Add(leaf);
            }
            root.Items.Add(relSec);
        }

        // Reverse: creatures in this faction
        if (GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Creature), out var creatureList) && creatureList is not null)
        {
            var members = creatureList.OfType<Creature>().Where(c => c.Faction == f.Id.ToString()).ToList();
            if (members.Count > 0)
            {
                var memSec = VisHelper.Section("Members", Brushes.DarkGreen);
                foreach (var m in members)
                    memSec.Items.Add(VisHelper.NavLeaf(m.Subject, () => ReferenceResolver.NavigateTo(typeof(Creature), m.EntityId), Brushes.Green));
                root.Items.Add(memSec);
            }
        }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Faction f) return new TextBlock { Text = "..." };
        var relationCount = string.IsNullOrWhiteSpace(f.DictFactions) ? 0 : f.DictFactions.Split(',').Length;
        var sp = VisHelper.OverviewHeader(f, null, $"{relationCount} relation(s)");
        return VisHelper.Wrap(sp);
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
        var tree = new TreeView();
        var root = VisHelper.Section(ing.Subject, Brushes.DodgerBlue);

        // Required vs Forbidden comparison
        var itemProps = ReferenceResolver.GetDedupedInt<ItemProp>();

        if (!string.IsNullOrWhiteSpace(ing.RequiredProps))
        {
            var reqSec = VisHelper.Section("Required Properties", Brushes.DarkGreen);
            foreach (var s in ing.RequiredProps.Split('&'))
            {
                if (int.TryParse(s.Trim(), out var pid) && itemProps.TryGetValue(pid, out var p))
                    reqSec.Items.Add(VisHelper.NavLeaf(p.PropertyName, () => ReferenceResolver.NavigateToByKey<ItemProp>(pid), Brushes.Green));
                else if (!string.IsNullOrWhiteSpace(s.Trim()))
                    reqSec.Items.Add(VisHelper.Leaf(s.Trim()));
            }
            root.Items.Add(reqSec);
        }

        if (!string.IsNullOrWhiteSpace(ing.ForbidProps))
        {
            var forbSec = VisHelper.Section("Forbidden Properties", Brushes.DarkRed);
            foreach (var s in ing.ForbidProps.Split('&'))
            {
                if (int.TryParse(s.Trim(), out var pid) && itemProps.TryGetValue(pid, out var p))
                    forbSec.Items.Add(VisHelper.NavLeaf(p.PropertyName, () => ReferenceResolver.NavigateToByKey<ItemProp>(pid), Brushes.Red));
                else if (!string.IsNullOrWhiteSpace(s.Trim()))
                    forbSec.Items.Add(VisHelper.Leaf(s.Trim()));
            }
            root.Items.Add(forbSec);
        }

        // Reverse: which recipes use this
        var revRefs = ReferenceResolver.FindReverseReferences(typeof(Ingredient), ing.Id);
        if (revRefs.Count > 0)
        {
            var revSec = VisHelper.Section($"Used in {revRefs.Count} Recipe(s)", Brushes.DarkMagenta);
            foreach (var (_, _, srcEntity) in revRefs.Take(20))
                revSec.Items.Add(VisHelper.NavLeaf(srcEntity.Subject, () => ReferenceResolver.NavigateTo(typeof(Recipe), srcEntity.EntityId), Brushes.Magenta));
            if (revRefs.Count > 20) revSec.Items.Add(VisHelper.Leaf($"... and {revRefs.Count - 20} more"));
            root.Items.Add(revSec);
        }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Ingredient ing) return new TextBlock { Text = "..." };
        var sp = VisHelper.OverviewHeader(ing);
        var reqCount = string.IsNullOrWhiteSpace(ing.RequiredProps) ? 0 : ing.RequiredProps.Split('&').Length;
        var forbCount = string.IsNullOrWhiteSpace(ing.ForbidProps) ? 0 : ing.ForbidProps.Split('&').Length;
        sp.Children.Add(VisHelper.Kv("Required", $"{reqCount} props", 65));
        if (forbCount > 0) sp.Children.Add(VisHelper.Kv("Forbidden", $"{forbCount} props", 65));
        return VisHelper.Wrap(sp);
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
        var tree = new TreeView();
        var root = VisHelper.Section(ip.Subject, Brushes.DodgerBlue);
        root.Items.Add(VisHelper.Leaf($"Name: {ip.PropertyName}", Brushes.SteelBlue));

        // Reverse references
        var revRefs = ReferenceResolver.FindReverseReferences(typeof(ItemProp), ip.Id);
        if (revRefs.Count > 0)
        {
            var revSec = VisHelper.Section($"Referenced by {revRefs.Count} Entities", Brushes.DarkMagenta);
            foreach (var (srcType, propName, srcEntity) in revRefs.Take(30))
            {
                var label = $"{srcType.Name}: {srcEntity.Subject}  ({propName})";
                revSec.Items.Add(VisHelper.NavLeaf(label, () => ReferenceResolver.NavigateTo(srcType, srcEntity.EntityId), Brushes.Magenta));
            }
            if (revRefs.Count > 30) revSec.Items.Add(VisHelper.Leaf($"... and {revRefs.Count - 30} more"));
            root.Items.Add(revSec);
        }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not ItemProp ip) return new TextBlock { Text = "..." };
        var sp = VisHelper.OverviewHeader(ip);
        sp.Children.Add(VisHelper.Kv("Property", ip.PropertyName, 55));
        return VisHelper.Wrap(sp);
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
        var tree = new TreeView();
        var root = VisHelper.Section(et.Subject, Brushes.DodgerBlue);

        // Trigger type
        var types = new List<string>();
        if (et.LocBased) types.Add("Location");
        if (et.DateBased) types.Add("Date");
        if (et.HexBased) types.Add("Hex Type");
        if (et.Unique) types.Add("Unique");
        if (et.AIPassable) types.Add("AI Passable");
        var typeSec = VisHelper.Section("Trigger Type", Brushes.SteelBlue);
        typeSec.Items.Add(VisHelper.Leaf(string.Join(" · ", types)));
        typeSec.Items.Add(VisHelper.Leaf($"Chance: {et.Chance:P0}"));
        root.Items.Add(typeSec);

        // Area
        if (!string.IsNullOrWhiteSpace(et.Area))
            { var n = VisHelper.Section("Location", Brushes.DarkCyan); n.Items.Add(VisHelper.Leaf($"Area: {et.Area}")); root.Items.Add(n); }

        // Date range
        if (!string.IsNullOrWhiteSpace(et.DateMin) || !string.IsNullOrWhiteSpace(et.DateMax))
        {
            var dateSec = VisHelper.Section("Date Range", Brushes.DarkCyan);
            if (!string.IsNullOrWhiteSpace(et.DateMin)) dateSec.Items.Add(VisHelper.Leaf($"From: {et.DateMin}"));
            if (!string.IsNullOrWhiteSpace(et.DateMax)) dateSec.Items.Add(VisHelper.Leaf($"To: {et.DateMax}"));
            root.Items.Add(dateSec);
        }

        // Hex Types
        if (!string.IsNullOrWhiteSpace(et.HexTypes))
            root.Items.Add(VisHelper.RefNode<HexType>(et.HexTypes, ",", null, null, "Hex Types", Brushes.DarkCyan));

        // Linked encounter
        if (et.EncounterId != 0)
            root.Items.Add(VisHelper.RefNode<Encounter>(et.EncounterId.ToString(), null, null, null, "Encounter", Brushes.Teal));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not EncounterTrigger et) return new TextBlock { Text = "..." };
        var types = new List<string>();
        if (et.LocBased) types.Add("Loc");
        if (et.DateBased) types.Add("Date");
        if (et.HexBased) types.Add("Hex");
        if (et.Unique) types.Add("Unique");
        var sub = $"{(types.Count > 0 ? string.Join("+", types) : "Manual")}  ·  {et.Chance:P0} chance";
        var sp = VisHelper.OverviewHeader(et, null, sub);
        if (et.EncounterId != 0)
            sp.Children.Add(VisHelper.Kv("Encounter", $"#{et.EncounterId}", 65));
        return VisHelper.Wrap(sp);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// CampType
// ══════════════════════════════════════════════════════════════════════════════

public class CampTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(CampType);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not CampType ct) return new TextBlock { Text = "Invalid" };
        var tree = new TreeView();
        var title = string.IsNullOrWhiteSpace(ct.Description) ? $"Camp #{ct.Id}" : $"{ct.Description} (#{ct.Id})";
        var root = VisHelper.Section(title, Brushes.DodgerBlue);

        // Image
        if (!string.IsNullOrWhiteSpace(ct.ImageList))
        {
            var bmp = VisHelper.LoadImage(ct.ImageList);
            if (bmp is not null)
            {
                var imgSp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
                imgSp.Children.Add(new TextBlock { Text = $"Image: {ct.ImageList}", FontSize = 9 });
                imgSp.Children.Add(new Image { Source = bmp, MaxWidth = 128, MaxHeight = 128 });
                root.Items.Add(new TreeViewItem { IsExpanded = true, Header = imgSp });
            }
        }

        // Stats
        var statsSec = VisHelper.Section("Stats", Brushes.SeaGreen);
        statsSec.Items.Add(VisHelper.Leaf($"Capacity: {ct.Capacities}"));
        statsSec.Items.Add(VisHelper.Leaf($"Alertness: {ct.Alertness:P0}"));
        statsSec.Items.Add(VisHelper.Leaf($"Visibility: {ct.Visibility:P0}"));
        statsSec.Items.Add(VisHelper.Leaf($"Heal/hr: {ct.HealPerHourMod:P0}"));
        statsSec.Items.Add(VisHelper.Leaf($"Sleep Quality: {ct.SleepQuality:P0}"));
        statsSec.Items.Add(VisHelper.Leaf($"Temp Adjust: {ct.WetTempAdjustMod:F2}"));
        root.Items.Add(statsSec);

        // Loot
        if (!string.IsNullOrWhiteSpace(ct.TreasureId) && ct.TreasureId != "3")
            root.Items.Add(VisHelper.RefNode<TreasureTable>(ct.TreasureId, null, null, null, "Loot Table", Brushes.Teal));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not CampType ct) return new TextBlock { Text = "..." };
        var bmp = VisHelper.LoadImage(ct.ImageList);
        var sub = $"Sleep {ct.SleepQuality:P0}  ·  Heal {ct.HealPerHourMod:P0}  ·  Vis {ct.Visibility:P0}";
        var sp = VisHelper.OverviewHeader(ct, bmp, sub);
        return VisHelper.Wrap(sp);
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
        var tree = new TreeView();
        var root = VisHelper.Section(cp.Subject, Brushes.DodgerBlue);

        var statsSec = VisHelper.Section("Consumption", Brushes.SeaGreen);
        statsSec.Items.Add(VisHelper.Leaf($"Item ID: {cp.ItemId}"));
        statsSec.Items.Add(VisHelper.Leaf($"Per Use: {cp.PerUse:F2}"));
        statsSec.Items.Add(VisHelper.Leaf($"Per Hour: {cp.PerHour:F2}"));
        statsSec.Items.Add(VisHelper.Leaf($"Per Hour Equipped: {cp.PerHourEquipped:F2}"));
        statsSec.Items.Add(VisHelper.Leaf($"Per Hex: {cp.PerHex:F2}"));
        if (cp.Degrade) statsSec.Items.Add(VisHelper.Leaf("Degradable", Brushes.DarkOrange));
        root.Items.Add(statsSec);

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not ChargeProfile cp) return new TextBlock { Text = "..." };
        var rates = new List<string>();
        if (cp.PerUse != 0) rates.Add($"Use:{cp.PerUse:F1}");
        if (cp.PerHour != 0) rates.Add($"Hr:{cp.PerHour:F1}");
        if (cp.PerHex != 0) rates.Add($"Hex:{cp.PerHex:F1}");
        var sub = string.Join("  ", rates);
        var sp = VisHelper.OverviewHeader(cp, null, sub);
        sp.Children.Add(VisHelper.Kv("Item", cp.ItemId, 40));
        return VisHelper.Wrap(sp);
    }
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
        var tree = new TreeView();
        var root = VisHelper.Section("Container Type", Brushes.DodgerBlue);
        root.Items.Add(VisHelper.Leaf($"Name: {ct.Name}"));
        root.Items.Add(VisHelper.Leaf($"ID: {ct.Id}"));

        // Reverse: which items use this container
        var revRefs = ReferenceResolver.FindReverseReferences(typeof(ContainerType), ct.Id);
        if (revRefs.Count > 0)
        {
            var revSec = VisHelper.Section($"Used by {revRefs.Count} Items", Brushes.DarkMagenta);
            foreach (var (_, _, srcEntity) in revRefs.Take(20))
                revSec.Items.Add(VisHelper.NavLeaf(srcEntity.Subject, () => ReferenceResolver.NavigateTo(typeof(ItemType), srcEntity.EntityId), Brushes.Magenta));
            if (revRefs.Count > 20) revSec.Items.Add(VisHelper.Leaf($"... and {revRefs.Count - 20} more"));
            root.Items.Add(revSec);
        }

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not ContainerType ct) return new TextBlock { Text = "..." };
        var sp = VisHelper.OverviewHeader(ct);
        return VisHelper.Wrap(sp);
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
        var tree = new TreeView();
        var root = VisHelper.Section(cs.Subject, Brushes.DodgerBlue);

        var posSec = VisHelper.Section("Spawn Info", Brushes.SteelBlue);
        posSec.Items.Add(VisHelper.Leaf($"Coords: ({cs.X}, {cs.Y})"));
        posSec.Items.Add(VisHelper.Leaf($"Spawn Count: {cs.Min}–{cs.Max}"));
        posSec.Items.Add(VisHelper.Leaf($"Weight: {cs.Weight:F2}"));
        root.Items.Add(posSec);

        if (!string.IsNullOrWhiteSpace(cs.CreatureId) && cs.CreatureId != "0")
            root.Items.Add(VisHelper.RefNode<Creature>(cs.CreatureId, null, null, null, "Creature", Brushes.Teal));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not CreatureSource cs) return new TextBlock { Text = "..." };
        var sub = $"({cs.X}, {cs.Y})  ·  {cs.Min}–{cs.Max} spawns";
        var sp = VisHelper.OverviewHeader(cs, null, sub);
        return VisHelper.Wrap(sp);
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
        var tree = new TreeView();
        var root = VisHelper.Section($"DMC Place #{dp.Id}", Brushes.DodgerBlue);

        // Image
        if (!string.IsNullOrWhiteSpace(dp.Image))
        {
            var bmp = VisHelper.LoadImage(dp.Image);
            if (bmp is not null)
            {
                var imgSp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4, 2) };
                imgSp.Children.Add(new TextBlock { Text = $"Icon: {dp.Image}", FontSize = 9 });
                imgSp.Children.Add(new Image { Source = bmp, MaxWidth = 128, MaxHeight = 128 });
                root.Items.Add(new TreeViewItem { IsExpanded = true, Header = imgSp });
            }
        }

        var posSec = VisHelper.Section("Location", Brushes.SteelBlue);
        posSec.Items.Add(VisHelper.Leaf($"Coords: ({dp.X}, {dp.Y})"));
        root.Items.Add(posSec);

        if (dp.EncounterId != 1)
            root.Items.Add(VisHelper.RefNode<Encounter>(dp.EncounterId.ToString(), null, null, null, "Encounter", Brushes.Teal));

        tree.Items.Add(root);
        return VisHelper.Wrap(tree);
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not DmcPlace dp) return new TextBlock { Text = "..." };
        var bmp = VisHelper.LoadImage(dp.Image);
        var sub = $"({dp.X}, {dp.Y})";
        var sp = VisHelper.OverviewHeader(dp, bmp, sub);
        return VisHelper.Wrap(sp);
    }
}
