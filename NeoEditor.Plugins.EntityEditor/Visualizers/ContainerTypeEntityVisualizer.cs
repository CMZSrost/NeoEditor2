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

public class ContainerTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ContainerType);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    public ContainerTypeEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ContainerType ct) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(ct), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ct));
        root.Children.Add(BuildReversePanel(ct));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(ContainerType ct)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ct.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        _vis.AddModBadge(ct, badgeRow);
        identity.Children.Add(badgeRow);
        identity.Children.Add(new TextBlock
        {
            Text = ct.Subject ?? ct.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildReversePanel(ContainerType ct)
    {
        var sp = new StackPanel();
        var store = _dataTable?.ActiveMergeStore ?? _dataTable?.BrowserStore;
        if (store == null) return sp;
        var rawRefs = store.IndexService?.ReverseLookup(ct.EntityId) ?? [];
        if (rawRefs.Count == 0) return sp;

        var resolved = new List<(ItemType Entity, string Subject)>();
        foreach (var (srcEid, _, _) in rawRefs)
        {
            if (store.ReferenceLookups.TryGetValue(typeof(ItemType), out var list) && list is not null)
            {
                var m = list.OfType<ItemType>().FirstOrDefault(e => e.EntityId == srcEid);
                if (m != null) resolved.Add((m, m.Subject));
            }
        }

        if (resolved.Count == 0) return sp;

        sp.Children.Add(_vis.SectionLabel($"{_vis.Loc("Vis.UsedBy")} ({resolved.Count})"));
        var wp = new WrapPanel();
        foreach (var (entity, subject) in resolved.Take(20))
            wp.Children.Add(_refNode.BadgeForEntity(ct, entity, subject,
                "#E3F2FD", "#1565C0"));
        if (resolved.Count > 20)
            wp.Children.Add(_vis.MiniBadge($"+{resolved.Count - 20} more", "#F5F5F5", "#999"));
        sp.Children.Add(_vis.Card(wp));
        return sp;
    }
}
