using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.EntityEditor.Services;

namespace NeoEditor.Plugins.EntityEditor.Visualizers;

/// <summary>
/// D05: Creature（生物）detail visualizer — 单实体 · 只读 · 语义翻译。
/// 回答「这个生物在游戏里是什么、怎么打、属性如何、掉什么、在哪遇到」。
/// 布局（R40 两段式）：Raw Data（折叠）→ Hero → 情境 1（⚔ 战斗 | 🧬 属性与出场状态）
/// → 情境 2（🎁 战利品 | 📍 遭遇）→ 被引用面板（横贯底部）。
/// </summary>
public class CreatureEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Creature);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    /// <summary>Create with injected services.</summary>
    public CreatureEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router,
            vis.BuildRefTooltip);
        _dataTable = dataTable!;
    }

    // ═══════════════ Detail ═══════════════

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Creature c) return new TextBlock { Text = "Invalid" };
        // D05 §二: R40 两段式信息架构 — 认知顺序：是谁 → 怎么打多强 → 掉什么 → 在哪遇 → 改动影响。
        var root = new StackPanel { Spacing = 14, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(c));
        root.Children.Add(BuildHeroHeader(c));

        // 情境 1（两列）：⚔ 战斗 | 🧬 属性与出场状态
        AddRow(root,
            Section(_vis.Loc("Vis.Combat"), BuildCombatBody(c), Symbol.Flash, "#C62828"),
            Section(_vis.Loc("Vis.Attributes"), BuildAttributesBody(c), Symbol.Pulse, "#6A1B9A"));

        // 情境 2（两列）：🎁 战利品 | 📍 遭遇（Creature 特有）
        AddRow(root,
            Section(_vis.Loc("Vis.Loot"), BuildLootBody(c), Symbol.Gift, "#2E7D32"),
            Section(_vis.Loc("Vis.Encounters"), BuildEncounterBody(c), Symbol.Map, "#00695C"));

        // 被引用（横贯底部）
        root.Children.Add(BuildReverseRefsPanel(c));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    /// <summary>R40: 一个区块 = 图标 SectionHeader + Card body；body 为 null 时整个区块跳过。</summary>
    private Control? Section(string title, Control? body, Symbol icon, string accent)
        => body is null ? null
            : new StackPanel { Spacing = 8, Children = { _vis.SectionHeader(title, icon, accent: accent), _vis.Card(body) } };

    /// <summary>R40: 左右两块并排；某侧缺失时另一侧整行合并。</summary>
    private static void AddRow(StackPanel root, Control? left, Control? right)
    {
        if (left is null && right is null) return;
        if (left is null) { root.Children.Add(right!); return; }
        if (right is null) { root.Children.Add(left); return; }
        var row = new Grid
        {
            ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star) },
            ColumnSpacing = 14
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        row.Children.Add(left);
        row.Children.Add(right);
        root.Children.Add(row);
    }

    // ═══════════════ Hero header: 图片区 + 身份（D05 §4.1）═══════════════

    private Control BuildHeroHeader(Creature c)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        // ── 图片区（左上，132×132，点击放大；无图 Person 图标兜底）──
        var bmp = _vis.LoadImage(c.Image.ToRawString(","));
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
            var capturedBmp = bmp;
            imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(capturedBmp, c.Subject ?? c.Name);
        }
        else
        {
            // 无图兜底：Person 图标（不崩溃）
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.Person, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        }
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        // ── 身份区（右侧）──
        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

        // 徽章行：ID + mod 徽章（D05 §4.1: id 是游戏内引用键，刷新点/剧情直接引用它）
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var idBadge = new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {c.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        };
        ToolTip.SetTip(idBadge, _vis.Loc("Vis.CreatureIdHint"));
        idRow.Children.Add(idBadge);
        _vis.AddModBadge(c, idRow);
        identity.Children.Add(idRow);

        // 数字行：行动点橙 chip + 阵营名 chip（0=玩家/中立 不显示，可 Ctrl+Click 跳转 Faction）
        var infoRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 0) };
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFF3E0"), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = $"{c.MovesPerTurn} moves/turn", FontSize = 10, Foreground = Brush.Parse("#E65100") }
        });
        var factionRaw = c.Faction.ToRawString(null);
        if (!string.IsNullOrWhiteSpace(factionRaw) && factionRaw != "0")
        {
            var faction = _vis.Resolver.LookupRef<Faction>(c, nameof(Creature.Faction), factionRaw);
            if (faction is not null)
                infoRow.Children.Add(_refNode.BadgeForEntity(c, faction,
                    faction.Subject ?? faction.Name, "#E8EAF6", "#283593"));
            else
                infoRow.Children.Add(_vis.MiniBadge(factionRaw, "#F5F5F5", "#999")); // 未解析灰色兜底
        }
        if (infoRow.Children.Count > 0)
            identity.Children.Add(infoRow);

        // 标题 strName（18px bold）
        identity.Children.Add(new TextBlock
        {
            Text = c.Subject ?? c.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap
        });
        // strNamePublic：未接触前的名称，与 Name 相同则隐藏（去噪）
        if (!string.IsNullOrWhiteSpace(c.NamePublic) && c.NamePublic != c.Name)
            identity.Children.Add(new TextBlock
            {
                Text = c.NamePublic, FontSize = 12, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#888")
            });
        // strNotes：剧情身份注解，为空隐藏
        if (!string.IsNullOrWhiteSpace(c.Notes))
            identity.Children.Add(new TextBlock
                { Text = c.Notes, FontSize = 11, Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);
        return _vis.Card(grid);
    }

    // ═══════════════ 战斗区块（D05 §4.2 三层）═══════════════

    private sealed record CreatureAttack(AttackMode Mode, bool IsUnresolved);

    /// <summary>解析 vAttackModes：生物无槽位前缀（不像物品分左右手），直接裸 ID 解析。</summary>
    private List<CreatureAttack> ParseAttackModes(Creature c)
    {
        var result = new List<CreatureAttack>();
        foreach (var seg in c.AttackModes.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var am = _vis.Resolver.LookupRef<AttackMode>(c, nameof(Creature.AttackModes), seg);
            if (am is null)
            {
                // 未解析 — 保留原始段可见（灰色行，无展开，可审计）
                result.Add(new CreatureAttack(new AttackMode
                {
                    EntityId = $"__unresolved_{seg}",
                    Name = seg,
                    DamageCut = 0,
                    DamageBlunt = 0
                }, true));
                continue;
            }
            result.Add(new CreatureAttack(am, false));
        }
        return result;
    }

    private Control? BuildCombatBody(Creature c)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        var modes = ParseAttackModes(c);
        if (modes.Count > 0)
        {
            // 1. Σ 总伤害条（第一层）：Cut 红 / Blunt 蓝比例条 — 回答「切割型还是钝击型」
            var totalCut = modes.Sum(m => m.Mode.DamageCut);
            var totalBlunt = modes.Sum(m => m.Mode.DamageBlunt);
            body.Children.Add(_vis.StackedDamageBar(_vis.Loc("Vis.TotalDamage"), totalCut, totalBlunt));

            // 2. Σ 有效伤害（第二层，R41：没有比较对象的填充条无意义，指标值即可）
            var totalBase = totalCut + totalBlunt;
            var totalEffective = modes.Sum(m => (m.Mode.DamageCut + m.Mode.DamageBlunt) * (1 + m.Mode.Morale));
            if (totalEffective > totalBase + 0.001)
            {
                body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.Effective"),
                    $"{totalEffective:F1} (×{totalEffective / Math.Max(totalBase, 0.01):F2})", "#9575CD"));
            }

            // 全部为 1（拳头）时：默认值去噪注释
            if (modes.All(m => !m.IsUnresolved && m.Mode.Id == 1))
                body.Children.Add(new TextBlock
                    { Text = _vis.Loc("Vis.FistsOnly"), FontSize = 10, Foreground = Brush.Parse("#999") });

            // 3. 逐攻击模式行 + 展开详情（第三层）
            foreach (var entry in modes)
                body.Children.Add(BuildAttackModeRow(c, entry));
            hasAny = true;
        }

        // 阵营关系增强项：Faction.dictFactions「对玩家(0)」声望（回答「见面打不打」）
        var relation = BuildFactionRelation(c);
        if (relation is not null)
        {
            body.Children.Add(_vis.Separator());
            body.Children.Add(relation);
            hasAny = true;
        }

        return hasAny ? body : null;
    }

    /// <summary>阵营关系行：声望 ≥50 绿（友好）/ 0-50 灰（中立）/ &lt;0 红（敌对）；解析失败静默隐藏。</summary>
    private Control? BuildFactionRelation(Creature c)
    {
        var factionRaw = c.Faction.ToRawString(null);
        if (string.IsNullOrWhiteSpace(factionRaw) || factionRaw == "0") return null;
        var faction = _vis.Resolver.LookupRef<Faction>(c, nameof(Creature.Faction), factionRaw);
        if (faction is null || string.IsNullOrWhiteSpace(faction.DictFactions.ToRawString(","))) return null;

        double? rel = null;
        foreach (var seg in faction.DictFactions.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var parts = seg.Split('=');
            if (parts.Length < 2) continue;
            if (parts[0].Trim() == "0"
                && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                rel = v;
                break;
            }
        }
        if (rel is not double relVal) return null; // 无「0=」条目 → 静默隐藏

        var desc = relVal >= 50 ? "友好" : relVal >= 0 ? "中立" : "敌对";
        // 0-50 中立 → 灰色填充；≥50 友好 → 默认绿；<0 敌对 → 默认红
        var posColor = relVal >= 50 ? null : "#9E9E9E";
        return _vis.CenteredStatBar(_vis.Loc("Vis.TowardPlayer"),
            $"{relVal:+#;-#;0} ({desc})", relVal, 100, posColor: posColor);
    }

    /// <summary>单个攻击模式行：名称 | 伤害堆叠条 | 射程/穿透/士气/音效 | ▶展开（D05 §4.2 第三层）。</summary>
    private Control BuildAttackModeRow(Creature c, CreatureAttack entry)
    {
        var am = entry.Mode;
        var detail = BuildAttackModeExpanded(c, am);
        detail.IsVisible = false;
        var arrow = new TextBlock
        {
            Text = "▶", FontSize = 10, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameTb = new TextBlock
        {
            Text = am.Subject ?? am.Name,
            FontSize = 12,
            FontWeight = entry.IsUnresolved ? FontWeight.Normal : FontWeight.Medium,
            Foreground = Brush.Parse(entry.IsUnresolved ? "#999" : "#333"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220
        };
        if (!entry.IsUnresolved)
        {
            _refNode.WireNavigation(nameTb, typeof(AttackMode), am.EntityId, am);
            ToolTip.SetTip(nameTb, _vis.BuildRefTooltip(am));
        }

        var meta = new TextBlock
        {
            FontSize = 10, Foreground = Brush.Parse("#777"), VerticalAlignment = VerticalAlignment.Center,
            Text = string.Join(" · ",
                new[]
                {
                    am.Range > 1 || am.Type == AttackType.Ranged ? $"{_vis.Loc("Vis.Range")} {am.Range}" : null,
                    am.Penetration > 0 ? $"{_vis.Loc("Vis.Penetration")} {am.Penetration}" : null,
                    // 武器自带士气补正（实际伤害公式 (1+角色士气+武器士气)×(1+加成)×武器伤害，Doc 38）
                    am.Morale != 0 ? $"{_vis.Loc("Morale")} {am.Morale:+0%;-0%;0}" : null,
                    !string.IsNullOrWhiteSpace(am.Sound) && am.Sound != "cueNone" ? am.Sound : null
                }.Where(s => s is not null))
        };

        // R42: 行内试听攻击音效（无音频索引自动隐藏）
        var playBtn = !string.IsNullOrWhiteSpace(am.Sound) && am.Sound != "cueNone"
            ? _vis.PlaySoundButton(am.Sound)
            : null;

        var row = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(1, GridUnitType.Star), new(GridLength.Auto) },
            Margin = new Thickness(0, 1)
        };
        Grid.SetColumn(nameTb, 0);
        row.Children.Add(nameTb);
        var bar = _vis.StackedDamageBar("", am.DamageCut, am.DamageBlunt);
        Grid.SetColumn(bar, 1);
        row.Children.Add(bar);
        var rightPanel = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        rightPanel.Children.Add(meta);
        if (playBtn is not null)
            rightPanel.Children.Add(playBtn);
        rightPanel.Children.Add(arrow);
        Grid.SetColumn(rightPanel, 2);
        row.Children.Add(rightPanel);

        var expanded = false;
        var header = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#0A000000"),
            Padding = new Thickness(10, 5),
            Cursor = entry.IsUnresolved ? null : new Cursor(StandardCursorType.Hand),
            Child = row
        };
        if (!entry.IsUnresolved)
        {
            header.PointerPressed += (_, e) =>
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0) return; // Ctrl+Click = 跳转（WireNavigation）
                expanded = !expanded;
                arrow.Text = expanded ? "▼" : "▶";
                detail.IsVisible = expanded;
            };
        }

        var sp = new StackPanel { Spacing = 2, Children = { header, detail } };
        return sp;
    }

    /// <summary>攻击模式展开详情：图标 + 近战/远程 + 士气% + 有效伤害 + 公式注 + 数值格 + 弹药 + 条件 + 短语 + 注解。</summary>
    private Control BuildAttackModeExpanded(Creature c, AttackMode am)
    {
        var sp = new StackPanel { Spacing = 8, Margin = new Thickness(14, 4, 4, 2) };

        // ── R41 紧凑顶行：36px 武器图标 + 近战/远程 + 士气%（25% 标注 base）+ 有效伤害 ──
        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var imgRaw = am.Image.ToRawString(",");
        if (!string.IsNullOrWhiteSpace(imgRaw))
        {
            var bmp = _vis.LoadImage(imgRaw);
            if (bmp is not null)
            {
                topRow.Children.Add(new Border
                {
                    Width = 36, Height = 36, CornerRadius = new CornerRadius(5),
                    Background = Brush.Parse("#0A000000"), ClipToBounds = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new Image { Source = bmp, Stretch = Stretch.Uniform }
                });
            }
        }
        topRow.Children.Add(new TextBlock
        {
            Text = am.Type == AttackType.Ranged ? _vis.Loc("Vis.CombatRanged") : _vis.Loc("Vis.CombatMelee"),
            FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#555"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var moralePct = (int)(am.Morale * 100);
        var moraleLabel = moralePct == 25 ? $"{moralePct}% (base)" : $"{moralePct}%";
        var moraleColor = am.Morale > 0.25 ? "#66BB6A" : am.Morale < 0.25 ? "#E57373" : "#78909C";
        topRow.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Morale")} {moraleLabel}", FontSize = 11, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(moraleColor), VerticalAlignment = VerticalAlignment.Center
        });

        var totalDmg = am.DamageCut + am.DamageBlunt;
        if (totalDmg > 0)
        {
            var effectiveDmg = totalDmg * (1 + am.Morale);
            topRow.Children.Add(new TextBlock
            {
                Text = $"{_vis.Loc("Vis.Effective")} {effectiveDmg:F1} (×{1 + am.Morale:F2})",
                FontSize = 11, FontWeight = FontWeight.Medium,
                Foreground = Brush.Parse("#9575CD"), VerticalAlignment = VerticalAlignment.Center
            });
        }
        sp.Children.Add(topRow);

        // R37 公式注：实际伤害 = (1+角色士气+武器士气)×(1+近战/远程加成)×武器伤害
        if (totalDmg > 0)
        {
            sp.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.DamageFormula"),
                FontSize = 9, Foreground = Brush.Parse("#999"), TextWrapping = TextWrapping.Wrap
            });
        }

        // 数值格：射程 / 穿透 / 弹药转移
        var cells = new List<(string, string, string?)>();
        if (am.Range > 1 || am.Type == AttackType.Ranged)
            cells.Add((_vis.Loc("Vis.Range"), $"{am.Range}", "#1565C0"));
        if (am.Penetration > 0)
            cells.Add((_vis.Loc("Vis.Penetration"), $"{am.Penetration}", "#6A1B9A"));
        if (am.Transfer)
            cells.Add(("Transfer", _vis.Loc("Vis.Yes"), "#546E7A"));
        if (cells.Count > 0)
            sp.Children.Add(_vis.CreatureStatGrid(cells));

        // 弹药：ChargeProfile 徽章带消耗率，degrade 加 ⚠
        if (!string.IsNullOrWhiteSpace(am.ChargeProfiles.ToRawString(",")))
        {
            var wp = new WrapPanel();
            foreach (var seg in am.ChargeProfiles.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cp = _vis.Resolver.LookupRef<ChargeProfile>(am, nameof(AttackMode.ChargeProfiles), seg);
                if (cp is not null)
                {
                    var rates = new List<string>();
                    if (cp.PerUse > 0) rates.Add($"{_vis.Loc("Vis.PerUse")} {cp.PerUse:F2}");
                    if (cp.PerHour > 0) rates.Add($"{_vis.Loc("Vis.PerHour")} {cp.PerHour:F2}");
                    if (cp.PerHourEquipped > 0) rates.Add($"{_vis.Loc("Vis.PerHourEquipped")} {cp.PerHourEquipped:F2}");
                    if (cp.PerHex > 0) rates.Add($"{_vis.Loc("Vis.PerHex")} {cp.PerHex:F2}");
                    var label = cp.Subject ?? cp.Name ?? $"CP#{cp.Id}";
                    if (rates.Count > 0) label += $" ({string.Join(" · ", rates)})";
                    if (cp.Degrade) label += " ⚠";
                    wp.Children.Add(_refNode.BadgeForEntity(am, cp, label, "#E0F7FA", "#006064"));
                }
                else
                {
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
                }
            }
            sp.Children.Add(LabeledSection(_vis.Loc("Vis.ChargeAmmo"), wp));
        }

        // 攻击者条件（语义色：Fatal 红 / Instant 橙 / Stackable 绿 / 时长蓝）
        if (!string.IsNullOrWhiteSpace(am.AttackerConditions.ToRawString(",")))
        {
            var wp = new WrapPanel();
            foreach (var seg in am.AttackerConditions.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = _vis.Resolver.LookupRef<Condition>(am, nameof(AttackMode.AttackerConditions), seg);
                if (cond is not null)
                {
                    var extra = ReferencePattern.FromName("{id}x{mult}").FormatExtraInfo(seg);
                    wp.Children.Add(_refNode.BadgeForEntity(am, cond,
                        ConditionLabel(cond, extra), ConditionBg(cond), ConditionFg(cond)));
                }
                else
                {
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
                }
            }
            sp.Children.Add(LabeledSection(_vis.Loc("Vis.AttackerConditions"), wp));
        }

        // 挥击短语（斜体引用）→ 攻击短语（蓝徽章）→ 注解
        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
            sp.Children.Add(new TextBlock
            {
                Text = $"“{am.WieldPhrase}”", FontSize = 11, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap
            });

        if (!string.IsNullOrWhiteSpace(am.AttackPhrases))
        {
            var wp = new WrapPanel();
            foreach (var p in am.AttackPhrases.Split(',', '，').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var display = p.Length > 60 ? p[..60] + "..." : p;
                wp.Children.Add(_vis.MiniBadge(display, "#E3F2FD", "#1565C0"));
            }
            sp.Children.Add(LabeledSection(_vis.Loc("Vis.AttackPhrases"), wp));
        }

        if (!string.IsNullOrWhiteSpace(am.Notes))
            sp.Children.Add(new TextBlock
            {
                Text = am.Notes, FontSize = 10, Foreground = Brush.Parse("#888"),
                TextWrapping = TextWrapping.Wrap
            });

        sp.Children.Add(new TextBlock
        {
            Text = _vis.Loc("Vis.CtrlClickHint"), FontSize = 9, Foreground = Brush.Parse("#999")
        });
        return sp;
    }

    // ═══════════════ 属性与出场状态区块（D05 §4.3）═══════════════

    private Control? BuildAttributesBody(Creature c)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        // 属性格：只呈现真实数字（行动点/攻击模式数/出场状态数/战利品池数），
        // 不预留虚构槽位（nHP/nStrength 等在游戏数据中不存在，D05 §4.3 设计口径）
        var cells = new List<(string, string, string?)>();
        if (c.MovesPerTurn > 0)
            cells.Add((_vis.Loc("Vis.MovesPerTurn"), $"{c.MovesPerTurn}", "#E65100"));
        var atkCount = CountSegments(c.AttackModes);
        if (atkCount > 0)
            cells.Add((_vis.Loc("Vis.Attacks"), $"{atkCount}", "#C62828"));
        var condCount = CountSegments(c.BaseConditions);
        if (condCount > 0)
            cells.Add((_vis.Loc("Vis.SpawnStatus"), $"{condCount}", "#C62828"));
        var poolCount = CountPools(c);
        if (poolCount > 0)
            cells.Add((_vis.Loc("Vis.LootTable"), $"{poolCount}", "#2E7D32"));
        if (cells.Count > 0)
        {
            body.Children.Add(_vis.CreatureStatGrid(cells));
            hasAny = true;
        }

        // 出场状态：状态=概率 → 状态概率徽章（hover 显示条件效果翻译）
        var statusBadges = BuildSpawnStatusBadges(c);
        if (statusBadges is not null)
        {
            body.Children.Add(statusBadges);
            hasAny = true;
        }

        // 日常行为：轻量徽章行（最多 30 个 + +N more，有意的低价值弱化）
        var activities = BuildActivitiesBadges(c);
        if (activities is not null)
        {
            body.Children.Add(activities);
            hasAny = true;
        }

        return hasAny ? body : null;
    }

    private static int CountSegments(ReferenceList<IReferenceEntry> list)
        => list.ToRawString(",").Split(',').Count(s => s.Trim().Length > 0);

    private int CountPools(Creature c)
    {
        var n = 0;
        var carried = c.TreasureId.ToRawString(null);
        if (!string.IsNullOrWhiteSpace(carried) && carried != "3") n++;
        var corpse = c.CorpseId.ToRawString(null);
        if (!string.IsNullOrWhiteSpace(corpse) && corpse != "3") n++;
        return n;
    }

    /// <summary>出场状态徽章：`{id}={value}` 段 → 状态名 + 概率后缀（1 无后缀，&lt;1 `· 50%`）。</summary>
    private Control? BuildSpawnStatusBadges(Creature c)
    {
        var raw = c.BaseConditions.ToRawString(",");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var wp = new WrapPanel();
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cond = _vis.Resolver.LookupRef<Condition>(c, nameof(Creature.BaseConditions), seg);
            if (cond is not null)
            {
                var prob = 1.0;
                var eqIdx = seg.IndexOf('=');
                if (eqIdx > 0 && double.TryParse(seg[(eqIdx + 1)..].Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                    prob = p;
                var suffix = prob >= 1.0 ? "" : $" · {prob.ToString("0%", CultureInfo.InvariantCulture)}";
                wp.Children.Add(_refNode.BadgeForEntity(c, cond, $"{cond.Subject}{suffix}",
                    ConditionBg(cond), ConditionFg(cond)));
            }
            else
            {
                wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999")); // 未解析灰色兜底
            }
        }
        return LabeledSection(_vis.Loc("Vis.SpawnStatus"), wp);
    }

    /// <summary>日常行为：待机活动描述（逗号分隔，仅注释用途）→ 轻量徽章行。</summary>
    private Control? BuildActivitiesBadges(Creature c)
    {
        var acts = c.Activities.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (acts.Count == 0) return null;
        var wp = new WrapPanel();
        foreach (var act in acts.Take(30))
            wp.Children.Add(_vis.MiniBadge(act, "#E8EAF6", "#283593"));
        if (acts.Count > 30)
            wp.Children.Add(_vis.MiniBadge($"+{acts.Count - 30} more", "#F5F5F5", "#999"));
        return LabeledSection(_vis.Loc("Vis.Activities"), wp);
    }

    // ═══════════════ 战利品区块（D05 §4.4 双池并置）═══════════════

    private Control? BuildLootBody(Creature c)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        // 随身携带池（nTreasureID，3=空池无信息量隐藏）
        var carried = BuildPoolSection(_vis.Loc("Vis.CarriedLoot"),
            c, nameof(Creature.TreasureId), c.TreasureId, "#E8F5E9", "#2E7D32");
        if (carried is not null) { body.Children.Add(carried); hasAny = true; }

        // 尸体掉落池（nCorpseID）— 与携带池并置对比（活着搜 vs 杀掉摸尸）
        var corpse = BuildPoolSection(_vis.Loc("Vis.CorpseLoot"),
            c, nameof(Creature.CorpseId), c.CorpseId, "#FCE4EC", "#880E4F");
        if (corpse is not null) { body.Children.Add(corpse); hasAny = true; }

        return hasAny ? body : null;
    }

    /// <summary>单个战利品池：池名徽章（可跳转）+ 内联战利品树（概率 = 权重/Σ权重）。</summary>
    private Control? BuildPoolSection(string label, Creature c, string propName,
        ReferenceList<IReferenceEntry> refList, string badgeBg, string badgeFg)
    {
        var raw = refList.ToRawString(null);
        if (string.IsNullOrWhiteSpace(raw) || raw == "3") return null; // 3=空池

        var tt = _vis.Resolver.LookupRef<TreasureTable>(c, propName, raw);
        var section = new StackPanel { Spacing = 4 };
        if (tt is not null)
        {
            section.Children.Add(_refNode.BadgeForEntity(c, tt,
                tt.Subject ?? tt.Name ?? $"TT#{tt.Id}", badgeBg, badgeFg));
            if (!string.IsNullOrWhiteSpace(tt.Treasures.ToRawString(",")))
                section.Children.Add(BuildTreasureLootTree(tt));
        }
        else
        {
            // 未解析引用：灰色兜底（不崩溃、不静默丢失）
            section.Children.Add(_vis.MiniBadge(raw, "#F5F5F5", "#999"));
        }
        return LabeledSection(label, section);
    }

    /// <summary>内联战利品树：解析 `物品x权重x数量` 段，真实概率 = 权重/Σ权重；
    /// 直接复用 TreasureTable 视觉器的 internal static 组件（BuildItemRow / BuildNestedItems）。</summary>
    private Control BuildTreasureLootTree(TreasureTable tt)
    {
        var sp = new StackPanel { Spacing = 2 };
        if (string.IsNullOrWhiteSpace(tt.Treasures.ToRawString(",")))
        {
            sp.Children.Add(new TextBlock
                { Text = _vis.Loc("Vis.Empty"), FontSize = 10, Foreground = Brush.Parse("#999") });
            return sp;
        }

        var itemTypes = _dataTable.GetCompositeEntities<ItemType>(
            it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);

        var allSegs = tt.Treasures.ToRawString(",").Split('|', ',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s.Contains('x'))
            .ToList();

        var allParsed = new List<(string itemId, double weight, string qtyRange)>();
        double totalWeight = 0;
        foreach (var seg in allSegs)
        {
            var parts = seg.Split('x');
            if (parts.Length < 2) continue;
            var itemId = parts[0].Trim();
            var weightStr = parts[1].Trim();
            var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
            var weight = double.TryParse(weightStr, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var w) ? w : 1.0;
            totalWeight += weight;
            allParsed.Add((itemId, weight, qtyRange));
        }

        foreach (var (itemId, weight, qtyRange) in allParsed)
        {
            var actualProb = totalWeight > 0 ? weight / totalWeight : 1.0 / allParsed.Count;

            if (itemTypes.TryGetValue(itemId, out var matched))
            {
                sp.Children.Add(TreasureTableEntityVisualizer.BuildItemRow(
                    _vis,
                    matched.Description ?? matched.Name ?? itemId, "ItemType", "#E0F2F1", "#00695C",
                    _refNode.NavAction(typeof(ItemType), matched.EntityId),
                    weight, actualProb, qtyRange));
            }
            else
            {
                var nested = _vis.Resolver.LookupRef<TreasureTable>(tt,
                    nameof(TreasureTable.Treasures), itemId);
                if (nested is not null)
                {
                    var row = TreasureTableEntityVisualizer.BuildItemRow(
                        _vis,
                        nested.Name ?? $"TT#{nested.Id}", "TT", "#E8EAF6", "#283593",
                        _refNode.NavAction(typeof(TreasureTable), nested.EntityId),
                        weight, actualProb, qtyRange);
                    var sub = TreasureTableEntityVisualizer.BuildNestedItems(
                        _vis, _dataTable, nested, itemTypes, 1, _refNode);
                    sp.Children.Add(row);
                    if (sub is not null)
                    {
                        var subExpanded = true;
                        sub.IsVisible = true;
                        row.Cursor = new Cursor(StandardCursorType.Hand);
                        row.PointerPressed += (_, e) =>
                        {
                            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
                            {
                                subExpanded = !subExpanded;
                                sub.IsVisible = subExpanded;
                            }
                        };
                        sp.Children.Add(sub);
                    }
                }
                else
                {
                    sp.Children.Add(TreasureTableEntityVisualizer.BuildItemRow(
                        _vis,
                        itemId, null, "#F5F5F5", "#999", null,
                        weight, actualProb, qtyRange));
                }
            }
        }
        return sp;
    }

    // ═══════════════ 遭遇区块（D05 §4.5，Creature 特有）═══════════════

    private Control? BuildEncounterBody(Creature c)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        // 正向：出场事件链（vEncounterIDs → Encounter；不再标成 OnEnterConditions）
        var chain = BuildEncounterChain(c);
        if (chain is not null) { body.Children.Add(chain); hasAny = true; }

        // 反向：会出现在哪些剧情（Encounter.CreatureId → 本生物）
        var appears = BuildAppearsInPanel(c);
        if (appears is not null) { body.Children.Add(appears); hasAny = true; }

        // 反向：刷新点（CreatureSource.CreatureId → 本生物）
        var spawns = BuildSpawnPointsPanel(c);
        if (spawns is not null) { body.Children.Add(spawns); hasAny = true; }

        return hasAny ? body : null;
    }

    /// <summary>出场事件链：Encounter 徽章行（事件名 + 类型标签 剧情/搜刮/战斗/破解 + 跳转 + hover 预览）。</summary>
    private Control? BuildEncounterChain(Creature c)
    {
        var raw = c.EncounterIds.ToRawString(",");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var wp = new WrapPanel();
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var enc = _vis.Resolver.LookupRef<Encounter>(c, nameof(Creature.EncounterIds), seg);
            if (enc is not null)
            {
                wp.Children.Add(_refNode.BadgeForEntity(c, enc,
                    $"{enc.Subject ?? enc.Name} · {EncTypeLabel(enc)}", "#E8EAF6", "#283593"));
            }
            else
            {
                wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999")); // 未解析灰色兜底
            }
        }
        return LabeledSection(_vis.Loc("Vis.EncounterChain"), wp);
    }

    /// <summary>遭遇类型标签：0=剧情 / 1=搜刮 / 2=战斗（仅 id236）/ 3=破解（field_descriptions.json 实测）。</summary>
    private string EncTypeLabel(Encounter e) => e.Type switch
    {
        EncounterType.Normal => _vis.Loc("Vis.EncTypeStory"),
        EncounterType.Scavenge => _vis.Loc("Vis.EncTypeScavenge"),
        (EncounterType)2 => _vis.Loc("Vis.EncTypeCombat"),
        (EncounterType)3 => _vis.Loc("Vis.EncTypeHack"),
        _ => $"Type {e.Type}",
    };

    /// <summary>会出现在哪些剧情：反查 Encounter.CreatureId 指向本生物的 Encounter 徽章 + creatureHex。</summary>
    private Control? BuildAppearsInPanel(Creature c)
    {
        var encounters = ReverseEncounters(c);
        if (encounters.Count == 0) return null;
        var wp = new WrapPanel();
        foreach (var enc in encounters)
        {
            var label = enc.Subject ?? enc.Name ?? $"Encounter#{enc.Id}";
            // creatureHex：`半径,方向`（如 `40,0`=半径 40 任意方向；`0,0`=本格 → 去噪不显示）
            if (!string.IsNullOrWhiteSpace(enc.CreatureHex) && enc.CreatureHex != "0,0")
                label += $" · {enc.CreatureHex}";
            wp.Children.Add(_refNode.BadgeForEntity(c, enc, label, "#E8EAF6", "#283593"));
        }
        return LabeledSection(_vis.Loc("Vis.AppearsIn"), wp);
    }

    /// <summary>刷新点：反查 CreatureSource.CreatureId 指向本生物的源；
    /// 每行 `点名 (x,y) · 2–4 只 · 权重 0.5（占同点 45%）`，权重按同坐标 Σ 归一，可跳转 CreatureSource。</summary>
    private Control? BuildSpawnPointsPanel(Creature c)
    {
        var allSources = new List<CreatureSource>();
        if (_dataTable.ReferenceLookups.TryGetValue(typeof(CreatureSource), out var list) && list is not null)
            allSources = list.OfType<CreatureSource>().ToList();
        var sources = allSources.Where(s => ReferencesCreature(s.CreatureId, c.Id)).ToList();
        if (sources.Count == 0) return null;

        // 同点权重归一（CreatureSource 视觉器 GetWeightInfo 同源逻辑：同 (x,y) 全部源的权重合计）
        var weightsAt = allSources
            .GroupBy(s => (s.X, s.Y))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Weight));

        var rows = new StackPanel { Spacing = 3 };
        foreach (var cs in sources)
        {
            var total = weightsAt.TryGetValue((cs.X, cs.Y), out var w) ? w : 0.0;
            var proportion = total > 0 ? cs.Weight / total : 1.0;
            var row = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#0D000000"),
                Padding = new Thickness(8, 3),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = cs.Subject ?? cs.Name ?? $"Source#{cs.Id}",
                            FontSize = 11, FontWeight = FontWeight.Medium,
                            Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"({cs.X}, {cs.Y})", FontSize = 10,
                            Foreground = Brush.Parse("#777"), VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"{cs.Min}–{cs.Max} 只", FontSize = 10,
                            Foreground = Brush.Parse("#777"), VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"权重 {cs.Weight:F2}（占同点 {proportion.ToString("0%", CultureInfo.InvariantCulture)}）",
                            FontSize = 10, Foreground = Brush.Parse("#E65100"),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            var csRef = cs;
            _refNode.WireNavigation(row, typeof(CreatureSource), csRef.EntityId, csRef);
            rows.Children.Add(row);
        }
        return LabeledSection(_vis.Loc("Vis.SpawnPoints"), rows);
    }

    /// <summary>反查 Encounter.CreatureId 指向本生物的遭遇（ReferenceLookups 与 Faction 成员面板同源）。</summary>
    private List<Encounter> ReverseEncounters(Creature c)
    {
        var result = new List<Encounter>();
        if (_dataTable.ReferenceLookups.TryGetValue(typeof(Encounter), out var list) && list is not null)
        {
            foreach (var e in list.OfType<Encounter>())
                if (ReferencesCreature(e.CreatureId, c.Id))
                    result.Add(e);
        }
        return result;
    }

    /// <summary>引用列（CreatureId）是否指向给定生物编号；空列表守卫（Count&gt;0），支持 "NSE:17" 前缀。</summary>
    private static bool ReferencesCreature(ReferenceList<IReferenceEntry> list, int creatureId)
    {
        if (list is null || list.Count == 0) return false;
        var raw = list.ToRawString(null);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var colonIdx = raw.IndexOf(':');
        var idPart = colonIdx >= 0 ? raw[(colonIdx + 1)..].Trim() : raw.Trim();
        return int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && id == creatureId;
    }

    // ═══════════════ 条件语义色（R36，D04 同款）═══════════════

    /// <summary>Doc 21 §4-C 颜色语义：Fatal 红 / Instant 橙 / Stackable 绿 / 时长蓝。</summary>
    private static string ConditionBg(Condition c)
        => c.Fatal ? "#FFEBEE" : c.Permanent ? "#FFF3E0" : c.Stackable ? "#E8F5E9" : "#E3F2FD";

    private static string ConditionFg(Condition c)
        => c.Fatal ? "#C62828" : c.Permanent ? "#E65100" : c.Stackable ? "#2E7D32" : "#1565C0";

    /// <summary>"Bleeding · FATAL" / "WellFed · 12h" — 不开条件详情即可判断严重性。</summary>
    private static string ConditionLabel(Condition c, string? extra)
    {
        var suffix = c.Fatal ? "FATAL"
            : c.Permanent ? "Instant"
            : c.Stackable ? "Stackable"
            : $"{c.Duration:F0}h";
        var label = $"{c.Subject} · {suffix}";
        return string.IsNullOrEmpty(extra) ? label : $"{label} ({extra})";
    }

    private static Control LabeledSection(string label, Control content)
    {
        return new StackPanel
        {
            Spacing = 2, Children =
            {
                new TextBlock { Text = label, FontSize = 10, Foreground = Brush.Parse("#999") },
                content
            }
        };
    }

    // ═══════════════ 被引用面板（反向关联）═══════════════

    private Control BuildReverseRefsPanel(Creature c)
        => _vis.BuildReverseRefsPanel(c.EntityId);
}
