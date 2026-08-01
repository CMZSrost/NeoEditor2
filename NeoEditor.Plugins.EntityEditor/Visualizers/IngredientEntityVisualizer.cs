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

public class IngredientEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Ingredient);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    public IngredientEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Ingredient ing) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(ing), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ing));
        root.Children.Add(BuildPropsPanel(ing));
        root.Children.Add(BuildReversePanel(ing));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Ingredient ing)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ing.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        _vis.AddModBadge(ing, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = ing.Subject ?? ing.Name, FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildPropsPanel(Ingredient ing)
    {
        var sp = new StackPanel { Spacing = 8 };

        void AddProps(string label, string raw, string propName, string bg, string fg)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            sp.Children.Add(_vis.SectionLabel(label));
            var wp = new WrapPanel();
            foreach (var s in raw.Split('&').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<ItemProp>(ing, propName, s, bg, fg));
            }

            sp.Children.Add(_vis.Card(wp));
        }

        AddProps($"{_vis.Loc("Vis.Required")} {_vis.Loc("Vis.Properties")}", ing.RequiredProps,
            nameof(Ingredient.RequiredProps), "#E8F5E9", "#2E7D32");
        AddProps($"{_vis.Loc("Vis.Forbidden")} {_vis.Loc("Vis.Properties")}", ing.ForbidProps,
            nameof(Ingredient.ForbidProps), "#FFEBEE", "#C62828");
        return sp;
    }

    private Control BuildReversePanel(Ingredient ing)
        => _vis.BuildReverseRefsPanel(ing.EntityId);
}
