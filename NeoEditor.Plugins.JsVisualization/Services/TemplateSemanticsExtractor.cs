using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// D10 §四 P4: 24 类型全覆盖的"剩余 17 类型"语义提取器（B 级 7 个专项 + D 级 10 个
/// 通用模板）——全部输出 <see cref="TemplateSemantics"/>（Hero + Blocks），JS 侧由
/// 薄模板渲染器组合（零 per-type 渲染器，D10 §3.8）。
///  - B 级（语义原样迁移，区块 Section 化）：AttackMode / Condition / TreasureTable /
///    HexType / Faction / BattleMove / CampType；
///  - D 级（模板组合，保持薄）：GameVar / ItemProp / Headline / ForbiddenHex /
///    ChargeProfile / Ingredient / DmcPlace / CreatureSource / EncounterTrigger / DataFile。
/// 纯数据移植自对应 Avalonia visualizer；显示串预本地化。
/// </summary>
public sealed class TemplateSemanticsExtractor
{
    private readonly SemanticsShared _shared;
    private readonly LootTreeBuilder _lootTrees;

    public TemplateSemanticsExtractor(SemanticsShared shared, LootTreeBuilder lootTrees)
    {
        _shared = shared;
        _lootTrees = lootTrees;
    }

    public string Loc(string key) => _shared.Loc(key);
    public string Loc(string key, params object[] args) => _shared.Loc(key, args);

    private RefSummaryDto? Refs(IEntity e) => SemanticsShared.BuildRefSummary(_shared.DataTable, e.EntityId);

    /// <summary>D 级入口：通用字段表 + 类型特化（Hero 徽章/副文本/专有区块）。</summary>
    public TemplateSemantics ExtractThin(IEntity entity) => entity switch
    {
        GameVar g => ExtractGameVar(g),
        Headline h => ExtractHeadline(h),
        ForbiddenHex f => ExtractForbiddenHex(f),
        ChargeProfile c => ExtractChargeProfile(c),
        Ingredient i => ExtractIngredient(i),
        DmcPlace d => ExtractDmcPlace(d),
        CreatureSource cs => ExtractCreatureSource(cs),
        EncounterTrigger t => ExtractEncounterTrigger(t),
        DataFile df => ExtractDataFile(df),
        _ => BuildThin(entity, null, null, null),   // ItemProp 等：纯通用
    };

    // ═══════════════ D 级：通用模板（反射字段表 + 可选特化叠加）═══════════════

    /// <summary>通用模板：Hero（ID/类型 chip）+ 字段表（[Display] 短字段名）+ refs。
    /// heroBadges/heroStats/subtitle/blocks 为特化叠加（可 null）。</summary>
    private TemplateSemantics BuildThin(IEntity entity,
        List<BadgeDto>? heroBadges, List<FieldRowDto>? heroStats, string? subtitle,
        List<TemplateBlockDto>? extraBlocks = null)
    {
        var blocks = new List<TemplateBlockDto>();
        var table = BuildFieldTable(entity);
        if (table is not null) blocks.Add(table);
        if (extraBlocks is not null) blocks.AddRange(extraBlocks);
        return new TemplateSemantics
        {
            HeroBadges = heroBadges ?? [],
            HeroStats = heroStats ?? [],
            Subtitle = subtitle,
            Blocks = blocks,
            Refs = Refs(entity),
        };
    }

