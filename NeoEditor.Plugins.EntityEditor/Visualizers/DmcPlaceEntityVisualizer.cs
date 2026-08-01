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

public class DmcPlaceEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(DmcPlace);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    public DmcPlaceEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not DmcPlace dp) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(dp), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(dp));
        root.Children.Add(BuildStatsPanel(dp));
        root.Children.Add(BuildRefsPanel(dp));
        root.Children.Add(BuildReverseRefsPanel(dp));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(DmcPlace dp)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = _vis.LoadImage(dp.Image);
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
        _vis.AddModBadge(dp, infoRow);
        identity.Children.Add(infoRow);
        identity.Children.Add(new TextBlock
        {
            Text = !string.IsNullOrWhiteSpace(dp.Image)
                ? dp.Image
                : (dp.Subject ?? $"{_vis.Loc("Vis.DMCPlace")} #{dp.Id}"),
            FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(dp.Image))
            identity.Children.Add(new TextBlock
                { Text = $"{_vis.Loc("Vis.Icon")}: {dp.Image}", FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildStatsPanel(DmcPlace dp)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Location")));
        var stats = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.Position"), $"({dp.X}, {dp.Y})", null),
        };
        sp.Children.Add(_vis.BuildStatCard(stats));
        return sp;
    }

    private Control BuildRefsPanel(DmcPlace dp)
    {
        var sp = new StackPanel();
        if (dp.EncounterId.Count == 0) return sp;
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Encounter")));
        var wp = new WrapPanel();
        wp.Children.Add(_refNode.Badge<Encounter>(dp, nameof(DmcPlace.EncounterId),
            dp.EncounterId.ToString(), "#E8F5E9", "#2E7D32"));
        sp.Children.Add(_vis.Card(wp));
        return sp;
    }

    private Control BuildReverseRefsPanel(DmcPlace dp)
        => _vis.BuildReverseRefsPanel(dp.EntityId);
}
