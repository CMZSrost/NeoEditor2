using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// D05 页面语义（Creature）：Hero（ID/NamePublic/Notes/N moves/阵营）→ ⚔ 战斗三层
/// （+阵营关系条/空手去噪）→ 🧬 属性与出场状态（StatGrid/状态概率徽章/Activities）→
/// 🎁 战利品（携带池/尸体池）→ 📍 遭遇三侧（事件链/出现于/刷新点权重归一）。
/// 纯数据移植自 CreatureEntityVisualizer（969 行 Avalonia 版），只出 DTO。
/// </summary>
public sealed class CreatureSemanticsExtractor
{
    private readonly SemanticsShared _shared;
    private readonly LootTreeBuilder _lootTrees;

    public CreatureSemanticsExtractor(SemanticsShared shared, LootTreeBuilder lootTrees)
    {
        _shared = shared;
        _lootTrees = lootTrees;
    }

    public string Loc(string key) => _shared.Loc(key);

    public CreatureSemantics Extract(Creature c)
    {
        return new CreatureSemantics
        {
            NamePublic = !string.IsNullOrWhiteSpace(c.NamePublic) && c.NamePublic != c.Name
                ? c.NamePublic : null,
            Notes = string.IsNullOrWhiteSpace(c.Notes) ? null : c.Notes,
            HeroBadges = BuildHeroBadges(c),
            Combat = BuildCombat(c),
            FactionRelation = BuildFactionRelation(c),
            AttributeCells = BuildAttributeCells(c),
            SpawnStatus = BuildSpawnStatus(c),
            Activities = BuildActivities(c),
            LootPools = BuildLootPools(c),
            EncounterChain = BuildEncounterChain(c),
            AppearsIn = BuildAppearsIn(c),
            SpawnPoints = BuildSpawnPoints(c),
            Refs = SemanticsShared.BuildRefSummary(_shared.DataTable, c.EntityId),
        };
    }

    // ═══════════════ Hero ═══════════════

    private List<BadgeDto> BuildHeroBadges(Creature c)
    {
        var badges = new List<BadgeDto>();
        if (c.MovesPerTurn > 0)
            badges.Add(new BadgeDto { Text = $"{c.MovesPerTurn} moves/turn", Bg = "#FFF3E0", Fg = "#E65100" });

        var factionRaw = SemanticsShared.Raw(c.Faction, null);
        if (!string.IsNullOrWhiteSpace(factionRaw) && factionRaw != "0")
        {
            var faction = _shared.Resolver.LookupRef<Faction>(c, nameof(Creature.Faction), factionRaw);
            badges.Add(faction is not null
                ? new BadgeDto { Text = faction.Subject ?? faction.Name, Bg = "#E8EAF6", Fg = "#283593", TargetType = "Faction", TargetId = faction.EntityId }
                : new BadgeDto { Text = factionRaw, Bg = "#F5F5F5", Fg = "#999" });
        }
        return badges;
    }

    // ═══════════════ ⚔ 战斗（D05 §4.2 三层）═══════════════

    private sealed record CreatureAttack(AttackMode Mode, bool Unresolved);

