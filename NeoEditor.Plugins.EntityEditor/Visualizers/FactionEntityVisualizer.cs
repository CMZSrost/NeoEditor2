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

public class FactionEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Faction);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    public FactionEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Faction f) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(f), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(f));
        if (!string.IsNullOrWhiteSpace(f.DictFactions))
            root.Children.Add(BuildRelationsPanel(f));
        root.Children.Add(BuildMembersPanel(f));
        root.Children.Add(BuildReverseRefsPanel(f));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Faction f)
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
        _vis.AddModBadge(f, idRow);
        identity.Children.Add(idRow);
        identity.Children.Add(new TextBlock
        {
            Text = f.Subject ?? f.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildRelationsPanel(Faction f)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Diplomacy")));

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
        var factions = _dataTable?.GetEntities<Faction>() ?? [];
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

        sp.Children.Add(_vis.Card(relationsStack));

        return sp;
    }

    private Control BuildMembersPanel(Faction f)
    {
        var sp = new StackPanel();
        var store = _dataTable?.BrowserStore ?? _dataTable?.ActiveMergeStore;
        if (store?.IndexService is not { } indexService) return sp;
        if (!store.ReferenceLookups.TryGetValue(typeof(Creature), out var creatureList) || creatureList is null)
            return sp;

        // Use reverse index: find all Creatures whose Faction property points to this Faction
        var rawRefs = indexService.ReverseLookup(f.EntityId);
        var creatureLookup = creatureList.OfType<Creature>()
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.ModId).First());

        var members = new List<Creature>();
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            if (propName != nameof(Creature.Faction)) continue;
            if (creatureLookup.Values.FirstOrDefault(c => c.EntityId == srcEid) is { } match)
                members.Add(match);
        }
        // Deduplicate by Id (reverse index may have multiple entries for same Creature + Faction)
        members = members.GroupBy(c => c.Id).Select(g => g.First()).ToList();

        if (members.Count == 0) return sp;

        sp.Children.Add(_vis.SectionLabel($"{_vis.Loc("Vis.Members")} ({members.Count})"));
        var wp = new WrapPanel();
        foreach (var m in members)
            wp.Children.Add(_refNode.BadgeForEntity(f, m, m.Subject, "#E8EAF6", "#283593"));
        sp.Children.Add(_vis.Card(wp));
        return sp;
    }

    private Control BuildReverseRefsPanel(Faction f)
        => _vis.BuildReverseRefsPanel(f.EntityId);
}
