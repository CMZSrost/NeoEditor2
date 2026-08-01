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
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.Services;

namespace NeoEditor.Plugins.EntityEditor.Visualizers;

public class TreasureTableEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(TreasureTable);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    /// <summary>Create with injected services.</summary>
    public TreasureTableEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            _vis.Resolver,
            _vis.Router);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not TreasureTable tt) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(tt), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(tt));
        if (!string.IsNullOrWhiteSpace(tt.Treasures))
        {
            root.Children.Add(BuildLootPanel(tt));
        }
        else
        {
            root.Children.Add(_vis.Card(new TextBlock
                { Text = _vis.Loc("Vis.Empty"), FontSize = 11, Foreground = Brush.Parse("#999") }));
        }

        root.Children.Add(BuildReverseRefsPanel(tt));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(TreasureTable tt)
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
        _vis.AddModBadge(tt, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var flags = new List<string>();
        if (tt.Nested) flags.Add(_vis.Loc("Vis.Nested"));
        if (tt.Suppress) flags.Add(_vis.Loc("Vis.Suppress"));
        if (tt.Identify) flags.Add(_vis.Loc("Vis.Identify"));
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
        return _vis.Card(grid);
    }

    private Control BuildLootPanel(TreasureTable tt)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Loot")));
        Serilog.Log.Logger.Information(
            "[TT:BuildLootPanel] TT name={Name} eid={Eid} modId={ModId} ns={Ns} nsRaw={NsRaw} Treasures={Treasures}",
            tt.Name, tt.EntityId, tt.ModId,
            NeoEditor.Helper.ReferenceParser.NormalizeNamespace(
                (_dataTable?.EntityNamespaces ?? []).TryGetValue(tt.EntityId, out var tns) ? tns : null),
            (_dataTable?.EntityNamespaces ?? []).TryGetValue(tt.EntityId, out var tnsr) ? tnsr : "(none)",
            tt.Treasures);
        var itemTypes = _dataTable?.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);

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
                var itemRow = BuildItemRow(_vis, matched.Description, "ItemType", "#E0F2F1", "#00695C",
                    _refNode.NavAction(typeof(ItemType), matched.EntityId),
                    weight, actualProb, qtyRange);
                cardStack.Children.Add(itemRow);
            }
            else
            {
                var nested = _vis.Resolver.LookupRef<TreasureTable>(tt,
                    nameof(TreasureTable.Treasures), itemId);
                if (nested is not null)
                {
                    Serilog.Log.Logger.Information(
                        "[TT:BuildLootPanel] NestedTT found: rawId={RawId} → name={Name} eid={Eid} modId={ModId} ns={Ns}",
                        itemId, nested.Name, nested.EntityId, nested.ModId,
                        NeoEditor.Helper.ReferenceParser.NormalizeNamespace(
                            (_dataTable?.EntityNamespaces ?? []).TryGetValue(nested.EntityId, out var nns) ? nns : null));
                    var nestedHeader = BuildItemRow(_vis, nested.Name, "TT", "#E8EAF6", "#283593",
                        _refNode.NavAction(typeof(TreasureTable), nested.EntityId),
                        weight, actualProb, qtyRange);

                    // Recursively expand nested TT contents (indented, collapsible via click on parent row)
                    var nestedItems = BuildNestedItems(_vis, _dataTable, nested, itemTypes, 1, _refNode);
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
                    var unknownRow = BuildItemRow(_vis, itemId, null, "#F5F5F5", "#999", null,
                        weight, actualProb, qtyRange);
                    cardStack.Children.Add(unknownRow);
                }
            }
        }

        sp.Children.Add(_vis.Card(cardStack));

        return sp;
    }

    internal static Control BuildItemRow(VisHelperService vis, string name, string? typeTag, string typeBg, string typeFg,
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
        leftStack.Children.Add(vis.MiniBadge(name, typeTag is not null ? typeBg : "#F5F5F5",
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

    internal static Control? BuildNestedItems(VisHelperService vis, IEntityLookupService dataTable,
        TreasureTable tt, Dictionary<string, ItemType> itemTypes,
        int depth, Services.RefNode? refNode = null)
    {
        if (depth > 3 || string.IsNullOrWhiteSpace(tt.Treasures)) return null;

        Serilog.Log.Logger.Information(
            "[TT:BuildNestedItems] depth={Depth} TT name={Name} eid={Eid} modId={ModId} ns={Ns} Treasures={Treasures}",
            depth, tt.Name, tt.EntityId, tt.ModId,
            NeoEditor.Helper.ReferenceParser.NormalizeNamespace(
                (dataTable?.EntityNamespaces ?? []).TryGetValue(tt.EntityId, out var tnns) ? tnns : null),
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
                var row = BuildItemRow(vis, matched.Description, null, "#E0F2F1", "#00695C",
                    refNode?.NavAction(typeof(ItemType), matched.EntityId),
                    weight, actualProb, qtyRange);
                contentPanel.Children.Add(row);
            }
            else
            {
                var nestedTt = vis.Resolver.LookupRef<TreasureTable>(tt,
                    nameof(TreasureTable.Treasures), itemId);
                if (nestedTt is not null)
                {
                    var row = BuildItemRow(vis, nestedTt.Name, "TT", "#E8EAF6", "#283593",
                        refNode?.NavAction(typeof(TreasureTable), nestedTt.EntityId),
                        weight, actualProb, qtyRange);
                    var sub = BuildNestedItems(vis, dataTable, nestedTt, itemTypes, depth + 1, refNode);
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
                    var row = BuildItemRow(vis, itemId, null, "#F5F5F5", "#999", null,
                        weight, actualProb, qtyRange);
                    contentPanel.Children.Add(row);
                }
            }
        }

        if (contentPanel.Children.Count == 0) return null;

        return contentPanel;
    }

    private Control BuildReverseRefsPanel(TreasureTable tt)
        => _vis.BuildReverseRefsPanel(tt.EntityId);
}
