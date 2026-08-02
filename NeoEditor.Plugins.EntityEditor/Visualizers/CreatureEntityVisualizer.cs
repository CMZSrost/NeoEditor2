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

public class CreatureEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Creature);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    public CreatureEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router,
            vis.BuildRefTooltip);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Creature c) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(c), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(c));
        root.Children.Add(BuildStatsPanel(c));
        root.Children.Add(BuildRefsPanel(c));
        root.Children.Add(BuildReverseRefsPanel(c));
        if (!string.IsNullOrWhiteSpace(c.Activities))
            root.Children.Add(BuildActivitiesPanel(c));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Creature c)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = _vis.LoadImage(c.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
            var capturedBmp = bmp;
            imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(capturedBmp, c.Subject ?? c.Name);
        }
        else
            imageArea.Child = new TextBlock
            {
                Text = "Creature", FontSize = 14, Foreground = Brush.Parse("#999"),
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
                Text = $"ID: {c.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        _vis.AddModBadge(c, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = $"{c.MovesPerTurn} moves/turn", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        var factionName = _vis.Resolver.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction)
            ?.Subject;
        if (!string.IsNullOrWhiteSpace(factionName) && c.Faction != "0")
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = factionName, FontSize = 10, Foreground = Brush.Parse("#283593") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = c.Subject ?? c.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(c.NamePublic) && c.NamePublic != c.Name)
            identity.Children.Add(new TextBlock
            {
                Text = $"Public: {c.NamePublic}", FontSize = 12, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#888")
            });
        if (!string.IsNullOrWhiteSpace(c.Notes))
            identity.Children.Add(new TextBlock
                { Text = c.Notes, FontSize = 11, Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildRefsPanel(Creature c)
    {
        var sp = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(c.Faction) && c.Faction != "0")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Faction")));
            var wp = new WrapPanel();
            wp.Children.Add(_refNode.Badge<Faction>(c, nameof(Creature.Faction), c.Faction,
                "#FFF3E0", "#E65100"));
            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.AttackModes))
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.AttackModes")));
            var wp = new WrapPanel();
            foreach (var seg in c.AttackModes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<AttackMode>(c, nameof(Creature.AttackModes), seg,
                    "#FFEBEE", "#C62828"));
            }

            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.BaseConditions))
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.CreatureStatus")));
            var eqPattern = NeoEditor.Helper.ReferencePattern.FromName("{id}={value}");
            var wp = new WrapPanel();
            foreach (var seg in c.BaseConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = _vis.Resolver.LookupRef<Condition>(c, nameof(Creature.BaseConditions), seg);
                if (cond is not null)
                {
                    var extra = eqPattern.FormatExtraInfo(seg);
                    var label = string.IsNullOrEmpty(extra) ? cond.Subject : $"{cond.Subject} ={extra}";
                    wp.Children.Add(_refNode.BadgeForEntity(c, cond, label, "#FCE4EC", "#C62828"));
                    continue;
                }

                wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.EncounterIds))
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.OnEnterConditions")));
            var wp = new WrapPanel();
            foreach (var seg in c.EncounterIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                // R30: EncounterIds points to Encounter (model annotation since round28),
                // not Condition — the badge type must match or resolution misses.
                wp.Children.Add(_refNode.Badge<Encounter>(c, nameof(Creature.EncounterIds), seg,
                    "#E8EAF6", "#283593"));
            }

            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.TreasureId) && c.TreasureId != "3")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.LootTable")));
            var wp = new WrapPanel();
            wp.Children.Add(_refNode.Badge<TreasureTable>(c, nameof(Creature.TreasureId), c.TreasureId,
                "#E8F5E9", "#2E7D32"));
            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(c.CorpseId) && c.CorpseId != "3")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.CorpseLoot")));
            var wp = new WrapPanel();
            wp.Children.Add(_refNode.Badge<TreasureTable>(c, nameof(Creature.CorpseId), c.CorpseId,
                "#FCE4EC", "#880E4F"));
            sp.Children.Add(_vis.Card(wp));
        }

        return sp;
    }

    private Control BuildStatsPanel(Creature c)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel($"{_vis.Loc("Vis.Stats")} & Activity"));

        var conditionsCount = string.IsNullOrWhiteSpace(c.BaseConditions) ? 0 : c.BaseConditions.Split(',').Length;
        var encConditionsCount = string.IsNullOrWhiteSpace(c.EncounterIds) ? 0 : c.EncounterIds.Split(',').Length;
        var atkCount = string.IsNullOrWhiteSpace(c.AttackModes) ? 0 : c.AttackModes.Split(',').Length;
        var hasLoot = !string.IsNullOrWhiteSpace(c.TreasureId) && c.TreasureId != "3";
        var hasCorpse = !string.IsNullOrWhiteSpace(c.CorpseId) && c.CorpseId != "3";

        var grid = new Grid
        {
            ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star) },
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
            Margin = new Thickness(4, 0)
        };

        void AddCell(int r, int c, string label, string value, string? color = null)
        {
            var cell = new StackPanel { Margin = new Thickness(4, 3) };
            cell.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Brush.Parse("#999") });
            cell.Children.Add(new TextBlock
            {
                Text = value, FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333")
            });
            Grid.SetRow(cell, r);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }

        var factionName = _vis.Resolver.LookupRef<Faction>(c, nameof(Creature.Faction), c.Faction)
            ?.Subject;
        AddCell(0, 0, _vis.Loc("Vis.MovesPerTurn"), $"{c.MovesPerTurn}", "#E65100");
        AddCell(0, 1, _vis.Loc("Vis.Faction"), factionName ?? "None", "#283593");
        AddCell(1, 0, _vis.Loc("Vis.Attacks"), $"{atkCount}", atkCount > 0 ? "#C62828" : "#999");
        AddCell(1, 1, _vis.Loc("Vis.CreatureStatus"), $"{conditionsCount}",
            conditionsCount > 0 ? "#C62828" : "#999");
        AddCell(2, 0, _vis.Loc("Vis.EncConditions"), $"{encConditionsCount}",
            encConditionsCount > 0 ? "#283593" : "#999");
        AddCell(2, 1, _vis.Loc("Vis.LootTable"), hasLoot ? "Yes" : "No", hasLoot ? "#2E7D32" : "#999");
        AddCell(3, 0, _vis.Loc("Vis.CorpseLoot"), hasCorpse ? "Yes" : "No", hasCorpse ? "#880E4F" : "#999");
        AddCell(3, 1, _vis.Loc("Vis.Activities"), string.IsNullOrWhiteSpace(c.Activities) ? "None" : "Yes",
            string.IsNullOrWhiteSpace(c.Activities) ? "#999" : "#2E7D32");

        sp.Children.Add(_vis.Card(grid));
        return sp;
    }

    private Control BuildActivitiesPanel(Creature c)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Activities")));
        var acts = c.Activities.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (acts.Count == 0)
        {
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = "(None)", FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        var wp = new WrapPanel();
        foreach (var act in acts.Take(30))
        {
            wp.Children.Add(_vis.MiniBadge(act, "#E8EAF6", "#283593"));
        }

        if (acts.Count > 30)
            wp.Children.Add(_vis.MiniBadge($"+{acts.Count - 30} more", "#F5F5F5", "#999"));
        sp.Children.Add(_vis.Card(wp));
        return sp;
    }

    private Control BuildReverseRefsPanel(Creature c)
        => _vis.BuildReverseRefsPanel(c.EntityId);
}
