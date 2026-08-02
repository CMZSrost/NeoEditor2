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

public class BarterHexEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(BarterHex);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    public BarterHexEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router,
            vis.BuildRefTooltip);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not BarterHex bh) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(bh), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(bh));
        root.Children.Add(BuildStatsPanel(bh));
        if (bh.RestockTreasureId > 0 && bh.RestockTreasureId != 3)
            root.Children.Add(BuildRestockPanel(bh));
        root.Children.Add(BuildReverseRefsPanel(bh));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(BarterHex bh)
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
                Text = bh.Buys ? _vis.Loc("Vis.ShopBuys") : _vis.Loc("Vis.ShopSells"), FontSize = 10,
                FontWeight = FontWeight.Bold, Foreground = Brush.Parse(bh.Buys ? "#2E7D32" : "#C62828")
            }
        });
        _vis.AddModBadge(bh, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = bh.Subject ?? $"Barter Hex #{bh.Id}", FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        identity.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.Position")}: ({bh.X}, {bh.Y})", FontSize = 12, Foreground = Brush.Parse("#888")
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildStatsPanel(BarterHex bh)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.ShopInfo")));

        var cells = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.Position"), $"({bh.X}, {bh.Y})", null),
            (_vis.Loc("Vis.Buys"), bh.Buys ? _vis.Loc("Vis.Yes") : _vis.Loc("Vis.No"),
                bh.Buys ? "#2E7D32" : "#999"),
        };
        sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }

    private Control BuildRestockPanel(BarterHex bh)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.RestockTT")));
        var wp = new WrapPanel();
        var store = _dataTable?.ActiveMergeStore ?? _dataTable?.BrowserStore;
        if (store?.ReferenceLookups.TryGetValue(typeof(TreasureTable), out var list) == true && list is not null)
        {
            var tt = list.OfType<TreasureTable>().FirstOrDefault(t => t.Id == bh.RestockTreasureId);
            if (tt is not null)
                wp.Children.Add(_refNode.BadgeForEntity<TreasureTable>(bh, tt, tt.Subject, "#E8F5E9", "#2E7D32"));
            else
                wp.Children.Add(_vis.MiniBadge($"TT #{bh.RestockTreasureId}", "#F5F5F5", "#999"));
        }
        else
            wp.Children.Add(_vis.MiniBadge($"TT #{bh.RestockTreasureId}", "#F5F5F5", "#999"));

        sp.Children.Add(_vis.Card(wp));
        return sp;
    }

    private Control BuildReverseRefsPanel(BarterHex bh)
        => _vis.BuildReverseRefsPanel(bh.EntityId);
}