    /// <summary>vAttackModes：生物无槽位前缀，裸 ID 解析；未解析保留灰色行。</summary>
    private List<CreatureAttack> ParseAttackModes(Creature c)
    {
        var result = new List<CreatureAttack>();
        foreach (var seg in SemanticsShared.Raw(c.AttackModes, ",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var am = _shared.Resolver.LookupRef<AttackMode>(c, nameof(Creature.AttackModes), seg);
            if (am is null)
            {
                result.Add(new CreatureAttack(new AttackMode
                {
                    EntityId = $"__unresolved_{seg}",
                    Name = seg,
                }, true));
                continue;
            }
            result.Add(new CreatureAttack(am, false));
        }
        return result;
    }

    private CombatDto? BuildCombat(Creature c)
    {
        var modes = ParseAttackModes(c);
        if (modes.Count == 0) return null;

        var totalCut = modes.Sum(m => m.Mode.DamageCut);
        var totalBlunt = modes.Sum(m => m.Mode.DamageBlunt);
        var totalBase = totalCut + totalBlunt;
        var totalEffective = modes.Sum(m => (m.Mode.DamageCut + m.Mode.DamageBlunt) * (1 + m.Mode.Morale));

        return new CombatDto
        {
            TotalBar = new StatBarDto
            {
                Mode = "stacked",
                Segments =
                {
                    new StatSegmentDto { Value = totalCut, Color = "#E57373" },
                    new StatSegmentDto { Value = totalBlunt, Color = "#64B5F6" },
                },
            },
            TotalEffective = totalEffective > totalBase + 0.001
                ? $"{totalEffective:F1} (×{totalEffective / Math.Max(totalBase, 0.01):F2})"
                : null,
            // 全部为 1（拳头）时去噪注释（D05）
            FistsOnlyNote = modes.All(m => !m.Unresolved && m.Mode.Id == 1) ? Loc("Vis.FistsOnly") : null,
            Modes = modes.Select(m => _shared.BuildAttackMode(m.Mode, null, m.Unresolved)).ToList(),
        };
    }

    /// <summary>阵营关系条：声望 ≥50 绿（友好）/ 0-50 灰（中立）/ &lt;0 红（敌对）；解析失败静默隐藏。</summary>
    private StatBarDto? BuildFactionRelation(Creature c)
    {
        var factionRaw = SemanticsShared.Raw(c.Faction, null);
        if (string.IsNullOrWhiteSpace(factionRaw) || factionRaw == "0") return null;
        var faction = _shared.Resolver.LookupRef<Faction>(c, nameof(Creature.Faction), factionRaw);
        if (faction is null || string.IsNullOrWhiteSpace(SemanticsShared.Raw(faction.DictFactions, ","))) return null;

        double? rel = null;
        foreach (var seg in SemanticsShared.Raw(faction.DictFactions, ",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
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
        if (rel is not double relVal) return null;

        var desc = relVal >= 50 ? "友好" : relVal >= 0 ? "中立" : "敌对";
        // 0-50 中立 → 灰填充；≥50 友好 → 默认绿；<0 敌对 → 默认红（JS 侧负值兜底）
        var posColor = relVal >= 50 ? "#4CAF50" : relVal >= 0 ? "#9E9E9E" : "#E57373";
        var ratio = Math.Clamp(relVal / 100.0, 0.0, 1.0);
        return new StatBarDto
        {
            Mode = "centered",
            Segments = { new StatSegmentDto { Value = ratio, Color = relVal >= 0 ? posColor : "#E57373" } },
            Max = 1.0,
            Text = $"{Loc("Vis.TowardPlayer")} {relVal:+#;-#;0} ({desc})",
            PosColor = posColor,
        };
    }

    // ═══════════════ 🧬 属性与出场状态（D05 §4.3）═══════════════

    private static int CountSegments(ReferenceList<IReferenceEntry> list)
        => SemanticsShared.Raw(list, ",").Split(',').Count(s => s.Trim().Length > 0);

    private int CountPools(Creature c)
    {
        var n = 0;
        var carried = SemanticsShared.Raw(c.TreasureId, null);
        if (!string.IsNullOrWhiteSpace(carried) && carried != "3") n++;
        var corpse = SemanticsShared.Raw(c.CorpseId, null);
        if (!string.IsNullOrWhiteSpace(corpse) && corpse != "3") n++;
        return n;
    }

    private List<FieldRowDto> BuildAttributeCells(Creature c)
    {
        var cells = new List<FieldRowDto>();
        if (c.MovesPerTurn > 0) cells.Add(new FieldRowDto { Label = Loc("Vis.MovesPerTurn"), Value = $"{c.MovesPerTurn}", Color = "#E65100" });
        var atkCount = CountSegments(c.AttackModes);
        if (atkCount > 0) cells.Add(new FieldRowDto { Label = Loc("Vis.Attacks"), Value = $"{atkCount}", Color = "#C62828" });
        var condCount = CountSegments(c.BaseConditions);
        if (condCount > 0) cells.Add(new FieldRowDto { Label = Loc("Vis.SpawnStatus"), Value = $"{condCount}", Color = "#C62828" });
        var poolCount = CountPools(c);
        if (poolCount > 0) cells.Add(new FieldRowDto { Label = Loc("Vis.LootTable"), Value = $"{poolCount}", Color = "#2E7D32" });
        return cells;
    }

    /// <summary>出场状态徽章：`{id}={value}` 段 → 状态名 + 概率后缀（1 无后缀，&lt;1 `· 50%`）。</summary>
    private List<BadgeDto> BuildSpawnStatus(Creature c)
    {
        var result = new List<BadgeDto>();
        var raw = SemanticsShared.Raw(c.BaseConditions, ",");
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cond = _shared.Resolver.LookupRef<Condition>(c, nameof(Creature.BaseConditions), seg);
            if (cond is null)
            {
                result.Add(new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
                continue;
            }
            var prob = 1.0;
            var eqIdx = seg.IndexOf('=');
            if (eqIdx > 0 && double.TryParse(seg[(eqIdx + 1)..].Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                prob = p;
            var suffix = prob >= 1.0 ? "" : $" · {prob.ToString("0%", CultureInfo.InvariantCulture)}";
            var (bg, fg) = _shared.ConditionColors(cond);
            result.Add(new BadgeDto
            {
                Text = $"{cond.Subject}{suffix}", Bg = bg, Fg = fg,
                TargetType = "Condition", TargetId = cond.EntityId,
                Tooltip = _shared.ConditionEffectText(cond),
            });
        }
        return result;
    }

    /// <summary>日常行为：逗号分隔轻量徽章行（≤30 + +N more，有意的低价值弱化）。</summary>
    private List<string> BuildActivities(Creature c)
    {
        var acts = c.Activities.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (acts.Count == 0) return [];
        var total = acts.Count;
        if (total > 30) acts = acts.Take(30).ToList();
        if (total > 30) acts.Add($"+{total - 30} more");
        return acts;
    }

    // ═══════════════ 🎁 战利品（D05 §4.4 双池并置）═══════════════

    private List<LootPoolDto> BuildLootPools(Creature c)
    {
        var pools = new List<LootPoolDto>();
        AddPool(pools, Loc("Vis.CarriedLoot"), c, nameof(Creature.TreasureId), c.TreasureId);
        AddPool(pools, Loc("Vis.CorpseLoot"), c, nameof(Creature.CorpseId), c.CorpseId);
        return pools;
    }

    private void AddPool(List<LootPoolDto> pools, string label, Creature c, string propName,
        ReferenceList<IReferenceEntry> refList)
    {
        var raw = SemanticsShared.Raw(refList, null);
        if (string.IsNullOrWhiteSpace(raw) || raw == "3") return;   // 3=空池
        var tt = _shared.Resolver.LookupRef<TreasureTable>(c, propName, raw);
        if (tt is null)
        {
            pools.Add(new LootPoolDto { Label = label, UnresolvedId = raw });
            return;
        }
        pools.Add(new LootPoolDto { Label = label, Tree = _lootTrees.Build(tt) });
    }

    // ═══════════════ 📍 遭遇（D05 §4.5，Creature 特有三侧）═══════════════

    private List<BadgeDto> BuildEncounterChain(Creature c)
    {
        var result = new List<BadgeDto>();
        var raw = SemanticsShared.Raw(c.EncounterIds, ",");
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var enc = _shared.Resolver.LookupRef<Encounter>(c, nameof(Creature.EncounterIds), seg);
            result.Add(enc is not null
                ? new BadgeDto { Text = $"{enc.Subject ?? enc.Name} · {EncTypeLabel(enc)}", Bg = "#E8EAF6", Fg = "#283593", TargetType = "Encounter", TargetId = enc.EntityId }
                : new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
        }
        return result;
    }

    /// <summary>遭遇类型标签：0=剧情 / 1=搜刮 / 2=战斗 / 3=破解。</summary>
    private string EncTypeLabel(Encounter e) => e.Type switch
    {
        EncounterType.Normal => Loc("Vis.EncTypeStory"),
        EncounterType.Scavenge => Loc("Vis.EncTypeScavenge"),
        (EncounterType)2 => Loc("Vis.EncTypeCombat"),
        (EncounterType)3 => Loc("Vis.EncTypeHack"),
        _ => $"Type {e.Type}",
    };

    /// <summary>会出现在哪些剧情：反查 Encounter.CreatureId → 本生物（含 creatureHex 半径）。</summary>
    private List<BadgeDto> BuildAppearsIn(Creature c)
    {
        var result = new List<BadgeDto>();
        if (!_shared.DataTable.ReferenceLookups.TryGetValue(typeof(Encounter), out var list) || list is null) return result;
        foreach (var e in list.OfType<Encounter>())
        {
            if (!ReferencesCreature(e.CreatureId, c.Id)) continue;
            var label = e.Subject ?? e.Name ?? $"Encounter#{e.Id}";
            if (!string.IsNullOrWhiteSpace(e.CreatureHex) && e.CreatureHex != "0,0")
                label += $" · {e.CreatureHex}";
            result.Add(new BadgeDto { Text = label, Bg = "#E8EAF6", Fg = "#283593", TargetType = "Encounter", TargetId = e.EntityId });
        }
        return result;
    }

    /// <summary>刷新点：反查 CreatureSource.CreatureId；权重按同坐标 Σ 归一，可跳转 CreatureSource。</summary>
    private List<SpawnPointDto> BuildSpawnPoints(Creature c)
    {
        var result = new List<SpawnPointDto>();
        if (!_shared.DataTable.ReferenceLookups.TryGetValue(typeof(CreatureSource), out var list) || list is null)
            return result;
        var allSources = list.OfType<CreatureSource>().ToList();
        var sources = allSources.Where(s => ReferencesCreature(s.CreatureId, c.Id)).ToList();
        if (sources.Count == 0) return result;

        // 同点权重归一（与 Avalonia GetWeightInfo 同源）
        var weightsAt = allSources
            .GroupBy(s => (s.X, s.Y))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Weight));

        foreach (var cs in sources)
        {
            var total = weightsAt.TryGetValue((cs.X, cs.Y), out var w) ? w : 0.0;
            var proportion = total > 0 ? cs.Weight / total : 1.0;
            result.Add(new SpawnPointDto
            {
                Name = cs.Subject ?? cs.Name ?? $"Source#{cs.Id}",
                TargetType = "CreatureSource",
                TargetId = cs.EntityId,
                Position = $"({cs.X}, {cs.Y})",
                CountText = $"{cs.Min}–{cs.Max} 只",
                WeightText = $"权重 {cs.Weight:F2}（占同点 {proportion.ToString("0%", CultureInfo.InvariantCulture)}）",
            });
        }
        return result;
    }

    /// <summary>引用列（CreatureId）是否指向给定生物编号；空列表守卫，支持 "NSE:" 前缀。</summary>
    private static bool ReferencesCreature(ReferenceList<IReferenceEntry> list, int creatureId)
    {
        if (list is null) return false;
        var raw = SemanticsShared.Raw(list, null);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var colonIdx = raw.IndexOf(':');
        var idPart = colonIdx >= 0 ? raw[(colonIdx + 1)..].Trim() : raw.Trim();
        return int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && id == creatureId;
    }
}
