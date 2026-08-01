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
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.EntityEditor.Services;
namespace NeoEditor.Plugins.EntityEditor.Visualizers;

public class GameVarEntityVisualizer : IEntityVisualizer
{
    private readonly VisHelperService _vis;

    public Type EntityType => typeof(GameVar);

    public GameVarEntityVisualizer(VisHelperService vis)
    {
        _vis = vis;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not GameVar gv) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(gv), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(gv));
        root.Children.Add(BuildStatsPanel(gv));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(GameVar gv)
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
        _vis.AddModBadge(gv, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = gv.Subject ?? gv.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        identity.Children.Add(new TextBlock
            { Text = $"{_vis.Loc("Vis.Value")}: {gv.Value}", FontSize = 14, Foreground = Brush.Parse("#2E7D32") });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildStatsPanel(GameVar gv)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Stats")));

        var cells = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.Type"), gv.Type, "#1565C0"),
            (_vis.Loc("Vis.Value"), gv.Value, "#2E7D32"),
        };
        sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }
}
