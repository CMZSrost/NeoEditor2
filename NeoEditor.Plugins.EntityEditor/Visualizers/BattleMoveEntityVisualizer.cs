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

public class BattleMoveEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(BattleMove);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    // ═══════════════ Detail ═══════════════

    public BattleMoveEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not BattleMove bm) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(bm), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(bm));
        root.Children.Add(BuildStatsPanel(bm));

        if (!string.IsNullOrWhiteSpace(bm.PopUp))
            root.Children.Add(BuildTextPanel(_vis.Loc("Vis.Description"), bm.PopUp, 800));
        if (!string.IsNullOrWhiteSpace(bm.Success))
            root.Children.Add(BuildTextPanel(_vis.Loc("Vis.OnSuccess"), bm.Success, 400, "#2E7D32"));
        if (!string.IsNullOrWhiteSpace(bm.Fail))
            root.Children.Add(BuildTextPanel(_vis.Loc("Vis.OnFail"), bm.Fail, 400, "#C62828"));

        root.Children.Add(BuildConditionsPanel(bm));
        root.Children.Add(BuildReverseRefsPanel(bm));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Hero Header ═══════════════

    private Control BuildHeroHeader(BattleMove bm)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        // --- badge row ---
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {bm.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });

        var (typeLabel, typeBg, typeFg) = GetTypeBadge(bm);
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(typeBg),
            Padding = new Thickness(8, 2),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 5,
                Children =
                {
                    new SymbolIcon { Symbol = GetTypeIconSymbol(bm), FontSize = 11, Foreground = Brush.Parse(typeFg) },
                    new TextBlock
                    {
                        Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg)
                    }
                }
            }
        });
        _vis.AddModBadge(bm, idRow);
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (!string.IsNullOrWhiteSpace(bm.StrId))
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#F3E5F5"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = bm.StrId, FontSize = 10, Foreground = Brush.Parse("#6A1B9A") }
            });

        var flags = new List<string>();
        if (bm.Offense) flags.Add(_vis.Loc("Vis.Offensive"));
        if (bm.Approach) flags.Add("Approach");
        if (bm.FallBack) flags.Add("FallBack");
        if (bm.Retreat) flags.Add(_vis.Loc("Vis.Retreat"));
        if (bm.Position) flags.Add("Position");
        if (bm.Passive) flags.Add(_vis.Loc("Vis.Passive"));
        if (bm.AllOutOfRange) flags.Add("AllOutOfRange");
        if (bm.InAttackRange) flags.Add("InAttackRange");
        if (flags.Count > 0)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8EAF6"), Padding = new Thickness(8, 2),
                Child = new TextBlock
                {
                    Text = $"{_vis.Loc("Vis.BattleMove.Flags")}: {string.Join(" · ", flags)}", FontSize = 10,
                    Foreground = Brush.Parse("#283593")
                }
            });
        identity.Children.Add(infoRow);

        // --- name ---
        identity.Children.Add(new TextBlock
        {
            Text = bm.Subject ?? bm.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(bm.Notes))
            identity.Children.Add(new TextBlock
            {
                Text = bm.Notes, FontSize = 12, Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap
            });

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    // ═══════════════ Stats Panel ═══════════════

    private Control BuildStatsPanel(BattleMove bm)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Stats")));

        var bars = new StackPanel { Spacing = 6 };

        // Chance — 0–1 probability, normalized
        bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Chance"), $"{bm.Chance:P0}", bm.Chance,
            bm.Chance >= 1 ? "#2E7D32" : "#E65100"));

        // Detect — 0–1 probability, normalized
        bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Detect"), $"{bm.Detect:P0}", bm.Detect,
            bm.Detect <= 0 ? "#2E7D32" : bm.Detect >= 0.5 ? "#C62828" : "#FB8C00"));

        // Priority — bot-only, default 0
        var priorityFill = Math.Clamp(bm.Priority / 1.0, 0.05, 1.0);
        bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Priority"), $"{bm.Priority:F2}", priorityFill,
            bm.Priority > 0 ? "#1565C0" : "#78909C"));

        // Fatigue — display as StatBar (range typically -5 to 5, use maxAbs=5)
        var fatigueMax = Math.Max(Math.Abs(bm.Fatigue), 5.0);
        bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Fatigue"), $"{bm.Fatigue:F1}",
            Math.Clamp(Math.Abs(bm.Fatigue) / fatigueMax, 0.05, 1.0),
            bm.Fatigue > 0 ? "#C62828" : "#2E7D32"));

        // AI Order — display as StatBar (range typically 0-1)
        bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Order"), $"{bm.Order:F2}",
            Math.Clamp(bm.Order, 0.05, 1.0), "#1565C0"));

        // Key-value rows for non-normalized stats — equal-width grid
        var kvItems = new List<(string label, string value)>();
        var rangeText = bm.MinRange == -1 && bm.MaxRange == -1 ? "All"
            : bm.MinRange == 0 ? $"0–{bm.MaxRange}" : $"{bm.MinRange}–{bm.MaxRange}";
        kvItems.Add((_vis.Loc("Vis.Range"), rangeText));
        kvItems.Add((_vis.Loc("Vis.Exposure"), $"them {FmtExp(bm.SeeThem)} / us {FmtExp(bm.SeeUs)}"));
        if (bm.MinCharges > 0)
            kvItems.Add((_vis.Loc("Vis.MinCharges"), $"{bm.MinCharges}"));
        if (!string.IsNullOrWhiteSpace(bm.ChanceType) && bm.ChanceType != "0,0,0")
            kvItems.Add(("Chance Type", bm.ChanceType));
        if (kvItems.Count > 0)
        {
            var kvGrid = new Grid { Margin = new Thickness(4, 4, 4, 4) };
            foreach (var _ in kvItems)
                kvGrid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
            for (int i = 0; i < kvItems.Count; i++)
            {
                var mini = MiniKv(kvItems[i].label, kvItems[i].value);
                mini.Margin = new Thickness(i > 0 ? 8 : 0, 0, 0, 0);
                Grid.SetColumn(mini, i);
                kvGrid.Children.Add(mini);
            }
            bars.Children.Add(kvGrid);
        }

        sp.Children.Add(_vis.Card(bars));
        return sp;
    }

    // ═══════════════ Text panels ═══════════════

    private Control BuildTextPanel(string label, string text, int maxLen, string? color = null)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(label));
        var display = text.Length > maxLen ? text[..maxLen] + "..." : text;
        sp.Children.Add(_vis.Card(new TextBlock
        {
            Text = display, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse(color ?? "#333")
        }));
        return sp;
    }

    // ═══════════════ Conditions Panel ═══════════════

    private Control BuildConditionsPanel(BattleMove bm)
    {
        var sp = new StackPanel { Spacing = 8 };
        var hasAny = false;

        void AddCondGroup(string label, string raw, string separator, string propName, string bg, string fg)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            hasAny = true;
            sp.Children.Add(_vis.SectionLabel(label));
            var wp = new WrapPanel();
            foreach (var seg in raw.Split(separator).Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var clean = seg.Trim('[', ']');
                var isNegative = clean.StartsWith("-");
                var lookupId = isNegative ? clean[1..] : seg;

                var cond = _vis.Resolver.LookupRef<Condition>(bm, propName, lookupId);
                if (cond is not null)
                {
                    var display = isNegative ? $"NOT {cond.Subject}" : cond.Subject;
                    var (cbg, cfg) = isNegative ? ("#FFEBEE", "#C62828") : (bg, fg);
                    wp.Children.Add(_refNode.BadgeForEntity<Condition>(bm, cond, display, cbg, cfg));
                }
                else
                {
                    wp.Children.Add(_vis.MiniBadge(clean, "#F5F5F5", "#999"));
                }
            }

            sp.Children.Add(_vis.Card(wp));
        }

        // Pre-conditions — must have / must NOT have (negative IDs = "NOT")
        AddCondGroup(_vis.Loc("Vis.BattleMove.UsPreCond"), bm.UsPreConditions, ",",
            nameof(BattleMove.UsPreConditions), "#FFF3E0", "#E65100");
        AddCondGroup(_vis.Loc("Vis.BattleMove.ThemPreCond"), bm.ThemPreConditions, ",",
            nameof(BattleMove.ThemPreConditions), "#FFF3E0", "#E65100");
        // Applied on success
        AddCondGroup(_vis.Loc("Vis.BattleMove.UsRequired"), bm.UsConditions, "],[",
            nameof(BattleMove.UsConditions), "#FCE4EC", "#C62828");
        AddCondGroup(_vis.Loc("Vis.BattleMove.ThemRequired"), bm.ThemConditions, "],[",
            nameof(BattleMove.ThemConditions), "#FCE4EC", "#C62828");
        AddCondGroup(_vis.Loc("Vis.BattleMove.SelfEffects"), bm.PairConditions, "],[",
            nameof(BattleMove.PairConditions), "#E8EAF6", "#283593");
        // Applied on fail
        AddCondGroup(_vis.Loc("Vis.BattleMove.UsFail"), bm.UsFailConditions, "],[",
            nameof(BattleMove.UsFailConditions), "#F5F5F5", "#999");
        AddCondGroup(_vis.Loc("Vis.BattleMove.ThemFail"), bm.ThemFailConditions, "],[",
            nameof(BattleMove.ThemFailConditions), "#F5F5F5", "#999");
        AddCondGroup(_vis.Loc("Vis.BattleMove.PairFail"), bm.PairFailConditions, "],[",
            nameof(BattleMove.PairFailConditions), "#F5F5F5", "#999");

        if (!hasAny)
            sp.Children.Add(new TextBlock
                { Text = "(No conditions)", FontSize = 11, Foreground = Brush.Parse("#999") });
        return sp;
    }

    // ═══════════════ Reverse References ═══════════════

    private Control BuildReverseRefsPanel(BattleMove bm)
        => _vis.BuildReverseRefsPanel(bm.EntityId);

    // ═══════════════ Helpers ═══════════════

    private (string label, string bg, string fg) GetTypeBadge(BattleMove bm)
    {
        var kind = bm.Offense ? _vis.Loc("Vis.Offensive")
            : bm.Retreat ? _vis.Loc("Vis.Retreat")
            : bm.Passive ? _vis.Loc("Vis.Passive")
            : _vis.Loc("Vis.Action");
        var attackLabel = GetAttackTypeLabel(bm);
        var label = $"{attackLabel} · {kind}";
        var bg = bm.Offense ? "#FFEBEE" : bm.Retreat ? "#E3F2FD" : bm.Passive ? "#F5F5F5" : "#FFF3E0";
        var fg = bm.Offense ? "#C62828" : bm.Retreat ? "#1565C0" : bm.Passive ? "#999" : "#E65100";
        return (label, bg, fg);
    }

    private string GetAttackTypeLabel(BattleMove bm) => bm.AttackModeType switch
    {
        BattleMoveType.NonAttack => _vis.Loc("Vis.NonAttack"),
        BattleMoveType.Melee => _vis.Loc("Vis.CombatMelee"),
        BattleMoveType.Ranged => _vis.Loc("Vis.CombatRanged"),
        _ => "?"
    };

    private static Symbol GetTypeIconSymbol(BattleMove bm) => bm.AttackModeType switch
    {
        BattleMoveType.NonAttack => Symbol.Question,
        BattleMoveType.Melee => Symbol.Flash,
        BattleMoveType.Ranged => Symbol.Target,
        _ => Symbol.Question
    };

    private List<(string label, int count)> GetConditionCounts(BattleMove bm)
    {
        var counts = new List<(string, int)>();

        int Count(string raw, string sep) => string.IsNullOrWhiteSpace(raw)
            ? 0
            : raw.Split(sep).Select(s => s.Trim()).Count(s => s.Length > 0);

        void Add(string label, int n)
        {
            if (n > 0) counts.Add((label, n));
        }

        Add(_vis.Loc("Vis.BattleMove.UsPreCond"), Count(bm.UsPreConditions, ","));
        Add(_vis.Loc("Vis.BattleMove.ThemPreCond"), Count(bm.ThemPreConditions, ","));
        Add(_vis.Loc("Vis.BattleMove.UsRequired"), Count(bm.UsConditions, "],["));
        Add(_vis.Loc("Vis.BattleMove.ThemRequired"), Count(bm.ThemConditions, "],["));
        Add(_vis.Loc("Vis.BattleMove.SelfEffects"), Count(bm.PairConditions, "],["));
        Add(_vis.Loc("Vis.BattleMove.UsFail"), Count(bm.UsFailConditions, "],["));
        Add(_vis.Loc("Vis.BattleMove.ThemFail"), Count(bm.ThemFailConditions, "],["));
        Add(_vis.Loc("Vis.BattleMove.PairFail"), Count(bm.PairFailConditions, "],["));
        return counts;
    }

    private string FmtExp(int level) => level switch
    {
        0 => $"{_vis.Loc("Vis.Exposure.Hidden")} (0)",
        1 => $"{_vis.Loc("Vis.Exposure.Seen")} (1)",
        _ => $"{_vis.Loc("Vis.Exposure.Any")} (2)"
    };

    private static StackPanel MiniKv(string label, string value) => new()
    {
        Spacing = 1, Children =
        {
            new TextBlock { Text = label, FontSize = 9, Foreground = Brush.Parse("#999") },
            new TextBlock
            {
                Text = value, FontSize = 12, FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#333")
            }
        }
    };
}
