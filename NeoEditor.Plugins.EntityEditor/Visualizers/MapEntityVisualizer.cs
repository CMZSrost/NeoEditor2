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
using NeoEditor.UI.Common.Helpers;

namespace NeoEditor.Plugins.EntityEditor.Visualizers;

public class MapEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Map);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    public MapEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            _vis.Resolver,
            _vis.Router,
            _vis.BuildRefTooltip);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Map m) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(m));

        root.Children.Add(BuildHeroHeader(m));
        root.Children.Add(BuildMapImagePanel(m));
        if (!string.IsNullOrWhiteSpace(m.Definition))
            root.Children.Add(BuildDefinitionPanel(m));
        root.Children.Add(BuildReverseRefsPanel(m));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Map m)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var bmp = _vis.LoadImage(m.Name);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
            // R30 (Doc 21 §10): click the hero image to zoom.
            var capturedBmp = bmp;
            imageArea.Cursor = new Cursor(StandardCursorType.Hand);
            imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(capturedBmp, m.Subject ?? m.Name);
        }
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
        _vis.AddModBadge(m, infoRow);
        identity.Children.Add(infoRow);
        identity.Children.Add(new TextBlock
        {
            Text = m.Subject ?? m.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(m.Name))
            identity.Children.Add(new TextBlock { Text = m.Name, FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildMapImagePanel(Map m)
    {
        var sp = new StackPanel();
        var bmp = _vis.LoadImage(m.Name);
        if (bmp is not null)
        {
            sp.Children.Add(_vis.SectionLabel("Map Image"));
            const double maxW = 600;
            var scale = Math.Min(1.0, maxW / bmp.Size.Width);
            sp.Children.Add(_vis.Card(new Border
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
            sp.Children.Add(_vis.SectionLabel($"Hex Data ({gw}×{gh}, {hexes.Count} cells)"));
        }

        return sp;
    }

    private Control BuildDefinitionPanel(Map m)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.MapDefinition")));
        var def = m.Definition.Length > 3000 ? m.Definition[..3000] + "..." : m.Definition;
        sp.Children.Add(_vis.Card(new TextBlock
        {
            Text = def, FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#555555"),
            FontFamily = "Consolas, monospace"
        }));
        return sp;
    }

    private Control BuildReverseRefsPanel(Map m)
    {
        // M3: delegate to shared VisHelper.BuildReverseRefsPanel (N03-compliant via RefNode)
        return _vis.BuildReverseRefsPanel(m.EntityId);
    }
}
