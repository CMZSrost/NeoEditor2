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

public class ConditionEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Condition);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    // ═══ Field name translations from NEO全代码.注释与基础修改思路.xml ═══

    public ConditionEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router);
    }
    private static readonly Dictionary<string, string> ConditionFieldTranslations = new()
    {
        ["m_fHealPerHourMod"] = "每小时恢复能力",
        ["m_fImmuneRestoreRate"] = "免疫恢复能力",
        ["m_fBloodRestoreRate"] = "血液恢复能力",
        ["fMovesPerTurnModifier"] = "每回合移动点数",
        ["m_fEncumberanceLimit"] = "负重值",
        ["m_fMoraleHidden"] = "隐藏的士气值",
        ["m_fDefense"] = "闪避几率",
        ["Asleep"] = "陷入沉睡",
        ["fSleepQuality"] = "睡眠质量",
        ["m_fSleepAwareness"] = "睡眠意识",
        ["fFoodConsumptionRate"] = "食物消耗速率",
        ["fWaterConsumptionRate"] = "水消耗速率",
        ["BaseDetectionLevel"] = "基准警觉值",
        ["MinSafeTemp"] = "最低安全温度",
        ["MaxSafeTemp"] = "最高安全温度",
        ["m_fFatigueModifier"] = "疲劳修饰值",
        ["fPassiveRewarmPerHour"] = "每小时被动升温",
        ["m_fMorale"] = "士气",
        ["m_fBloodLeft"] = "血液总量",
        ["BodyInsulation"] = "身体热量散发值",
        ["m_fVisibility"] = "自身可见度",
        ["m_fMoveReserve"] = "行动点数",
        ["m_fMoveReserveRemaining"] = "行动点总量",
        ["MinLightLevel"] = "最小适应亮度",
        ["AttDmgMult"] = "攻击伤害",
        ["VisionRange"] = "视觉范围",
        ["m_fScent"] = "气味",
        ["LightLevel"] = "光照等级",
        ["fFoodDebt"] = "饥饿值",
        ["fWaterDebt"] = "饥渴值",
        ["m_fImmuneLeft"] = "免疫总量",
        ["m_fTrackingThreshold"] = "追踪阈值",
        ["m_fPainLeftBase"] = "疼痛总量阈值",
        ["m_fImmuneLeftBase"] = "免疫总量阈值",
        ["m_fPainLeft"] = "疼痛恢复",
        ["DefDmgMult"] = "防御伤害数值",
        ["fSleepDebt"] = "睡眠不足",
        ["fCoreTemp"] = "核心体温",
        ["m_fMovesLeft"] = "剩余行动点",
        ["WetTempAdjustMod"] = "潮湿调节数值",
        ["MoveCost"] = "额外行动点消耗",
        ["ChangeRange"] = "改变距离",
        ["Attack"] = "攻击动作",
        ["ExitBattle"] = "退出战斗",
        ["KnockDown"] = "击倒",
        ["JustMoved"] = "刚刚移动过",
        ["Discharge"] = "解除武装",
        ["Trip"] = "被绊倒",
        ["Bandaged"] = "包扎",
        ["Infected"] = "感染",
        ["Disinfected"] = "消毒",
        ["m_fPain"] = "疼痛",
        ["Crippled"] = "残废",
        ["Splinted"] = "使用夹板",
        ["Threat"] = "威胁",
        ["ChangeRangeAll"] = "朝所有人退后",
        ["TriggerEncounter"] = "触发剧情",
        ["m_nMorality"] = "道德值",
        ["LoseRandomItem"] = "丢失随机物品",
        ["LoseAllItems"] = "丢失所有物品",
        ["Money"] = "钱",
        ["GetDiagnostic"] = "获取诊断结果",
        ["ResetTemp"] = "核心体温调节",
        ["SpawnNewCreature"] = "繁殖新生物",
        ["DropAllItems"] = "丢下所有物品",
        ["LootTarget"] = "掠夺目标",
        ["ApplyCutDamage"] = "从伤口上取出异物",
        ["ScatterMissile"] = "导弹",
        ["UseGPS"] = "正在使用GPS",
        ["ResetUsSpotted"] = "重置视觉",
        ["EmptyGroundSlot"] = "空的地面格",
        ["CleanAndDress"] = "清理并包扎伤口",
        ["BattleRange"] = "攻击距离",
        ["AddRecipe"] = "得到配方",
    };

    private static string TranslateFieldName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return ConditionFieldTranslations.TryGetValue(name, out var translation)
            ? $"{translation} ({name})"
            : name;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Condition cond) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(cond), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(cond));
        root.Children.Add(BuildPropertiesPanel(cond));
        if (!string.IsNullOrWhiteSpace(cond.Description))
            root.Children.Add(BuildDescriptionPanel(cond));
        root.Children.Add(BuildModifiersPanel(cond));
        if (!string.IsNullOrWhiteSpace(cond.Effects))
            root.Children.Add(BuildEffectsPanel(cond));
        if (!string.IsNullOrWhiteSpace(cond.IdNext) && cond.IdNext != "0")
            root.Children.Add(BuildNextPanel(cond));
        root.Children.Add(BuildReverseRefsPanel(cond));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Condition cond)
    {
        var grid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star) }, Margin = new Thickness(0, 0, 0, 4) };
        var identity = new StackPanel { Spacing = 4 };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {cond.Id}", FontSize = 11, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#1565C0")
            }
        });

        _vis.AddModBadge(cond, idRow);

        var sevBg = cond.Fatal ? "#FFEBEE" : cond.Permanent ? "#FFF3E0" : "#E8F5E9";
        var sevFg = cond.Fatal ? "#C62828" : cond.Permanent ? "#E65100" : "#2E7D32";
        var sevLabel = cond.Fatal ? "FATAL" : cond.Permanent ? "Instant" : "Duration";
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(sevBg), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = sevLabel, FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(sevFg) }
        });
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (cond.Stackable)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E8F5E9"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "Stackable", FontSize = 10, Foreground = Brush.Parse("#2E7D32") }
            });
        if (!cond.Display)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#F5F5F5"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "Hidden", FontSize = 10, Foreground = Brush.Parse("#999") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = cond.Subject ?? cond.Name, FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        var statRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        statRow.Children.Add(new TextBlock
        {
            Text = cond.Permanent ? "Instant" : $"{cond.Duration}h", FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
        {
            Text = cond.Color switch
            {
                ConditionColor.Red => "Red (-)", ConditionColor.Green => "Green (+)",
                ConditionColor.Yellow => "Yellow", _ => "White"
            },
            FontSize = 11, Foreground = Brush.Parse("#666")
        });
        statRow.Children.Add(new TextBlock
            { Text = $"Transfer: {cond.TransferRange}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.ResetTimer)
            statRow.Children.Add(new TextBlock
                { Text = "ResetTimer", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.DisplayOther)
            statRow.Children.Add(new TextBlock
                { Text = "Visible to Others", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (cond.DisplayGameOver)
            statRow.Children.Add(new TextBlock
                { Text = "GameOver Log", FontSize = 11, Foreground = Brush.Parse("#666") });
        identity.Children.Add(statRow);

        if (!string.IsNullOrWhiteSpace(cond.Thresholds))
            identity.Children.Add(new TextBlock
                { Text = $"Thresholds: {cond.Thresholds}", FontSize = 11, Foreground = Brush.Parse("#6A1B9A") });
        if (!string.IsNullOrWhiteSpace(cond.ChanceNext) && cond.ChanceNext != "0")
            identity.Children.Add(new TextBlock
                { Text = $"Chance Next: {cond.ChanceNext}", FontSize = 11, Foreground = Brush.Parse("#666") });

        Grid.SetColumn(identity, 0);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    private Control BuildDescriptionPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Description")));
        var desc = cond.Description.Length > 800 ? cond.Description[..800] + "..." : cond.Description;
        sp.Children.Add(_vis.Card(new TextBlock
            { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
        return sp;
    }

    private Control BuildPropertiesPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Properties")));
        var cells = new List<(string, string, string?)>
        {
            (_vis.Loc("Vis.Duration"), cond.Permanent ? "Instant" : $"{cond.Duration}h", null),
            (_vis.Loc("Vis.Color"),
                cond.Color switch
                {
                    ConditionColor.Red => "Red (-)", ConditionColor.Green => "Green (+)",
                    ConditionColor.Yellow => "Yellow", _ => "White"
                }, null),
            (_vis.Loc("Vis.Transfer"), cond.TransferRange >= 0 ? $"{cond.TransferRange}" : "None", null),
        };
        if (cond.Fatal) cells.Add((_vis.Loc("Vis.Fatal"), "Yes", "#C62828"));
        if (cond.Permanent) cells.Add((_vis.Loc("Vis.Permanent"), "Yes", "#E65100"));
        if (cond.Stackable) cells.Add((_vis.Loc("Vis.Stackable"), "Yes", "#2E7D32"));
        if (!cond.Display) cells.Add((_vis.Loc("Vis.Hidden"), "Yes", "#999"));
        if (cond.DisplayOther) cells.Add((_vis.Loc("Vis.DisplayOther"), "Yes", "#666"));
        cells.Add((_vis.Loc("Vis.ResetTimer"),
            cond.ResetTimer ? _vis.Loc("Vis.Yes") : _vis.Loc("Vis.No"),
            cond.ResetTimer ? "#2E7D32" : "#E65100"));
        if (cond.RemoveAll) cells.Add((_vis.Loc("Vis.RemoveAll"), "Yes", "#999"));
        if (cond.RemovePostCombat) cells.Add((_vis.Loc("Vis.RemovePostCombat"), "Yes", "#999"));
        if (cond.DisplayGameOver) cells.Add((_vis.Loc("Vis.DisplayGameOver"), "Yes", "#666"));
        if (!string.IsNullOrWhiteSpace(cond.Thresholds))
            cells.Add((_vis.Loc("Vis.Thresholds"), cond.Thresholds, "#6A1B9A"));
        sp.Children.Add(_vis.CreatureStatGrid(cells));
        return sp;
    }

    private Control BuildModifiersPanel(Condition cond)
    {
        var names = (cond.FieldNames ?? "").Split(',').Select(s => s.Trim()).ToList();
        var mods = (cond.Modifiers ?? "").Split(',').Select(s => s.Trim()).ToList();
        if (names.Count == 0 || names.All(string.IsNullOrEmpty)) return new StackPanel();

        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Modifiers")));

        // Collect valid pairs
        var pairs = new List<(string name, double val)>();
        for (int i = 0; i < Math.Max(names.Count, mods.Count); i++)
        {
            var name = i < names.Count ? names[i] : "";
            var modStr = i < mods.Count ? mods[i] : "";
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(modStr)) continue;
            if (double.TryParse(modStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                pairs.Add((name, val));
        }

        if (pairs.Count == 0) return new StackPanel();

        // Grid layout: field name | value text | bar
        var maxAbs = Math.Max(pairs.Max(p => Math.Abs(p.val)), 0.01);
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new(260, GridUnitType.Pixel), // field name (wider for Chinese)
                new(64, GridUnitType.Pixel), // value
                new(1, GridUnitType.Star) // bar fill
            },
            Margin = new Thickness(4, 0)
        };
        int row = 0;
        foreach (var (name, val) in pairs)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));
            var isNeg = val < 0;
            var absRatio = Math.Clamp(Math.Abs(val) / maxAbs, 0.08, 1.0);
            var barColor = isNeg ? "#C62828" : val > 0 ? "#2E7D32" : "#999";

            // Field name (with Chinese translation if available)
            var nameTb = new TextBlock
            {
                Text = TranslateFieldName(name), FontSize = 10, Foreground = Brush.Parse("#555"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 2, 8, 2),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(nameTb, row);
            Grid.SetColumn(nameTb, 0);
            grid.Children.Add(nameTb);

            // Value text
            var valTb = new TextBlock
            {
                Text = $"{val:+#.##;-#.##;0}", FontSize = 10, FontWeight = FontWeight.Medium,
                Foreground = Brush.Parse(barColor),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 8, 2)
            };
            Grid.SetRow(valTb, row);
            Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            // Bar (centered at 0)
            var barRow = new Grid { Margin = new Thickness(0, 3) };
            barRow.ColumnDefinitions.Add(new(1, GridUnitType.Star));
            barRow.ColumnDefinitions.Add(new(3, GridUnitType.Pixel));
            barRow.ColumnDefinitions.Add(new(1, GridUnitType.Star));

            // Center line
            var center = new Border { Background = Brush.Parse("#18000000"), Margin = new Thickness(0, 0) };
            Grid.SetColumn(center, 1);
            barRow.Children.Add(center);

            if (isNeg)
            {
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(3, 0, 0, 3),
                    Background = Brush.Parse(barColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Width = absRatio * 120, MaxWidth = 120
                };
                Grid.SetColumn(fill, 0);
                barRow.Children.Add(fill);
            }
            else if (val > 0)
            {
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(0, 3, 3, 0),
                    Background = Brush.Parse(barColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Height = 14,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = absRatio * 120, MaxWidth = 120
                };
                Grid.SetColumn(fill, 2);
                barRow.Children.Add(fill);
            }

            Grid.SetRow(barRow, row);
            Grid.SetColumn(barRow, 2);
            grid.Children.Add(barRow);

            row++;
        }

        sp.Children.Add(_vis.Card(grid));
        return sp;
    }

    private Control BuildEffectsPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Effects")));
        var eff = cond.Effects.Length > 800 ? cond.Effects[..800] + "..." : cond.Effects;
        sp.Children.Add(_vis.Card(new TextBlock
        {
            Text = eff, FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#00695C"),
            FontFamily = "Consolas, monospace"
        }));
        return sp;
    }

    private Control BuildNextPanel(Condition cond)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.ConditionChain")));
        var chainStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var segments = cond.IdNext.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0 && s != "0").ToList();
        if (segments.Count == 0)
        {
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = "(No next stage)", FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
                chainStack.Children.Add(new TextBlock
                {
                    Text = "→", FontSize = 14, Foreground = Brush.Parse("#6A1B9A"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            chainStack.Children.Add(_refNode.Badge<Condition>(cond, nameof(Condition.IdNext), segments[i],
                "#F3E5F5", "#6A1B9A", unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
        }

        // Chance indicators
        if (!string.IsNullOrWhiteSpace(cond.ChanceNext) && cond.ChanceNext != "0")
        {
            sp.Children.Add(_vis.Card(chainStack));
            sp.Children.Add(_vis.SectionLabel("Progression Chances"));
            var chanceParts = cond.ChanceNext.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var chanceStack = new StackPanel { Spacing = 4 };
            foreach (var part in chanceParts)
            {
                var eq = part.Split('=');
                var chLabel = part;
                double chVal = 0;
                if (eq.Length == 2 && double.TryParse(eq[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var cv))
                {
                    chVal = cv;
                    var chPct = (int)(chVal * 100);
                    chLabel = $"{eq[0]} → {chPct}%";
                }

                var color = chVal >= 1.0 ? "#2E7D32" : chVal >= 0.5 ? "#E65100" : chVal > 0 ? "#999" : "#999";
                var barWidth = Math.Max(chVal * 200, 30);
                chanceStack.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = Brush.Parse(color),
                    Height = 20,
                    Width = barWidth,
                    Child = new TextBlock
                    {
                        Text = chLabel, FontSize = 10, Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(6, 0)
                    }
                });
            }

            sp.Children.Add(_vis.Card(chanceStack));
            return sp;
        }

        sp.Children.Add(_vis.Card(chainStack));
        return sp;
    }

    private Control BuildReverseRefsPanel(Condition cond)
        => _vis.BuildReverseRefsPanel(cond.EntityId);
}
