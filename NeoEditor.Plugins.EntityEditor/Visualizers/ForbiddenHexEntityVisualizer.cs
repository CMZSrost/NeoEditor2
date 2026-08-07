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

public class ForbiddenHexEntityVisualizer : IEntityVisualizer
{
    private readonly VisHelperService _vis;

    public ForbiddenHexEntityVisualizer(VisHelperService vis)
    {
        _vis = vis;
    }

    public Type EntityType => typeof(ForbiddenHex);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ForbiddenHex fh) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(fh));

        root.Children.Add(BuildHeroHeader(fh));
        root.Children.Add(BuildStatsPanel(fh));
        root.Children.Add(BuildReverseRefsPanel(fh));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(ForbiddenHex fh)
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
                        { Text = _vis.Loc("Vis.Forbidden"), FontSize = 10, Foreground = Brush.Parse("#C62828") }
                }
            }
        });
        _vis.AddModBadge(fh, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = fh.Subject ?? fh.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        identity.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.Position")}: ({fh.X}, {fh.Y})", FontSize = 12, Foreground = Brush.Parse("#888")
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildStatsPanel(ForbiddenHex fh)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Location")));
        var cells = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.Position"), $"({fh.X}, {fh.Y})", null),
        };
        if (!string.IsNullOrWhiteSpace(fh.Name))
            cells.Add((_vis.Loc("Vis.Name"), fh.Name, "#C62828"));
        sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }

    private Control BuildReverseRefsPanel(ForbiddenHex fh)
        => _vis.BuildReverseRefsPanel(fh.EntityId);
}
