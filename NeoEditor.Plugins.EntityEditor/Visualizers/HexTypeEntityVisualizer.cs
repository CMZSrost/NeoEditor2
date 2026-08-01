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

public class HexTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(HexType);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    public HexTypeEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not HexType ht) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(ht), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(ht));
        root.Children.Add(BuildTerrainPanel(ht));
        root.Children.Add(BuildLightPanel(ht));
        root.Children.Add(BuildRefsPanel(ht));
        root.Children.Add(BuildReverseRefsPanel(ht));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(HexType ht)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {ht.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        _vis.AddModBadge(ht, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var passLabel = ht.Passable == PassableType.Passable
            ? _vis.Loc("Vis.Passable")
            : _vis.Loc("Vis.Blocked");
        var passBg = ht.Passable == PassableType.Passable ? "#E8F5E9" : "#FFEBEE";
        var passFg = ht.Passable == PassableType.Passable ? "#2E7D32" : "#C62828";
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(passBg), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = passLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(passFg) }
        });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = ht.Subject ?? ht.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(ht.Description))
            identity.Children.Add(new TextBlock
                { Text = ht.Description, FontSize = 12, Foreground = Brush.Parse("#888") });

        var statRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.MovementCost")}: {ht.TerrainCost} AP", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text =
                $"{_vis.Loc("Vis.VisibilityLabel")}: {ht.VizIncrease - ht.VizLimiter} (+{ht.VizIncrease}, -{ht.VizLimiter})",
            FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.EncounterRange")}: {ht.MinRange}–{ht.MaxRange}", FontSize = 11,
            Foreground = Brush.Parse("#666")
        });
        identity.Children.Add(statRow);

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildTerrainPanel(HexType ht)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.TerrainMovement")));

        var costColor = ht.TerrainCost <= 1 ? "#2E7D32" : ht.TerrainCost <= 3 ? "#E65100" : "#C62828";
        var vizNet = ht.VizIncrease - ht.VizLimiter;
        var vizColor = vizNet >= 0 ? "#2E7D32" : "#C62828";
        var cells = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.MovementCost"), $"{ht.TerrainCost} AP", costColor),
            (_vis.Loc("Vis.VisibilityLabel"), $"{vizNet:+#;-#;0} (+{ht.VizIncrease}, -{ht.VizLimiter})", vizColor),
        };
        if (ht.MinRange > 0 || ht.MaxRange > 0)
            cells.Add((_vis.Loc("Vis.EncounterRange"), $"{ht.MinRange}–{ht.MaxRange} {_vis.Loc("Vis.Tiles")}",
                "#1565C0"));
        if (ht.CampItems != 5)
        {
            var campLabel = ht.CampItems switch
            {
                0 => _vis.Loc("Vis.CampItemNone"), 1 => _vis.Loc("Vis.CampItemSparse"),
                2 => _vis.Loc("Vis.CampItemModerate"), 3 => _vis.Loc("Vis.CampItemAbundant"),
                4 => _vis.Loc("Vis.CampItemRich"), 5 => _vis.Loc("Vis.CampItemDefault"),
                _ => $"Lv.{ht.CampItems}"
            };
            cells.Add((_vis.Loc("Vis.CampItemsLabel"), campLabel, "#E65100"));
        }

        sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }

    private Control BuildLightPanel(HexType ht)
    {
        if (string.IsNullOrWhiteSpace(ht.LightLevels)) return new StackPanel();
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.LightLevels")));
        var lightNames = new[] { "Dawn", "Morning", "Noon", "Afternoon", "Dusk", "Midnight" };
        var levels = ht.LightLevels.Split(',').Select(s => s.Trim()).ToList();
        var parsedLevels = levels.Select(s =>
            double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)
                ? (double?)v
                : null).ToList();
        var maxLight = parsedLevels.Where(x => x.HasValue).DefaultIfEmpty(1.0).Max() ?? 1.0;
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star),
                new(1, GridUnitType.Star), new(1, GridUnitType.Star), new(1, GridUnitType.Star)
            },
            Margin = new Thickness(4, 0)
        };
        grid.RowDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < lightNames.Length; i++)
        {
            var col = new StackPanel { Margin = new Thickness(2, 4) };
            col.Children.Add(new TextBlock
            {
                Text = lightNames[i], FontSize = 9, Foreground = Brush.Parse("#999"),
                TextAlignment = TextAlignment.Center
            });
            var valStr = i < levels.Count ? levels[i] : "?";
            var val = i < parsedLevels.Count ? parsedLevels[i] : null;
            // Heatmap: red (0) → yellow (0.5) → green (1.0+)
            var ratio = val.HasValue && maxLight > 0 ? Math.Clamp(val.Value / maxLight, 0.0, 1.0) : 0.0;
            int r = (int)((1 - ratio) * 198 + ratio * 46); // 198→46
            int g = (int)(ratio < 0.5 ? ratio * 2 * 125 : (1 - ratio) * 2 * 125 + 125); // 0→125→0
            int bv = (int)(ratio < 0.5 ? (1 - ratio * 2) * 40 : 0); // 40→0
            var cellBg = $"#{r:X2}{g:X2}{bv:X2}";
            col.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(3),
                Background = Brush.Parse(val.HasValue ? cellBg : "#F5F5F5"),
                Padding = new Thickness(4, 2),
                Child = new TextBlock
                {
                    Text = valStr, FontSize = 11, FontWeight = FontWeight.Medium,
                    Foreground = ratio > 0.5 ? Brushes.White : Brush.Parse("#333"), TextAlignment = TextAlignment.Center
                }
            });
            Grid.SetColumn(col, i);
            grid.Children.Add(col);
        }

        sp.Children.Add(_vis.Card(grid));
        return sp;
    }

    private Control BuildRefsPanel(HexType ht)
    {
        var sp = new StackPanel { Spacing = 8 };

        void AddRef<T>(string label, string raw, string propName, string bg, string fg) where T : IEntity
        {
            if (string.IsNullOrWhiteSpace(raw) || raw == "3" || raw == "25") return;
            sp.Children.Add(_vis.SectionLabel(label));
            var wp = new WrapPanel();
            foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<T>(ht, propName, seg, bg, fg));
            }

            sp.Children.Add(_vis.Card(wp));
        }

        AddRef<TreasureTable>(_vis.Loc("Vis.ScavengeLoot"), ht.TreasureId, nameof(HexType.TreasureId), "#E8F5E9",
            "#2E7D32");
        AddRef<TreasureTable>(_vis.Loc("Vis.InitialScavenge"), ht.ScavengeInitialId,
            nameof(HexType.ScavengeInitialId), "#E0F2F1", "#00695C");
        AddRef<TreasureTable>(_vis.Loc("Vis.HourlyScavenge"), ht.ScavengeItemsIdPerHour,
            nameof(HexType.ScavengeItemsIdPerHour), "#B2DFDB", "#004D40");
        AddRef<Condition>(_vis.Loc("Vis.OnEnterConditions"), ht.ConditionIds, nameof(HexType.ConditionIds),
            "#FCE4EC", "#C62828");

        if (ht.DefaultCampId.Count > 0)
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.DefaultCamp")));
            var wp = new WrapPanel();
            wp.Children.Add(_refNode.Badge<CampType>(ht, nameof(HexType.DefaultCampId),
                ht.DefaultCampId.ToString(), "#FFF3E0", "#E65100"));
            sp.Children.Add(_vis.Card(wp));
        }

        return sp;
    }

    private Control BuildReverseRefsPanel(HexType ht)
        => _vis.BuildReverseRefsPanel(ht.EntityId);
}
