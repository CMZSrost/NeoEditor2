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

public class CreatureSourceEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(CreatureSource);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    public CreatureSourceEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not CreatureSource cs) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(cs), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(cs));
        root.Children.Add(BuildStatsPanel(cs));
        if (!string.IsNullOrWhiteSpace(cs.CreatureId) && cs.CreatureId != "0")
            root.Children.Add(BuildCreaturePanel(cs));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private (double TotalWeight, double Proportion) GetWeightInfo(CreatureSource cs)
    {
        var store = _dataTable?.ActiveMergeStore ?? _dataTable?.BrowserStore;
        if (store?.ReferenceLookups.TryGetValue(typeof(CreatureSource), out var list) != true || list is null)
            return (cs.Weight, 1.0);
        var atPos = list.OfType<CreatureSource>().Where(s => s.X == cs.X && s.Y == cs.Y).ToList();
        var total = atPos.Sum(s => s.Weight);
        return (total, total > 0 ? cs.Weight / total : 1.0);
    }

    private Control BuildHeroHeader(CreatureSource cs)
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
        _vis.AddModBadge(cs, badgeRow);
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
        return _vis.Card(grid);
    }

    private Control BuildStatsPanel(CreatureSource cs)
    {
        var (totalW, proportion) = GetWeightInfo(cs);
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Spawn")));

        var cells = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.Weight"), $"{cs.Weight:F2} ({proportion:P0})", "#1565C0"),
            (_vis.Loc("Vis.Position"), $"({cs.X}, {cs.Y})", null),
            (_vis.Loc("Vis.Count"), $"{cs.Min}–{cs.Max}", cs.Max > 0 ? "#1565C0" : null),
        };
        sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }

    private Control BuildCreaturePanel(CreatureSource cs)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Creature")));
        var wp = new WrapPanel();
        wp.Children.Add(_refNode.Badge<Creature>(cs, nameof(CreatureSource.CreatureId), cs.CreatureId,
            "#E8EAF6", "#283593"));
        sp.Children.Add(_vis.Card(wp));
        return sp;
    }
}
