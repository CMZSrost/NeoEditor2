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

public class EncounterTriggerEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(EncounterTrigger);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    public EncounterTriggerEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router,
            vis.BuildRefTooltip);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not EncounterTrigger et) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(et), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(et));
        root.Children.Add(BuildStatsPanel(et));
        root.Children.Add(BuildRefsPanel(et));
        root.Children.Add(BuildReverseRefsPanel(et));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(EncounterTrigger et)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {et.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        var types2 = new List<string>();
        if (et.LocBased) types2.Add(_vis.Loc("Vis.LocType"));
        if (et.DateBased) types2.Add(_vis.Loc("Vis.DateType"));
        if (et.HexBased) types2.Add(_vis.Loc("Vis.HexType"));
        if (et.Unique) types2.Add(_vis.Loc("Vis.UniqueType"));
        if (et.AIPassable) types2.Add(_vis.Loc("Vis.AIType"));
        if (types2.Count > 0)
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                    { Text = string.Join(" · ", types2), FontSize = 10, Foreground = Brush.Parse("#283593") }
            });
        badgeRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"{_vis.Loc("Vis.Chance")}: {et.Chance:P0}", FontSize = 10,
                Foreground = Brush.Parse("#E65100")
            }
        });
        _vis.AddModBadge(et, badgeRow);
        identity.Children.Add(badgeRow);

        identity.Children.Add(new TextBlock
        {
            Text = et.Subject ?? et.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(et.Area))
            identity.Children.Add(new TextBlock
                { Text = $"{_vis.Loc("Vis.Area")}: {et.Area}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (!string.IsNullOrWhiteSpace(et.DateMin) || !string.IsNullOrWhiteSpace(et.DateMax))
            identity.Children.Add(new TextBlock
            {
                Text = $"{_vis.Loc("Vis.DateRange")}: {et.DateMin} – {et.DateMax}", FontSize = 11,
                Foreground = Brush.Parse("#666")
            });

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildRefsPanel(EncounterTrigger et)
    {
        var sp = new StackPanel { Spacing = 8 };
        if (et.EncounterId.Count > 0)
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.EncounterRef")));
            var wp = new WrapPanel();
            // R30: ReferenceList.ToString() emits "[a, b]" and breaks resolution —
            // the DataGrid shows the raw text via ReferenceText.GetRawString, so the
            // badge must receive the same clean raw id ("123", not "[123]").
            wp.Children.Add(_refNode.Badge<Encounter>(et, nameof(EncounterTrigger.EncounterId),
                et.EncounterId.ToRawString(null), "#E8F5E9", "#2E7D32"));
            sp.Children.Add(_vis.Card(wp));
        }

        if (et.HexTypes.Count > 0)
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.HexTypesRef")));
            var wp = new WrapPanel();
            foreach (var seg in et.HexTypes.ToRawString(",")
                         .Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<HexType>(et, nameof(EncounterTrigger.HexTypes), seg,
                    "#E0F2F1", "#00695C"));
            }

            sp.Children.Add(_vis.Card(wp));
        }

        return sp;
    }

    private Control BuildReverseRefsPanel(EncounterTrigger et)
        => _vis.BuildReverseRefsPanel(et.EntityId);

    private Control BuildStatsPanel(EncounterTrigger et)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.TriggerDetails")));
        var cells = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.Chance"), $"{et.Chance:P0}", et.Chance > 0 ? "#1565C0" : "#999"),
            (_vis.Loc("Vis.Unique"), et.Unique ? _vis.Loc("Vis.YesOnce") : _vis.Loc("Vis.NoRepeat"),
                et.Unique ? "#E65100" : "#999"),
            (_vis.Loc("Vis.AIPassable"), et.AIPassable ? _vis.Loc("Vis.Yes") : _vis.Loc("Vis.No"),
                et.AIPassable ? "#2E7D32" : "#999"),
        };
        if (!string.IsNullOrWhiteSpace(et.Area))
            cells.Add((_vis.Loc("Vis.Area"), et.Area, "#2E7D32"));
        if (!string.IsNullOrWhiteSpace(et.DateMin))
            cells.Add((_vis.Loc("Vis.DateRange"), $"{et.DateMin} – {et.DateMax}", "#6A1B9A"));
        sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }
}
