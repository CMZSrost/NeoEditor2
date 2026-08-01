using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.EntityEditor.Services;

namespace NeoEditor.Plugins.EntityEditor.Visualizers;

public class CampTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(CampType);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    // ═══════════════ Detail ═══════════════

    public CampTypeEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not CampType ct) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(ct), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ct));
        root.Children.Add(BuildStatsPanel(ct));
        if (!string.IsNullOrWhiteSpace(ct.TreasureId) && ct.TreasureId != "3")
            root.Children.Add(BuildLootPanel(ct));
        root.Children.Add(BuildReverseRefsPanel(ct));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Hero Header ═══════════════

    private Control BuildHeroHeader(CampType ct)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var bmp = _vis.LoadImage(ct.ImageList);
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
        _vis.AddModBadge(ct, idRow);
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
            Text = $"{_vis.Loc("Vis.SleepQuality")}: {ct.SleepQuality:P0}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.HealPerHour")}: {ct.HealPerHourMod:P0}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.Alertness")}: {ct.Alertness:P0}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    // ═══════════════ Stats Panel ═══════════════

    private Control BuildStatsPanel(CampType ct)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.CampStats")));

        var bars = new StackPanel { Spacing = 6 };

        // Alertness — 0 to 1, higher = more dangerous guards
        bars.Children.Add(_vis.CenteredStatBar(_vis.Loc("Vis.Alertness"), $"{ct.Alertness:P0}",
            ct.Alertness, 1.0, negColor: "#78909C"));

        // Visibility — negative = stealth bonus, positive = exposed
        bars.Children.Add(_vis.CenteredStatBar(_vis.Loc("Vis.VisibilityMod"), $"{ct.Visibility:P0}",
            ct.Visibility, 1.0));

        // Sleep quality — -1 to 1, 0 = baseline
        bars.Children.Add(_vis.CenteredStatBar(_vis.Loc("Vis.SleepQuality"), $"{ct.SleepQuality:P0}",
            ct.SleepQuality, 1.0));

        // Temp adjust — degrees, ±5°C max range
        bars.Children.Add(_vis.CenteredStatBar(_vis.Loc("Vis.TempAdjust"), $"{ct.WetTempAdjustMod:+#;-#;0}",
            ct.WetTempAdjustMod, 5.0));

        // Heal per hour — positive = healing, bottom of the list
        bars.Children.Add(_vis.CenteredStatBar(_vis.Loc("Vis.HealPerHour"), $"{ct.HealPerHourMod:P0}",
            ct.HealPerHourMod, 1.0, negColor: "#78909C"));

        sp.Children.Add(_vis.Card(bars));
        return sp;
    }

    // ═══════════════ Loot Panel ═══════════════

    private Control BuildLootPanel(CampType ct)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Contents")));
        var tt = _vis.Resolver.LookupRef<TreasureTable>(ct, nameof(CampType.TreasureId), ct.TreasureId);
        if (tt is null || string.IsNullOrWhiteSpace(tt.Treasures))
        {
            var wp = new WrapPanel();
            if (tt is not null)
                wp.Children.Add(_refNode.BadgeForEntity<TreasureTable>(ct, tt, tt.Name, "#E8F5E9", "#2E7D32"));
            else
                wp.Children.Add(_vis.MiniBadge(ct.TreasureId, "#F5F5F5", "#999"));
            sp.Children.Add(_vis.Card(wp));
            return sp;
        }

        // TreasureTable reference label above contents (100% — camp always spawns this TT)
        var ttBadge = TreasureTableEntityVisualizer.BuildItemRow(_vis, tt.Name, "TT", "#E8EAF6", "#283593",
            _refNode.NavAction(typeof(TreasureTable), tt.EntityId),
            1.0, 1.0, "");
        ttBadge.Margin = new Thickness(0, 0, 0, 8);
        sp.Children.Add(ttBadge);

        var itemTypes = _dataTable?.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);

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
                    Text = _vis.Loc("Vis.ORGroup"), FontSize = 10, FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#5C6BC0"), Margin = new Thickness(0, 0, 0, 4)
                });

            foreach (var (itemId, weight, qtyRange) in parsed)
            {
                var actualProb = totalWeight > 0 ? weight / totalWeight : 1.0 / parsed.Count;

                if (itemTypes.TryGetValue(itemId, out var matched))
                {
                    var itemRow = TreasureTableEntityVisualizer.BuildItemRow(_vis, matched.Description, "ItemType", "#E0F2F1", "#00695C",
                        _refNode.NavAction(typeof(ItemType), matched.EntityId),
                        weight, actualProb, qtyRange);
                    cardStack.Children.Add(itemRow);
                }
                else
                {
                    var nested = _vis.Resolver.LookupRef<TreasureTable>(ct,
                        nameof(CampType.TreasureId), itemId);
                    if (nested is not null)
                    {
                        var nestedHeader = TreasureTableEntityVisualizer.BuildItemRow(_vis, nested.Name, "TT", "#E8EAF6", "#283593",
                            _refNode.NavAction(typeof(TreasureTable), nested.EntityId),
                            weight, actualProb, qtyRange);

                        // Recursively expand nested TT contents (indented, collapsible via click on parent row)
                        var nestedItems = TreasureTableEntityVisualizer.BuildNestedItems(_vis, _dataTable, nested, itemTypes, 1, _refNode);
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
                        var unknownRow = TreasureTableEntityVisualizer.BuildItemRow(_vis, itemId, null, "#F5F5F5", "#999", null,
                            weight, actualProb, qtyRange);
                        cardStack.Children.Add(unknownRow);
                    }
                }
            }

            sp.Children.Add(_vis.Card(cardStack));
        }

        return sp;
    }

    // ═══════════════ Reverse References ═══════════════

    private Control BuildReverseRefsPanel(CampType ct)
        => _vis.BuildReverseRefsPanel(ct.EntityId);
}
