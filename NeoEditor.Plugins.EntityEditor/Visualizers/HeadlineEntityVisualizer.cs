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

public class HeadlineEntityVisualizer : IEntityVisualizer
{
    private readonly VisHelperService _vis;

    public HeadlineEntityVisualizer(VisHelperService vis)
    {
        _vis = vis;
    }

    public Type EntityType => typeof(Headline);

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Headline h) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(h));

        root.Children.Add(BuildHeroHeader(h));
        if (!string.IsNullOrWhiteSpace(h.HeadlineText))
        {
            var sp = new StackPanel();
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.HeadlineText")));
            var text = h.HeadlineText.Length > 2000 ? h.HeadlineText[..2000] + "..." : h.HeadlineText;
            sp.Children.Add(_vis.Card(new TextBlock
            {
                Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333333"),
                FontWeight = FontWeight.Medium
            }));
            root.Children.Add(sp);
        }

        // R48: reverse refs — who shows this headline.
        root.Children.Add(_vis.BuildReverseRefsPanel(h.EntityId));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Headline h)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 5,
                Children =
                {
                    new SymbolIcon { Symbol = Symbol.News, FontSize = 11, Foreground = Brush.Parse("#1565C0") },
                    new TextBlock
                    {
                        Text = $"ID: {h.Id}", FontSize = 11, FontWeight = FontWeight.Bold,
                        Foreground = Brush.Parse("#1565C0")
                    }
                }
            }
        });
        var len = string.IsNullOrWhiteSpace(h.HeadlineText) ? 0 : h.HeadlineText.Length;
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock { Text = $"{len} chars", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        _vis.AddModBadge(h, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
            { Text = $"News #{h.Id}", FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }
}