    /// <summary>反射实体全部列 → 短字段名（[Display] 名，与合并视图列名一致）+ 原始值
    /// （D 级"简短而完整"，空值不渲染；不用 FieldDescriptions——描述/实测值域太长且随数据漂移）。</summary>
    private TemplateBlockDto? BuildFieldTable(IEntity entity)
    {
        var type = entity.GetType();
        var rows = new List<FieldRowDto>();
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null
                                 && p.DeclaringType != typeof(IEntity))
                     .OrderBy(p => p.MetadataToken))
        {
            var val = p.GetValue(entity);
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            var strVal = val is bool b ? (b ? "1" : "0")
                : val is ReferenceList<IReferenceEntry> rl ? SemanticsShared.Raw(rl, refAttr?.Separator)
                : val?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(strVal)) continue;
            var label = p.GetCustomAttribute<DisplayAttribute>()?.Name ?? p.Name;
            rows.Add(new FieldRowDto
            {
                Label = label,
                Value = strVal.Length > 120 ? strVal[..120] + "…" : strVal,
            });
        }
        return rows.Count > 0 ? new TemplateBlockDto { Title = Loc("Vis.RawFields", rows.Count, rows.Count), Accent = "#546E7A", Rows = rows } : null;
    }

    // ═══════════════ D 级特化（GameVar / Headline / ForbiddenHex / ChargeProfile / Ingredient / DmcPlace / CreatureSource / EncounterTrigger / DataFile）═══════════════

    private TemplateSemantics ExtractGameVar(GameVar g)
    {
        var table = BuildFieldTable(g);
        var blocks = new List<TemplateBlockDto>();
        if (table is not null) blocks.Add(table);
        return new TemplateSemantics
        {
            HeroBadges = string.IsNullOrWhiteSpace(g.Type) ? [] : [new BadgeDto { Text = g.Type, Bg = "#E3F2FD", Fg = "#1565C0" }],
            HeroStats = string.IsNullOrWhiteSpace(g.Value) ? [] : [new FieldRowDto { Value = g.Value, Color = "#2E7D32" }],
            Blocks = blocks,
            Refs = Refs(g),
        };
    }

    private TemplateSemantics ExtractHeadline(Headline h)
    {
        var blocks = new List<TemplateBlockDto>();
        if (!string.IsNullOrWhiteSpace(h.HeadlineText))
            blocks.Add(new TemplateBlockDto
            {
                Title = Loc("Vis.HeadlineText"),
                Accent = "#E65100",
                Text = h.HeadlineText.Length > 2000 ? h.HeadlineText[..2000] + "…" : h.HeadlineText,
            });
        return new TemplateSemantics
        {
            HeroBadges = [new BadgeDto
            {
                Text = $"{h.HeadlineText.Length} chars", Bg = "#FFF3E0", Fg = "#E65100",
            }],
            Blocks = blocks,
            Refs = Refs(h),
        };
    }

    private TemplateSemantics ExtractForbiddenHex(ForbiddenHex f)
    {
        return BuildThin(f,
            [new BadgeDto { Text = "Forbidden", Bg = "#FFEBEE", Fg = "#C62828" },
             new BadgeDto { Text = $"({f.X}, {f.Y})", Bg = "#FFF3E0", Fg = "#E65100" }],
            null, null);
    }

    private TemplateSemantics ExtractChargeProfile(ChargeProfile c)
    {
        var blocks = new List<TemplateBlockDto>();
        var rates = new List<FieldRowDto>();
        if (c.PerUse > 0) rates.Add(new FieldRowDto { Label = Loc("Vis.PerUse"), Value = $"{c.PerUse:F2}", Color = "#C62828" });
        if (c.PerHour > 0) rates.Add(new FieldRowDto { Label = Loc("Vis.PerHour"), Value = $"{c.PerHour:F2}", Color = "#E65100" });
        if (c.PerHourEquipped > 0) rates.Add(new FieldRowDto { Label = Loc("Vis.PerHourEquipped"), Value = $"{c.PerHourEquipped:F2}", Color = "#F57F17" });
        if (c.PerHex > 0) rates.Add(new FieldRowDto { Label = Loc("Vis.PerHex"), Value = $"{c.PerHex:F2}", Color = "#6A1B9A" });
        if (rates.Count > 0) blocks.Add(new TemplateBlockDto { Title = "消耗率", Accent = "#6A1B9A", Rows = rates });

        var badges = new List<BadgeDto>();
        if (!string.IsNullOrWhiteSpace(c.ItemId))
        {
            var item = _shared.Resolver.LookupRef<ItemType>(c, nameof(ChargeProfile.ItemId), c.ItemId);
            badges.Add(item is not null
                ? new BadgeDto { Text = item.Subject ?? c.ItemId, Bg = "#E3F2FD", Fg = "#1565C0", TargetType = "ItemType", TargetId = item.EntityId }
                : new BadgeDto { Text = c.ItemId, Bg = "#F5F5F5", Fg = "#999" });
        }
        if (badges.Count > 0) blocks.Add(new TemplateBlockDto { Title = "对应物品", Accent = "#1565C0", Badges = badges });

        return new TemplateSemantics
        {
            HeroBadges = c.Degrade ? [new BadgeDto { Text = "Degrade ⚠", Bg = "#FFF3E0", Fg = "#E65100" }] : [],
            Blocks = blocks,
            Refs = Refs(c),
        };
    }

    private TemplateSemantics ExtractIngredient(Ingredient ing)
    {
        var blocks = new List<TemplateBlockDto>();
        var req = BuildPropBadges(ing, ing.RequiredProps, nameof(Ingredient.RequiredProps), "#E8F5E9", "#2E7D32");
        var forbid = BuildPropBadges(ing, ing.ForbidProps, nameof(Ingredient.ForbidProps), "#FFEBEE", "#C62828");
        var groups = new List<TemplateBadgeGroupDto>();
        if (req.Count > 0) groups.Add(new TemplateBadgeGroupDto { Label = Loc("Vis.Required"), Badges = req });
        if (forbid.Count > 0) groups.Add(new TemplateBadgeGroupDto { Label = Loc("Vis.Forbidden"), Badges = forbid });
        if (groups.Count > 0)
            blocks.Add(new TemplateBlockDto { Title = "属性要求", Accent = "#2E7D32", BadgeGroups = groups });
        return BuildThin(ing, null, null, null, blocks);
    }

    private List<BadgeDto> BuildPropBadges(Ingredient ing, ReferenceList<IReferenceEntry> raw, string propName,
        string bg, string fg)
    {
        var result = new List<BadgeDto>();
        var rawText = SemanticsShared.Raw(raw, "&");
        if (string.IsNullOrWhiteSpace(rawText)) return result;
        foreach (var pid in rawText.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var prop = _shared.Resolver.LookupRef<ItemProp>(ing, propName, pid);
            result.Add(prop is not null
                ? new BadgeDto { Text = prop.PropertyName ?? pid, Bg = bg, Fg = fg, TargetType = "ItemProp", TargetId = prop.EntityId }
                : new BadgeDto { Text = pid, Bg = "#F5F5F5", Fg = "#999" });
        }
        return result;
    }

    private TemplateSemantics ExtractDmcPlace(DmcPlace d)
    {
        var blocks = new List<TemplateBlockDto>();
        var rawEnc = SemanticsShared.Raw(d.EncounterId, null);
        if (!string.IsNullOrWhiteSpace(rawEnc) && rawEnc != "0")
        {
            var enc = _shared.Resolver.LookupRef<Encounter>(d, nameof(DmcPlace.EncounterId), rawEnc);
            blocks.Add(new TemplateBlockDto
            {
                Title = "剧情",
                Accent = "#2E7D32",
                Badges = [enc is not null
                    ? new BadgeDto { Text = enc.Subject ?? rawEnc, Bg = "#E8F5E9", Fg = "#2E7D32", TargetType = "Encounter", TargetId = enc.EntityId }
                    : new BadgeDto { Text = rawEnc, Bg = "#F5F5F5", Fg = "#999" }],
            });
        }
        return BuildThin(d,
            [new BadgeDto { Text = $"({d.X}, {d.Y})", Bg = "#FFF3E0", Fg = "#E65100" }],
            null, null, blocks);
    }

    private TemplateSemantics ExtractCreatureSource(CreatureSource cs)
    {
        // 同坐标权重归一（与 Avalonia GetWeightInfo 同源）
        var allSources = _shared.DataTable.ReferenceLookups.TryGetValue(typeof(CreatureSource), out var list)
            ? list.OfType<CreatureSource>().ToList() : [];
        var total = allSources.Where(s => s.X == cs.X && s.Y == cs.Y).Sum(s => s.Weight);
        var proportion = total > 0 ? cs.Weight / total : 1.0;

        var blocks = new List<TemplateBlockDto>();
        var rawCreature = SemanticsShared.Raw(cs.CreatureId, null);
        if (!string.IsNullOrWhiteSpace(rawCreature) && rawCreature != "0")
        {
            var creature = _shared.Resolver.LookupRef<Creature>(cs, nameof(CreatureSource.CreatureId), rawCreature);
            blocks.Add(new TemplateBlockDto
            {
                Title = "生物",
                Accent = "#283593",
                Badges = [creature is not null
                    ? new BadgeDto { Text = creature.Subject ?? rawCreature, Bg = "#E8EAF6", Fg = "#283593", TargetType = "Creature", TargetId = creature.EntityId }
                    : new BadgeDto { Text = rawCreature, Bg = "#F5F5F5", Fg = "#999" }],
            });
        }
        return BuildThin(cs,
            [new BadgeDto
            {
                Text = $"({cs.X}, {cs.Y}) · {cs.Min}–{cs.Max}",
                Bg = "#FFF3E0", Fg = "#E65100",
            }],
            [new FieldRowDto { Label = "权重（占同点）", Value = $"{cs.Weight:F2}（{proportion.ToString("0%", CultureInfo.InvariantCulture)}）", Color = "#E65100" }],
            null, blocks);
    }

    private TemplateSemantics ExtractEncounterTrigger(EncounterTrigger t)
    {
        // 类型徽章（LocBased/DateBased/HexBased/Unique/AIPassable）
        var typeParts = new List<string>();
        if (t.LocBased) typeParts.Add("📍 LocBased");
        if (t.DateBased) typeParts.Add("📅 DateBased");
        if (t.HexBased) typeParts.Add("🧱 HexBased");
        if (t.Unique) typeParts.Add("♻ Unique");
        if (!t.AIPassable) typeParts.Add("AI Passable");
        var heroBadges = new List<BadgeDto>();
        if (typeParts.Count > 0)
            heroBadges.Add(new BadgeDto { Text = string.Join(" · ", typeParts), Bg = "#E8EAF6", Fg = "#283593" });
        heroBadges.Add(new BadgeDto { Text = $"Chance: {t.Chance.ToString("0%", CultureInfo.InvariantCulture)}", Bg = "#FFF3E0", Fg = "#E65100" });

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(t.Area)) subtitleParts.Add($"📍 {t.Area}");
        if (!string.IsNullOrWhiteSpace(t.DateMin) || !string.IsNullOrWhiteSpace(t.DateMax))
            subtitleParts.Add($"📅 {t.DateMin}~{t.DateMax}");

        var blocks = new List<TemplateBlockDto>();
        var rawEnc = SemanticsShared.Raw(t.EncounterId, null);
        if (!string.IsNullOrWhiteSpace(rawEnc))
        {
            var enc = _shared.Resolver.LookupRef<Encounter>(t, nameof(EncounterTrigger.EncounterId), rawEnc);
            blocks.Add(new TemplateBlockDto
            {
                Title = "触发剧情",
                Accent = "#2E7D32",
                Badges = [enc is not null
                    ? new BadgeDto { Text = enc.Subject ?? rawEnc, Bg = "#E8F5E9", Fg = "#2E7D32", TargetType = "Encounter", TargetId = enc.EntityId }
                    : new BadgeDto { Text = rawEnc, Bg = "#F5F5F5", Fg = "#999" }],
            });
        }
        var hexRaw = SemanticsShared.Raw(t.HexTypes, ",");
        if (!string.IsNullOrWhiteSpace(hexRaw))
        {
            var hexBadges = hexRaw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)
                .Select(s => new BadgeDto { Text = s, Bg = "#ECEFF1", Fg = "#546E7A" }).ToList();
            blocks.Add(new TemplateBlockDto { Title = "格类型", Accent = "#546E7A", Badges = hexBadges });
        }
        return BuildThin(t, heroBadges, null, subtitleParts.Count > 0 ? string.Join("  ", subtitleParts) : null, blocks);
    }

    private TemplateSemantics ExtractDataFile(DataFile df)
    {
        var blocks = new List<TemplateBlockDto>();
        if (!string.IsNullOrWhiteSpace(df.Description))
            blocks.Add(new TemplateBlockDto
            {
                Title = "内容",
                Accent = "#546E7A",
                Text = df.Description.Length > 2000 ? df.Description[..2000] + "…" : df.Description,
            });
        return new TemplateSemantics
        {
            HeroBadges = df.Value > 0 ? [new BadgeDto { Text = $"${df.Value:F2}", Bg = "#E8F5E9", Fg = "#2E7D32" }] : [],
            Blocks = blocks,
            Refs = Refs(df),
        };
    }

    // ═══════════════ B 级：AttackMode（单攻击模式详情）═══════════════

    public TemplateSemantics ExtractAttackMode(AttackMode am)
    {
        var dto = _shared.BuildAttackMode(am);
        var isRanged = am.Type == AttackType.Ranged;
        var blocks = new List<TemplateBlockDto>
        {
            new() { Title = Loc("Vis.Combat"), Accent = "#C62828", Mode = dto },
        };
        if (dto.ChargeBadges.Count > 0)
            blocks.Add(new TemplateBlockDto { Title = Loc("Vis.ChargeAmmo"), Accent = "#006064", Badges = dto.ChargeBadges });
        if (dto.AttackerConditions.Count > 0)
            blocks.Add(new TemplateBlockDto
            {
                Title = Loc("Vis.AttackerConditions"), Accent = "#6A1B9A",
                Badges = dto.AttackerConditions.Select(c => new BadgeDto { Text = c.Label, Bg = c.Bg, Fg = c.Fg, TargetType = c.TargetType, TargetId = c.TargetId }).ToList(),
            });
        if (dto.AttackPhrases.Count > 0)
            blocks.Add(new TemplateBlockDto
            {
                Title = Loc("Vis.AttackPhrases"), Accent = "#1565C0",
                Badges = dto.AttackPhrases.Select(p => new BadgeDto { Text = p, Bg = "#E3F2FD", Fg = "#1565C0" }).ToList(),
            });

        return new TemplateSemantics
        {
            HeroBadges = [new BadgeDto
            {
                Text = isRanged ? $"{Loc("Vis.CombatRanged")} ({am.Range} tile)" : $"{Loc("Vis.CombatMelee")} ({am.Range} tiles)",
                Bg = isRanged ? "#FFEBEE" : "#E8F5E9",
                Fg = isRanged ? "#C62828" : "#2E7D32",
            }],
            Subtitle = string.IsNullOrWhiteSpace(am.WieldPhrase) ? null : $"“{am.WieldPhrase}”",
            Blocks = blocks,
            Refs = Refs(am),
        };
    }

    // ═══════════════ B 级：Condition（严重度/属性/修饰值双向条/效果/链条）═══════════════

    /// <summary>条件字段名中文翻译（Avalonia ConditionEntityVisualizer 同款硬编码字典）。</summary>
    private static readonly Dictionary<string, string> ConditionFieldTranslations = new()
    {
        ["m_fHealPerHourMod"] = "每小时恢复能力", ["m_fImmuneRestoreRate"] = "免疫恢复能力",
        ["m_fBloodRestoreRate"] = "血液恢复能力", ["fMovesPerTurnModifier"] = "每回合移动点数",
        ["m_fEncumberanceLimit"] = "负重值", ["m_fMoraleHidden"] = "隐藏的士气值",
        ["m_fDefense"] = "闪避几率", ["Asleep"] = "陷入沉睡", ["fSleepQuality"] = "睡眠质量",
        ["m_fSleepAwareness"] = "睡眠意识", ["fFoodConsumptionRate"] = "食物消耗速率",
        ["fWaterConsumptionRate"] = "水消耗速率", ["BaseDetectionLevel"] = "基准警觉值",
        ["MinSafeTemp"] = "最低安全温度", ["MaxSafeTemp"] = "最高安全温度",
        ["m_fFatigueModifier"] = "疲劳修饰值", ["fPassiveRewarmPerHour"] = "每小时被动升温",
        ["m_fMorale"] = "士气", ["m_fBloodLeft"] = "血液总量", ["BodyInsulation"] = "身体热量散发值",
        ["m_fVisibility"] = "自身可见度", ["m_fMoveReserve"] = "行动点数",
        ["m_fMoveReserveRemaining"] = "行动点总量", ["MinLightLevel"] = "最小适应亮度",
        ["AttDmgMult"] = "攻击伤害", ["VisionRange"] = "视觉范围", ["m_fScent"] = "气味",
        ["LightLevel"] = "光照等级", ["fFoodDebt"] = "饥饿值", ["fWaterDebt"] = "饥渴值",
        ["m_fImmuneLeft"] = "免疫总量", ["m_fTrackingThreshold"] = "追踪阈值",
        ["m_fPainLeftBase"] = "疼痛总量阈值", ["m_fImmuneLeftBase"] = "免疫总量阈值",
        ["m_fPainLeft"] = "疼痛恢复", ["DefDmgMult"] = "防御伤害数值", ["fSleepDebt"] = "睡眠不足",
        ["fCoreTemp"] = "核心体温", ["m_fMovesLeft"] = "剩余行动点", ["WetTempAdjustMod"] = "潮湿调节数值",
        ["MoveCost"] = "额外行动点消耗", ["ChangeRange"] = "改变距离", ["Attack"] = "攻击动作",
        ["ExitBattle"] = "退出战斗", ["KnockDown"] = "击倒", ["JustMoved"] = "刚刚移动过",
        ["Discharge"] = "解除武装", ["Trip"] = "被绊倒", ["Bandaged"] = "包扎", ["Infected"] = "感染",
        ["Disinfected"] = "消毒", ["m_fPain"] = "疼痛", ["Crippled"] = "残废", ["Splinted"] = "使用夹板",
        ["Threat"] = "威胁", ["ChangeRangeAll"] = "朝所有人退后", ["TriggerEncounter"] = "触发剧情",
        ["m_nMorality"] = "道德值", ["LoseRandomItem"] = "丢失随机物品", ["LoseAllItems"] = "丢失所有物品",
        ["Money"] = "钱", ["GetDiagnostic"] = "获取诊断结果", ["ResetTemp"] = "核心体温调节",
        ["SpawnNewCreature"] = "繁殖新生物", ["DropAllItems"] = "丢下所有物品", ["LootTarget"] = "掠夺目标",
        ["ApplyCutDamage"] = "从伤口上取出异物", ["ScatterMissile"] = "导弹", ["UseGPS"] = "正在使用GPS",
        ["ResetUsSpotted"] = "重置视觉", ["EmptyGroundSlot"] = "空的地面格",
        ["CleanAndDress"] = "清理并包扎伤口", ["BattleRange"] = "攻击距离", ["AddRecipe"] = "得到配方",
    };

    private static string TranslateFieldName(string name)
        => ConditionFieldTranslations.TryGetValue(name, out var zh) ? $"{zh} ({name})" : name;

    public TemplateSemantics ExtractCondition(Condition c)
    {
        // Hero：严重度 + Stackable/Hidden + statRow
        var heroBadges = new List<BadgeDto>();
        var severity = c.Fatal ? ("FATAL", "#FFEBEE", "#C62828")
            : c.Permanent ? ("Instant", "#FFF3E0", "#E65100")
            : c.Stackable ? ("Stackable", "#E8F5E9", "#2E7D32")
            : ($"{c.Duration:F0}h", "#E3F2FD", "#1565C0");
        heroBadges.Add(new BadgeDto { Text = severity.Item1, Bg = severity.Item2, Fg = severity.Item3 });
        if (c.Stackable && !c.Fatal && !c.Permanent)
            heroBadges.Add(new BadgeDto { Text = "Stackable", Bg = "#E8F5E9", Fg = "#2E7D32" });
        if (!c.Display) heroBadges.Add(new BadgeDto { Text = "Hidden", Bg = "#ECEFF1", Fg = "#546E7A" });

        var heroStats = new List<FieldRowDto>();
        heroStats.Add(new FieldRowDto
        {
            Label = "时长",
            Value = c.Permanent ? "Instant" : $"{c.Duration:F0}h",
            Color = c.Permanent ? "#E65100" : "#1565C0",
        });
        heroStats.Add(new FieldRowDto { Label = "Color", Value = c.Color.ToString(), Color = "#546E7A" });
        if (c.TransferRange != -1) heroStats.Add(new FieldRowDto { Label = "Transfer", Value = $"{c.TransferRange}", Color = "#546E7A" });
        if (c.ResetTimer) heroStats.Add(new FieldRowDto { Label = "ResetTimer", Value = "是", Color = "#2E7D32" });
        if (c.DisplayOther) heroStats.Add(new FieldRowDto { Label = "DisplayOther", Value = "是", Color = "#2E7D32" });
        if (c.DisplayGameOver) heroStats.Add(new FieldRowDto { Label = "DisplayGameOver", Value = "是", Color = "#2E7D32" });

        var blocks = new List<TemplateBlockDto>();

        // Properties：键值表（Duration/Color/Transfer + 条件性行）
        var propRows = new List<FieldRowDto>
        {
            new() { Label = "时长", Value = c.Permanent ? "Instant" : $"{c.Duration:F0}h" },
            new() { Label = "Color", Value = c.Color.ToString() },
        };
        if (c.Fatal) propRows.Add(new FieldRowDto { Label = "Fatal", Value = "是", Color = "#C62828" });
        if (c.Permanent) propRows.Add(new FieldRowDto { Label = "Instant", Value = "是", Color = "#E65100" });
        if (c.Stackable) propRows.Add(new FieldRowDto { Label = "Stackable", Value = "是", Color = "#2E7D32" });
        if (!c.Display) propRows.Add(new FieldRowDto { Label = "Hidden", Value = "是", Color = "#999" });
        if (c.DisplayOther) propRows.Add(new FieldRowDto { Label = "DisplayOther", Value = "是" });
        if (c.ResetTimer) propRows.Add(new FieldRowDto { Label = "ResetTimer", Value = "是" });
        if (c.RemoveAll) propRows.Add(new FieldRowDto { Label = "RemoveAll", Value = "是" });
        if (c.RemovePostCombat) propRows.Add(new FieldRowDto { Label = "RemovePostCombat", Value = "是" });
        if (c.TransferRange != -1) propRows.Add(new FieldRowDto { Label = "Transfer", Value = $"{c.TransferRange}" });
        blocks.Add(new TemplateBlockDto { Title = "属性", Accent = "#546E7A", Rows = propRows });

        // Modifiers：字段名 + 带符号值 + 零中心双向条
        var fields = c.FieldNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mods = c.Modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length > 0)
        {
            var modRows = new List<FieldRowDto>();
            var modBars = new List<StatBarDto>();
            var maxAbs = 0.0;
            var parsed = new List<double>();
            for (int i = 0; i < fields.Length; i++)
            {
                var mod = i < mods.Length && double.TryParse(mods[i],
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 0;
                parsed.Add(mod);
                maxAbs = Math.Max(maxAbs, Math.Abs(mod));
            }
            for (int i = 0; i < fields.Length; i++)
            {
                var mod = parsed[i];
                var color = mod >= 0 ? "#2E7D32" : "#C62828";
                modRows.Add(new FieldRowDto
                {
                    Label = TranslateFieldName(fields[i]),
                    Value = mod.ToString("+#0.###;-#0.###;0", CultureInfo.InvariantCulture),
                    Color = color,
                });
                modBars.Add(new StatBarDto
                {
                    Mode = "bipolar",
                    Segments = { new StatSegmentDto { Value = mod, Color = color } },
                    Max = maxAbs > 0 ? maxAbs : 1.0,
                    NegativeColor = "#C62828",
                });
            }
            blocks.Add(new TemplateBlockDto { Title = "效果", Accent = "#283593", Rows = modRows, Bars = modBars });
        }

        // Effects：原文截断
        if (!string.IsNullOrWhiteSpace(c.Effects))
            blocks.Add(new TemplateBlockDto
            {
                Title = "Effects 原文",
                Accent = "#00838F",
                Text = c.Effects.Length > 800 ? c.Effects[..800] + "…" : c.Effects,
            });

        // ConditionChain：IdNext 徽章链 + ChanceNext
        var chainBadges = new List<BadgeDto>();
        var rawNext = SemanticsShared.Raw(c.IdNext, ",");
        if (!string.IsNullOrWhiteSpace(rawNext))
        {
            foreach (var seg in rawNext.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var next = _shared.Resolver.LookupRef<Condition>(c, nameof(Condition.IdNext), seg);
                chainBadges.Add(next is not null
                    ? new BadgeDto { Text = next.Subject ?? seg, Bg = "#F3E5F5", Fg = "#6A1B9A", TargetType = "Condition", TargetId = next.EntityId }
                    : new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
            }
        }
        var chanceNext = c.ChanceNext;
        if (!string.IsNullOrWhiteSpace(chanceNext) && chanceNext != "0")
        {
            chainBadges.Add(new BadgeDto { Text = $"ChanceNext: {chanceNext}", Bg = "#FFF3E0", Fg = "#E65100" });
        }
        if (chainBadges.Count > 0)
            blocks.Add(new TemplateBlockDto { Title = "状态链", Accent = "#6A1B9A", Badges = chainBadges });

        return new TemplateSemantics
        {
            HeroBadges = heroBadges,
            HeroStats = heroStats,
            Blocks = blocks,
            Refs = Refs(c),
        };
    }

    // ═══════════════ B 级：TreasureTable（战利品表）═══════════════

    public TemplateSemantics ExtractTreasureTable(TreasureTable tt)
    {
        var blocks = new List<TemplateBlockDto>();
        var tree = _lootTrees.Build(tt);
        if (tree is not null)
            blocks.Add(new TemplateBlockDto { Title = Loc("Vis.Loot"), Accent = "#2E7D32", Trees = [tree] });

        var flags = new List<BadgeDto>();
        if (tt.Nested) flags.Add(new BadgeDto { Text = "Nested", Bg = "#FFF3E0", Fg = "#E65100" });
        if (tt.Suppress) flags.Add(new BadgeDto { Text = "Suppress", Bg = "#FFF3E0", Fg = "#E65100" });
        if (tt.Identify) flags.Add(new BadgeDto { Text = "Identify", Bg = "#FFF3E0", Fg = "#E65100" });

        return new TemplateSemantics
        {
            HeroBadges = flags,
            Blocks = blocks,
            Refs = Refs(tt),
        };
    }

    // ═══════════════ B 级：HexType（地形）═══════════════

    public TemplateSemantics ExtractHexType(HexType ht)
    {
        var heroBadges = new List<BadgeDto>
        {
            ht.Passable == PassableType.Passable
                ? new BadgeDto { Text = "Passable", Bg = "#E8F5E9", Fg = "#2E7D32" }
                : new BadgeDto { Text = "Blocked", Bg = "#FFEBEE", Fg = "#C62828" },
        };
        var heroStats = new List<FieldRowDto>();
        if (ht.TerrainCost > 0)
            heroStats.Add(new FieldRowDto { Value = $"{ht.TerrainCost} AP", Color = "#E65100" });
        var netViz = ht.VizIncrease - ht.VizLimiter;
        if (netViz != 0)
            heroStats.Add(new FieldRowDto { Value = $"能见度 {netViz:+0;-0}", Color = netViz > 0 ? "#2E7D32" : "#C62828" });
        if (ht.MinRange > 0 || ht.MaxRange > 0)
            heroStats.Add(new FieldRowDto { Value = $"遭遇范围 {ht.MinRange}–{ht.MaxRange}", Color = "#283593" });

        var blocks = new List<TemplateBlockDto>();
        var terrainRows = new List<FieldRowDto>();
        if (ht.TerrainCost > 0)
            terrainRows.Add(new FieldRowDto
            {
                Label = "移动消耗",
                Value = $"{ht.TerrainCost} AP",
                Color = ht.TerrainCost <= 1 ? "#2E7D32" : ht.TerrainCost <= 3 ? "#E65100" : "#C62828",
            });
        if (netViz != 0)
            terrainRows.Add(new FieldRowDto { Label = "净能见度", Value = $"{netViz:+0;-0}", Color = netViz > 0 ? "#2E7D32" : "#C62828" });
        if (ht.MinRange > 0 || ht.MaxRange > 0)
            terrainRows.Add(new FieldRowDto { Label = "遭遇范围", Value = $"{ht.MinRange}–{ht.MaxRange}" });
        if (ht.CampItems != 5)
        {
            var campLabel = ht.CampItems switch
            {
                0 => "无", 1 => "稀疏", 2 => "中等", 3 => "丰富", 4 => "富饶", 5 => "默认",
                _ => $"Lv.{ht.CampItems}",
            };
            terrainRows.Add(new FieldRowDto { Label = "营地物资", Value = campLabel, Color = "#E65100" });
        }
        if (terrainRows.Count > 0)
            blocks.Add(new TemplateBlockDto { Title = "地形移动", Accent = "#546E7A", Rows = terrainRows });

        // LightLevels：6 时段热力格（与 Avalonia BuildLightPanel 同款——从早到晚横排，
        // 时段名在上、热力色块内数值；红(0)→黄(0.5)→绿(1.0+) 同公式插值）
        if (!string.IsNullOrWhiteSpace(ht.LightLevels))
        {
            var lightNames = new[] { "Dawn", "Morning", "Noon", "Afternoon", "Dusk", "Midnight" };
            var levels = ht.LightLevels.Split(',').Select(s => s.Trim()).ToList();
            var parsed = levels.Select(s => double.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? (double?)v : null).ToList();
            var maxLight = parsed.Where(x => x.HasValue).DefaultIfEmpty(1.0).Max() ?? 1.0;
            var lightCells = new List<LightCellDto>();
            for (int i = 0; i < lightNames.Length; i++)
            {
                var valStr = i < levels.Count ? levels[i] : "?";
                var val = i < parsed.Count ? parsed[i] : null;
                // Heatmap: red (0) → yellow (0.5) → green (1.0+)，Avalonia 同公式
                var ratio = val.HasValue && maxLight > 0 ? Math.Clamp(val.Value / maxLight, 0.0, 1.0) : 0.0;
                int r = (int)((1 - ratio) * 198 + ratio * 46); // 198→46
                int g = (int)(ratio < 0.5 ? ratio * 2 * 125 : (1 - ratio) * 2 * 125 + 125); // 0→125→0
                int bv = (int)(ratio < 0.5 ? (1 - ratio * 2) * 40 : 0); // 40→0
                lightCells.Add(new LightCellDto
                {
                    Label = lightNames[i],
                    Value = valStr,
                    Bg = val.HasValue ? $"#{r:X2}{g:X2}{bv:X2}" : "#F5F5F5",
                    Fg = ratio > 0.5 ? "#FFFFFF" : "#333333",
                });
            }
            blocks.Add(new TemplateBlockDto { Title = "光照等级", Accent = "#F57F17", LightCells = lightCells });
        }

        // Refs：TreasureId 绿 / ScavengeInitialId / ScavengeItemsIdPerHour / ConditionIds 粉 / DefaultCampId 橙
        var refGroups = new List<TemplateBadgeGroupDto>();
        AddRefGroup(refGroups, "搜刮战利品", ht.TreasureId, ht, nameof(HexType.TreasureId), "#E8F5E9", "#2E7D32", "TreasureTable");
        AddRefGroup(refGroups, "初始搜刮", ht.ScavengeInitialId, ht, nameof(HexType.ScavengeInitialId), "#E8F5E9", "#2E7D32", "TreasureTable");
        AddRefGroup(refGroups, "每小时搜刮", ht.ScavengeItemsIdPerHour, ht, nameof(HexType.ScavengeItemsIdPerHour), "#E8F5E9", "#2E7D32", "TreasureTable");
        AddRefGroup(refGroups, "条件", ht.ConditionIds, ht, nameof(HexType.ConditionIds), "#F3E5F5", "#6A1B9A", "Condition");
        AddRefGroup(refGroups, "默认营地", ht.DefaultCampId, ht, nameof(HexType.DefaultCampId), "#FFF3E0", "#E65100", "CampType");
        if (refGroups.Count > 0)
            blocks.Add(new TemplateBlockDto { Title = "引用", Accent = "#283593", BadgeGroups = refGroups });

        return new TemplateSemantics
        {
            HeroBadges = heroBadges,
            HeroStats = heroStats,
            Subtitle = string.IsNullOrWhiteSpace(ht.Description) ? null : ht.Description,
            Blocks = blocks,
            Refs = Refs(ht),
        };
    }

    private void AddRefGroup(List<TemplateBadgeGroupDto> groups, string label, ReferenceList<IReferenceEntry> raw,
        IEntity source, string propName, string bg, string fg, string targetType)
    {
        var rawText = SemanticsShared.Raw(raw, ",");
        if (string.IsNullOrWhiteSpace(rawText) || rawText is "3" or "25") return;   // 哨兵跳过
        var badges = new List<BadgeDto>();
        foreach (var seg in rawText.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var target = _shared.Resolver.LookupRefByRawId(source, seg, ResolveType(targetType));
            badges.Add(target is not null
                ? new BadgeDto { Text = target.Subject ?? seg, Bg = bg, Fg = fg, TargetType = targetType, TargetId = target.EntityId }
                : new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
        }
        if (badges.Count > 0) groups.Add(new TemplateBadgeGroupDto { Label = label, Badges = badges });
    }

    private static Type? ResolveType(string name)
        => typeof(Encounter).Assembly.GetTypes().FirstOrDefault(t => t.Name == name);

    // ═══════════════ B 级：Faction（外交 + 成员）═══════════════

    public TemplateSemantics ExtractFaction(Faction f)
    {
        var blocks = new List<TemplateBlockDto>();

        // Diplomacy：DictFactions → 名称 + 值文本 + 零中心双向条（按值升序）
        var rawDict = SemanticsShared.Raw(f.DictFactions, ",");
        if (!string.IsNullOrWhiteSpace(rawDict))
        {
            var entries = new List<(string Name, double Value)>();
            foreach (var seg in rawDict.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var parts = seg.Split('=');
                if (parts.Length < 2) continue;
                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    continue;
                var name = parts[0].Trim();
                var other = _shared.DataTable.ReferenceLookups.TryGetValue(typeof(Faction), out var list)
                    ? list.OfType<Faction>().FirstOrDefault(x => x.Id.ToString() == name)
                    : null;
                entries.Add((other?.Subject ?? name, v));
            }
            entries.Sort((a, b) => a.Value.CompareTo(b.Value));
            var bars = new List<StatBarDto>();
            double maxAbs = entries.Count > 0 ? Math.Max(1.0, entries.Max(e => Math.Abs(e.Value))) : 1.0;
            foreach (var (name, value) in entries)
            {
                var desc = value >= 100 ? "同盟" : value >= 50 ? "友好" : value >= 0 ? "中立"
                    : value >= -50 ? "敌对" : "仇敌";
                bars.Add(new StatBarDto
                {
                    Mode = "bipolar",
                    Text = $"{name}  {value:+0;-0} ({desc})",
                    Segments = { new StatSegmentDto { Value = value, Color = value >= 0 ? "#2E7D32" : "#C62828" } },
                    Max = maxAbs,
                    NegativeColor = "#C62828",
                });
            }
            if (bars.Count > 0)
                blocks.Add(new TemplateBlockDto { Title = "外交关系", Accent = "#283593", Bars = bars });
        }

        // Members：ReverseLookup Creature.Faction → 本实体
        var members = new List<BadgeDto>();
        var store = _shared.DataTable.BrowserStore ?? _shared.DataTable.ActiveMergeStore;
        if (store is not null)
        {
            var rawRefs = store.IndexService?.ReverseLookup(f.EntityId) ?? [];
            var creatures = store.ReferenceLookups.TryGetValue(typeof(Creature), out var clist)
                ? clist.OfType<Creature>().ToDictionary(c => c.EntityId, c => c) : [];
            foreach (var (srcEid, propName, _) in rawRefs)
            {
                if (propName is not ("nFaction" or "Faction")) continue;
                if (creatures.TryGetValue(srcEid, out var creature))
                    members.Add(new BadgeDto
                    {
                        Text = creature.Subject ?? srcEid, Bg = "#E8EAF6", Fg = "#283593",
                        TargetType = "Creature", TargetId = creature.EntityId,
                    });
            }
        }
        if (members.Count > 0)
            blocks.Add(new TemplateBlockDto { Title = "成员", Accent = "#283593", Badges = members });

        return new TemplateSemantics { Blocks = blocks, Refs = Refs(f) };
    }

    // ═══════════════ B 级：BattleMove（战斗决策）═══════════════

    public TemplateSemantics ExtractBattleMove(BattleMove bm)
    {
        // Hero：类型徽章（NonAttack/Melee/Ranged · 大类）+ StrId 紫 + flags
        var kind = bm.Offense ? "进攻" : bm.Retreat ? "撤退" : bm.Passive ? "被动" : "行动";
        var attackLabel = bm.AttackModeType switch
        {
            BattleMoveType.NonAttack => "非攻击",
            BattleMoveType.Melee => "近战",
            BattleMoveType.Ranged => "远程",
            _ => "?",
        };
        var (bg, fg) = bm.Offense ? ("#FFEBEE", "#C62828")
            : bm.Retreat ? ("#E3F2FD", "#1565C0")
            : bm.Passive ? ("#F5F5F5", "#999")
            : ("#FFF3E0", "#E65100");
        var heroBadges = new List<BadgeDto> { new() { Text = $"{attackLabel} · {kind}", Bg = bg, Fg = fg } };
        if (!string.IsNullOrWhiteSpace(bm.StrId))
            heroBadges.Add(new BadgeDto { Text = bm.StrId, Bg = "#F3E5F5", Fg = "#6A1B9A" });
        var flags = new List<string>();
        if (bm.Offense) flags.Add("Offense");
        if (bm.Approach) flags.Add("Approach");
        if (bm.FallBack) flags.Add("FallBack");
        if (bm.Retreat) flags.Add("Retreat");
        if (bm.Position) flags.Add("Position");
        if (bm.Passive) flags.Add("Passive");
        if (bm.AllOutOfRange) flags.Add("AllOutOfRange");
        if (bm.InAttackRange) flags.Add("InAttackRange");
        if (flags.Count > 0)
            heroBadges.Add(new BadgeDto { Text = string.Join(" · ", flags), Bg = "#ECEFF1", Fg = "#546E7A" });

        var blocks = new List<TemplateBlockDto>();
        // Stats：Bars（Chance/Detect/Priority/Fatigue/Order）+ Rows（Range/Exposure/MinCharges/ChanceType）
        var statBars = new List<StatBarDto>
        {
            new() { Mode = "centered", Text = $"Chance {bm.Chance.ToString("0%", CultureInfo.InvariantCulture)}", Segments = { new StatSegmentDto { Value = Math.Min(bm.Chance, 1.0), Color = bm.Chance >= 1 ? "#2E7D32" : "#E65100" } }, Max = 1.0 },
            new() { Mode = "bipolar", Text = $"Detect {bm.Detect:0.###}", Segments = { new StatSegmentDto { Value = bm.Detect, Color = bm.Detect <= 0 ? "#2E7D32" : "#C62828" } }, Max = 1.0, NegativeColor = "#C62828" },
            new() { Mode = "bipolar", Text = $"Fatigue {bm.Fatigue:+0;-0}", Segments = { new StatSegmentDto { Value = bm.Fatigue, Color = bm.Fatigue >= 0 ? "#2E7D32" : "#C62828" } }, Max = 5.0, NegativeColor = "#C62828" },
            new() { Mode = "bipolar", Text = $"Order {bm.Order:0.##}", Segments = { new StatSegmentDto { Value = bm.Order - 0.5, Color = "#1565C0" } }, Max = 0.5, NegativeColor = "#1565C0" },
        };
        var statRows = new List<FieldRowDto>();
        var rangeText = bm.MinRange == -1 && bm.MaxRange == -1 ? "All"
            : bm.MinRange == 0 ? $"0–{bm.MaxRange}"
            : $"{bm.MinRange}–{bm.MaxRange}";
        statRows.Add(new FieldRowDto { Label = "射程", Value = rangeText });
        statRows.Add(new FieldRowDto { Label = "暴露", Value = $"他们 {FmtExposure(bm.SeeThem)} / 我们 {FmtExposure(bm.SeeUs)}" });
        if (bm.MinCharges > 0) statRows.Add(new FieldRowDto { Label = "MinCharges", Value = $"{bm.MinCharges}" });
        if (bm.ChanceType != "0,0,0") statRows.Add(new FieldRowDto { Label = "ChanceType", Value = bm.ChanceType });
        blocks.Add(new TemplateBlockDto { Title = "决策属性", Accent = "#C62828", Bars = statBars, Rows = statRows });

        // 文本：PopUp/Success/Fail
        if (!string.IsNullOrWhiteSpace(bm.PopUp))
            blocks.Add(new TemplateBlockDto { Title = "描述", Accent = "#546E7A", Text = Truncate(bm.PopUp, 800) });
        if (!string.IsNullOrWhiteSpace(bm.Success))
            blocks.Add(new TemplateBlockDto { Title = "成功", Accent = "#2E7D32", Text = Truncate(bm.Success, 400) });
        if (!string.IsNullOrWhiteSpace(bm.Fail))
            blocks.Add(new TemplateBlockDto { Title = "失败", Accent = "#C62828", Text = Truncate(bm.Fail, 400) });

        // Conditions：8 组（Pre 橙 / 双方 粉蓝 / Fail 灰）
        var condGroups = new List<TemplateBadgeGroupDto>();
        AddCondGroup(condGroups, "我方前置", bm.UsPreConditions, bm, nameof(BattleMove.UsPreConditions), "#FFF3E0", "#E65100", ",");
        AddCondGroup(condGroups, "敌方前置", bm.ThemPreConditions, bm, nameof(BattleMove.ThemPreConditions), "#FFF3E0", "#E65100", ",");
        AddCondGroup(condGroups, "我方条件", bm.UsConditions, bm, nameof(BattleMove.UsConditions), "#F3E5F5", "#6A1B9A", "],[");
        AddCondGroup(condGroups, "敌方条件", bm.ThemConditions, bm, nameof(BattleMove.ThemConditions), "#E3F2FD", "#1565C0", "],[");
        AddCondGroup(condGroups, "双方条件", bm.PairConditions, bm, nameof(BattleMove.PairConditions), "#E3F2FD", "#1565C0", "],[");
        AddCondGroup(condGroups, "我方失败", bm.UsFailConditions, bm, nameof(BattleMove.UsFailConditions), "#ECEFF1", "#546E7A", ",");
        AddCondGroup(condGroups, "敌方失败", bm.ThemFailConditions, bm, nameof(BattleMove.ThemFailConditions), "#ECEFF1", "#546E7A", ",");
        AddCondGroup(condGroups, "双方失败", bm.PairFailConditions, bm, nameof(BattleMove.PairFailConditions), "#ECEFF1", "#546E7A", "],[");
        if (condGroups.Count > 0)
            blocks.Add(new TemplateBlockDto { Title = "条件", Accent = "#6A1B9A", BadgeGroups = condGroups });

        return new TemplateSemantics
        {
            HeroBadges = heroBadges,
            Subtitle = string.IsNullOrWhiteSpace(bm.Notes) ? null : bm.Notes,
            Blocks = blocks,
            Refs = Refs(bm),
        };
    }

    private static string FmtExposure(int v) => v switch
    {
        0 => "Hidden", 1 => "Seen", _ => "Any",
    };

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] + "…" : s;

    private void AddCondGroup(List<TemplateBadgeGroupDto> groups, string label, ReferenceList<IReferenceEntry> raw,
        IEntity source, string propName, string bg, string fg, string sep)
    {
        var rawText = SemanticsShared.Raw(raw, sep);
        if (string.IsNullOrWhiteSpace(rawText)) return;
        var badges = new List<BadgeDto>();
        foreach (var seg in rawText.Split(sep, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var isNeg = seg.StartsWith('-');
            var rid = isNeg ? seg[1..] : seg;
            var cond = _shared.Resolver.LookupRef<Condition>(source, propName, rid);
            badges.Add(cond is not null
                ? new BadgeDto
                {
                    Text = (isNeg ? "NOT " : "") + (cond.Subject ?? rid),
                    Bg = isNeg ? "#FFEBEE" : bg, Fg = isNeg ? "#C62828" : fg,
                    TargetType = "Condition", TargetId = cond.EntityId,
                }
                : new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
        }
        if (badges.Count > 0) groups.Add(new TemplateBadgeGroupDto { Label = label, Badges = badges });
    }

    // ═══════════════ B 级：CampType（营地类型）═══════════════

    public TemplateSemantics ExtractCampType(CampType ct)
    {
        var heroBadges = new List<BadgeDto>();
        if (!string.IsNullOrWhiteSpace(ct.Capacities) && ct.Capacities != "30x30")
            heroBadges.Add(new BadgeDto { Text = ct.Capacities, Bg = "#FFF3E0", Fg = "#E65100" });
        var heroStats = new List<FieldRowDto>();
        if (ct.SleepQuality != 0) heroStats.Add(new FieldRowDto { Value = $"睡眠质量 {ct.SleepQuality.ToString("0%", CultureInfo.InvariantCulture)}", Color = "#546E7A" });
        if (ct.HealPerHourMod != 0) heroStats.Add(new FieldRowDto { Value = $"每小时恢复 {ct.HealPerHourMod:+0%;-0%}", Color = ct.HealPerHourMod > 0 ? "#2E7D32" : "#C62828" });
        if (ct.Alertness != 0) heroStats.Add(new FieldRowDto { Value = $"警觉 {ct.Alertness.ToString("0%", CultureInfo.InvariantCulture)}", Color = "#546E7A" });

        var blocks = new List<TemplateBlockDto>();
        var statBars = new List<StatBarDto>
        {
            new() { Mode = "centered", Text = $"警觉 {ct.Alertness.ToString("0%", CultureInfo.InvariantCulture)}", Segments = { new StatSegmentDto { Value = Math.Clamp(ct.Alertness, 0, 1), Color = "#E65100" } }, Max = 1.0 },
            new() { Mode = "bipolar", Text = $"能见度 {ct.Visibility:+0.00;-0.00}", Segments = { new StatSegmentDto { Value = ct.Visibility, Color = "#1565C0" } }, Max = 1.0, NegativeColor = "#1565C0" },
            new() { Mode = "bipolar", Text = $"睡眠质量 {ct.SleepQuality:+0.0;-0.0}", Segments = { new StatSegmentDto { Value = ct.SleepQuality, Color = "#2E7D32" } }, Max = 1.0, NegativeColor = "#C62828" },
            new() { Mode = "bipolar", Text = $"潮湿温度调节 {ct.WetTempAdjustMod:+0;-0}°C", Segments = { new StatSegmentDto { Value = ct.WetTempAdjustMod, Color = "#E65100" } }, Max = 5.0, NegativeColor = "#1565C0" },
            new() { Mode = "bipolar", Text = $"每小时恢复 {ct.HealPerHourMod:+0.0;-0.0}", Segments = { new StatSegmentDto { Value = ct.HealPerHourMod, Color = "#2E7D32" } }, Max = 1.0, NegativeColor = "#C62828" },
        };
        blocks.Add(new TemplateBlockDto { Title = "营地属性", Accent = "#00695C", Bars = statBars });

        // Contents：TreasureId "3" 哨兵跳过 → 战利品树
        var rawTt = SemanticsShared.Raw(ct.TreasureId, null);
        if (!string.IsNullOrWhiteSpace(rawTt) && rawTt != "3")
        {
            var tt = _shared.Resolver.LookupRef<TreasureTable>(ct, nameof(CampType.TreasureId), rawTt);
            if (tt is not null)
            {
                var tree = _lootTrees.Build(tt);
                if (tree is not null)
                    blocks.Add(new TemplateBlockDto { Title = "营地物资", Accent = "#2E7D32", Trees = [tree] });
            }
        }

        return new TemplateSemantics
        {
            HeroBadges = heroBadges,
            HeroStats = heroStats,
            Subtitle = string.IsNullOrWhiteSpace(ct.Description) ? null : ct.Description,
            Blocks = blocks,
            Refs = Refs(ct),
        };
    }
}
