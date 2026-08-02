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

public class DataFileEntityVisualizer : IEntityVisualizer
{
    private readonly VisHelperService _vis;

    public DataFileEntityVisualizer(VisHelperService vis)
    {
        _vis = vis;
    }

    public Type EntityType => typeof(DataFile);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not DataFile df) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(df), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(df));
        if (!string.IsNullOrWhiteSpace(df.Description))
        {
            var sp = new StackPanel();
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Content")));
            var desc = df.Description.Length > 2000 ? df.Description[..2000] + "..." : df.Description;
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = desc, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
            root.Children.Add(sp);
        }

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(DataFile df)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = _vis.LoadImage(df.Image);
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
            imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(capturedBmp, df.Subject ?? df.Name);
        }
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
        _vis.AddModBadge(df, infoRow);
        identity.Children.Add(infoRow);
        identity.Children.Add(new TextBlock
        {
            Text = df.Subject ?? df.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(df.Image))
            identity.Children.Add(new TextBlock { Text = df.Image, FontSize = 11, Foreground = Brush.Parse("#666") });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }
}
