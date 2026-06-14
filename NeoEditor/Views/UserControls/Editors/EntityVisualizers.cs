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
using NeoEditor.Views.UserControls;

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
        item.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) != 0) nav();
        };
        return item;
    }

    public static TreeViewItem RefNode<T>(string raw, string? separator, string? pattern, string? targetKey,
        string label, IBrush fg) where T : IEntity
    {
        var node = Section(label, fg);
        if (string.IsNullOrWhiteSpace(raw))
        {
            node.Items.Add(Leaf("(None)", Brushes.Gray));
            return node;
        }

        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null)
        {
            node.Items.Add(Leaf(raw, Brushes.Gray));
            return node;
        }

        var parts = separator is not null ? raw.Split(separator) : [raw];
        foreach (var seg in parts)
        {
            var s = seg.Trim();
            if (string.IsNullOrEmpty(s)) continue;
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
        // Try exact name first, then with .png extension (e.g. "btn_dmc_diner" → "btn_dmc_diner.png")
        var candidates = name.Contains('.') ? new[] { name } : new[] { name + ".png", name };
        string? path = null;
        foreach (var c in candidates)
        {
            path = ImageService.FindImage(c);
            if (path is not null) break;
        }

        if (path is null) return null;
        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    public static string StripNs(string name)
    {
        var c = name.IndexOf(':');
        return c > 0 ? name[(c + 1)..] : name;
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
        titleRow.Children.Add(new TextBlock
        {
            Text = entity.Subject ?? $"[{entity.GetType().Name}]", FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        textCol.Children.Add(titleRow);

        // ID-related badges: modId:modName, mid, pk, entityId
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var modName = Helper.GenericDataGridHelper.EntityModNames.TryGetValue(entity.EntityId, out var mn)
            ? mn
            : $"mod_{entity.ModId}";
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#20000000"),
            Padding = new Thickness(5, 1),
            Child = new TextBlock { Text = $"{entity.ModId}:{modName}", FontSize = 9, Foreground = Brush.Parse("#888") }
        });
        var mergedId = Helper.GenericDataGridHelper.EntityMergedIds.TryGetValue(entity.EntityId, out var mid)
            ? mid
            : 0;
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3), Background = Brush.Parse("#E65100"), Padding = new Thickness(4, 1),
            Child = new TextBlock { Text = $"mid={mergedId}", FontSize = 8, Foreground = Brushes.White }
        });
        var pkProp = Helper.EntityHelper.ResolveKeyProperty(entity.GetType());
        var pkVal = pkProp?.GetValue(entity) is int pk ? pk : -1;
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3), Background = Brush.Parse("#6A1B9A"), Padding = new Thickness(4, 1),
            Child = new TextBlock { Text = $"pk={pkVal}", FontSize = 8, Foreground = Brushes.White }
        });
        var eidShort = entity.EntityId.Length > 10 ? entity.EntityId[..10] + "…" : entity.EntityId;
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3), Background = Brush.Parse("#37474F"), Padding = new Thickness(4, 1),
            Child = new TextBlock { Text = eidShort, FontSize = 8, Foreground = Brushes.White }
        });
        textCol.Children.Add(idRow);
        if (subtitle is not null)
            textCol.Children.Add(new TextBlock
                { Text = subtitle, FontSize = 10, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
        header.Children.Add(textCol);
        sp.Children.Add(header);
        return sp;
    }

    public static TextBox Kv(string key, string value, int keyWidth = 90)
    {
        var tb = EditorUIFactory.SelectableText($"{key}: {value}", fontSize: 11);
        tb.Margin = new Thickness(0, 1);
        return tb;
    }

    public static ScrollViewer Wrap(Control content)
        => new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

    // ═══════════════ Shared layout primitives ═══════════════

    public static Border Card(Control content, string? title = null)
    {
        var child = content;
        if (title is not null)
        {
            var sp = new StackPanel { Spacing = 4 };
            sp.Children.Add(new TextBlock
            {
                Text = title, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 6)
            });
            sp.Children.Add(content);
            child = sp;
        }
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Brush.Parse("#08000000"),
            BorderBrush = Brush.Parse("#18000000"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14),
            Child = child
        };
    }

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
        var tb = new TextBlock
            { Text = text, FontSize = 10, Foreground = Brush.Parse(fg), Padding = new Thickness(7, 2) };
        var badge = new Border { CornerRadius = new CornerRadius(9), Background = Brush.Parse(bg), Child = tb };
        if (onClick is not null)
        {
            badge.Cursor = new Cursor(StandardCursorType.Hand);
            badge.PointerPressed += (_, e) =>
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0) onClick();
            };
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
            var colName = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ??
                          p.Name;
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            var strVal = val is bool b ? (b ? "1" : "0") : val?.ToString() ?? "";

            var isRef = refAttr is not null && !string.IsNullOrWhiteSpace(strVal);
            var display = strVal.Length > 100 ? strVal[..100] + "..." : strVal;
            if (string.IsNullOrWhiteSpace(strVal)) display = "(empty)";

            var keyTb = EditorUIFactory.SelectableText(colName, fontSize: 10,
                foreground: Brush.Parse("#888888"));
            keyTb.Margin = new Thickness(4, 2, 8, 2);
            keyTb.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(keyTb, row);
            Grid.SetColumn(keyTb, 0);
            grid.Children.Add(keyTb);

            var valTb = EditorUIFactory.SelectableText(display, fontSize: 10,
                foreground: isRef ? Brush.Parse("#00796B") :
                    string.IsNullOrWhiteSpace(strVal) ? Brush.Parse("#CCC") : Brush.Parse("#333"),
                fontWeight: isRef ? FontWeight.Medium : FontWeight.Normal);
            valTb.Margin = new Thickness(0, 2, 4, 2);
            valTb.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(valTb, row);
            Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            row++;
        }

        return grid;
    }

    // ═══════════════ Shared layout primitives (moved from AttackMode) ═══════════════

    public static Control StatBar(string label, string valueText, double fillRatio, string colorHex)
    {
        fillRatio = Math.Clamp(fillRatio, 0.05, 1.0);
        var grid = new Grid { MinHeight = 26 };
        grid.ColumnDefinitions.Add(new(80, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(2, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new(3, GridUnitType.Star));

        var labelTb = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        var fillStar = Math.Max((int)(fillRatio * 100), 6);
        var emptyStar = Math.Max(100 - fillStar, 0);
        grid.ColumnDefinitions[1] = new(fillStar, GridUnitType.Star);
        grid.ColumnDefinitions[2] = new(emptyStar, GridUnitType.Star);

        var fill = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = Brush.Parse(colorHex),
            Margin = new Thickness(0, 1)
        };
        Grid.SetColumn(fill, 1);
        grid.Children.Add(fill);

        // Text overlay — spans width of bar area so it's always fully visible
        var textOverlay = new TextBlock
        {
            Text = valueText, FontSize = 10, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0),
            Background = Brushes.Transparent
        };
        Grid.SetColumn(textOverlay, 1);
        Grid.SetColumnSpan(textOverlay, 2);
        grid.Children.Add(textOverlay);
        return grid;
    }

    /// <summary>StatBar with 0 at center — fills left for negative values, right for positive.</summary>
    public static Control CenteredStatBar(string label, string valueText, double value, double maxAbs,
        string? posColor = null, string? negColor = null)
    {
        posColor ??= "#2E7D32";
        negColor ??= "#C62828";
        var absRatio = Math.Clamp(Math.Abs(value) / Math.Max(maxAbs, 0.01), 0.08, 1.0);
        var isNeg = value < 0;

        var grid = new Grid { Height = 26 };
        grid.ColumnDefinitions.Add(new(80, GridUnitType.Pixel)); // label
        grid.ColumnDefinitions.Add(new(56, GridUnitType.Pixel)); // value text
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star)); // left fill
        grid.ColumnDefinitions.Add(new(3, GridUnitType.Pixel)); // center zero line
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star)); // right fill

        var labelTb = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        // Value text — always visible, between label and bar
        var valTb = new TextBlock
        {
            Text = valueText, FontSize = 10, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(isNeg ? negColor : value > 0 ? posColor : "#999"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0)
        };
        Grid.SetColumn(valTb, 1);
        grid.Children.Add(valTb);

        // Center zero marker
        var center = new Border { Background = Brush.Parse("#20000000"), Margin = new Thickness(0, 4) };
        Grid.SetColumn(center, 3);
        grid.Children.Add(center);

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
            Grid.SetColumn(fill, 2);
            grid.Children.Add(fill);
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
            Grid.SetColumn(fill, 4);
            grid.Children.Add(fill);
        }

        return grid;
    }

    /// <summary>Creature-style stat grid — label above big value, 2 columns by default.</summary>
    public static Control CreatureStatGrid(List<(string label, string value, string? color)> cells, int cols = 2)
    {
        var grid = new Grid { Margin = new Thickness(4, 0) };
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
        int rows = (cells.Count + cols - 1) / cols;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < cells.Count; i++)
        {
            int r = i / cols, c = i % cols;
            var (label, value, color) = cells[i];
            var cell = new StackPanel { Margin = new Thickness(4, 3) };
            cell.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Brush.Parse("#999") });
            cell.Children.Add(new TextBlock
            {
                Text = value, FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333")
            });
            Grid.SetRow(cell, r);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        return Card(grid);
    }

    /// <summary>Shared reverse-references panel — tabbed by source type, paginated.</summary>
    public static Control BuildReverseRefsPanel(string entityId)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return new StackPanel();
        var rawRefs = store.Index.ReverseLookup(entityId);
        if (rawRefs.Count == 0) return new StackPanel();

        var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var m = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (m != null)
                {
                    resolved.Add((t, m.Subject, srcEid, propName));
                    break;
                }
            }
        }

        if (resolved.Count == 0) return new StackPanel();

        var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();

        Control BuildRefList(IReadOnlyList<(Type SrcType, string SrcSubject, string SrcEid, string PropName)> items)
        {
            const int pageSize = 15;
            int totalPages = (items.Count + pageSize - 1) / pageSize;
            int currentPage = 0;

            var container = new StackPanel();
            var list = new StackPanel { Spacing = 3 };

            void RefreshPage()
            {
                list.Children.Clear();
                var page = items.Skip(currentPage * pageSize).Take(pageSize).ToList();
                foreach (var (srcType, srcSubject, srcEid, propName) in page)
                {
                    var tc = srcType == typeof(Creature) ? ("#E8EAF6", "#283593")
                        : srcType == typeof(ItemType) ? ("#E3F2FD", "#1565C0")
                        : srcType == typeof(Recipe) ? ("#F3E5F5", "#6A1B9A")
                        : srcType == typeof(Condition) ? ("#FCE4EC", "#C62828")
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
                                new Border
                                {
                                    CornerRadius = new CornerRadius(3), Background = Brush.Parse(tc.Item1),
                                    Padding = new Thickness(5, 1),
                                    Child = new TextBlock
                                        { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(tc.Item2) }
                                },
                                new TextBlock
                                {
                                    Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"),
                                    VerticalAlignment = VerticalAlignment.Center
                                },
                                new TextBlock
                                {
                                    Text = $"({propName})", FontSize = 9, Foreground = Brush.Parse("#999"),
                                    VerticalAlignment = VerticalAlignment.Center
                                }
                            }
                        }
                    };
                    var ct = srcType;
                    var ci = srcEid;
                    row.PointerPressed += (_, e) =>
                    {
                        if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct, ci);
                    };
                    list.Children.Add(row);
                }
            }

            RefreshPage();
            container.Children.Add(Card(list));

            if (totalPages > 1)
            {
                var pager = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var prevBtn = new Button
                {
                    Content = "←", FontSize = 12, Padding = new Thickness(8, 4),
                    Background = Brush.Parse("#0D000000"), BorderThickness = new Thickness(1),
                    BorderBrush = Brush.Parse("#18000000"), Cursor = new Cursor(StandardCursorType.Hand)
                };
                var pageLabel = new TextBlock
                {
                    Text = $"{Loc("Vis.Page")} 1 / {totalPages}", FontSize = 11,
                    Foreground = Brush.Parse("#666"), VerticalAlignment = VerticalAlignment.Center
                };
                var nextBtn = new Button
                {
                    Content = "→", FontSize = 12, Padding = new Thickness(8, 4),
                    Background = Brush.Parse("#0D000000"), BorderThickness = new Thickness(1),
                    BorderBrush = Brush.Parse("#18000000"), Cursor = new Cursor(StandardCursorType.Hand)
                };
                prevBtn.IsEnabled = false;
                prevBtn.Click += (_, _) =>
                {
                    if (currentPage > 0)
                    {
                        currentPage--;
                        RefreshPage();
                        pageLabel.Text = $"{Loc("Vis.Page")} {currentPage + 1} / {totalPages}";
                        prevBtn.IsEnabled = currentPage > 0;
                        nextBtn.IsEnabled = true;
                    }
                };
                nextBtn.Click += (_, _) =>
                {
                    if (currentPage < totalPages - 1)
                    {
                        currentPage++;
                        RefreshPage();
                        pageLabel.Text = $"{Loc("Vis.Page")} {currentPage + 1} / {totalPages}";
                        nextBtn.IsEnabled = currentPage < totalPages - 1;
                        prevBtn.IsEnabled = true;
                    }
                };
                pager.Children.Add(prevBtn);
                pager.Children.Add(pageLabel);
                pager.Children.Add(nextBtn);
                container.Children.Add(pager);
            }

            return container;
        }

        var sp = new StackPanel();

        // Always use TabControl for consistency — tabs by source entity type
        var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
        sp.Children.Add(SectionLabel($"{Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var tabControl = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var g in byType)
        {
            var tabItem = new TabItem
            {
                Header = $"{g.Key.Name} ({g.Count()})",
                Content = BuildRefList(g.ToList())
            };
            tabControl.Items.Add(tabItem);
        }
        if (tabControl.Items.Count > 0)
            tabControl.SelectedIndex = 0;
        sp.Children.Add(tabControl);

        return sp;
    }

    public static Control BuildExpander(string label, Border body)
    {
        var arrow = new TextBlock
        {
            Text = "▶", FontSize = 10, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center
        };
        var labelTb = new TextBlock
        {
            Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#888"),
            VerticalAlignment = VerticalAlignment.Center
        };
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

    /// <summary>Open an image in a zoomable popup overlay.</summary>
    public static void OpenZoomableImage(Bitmap? bitmap, string? title = null)
    {
        if (bitmap is null) return;
        var zoomView = new ZoomableImageView
        {
            Source = bitmap,
            Width = 600,
            Height = 480
        };
        var headerBorder = new Border
        {
            Background = Brush.Parse("#06000000"),
            Padding = new Thickness(16, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8, Children =
                {
                    new TextBlock
                    {
                        Text = title ?? "Image Preview", FontSize = 13, FontWeight = FontWeight.SemiBold,
                        Foreground = Brush.Parse("#555"), VerticalAlignment = VerticalAlignment.Center
                    },
                    new Button
                    {
                        Content = "✕", FontSize = 14, Padding = new Thickness(8, 2), Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0), Foreground = Brush.Parse("#999"),
                        HorizontalAlignment = HorizontalAlignment.Right, Cursor = new Cursor(StandardCursorType.Hand)
                    }
                }
            }
        };
        DockPanel.SetDock(headerBorder, Avalonia.Controls.Dock.Top);
        var closeBtn = (Button)((StackPanel)headerBorder.Child).Children[1];
        var popup = new Popup
        {
            PlacementTarget = null,
            Placement = PlacementMode.Center,
            Child = new Border
            {
                Width = 640,
                Height = 520,
                CornerRadius = new CornerRadius(12),
                Background = Brush.Parse("#F8F8F8"),
                BorderBrush = Brush.Parse("#20000000"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Child = new DockPanel
                {
                    Children =
                    {
                        headerBorder,
                        new Border { Child = zoomView, Margin = new Thickness(8, 0, 8, 8) }
                    }
                }
            },
            IsOpen = true
        };
        closeBtn.Click += (_, _) => popup.IsOpen = false;
    }

    public static TextBlock OvSectionLabel(string text) => new()
    {
        Text = text, FontSize = 10, FontWeight = FontWeight.SemiBold,
        Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 4)
    };

    public static void AddModBadge(IEntity entity, StackPanel row)
    {
        var modName = Helper.GenericDataGridHelper.EntityModNames.TryGetValue(entity.EntityId, out var mn)
            ? mn
            : $"mod_{entity.ModId}";
        // Mod badge
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse(entity.ModId >= 10000 ? "#1B5E20" : "#1565C0"),
            Padding = new Thickness(6, 2),
            Child = new TextBlock { Text = $"{entity.ModId}:{modName}", FontSize = 10, Foreground = Brushes.White }
        });
        // MergedId badge
        var mergedId = Helper.GenericDataGridHelper.EntityMergedIds.TryGetValue(entity.EntityId, out var mid)
            ? mid
            : 0;
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#E65100"),
            Padding = new Thickness(5, 2),
            Child = new TextBlock { Text = $"mid={mergedId}", FontSize = 10, Foreground = Brushes.White }
        });
        // PK badge
        var pkProp = Helper.EntityHelper.ResolveKeyProperty(entity.GetType());
        var pkVal = pkProp?.GetValue(entity) is int pk ? pk : -1;
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#6A1B9A"),
            Padding = new Thickness(5, 2),
            Child = new TextBlock { Text = $"pk={pkVal}", FontSize = 10, Foreground = Brushes.White }
        });
        // EntityId badge (short prefix)
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#37474F"),
            Padding = new Thickness(5, 2),
            Child = new TextBlock
            {
                Text = entity.EntityId.Length > 10 ? entity.EntityId[..10] : entity.EntityId, FontSize = 9,
                Foreground = Brushes.White
            }
        });
    }

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
            var lbl = new TextBlock
            {
                Text = label, FontSize = 10, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 1)
            };
            var val = new TextBlock
            {
                Text = value, FontSize = 10, FontWeight = FontWeight.Medium,
                Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1)
            };
            Grid.SetRow(lbl, i);
            Grid.SetColumn(lbl, 0);
            Grid.SetRow(val, i);
            Grid.SetColumn(val, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(val);
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
            var colName = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ??
                          p.Name;
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
            var col = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ??
                      p.Name;
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(it), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(it));

        var hasStats = it.Weight > 0 || it.StackLimit > 0 || it.MonetaryValue > 0 || it.Mirrored || it.SlotDepth > 0;
        if (hasStats)
            root.Children.Add(BuildStatsPanel(it));

        var hasDurability = it.Durability > 0 || it.DegradePerHour > 0 || it.EquipDegradePerHour > 0 || it.DegradePerUse > 0 ||
                           (!string.IsNullOrWhiteSpace(it.DegradeTreasureIds) && it.DegradeTreasureIds != "3,3");
        if (hasDurability)
            root.Children.Add(BuildDurabilityCard(it));

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
                           !string.IsNullOrWhiteSpace(it.ContentIds);
        if (hasContainer)
            root.Children.Add(BuildContainerCard(it));

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
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (!isImageList && imageNames.Count == 1)
        {
            var bmp = VisHelper.LoadImage(imageNames[0]);
            if (bmp is not null)
            {
                imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
                var b = bmp;
                imageArea.PointerPressed += (_, _) => VisHelper.OpenZoomableImage(b, it.Name);
            }
        }
        else if (imageNames.Count > 0)
            imageArea.Child = BuildImageGallery(imageNames);

        Grid.SetColumn(imageArea, 0);
        Grid.SetRowSpan(imageArea, 2);
        grid.Children.Add(imageArea);

        // ── Identity (right) ──
        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = $"{it.GroupId}.{it.SubgroupId}", FontSize = 11, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(it, idRow);
        identity.Children.Add(idRow);
        identity.Children.Add(new TextBlock
            { Text = it.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(it.Description) && it.Description != it.Name)
            identity.Children.Add(new TextBlock
            {
                Text = it.Description, FontSize = 12, Foreground = Brush.Parse("#666666"),
                TextWrapping = TextWrapping.Wrap
            });
        if (!string.IsNullOrWhiteSpace(it.DescriptionAlt))
            identity.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#FFF3E0"),
                Padding = new Thickness(8, 3),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new StackPanel
                {
                    Spacing = 1, Children =
                    {
                        new TextBlock
                        {
                            Text = $"✦ {VisHelper.Loc("Vis.Identified")}", FontSize = 9,
                            Foreground = Brush.Parse("#E65100")
                        },
                        new TextBlock
                        {
                            Text = it.DescriptionAlt, FontSize = 11, Foreground = Brush.Parse("#BF360C"),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            });
        Grid.SetColumn(identity, 1);
        Grid.SetRow(identity, 0);
        Grid.SetRowSpan(identity, 2);
        grid.Children.Add(identity);

        return VisHelper.Card(grid);
    }

    private static Control BuildImageGallery(List<string> names)
    {
        var idx = 0;
        var bmps = names.Select(VisHelper.LoadImage).Where(b => b is not null).Cast<Bitmap>().ToList();
        if (bmps.Count == 0)
            return new TextBlock
            {
                Text = "No images", FontSize = 10, Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
            };

        var imageView = new Image { Source = bmps[0], Stretch = Stretch.Uniform, Width = 132, Height = 106 };

        // Navigation dots + prev/next
        var nav = new DockPanel { Height = 26, Background = Brush.Parse("#14000000"), LastChildFill = true };
        var prevBtn = new Button
        {
            Content = "◀", FontSize = 9, Padding = new Thickness(4, 0), Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        var nextBtn = new Button
        {
            Content = "▶", FontSize = 9, Padding = new Thickness(4, 0), Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        var dotPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Spacing = 4
        };
        var dots = new List<Border>();
        for (int i = 0; i < bmps.Count; i++)
        {
            var dot = new Border
            {
                Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                Background = i == 0 ? Brush.Parse("#666") : Brush.Parse("#CCC")
            };
            dots.Add(dot);
            dotPanel.Children.Add(dot);
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

        imageView.Cursor = new Cursor(StandardCursorType.Hand);
        imageView.PointerPressed += (_, _) =>
        {
            if (bmps.Count > 0 && idx < bmps.Count)
                VisHelper.OpenZoomableImage(bmps[idx]);
        };

        var gallery = new DockPanel();
        var imgCapture = new Avalonia.Controls.DockPanel();
        imgCapture.Children.Add(imageView);
        gallery.Children.Add(nav);
        DockPanel.SetDock(nav, Avalonia.Controls.Dock.Bottom);
        gallery.Children.Add(imgCapture);

        return gallery;
    }

    // ═══════════════ Stats (Creature-style grid) ═══════════════

    private static Control BuildStatsPanel(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Stats")));

        var cells = new List<(string, string, string?)>();
        if (it.Weight > 0)
            cells.Add((VisHelper.Loc("Vis.Weight"), $"{it.Weight:F1} kg", "#4CAF50"));
        if (it.StackLimit > 0)
            cells.Add((VisHelper.Loc("Vis.StackLimit"), $"×{it.StackLimit}", "#2196F3"));
        if (it.MonetaryValue > 0)
        {
            var valText = it.MonetaryValueAlt > 0 && it.MonetaryValueAlt != it.MonetaryValue
                ? $"${it.MonetaryValue:F2} → ${it.MonetaryValueAlt:F2} (real)"
                : $"${it.MonetaryValue:F2}";
            cells.Add((VisHelper.Loc("Vis.Value"), valText, "#9C27B0"));
        }

        if (it.Mirrored)
            cells.Add(("", VisHelper.Loc("Vis.MirroredDesc"), "#607D8B"));
        if (it.SlotDepth > 0)
            cells.Add((VisHelper.Loc("Vis.SlotDepth"), $"{it.SlotDepth}", "#546E7A"));

        if (cells.Count == 0)
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = "(No stats)", FontSize = 10, Foreground = Brush.Parse("#999") }));
        else
            sp.Children.Add(VisHelper.CreatureStatGrid(cells));

        return sp;
    }

    // ═══════════════ Durability Card (耐久消耗) ═══════════════

    private static Control BuildDurabilityCard(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Degradation")));

        // Build card content: stat grid + break parts (损坏掉落) inside a single Card
        var cardContent = new StackPanel { Spacing = 8 };

        var cells = new List<(string, string, string?)>();
        if (it.Durability > 0)
        {
            var durText = it.Durability >= 999 ? "Infinite" : $"{it.Durability * 100:F0}%";
            cells.Add((VisHelper.Loc("Vis.Durability"), durText, it.Durability >= 999 ? "#607D8B" : "#FF9800"));
        }
        if (it.DegradePerHour > 0)
            cells.Add((VisHelper.Loc("Vis.PerHour"), $"{it.DegradePerHour:F3}", "#E65100"));
        if (it.EquipDegradePerHour > 0)
            cells.Add((VisHelper.Loc("Vis.PerHourEquipped"), $"{it.EquipDegradePerHour:F3}", "#C62828"));
        if (it.DegradePerUse > 0)
            cells.Add((VisHelper.Loc("Vis.PerUse"), $"{it.DegradePerUse:F3}", "#F57F17"));

        // Build stat grid inline (same structure as CreatureStatGrid but without Card wrapper)
        if (cells.Count > 0)
        {
            var statGrid = new Grid { Margin = new Thickness(4, 0) };
            statGrid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
            statGrid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
            int rows = (cells.Count + 1) / 2;
            for (int r = 0; r < rows; r++) statGrid.RowDefinitions.Add(new(GridLength.Auto));
            for (int i = 0; i < cells.Count; i++)
            {
                int r = i / 2, c = i % 2;
                var (label, value, color) = cells[i];
                var cell = new StackPanel { Margin = new Thickness(4, 3) };
                cell.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Brush.Parse("#999") });
                cell.Children.Add(new TextBlock
                {
                    Text = value, FontSize = 13, FontWeight = FontWeight.SemiBold,
                    Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333")
                });
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                statGrid.Children.Add(cell);
            }
            cardContent.Children.Add(statGrid);
        }

        // 损坏掉落 — inside the same card as durability stats
        if (!string.IsNullOrWhiteSpace(it.DegradeTreasureIds) && it.DegradeTreasureIds != "3,3")
        {
            var wp = new WrapPanel();
            foreach (var seg in it.DegradeTreasureIds.Split(',').Select(s => s.Trim())
                         .Where(s => s.Length > 0 && s != "3"))
            {
                var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(it, nameof(ItemType.DegradeTreasureIds),
                    seg);
                if (tt is not null)
                    wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#FFF8E1", "#F57F17",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            }

            if (wp.Children.Count > 0)
                cardContent.Children.Add(new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                            { Text = VisHelper.Loc("Vis.BreakParts"), FontSize = 10, Foreground = Brushes.Gray },
                        wp
                    }
                });
        }

        // Wrap everything in a single Card
        if (cardContent.Children.Count > 0)
            sp.Children.Add(VisHelper.Card(cardContent));

        return sp;
    }

    // ═══════════════ Properties → ItemProp ═══════════════

    private static Control BuildPropertiesPanel(ItemType it)
    {
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

        return VisHelper.Card(wp, VisHelper.Loc("Vis.ItemProperties"));
    }

    // ═══════════════ AttackModes → AttackMode  (format: {slot}={id}) ═══════════════

    private static Control BuildAttackModesPanel(ItemType it)
    {
        var wp = new WrapPanel();
        foreach (var seg in it.AttackModes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var eqIdx = seg.IndexOf('=');
            var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
            var amId = eqIdx > 0 ? seg[(eqIdx + 1)..].Trim() : seg;

            var slotName = int.TryParse(slotPart, out var sn) && SlotNames.TryGetValue(sn, out var snv)
                ? snv
                : slotPart;
            var am = ReferenceResolver.Instance.LookupRef<AttackMode>(it, nameof(ItemType.AttackModes), seg);
            if (am is not null)
            {
                var label = string.IsNullOrEmpty(slotName) ? am.Subject : $"{slotName}: {am.Subject}";
                wp.Children.Add(VisHelper.MiniBadge(label, "#FFEBEE", "#C62828",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(AttackMode), am.EntityId)));
            }
            else
                wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
        }

        return VisHelper.Card(wp, $"{VisHelper.Loc("Vis.AttackModes")} (→ AttackMode)");
    }

    // ═══════════════ Equipment card ═══════════════

    private static Control BuildEquipmentCard(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Equipment")));

        var cardContent = new StackPanel { Spacing = 8 };

        // Equip slots
        if (!string.IsNullOrWhiteSpace(it.EquipSlots))
        {
            var wp = new WrapPanel();
            foreach (var s in it.EquipSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                var name = int.TryParse(s, out var sn) && SlotNames.TryGetValue(sn, out var snv) ? snv : s;
                wp.Children.Add(VisHelper.MiniBadge(name, "#E3F2FD", "#1565C0"));
            }

            cardContent.Children.Add(new StackPanel
            {
                Spacing = 3, Children =
                {
                    new TextBlock { Text = VisHelper.Loc("Vis.EquipSlots"), FontSize = 10, Foreground = Brushes.Gray },
                    wp
                }
            });
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

            cardContent.Children.Add(new StackPanel
            {
                Spacing = 3, Children =
                {
                    new TextBlock { Text = VisHelper.Loc("Vis.UseSlots"), FontSize = 10, Foreground = Brushes.Gray },
                    wp
                }
            });
        }

        // SocketLocked
        if (it.SocketLocked)
        {
            cardContent.Children.Add(new StackPanel
            {
                Spacing = 2, Children =
                {
                    new TextBlock
                        { Text = VisHelper.Loc("Vis.SocketLocked"), FontSize = 10, Foreground = Brushes.Gray },
                    VisHelper.MiniBadge(VisHelper.Loc("Vis.SocketLockedDesc"), "#FFEBEE", "#C62828")
                }
            });
        }

        // Condition references
        if (!string.IsNullOrWhiteSpace(it.EquipConditions))
            cardContent.Children.Add(ConditionRow(VisHelper.Loc("Vis.WhenEquipped"), it.EquipConditions, it,
                nameof(ItemType.EquipConditions)));
        if (!string.IsNullOrWhiteSpace(it.UseConditions))
            cardContent.Children.Add(ConditionRow(VisHelper.Loc("Vis.WhenUsed"), it.UseConditions, it,
                nameof(ItemType.UseConditions)));
        if (!string.IsNullOrWhiteSpace(it.PossessConditions))
            cardContent.Children.Add(ConditionRow(VisHelper.Loc("Vis.WhenCarried"), it.PossessConditions, it,
                nameof(ItemType.PossessConditions)));

        sp.Children.Add(VisHelper.Card(cardContent));
        return sp;
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
            ? new StackPanel
            {
                Spacing = 3, Children = { new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.Gray }, wp }
            }
            : new TextBlock();
    }

    // ═══════════════ Container card ═══════════════

    private static Control BuildContainerCard(ItemType it)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.SpaceAttrs")));

        var cardContent = new StackPanel { Spacing = 6 };

        if (!string.IsNullOrWhiteSpace(it.Capacities))
            cardContent.Children.Add(new TextBlock
                { Text = $"{VisHelper.Loc("Vis.Capacity")}: {it.Capacities}", FontSize = 11 });

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
                cardContent.Children.Add(new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                            { Text = VisHelper.Loc("Vis.AcceptsContent"), FontSize = 10, Foreground = Brushes.Gray },
                        wp
                    }
                });
        }

        sp.Children.Add(VisHelper.Card(cardContent));
        return sp;
    }

    // ═══════════════ Charge card ═══════════════

    private static Control BuildChargeCard(ItemType it)
    {
        var wp = new WrapPanel();
        foreach (var seg in it.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cp = ReferenceResolver.Instance.LookupRef<ChargeProfile>(it, nameof(ItemType.ChargeProfiles), seg);
            if (cp is not null)
                wp.Children.Add(VisHelper.MiniBadge(cp.Name, "#E0F7FA", "#006064",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ChargeProfile), cp.EntityId)));
        }

        return VisHelper.Card(wp, VisHelper.Loc("Vis.ChargeAmmo"));
    }

    // ═══════════════ Switches → ItemType (toggle states) ═══════════════

    private static Control BuildSwitchesPanel(ItemType it)
    {
        var wp = new WrapPanel();
        foreach (var seg in it.SwitchIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var sw = ReferenceResolver.Instance.LookupRef<ItemType>(it, nameof(ItemType.SwitchIds), seg);
            if (sw is not null)
            {
                var descShort = string.IsNullOrWhiteSpace(sw.Description) ? ""
                    : sw.Description.Length > 10 ? sw.Description[..10] : sw.Description;
                var display = string.IsNullOrEmpty(descShort) ? sw.Name! : $"{sw.Name}({descShort})";
                wp.Children.Add(VisHelper.MiniBadge($"{sw.GroupId}.{sw.SubgroupId} {display}", "#F3E5F5", "#6A1B9A",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), sw.EntityId)));
            }
            else
                wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
        }

        return VisHelper.Card(wp, $"{VisHelper.Loc("Vis.SwitchStates")} (→ ItemType)");
    }

    // ═══════════════ Reference bars (resolved subjects) ═══════════════

    private static Control BuildRefsPanel(ItemType it)
    {
        var cardContent = new StackPanel { Spacing = 6 };

        if (!string.IsNullOrWhiteSpace(it.TreasureId) && it.TreasureId != "3")
        {
            cardContent.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.TreasureTable"), it.TreasureId, typeof(TreasureTable), sourceEntityId: it.EntityId));
        }

        if (!string.IsNullOrWhiteSpace(it.CondId) && it.CondId != "1")
        {
            cardContent.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.RequiredCondition"), it.CondId, typeof(Condition), sourceEntityId: it.EntityId));
        }

        if (!string.IsNullOrWhiteSpace(it.ComponentId) && it.ComponentId != "0")
        {
            cardContent.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.Component"), it.ComponentId, typeof(TreasureTable), sourceEntityId: it.EntityId));
        }

        if (!string.IsNullOrWhiteSpace(it.FormatId) && it.FormatId != "3")
        {
            cardContent.Children.Add(ResolvedRefRow(VisHelper.Loc("Vis.Format"), it.FormatId, typeof(ContainerType), sourceEntityId: it.EntityId));
        }

        return VisHelper.Card(cardContent, VisHelper.Loc("Vis.References"));
    }

    private static Control ResolvedRefRow(string label, string raw, Type targetType, string? targetKey = null, string sourceEntityId = "")
    {
        IEntity? match = GenericDataGridHelper.FindBestMatch(targetType, raw, targetKey, sourceEntityId, "");
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
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(tt, m.EntityId);
            };
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
            Child = new TextBlock
                { Text = "ItemType", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#283593") }
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
            Child = new TextBlock
            {
                Text = $"{it.GroupId}.{it.SubgroupId}", FontSize = 10, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#1565C0")
            }
        });

        // Title with optional thumb
        if (thumb is not null)
        {
            var headerRow = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            headerRow.Children.Add(new Image
                { Source = thumb, MaxWidth = 48, MaxHeight = 48, Stretch = Stretch.Uniform });
            headerRow.Children.Add(new TextBlock
            {
                Text = it.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            });
            root.Children.Add(headerRow);
        }
        else
        {
            root.Children.Add(new TextBlock
            {
                Text = it.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });
        }

        // Flags
        var flags = new List<string>();
        if (it.Mirrored) flags.Add(VisHelper.Loc("Vis.Mirrored"));
        if (it.SocketLocked) flags.Add(VisHelper.Loc("Vis.SocketLocked"));
        if (!string.IsNullOrWhiteSpace(it.SwitchIds)) flags.Add(VisHelper.Loc("Vis.Toggleable"));
        if (flags.Count > 0)
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                    { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });

        // Core stats
        var cells = new List<(string, string, string?)>();
        if (it.Weight > 0)
            cells.Add((VisHelper.Loc("Vis.Weight"), $"{it.Weight:F1} kg", "#4CAF50"));
        if (it.StackLimit > 0)
            cells.Add((VisHelper.Loc("Vis.StackLimit"), $"×{it.StackLimit}", "#2196F3"));
        if (it.Durability > 0)
            cells.Add((VisHelper.Loc("Vis.Durability"),
                it.Durability >= 999 ? "Infinite" : $"{it.Durability * 100:F0}%",
                it.Durability >= 999 ? "#607D8B" : "#FF9800"));
        if (it.MonetaryValue > 0)
            cells.Add((VisHelper.Loc("Vis.Value"), $"${it.MonetaryValue:F2}", "#9C27B0"));
        if (it.SlotDepth > 0)
            cells.Add((VisHelper.Loc("Vis.SlotDepth"), $"{it.SlotDepth}", "#546E7A"));

        if (cells.Count > 0)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
            root.Children.Add(VisHelper.CreatureStatGrid(cells));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(r), Padding = new Thickness(8) };
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
            Child = new TextBlock
                { Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") }
        });
        root.Children.Add(new TextBlock
        {
            Text = r.Subject ?? r.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        // Flags
        var flags = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.SecretName)) flags.Add(VisHelper.Loc("Vis.Secret"));
        if (r.Scrap) flags.Add(VisHelper.Loc("Vis.Scrap"));
        if (r.Identify) flags.Add(VisHelper.Loc("Vis.Identify"));
        if (r.DegradeOutput) flags.Add(VisHelper.Loc("Vis.DegradeOutput"));
        if (r.TransferComponents) flags.Add(VisHelper.Loc("Vis.TransferComponents"));
        if (flags.Count > 0)
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                    { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });

        var statRows = new List<(string, string, string?)>();
        statRows.Add((VisHelper.Loc("Vis.Hours"), $"{r.Hours:F1}", null));
        statRows.Add((VisHelper.Loc("Vis.Reverse"), r.Reverse > 0 ? VisHelper.Loc("Vis.Yes") : VisHelper.Loc("Vis.No"),
            null));
        statRows.Add(
            (VisHelper.Loc("Vis.Hidden"), r.HiddenId != "0" ? $"#{r.HiddenId}" : VisHelper.Loc("Vis.No"), null));
        var toolCount = string.IsNullOrWhiteSpace(r.Tools) ? 0 : r.Tools.Split('+').Length;
        var consCount = string.IsNullOrWhiteSpace(r.Consumed) ? 0 : r.Consumed.Split('+').Length;
        statRows.Add((VisHelper.Loc("Vis.Tools"), $"{toolCount}", null));
        statRows.Add((VisHelper.Loc("Vis.Consumed"), $"{consCount}", null));
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
        root.Children.Add(VisHelper.BuildStatCard(statRows));

        // Product preview
        if (!string.IsNullOrWhiteSpace(r.TreasureId) && r.TreasureId != "3")
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Loot")));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(r, nameof(Recipe.TreasureId), r.TreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32"));
            else
                wp.Children.Add(VisHelper.MiniBadge($"TT #{r.TreasureId}", "#F5F5F5", "#999"));
            root.Children.Add(VisHelper.Card(wp));
        }

        return root;
    }

    private static Control BuildHeroHeader(Recipe r)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {r.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(r, idRow);
        if (!string.IsNullOrWhiteSpace(r.Type))
            idRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"),
                Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = r.Type, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") }
            });
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var flags = new List<string>();
        if (r.Scrap) flags.Add("Scrap");
        if (r.Identify) flags.Add("Identify");
        if (r.DegradeOutput) flags.Add("DegradeOutput");
        if (r.TransferComponents) flags.Add("TransferComponents");
        if (flags.Count > 0)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"),
                Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = r.Subject ?? r.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(r.SecretName))
            identity.Children.Add(new TextBlock
            {
                Text = $"{VisHelper.Loc("Vis.Secret")}: {r.SecretName}", FontSize = 12, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#888")
            });
        var statRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock
            { Text = $"{VisHelper.Loc("Vis.Hours")}: {r.Hours:F1}", FontSize = 11, Foreground = Brush.Parse("#666") });
        statRow.Children.Add(new TextBlock
        {
            Text =
                $"{VisHelper.Loc("Vis.Reverse")}: {(r.Reverse > 0 ? VisHelper.Loc("Vis.Yes") : VisHelper.Loc("Vis.No"))}",
            FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = r.DegradeOutput
                ? $"{VisHelper.Loc("Vis.DegradeOutput")}: On"
                : $"{VisHelper.Loc("Vis.DegradeOutput")}: Off",
            FontSize = 11, Foreground = r.DegradeOutput ? Brush.Parse("#2E7D32") : Brush.Parse("#999")
        });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildIngredientsPanel(Recipe r)
    {
        var sp = new StackPanel();
        var itemProps = GenericDataGridHelper.GetEntities<ItemProp>();
        var hasAny = false;

        // ── Fieldset: legend overlaps the top border ──
        var fieldset = new Grid();

        // Content area with border (added first → behind legend)
        var contentStack = new StackPanel { Spacing = 10 };
        var contentBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderBrush = Brush.Parse("#18000000"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 18, 16, 12),
            Margin = new Thickness(0, 10, 0, 0),
            Child = contentStack
        };
        fieldset.Children.Add(contentBorder);

        // Legend title — floats above the border top (fieldset style)
        var legendContent = new Border
        {
            Background = Brush.Parse("#FAFAFA"),
            Padding = new Thickness(12, 4),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = VisHelper.Loc("Vis.Ingredients"),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#555")
            }
        };
        var legendBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(20, 0, 0, 0),
            Child = legendContent
        };
        fieldset.Children.Add(legendBorder);

        // Ctrl+Click on legend to expand/collapse the content
        var expanded = true;
        legendContent.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                expanded = !expanded;
                contentBorder.IsVisible = expanded;
            }
        };

        var pattern = ReferencePattern.FromName("{mult}x{id}");

        void AddGroup(string label, string raw, string propName, string bg, string fg)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            hasAny = true;

            // Group sub-heading
            contentStack.Children.Add(new TextBlock
            {
                Text = label, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse(fg), Margin = new Thickness(4, 0, 0, 0)
            });

            // Vertical list of ingredients, each in its own Card
            var list = new StackPanel { Spacing = 6 };

            foreach (var part in raw.Split('+'))
            {
                var seg = part.Trim();
                var ing = ReferenceResolver.Instance.LookupRef<Ingredient>(r, propName, seg);
                var extra = pattern.FormatExtraInfo(seg);
                // FormatExtraInfo returns "x{N}" for {mult}x{id} pattern — strip the "x" for quantity
                var qty = string.IsNullOrEmpty(extra) ? "1" : extra.TrimStart('x');

                var cardStack = new StackPanel { Spacing = 4 };

                // Row 1: type badge + name + quantity
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                if (ing is not null)
                {
                    var capturedIng = ing;
                    nameRow.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(3), Background = Brush.Parse(bg),
                        Padding = new Thickness(5, 1),
                        Child = new TextBlock { Text = "Ingredient", FontSize = 9, Foreground = Brush.Parse(fg) }
                    });
                    var nameBadge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Background = Brush.Parse("#0D000000"),
                        Padding = new Thickness(8, 3),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Child = new TextBlock { Text = ing.Name ?? seg, FontSize = 11, Foreground = Brush.Parse("#333") }
                    };
                    nameBadge.PointerPressed += (_, e) =>
                    {
                        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                            ReferenceResolver.Instance.NavigateTo(typeof(Ingredient), capturedIng.EntityId);
                    };
                    nameRow.Children.Add(nameBadge);
                }
                else
                {
                    nameRow.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(3), Background = Brush.Parse("#F5F5F5"),
                        Padding = new Thickness(5, 1),
                        Child = new TextBlock { Text = "?", FontSize = 9, Foreground = Brush.Parse("#999") }
                    });
                    nameRow.Children.Add(new TextBlock { Text = seg, FontSize = 11, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center });
                }
                // Quantity — only show when > 1
                if (qty != "1")
                    nameRow.Children.Add(new Border
                    {
                        CornerRadius = new CornerRadius(3),
                        Background = Brush.Parse("#08000000"),
                        Padding = new Thickness(6, 1),
                        Child = new TextBlock { Text = $"×{qty}", FontSize = 10, Foreground = Brush.Parse("#666") }
                    });
                cardStack.Children.Add(nameRow);

                // Row 2: Required / Forbidden property badges — 2-col, Ctrl+Clickable
                if (ing is not null && (!string.IsNullOrWhiteSpace(ing.RequiredProps) ||
                                        !string.IsNullOrWhiteSpace(ing.ForbidProps)))
                {
                    var propGrid = new Grid
                    {
                        ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star) },
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    propGrid.RowDefinitions.Add(new(GridLength.Auto));

                    if (!string.IsNullOrWhiteSpace(ing.RequiredProps))
                    {
                        var reqStack = new StackPanel { Spacing = 2 };
                        reqStack.Children.Add(new TextBlock { Text = VisHelper.Loc("Vis.Required"), FontSize = 9, Foreground = Brush.Parse("#2E7D32") });
                        var reqWp = new WrapPanel();
                        foreach (var pid in ing.RequiredProps.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0))
                        {
                            // Use unified LookupRef first — handles int IDs, prefixed IDs, MergedIds correctly
                            var prop = ReferenceResolver.Instance.LookupRef<ItemProp>(ing, nameof(Ingredient.RequiredProps), pid);
                            if (prop is null && int.TryParse(pid, out var pidi))
                            {
                                // Fallback: dictionary lookup by business key (Id)
                                itemProps.TryGetValue(pidi, out prop);
                            }
                            var propName2 = prop?.PropertyName ?? $"#{pid}";
                            reqWp.Children.Add(VisHelper.MiniBadge(propName2, "#E8F5E9", "#2E7D32",
                                prop is not null ? () => ReferenceResolver.Instance.NavigateTo(typeof(ItemProp), prop.EntityId) : null));
                        }
                        reqStack.Children.Add(reqWp);
                        Grid.SetColumn(reqStack, 0);
                        propGrid.Children.Add(reqStack);
                    }

                    if (!string.IsNullOrWhiteSpace(ing.ForbidProps))
                    {
                        var forbStack = new StackPanel { Spacing = 2 };
                        forbStack.Children.Add(new TextBlock { Text = VisHelper.Loc("Vis.Forbidden"), FontSize = 9, Foreground = Brush.Parse("#C62828") });
                        var forbWp = new WrapPanel();
                        foreach (var pid in ing.ForbidProps.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0))
                        {
                            // Use unified LookupRef first — handles int IDs, prefixed IDs, MergedIds correctly
                            var prop = ReferenceResolver.Instance.LookupRef<ItemProp>(ing, nameof(Ingredient.ForbidProps), pid);
                            if (prop is null && int.TryParse(pid, out var pidi))
                            {
                                // Fallback: dictionary lookup by business key (Id)
                                itemProps.TryGetValue(pidi, out prop);
                            }
                            var propName2 = prop?.PropertyName ?? $"#{pid}";
                            forbWp.Children.Add(VisHelper.MiniBadge(propName2, "#FFEBEE", "#C62828",
                                prop is not null ? () => ReferenceResolver.Instance.NavigateTo(typeof(ItemProp), prop.EntityId) : null));
                        }
                        forbStack.Children.Add(forbWp);
                        Grid.SetColumn(forbStack, 1);
                        propGrid.Children.Add(forbStack);
                    }

                    cardStack.Children.Add(propGrid);
                }

                list.Children.Add(VisHelper.Card(cardStack));
            }

            contentStack.Children.Add(list);
        }

        AddGroup(VisHelper.Loc("Vis.Tools"), r.Tools, nameof(Recipe.Tools), "#FFF3E0", "#E65100");
        AddGroup(VisHelper.Loc("Vis.Consumed"), r.Consumed, nameof(Recipe.Consumed), "#FFEBEE", "#C62828");
        AddGroup("Destroyed", r.Destroyed, nameof(Recipe.Destroyed), "#FCE4EC", "#880E4F");

        if (!hasAny)
            sp.Children.Add(
                new TextBlock { Text = "(No ingredients)", FontSize = 11, Foreground = Brush.Parse("#999") });
        else
            sp.Children.Add(fieldset);

        return sp;
    }

    private static Control BuildProductPanel(Recipe r)
    {
        var sp = new StackPanel();
        var wp = new WrapPanel();
        var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(r, nameof(Recipe.TreasureId), r.TreasureId);
        if (tt is not null)
        {
            wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32",
                () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            if (!string.IsNullOrWhiteSpace(tt.Treasures))
            {
                var itemTypes =
                    GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);
                foreach (var seg in tt.Treasures.Split(',').Take(6))
                {
                    var parts = seg.Trim().Split('x');
                    if (parts.Length < 2) continue;
                    var itemId = parts[0];
                    var it = itemTypes.GetValueOrDefault(itemId);
                    if (it is not null)
                        wp.Children.Add(VisHelper.MiniBadge(it.Description, "#E0F2F1", "#00695C",
                            () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), it.EntityId)));
                }
            }
        }
        else
            wp.Children.Add(VisHelper.MiniBadge($"TT #{r.TreasureId}", "#F5F5F5", "#999"));

        sp.Children.Add(VisHelper.Card(wp, VisHelper.Loc("Vis.Loot")));

        if (r.TempTreasureId != "3" && r.TempTreasureId != r.TreasureId)
        {
            var wp2 = new WrapPanel();
            var tmpTt = ReferenceResolver.Instance.LookupRef<TreasureTable>(r, nameof(Recipe.TempTreasureId),
                r.TempTreasureId);
            if (tmpTt is not null)
                wp2.Children.Add(VisHelper.MiniBadge(tmpTt.Name, "#E3F2FD", "#1565C0",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tmpTt.EntityId)));
            else
                wp2.Children.Add(VisHelper.MiniBadge($"TT #{r.TempTreasureId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp2, "Temp Product Preview"));
        }

        return sp;
    }

    private static Control BuildAlsoTryPanel(Recipe r)
    {
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

        return VisHelper.Card(wp, "Also Try (Alternative Recipes)");
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(tt), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(tt));
        if (!string.IsNullOrWhiteSpace(tt.Treasures))
        {
            root.Children.Add(BuildLootPanel(tt));
        }
        else
        {
            root.Children.Add(VisHelper.Card(new TextBlock
                { Text = VisHelper.Loc("Vis.Empty"), FontSize = 11, Foreground = Brush.Parse("#999") }));
        }

        root.Children.Add(BuildReverseRefsPanel(tt));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not TreasureTable tt) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock
        {
            Text = tt.Subject ?? tt.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        var flags = new List<string>();
        if (tt.Nested) flags.Add(VisHelper.Loc("Vis.Nested"));
        if (tt.Suppress) flags.Add(VisHelper.Loc("Vis.Suppress"));
        if (tt.Identify) flags.Add(VisHelper.Loc("Vis.Identify"));
        if (flags.Count > 0)
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                    { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });

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

            // Preview first few items
            var itemTypes = GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);
            var wp = new WrapPanel();
            foreach (var seg in tt.Treasures.Split(',').Take(5))
            {
                var parts = seg.Trim().Split('x');
                if (parts.Length < 2) continue;
                var itemId = parts[0];
                var it = itemTypes.GetValueOrDefault(itemId);
                if (it is not null)
                    wp.Children.Add(VisHelper.MiniBadge(it.Description, "#E0F2F1", "#00695C"));
            }

            if (wp.Children.Count > 0)
                root.Children.Add(VisHelper.Card(wp));
        }

        return root;
    }

    private static Control BuildHeroHeader(TreasureTable tt)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {tt.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(tt, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var flags = new List<string>();
        if (tt.Nested) flags.Add(VisHelper.Loc("Vis.Nested"));
        if (tt.Suppress) flags.Add(VisHelper.Loc("Vis.Suppress"));
        if (tt.Identify) flags.Add(VisHelper.Loc("Vis.Identify"));
        if (flags.Count > 0)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = string.Join(" · ", flags), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = tt.Subject ?? tt.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildLootPanel(TreasureTable tt)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Loot")));
        Serilog.Log.Logger.Information(
            "[TT:BuildLootPanel] TT name={Name} eid={Eid} modId={ModId} ns={Ns} nsRaw={NsRaw} Treasures={Treasures}",
            tt.Name, tt.EntityId, tt.ModId,
            Helper.ReferenceParser.NormalizeNamespace(
                Helper.GenericDataGridHelper.EntityNamespaces.TryGetValue(tt.EntityId, out var tns) ? tns : null),
            Helper.GenericDataGridHelper.EntityNamespaces.TryGetValue(tt.EntityId, out var tnsr) ? tnsr : "(none)",
            tt.Treasures);
        var itemTypes = GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);

        // Flatten all loot entries —  |  and  ,  are both item-level separators.
        // Probability must be computed across ALL items in this TT, not per |  group.
        var allSegs = tt.Treasures.Split('|', ',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s.Contains('x'))
            .ToList();

        var allParsed = new List<(string itemId, double weight, string qtyRange)>();
        double totalWeight = 0;
        foreach (var seg in allSegs)
        {
            var parts = seg.Split('x');
            if (parts.Length < 2) continue;
            var itemId = parts[0].Trim();
            var weightStr = parts[1].Trim();
            var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
            var weight = double.TryParse(weightStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 1.0;
            totalWeight += weight;
            allParsed.Add((itemId, weight, qtyRange));
        }

        var cardStack = new StackPanel { Spacing = 6 };

        foreach (var (itemId, weight, qtyRange) in allParsed)
        {
            var actualProb = totalWeight > 0 ? weight / totalWeight : 1.0 / allParsed.Count;

            if (itemTypes.TryGetValue(itemId, out var matched))
            {
                var itemRow = BuildItemRow(matched.Description, "ItemType", "#E0F2F1", "#00695C",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), matched.EntityId),
                    weight, actualProb, qtyRange);
                cardStack.Children.Add(itemRow);
            }
            else
            {
                var nested = ReferenceResolver.Instance.LookupRef<TreasureTable>(tt,
                    nameof(TreasureTable.Treasures), itemId);
                if (nested is not null)
                {
                    Serilog.Log.Logger.Information(
                        "[TT:BuildLootPanel] NestedTT found: rawId={RawId} → name={Name} eid={Eid} modId={ModId} ns={Ns}",
                        itemId, nested.Name, nested.EntityId, nested.ModId,
                        Helper.ReferenceParser.NormalizeNamespace(
                            Helper.GenericDataGridHelper.EntityNamespaces.TryGetValue(nested.EntityId, out var nns) ? nns : null));
                    var nestedHeader = BuildItemRow(nested.Name, "TT", "#E8EAF6", "#283593",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), nested.EntityId),
                        weight, actualProb, qtyRange);

                    // Recursively expand nested TT contents (indented, collapsible via click on parent row)
                    var nestedItems = BuildNestedItems(nested, itemTypes, 1);
                    if (nestedItems is not null)
                    {
                        var isExpanded = true;
                        nestedItems.IsVisible = true;
                        nestedHeader.Cursor = new Cursor(StandardCursorType.Hand);
                        nestedHeader.PointerPressed += (_, e) =>
                        {
                            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
                            {
                                isExpanded = !isExpanded;
                                nestedItems.IsVisible = isExpanded;
                            }
                            // Ctrl+click is handled by MiniBadge inside the row
                        };
                    }
                    cardStack.Children.Add(nestedHeader);
                    if (nestedItems is not null)
                        cardStack.Children.Add(nestedItems);
                }
                else
                {
                    Serilog.Log.Logger.Information(
                        "[TT:BuildLootPanel] NestedTT NOT found: rawId={RawId} — showing as unknown",
                        itemId);
                    var unknownRow = BuildItemRow(itemId, null, "#F5F5F5", "#999", null,
                        weight, actualProb, qtyRange);
                    cardStack.Children.Add(unknownRow);
                }
            }
        }

        sp.Children.Add(VisHelper.Card(cardStack));

        return sp;
    }

    internal static Control BuildItemRow(string name, string? typeTag, string typeBg, string typeFg,
        Action? nav, double weight, double actualProb, string qtyRange)
    {
        // Full-width row: name(left, 1*) | prob bar(mid, Auto) | qty(right, 44px)
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new(1, GridUnitType.Star),
                new(GridLength.Auto),
                new(44, GridUnitType.Pixel)
            },
            Margin = new Thickness(0, 3)
        };

        // Left: item name badge (color-coded by type)
        var leftStack = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        leftStack.Children.Add(VisHelper.MiniBadge(name, typeTag is not null ? typeBg : "#F5F5F5",
            typeTag is not null ? typeFg : "#999", nav));
        Grid.SetColumn(leftStack, 0);
        row.Children.Add(leftStack);

        // Mid: weight(probability%) with gradient red(0%)→green(100%) based on actualProb
        var t = Math.Clamp(actualProb, 0.0, 1.0);
        var r = (byte)(t < 0.5 ? 198 + (int)(t * 2 * 57) : 255 - (int)((t - 0.5) * 2 * 57));
        var g = (byte)(t < 0.5 ? (int)(t * 2 * 140) : 140 + (int)((t - 0.5) * 2 * 46));
        var bb = (byte)(t < 0.5 ? 40 + (int)(t * 2 * 10) : 50 - (int)((t - 0.5) * 2 * 10));
        var gradientColor = $"#{r:X2}{g:X2}{bb:X2}";
        var probBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse($"#0A{r:X2}{g:X2}{bb:X2}"),
            Padding = new Thickness(7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(8, 0),
            Child = new TextBlock
            {
                Text = $"{weight:F4}({actualProb:P2})", FontSize = 10, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse(gradientColor),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetColumn(probBadge, 1);
        row.Children.Add(probBadge);

        // Right: quantity
        var qtyTb = new TextBlock
        {
            Text = qtyRange != "1" ? $"×{qtyRange}" : "",
            FontSize = 10,
            Foreground = Brush.Parse("#888"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(4, 0, 0, 0)
        };
        Grid.SetColumn(qtyTb, 2);
        row.Children.Add(qtyTb);

        return row;
    }

    internal static Control? BuildNestedItems(TreasureTable tt,
        Dictionary<string, ItemType> itemTypes,
        int depth)
    {
        if (depth > 3 || string.IsNullOrWhiteSpace(tt.Treasures)) return null;

        Serilog.Log.Logger.Information(
            "[TT:BuildNestedItems] depth={Depth} TT name={Name} eid={Eid} modId={ModId} ns={Ns} Treasures={Treasures}",
            depth, tt.Name, tt.EntityId, tt.ModId,
            Helper.ReferenceParser.NormalizeNamespace(
                Helper.GenericDataGridHelper.EntityNamespaces.TryGetValue(tt.EntityId, out var tnns) ? tnns : null),
            tt.Treasures);

        var contentPanel = new StackPanel { Spacing = 3, Margin = new Thickness(20, 2, 0, 4) };

        // Flatten all loot entries —  |  and  ,  are both item-level separators.
        // Probability must be computed across ALL items in this TT, not per |  group.
        var allSegs = tt.Treasures.Split('|', ',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s.Contains('x'))
            .ToList();

        var allParsed = new List<(string itemId, double weight, string qtyRange)>();
        double totalWeight = 0;
        foreach (var seg in allSegs)
        {
            var parts = seg.Split('x');
            if (parts.Length < 2) continue;
            var itemId = parts[0].Trim();
            var weightStr = parts[1].Trim();
            var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
            var weight = double.TryParse(weightStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 1.0;
            totalWeight += weight;
            allParsed.Add((itemId, weight, qtyRange));
        }

        foreach (var (itemId, weight, qtyRange) in allParsed)
        {
            // Nested TT probability: independently calculated within this TT (weight / totalWeight)
            // NOT chained with parent's probability
            var innerProb = totalWeight > 0 ? weight / totalWeight : 1.0 / allParsed.Count;
            var actualProb = innerProb;
            Serilog.Log.Logger.Information(
                "[TT:Nested] depth={Depth} item={ItemId} weight={Weight} totalWeight={TotalWeight} prob={Prob:P2}",
                depth, itemId, weight, totalWeight, actualProb);

            if (itemTypes.TryGetValue(itemId, out var matched))
            {
                var row = BuildItemRow(matched.Description, null, "#E0F2F1", "#00695C",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), matched.EntityId),
                    weight, actualProb, qtyRange);
                contentPanel.Children.Add(row);
            }
            else
            {
                var nestedTt = ReferenceResolver.Instance.LookupRef<TreasureTable>(tt,
                    nameof(TreasureTable.Treasures), itemId);
                if (nestedTt is not null)
                {
                    var row = BuildItemRow(nestedTt.Name, "TT", "#E8EAF6", "#283593",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), nestedTt.EntityId),
                        weight, actualProb, qtyRange);
                    var sub = BuildNestedItems(nestedTt, itemTypes, depth + 1);
                    if (sub is not null)
                    {
                        var isExpanded = true;
                        sub.IsVisible = true;
                        row.Cursor = new Cursor(StandardCursorType.Hand);
                        row.PointerPressed += (_, e) =>
                        {
                            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
                            {
                                isExpanded = !isExpanded;
                                sub.IsVisible = isExpanded;
                            }
                        };
                    }
                    contentPanel.Children.Add(row);
                    if (sub is not null) contentPanel.Children.Add(sub);
                }
                else
                {
                    var row = BuildItemRow(itemId, null, "#F5F5F5", "#999", null,
                        weight, actualProb, qtyRange);
                    contentPanel.Children.Add(row);
                }
            }
        }

        if (contentPanel.Children.Count == 0) return null;

        return contentPanel;
    }

    private static Control BuildReverseRefsPanel(TreasureTable tt)
        => VisHelper.BuildReverseRefsPanel(tt.EntityId);
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(enc), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(enc));
        if (!string.IsNullOrWhiteSpace(enc.Description))
            root.Children.Add(BuildStoryPanel(enc));
        if (!string.IsNullOrWhiteSpace(enc.Responses))
        {
            root.Children.Add(BuildStoryBranchDiagram(enc));
            root.Children.Add(BuildResponsesPanel(enc));
        }

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
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 }
            });
        root.Children.Add(imgStack);

        var typeLabel = enc.Type == EncounterType.Scavenge ? "Scavenge" : "Normal";
        var typeBg = enc.Type == EncounterType.Scavenge ? "#FFF3E0" : "#E3F2FD";
        var typeFg = enc.Type == EncounterType.Scavenge ? "#E65100" : "#1565C0";
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
                { Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });
        root.Children.Add(new TextBlock
        {
            Text = enc.Subject ?? enc.Name, FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
        });

        if (!string.IsNullOrWhiteSpace(enc.Description))
        {
            var desc = enc.Description.Length > 150 ? enc.Description[..150] + "..." : enc.Description;
            root.Children.Add(new TextBlock
            {
                Text = desc, FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888"),
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
            });
        }

        var statRows = new List<(string, string, string?)>();
        if (enc.Price != 0) statRows.Add((VisHelper.Loc("Vis.Price"), $"${enc.Price:F2}", null));
        statRows.Add((VisHelper.Loc("Vis.Type"), enc.Type.ToString(), null));
        if (enc.LootChance > 0) statRows.Add((VisHelper.Loc("Vis.LootChance"), $"{enc.LootChance:P0}", null));
        if (enc.AccidentChance > 0)
            statRows.Add((VisHelper.Loc("Vis.Accident"), $"{enc.AccidentChance:P0}", "#C62828"));
        if (enc.CreatureId != "0") statRows.Add((VisHelper.Loc("Vis.CreatureRef"), $"#{enc.CreatureId}", null));
        if (statRows.Count > 0)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
            root.Children.Add(VisHelper.BuildStatCard(statRows));
        }

        return root;
    }

    private static Control BuildHeroHeader(Encounter enc)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = VisHelper.LoadImage(enc.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.BookOpen, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {enc.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(enc, idRow);
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var typeLabel = enc.Type == EncounterType.Scavenge ? "Scavenge" : "Normal";
        var typeBg = enc.Type == EncounterType.Scavenge ? "#FFF3E0" : "#E3F2FD";
        var typeFg = enc.Type == EncounterType.Scavenge ? "#E65100" : "#1565C0";
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });
        if (enc.RemoveCreatures)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "RemoveCreatures", FontSize = 10, Foreground = Brush.Parse("#C62828") }
            });
        if (enc.RemoveUsed)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "RemoveUsed", FontSize = 10, Foreground = Brush.Parse("#C62828") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = enc.Subject ?? enc.Name, FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        var chanceRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        if (enc.Price != 0)
            chanceRow.Children.Add(new TextBlock
                { Text = $"Price: ${enc.Price:F2}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (enc.LootChance > 0)
            chanceRow.Children.Add(new TextBlock
                { Text = $"Loot: {enc.LootChance:P0}", FontSize = 11, Foreground = Brush.Parse("#2E7D32") });
        if (enc.AccidentChance > 0)
            chanceRow.Children.Add(new TextBlock
                { Text = $"Accident: {enc.AccidentChance:P0}", FontSize = 11, Foreground = Brush.Parse("#C62828") });
        if (chanceRow.Children.Count > 0) identity.Children.Add(chanceRow);
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);

        return VisHelper.Card(grid);
    }

    private static Control BuildStoryPanel(Encounter enc)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.StoryText")));
        var desc = enc.Description.Length > 2000 ? enc.Description[..2000] + "..." : enc.Description;
        sp.Children.Add(VisHelper.Card(new TextBlock
            { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
        return sp;
    }

    // Response entry: optional item prefix + target encounter
    private sealed record ResponseEntry(
        string? ItemId, double ItemMult, ItemType? Item,
        int TargetId, double Weight, double Probability, Encounter? TargetEncounter);

    private static Control BuildResponsesPanel(Encounter enc)
    {
        var sp = new StackPanel();
        var responseList = ParseResponseEntries(enc.Responses, enc);
        sp.Children.Add(VisHelper.SectionLabel(
            $"{VisHelper.Loc("Vis.Responses")} ({responseList.Count} {(responseList.Count > 1 ? VisHelper.Loc("Vis.Options") : VisHelper.Loc("Vis.Option"))})"));

        // Response format hint (from Comment attribute)
        sp.Children.Add(new TextBlock
        {
            Text = "格式: [物品ID]x[数量]=[剧情ID]x[权重]  ·  空物品(=开头)=无需物品的选项  ·  概率=权重/权重和",
            FontSize = 9, Foreground = Brush.Parse("#AAA"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, -4, 0, 4)
        });

        if (responseList.Count == 0)
        {
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = VisHelper.Loc("Vis.NoResponses"), FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        var cardStack = new StackPanel { Spacing = 8 };
        foreach (var resp in responseList)
        {
            var row = new StackPanel { Spacing = 4 };

            // Row 1: item usage hint (if applicable)
            if (resp.Item is not null)
            {
                var itemRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                itemRow.Children.Add(new TextBlock
                {
                    Text = "使用物品:", FontSize = 9, Foreground = Brush.Parse("#888"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                var qtyText = resp.ItemMult > 1 ? $" ×{resp.ItemMult}" : "";
                itemRow.Children.Add(VisHelper.MiniBadge(
                    $"{resp.Item.Description}{qtyText}", "#E3F2FD", "#1565C0",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), resp.Item.EntityId)));
                itemRow.Children.Add(new TextBlock
                {
                    Text = "→ 触发:", FontSize = 9, Foreground = Brush.Parse("#888"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(itemRow);
            }
            else if (resp.ItemId is not null)
            {
                var itemRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                itemRow.Children.Add(new TextBlock
                {
                    Text = $"使用物品 #{resp.ItemId}", FontSize = 9, Foreground = Brush.Parse("#999"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                if (resp.ItemMult > 1)
                    itemRow.Children.Add(new TextBlock
                    {
                        Text = $"×{resp.ItemMult}", FontSize = 9, Foreground = Brush.Parse("#999"),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                itemRow.Children.Add(new TextBlock
                {
                    Text = "→", FontSize = 9, Foreground = Brush.Parse("#888"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(itemRow);
            }

            // Row 2: target encounter + probability bar
            var targetRow = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star), new(100, GridUnitType.Pixel) } };

            var leftStack = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            if (resp.TargetEncounter is not null)
            {
                leftStack.Children.Add(VisHelper.MiniBadge(
                    resp.TargetEncounter.Subject,
                    "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), resp.TargetEncounter.EntityId)));
                if (resp.TargetEncounter.Type == EncounterType.Scavenge)
                    leftStack.Children.Add(VisHelper.MiniBadge("Scavenge", "#FFF3E0", "#E65100"));
            }
            else
                leftStack.Children.Add(VisHelper.MiniBadge($"Enc #{resp.TargetId}", "#F5F5F5", "#999"));

            Grid.SetColumn(leftStack, 0);
            targetRow.Children.Add(leftStack);

            // Right: probability bar from calculated probability
            var probPct = Math.Clamp(resp.Probability, 0.0, 1.0);
            var probColor = probPct >= 0.5 ? "#2E7D32" : probPct >= 0.1 ? "#E65100" : "#999";
            var probBar = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background = Brush.Parse(probColor),
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"{resp.Weight:F1}({resp.Probability:P2})",
                    FontSize = 9,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(6, 0)
                },
                Width = Math.Max(probPct * 100, 50)
            };
            Grid.SetColumn(probBar, 1);
            targetRow.Children.Add(probBar);

            row.Children.Add(targetRow);
            cardStack.Children.Add(row);
        }

        sp.Children.Add(VisHelper.Card(cardStack));
        return sp;
    }

    private static List<ResponseEntry> ParseResponseEntries(string raw, Encounter sourceEnc)
    {
        var result = new List<ResponseEntry>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        // Build itemTypes lookup for item prefix resolution
        var itemTypes = new Dictionary<string, ItemType>();
        try
        {
            itemTypes = GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", sourceEnc.ModId);
        }
        catch { /* ignore if not available */ }

        // Format: [itemId]x[mult]=[encounterId]x[weight]x0x0x0
        //   or just: =[encounterId]x[weight]x0x0x0  (no item needed)
        // weight is used to calculate probability: thisWeight / sumOfAllWeights
        var rawEntries = new List<(string? itemId, double itemMult, ItemType? item, int targetId, double weight, Encounter? targetEnc)>();
        double totalWeight = 0;

        foreach (var seg in raw.Split(','))
        {
            var s = seg.Trim();
            if (s.Length == 0) continue;

            string? itemId = null;
            double itemMult = 1.0;
            ItemType? item = null;
            int targetId;
            double weight = 1.0;

            var eqIdx = s.IndexOf('=');
            if (eqIdx < 0)
            {
                // No '=' => treat whole thing as encounter reference for backward compat
                var parts = s.Split('x');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out targetId)) continue;
                weight = double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p1) ? p1 : 1.0;
            }
            else
            {
                // Parse optional item prefix (before '=')
                if (eqIdx > 0)
                {
                    var itemPart = s[..eqIdx].Trim();
                    if (itemPart.EndsWith('x')) itemPart = itemPart[..^1];
                    var itemParts = itemPart.Split('x');
                    if (itemParts.Length >= 1)
                    {
                        itemId = itemParts[0].Trim();
                        if (!string.IsNullOrEmpty(itemId) && !int.TryParse(itemId, out _))
                        {
                            // itemId is like "90.3" or "87.1"
                            if (itemParts.Length >= 2)
                            {
                                itemMult = double.TryParse(itemParts[1], System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var im) ? im : 1.0;
                            }

                            if (itemTypes.TryGetValue(itemId, out var found))
                                item = found;
                        }
                    }
                }

                // Parse encounter suffix (after '=')
                var encPart = s[(eqIdx + 1)..].Trim();
                var encParts = encPart.Split('x');
                if (encParts.Length < 2) continue;
                if (!int.TryParse(encParts[0], out targetId)) continue;
                // encParts[1] is the weight (not direct probability)
                weight = double.TryParse(encParts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p2) ? p2 : 1.0;
            }

            totalWeight += weight;
            Encounter? targetEnc =
                ReferenceResolver.Instance.LookupRef<Encounter>(sourceEnc, nameof(Encounter.Responses),
                    targetId.ToString());

            rawEntries.Add((itemId, itemMult, item, targetId, weight, targetEnc));
        }

        // Calculate probability from weights
        foreach (var (itemId, itemMult, item, targetId, weight, targetEnc) in rawEntries)
        {
            var prob = totalWeight > 0 ? weight / totalWeight : 1.0 / rawEntries.Count;
            result.Add(new ResponseEntry(itemId, itemMult, item, targetId, weight, prob, targetEnc));
        }

        return result;
    }

    // ═══════════════ Story Branch Diagram (Mermaid-style) ═══════════════

    private static Control BuildStoryBranchDiagram(Encounter enc)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.StoryBranch")));

        var responseList = ParseResponseEntries(enc.Responses, enc);
        if (responseList.Count == 0)
        {
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = VisHelper.Loc("Vis.NoBranches"), FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        // ── Collect all unique PreConditions for checkbox filtering ──
        var allPreConds = new List<(string RawId, string Display, bool IsNeg)>();
        var seenPre = new HashSet<string>();
        void AddPreConds(string? preStr, Encounter ctx)
        {
            if (string.IsNullOrWhiteSpace(preStr)) return;
            foreach (var seg in preStr.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var isNeg = seg.StartsWith("-");
                var rawId = isNeg ? seg[1..] : seg;
                if (!seenPre.Add(rawId)) continue;
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(ctx, nameof(Encounter.PreConditions), seg);
                allPreConds.Add((rawId, cond?.Subject ?? rawId, isNeg));
            }
        }
        // Only collect pre-conditions of NEXT encounters (not current step)
        foreach (var resp in responseList)
        {
            if (resp.TargetEncounter is not null)
                AddPreConds(resp.TargetEncounter.PreConditions, resp.TargetEncounter);
        }

        // ── Collect reverse references (previous encounters → current) ──
        // Scan Encounter Responses directly (not indexed via ReferenceField)
        var reverseRefs = new List<(Encounter Src, string? ItemDesc, double ItemMult, double Weight)>();
        var revSeen = new HashSet<string>();
        {
            if (GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Encounter), out var allEncs) && allEncs is not null)
            {
                var itemTypes = new Dictionary<string, ItemType>();
                try { itemTypes = GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", enc.ModId); }
                catch { }
                foreach (var obj in allEncs)
                {
                    if (obj is not Encounter parentEnc || parentEnc.EntityId == enc.EntityId) continue;
                    if (string.IsNullOrWhiteSpace(parentEnc.Responses)) continue;
                    foreach (var seg in parentEnc.Responses.Split(','))
                    {
                        var s = seg.Trim();
                        if (s.Length == 0) continue;
                        var eqIdx = s.IndexOf('=');
                        if (eqIdx < 0) continue;
                        // Parse item part (before =)
                        string? itemDesc = null;
                        double itemMult = 1.0;
                        if (eqIdx > 0)
                        {
                            var itemPart = s[..eqIdx].Trim();
                            var itemParts = itemPart.Split('x');
                            var itemIdRaw = itemParts[0].Trim();
                            if (!string.IsNullOrEmpty(itemIdRaw) && !int.TryParse(itemIdRaw, out _))
                            {
                                if (itemParts.Length >= 2)
                                    double.TryParse(itemParts[1], System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out itemMult);
                                itemDesc = itemTypes.TryGetValue(itemIdRaw, out var fi) ? fi.Description : itemIdRaw;
                            }
                        }
                        // Parse encounter target (after =)
                        var encPart = s[(eqIdx + 1)..].Trim();
                        var encParts = encPart.Split('x');
                        if (encParts.Length < 2) continue;
                        if (!int.TryParse(encParts[0], out var targetId) || targetId != enc.Id) continue;
                        double weight = double.TryParse(encParts[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 1.0;
                        var key = $"{parentEnc.EntityId}|{s}";
                        if (!revSeen.Add(key)) continue;
                        reverseRefs.Add((parentEnc, itemDesc, itemMult, weight));
                    }
                }
            }
        }

        // Shared state for preCondition checkbox → Mermaid refresh
        var selectedPreConds = new HashSet<string>();

        // ── Helper: check if a single preCondition is satisfied (handles Y/N polarity) ──
        bool IsPreCondSatisfied(string preStr, HashSet<string> activeSet)
        {
            if (activeSet.Count == 0) return true; // no active filter — all conditions considered satisfied
            var isNeg = preStr.StartsWith("-");
            var rid = isNeg ? preStr[1..] : preStr;
            // Positive preCond "5": satisfied if checkbox IS checked (player has condition)
            // Negative preCond "-5": satisfied if checkbox is NOT checked (player does NOT have condition)
            return isNeg ? !activeSet.Contains(rid) : activeSet.Contains(rid);
        }

        // ── Helper: check if ALL target encounter's preConditions are satisfied ──
        bool AreAllPreCondsSatisfied(Encounter? target, HashSet<string> activeSet)
        {
            if (target is null || string.IsNullOrWhiteSpace(target.PreConditions)) return true;
            var pres = target.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
            return pres.All(p => IsPreCondSatisfied(p, activeSet));
        }

        // ── Helper: build context label (treasure, creature, pre) ──
        static string BuildCtxLabel(Encounter e)
        {
            var ctx = "";
            if (!string.IsNullOrWhiteSpace(e.TreasureId) && e.TreasureId != "3")
            {
                var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(e, nameof(Encounter.TreasureId), e.TreasureId);
                if (tt is not null) ctx += $"🎒{tt.Name} ";
            }
            if (e.CreatureId != "0") ctx += "🐾 ";
            if (!string.IsNullOrWhiteSpace(e.PreConditions))
            {
                var preCount = e.PreConditions.Split(',').Length;
                ctx += $"📋pre:{preCount} ";
            }
            return ctx;
        }

        // ── Mermaid text builder ──
        string BuildMermaid()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("flowchart LR");

            // ── Reverse refs: previous encounters → current ──
            var seenRev = new HashSet<int>();
            int revIdx = 0;
            foreach (var (src, itemDesc, itemMult, weight) in reverseRefs)
            {
                if (!seenRev.Add(src.Id)) continue;
                var revNodeId = $"R{revIdx}";
                var revName = (src.Subject ?? $"Enc #{src.Id}").Replace("\"", "\\\"");
                var viaLabel = itemDesc is not null
                    ? $"{itemDesc}{(itemMult > 1 ? $" x{itemMult}" : "")} | {weight:F1}"
                    : $"{weight:F1}";
                sb.AppendLine($"    {revNodeId}[\"← {revName}\"]");
                sb.AppendLine($"    {revNodeId} -->|\"{viaLabel}\"| A");
                revIdx++;
            }

            // ── Current node ──
            var currentCtx = BuildCtxLabel(enc);
            var currentName = (enc.Subject ?? $"Enc #{enc.Id}").Replace("\"", "\\\"");
            var currentLabel = string.IsNullOrEmpty(currentCtx)
                ? $"📍 {currentName}"
                : $"📍 {currentName}<br/>{currentCtx.Trim()}";
            sb.AppendLine($"    A[\"{currentLabel}\"]");

            // Calculate effective probability: only count valid branches (Y/N matching)
            double validTotalWeight = 0;
            foreach (var r in responseList)
            {
                if (AreAllPreCondsSatisfied(r.TargetEncounter, selectedPreConds))
                    validTotalWeight += r.Weight;
            }

            // ── Forward edges ──
            for (int i = 0; i < responseList.Count; i++)
            {
                var resp = responseList[i];
                var nodeId = (char)('B' + i);
                var targetCtx = resp.TargetEncounter is not null ? BuildCtxLabel(resp.TargetEncounter) : "";
                var targetName = (resp.TargetEncounter?.Subject ?? $"Enc #{resp.TargetId}").Replace("\"", "\\\"");
                var targetLabel = string.IsNullOrEmpty(targetCtx) ? targetName : $"{targetName}<br/>{targetCtx.Trim()}";

                // PreCondition match info for edge label (respects Y/N polarity)
                var matchInfo = "";
                if (resp.TargetEncounter is not null && !string.IsNullOrWhiteSpace(resp.TargetEncounter.PreConditions) && selectedPreConds.Count > 0)
                {
                    var targetPres = resp.TargetEncounter.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    int matched = 0;
                    foreach (var tp in targetPres)
                    {
                        if (IsPreCondSatisfied(tp, selectedPreConds)) matched++;
                    }
                    if (matched < targetPres.Count)
                        matchInfo = $" ⚠{matched}/{targetPres.Count}";
                }

                var isBranchValid = AreAllPreCondsSatisfied(resp.TargetEncounter, selectedPreConds);
                var effectiveProb = validTotalWeight > 0 && isBranchValid ? resp.Weight / validTotalWeight : 0.0;

                var edgeLabel = resp.Item is not null
                    ? $"{resp.Item.Description}{(resp.ItemMult > 1 ? $" x{resp.ItemMult}" : "")} | {resp.Weight:F1}({effectiveProb:P2}){matchInfo}"
                    : resp.ItemId is not null
                        ? $"#{resp.ItemId}{(resp.ItemMult > 1 ? $" x{resp.ItemMult}" : "")} | {resp.Weight:F1}({effectiveProb:P2}){matchInfo}"
                        : $"{resp.Weight:F1}({effectiveProb:P2}){matchInfo}";
                edgeLabel = edgeLabel.Replace("\"", "'");
                sb.AppendLine($"    A -->|\"{edgeLabel}\"| {nodeId}[\"{targetLabel}\"]");
            }

            return sb.ToString();
        }

        // ── Mermaid display block ──
        var mermaidTextBlock = new TextBlock
        {
            Text = BuildMermaid(), FontSize = 10, FontFamily = new FontFamily("Consolas, Menlo, monospace"),
            Foreground = Brush.Parse("#555"), TextWrapping = TextWrapping.NoWrap
        };
        var mermaidBlock = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#FAFAFA"),
            BorderBrush = Brush.Parse("#E0E0E0"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = mermaidTextBlock
            }
        };

        // ── PreCondition checkbox panel ──
        var preCondPanel = new StackPanel();
        var branchesPanel = new StackPanel
            { Orientation = Orientation.Vertical, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        if (allPreConds.Count > 0)
        {
            preCondPanel.Children.Add(new TextBlock
            {
                Text = VisHelper.Loc("Vis.PreConditions"), FontSize = 9, Foreground = Brush.Parse("#999"),
                FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4)
            });
            var cbPanel = new WrapPanel();
            foreach (var (rawId, display, isNeg) in allPreConds)
            {
                // Show negative precond with strikethrough style instead of "NOT " prefix
                var cbContent = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 4 };
                if (isNeg)
                {
                    cbContent.Children.Add(new TextBlock
                        { Text = "¬", FontSize = 9, Foreground = Brush.Parse("#C62828"),
                          VerticalAlignment = VerticalAlignment.Center });
                }
                cbContent.Children.Add(new TextBlock
                {
                    Text = display, FontSize = 10,
                    TextDecorations = isNeg ? TextDecorations.Strikethrough : null,
                    Foreground = isNeg ? Brush.Parse("#888") : Brush.Parse("#333")
                });
                var cb = new CheckBox
                {
                    Content = cbContent,
                    FontSize = 10, IsChecked = false, Margin = new Thickness(0, 0, 8, 2)
                };
                cb.IsCheckedChanged += (_, _) =>
                {
                    if (cb.IsChecked == true) selectedPreConds.Add(rawId);
                    else selectedPreConds.Remove(rawId);
                    mermaidTextBlock.Text = BuildMermaid();
                    // Rebuild visual tree branches to reflect preCondition changes
                    branchesPanel.Children.Clear();
                    BuildBranchNodes(branchesPanel, selectedPreConds);
                };
                cbPanel.Children.Add(cb);
            }
            preCondPanel.Children.Add(cbPanel);
        }

        // ── Local function: build branch nodes reflecting preCondition selection ──
        void BuildBranchNodes(StackPanel panel, HashSet<string> selPreConds)
        {
            // Recalculate valid-total weight based on Y/N condition matching
            double validTotalWeight = 0;
            foreach (var r in responseList)
            {
                if (AreAllPreCondsSatisfied(r.TargetEncounter, selPreConds))
                    validTotalWeight += r.Weight;
            }

            foreach (var resp in responseList)
            {
                var isBranchValid = AreAllPreCondsSatisfied(resp.TargetEncounter, selPreConds);
                // Effective probability: weight / validTotalWeight (or 0 if branch invalid)
                var effectiveProb = validTotalWeight > 0 && isBranchValid ? resp.Weight / validTotalWeight : 0.0;
                var probRatio = Math.Clamp(effectiveProb, 0.0, 1.0);
                var branchColor = probRatio >= 0.5 ? "#2E7D32" : probRatio >= 0.1 ? "#E65100" : "#999";
                var branchBg = probRatio >= 0.5 ? "#E8F5E9" : probRatio >= 0.1 ? "#FFF3E0" : "#F5F5F5";

                var branchOpacity = (!isBranchValid && selPreConds.Count > 0) ? 0.5 : 1.0;

                var branchNode = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, Opacity = branchOpacity };

                // Item usage hint badge (if applicable)
                if (resp.Item is not null)
                {
                    var qtyText = resp.ItemMult > 1 ? $" ×{resp.ItemMult}" : "";
                    branchNode.Children.Add(VisHelper.MiniBadge(
                        $"🛡 {resp.Item.Description}{qtyText}", "#E3F2FD", "#1565C0"));
                }
                else if (resp.ItemId is not null)
                {
                    branchNode.Children.Add(VisHelper.MiniBadge(
                        $"Item #{resp.ItemId}", "#F5F5F5", "#999"));
                }

                // Probability badge: weight(effective%)
                branchNode.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = Brush.Parse(branchColor),
                    Padding = new Thickness(8, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = $"{resp.Weight:F1}({effectiveProb:P2})", FontSize = 9, FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    }
                });

                // PreCondition badges (always shown, expanded)
                if (resp.TargetEncounter is not null && !string.IsNullOrWhiteSpace(resp.TargetEncounter.PreConditions))
                {
                    var targetPres = resp.TargetEncounter.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    if (targetPres.Count > 0)
                    {
                        var preBadgesPanel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        foreach (var tp in targetPres)
                        {
                            var isNeg = tp.StartsWith("-");
                            var rid = isNeg ? tp[1..] : tp;
                            var isOn = selPreConds.Count == 0 || IsPreCondSatisfied(tp, selPreConds);
                            var cond = ReferenceResolver.Instance.LookupRef<Condition>(
                                resp.TargetEncounter, nameof(Encounter.PreConditions), tp);
                            var label = (isNeg ? "¬" : "") + (cond?.Subject ?? rid);
                            var bg = isNeg ? "#FFF3E0" : "#E8F5E9";
                            var fg = isNeg ? "#E65100" : "#2E7D32";
                            if (selPreConds.Count > 0 && !isOn) { bg = "#F5F5F5"; fg = "#CCC"; }
                            preBadgesPanel.Children.Add(new Border
                            {
                                CornerRadius = new CornerRadius(3),
                                Background = Brush.Parse(bg),
                                Padding = new Thickness(4, 1),
                                Margin = new Thickness(1),
                                Child = new TextBlock
                                {
                                    Text = label, FontSize = 7,
                                    Foreground = Brush.Parse(fg),
                                    TextDecorations = isNeg ? TextDecorations.Strikethrough : null
                                }
                            });
                        }
                        branchNode.Children.Add(preBadgesPanel);
                    }
                }

                // Target encounter badge
                var targetBadge = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Background = Brush.Parse(branchBg),
                    Padding = new Thickness(8, 4),
                    Cursor = resp.TargetEncounter is not null
                        ? new Cursor(StandardCursorType.Hand)
                        : new Cursor(StandardCursorType.Arrow),
                    Child = new TextBlock
                    {
                        Text = resp.TargetEncounter?.Subject ?? $"Enc #{resp.TargetId}",
                        FontSize = 11,
                        Foreground = Brush.Parse(branchColor),
                        TextAlignment = TextAlignment.Center,
                        FontWeight = FontWeight.Medium
                    }
                };
                if (resp.TargetEncounter is not null)
                {
                    var capturedEnc = resp.TargetEncounter;
                    targetBadge.PointerPressed += (_, e) =>
                    {
                        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                            ReferenceResolver.Instance.NavigateTo(typeof(Encounter), capturedEnc.EntityId);
                    };
                }

                branchNode.Children.Add(targetBadge);
                panel.Children.Add(branchNode);
            }
        }

        // ── Visual tree diagram (horizontal: reverse ← current → branches) ──
        var treePanel = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };

        // LEFT column: Reverse refs (stacked vertically)
        var leftColumn = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };

        // Reverse refs: previous encounters → current (shown on the left)
        {
            if (reverseRefs.Count > 0)
            {
                foreach (var (src, itemDesc, itemMult, weight) in reverseRefs)
                {
                    var refInfo = itemDesc is not null
                        ? $"{itemDesc}{(itemMult > 1 ? $" ×{itemMult}" : "")} （权重 {weight:F0}）"
                        : $"权重 {weight:F0}";
                    var revBadge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Background = Brush.Parse("#FFF3E0"),
                        BorderBrush = Brush.Parse("#E65100"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 3),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Child = new StackPanel
                        {
                            Spacing = 1, Children =
                            {
                                new TextBlock
                                {
                                    Text = src.Subject ?? $"Enc #{src.Id}", FontSize = 10,
                                    FontWeight = FontWeight.Medium,
                                    Foreground = Brush.Parse("#BF360C"), TextAlignment = TextAlignment.Center
                                },
                                new TextBlock
                                {
                                    Text = refInfo, FontSize = 7,
                                    Foreground = Brush.Parse("#E65100"), TextAlignment = TextAlignment.Center
                                }
                            }
                        }
                    };
                    var capturedSrc = src;
                    revBadge.PointerPressed += (_, e) =>
                    {
                        if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                            ReferenceResolver.Instance.NavigateTo(typeof(Encounter), capturedSrc.EntityId);
                    };
                    leftColumn.Children.Add(revBadge);
                }
            }
            else
            {
                // No previous encounter — this is a root event
                var rootIndicatorPanel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
                rootIndicatorPanel.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = Brush.Parse("#E8F5E9"),
                    BorderBrush = Brush.Parse("#2E7D32"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 3),
                    Child = new TextBlock
                    {
                        Text = VisHelper.Loc("Vis.RootEncounter"), FontSize = 9,
                        Foreground = Brush.Parse("#2E7D32"), TextAlignment = TextAlignment.Center,
                        FontWeight = FontWeight.Medium
                    }
                });

                // Show current encounter's preconditions for easy reference
                if (!string.IsNullOrWhiteSpace(enc.PreConditions))
                {
                    var preList = enc.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    if (preList.Count > 0)
                    {
                        var preBadgesPanel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        foreach (var p in preList)
                        {
                            var isNeg = p.StartsWith("-");
                            var rid = isNeg ? p[1..] : p;
                            var cond = ReferenceResolver.Instance.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), p);
                            var label = (isNeg ? "¬" : "") + (cond?.Subject ?? rid);
                            var bg = isNeg ? "#FFF3E0" : "#E8F5E9";
                            var fg = isNeg ? "#E65100" : "#2E7D32";
                            preBadgesPanel.Children.Add(new Border
                            {
                                CornerRadius = new CornerRadius(3),
                                Background = Brush.Parse(bg),
                                Padding = new Thickness(4, 1),
                                Margin = new Thickness(1),
                                Child = new TextBlock
                                {
                                    Text = label, FontSize = 7, Foreground = Brush.Parse(fg),
                                    TextDecorations = isNeg ? TextDecorations.Strikethrough : null
                                }
                            });
                        }
                        rootIndicatorPanel.Children.Add(preBadgesPanel);
                    }
                }
                leftColumn.Children.Add(rootIndicatorPanel);
            }
        }
        treePanel.Children.Add(leftColumn);

        // Arrow: left → center
        treePanel.Children.Add(new TextBlock
        {
            Text = "→", FontSize = 16,
            Foreground = Brush.Parse(reverseRefs.Count > 0 ? "#E65100" : "#2E7D32"),
            VerticalAlignment = VerticalAlignment.Center
        });

        // CENTER column: Current encounter (highlighted)
        var centerColumn = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        {
            var rootNode = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brush.Parse("#E3F2FD"),
                BorderBrush = Brush.Parse("#1565C0"),
                BorderThickness = new Thickness(3),
                Padding = new Thickness(12, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var rootSp = new StackPanel { Spacing = 2 };
            rootSp.Children.Add(new TextBlock
            {
                Text = VisHelper.Loc("Vis.CurrentEncounter"), FontSize = 8, Foreground = Brush.Parse("#1565C0"),
                TextAlignment = TextAlignment.Center
            });
            rootSp.Children.Add(new TextBlock
            {
                Text = enc.Subject ?? $"Enc #{enc.Id}", FontSize = 13, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#0D47A1"), TextAlignment = TextAlignment.Center
            });

            // Show current encounter's preconditions below the title
            if (!string.IsNullOrWhiteSpace(enc.PreConditions))
            {
                var preList = enc.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                if (preList.Count > 0)
                {
                    var preWrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    foreach (var p in preList)
                    {
                        var isNeg = p.StartsWith("-");
                        var rid = isNeg ? p[1..] : p;
                        var cond = ReferenceResolver.Instance.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), p);
                        var label = (isNeg ? "¬" : "") + (cond?.Subject ?? rid);
                        var bg = isNeg ? "#FFF3E0" : "#E8F5E9";
                        var fg = isNeg ? "#E65100" : "#2E7D32";
                        preWrap.Children.Add(new Border
                        {
                            CornerRadius = new CornerRadius(3),
                            Background = Brush.Parse(bg),
                            Padding = new Thickness(4, 1),
                            Margin = new Thickness(1),
                            Child = new TextBlock
                            {
                                Text = label, FontSize = 7, Foreground = Brush.Parse(fg),
                                TextDecorations = isNeg ? TextDecorations.Strikethrough : null
                            }
                        });
                    }
                    rootSp.Children.Add(preWrap);
                }
            }

            rootNode.Child = rootSp;
            centerColumn.Children.Add(rootNode);
        }
        treePanel.Children.Add(centerColumn);

        // Arrow: center → right
        treePanel.Children.Add(new TextBlock
        {
            Text = "→", FontSize = 16, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center
        });

        // RIGHT column: Branch nodes (stacked vertically)
        // Build initial branch nodes
        BuildBranchNodes(branchesPanel, selectedPreConds);
        treePanel.Children.Add(branchesPanel);

        // ── Combine story branch content ──
        var storyBranchContent = new StackPanel();
        
        // PreCondition checkbox panel (at top of story branch tab)
        if (preCondPanel.Children.Count > 0)
            storyBranchContent.Children.Add(preCondPanel);
        
        // Visual tree diagram (horizontally scrollable, left→right layout)
        storyBranchContent.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
            Content = VisHelper.Card(treePanel)
        });

        // ── Build recursive encounter chain (depth-limited, dedup) ──
        var forwardChain = BuildEncounterChainTree(enc, new HashSet<int>(), 0, 6);
        var reverseChain = BuildReverseChainPanel(enc);

        // Reverse chain (previous encounters → current, with dedup and position mark)
        if (reverseChain is not null)
            storyBranchContent.Children.Add(reverseChain);

        // ── TabControl: 剧情分支 | 剧情链 | Mermaid源码 ──
        var tabControl = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        var storyTab = new TabItem
        {
            Header = VisHelper.Loc("Vis.StoryBranch"),
            Content = storyBranchContent
        };
        var chainTab = new TabItem { Header = VisHelper.Loc("Vis.EncounterChain"), Content = forwardChain };
        
        // Mermaid tab: only raw source code
        var mermaidTabContent = new StackPanel { Spacing = 6 };
        mermaidTabContent.Children.Add(mermaidBlock);
        var mermaidTab = new TabItem
        {
            Header = VisHelper.Loc("Vis.MermaidSource"),
            Content = mermaidTabContent
        };
        tabControl.Items.Add(storyTab);
        tabControl.Items.Add(chainTab);
        tabControl.Items.Add(mermaidTab);
        tabControl.SelectedIndex = 0;

        sp.Children.Add(tabControl);
        return sp;
    }

    // ═══ Recursive encounter chain tree (dedup, depth-limited) ═══
    private static Control BuildEncounterChainTree(Encounter root, HashSet<int> visited, int depth, int maxDepth)
    {
        if (depth > maxDepth || !visited.Add(root.Id))
            return new StackPanel(); // dedup: already visited

        var sp = new StackPanel();
        if (depth == 0)
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.EncounterChain")));
        }

        var responseList = ParseResponseEntries(root.Responses, root);
        var marginLeft = depth * 24;

        // Current node
        var nodePanel = new StackPanel { Spacing = 4, Margin = new Thickness(marginLeft, 4, 0, 4) };
        var isCurrent = depth == 0;

        // Collect entity context for this node
        var contextBadges = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
        if (!string.IsNullOrWhiteSpace(root.TreasureId) && root.TreasureId != "3")
        {
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(root, nameof(Encounter.TreasureId), root.TreasureId);
            if (tt is not null)
                contextBadges.Children.Add(VisHelper.MiniBadge($"🎒{tt.Name}", "#E8F5E9", "#2E7D32"));
        }
        if (root.CreatureId != "0")
        {
            var cs = ReferenceResolver.Instance.LookupRef<CreatureSource>(root, nameof(Encounter.CreatureId), root.CreatureId);
            if (cs is not null)
                contextBadges.Children.Add(VisHelper.MiniBadge($"🐾{cs.Subject}", "#E8EAF6", "#283593"));
        }
        if (!string.IsNullOrWhiteSpace(root.Conditions) && root.Conditions != "1")
        {
            var condCount = root.Conditions.Split(',').Length;
            contextBadges.Children.Add(VisHelper.MiniBadge($"⚡{condCount} conditions", "#FCE4EC", "#C62828"));
        }
        if (!string.IsNullOrWhiteSpace(root.PreConditions))
        {
            var preCount = root.PreConditions.Split(',').Length;
            contextBadges.Children.Add(VisHelper.MiniBadge($"📋pre:{preCount}", "#E8F5E9", "#2E7D32"));
        }

        var nodeBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse(isCurrent ? "#E3F2FD" : "#F5F5F5"),
            BorderBrush = Brush.Parse(isCurrent ? "#1565C0" : "#E0E0E0"),
            BorderThickness = new Thickness(isCurrent ? 3 : 1),
            Padding = new Thickness(10, 4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    isCurrent
                        ? new TextBlock { Text = "📍 " + VisHelper.Loc("Vis.CurrentPosition"), FontSize = 8, Foreground = Brush.Parse("#1565C0") }
                        : new TextBlock(),
                    new TextBlock
                    {
                        Text = root.Subject ?? $"Enc #{root.Id}",
                        FontSize = isCurrent ? 12 : 11,
                        FontWeight = isCurrent ? FontWeight.Bold : FontWeight.Medium,
                        Foreground = Brush.Parse(isCurrent ? "#0D47A1" : "#555"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"ID: {root.Id} · Type: {(root.Type == EncounterType.Scavenge ? "Scavenge" : "Normal")}",
                        FontSize = 8, Foreground = Brush.Parse("#999")
                    },
                    contextBadges.Children.Count > 0 ? contextBadges : new TextBlock()
                }
            }
        };
        nodeBorder.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                ReferenceResolver.Instance.NavigateTo(typeof(Encounter), root.EntityId);
        };
        nodePanel.Children.Add(nodeBorder);

        // Children (response targets)
        if (responseList.Count > 0 && depth < maxDepth)
        {
            var childrenPanel = new StackPanel { Spacing = 2 };
            foreach (var resp in responseList)
            {
                if (resp.TargetEncounter is null) continue;
                // Edge label: item + weight(probability)
                var edgeLabel = "";
                if (resp.Item is not null)
                    edgeLabel = $"🛡 {resp.Item.Description}{(resp.ItemMult > 1 ? $" ×{resp.ItemMult}" : "")} ";
                edgeLabel += $"→ {resp.Weight:F1}({resp.Probability:P2})";
                childrenPanel.Children.Add(new TextBlock
                {
                    Text = edgeLabel, FontSize = 9, Foreground = Brush.Parse("#888"),
                    Margin = new Thickness(marginLeft + 20, 1, 0, 1)
                });
                var childTree = BuildEncounterChainTree(resp.TargetEncounter, visited, depth + 1, maxDepth);
                if (childTree is StackPanel childSp && childSp.Children.Count > 0)
                    childrenPanel.Children.Add(childTree);
            }
            nodePanel.Children.Add(childrenPanel);
        }
        else if (responseList.Count == 0)
        {
            nodePanel.Children.Add(new TextBlock
            {
                Text = "(leaf)", FontSize = 9, Foreground = Brush.Parse("#CCC"),
                Margin = new Thickness(marginLeft + 20, 0, 0, 0)
            });
        }

        sp.Children.Add(nodePanel);
        return depth == 0
            ? new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 500,
                Content = VisHelper.Card(sp)
            }
            : sp;
    }

    // ═══ Reverse encounter chain (who references me) ═══
    private static Control? BuildReverseChainPanel(Encounter enc)
    {
        var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (store == null) return null;

        var rawRefs = store.Index.ReverseLookup(enc.EntityId);
        var refs = new List<(Encounter Source, string ViaItem)>();

        foreach (var (srcEid, propName, rawId) in rawRefs)
        {
            if (propName != nameof(Encounter.Responses)) continue;
            if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Encounter), out var list) || list is null)
                continue;
            var src = list.OfType<Encounter>().FirstOrDefault(e => e.EntityId == srcEid);
            if (src is null) continue;
            refs.Add((src, rawId));
        }

        if (refs.Count == 0) return null;

        var sp = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        sp.Children.Add(VisHelper.SectionLabel($"👈 Referenced By ({refs.Count})"));
        var tree = BuildReverseChainTree(enc, refs, new HashSet<int>(), 0, 4);
        if (tree is StackPanel tsp && tsp.Children.Count > 0)
            sp.Children.Add(VisHelper.Card(tree));
        return sp;
    }

    private static Control BuildReverseChainTree(Encounter target, List<(Encounter Source, string ViaItem)> refs,
        HashSet<int> visited, int depth, int maxDepth)
    {
        if (depth > maxDepth) return new StackPanel();

        var sp = new StackPanel { Spacing = 2 };
        var marginLeft = depth * 24;

        foreach (var (src, viaItem) in refs)
        {
            if (!visited.Add(src.Id)) continue;

            // Edge label (above node)
            sp.Children.Add(new TextBlock
            {
                Text = $"← {src.Subject ?? $"Enc #{src.Id}"} via Responses", FontSize = 9, Foreground = Brush.Parse("#888"),
                Margin = new Thickness(marginLeft + 12, 2, 0, 2)
            });

            // Source node
            var contextBadges = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
            if (!string.IsNullOrWhiteSpace(src.TreasureId) && src.TreasureId != "3")
            {
                var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(src, nameof(Encounter.TreasureId), src.TreasureId);
                if (tt is not null)
                    contextBadges.Children.Add(VisHelper.MiniBadge($"🎒{tt.Name}", "#E8F5E9", "#2E7D32"));
            }
            if (src.CreatureId != "0")
            {
                contextBadges.Children.Add(VisHelper.MiniBadge("🐾", "#E8EAF6", "#283593"));
            }

            var capturedSrc = src;
            var nodeBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brush.Parse(depth == 0 ? "#FFF8E1" : "#F5F5F5"),
                BorderBrush = Brush.Parse(depth == 0 ? "#F9A825" : "#E0E0E0"),
                BorderThickness = new Thickness(depth == 0 ? 2 : 1),
                Padding = new Thickness(10, 4),
                Margin = new Thickness(marginLeft, 0, 0, 0),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = src.Subject ?? $"Enc #{src.Id}",
                            FontSize = 11, FontWeight = FontWeight.Medium,
                            Foreground = Brush.Parse("#555"), TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"ID: {src.Id} · {(src.Type == EncounterType.Scavenge ? "Scavenge" : "Normal")}",
                            FontSize = 8, Foreground = Brush.Parse("#999")
                        },
                        contextBadges.Children.Count > 0 ? contextBadges : new TextBlock()
                    }
                }
            };
            nodeBorder.PointerPressed += (_, e) =>
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0)
                    ReferenceResolver.Instance.NavigateTo(typeof(Encounter), capturedSrc.EntityId);
            };
            sp.Children.Add(nodeBorder);

            // Recursively find who references this source
            var store = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
            if (store is not null && depth < maxDepth)
            {
                var subRefs = new List<(Encounter Source, string ViaItem)>();
                var rawSubRefs = store.Index.ReverseLookup(src.EntityId);
                foreach (var (subSrcEid, subPropName, subRawId) in rawSubRefs)
                {
                    if (subPropName != nameof(Encounter.Responses)) continue;
                    if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Encounter), out var elist) || elist is null)
                        continue;
                    var subSrc = elist.OfType<Encounter>().FirstOrDefault(e => e.EntityId == subSrcEid);
                    if (subSrc is null) continue;
                    subRefs.Add((subSrc, subRawId));
                }
                if (subRefs.Count > 0)
                {
                    var childTree = BuildReverseChainTree(src, subRefs, visited, depth + 1, maxDepth);
                    if (childTree is StackPanel csp && csp.Children.Count > 0)
                        sp.Children.Add(childTree);
                }
            }
        }

        return sp;
    }

    private static Control BuildRefsPanel(Encounter enc)
    {
        var sp = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(enc.TreasureId) && enc.TreasureId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.LootTable")));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(enc, nameof(Encounter.TreasureId),
                enc.TreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"TT #{enc.TreasureId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.RemoveTreasureId) && enc.RemoveTreasureId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.RemoveSubmit")));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(enc, nameof(Encounter.RemoveTreasureId),
                enc.RemoveTreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#FFEBEE", "#C62828",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"TT #{enc.RemoveTreasureId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.Conditions) && enc.Conditions != "1")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Conditions")));
            var wp = new WrapPanel();
            foreach (var seg in enc.Conditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(enc, nameof(Encounter.Conditions), seg);
                if (cond is not null)
                    wp.Children.Add(VisHelper.MiniBadge(cond.Subject, "#FCE4EC", "#C62828",
                        () => ReferenceResolver.Instance.NavigateToByKeyFor<Condition>(cond.Id, enc)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.PreConditions))
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.PreConditions")));
            var wp = new WrapPanel();
            foreach (var seg in enc.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var isNeg = seg.StartsWith("-");
                var rawId = isNeg ? seg[1..] : seg;
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), seg);
                if (cond is not null)
                    wp.Children.Add(VisHelper.MiniBadge((isNeg ? "NOT " : "") + cond.Subject,
                        isNeg ? "#FFEBEE" : "#E8F5E9", isNeg ? "#C62828" : "#2E7D32",
                        () => ReferenceResolver.Instance.NavigateToByKeyFor<Condition>(cond.Id, enc)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(VisHelper.Card(wp));
        }

        if (enc.CreatureId != "0")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.SpawnCreature")));
            var wp = new WrapPanel();
            var creature =
                ReferenceResolver.Instance.LookupRef<CreatureSource>(enc, nameof(Encounter.CreatureId), enc.CreatureId);
            if (creature is not null)
            {
                wp.Children.Add(VisHelper.MiniBadge(creature.Subject, "#E8EAF6", "#283593",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(CreatureSource), creature.EntityId)));
                if (!string.IsNullOrWhiteSpace(enc.CreatureHex) && enc.CreatureHex != "0,0")
                    wp.Children.Add(new TextBlock
                    {
                        Text = $" at {enc.CreatureHex}", FontSize = 10, Foreground = Brush.Parse("#999"),
                        VerticalAlignment = VerticalAlignment.Center
                    });
            }
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{enc.CreatureId}", "#F5F5F5", "#999"));

            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.Teleport) && enc.Teleport != "0,0")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Teleport")));
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = $"Destination: ({enc.Teleport})", FontSize = 11, Foreground = Brush.Parse("#6A1B9A") }));
        }

        if (!string.IsNullOrWhiteSpace(enc.Accidents) && enc.Accidents != "1")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Accidents")));
            var wp = new WrapPanel();
            foreach (var seg in enc.Accidents.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var accident = ReferenceResolver.Instance.LookupRef<Encounter>(enc, nameof(Encounter.Accidents), seg);
                if (accident is not null)
                    wp.Children.Add(VisHelper.MiniBadge(accident.Subject, "#FFEBEE", "#C62828",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), accident.EntityId)));
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
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.TriggeredBy")} ({triggers.Count})"));
        var wp = new WrapPanel();
        foreach (var trigger in triggers)
            wp.Children.Add(VisHelper.MiniBadge($"{trigger.Name}", "#F3E5F5", "#6A1B9A",
                () => ReferenceResolver.Instance.NavigateTo(typeof(EncounterTrigger), trigger.EntityId)));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(c), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(c));
        root.Children.Add(BuildStatsPanel(c));
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
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 }
            });
        else
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#0A000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "??", FontSize = 24, Foreground = Brush.Parse("#999"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            });
        root.Children.Add(imgStack);

        root.Children.Add(new TextBlock
        {
            Text = c.Subject ?? c.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        if (!string.IsNullOrWhiteSpace(c.NamePublic) && c.NamePublic != c.Name)
            root.Children.Add(new TextBlock
            {
                Text = $"\"{c.NamePublic}\"", FontSize = 10, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#888"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
            });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
        var factionName = ReferenceResolver.Instance.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction)
            ?.Subject;
        var statRows = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.MovesPerTurn"), $"{c.MovesPerTurn}", null),
            (VisHelper.Loc("Vis.Faction"), factionName ?? $"#{c.Faction}", null)
        };
        var atkCount = string.IsNullOrWhiteSpace(c.AttackModes) ? 0 : c.AttackModes.Split(',').Length;
        if (atkCount > 0) statRows.Add((VisHelper.Loc("Vis.Attacks"), $"{atkCount} modes", null));
        var conditionsCount = string.IsNullOrWhiteSpace(c.BaseConditions) ? 0 : c.BaseConditions.Split(',').Length;
        if (conditionsCount > 0) statRows.Add((VisHelper.Loc("Vis.CreatureStatus"), $"{conditionsCount}", null));
        var hasLoot = !string.IsNullOrWhiteSpace(c.TreasureId) && c.TreasureId != "3";
        if (hasLoot) statRows.Add((VisHelper.Loc("Vis.LootTable"), "Yes", "#2E7D32"));
        if (!string.IsNullOrWhiteSpace(c.Activities))
            statRows.Add((VisHelper.Loc("Vis.Activities"), "Yes", "#2E7D32"));
        root.Children.Add(VisHelper.BuildStatCard(statRows));

        return root;
    }

    private static Control BuildHeroHeader(Creature c)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = VisHelper.LoadImage(c.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
            var capturedBmp = bmp;
            imageArea.PointerPressed += (_, _) => VisHelper.OpenZoomableImage(capturedBmp, c.Subject ?? c.Name);
        }
        else
            imageArea.Child = new TextBlock
            {
                Text = "Creature", FontSize = 14, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };

        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {c.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(c, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = $"{c.MovesPerTurn} moves/turn", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        var factionName = ReferenceResolver.Instance.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction)
            ?.Subject;
        if (!string.IsNullOrWhiteSpace(factionName) && c.Faction != "0")
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = factionName, FontSize = 10, Foreground = Brush.Parse("#283593") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = c.Subject ?? c.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(c.NamePublic) && c.NamePublic != c.Name)
            identity.Children.Add(new TextBlock
            {
                Text = $"Public: {c.NamePublic}", FontSize = 12, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#888")
            });
        if (!string.IsNullOrWhiteSpace(c.Notes))
            identity.Children.Add(new TextBlock
                { Text = c.Notes, FontSize = 11, Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildRefsPanel(Creature c)
    {
        var sp = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(c.Faction) && c.Faction != "0")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Faction")));
            var wp = new WrapPanel();
            var faction = ReferenceResolver.Instance.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction);
            if (faction is not null)
                wp.Children.Add(VisHelper.MiniBadge(faction.Subject, "#FFF3E0", "#E65100",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(Faction), faction.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{c.Faction}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.AttackModes))
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.AttackModes")));
            var wp = new WrapPanel();
            foreach (var seg in c.AttackModes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var am = ReferenceResolver.Instance.LookupRef<AttackMode>(c, nameof(Creature.AttackModes), seg);
                if (am is not null)
                    wp.Children.Add(VisHelper.MiniBadge(am.Subject, "#FFEBEE", "#C62828",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(AttackMode), am.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.BaseConditions))
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.CreatureStatus")));
            var eqPattern = ReferencePattern.FromName("{id}={value}");
            var wp = new WrapPanel();
            foreach (var seg in c.BaseConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(c, nameof(Creature.BaseConditions), seg);
                if (cond is not null)
                {
                    var extra = eqPattern.FormatExtraInfo(seg);
                    var label = string.IsNullOrEmpty(extra) ? cond.Subject : $"{cond.Subject} ={extra}";
                    wp.Children.Add(VisHelper.MiniBadge(label, "#FCE4EC", "#C62828",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), cond.EntityId)));
                    continue;
                }

                wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.EncounterIds))
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.OnEnterConditions")));
            var wp = new WrapPanel();
            foreach (var seg in c.EncounterIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = ReferenceResolver.Instance.LookupRef<Condition>(c, nameof(Creature.EncounterIds), seg);
                if (cond is not null)
                    wp.Children.Add(VisHelper.MiniBadge(cond.Subject, "#E8EAF6", "#283593",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), cond.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.TreasureId) && c.TreasureId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.LootTable")));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(c, nameof(Creature.TreasureId), c.TreasureId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(c.TreasureId, "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.CorpseId) && c.CorpseId != "3")
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.CorpseLoot")));
            var wp = new WrapPanel();
            var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(c, nameof(Creature.CorpseId), c.CorpseId);
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#FCE4EC", "#880E4F",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(c.CorpseId, "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        return sp;
    }

    private static Control BuildStatsPanel(Creature c)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.Stats")} & Activity"));

        var conditionsCount = string.IsNullOrWhiteSpace(c.BaseConditions) ? 0 : c.BaseConditions.Split(',').Length;
        var encConditionsCount = string.IsNullOrWhiteSpace(c.EncounterIds) ? 0 : c.EncounterIds.Split(',').Length;
        var atkCount = string.IsNullOrWhiteSpace(c.AttackModes) ? 0 : c.AttackModes.Split(',').Length;
        var hasLoot = !string.IsNullOrWhiteSpace(c.TreasureId) && c.TreasureId != "3";
        var hasCorpse = !string.IsNullOrWhiteSpace(c.CorpseId) && c.CorpseId != "3";

        var grid = new Grid
        {
            ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star) },
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
            Margin = new Thickness(4, 0)
        };

        void AddCell(int r, int c, string label, string value, string? color = null)
        {
            var cell = new StackPanel { Margin = new Thickness(4, 3) };
            cell.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Brush.Parse("#999") });
            cell.Children.Add(new TextBlock
            {
                Text = value, FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333")
            });
            Grid.SetRow(cell, r);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        var factionName = ReferenceResolver.Instance.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction)
            ?.Subject;
        AddCell(0, 0, VisHelper.Loc("Vis.MovesPerTurn"), $"{c.MovesPerTurn}", "#E65100");
        AddCell(0, 1, VisHelper.Loc("Vis.Faction"), factionName ?? "None", "#283593");
        AddCell(1, 0, VisHelper.Loc("Vis.Attacks"), $"{atkCount}", atkCount > 0 ? "#C62828" : "#999");
        AddCell(1, 1, VisHelper.Loc("Vis.CreatureStatus"), $"{conditionsCount}",
            conditionsCount > 0 ? "#C62828" : "#999");
        AddCell(2, 0, VisHelper.Loc("Vis.EncConditions"), $"{encConditionsCount}",
            encConditionsCount > 0 ? "#283593" : "#999");
        AddCell(2, 1, VisHelper.Loc("Vis.LootTable"), hasLoot ? "Yes" : "No", hasLoot ? "#2E7D32" : "#999");
        AddCell(3, 0, VisHelper.Loc("Vis.CorpseLoot"), hasCorpse ? "Yes" : "No", hasCorpse ? "#880E4F" : "#999");
        AddCell(3, 1, VisHelper.Loc("Vis.Activities"), string.IsNullOrWhiteSpace(c.Activities) ? "None" : "Yes",
            string.IsNullOrWhiteSpace(c.Activities) ? "#999" : "#2E7D32");

        sp.Children.Add(VisHelper.Card(grid));
        return sp;
    }

    private static Control BuildActivitiesPanel(Creature c)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Activities")));
        var acts = c.Activities.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (acts.Count == 0)
        {
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = "(None)", FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        var wp = new WrapPanel();
        foreach (var act in acts.Take(30))
        {
            wp.Children.Add(VisHelper.MiniBadge(act, "#E8EAF6", "#283593"));
        }

        if (acts.Count > 30)
            wp.Children.Add(VisHelper.MiniBadge($"+{acts.Count - 30} more", "#F5F5F5", "#999"));
        sp.Children.Add(VisHelper.Card(wp));
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

    // ═══ Field name translations from NEO全代码.注释与基础修改思路.xml ═══
    private static readonly Dictionary<string, string> ConditionFieldTranslations = new()
    {
        ["m_fHealPerHourMod"] = "每小时恢复能力",
        ["m_fImmuneRestoreRate"] = "免疫恢复能力",
        ["m_fBloodRestoreRate"] = "血液恢复能力",
        ["fMovesPerTurnModifier"] = "每回合移动点数",
        ["m_fEncumberanceLimit"] = "负重值",
        ["m_fMoraleHidden"] = "隐藏的士气值",
        ["m_fDefense"] = "闪避几率",
        ["Asleep"] = "陷入沉睡",
        ["fSleepQuality"] = "睡眠质量",
        ["m_fSleepAwareness"] = "睡眠意识",
        ["fFoodConsumptionRate"] = "食物消耗速率",
        ["fWaterConsumptionRate"] = "水消耗速率",
        ["BaseDetectionLevel"] = "基准警觉值",
        ["MinSafeTemp"] = "最低安全温度",
        ["MaxSafeTemp"] = "最高安全温度",
        ["m_fFatigueModifier"] = "疲劳修饰值",
        ["fPassiveRewarmPerHour"] = "每小时被动升温",
        ["m_fMorale"] = "士气",
        ["m_fBloodLeft"] = "血液总量",
        ["BodyInsulation"] = "身体热量散发值",
        ["m_fVisibility"] = "自身可见度",
        ["m_fMoveReserve"] = "行动点数",
        ["m_fMoveReserveRemaining"] = "行动点总量",
        ["MinLightLevel"] = "最小适应亮度",
        ["AttDmgMult"] = "攻击伤害",
        ["VisionRange"] = "视觉范围",
        ["m_fScent"] = "气味",
        ["LightLevel"] = "光照等级",
        ["fFoodDebt"] = "饥饿值",
        ["fWaterDebt"] = "饥渴值",
        ["m_fImmuneLeft"] = "免疫总量",
        ["m_fTrackingThreshold"] = "追踪阈值",
        ["m_fPainLeftBase"] = "疼痛总量阈值",
        ["m_fImmuneLeftBase"] = "免疫总量阈值",
        ["m_fPainLeft"] = "疼痛恢复",
        ["DefDmgMult"] = "防御伤害数值",
        ["fSleepDebt"] = "睡眠不足",
        ["fCoreTemp"] = "核心体温",
        ["m_fMovesLeft"] = "剩余行动点",
        ["WetTempAdjustMod"] = "潮湿调节数值",
        ["MoveCost"] = "额外行动点消耗",
        ["ChangeRange"] = "改变距离",
        ["Attack"] = "攻击动作",
        ["ExitBattle"] = "退出战斗",
        ["KnockDown"] = "击倒",
        ["JustMoved"] = "刚刚移动过",
        ["Discharge"] = "解除武装",
        ["Trip"] = "被绊倒",
        ["Bandaged"] = "包扎",
        ["Infected"] = "感染",
        ["Disinfected"] = "消毒",
        ["m_fPain"] = "疼痛",
        ["Crippled"] = "残废",
        ["Splinted"] = "使用夹板",
        ["Threat"] = "威胁",
        ["ChangeRangeAll"] = "朝所有人退后",
        ["TriggerEncounter"] = "触发剧情",
        ["m_nMorality"] = "道德值",
        ["LoseRandomItem"] = "丢失随机物品",
        ["LoseAllItems"] = "丢失所有物品",
        ["Money"] = "钱",
        ["GetDiagnostic"] = "获取诊断结果",
        ["ResetTemp"] = "核心体温调节",
        ["SpawnNewCreature"] = "繁殖新生物",
        ["DropAllItems"] = "丢下所有物品",
        ["LootTarget"] = "掠夺目标",
        ["ApplyCutDamage"] = "从伤口上取出异物",
        ["ScatterMissile"] = "导弹",
        ["UseGPS"] = "正在使用GPS",
        ["ResetUsSpotted"] = "重置视觉",
        ["EmptyGroundSlot"] = "空的地面格",
        ["CleanAndDress"] = "清理并包扎伤口",
        ["BattleRange"] = "攻击距离",
        ["AddRecipe"] = "得到配方",
    };

    private static string TranslateFieldName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return ConditionFieldTranslations.TryGetValue(name, out var translation)
            ? $"{translation} ({name})"
            : name;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Condition cond) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(cond), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(cond));
        root.Children.Add(BuildPropertiesPanel(cond));
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
        var severityLabel =
            cond.Fatal ? "FATAL" : cond.Permanent ? "Instant" : cond.Stackable ? "Stackable" : "Duration";
        var sevBg = cond.Fatal ? "#FFEBEE" : cond.Permanent ? "#FFF3E0" : cond.Stackable ? "#E8F5E9" : "#E3F2FD";
        var sevFg = cond.Fatal ? "#C62828" : cond.Permanent ? "#E65100" : cond.Stackable ? "#2E7D32" : "#1565C0";
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(sevBg), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"{colorIcon} {severityLabel}", FontSize = 10, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse(sevFg)
            }
        });
        root.Children.Add(new TextBlock
        {
            Text = cond.Subject ?? cond.Name, FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
        });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Stats")));
        var statRows = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Duration"), cond.Permanent ? "Instant" : $"{cond.Duration}h", null),
            (VisHelper.Loc("Vis.Color"),
                cond.Color switch
                {
                    ConditionColor.Red => "Red (-)", ConditionColor.Green => "Green (+)",
                    ConditionColor.Yellow => "Yellow", _ => "White"
                }, null),
            (VisHelper.Loc("Vis.Transfer"), cond.TransferRange >= 0 ? $"{cond.TransferRange}" : "None", null)
        };
        root.Children.Add(VisHelper.BuildStatCard(statRows));

        // Modifiers count summary
        var names = (cond.FieldNames ?? "").Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        if (names.Count > 0)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Modifiers")));
            root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
            {
                (VisHelper.Loc("Vis.Count"), $"{names.Count} fields", null)
            }));
        }

        if (!string.IsNullOrWhiteSpace(cond.IdNext) && cond.IdNext != "0")
        {
            var nextCount = cond.IdNext.Split(',').Length;
            root.Children.Add(VisHelper.Kv(VisHelper.Loc("Vis.NextStage"), $"{nextCount} condition(s)", 85));
        }

        return root;
    }

    private static Control BuildHeroHeader(Condition cond)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {cond.Id}", FontSize = 11, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#1565C0")
            }
        });

        VisHelper.AddModBadge(cond, idRow);

        var sevBg = cond.Fatal ? "#FFEBEE" : cond.Permanent ? "#FFF3E0" : "#E8F5E9";
        var sevFg = cond.Fatal ? "#C62828" : cond.Permanent ? "#E65100" : "#2E7D32";
        var sevLabel = cond.Fatal ? "FATAL" : cond.Permanent ? "Instant" : "Duration";
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(sevBg), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = sevLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(sevFg) }
        });
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (cond.Stackable)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "Stackable", FontSize = 10, Foreground = Brush.Parse("#2E7D32") }
            });
        if (!cond.Display)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#F5F5F5"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "Hidden", FontSize = 10, Foreground = Brush.Parse("#999") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = cond.Subject ?? cond.Name, FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        var statRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock
        {
            Text = cond.Permanent ? "Instant" : $"{cond.Duration}h", FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = cond.Color switch
            {
                ConditionColor.Red => "Red (-)", ConditionColor.Green => "Green (+)",
                ConditionColor.Yellow => "Yellow", _ => "White"
            },
            FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
            { Text = $"Transfer: {cond.TransferRange}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.ResetTimer)
            statRow.Children.Add(new TextBlock
                { Text = "ResetTimer", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.DisplayOther)
            statRow.Children.Add(new TextBlock
                { Text = "Visible to Others", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.DisplayGameOver)
            statRow.Children.Add(new TextBlock
                { Text = "GameOver Log", FontSize = 11, Foreground = Brush.Parse("#666") });
        identity.Children.Add(statRow);

        if (!string.IsNullOrWhiteSpace(cond.Thresholds))
            identity.Children.Add(new TextBlock
                { Text = $"Thresholds: {cond.Thresholds}", FontSize = 11, Foreground = Brush.Parse("#6A1B9A") });
        if (!string.IsNullOrWhiteSpace(cond.ChanceNext) && cond.ChanceNext != "0")
            identity.Children.Add(new TextBlock
                { Text = $"Chance Next: {cond.ChanceNext}", FontSize = 11, Foreground = Brush.Parse("#666") });

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildDescriptionPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Description")));
        var desc = cond.Description.Length > 800 ? cond.Description[..800] + "..." : cond.Description;
        sp.Children.Add(VisHelper.Card(new TextBlock
            { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
        return sp;
    }

    private static Control BuildPropertiesPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Properties")));
        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Duration"), cond.Permanent ? "Instant" : $"{cond.Duration}h", null),
            (VisHelper.Loc("Vis.Color"),
                cond.Color switch
                {
                    ConditionColor.Red => "Red (-)", ConditionColor.Green => "Green (+)",
                    ConditionColor.Yellow => "Yellow", _ => "White"
                }, null),
            (VisHelper.Loc("Vis.Transfer"), cond.TransferRange >= 0 ? $"{cond.TransferRange}" : "None", null),
        };
        if (cond.Fatal) cells.Add((VisHelper.Loc("Vis.Fatal"), "Yes", "#C62828"));
        if (cond.Permanent) cells.Add((VisHelper.Loc("Vis.Permanent"), "Yes", "#E65100"));
        if (cond.Stackable) cells.Add((VisHelper.Loc("Vis.Stackable"), "Yes", "#2E7D32"));
        if (!cond.Display) cells.Add((VisHelper.Loc("Vis.Hidden"), "Yes", "#999"));
        if (cond.DisplayOther) cells.Add((VisHelper.Loc("Vis.DisplayOther"), "Yes", "#666"));
        cells.Add((VisHelper.Loc("Vis.ResetTimer"),
            cond.ResetTimer ? VisHelper.Loc("Vis.Yes") : VisHelper.Loc("Vis.No"),
            cond.ResetTimer ? "#2E7D32" : "#E65100"));
        if (cond.RemoveAll) cells.Add((VisHelper.Loc("Vis.RemoveAll"), "Yes", "#999"));
        if (cond.RemovePostCombat) cells.Add((VisHelper.Loc("Vis.RemovePostCombat"), "Yes", "#999"));
        if (cond.DisplayGameOver) cells.Add((VisHelper.Loc("Vis.DisplayGameOver"), "Yes", "#666"));
        if (!string.IsNullOrWhiteSpace(cond.Thresholds))
            cells.Add((VisHelper.Loc("Vis.Thresholds"), cond.Thresholds, "#6A1B9A"));
        sp.Children.Add(VisHelper.CreatureStatGrid(cells));
        return sp;
    }

    private static Control BuildModifiersPanel(Condition cond)
    {
        var names = (cond.FieldNames ?? "").Split(',').Select(s => s.Trim()).ToList();
        var mods = (cond.Modifiers ?? "").Split(',').Select(s => s.Trim()).ToList();
        if (names.Count == 0 || names.All(string.IsNullOrEmpty)) return new StackPanel();

        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Modifiers")));

        // Collect valid pairs
        var pairs = new List<(string name, double val)>();
        for (int i = 0; i < Math.Max(names.Count, mods.Count); i++)
        {
            var name = i < names.Count ? names[i] : "";
            var modStr = i < mods.Count ? mods[i] : "";
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(modStr)) continue;
            if (double.TryParse(modStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                pairs.Add((name, val));
        }

        if (pairs.Count == 0) return new StackPanel();

        // Grid layout: field name | value text | bar
        var maxAbs = Math.Max(pairs.Max(p => Math.Abs(p.val)), 0.01);
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new(260, GridUnitType.Pixel), // field name (wider for Chinese)
                new(64, GridUnitType.Pixel), // value
                new(1, GridUnitType.Star) // bar fill
            },
            Margin = new Thickness(4, 0)
        };
        int row = 0;
        foreach (var (name, val) in pairs)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));
            var isNeg = val < 0;
            var absRatio = Math.Clamp(Math.Abs(val) / maxAbs, 0.08, 1.0);
            var barColor = isNeg ? "#C62828" : val > 0 ? "#2E7D32" : "#999";

            // Field name (with Chinese translation if available)
            var nameTb = new TextBlock
            {
                Text = TranslateFieldName(name), FontSize = 10, Foreground = Brush.Parse("#555"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 2, 8, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(nameTb, row);
            Grid.SetColumn(nameTb, 0);
            grid.Children.Add(nameTb);

            // Value text
            var valTb = new TextBlock
            {
                Text = $"{val:+#.##;-#.##;0}", FontSize = 10, FontWeight = FontWeight.Medium,
                Foreground = Brush.Parse(barColor),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 8, 2)
            };
            Grid.SetRow(valTb, row);
            Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            // Bar (centered at 0)
            var barRow = new Grid { Margin = new Thickness(0, 3) };
            barRow.ColumnDefinitions.Add(new(1, GridUnitType.Star));
            barRow.ColumnDefinitions.Add(new(3, GridUnitType.Pixel));
            barRow.ColumnDefinitions.Add(new(1, GridUnitType.Star));

            // Center line
            var center = new Border { Background = Brush.Parse("#18000000"), Margin = new Thickness(0, 0) };
            Grid.SetColumn(center, 1);
            barRow.Children.Add(center);

            if (isNeg)
            {
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(3, 0, 0, 3),
                    Background = Brush.Parse(barColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Width = absRatio * 120, MaxWidth = 120
                };
                Grid.SetColumn(fill, 0);
                barRow.Children.Add(fill);
            }
            else if (val > 0)
            {
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(0, 3, 3, 0),
                    Background = Brush.Parse(barColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = absRatio * 120, MaxWidth = 120
                };
                Grid.SetColumn(fill, 2);
                barRow.Children.Add(fill);
            }

            Grid.SetRow(barRow, row);
            Grid.SetColumn(barRow, 2);
            grid.Children.Add(barRow);

            row++;
        }

        sp.Children.Add(VisHelper.Card(grid));
        return sp;
    }

    private static Control BuildEffectsPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Effects")));
        var eff = cond.Effects.Length > 800 ? cond.Effects[..800] + "..." : cond.Effects;
        sp.Children.Add(VisHelper.Card(new TextBlock
        {
            Text = eff, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#00695C"),
            FontFamily = "Consolas, monospace"
        }));
        return sp;
    }

    private static Control BuildNextPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.ConditionChain")));
        var chainStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var segments = cond.IdNext.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0 && s != "0").ToList();
        if (segments.Count == 0)
        {
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = "(No next stage)", FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
                chainStack.Children.Add(new TextBlock
                {
                    Text = "→", FontSize = 14, Foreground = Brush.Parse("#6A1B9A"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            var seg = segments[i];
            var next = ReferenceResolver.Instance.LookupRef<Condition>(cond, nameof(Condition.IdNext), seg);
            var label = next?.Subject ?? $"#{seg}";
            var bg = next is not null ? "#F3E5F5" : "#F5F5F5";
            var fg = next is not null ? "#6A1B9A" : "#999";
            var badge = VisHelper.MiniBadge(label, bg, fg,
                next is not null
                    ? () => ReferenceResolver.Instance.NavigateTo(typeof(Condition), next.EntityId)
                    : null);
            chainStack.Children.Add(badge);
        }

        // Chance indicators
        if (!string.IsNullOrWhiteSpace(cond.ChanceNext) && cond.ChanceNext != "0")
        {
            sp.Children.Add(VisHelper.Card(chainStack));
            sp.Children.Add(VisHelper.SectionLabel("Progression Chances"));
            var chanceParts = cond.ChanceNext.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var chanceStack = new StackPanel { Spacing = 4 };
            foreach (var part in chanceParts)
            {
                var eq = part.Split('=');
                var chLabel = part;
                double chVal = 0;
                if (eq.Length == 2 && double.TryParse(eq[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var cv))
                {
                    chVal = cv;
                    var chPct = (int)(chVal * 100);
                    chLabel = $"{eq[0]} → {chPct}%";
                }

                var color = chVal >= 1.0 ? "#2E7D32" : chVal >= 0.5 ? "#E65100" : chVal > 0 ? "#999" : "#999";
                var barWidth = Math.Max(chVal * 200, 30);
                chanceStack.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = Brush.Parse(color),
                    Height = 20,
                    Width = barWidth,
                    Child = new TextBlock
                    {
                        Text = chLabel, FontSize = 10, Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(6, 0)
                    }
                });
            }

            sp.Children.Add(VisHelper.Card(chanceStack));
            return sp;
        }

        sp.Children.Add(VisHelper.Card(chainStack));
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
                Child = new SymbolIcon
                {
                    Symbol = iconSymbol, FontSize = 32, Foreground = Brush.Parse("#999"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
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
            Child = new TextBlock
            {
                Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse(isRanged ? "#C62828" : "#2E7D32")
            }
        });
        root.Children.Add(new TextBlock
        {
            Text = am.Subject ?? am.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
        {
            var quote = am.WieldPhrase.Length > 80 ? am.WieldPhrase[..80] + "..." : am.WieldPhrase;
            root.Children.Add(new TextBlock
            {
                Text = $"\"{quote}\"", FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888"),
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
            });
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
                var cp = ReferenceResolver.Instance.LookupRef<ChargeProfile>(am, nameof(AttackMode.ChargeProfiles),
                    seg);
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
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "▶", FontSize = 9, Foreground = Brush.Parse("#7B1FA2") },
                        new TextBlock { Text = am.Sound, FontSize = 10, Foreground = Brush.Parse("#7B1FA2") }
                    }
                }
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
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
            var capturedBmp = bmp;
            imageArea.PointerPressed += (_, _) => VisHelper.OpenZoomableImage(capturedBmp, am.Subject ?? am.Name);
        }
        else
        {
            var iconSymbol = GetSoundSymbol(am.Sound) ?? (am.Type == AttackType.Ranged ? Symbol.Target : Symbol.Flash);
            imageArea.Child = new SymbolIcon
            {
                Symbol = iconSymbol, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        }

        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {am.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        var rangeStr = am.Range <= 1 ? "1 tile" : $"{am.Range} tiles";
        var typeLabel = am.Type == AttackType.Ranged ? $"Ranged ({rangeStr})" : $"Melee ({rangeStr})";
        var typeBg = am.Type == AttackType.Ranged ? "#FCE4EC" : "#E8F5E9";
        var typeFg = am.Type == AttackType.Ranged ? "#C62828" : "#2E7D32";
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(typeBg),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });
        VisHelper.AddModBadge(am, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
            { Text = am.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
        {
            var quote = am.WieldPhrase.Length > 120 ? am.WieldPhrase[..120] + "..." : am.WieldPhrase;
            identity.Children.Add(new TextBlock
            {
                Text = $"\"{quote}\"", FontSize = 12, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#666"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(am.Notes))
            identity.Children.Add(new TextBlock
            {
                Text = am.Notes, FontSize = 12, Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap
            });

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
        var headerRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        headerRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12), Width = 24, Height = 24,
            Background = Brush.Parse(iconBg),
            Child = new SymbolIcon
            {
                Symbol = iconSymbol, FontSize = 14, Foreground = Brush.Parse(iconFg),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        });
        headerRow.Children.Add(new TextBlock
        {
            Text = isRanged ? VisHelper.Loc("Vis.CombatRanged") : VisHelper.Loc("Vis.CombatMelee"), FontSize = 13,
            FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#555"),
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(headerRow);

        var bars = new StackPanel { Spacing = 6 };

        var rangeMax = Math.Max(am.Range, 10);
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Range"), $"{am.Range} {VisHelper.Loc("Vis.Tiles")}",
            am.Range / (double)rangeMax, "#607D8B"));

        var maxDmg = Math.Max(am.DamageCut, Math.Max(am.DamageBlunt, 2.0));
        if (am.DamageCut > 0)
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Cut"), $"{am.DamageCut:F1}", am.DamageCut / maxDmg,
                "#E53935"));
        if (am.DamageBlunt > 0)
            bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Blunt"), $"{am.DamageBlunt:F1}",
                am.DamageBlunt / maxDmg, "#FB8C00"));

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
            var penRow = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            penRow.Children.Add(new TextBlock
            {
                Text = VisHelper.Loc("Penetration"), FontSize = 11, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var penDots = new string('●', am.Penetration) + new string('○', Math.Max(0, 4 - am.Penetration));
            penRow.Children.Add(new TextBlock
            {
                Text = $"{penDots}  Lv.{am.Penetration}", FontSize = 11, Foreground = Brush.Parse("#6A1B9A"),
                VerticalAlignment = VerticalAlignment.Center
            });
            bars.Children.Add(penRow);
        }

        if (!string.IsNullOrWhiteSpace(am.Sound))
        {
            var sndRow = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            sndRow.Children.Add(new TextBlock
            {
                Text = VisHelper.Loc("Sound"), FontSize = 11, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center
            });
            sndRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#F3E5F5"),
                Padding = new Thickness(8, 2),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "▶", FontSize = 9, Foreground = Brush.Parse("#7B1FA2") },
                        new TextBlock { Text = am.Sound, FontSize = 10, Foreground = Brush.Parse("#7B1FA2") }
                    }
                }
            });
            bars.Children.Add(sndRow);
        }

        if (am.Transfer)
        {
            var tRow = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            tRow.Children.Add(new TextBlock
            {
                Text = VisHelper.Loc("Transfer"), FontSize = 11, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center
            });
            tRow.Children.Add(new TextBlock
            {
                Text = VisHelper.Loc("Vis.TransferDesc"), FontSize = 11, Foreground = Brush.Parse("#558B2F"),
                VerticalAlignment = VerticalAlignment.Center
            });
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
        sp.Children.Add(
            VisHelper.SectionLabel($"{VisHelper.Loc("Vis.Ammo")} ({parts.Count} type{(parts.Count > 1 ? "s" : "")})"));

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
        => VisHelper.BuildReverseRefsPanel(am.EntityId);

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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(bm), Padding = new Thickness(8) };
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
            Child = new TextBlock
                { Text = typeLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
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
            root.Children.Add(new TextBlock
            {
                Text = $"\"{quote}\"", FontSize = 10, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#888"),
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
            });
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
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {bm.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });

        var (typeLabel, typeBg, typeFg) = GetTypeBadge(bm);
        idRow.Children.Add(new Border
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
                    new TextBlock
                    {
                        Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg)
                    }
                }
            }
        });
        VisHelper.AddModBadge(bm, idRow);
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (!string.IsNullOrWhiteSpace(bm.StrId))
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#F3E5F5"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = bm.StrId, FontSize = 10, Foreground = Brush.Parse("#6A1B9A") }
            });

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
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                {
                    Text = $"{VisHelper.Loc("Vis.BattleMove.Flags")}: {string.Join(" · ", flags)}", FontSize = 10,
                    Foreground = Brush.Parse("#283593")
                }
            });
        identity.Children.Add(infoRow);

        // --- name ---
        identity.Children.Add(new TextBlock
        {
            Text = bm.Subject ?? bm.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(bm.Notes))
            identity.Children.Add(new TextBlock
            {
                Text = bm.Notes, FontSize = 12, Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap
            });

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
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

        // Fatigue — display as StatBar (range typically -5 to 5, use maxAbs=5)
        var fatigueMax = Math.Max(Math.Abs(bm.Fatigue), 5.0);
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Fatigue"), $"{bm.Fatigue:F1}",
            Math.Clamp(Math.Abs(bm.Fatigue) / fatigueMax, 0.05, 1.0),
            bm.Fatigue > 0 ? "#C62828" : "#2E7D32"));

        // AI Order — display as StatBar (range typically 0-1)
        bars.Children.Add(VisHelper.StatBar(VisHelper.Loc("Vis.Order"), $"{bm.Order:F2}",
            Math.Clamp(bm.Order, 0.05, 1.0), "#1565C0"));

        // Key-value rows for non-normalized stats — equal-width grid
        var kvItems = new List<(string label, string value)>();
        var rangeText = bm.MinRange == -1 && bm.MaxRange == -1 ? "All"
            : bm.MinRange == 0 ? $"0–{bm.MaxRange}" : $"{bm.MinRange}–{bm.MaxRange}";
        kvItems.Add((VisHelper.Loc("Vis.Range"), rangeText));
        kvItems.Add((VisHelper.Loc("Vis.Exposure"), $"them {FmtExp(bm.SeeThem)} / us {FmtExp(bm.SeeUs)}"));
        if (bm.MinCharges > 0)
            kvItems.Add((VisHelper.Loc("Vis.MinCharges"), $"{bm.MinCharges}"));
        if (!string.IsNullOrWhiteSpace(bm.ChanceType) && bm.ChanceType != "0,0,0")
            kvItems.Add(("Chance Type", bm.ChanceType));
        if (kvItems.Count > 0)
        {
            var kvGrid = new Grid { Margin = new Thickness(4, 4, 4, 4) };
            foreach (var _ in kvItems)
                kvGrid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
            for (int i = 0; i < kvItems.Count; i++)
            {
                var mini = MiniKv(kvItems[i].label, kvItems[i].value);
                mini.Margin = new Thickness(i > 0 ? 8 : 0, 0, 0, 0);
                Grid.SetColumn(mini, i);
                kvGrid.Children.Add(mini);
            }
            bars.Children.Add(kvGrid);
        }

        sp.Children.Add(VisHelper.Card(bars));
        return sp;
    }

    // ═══════════════ Text panels ═══════════════

    private static Control BuildTextPanel(string label, string text, int maxLen, string? color = null)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(label));
        var display = text.Length > maxLen ? text[..maxLen] + "..." : text;
        sp.Children.Add(VisHelper.Card(new TextBlock
        {
            Text = display, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(color ?? "#333")
        }));
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
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.UsPreCond"), bm.UsPreConditions, ",",
            nameof(BattleMove.UsPreConditions), "#FFF3E0", "#E65100");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.ThemPreCond"), bm.ThemPreConditions, ",",
            nameof(BattleMove.ThemPreConditions), "#FFF3E0", "#E65100");
        // Applied on success
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.UsRequired"), bm.UsConditions, "],[",
            nameof(BattleMove.UsConditions), "#FCE4EC", "#C62828");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.ThemRequired"), bm.ThemConditions, "],[",
            nameof(BattleMove.ThemConditions), "#FCE4EC", "#C62828");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.SelfEffects"), bm.PairConditions, "],[",
            nameof(BattleMove.PairConditions), "#E8EAF6", "#283593");
        // Applied on fail
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.UsFail"), bm.UsFailConditions, "],[",
            nameof(BattleMove.UsFailConditions), "#F5F5F5", "#999");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.ThemFail"), bm.ThemFailConditions, "],[",
            nameof(BattleMove.ThemFailConditions), "#F5F5F5", "#999");
        AddCondGroup(VisHelper.Loc("Vis.BattleMove.PairFail"), bm.PairFailConditions, "],[",
            nameof(BattleMove.PairFailConditions), "#F5F5F5", "#999");

        if (!hasAny)
            sp.Children.Add(new TextBlock
                { Text = "(No conditions)", FontSize = 11, Foreground = Brush.Parse("#999") });
        return sp;
    }

    // ═══════════════ Reverse References ═══════════════

    private static Control BuildReverseRefsPanel(BattleMove bm)
        => VisHelper.BuildReverseRefsPanel(bm.EntityId);

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

        int Count(string raw, string sep) => string.IsNullOrWhiteSpace(raw)
            ? 0
            : raw.Split(sep).Select(s => s.Trim()).Count(s => s.Length > 0);

        void Add(string label, int n)
        {
            if (n > 0) counts.Add((label, n));
        }

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

    private static StackPanel MiniKv(string label, string value) => new()
    {
        Spacing = 1, Children =
        {
            new TextBlock { Text = label, FontSize = 9, Foreground = Brush.Parse("#999") },
            new TextBlock
            {
                Text = value, FontSize = 12, FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#333")
            }
        }
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(ht), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ht));
        root.Children.Add(BuildTerrainPanel(ht));
        root.Children.Add(BuildLightPanel(ht));
        root.Children.Add(BuildRefsPanel(ht));
        root.Children.Add(BuildReverseRefsPanel(ht));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not HexType ht) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var passLabel = ht.Passable == PassableType.Passable
            ? VisHelper.Loc("Vis.Passable")
            : VisHelper.Loc("Vis.Blocked");
        var passBg = ht.Passable == PassableType.Passable ? "#E8F5E9" : "#FFEBEE";
        var passFg = ht.Passable == PassableType.Passable ? "#2E7D32" : "#C62828";
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(passBg), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
                { Text = passLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(passFg) }
        });
        root.Children.Add(new TextBlock
        {
            Text = ht.Subject ?? ht.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.TerrainMovement")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.MovementCost"), $"{ht.TerrainCost} AP", null),
            (VisHelper.Loc("Vis.VisibilityLabel"), $"{ht.VizIncrease - ht.VizLimiter}", null),
            (VisHelper.Loc("Vis.EncounterRange"), $"{ht.MinRange}–{ht.MaxRange}", null)
        }));

        return root;
    }

    private static Control BuildHeroHeader(HexType ht)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ht.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(ht, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var passLabel = ht.Passable == PassableType.Passable
            ? VisHelper.Loc("Vis.Passable")
            : VisHelper.Loc("Vis.Blocked");
        var passBg = ht.Passable == PassableType.Passable ? "#E8F5E9" : "#FFEBEE";
        var passFg = ht.Passable == PassableType.Passable ? "#2E7D32" : "#C62828";
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(passBg), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = passLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(passFg) }
        });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = ht.Subject ?? ht.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(ht.Description))
            identity.Children.Add(new TextBlock
                { Text = ht.Description, FontSize = 12, Foreground = Brush.Parse("#888") });

        var statRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.MovementCost")}: {ht.TerrainCost} AP", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text =
                $"{VisHelper.Loc("Vis.VisibilityLabel")}: {ht.VizIncrease - ht.VizLimiter} (+{ht.VizIncrease}, -{ht.VizLimiter})",
            FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.EncounterRange")}: {ht.MinRange}–{ht.MaxRange}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildTerrainPanel(HexType ht)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.TerrainMovement")));

        var costColor = ht.TerrainCost <= 1 ? "#2E7D32" : ht.TerrainCost <= 3 ? "#E65100" : "#C62828";
        var vizNet = ht.VizIncrease - ht.VizLimiter;
        var vizColor = vizNet >= 0 ? "#2E7D32" : "#C62828";
        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.MovementCost"), $"{ht.TerrainCost} AP", costColor),
            (VisHelper.Loc("Vis.VisibilityLabel"), $"{vizNet:+#;-#;0} (+{ht.VizIncrease}, -{ht.VizLimiter})", vizColor),
        };
        if (ht.MinRange > 0 || ht.MaxRange > 0)
            cells.Add((VisHelper.Loc("Vis.EncounterRange"), $"{ht.MinRange}–{ht.MaxRange} {VisHelper.Loc("Vis.Tiles")}",
                "#1565C0"));
        if (ht.CampItems != 5)
        {
            var campLabel = ht.CampItems switch
            {
                0 => VisHelper.Loc("Vis.CampItemNone"), 1 => VisHelper.Loc("Vis.CampItemSparse"),
                2 => VisHelper.Loc("Vis.CampItemModerate"), 3 => VisHelper.Loc("Vis.CampItemAbundant"),
                4 => VisHelper.Loc("Vis.CampItemRich"), 5 => VisHelper.Loc("Vis.CampItemDefault"),
                _ => $"Lv.{ht.CampItems}"
            };
            cells.Add((VisHelper.Loc("Vis.CampItemsLabel"), campLabel, "#E65100"));
        }

        sp.Children.Add(VisHelper.CreatureStatGrid(cells));
        return sp;
    }

    private static Control BuildLightPanel(HexType ht)
    {
        if (string.IsNullOrWhiteSpace(ht.LightLevels)) return new StackPanel();
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.LightLevels")));
        var lightNames = new[] { "Dawn", "Morning", "Noon", "Afternoon", "Dusk", "Midnight" };
        var levels = ht.LightLevels.Split(',').Select(s => s.Trim()).ToList();
        var parsedLevels = levels.Select(s =>
            double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)
                ? (double?)v
                : null).ToList();
        var maxLight = parsedLevels.Where(x => x.HasValue).DefaultIfEmpty(1.0).Max() ?? 1.0;
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star),
                new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star)
            },
            Margin = new Thickness(4, 0)
        };
        grid.RowDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < lightNames.Length; i++)
        {
            var col = new StackPanel { Margin = new Thickness(2, 4) };
            col.Children.Add(new TextBlock
            {
                Text = lightNames[i], FontSize = 9, Foreground = Brush.Parse("#999"),
                TextAlignment = TextAlignment.Center
            });
            var valStr = i < levels.Count ? levels[i] : "?";
            var val = i < parsedLevels.Count ? parsedLevels[i] : null;
            // Heatmap: red (0) → yellow (0.5) → green (1.0+)
            var ratio = val.HasValue && maxLight > 0 ? Math.Clamp(val.Value / maxLight, 0.0, 1.0) : 0.0;
            int r = (int)((1 - ratio) * 198 + ratio * 46); // 198→46
            int g = (int)(ratio < 0.5 ? ratio * 2 * 125 : (1 - ratio) * 2 * 125 + 125); // 0→125→0
            int bv = (int)(ratio < 0.5 ? (1 - ratio * 2) * 40 : 0); // 40→0
            var cellBg = $"#{r:X2}{g:X2}{bv:X2}";
            col.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = Brush.Parse(val.HasValue ? cellBg : "#F5F5F5"),
                Padding = new Thickness(4, 2),
                Child = new TextBlock
                {
                    Text = valStr, FontSize = 11, FontWeight = FontWeight.Medium,
                    Foreground = ratio > 0.5 ? Brushes.White : Brush.Parse("#333"), TextAlignment = TextAlignment.Center
                }
            });
            Grid.SetColumn(col, i);
            grid.Children.Add(col);
        }

        sp.Children.Add(VisHelper.Card(grid));
        return sp;
    }

    private static Control BuildRefsPanel(HexType ht)
    {
        var sp = new StackPanel { Spacing = 8 };

        void AddRef<T>(string label, string raw, string propName, string bg, string fg) where T : IEntity
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "3" || raw == "25") return;
            sp.Children.Add(VisHelper.SectionLabel(label));
            var wp = new WrapPanel();
            foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var match = ReferenceResolver.Instance.LookupRef<T>(ht, propName, seg);
                if (match is not null)
                    wp.Children.Add(VisHelper.MiniBadge(match.Subject, bg, fg,
                        () => ReferenceResolver.Instance.NavigateTo(typeof(T), match.EntityId)));
                else
                    wp.Children.Add(VisHelper.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(VisHelper.Card(wp));
        }

        AddRef<TreasureTable>(VisHelper.Loc("Vis.ScavengeLoot"), ht.TreasureId, nameof(HexType.TreasureId), "#E8F5E9",
            "#2E7D32");
        AddRef<TreasureTable>(VisHelper.Loc("Vis.InitialScavenge"), ht.ScavengeInitialId,
            nameof(HexType.ScavengeInitialId), "#E0F2F1", "#00695C");
        AddRef<TreasureTable>(VisHelper.Loc("Vis.HourlyScavenge"), ht.ScavengeItemsIdPerHour,
            nameof(HexType.ScavengeItemsIdPerHour), "#B2DFDB", "#004D40");
        AddRef<Condition>(VisHelper.Loc("Vis.OnEnterConditions"), ht.ConditionIds, nameof(HexType.ConditionIds),
            "#FCE4EC", "#C62828");

        if (ht.DefaultCampId != 517)
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.DefaultCamp")));
            var wp = new WrapPanel();
            var camp = ReferenceResolver.Instance.LookupRef<CampType>(ht, nameof(HexType.DefaultCampId),
                ht.DefaultCampId.ToString());
            if (camp is not null)
                wp.Children.Add(VisHelper.MiniBadge(camp.Subject, "#FFF3E0", "#E65100",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(CampType), camp.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{ht.DefaultCampId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(f), Padding = new Thickness(8) };
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

        root.Children.Add(new TextBlock
        {
            Text = f.Subject ?? f.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        var relationCount = string.IsNullOrWhiteSpace(f.DictFactions) ? 0 : f.DictFactions.Split(',').Length;
        var members = new List<Creature>();
        if (GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Creature), out var cl) && cl is not null)
            members = cl.OfType<Creature>().Where(c => c.Faction == f.Id.ToString()).ToList();

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Diplomacy")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Relations"), $"{relationCount}", null),
            (VisHelper.Loc("Vis.Members"), $"{members.Count}", members.Count > 0 ? "#283593" : null)
        }));

        // Show first few members
        if (members.Count > 0)
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Members")));
            var wp = new WrapPanel();
            foreach (var m in members.Take(8))
                wp.Children.Add(VisHelper.MiniBadge(m.Subject, "#E8EAF6", "#283593",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(Creature), m.EntityId)));
            if (members.Count > 8)
                wp.Children.Add(VisHelper.MiniBadge($"+{members.Count - 8} more", "#F5F5F5", "#999"));
            root.Children.Add(VisHelper.Card(wp));
        }

        return root;
    }

    private static Control BuildHeroHeader(Faction f)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {f.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(f, idRow);
        identity.Children.Add(idRow);
        identity.Children.Add(new TextBlock
        {
            Text = f.Subject ?? f.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildRelationsPanel(Faction f)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Diplomacy")));

        // Parse relations
        var relations = new Dictionary<int, int>();
        foreach (var seg in f.DictFactions.Split(','))
        {
            var parts = seg.Trim().Split('=');
            if (parts.Length < 2) continue;
            if (int.TryParse(parts[0].Trim(), out var fid) && int.TryParse(parts[1].Trim(), out var rv))
                relations[fid] = rv;
        }

        // Relation bars (vertical layout)
        var relationsStack = new StackPanel { Spacing = 3 };
        var factions = GenericDataGridHelper.GetEntities<Faction>();
        foreach (var kv in relations.OrderBy(kv => kv.Value))
        {
            var fid = kv.Key;
            var relVal = kv.Value;
            var otherName = factions.TryGetValue(fid, out var of) 
                ? (of.Subject ?? of.Name ?? $"Faction#{fid}")
                : $"#{fid}";
            var relDesc = relVal >= 100 ? "Allied" :
                relVal >= 50 ? "Friendly" :
                relVal >= 0 ? "Neutral" :
                relVal >= -50 ? "Hostile" : "Enemy";

            // Custom relation row with full faction name visible
            var absRatio = Math.Clamp(Math.Abs(relVal) / 100.0, 0.08, 1.0);
            var isNeg = relVal < 0;
            var posColor = "#2E7D32";
            var negColor = "#C62828";

            var row = new Grid { Height = 26, Margin = new Thickness(0, 1) };
            row.ColumnDefinitions.Add(new(1, GridUnitType.Star)); // faction name (auto-expand)
            row.ColumnDefinitions.Add(new(GridLength.Auto)); // value text
            row.ColumnDefinitions.Add(new(1, GridUnitType.Star)); // left fill
            row.ColumnDefinitions.Add(new(3, GridUnitType.Pixel)); // center zero line
            row.ColumnDefinitions.Add(new(1, GridUnitType.Star)); // right fill

            var nameTb = new TextBlock
            {
                Text = otherName, FontSize = 11, Foreground = Brush.Parse("#666"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameTb, 0);
            row.Children.Add(nameTb);

            var valTb = new TextBlock
            {
                Text = $"{relVal:+#;-#;0} ({relDesc})", FontSize = 10, FontWeight = FontWeight.Medium,
                Foreground = Brush.Parse(isNeg ? negColor : relVal > 0 ? posColor : "#999"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(valTb, 1);
            row.Children.Add(valTb);

            var center = new Border { Background = Brush.Parse("#20000000"), Margin = new Thickness(0, 4) };
            Grid.SetColumn(center, 3);
            row.Children.Add(center);

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
                Grid.SetColumn(fill, 2);
                row.Children.Add(fill);
            }
            else if (relVal > 0)
            {
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(0, 4, 4, 0),
                    Background = Brush.Parse(posColor),
                    Margin = new Thickness(0, 1, 0, 1),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = absRatio * 160, MaxWidth = 160
                };
                Grid.SetColumn(fill, 4);
                row.Children.Add(fill);
            }

            relationsStack.Children.Add(row);
        }

        sp.Children.Add(VisHelper.Card(relationsStack));

        return sp;
    }

    private static Control BuildMembersPanel(Faction f)
    {
        var sp = new StackPanel();
        if (!GenericDataGridHelper.ReferenceLookups.TryGetValue(typeof(Creature), out var creatureList) ||
            creatureList is null)
            return sp;
        var members = creatureList.OfType<Creature>().Where(c => c.Faction == f.Id.ToString()).ToList();
        if (members.Count == 0) return sp;

        sp.Children.Add(VisHelper.SectionLabel($"{VisHelper.Loc("Vis.Members")} ({members.Count})"));
        var wp = new WrapPanel();
        foreach (var m in members)
            wp.Children.Add(VisHelper.MiniBadge(m.Subject, "#E8EAF6", "#283593",
                () => ReferenceResolver.Instance.NavigateTo(typeof(Creature), m.EntityId)));
        sp.Children.Add(VisHelper.Card(wp));
        return sp;
    }

    private static Control BuildReverseRefsPanel(Faction f)
        => VisHelper.BuildReverseRefsPanel(f.EntityId);
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(ing), Padding = new Thickness(8) };
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

        root.Children.Add(new TextBlock
        {
            Text = ing.Subject ?? ing.Name, FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
        });

        // Resolve and show required/forbidden property names
        if (!string.IsNullOrWhiteSpace(ing.RequiredProps) || !string.IsNullOrWhiteSpace(ing.ForbidProps))
        {
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Properties")));

            if (!string.IsNullOrWhiteSpace(ing.RequiredProps))
            {
                var wp = new WrapPanel();
                var reqIds = ing.RequiredProps.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0);
                foreach (var id in reqIds)
                {
                    var prop = ReferenceResolver.Instance.LookupRef<ItemProp>(ing, nameof(Ingredient.RequiredProps),
                        id);
                    var name = prop?.PropertyName ?? $"#{id}";
                    wp.Children.Add(VisHelper.MiniBadge($"{VisHelper.Loc("Vis.Required")}: {name}", "#E8F5E9",
                        "#2E7D32",
                        prop is not null
                            ? () => ReferenceResolver.Instance.NavigateTo(typeof(ItemProp), prop.EntityId)
                            : null));
                }

                root.Children.Add(VisHelper.Card(wp));
            }

            if (!string.IsNullOrWhiteSpace(ing.ForbidProps))
            {
                var wp = new WrapPanel();
                var forbIds = ing.ForbidProps.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0);
                foreach (var id in forbIds)
                {
                    var prop = ReferenceResolver.Instance.LookupRef<ItemProp>(ing, nameof(Ingredient.ForbidProps), id);
                    var name = prop?.PropertyName ?? $"#{id}";
                    wp.Children.Add(VisHelper.MiniBadge($"{VisHelper.Loc("Vis.Forbidden")}: {name}", "#FFEBEE",
                        "#C62828",
                        prop is not null
                            ? () => ReferenceResolver.Instance.NavigateTo(typeof(ItemProp), prop.EntityId)
                            : null));
                }

                root.Children.Add(VisHelper.Card(wp));
            }
        }

        root.Children.Add(BuildReversePanel(ing));
        return root;
    }

    private static Control BuildHeroHeader(Ingredient ing)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ing.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(ing, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = ing.Subject ?? ing.Name, FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
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

        AddProps($"{VisHelper.Loc("Vis.Required")} {VisHelper.Loc("Vis.Properties")}", ing.RequiredProps,
            nameof(Ingredient.RequiredProps), "#E8F5E9", "#2E7D32");
        AddProps($"{VisHelper.Loc("Vis.Forbidden")} {VisHelper.Loc("Vis.Properties")}", ing.ForbidProps,
            nameof(Ingredient.ForbidProps), "#FFEBEE", "#C62828");
        return sp;
    }

    private static Control BuildReversePanel(Ingredient ing)
        => VisHelper.BuildReverseRefsPanel(ing.EntityId);
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(ip), Padding = new Thickness(8) };
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
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ip.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(ip, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = ip.PropertyName ?? ip.Subject, FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildReversePanel(ItemProp ip)
        => VisHelper.BuildReverseRefsPanel(ip.EntityId);
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(et), Padding = new Thickness(8) };
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
        if (et.LocBased) types.Add(VisHelper.Loc("Vis.LocType"));
        if (et.DateBased) types.Add(VisHelper.Loc("Vis.DateType"));
        if (et.HexBased) types.Add(VisHelper.Loc("Vis.HexType"));
        if (et.Unique) types.Add(VisHelper.Loc("Vis.UniqueType"));
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = types.Count > 0 ? string.Join(" + ", types) : VisHelper.Loc("Vis.Manual"), FontSize = 10,
                FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#283593")
            }
        });
        root.Children.Add(new TextBlock
        {
            Text = et.Subject ?? et.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.TriggerLabel")));
        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Chance"), $"{et.Chance:P0}", et.Chance > 0 ? "#1565C0" : "#999"),
        };
        if (et.EncounterId != 0)
            cells.Add((VisHelper.Loc("Vis.EncounterRef"), $"#{et.EncounterId}", "#2E7D32"));
        root.Children.Add(VisHelper.CreatureStatGrid(cells));

        return root;
    }

    private static Control BuildHeroHeader(EncounterTrigger et)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {et.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        var types2 = new List<string>();
        if (et.LocBased) types2.Add(VisHelper.Loc("Vis.LocType"));
        if (et.DateBased) types2.Add(VisHelper.Loc("Vis.DateType"));
        if (et.HexBased) types2.Add(VisHelper.Loc("Vis.HexType"));
        if (et.Unique) types2.Add(VisHelper.Loc("Vis.UniqueType"));
        if (et.AIPassable) types2.Add(VisHelper.Loc("Vis.AIType"));
        if (types2.Count > 0)
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = string.Join(" · ", types2), FontSize = 10, Foreground = Brush.Parse("#283593") }
            });
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"{VisHelper.Loc("Vis.Chance")}: {et.Chance:P0}", FontSize = 10,
                Foreground = Brush.Parse("#E65100")
            }
        });
        VisHelper.AddModBadge(et, badgeRow);
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock
        {
            Text = et.Subject ?? et.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(et.Area))
            identity.Children.Add(new TextBlock
                { Text = $"{VisHelper.Loc("Vis.Area")}: {et.Area}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (!string.IsNullOrWhiteSpace(et.DateMin) || !string.IsNullOrWhiteSpace(et.DateMax))
            identity.Children.Add(new TextBlock
            {
                Text = $"{VisHelper.Loc("Vis.DateRange")}: {et.DateMin} – {et.DateMax}", FontSize = 11,
                Foreground = Brush.Parse("#666")
            });

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildRefsPanel(EncounterTrigger et)
    {
        var sp = new StackPanel { Spacing = 8 };
        if (et.EncounterId != 0)
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.EncounterRef")));
            var wp = new WrapPanel();
            var enc = ReferenceResolver.Instance.LookupRef<Encounter>(et, nameof(EncounterTrigger.EncounterId),
                et.EncounterId.ToString());
            if (enc is not null)
                wp.Children.Add(VisHelper.MiniBadge(enc.Subject, "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), enc.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge($"#{et.EncounterId}", "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(et.HexTypes))
        {
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.HexTypesRef")));
            var wp = new WrapPanel();
            foreach (var seg in et.HexTypes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var ht = ReferenceResolver.Instance.LookupRef<HexType>(et, nameof(EncounterTrigger.HexTypes), seg);
                if (ht is not null)
                    wp.Children.Add(VisHelper.MiniBadge(ht.Subject, "#E0F2F1", "#00695C",
                        () => ReferenceResolver.Instance.NavigateToByKeyFor<HexType>(ht.Id, et)));
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
        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Chance"), $"{et.Chance:P0}", et.Chance > 0 ? "#1565C0" : "#999"),
            (VisHelper.Loc("Vis.Unique"), et.Unique ? VisHelper.Loc("Vis.YesOnce") : VisHelper.Loc("Vis.NoRepeat"),
                et.Unique ? "#E65100" : "#999"),
            (VisHelper.Loc("Vis.AIPassable"), et.AIPassable ? VisHelper.Loc("Vis.Yes") : VisHelper.Loc("Vis.No"),
                et.AIPassable ? "#2E7D32" : "#999"),
        };
        if (!string.IsNullOrWhiteSpace(et.Area))
            cells.Add((VisHelper.Loc("Vis.Area"), et.Area, "#2E7D32"));
        if (!string.IsNullOrWhiteSpace(et.DateMin))
            cells.Add((VisHelper.Loc("Vis.DateRange"), $"{et.DateMin} – {et.DateMax}", "#6A1B9A"));
        sp.Children.Add(VisHelper.CreatureStatGrid(cells));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(ct), Padding = new Thickness(8) };
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
                Child = new SymbolIcon
                {
                    Symbol = Symbol.Home, FontSize = 32, Foreground = Brush.Parse("#999"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            });
        }

        root.Children.Add(imgStack);

        root.Children.Add(new TextBlock
        {
            Text = ct.Description ?? $"Camp #{ct.Id}", FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
        });
        if (!string.IsNullOrWhiteSpace(ct.Capacities))
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = ct.Capacities, FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.CampStats")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.SleepQuality"), $"{ct.SleepQuality:P0}",
                ct.SleepQuality > 0 ? "#2E7D32" : ct.SleepQuality < 0 ? "#C62828" : null),
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
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.Home, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ct.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(ct, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = ct.Capacities, FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = ct.Description ?? $"Camp #{ct.Id}", FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        var statRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.SleepQuality")}: {ct.SleepQuality:P0}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.HealPerHour")}: {ct.HealPerHourMod:P0}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.Alertness")}: {ct.Alertness:P0}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
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
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Contents")));
        var tt = ReferenceResolver.Instance.LookupRef<TreasureTable>(ct, nameof(CampType.TreasureId), ct.TreasureId);
        if (tt is null || string.IsNullOrWhiteSpace(tt.Treasures))
        {
            var wp = new WrapPanel();
            if (tt is not null)
                wp.Children.Add(VisHelper.MiniBadge(tt.Name, "#E8F5E9", "#2E7D32",
                    () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId)));
            else
                wp.Children.Add(VisHelper.MiniBadge(ct.TreasureId, "#F5F5F5", "#999"));
            sp.Children.Add(VisHelper.Card(wp));
            return sp;
        }

        // TreasureTable reference label above contents (100% — camp always spawns this TT)
        var ttBadge = TreasureTableEntityVisualizer.BuildItemRow(tt.Name, "TT", "#E8EAF6", "#283593",
            () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), tt.EntityId),
            1.0, 1.0, "");
        ttBadge.Margin = new Thickness(0, 0, 0, 8);
        sp.Children.Add(ttBadge);

        var itemTypes = GenericDataGridHelper.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);

        var orGroups = tt.Treasures.Split('|');
        foreach (var orSeg in orGroups)
        {
            var items = orSeg.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0 && s.Contains('x')).ToList();
            if (items.Count == 0) continue;

            // Parse all items first to calculate total weight for this OR group
            var parsed = new List<(string itemId, double weight, string qtyRange)>();
            double totalWeight = 0;
            foreach (var seg in items)
            {
                var parts = seg.Split('x');
                if (parts.Length < 2) continue;
                var itemId = parts[0].Trim();
                var weightStr = parts[1].Trim();
                var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
                var weight = double.TryParse(weightStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 1.0;
                totalWeight += weight;
                parsed.Add((itemId, weight, qtyRange));
            }

            var cardStack = new StackPanel { Spacing = 6 };
            if (orGroups.Length > 1)
                cardStack.Children.Add(new TextBlock
                {
                    Text = VisHelper.Loc("Vis.ORGroup"), FontSize = 10, FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#5C6BC0"), Margin = new Thickness(0, 0, 0, 4)
                });

            foreach (var (itemId, weight, qtyRange) in parsed)
            {
                var actualProb = totalWeight > 0 ? weight / totalWeight : 1.0 / parsed.Count;

                if (itemTypes.TryGetValue(itemId, out var matched))
                {
                    var itemRow = TreasureTableEntityVisualizer.BuildItemRow(matched.Description, "ItemType", "#E0F2F1", "#00695C",
                        () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), matched.EntityId),
                        weight, actualProb, qtyRange);
                    cardStack.Children.Add(itemRow);
                }
                else
                {
                    var nested = ReferenceResolver.Instance.LookupRef<TreasureTable>(ct,
                        nameof(CampType.TreasureId), itemId);
                    if (nested is not null)
                    {
                        var nestedHeader = TreasureTableEntityVisualizer.BuildItemRow(nested.Name, "TT", "#E8EAF6", "#283593",
                            () => ReferenceResolver.Instance.NavigateTo(typeof(TreasureTable), nested.EntityId),
                            weight, actualProb, qtyRange);

                        // Recursively expand nested TT contents (indented, collapsible via click on parent row)
                        var nestedItems = TreasureTableEntityVisualizer.BuildNestedItems(nested, itemTypes, 1);
                        if (nestedItems is not null)
                        {
                            var isExpanded = true;
                            nestedItems.IsVisible = true;
                            nestedHeader.Cursor = new Cursor(StandardCursorType.Hand);
                            nestedHeader.PointerPressed += (_, e) =>
                            {
                                if ((e.KeyModifiers & KeyModifiers.Control) == 0)
                                {
                                    isExpanded = !isExpanded;
                                    nestedItems.IsVisible = isExpanded;
                                }
                            };
                        }
                        cardStack.Children.Add(nestedHeader);
                        if (nestedItems is not null)
                            cardStack.Children.Add(nestedItems);
                    }
                    else
                    {
                        var unknownRow = TreasureTableEntityVisualizer.BuildItemRow(itemId, null, "#F5F5F5", "#999", null,
                            weight, actualProb, qtyRange);
                        cardStack.Children.Add(unknownRow);
                    }
                }
            }

            sp.Children.Add(VisHelper.Card(cardStack));
        }

        return sp;
    }

    // ═══════════════ Reverse References ═══════════════

    private static Control BuildReverseRefsPanel(CampType ct)
        => VisHelper.BuildReverseRefsPanel(ct.EntityId);
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(cp), Padding = new Thickness(8) };
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

        root.Children.Add(new TextBlock
        {
            Text = cp.Subject ?? cp.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        if (cp.Degrade)
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = VisHelper.Loc("Vis.Degradeable"), FontSize = 10, FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#E65100")
                }
            });

        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.ConsumptionRates")));
        var cells = new List<(string, string, string?)>();
        if (cp.PerUse > 0)
            cells.Add((VisHelper.Loc("Vis.PerUse"), $"{cp.PerUse:F1}", "#C62828"));
        if (cp.PerHour > 0)
            cells.Add((VisHelper.Loc("Vis.PerHour"), $"{cp.PerHour:F1}", "#E65100"));
        if (cp.PerHourEquipped > 0)
            cells.Add((VisHelper.Loc("Vis.PerHourEquipped"), $"{cp.PerHourEquipped:F1}", "#FB8C00"));
        if (cp.PerHex > 0)
            cells.Add((VisHelper.Loc("Vis.PerHex"), $"{cp.PerHex:F1}", "#6A1B9A"));
        if (cells.Count == 0)
            root.Children.Add(new TextBlock
                { Text = "(no consumption)", FontSize = 10, Foreground = Brush.Parse("#999") });
        else
            root.Children.Add(VisHelper.CreatureStatGrid(cells));
        root.Children.Add(VisHelper.Kv(VisHelper.Loc("Vis.ItemLabel"), cp.ItemId, 40));

        return root;
    }

    private static Control BuildHeroHeader(ChargeProfile cp)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {cp.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        if (cp.Degrade)
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = VisHelper.Loc("Vis.Degradeable"), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });
        VisHelper.AddModBadge(cp, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = cp.Subject ?? cp.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        identity.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.ItemLabel")}: {cp.ItemId}", FontSize = 12, Foreground = Brush.Parse("#888")
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(ChargeProfile cp)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.ConsumptionRates")));
        var cells = new List<(string, string, string?)>();
        if (cp.PerUse > 0)
            cells.Add((VisHelper.Loc("Vis.PerUse"), $"{cp.PerUse:F2}", "#C62828"));
        if (cp.PerHour > 0)
            cells.Add((VisHelper.Loc("Vis.PerHourGrowth"), $"{cp.PerHour:F2}", "#E65100"));
        if (cp.PerHourEquipped > 0)
            cells.Add((VisHelper.Loc("Vis.PerHourEquippedDrain"), $"{cp.PerHourEquipped:F2}", "#FB8C00"));
        if (cp.PerHex > 0)
            cells.Add((VisHelper.Loc("Vis.PerHex"), $"{cp.PerHex:F2}", "#6A1B9A"));
        if (cells.Count == 0)
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = "(no consumption)", FontSize = 10, Foreground = Brush.Parse("#999") }));
        else
            sp.Children.Add(VisHelper.CreatureStatGrid(cells));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(ct), Padding = new Thickness(8) };
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
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ct.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        VisHelper.AddModBadge(ct, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = ct.Subject ?? ct.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
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
            wp.Children.Add(VisHelper.MiniBadge(subject, "#E3F2FD", "#1565C0",
                () => ReferenceResolver.Instance.NavigateTo(typeof(ItemType), eid)));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(cs), Padding = new Thickness(8) };
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

        root.Children.Add(new TextBlock
        {
            Text = cs.Subject ?? cs.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
                { Text = $"({cs.X}, {cs.Y}) · {cs.Min}–{cs.Max}", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });

        var (totalW, proportion) = GetWeightInfo(cs);
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Spawn")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({cs.X}, {cs.Y})", null),
            (VisHelper.Loc("Vis.Count"), $"{cs.Min}–{cs.Max}", null),
            (VisHelper.Loc("Vis.Weight"), $"{cs.Weight:F2} ({proportion:P0})", null)
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
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {cs.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = $"({cs.X}, {cs.Y}) · {cs.Min}–{cs.Max}", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        VisHelper.AddModBadge(cs, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = cs.Subject ?? cs.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        var (totalW, proportion) = GetWeightInfo(cs);
        identity.Children.Add(new TextBlock
        {
            Text = $"Weight: {cs.Weight:F2} ({proportion:P0} of total {totalW:F1} at this location)", FontSize = 12,
            Foreground = Brush.Parse("#888")
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(CreatureSource cs)
    {
        var (totalW, proportion) = GetWeightInfo(cs);
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Spawn")));

        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Weight"), $"{cs.Weight:F2} ({proportion:P0})", "#1565C0"),
            (VisHelper.Loc("Vis.Position"), $"({cs.X}, {cs.Y})", null),
            (VisHelper.Loc("Vis.Count"), $"{cs.Min}–{cs.Max}", cs.Max > 0 ? "#1565C0" : null),
        };
        sp.Children.Add(VisHelper.CreatureStatGrid(cells));
        return sp;
    }

    private static Control BuildCreaturePanel(CreatureSource cs)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Creature")));
        var wp = new WrapPanel();
        var creature =
            ReferenceResolver.Instance.LookupRef<Creature>(cs, nameof(CreatureSource.CreatureId), cs.CreatureId);
        if (creature is not null)
            wp.Children.Add(VisHelper.MiniBadge(creature.Subject, "#E8EAF6", "#283593",
                () => ReferenceResolver.Instance.NavigateTo(typeof(Creature), creature.EntityId)));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(dp), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(dp));
        root.Children.Add(BuildStatsPanel(dp));
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
            imgStack.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 }
            });
        root.Children.Add(imgStack);

        root.Children.Add(new TextBlock
        {
            Text = !string.IsNullOrWhiteSpace(dp.Image)
                ? dp.Image
                : (dp.Subject ?? $"{VisHelper.Loc("Vis.DMCPlace")} #{dp.Id}"),
            FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Location")));
        root.Children.Add(VisHelper.BuildStatCard(new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({dp.X}, {dp.Y})", null),
            (VisHelper.Loc("Vis.Encounter"), $"#{dp.EncounterId}", dp.EncounterId != 1 ? null : "#999")
        }));

        return root;
    }

    private static Control BuildHeroHeader(DmcPlace dp)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = VisHelper.LoadImage(dp.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.Building, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {dp.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = $"({dp.X}, {dp.Y})", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        VisHelper.AddModBadge(dp, infoRow);
        identity.Children.Add(infoRow);
        identity.Children.Add(new TextBlock
        {
            Text = !string.IsNullOrWhiteSpace(dp.Image)
                ? dp.Image
                : (dp.Subject ?? $"{VisHelper.Loc("Vis.DMCPlace")} #{dp.Id}"),
            FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(dp.Image))
            identity.Children.Add(new TextBlock
                { Text = $"{VisHelper.Loc("Vis.Icon")}: {dp.Image}", FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(DmcPlace dp)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Location")));
        var stats = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({dp.X}, {dp.Y})", null),
        };
        sp.Children.Add(VisHelper.BuildStatCard(stats));
        return sp;
    }

    private static Control BuildRefsPanel(DmcPlace dp)
    {
        var sp = new StackPanel();
        if (dp.EncounterId == 1) return sp;
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Encounter")));
        var wp = new WrapPanel();
        var enc = ReferenceResolver.Instance.LookupRef<Encounter>(dp, nameof(DmcPlace.EncounterId),
            dp.EncounterId.ToString());
        if (enc is not null)
            wp.Children.Add(VisHelper.MiniBadge(enc.Subject, "#E8F5E9", "#2E7D32",
                () => ReferenceResolver.Instance.NavigateTo(typeof(Encounter), enc.EntityId)));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(bh), Padding = new Thickness(8) };
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
        var shopLabel = bh.Buys ? VisHelper.Loc("Vis.ShopBuysLabel") : VisHelper.Loc("Vis.ShopSellsLabel");
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
                { Text = shopLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#2E7D32") }
        });
        root.Children.Add(new TextBlock
        {
            Text = bh.Subject ?? $"Barter Hex #{bh.Id}", FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center
        });

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
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {bh.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(bh.Buys ? "#E8F5E9" : "#FCE4EC"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = bh.Buys ? VisHelper.Loc("Vis.ShopBuys") : VisHelper.Loc("Vis.ShopSells"), FontSize = 10,
                FontWeight = FontWeight.Bold, Foreground = Brush.Parse(bh.Buys ? "#2E7D32" : "#C62828")
            }
        });
        VisHelper.AddModBadge(bh, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = bh.Subject ?? $"Barter Hex #{bh.Id}", FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        identity.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.Position")}: ({bh.X}, {bh.Y})", FontSize = 12, Foreground = Brush.Parse("#888")
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(BarterHex bh)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.ShopInfo")));

        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({bh.X}, {bh.Y})", null),
            (VisHelper.Loc("Vis.Buys"), bh.Buys ? VisHelper.Loc("Vis.Yes") : VisHelper.Loc("Vis.No"),
                bh.Buys ? "#2E7D32" : "#999"),
        };
        sp.Children.Add(VisHelper.CreatureStatGrid(cells));
        return sp;
    }

    private static Control BuildRestockPanel(BarterHex bh)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.RestockTT")));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(df), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(df));
        if (!string.IsNullOrWhiteSpace(df.Description))
        {
            var sp = new StackPanel();
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Content")));
            var desc = df.Description.Length > 2000 ? df.Description[..2000] + "..." : df.Description;
            sp.Children.Add(VisHelper.Card(new TextBlock
                { Text = desc, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
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
            root.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 }
            });
        else
            root.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#0A000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new SymbolIcon
                {
                    Symbol = Symbol.Document, FontSize = 32, Foreground = Brush.Parse("#999"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            });

        // ID badge
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"DataFile #{df.Id}", FontSize = 10, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#1565C0")
            }
        });
        root.Children.Add(new TextBlock
        {
            Text = df.Subject ?? df.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        // Value badge
        if (df.Value > 0)
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"$ {df.Value:F2}", FontSize = 10, FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#2E7D32")
                }
            });

        // Description preview
        if (!string.IsNullOrWhiteSpace(df.Description))
        {
            var desc = df.Description.Length > 200 ? df.Description[..200] + "..." : df.Description;
            root.Children.Add(VisHelper.Separator());
            root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Description")));
            root.Children.Add(new TextBlock
                { Text = desc, FontSize = 10, Foreground = Brush.Parse("#555"), TextWrapping = TextWrapping.Wrap });
        }

        return root;
    }

    private static Control BuildHeroHeader(DataFile df)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = VisHelper.LoadImage(df.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.Document, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {df.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        if (df.Value > 0)
            idRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                {
                    Text = $"$ {df.Value:F2}", FontSize = 10, FontWeight = FontWeight.Bold,
                    Foreground = Brush.Parse("#2E7D32")
                }
            });
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        VisHelper.AddModBadge(df, infoRow);
        identity.Children.Add(infoRow);
        identity.Children.Add(new TextBlock
        {
            Text = df.Subject ?? df.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(df.Image))
            identity.Children.Add(new TextBlock { Text = df.Image, FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(gv), Padding = new Thickness(8) };
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

        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
                { Text = gv.Type, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") }
        });
        root.Children.Add(new TextBlock
        {
            Text = gv.Subject ?? gv.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

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
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = gv.Type, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0") }
        });
        VisHelper.AddModBadge(gv, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = gv.Subject ?? gv.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        identity.Children.Add(new TextBlock
            { Text = $"{VisHelper.Loc("Vis.Value")}: {gv.Value}", FontSize = 14, Foreground = Brush.Parse("#2E7D32") });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(GameVar gv)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Stats")));

        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Type"), gv.Type, "#1565C0"),
            (VisHelper.Loc("Vis.Value"), gv.Value, "#2E7D32"),
        };
        sp.Children.Add(VisHelper.CreatureStatGrid(cells));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(h), Padding = new Thickness(8) };
        root.Children.Add(VisHelper.BuildExpander(VisHelper.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(h));
        if (!string.IsNullOrWhiteSpace(h.HeadlineText))
        {
            var sp = new StackPanel();
            sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.HeadlineText")));
            var text = h.HeadlineText.Length > 2000 ? h.HeadlineText[..2000] + "..." : h.HeadlineText;
            sp.Children.Add(VisHelper.Card(new TextBlock
            {
                Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333333"),
                FontWeight = FontWeight.Medium
            }));
            root.Children.Add(sp);
        }

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    public Control BuildOverview(IEntity entity)
    {
        if (entity is not Headline h) return new TextBlock { Text = "..." };
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        root.Children.Add(new TextBlock
        {
            Text = $"News #{h.Id}", FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        if (!string.IsNullOrWhiteSpace(h.HeadlineText))
        {
            var preview = h.HeadlineText.Length > 150 ? h.HeadlineText[..150] + "..." : h.HeadlineText;
            root.Children.Add(new TextBlock
            {
                Text = $"\"{preview}\"", FontSize = 10, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            });
        }

        return root;
    }

    private static Control BuildHeroHeader(Headline h)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 5,
                Children =
                {
                    new SymbolIcon { Symbol = Symbol.News, FontSize = 11, Foreground = Brush.Parse("#1565C0") },
                    new TextBlock
                    {
                        Text = $"ID: {h.Id}", FontSize = 11, FontWeight = FontWeight.Bold,
                        Foreground = Brush.Parse("#1565C0")
                    }
                }
            }
        });
        var len = string.IsNullOrWhiteSpace(h.HeadlineText) ? 0 : h.HeadlineText.Length;
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = $"{len} chars", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        VisHelper.AddModBadge(h, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
            { Text = $"News #{h.Id}", FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(fh), Padding = new Thickness(8) };
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

        // Protected area badge
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 4,
                Children =
                {
                    new SymbolIcon { Symbol = Symbol.Shield, FontSize = 10, Foreground = Brush.Parse("#C62828") },
                    new TextBlock
                    {
                        Text = VisHelper.Loc("Vis.ForbiddenHex"), FontSize = 10, FontWeight = FontWeight.Bold,
                        Foreground = Brush.Parse("#C62828")
                    }
                }
            }
        });
        root.Children.Add(new TextBlock
        {
            Text = fh.Subject ?? fh.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });
        if (!string.IsNullOrWhiteSpace(fh.Name))
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = fh.Name, FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });
        root.Children.Add(VisHelper.Separator());
        root.Children.Add(VisHelper.OvSectionLabel(VisHelper.Loc("Vis.Location")));
        var stats = new List<(string, string, string?)> { (VisHelper.Loc("Vis.Position"), $"({fh.X}, {fh.Y})", null) };
        if (!string.IsNullOrWhiteSpace(fh.Name))
            stats.Add((VisHelper.Loc("Vis.Faction"), fh.Name, "#C62828"));
        root.Children.Add(VisHelper.BuildStatCard(stats));
        return root;
    }

    private static Control BuildHeroHeader(ForbiddenHex fh)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {fh.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 4,
                Children =
                {
                    new SymbolIcon { Symbol = Symbol.Shield, FontSize = 10, Foreground = Brush.Parse("#C62828") },
                    new TextBlock
                        { Text = VisHelper.Loc("Vis.Forbidden"), FontSize = 10, Foreground = Brush.Parse("#C62828") }
                }
            }
        });
        VisHelper.AddModBadge(fh, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = fh.Subject ?? fh.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        identity.Children.Add(new TextBlock
        {
            Text = $"{VisHelper.Loc("Vis.Position")}: ({fh.X}, {fh.Y})", FontSize = 12, Foreground = Brush.Parse("#888")
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return VisHelper.Card(grid);
    }

    private static Control BuildStatsPanel(ForbiddenHex fh)
    {
        var sp = new StackPanel();
        sp.Children.Add(VisHelper.SectionLabel(VisHelper.Loc("Vis.Location")));
        var cells = new List<(string, string, string?)>
        {
            (VisHelper.Loc("Vis.Position"), $"({fh.X}, {fh.Y})", null),
        };
        if (!string.IsNullOrWhiteSpace(fh.Name))
            cells.Add((VisHelper.Loc("Vis.Name"), fh.Name, "#C62828"));
        sp.Children.Add(VisHelper.CreatureStatGrid(cells));
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

        var rawBody = new Border
            { IsVisible = false, Child = VisHelper.BuildRawDataTable(m), Padding = new Thickness(8) };
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
            root.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), ClipToBounds = true,
                Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 72, Height = 72 }
            });
        }
        else
        {
            root.Children.Add(new Border
            {
                Width = 72, Height = 72, CornerRadius = new CornerRadius(8), Background = Brush.Parse("#0A000000"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new SymbolIcon
                {
                    Symbol = Symbol.Map, FontSize = 32, Foreground = Brush.Parse("#999"),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            });
        }

        root.Children.Add(new TextBlock
        {
            Text = m.Subject ?? m.Name, FontSize = 14, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

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
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var bmp = VisHelper.LoadImage(m.Name);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
        else
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.Map, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {m.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        if (!string.IsNullOrWhiteSpace(m.Definition))
        {
            var defLen = m.Definition.Split(',').Length;
            idRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = $"{defLen} cells", FontSize = 10, Foreground = Brush.Parse("#283593") }
            });
        }

        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        VisHelper.AddModBadge(m, infoRow);
        identity.Children.Add(infoRow);
        identity.Children.Add(new TextBlock
        {
            Text = m.Subject ?? m.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(m.Name))
            identity.Children.Add(new TextBlock { Text = m.Name, FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
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
                Child = new Image
                {
                    Source = bmp, Stretch = Stretch.Uniform, Width = bmp.Size.Width * scale,
                    Height = bmp.Size.Height * scale
                }
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
        sp.Children.Add(VisHelper.Card(new TextBlock
        {
            Text = def, FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#555555"),
            FontFamily = "Consolas, monospace"
        }));
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
        sp.Children.Add(
            VisHelper.SectionLabel($"{VisHelper.Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

        var list = new StackPanel { Spacing = 3 };
        foreach (var (srcType, srcSubject, srcEid, _) in resolved.Take(15))
        {
            var tc = srcType == typeof(Creature) ? ("#E8EAF6", "#283593") : ("#F5F5F5", "#666");
            var row = new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#0D000000"),
                Padding = new Thickness(8, 3), Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    Children =
                    {
                        new Border
                        {
                            CornerRadius = new CornerRadius(3), Background = Brush.Parse(tc.Item1),
                            Padding = new Thickness(5, 1),
                            Child = new TextBlock
                                { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(tc.Item2) }
                        },
                        new TextBlock
                        {
                            Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            var ct = srcType;
            var ci = srcEid;
            row.PointerPressed += (_, e) =>
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0) ReferenceResolver.Instance.NavigateTo(ct, ci);
            };
            list.Children.Add(row);
        }

        if (resolved.Count > 15)
            list.Children.Add(new TextBlock
            {
                Text = $"+ {resolved.Count - 15} more...", FontSize = 10, Foreground = Brushes.Gray,
                Margin = new Thickness(4, 2)
            });
        sp.Children.Add(VisHelper.Card(list));
        return sp;
    }
}