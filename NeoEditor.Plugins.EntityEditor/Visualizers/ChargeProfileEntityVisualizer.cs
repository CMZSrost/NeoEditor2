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

public class ChargeProfileEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ChargeProfile);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    /// <summary>Create with injected RefNode (M3). Falls back to VisHelper.Resolver when null.</summary>
    public ChargeProfileEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ChargeProfile cp) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(cp), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(cp));
        root.Children.Add(BuildStatsPanel(cp));
        root.Children.Add(BuildReverseRefsPanel(cp));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(ChargeProfile cp)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {cp.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        if (cp.Degrade)
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = _vis.Loc("Vis.Degradeable"), FontSize = 10, Foreground = Brush.Parse("#E65100") }
            });
        _vis.AddModBadge(cp, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = cp.Subject ?? cp.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        // M3: ItemId → RefNode badge (clickable ref to ItemType)
        var itemIdRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        itemIdRow.Children.Add(new TextBlock
            { Text = $"{_vis.Loc("Vis.ItemLabel")}:", FontSize = 12, Foreground = Brush.Parse("#888"),
              VerticalAlignment = VerticalAlignment.Center });
        itemIdRow.Children.Add(_refNode.Badge<ItemType>(cp, nameof(ChargeProfile.ItemId), cp.ItemId,
            "#E3F2FD", "#1565C0"));
        identity.Children.Add(itemIdRow);
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildStatsPanel(ChargeProfile cp)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.ConsumptionRates")));
        var cells = new List<(string, string, string?)>();
        if (cp.PerUse > 0)
            cells.Add((_vis.Loc("Vis.PerUse"), $"{cp.PerUse:F2}", "#C62828"));
        if (cp.PerHour > 0)
            cells.Add((_vis.Loc("Vis.PerHourGrowth"), $"{cp.PerHour:F2}", "#E65100"));
        if (cp.PerHourEquipped > 0)
            cells.Add((_vis.Loc("Vis.PerHourEquippedDrain"), $"{cp.PerHourEquipped:F2}", "#FB8C00"));
        if (cp.PerHex > 0)
            cells.Add((_vis.Loc("Vis.PerHex"), $"{cp.PerHex:F2}", "#6A1B9A"));
        if (cells.Count == 0)
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = "(no consumption)", FontSize = 10, Foreground = Brush.Parse("#999") }));
        else
            sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }

    private Control BuildReverseRefsPanel(ChargeProfile cp)
        => _vis.BuildReverseRefsPanel(cp.EntityId);
}
