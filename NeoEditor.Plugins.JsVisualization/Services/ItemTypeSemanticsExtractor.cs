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
/// D04 页面语义（ItemType）：Hero（G.S 徽章/✦辨识/关键数字行/画廊）→ ⚔ 战斗三层 →
/// ✨ 效果（三组条件 + 辨识条件 + Properties）→ ⏳ 生命周期（耐久/损耗/寿命推演/破损产物/弹药）→
/// 📦 容器 → 🔗 来源产出（战利品树/SwitchIds）。纯数据移植自
/// ItemTypeEntityVisualizer（1484 行 Avalonia 版），只出 DTO，显示串预本地化。
/// </summary>
public sealed class ItemTypeSemanticsExtractor
{
    private static readonly Dictionary<int, string> SlotNames = new()
    {
        [2] = "R-Foot", [3] = "L-Foot", [4] = "Legs",
        [5] = "L-Hand", [6] = "R-Hand",
        [11] = "Torso", [13] = "L-Back", [14] = "R-Shoulder",
        [17] = "Face", [20] = "L-Hand", [21] = "R-Hand",
        [22] = "Back", [23] = "Head"
    };

    private static readonly Dictionary<int, string> WoundNames = new()
    {
        [100] = "左肩", [101] = "头部", [102] = "左前臂下端",
        [103] = "左胳膊肘", [104] = "左侧锁骨", [105] = "左侧肋骨",
        [106] = "右腹部", [107] = "左髋骨", [108] = "左大腿根部",
        [109] = "左膝盖下方", [110] = "左小腿",
        [111] = "右肩", [112] = "左前臂下端",
        [113] = "右胳膊肘", [114] = "右大腿根部",
        [115] = "右侧小腿", [116] = "右膝盖下方"
    };

    private readonly SemanticsShared _shared;
    private readonly LootTreeBuilder _lootTrees;

    public ItemTypeSemanticsExtractor(SemanticsShared shared, LootTreeBuilder lootTrees)
    {
        _shared = shared;
        _lootTrees = lootTrees;
    }

    public string Loc(string key) => _shared.Loc(key);

    /// <summary>返回人读槽位名；100~112 视为伤口。</summary>
    private static string GetSlotName(int slotId)
    {
        if (SlotNames.TryGetValue(slotId, out var name)) return name;
        if (WoundNames.TryGetValue(slotId, out var wname)) return wname;
        return slotId.ToString();
    }

    public ItemTypeSemantics Extract(ItemType it)
    {
        var modes = ParseAttackModes(it);
        var combat = BuildCombat(it, modes);
        var conditionGroups = BuildConditionGroups(it);

        return new ItemTypeSemantics
        {
            Gs = $"{it.GroupId}.{it.SubgroupId}",
            Description = !string.IsNullOrWhiteSpace(it.Description) && it.Description != it.Name
                ? it.Description : null,
            IdentifiedLabel = string.IsNullOrWhiteSpace(it.DescriptionAlt) ? null : $"✦ {Loc("Vis.Identified")}",
            IdentifiedDesc = string.IsNullOrWhiteSpace(it.DescriptionAlt) ? null : it.DescriptionAlt,
            HeroStats = BuildHeroStats(it),
            GalleryImages = BuildGallery(it),
            Combat = combat,
            Equipment = BuildEquipment(it),
            ConditionGroups = conditionGroups,
            Properties = BuildProperties(it),
            Lifecycle = BuildLifecycle(it),
            Container = BuildContainer(it),
            Associations = BuildAssociations(it),
            Refs = SemanticsShared.BuildRefSummary(_shared.DataTable, it.EntityId),
        };
    }

    // ═══════════════ Hero ═══════════════

    private List<FieldRowDto> BuildHeroStats(ItemType it)
    {
        var stats = new List<FieldRowDto>();
        if (it.Weight > 0) stats.Add(new FieldRowDto { Value = $"{it.Weight:F1} kg", Color = "#4CAF50" });
        if (it.MonetaryValue > 0)
        {
            var vt = it.MonetaryValueAlt > 0 && it.MonetaryValueAlt != it.MonetaryValue
                ? $"${it.MonetaryValue:F2} → ${it.MonetaryValueAlt:F2}"
                : $"${it.MonetaryValue:F2}";
            stats.Add(new FieldRowDto { Value = vt, Color = "#9C27B0" });
        }
        if (it.StackLimit > 0) stats.Add(new FieldRowDto { Value = $"×{it.StackLimit}", Color = "#2196F3" });
        if (it.Mirrored) stats.Add(new FieldRowDto { Value = Loc("Vis.Mirrored"), Color = "#607D8B" });
        if (it.SlotDepth > 0) stats.Add(new FieldRowDto { Value = $"{Loc("Vis.SlotDepth")} {it.SlotDepth}", Color = "#546E7A" });
        return stats;
    }

    /// <summary>多图画廊（vImageList 逗号分隔）；单图由快照 image 字段承载。</summary>
    private List<string> BuildGallery(ItemType it)
    {
        var names = SemanticsShared.Raw(it.ImageList, ",").Split(',')
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (names.Count <= 1) return [];
        return names.Select(_shared.ImageUrl).Where(u => u is not null).Cast<string>().ToList();
    }

    // ═══════════════ 🧍 装备（D04 §装备）═══════════════

    private EquipmentDto? BuildEquipment(ItemType it)
    {
        // 槽位解析：`slot=img=sprite`；-1 跳过；手持位无后缀（hasSuffix 语义保留但 v1 不展示穿戴预览）
        var slots = new List<BadgeDto>();
        if (!string.IsNullOrWhiteSpace(it.EquipSlots))
        {
            foreach (var seg in it.EquipSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                var parts = seg.Split('=');
                if (parts.Length >= 1 && int.TryParse(parts[0], out var slotNum) && slotNum != -1)
                    slots.Add(new BadgeDto { Text = GetSlotName(slotNum), Bg = "#E3F2FD", Fg = "#1565C0" });
            }
        }

        var useSlots = new List<BadgeDto>();
        if (!string.IsNullOrWhiteSpace(it.UseSlots))
        {
            foreach (var s in it.UseSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                useSlots.Add(new BadgeDto { Text = s == "211" ? "Self" : s, Bg = "#E8EAF6", Fg = "#283593" });
        }

        var sound = !string.IsNullOrWhiteSpace(it.Sounds) && it.Sounds != "cuePickup,cuePutdown" ? it.Sounds : null;

        if (slots.Count == 0 && useSlots.Count == 0 && !it.SocketLocked && sound is null)
            return null;

        return new EquipmentDto
        {
            Slots = slots,
            UseSlots = useSlots,
            SocketLocked = it.SocketLocked,
            Sound = sound,
        };
    }

    // ═══════════════ ⚔ 战斗三层（D04 §战斗）═══════════════

    private sealed record AttackModeSlot(string SlotName, AttackMode Mode, bool Unresolved);

    /// <summary>解析 aAttackModes："20=14"（槽位=模式）或 "14"（裸 ID）。</summary>
    private List<AttackModeSlot> ParseAttackModes(ItemType it)
    {
        var result = new List<AttackModeSlot>();
        if (string.IsNullOrWhiteSpace(SemanticsShared.Raw(it.AttackModes, ","))) return result;
        foreach (var seg in SemanticsShared.Raw(it.AttackModes, ",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var eqIdx = seg.IndexOf('=');
            var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
            var slotName = int.TryParse(slotPart, out var sn) ? GetSlotName(sn) : slotPart;

            var am = _shared.Resolver.LookupRef<AttackMode>(it, nameof(ItemType.AttackModes), seg);
            if (am is null)
            {
                result.Add(new AttackModeSlot(slotName, new AttackMode
                {
                    EntityId = $"__unresolved_{seg}",
                    Name = seg,
                }, true));
                continue;
            }
            result.Add(new AttackModeSlot(slotName, am, false));
        }
        return result;
    }

    private CombatDto? BuildCombat(ItemType it, List<AttackModeSlot> modes)
    {
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
            Modes = modes.Select(m => _shared.BuildAttackMode(m.Mode, m.SlotName, m.Unresolved)).ToList(),
        };
    }

    // ═══════════════ ✨ 效果（D04 §效果）═══════════════

    /// <summary>携带/使用/装备三组条件 + 辨识条件（CondId）。</summary>
    private List<ConditionGroupDto> BuildConditionGroups(ItemType it)
    {
        var groups = new List<ConditionGroupDto>();
        AddConditionGroup(groups, Loc("Vis.WhenCarried"), it.PossessConditions, it, nameof(ItemType.PossessConditions));
        AddConditionGroup(groups, Loc("Vis.WhenUsed"), it.UseConditions, it, nameof(ItemType.UseConditions));
        AddConditionGroup(groups, Loc("Vis.WhenEquipped"), it.EquipConditions, it, nameof(ItemType.EquipConditions));

        if (!string.IsNullOrWhiteSpace(SemanticsShared.Raw(it.CondId, null)))
        {
            var cond = _shared.Resolver.LookupRef<Condition>(it, nameof(ItemType.CondId), it.CondId);
            if (cond is not null)
                groups.Add(new ConditionGroupDto
                {
                    Label = Loc("Vis.RequiredCondition"),
                    Conditions = { _shared.ConditionChip(it, nameof(ItemType.CondId), cond) },
                });
        }
        return groups;
    }

    private void AddConditionGroup(List<ConditionGroupDto> groups, string label,
        ReferenceList<IReferenceEntry> raw, ItemType it, string propName)
    {
        if (string.IsNullOrWhiteSpace(SemanticsShared.Raw(raw, ","))) return;
        var conds = new List<ConditionChipDto>();
        foreach (var seg in SemanticsShared.Raw(raw, ",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var c = _shared.Resolver.LookupRef<Condition>(it, propName, seg);
            if (c is null)
            {
                conds.Add(new ConditionChipDto { Label = seg, Bg = "#F5F5F5", Fg = "#999" });
                continue;
            }
            // {value}={id} pattern：槽位前缀 + ¬ 否定（R36 语义色，否定灰）
            var eqIdx = seg.IndexOf('=');
            var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
            var isNeg = slotPart.StartsWith('-');
            var slotNumStr = isNeg ? slotPart[1..] : slotPart;
            var slotName = int.TryParse(slotNumStr, out var sn) ? GetSlotName(sn) : slotNumStr;
            var (bg, fg) = isNeg ? ("#F5F5F5", "#999") : _shared.ConditionColors(c);
            var text = string.IsNullOrEmpty(slotName) ? c.Subject : $"{slotName}: {(isNeg ? "~" : "")}{c.Subject}";
            conds.Add(new ConditionChipDto
            {
                Label = $"{text} · {_shared.ConditionSuffix(c)}",
                Bg = bg, Fg = fg,
                TargetType = "Condition", TargetId = c.EntityId,
                Tooltip = _shared.ConditionEffectText(c),
            });
        }
        if (conds.Count > 0) groups.Add(new ConditionGroupDto { Label = label, Conditions = conds });
    }

    private List<BadgeDto> BuildProperties(ItemType it)
    {
        var result = new List<BadgeDto>();
        foreach (var s in SemanticsShared.Raw(it.Properties, ",").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var prop = _shared.Resolver.LookupRef<ItemProp>(it, nameof(ItemType.Properties), s);
            result.Add(prop is not null
                ? new BadgeDto { Text = prop.PropertyName ?? s, Bg = "#E8F5E9", Fg = "#2E7D32", TargetType = "ItemProp", TargetId = prop.EntityId }
                : new BadgeDto { Text = s, Bg = "#F5F5F5", Fg = "#999" });
        }
        return result;
    }

    // ═══════════════ ⏳ 生命周期（D04 §生命周期）═══════════════

    private LifecycleDto? BuildLifecycle(ItemType it)
    {
        var lossRates = new List<FieldRowDto>();
        if (it.DegradePerHour > 0) lossRates.Add(new FieldRowDto { Label = Loc("Vis.PerHour"), Value = $"{it.DegradePerHour:F3}", Color = "#E65100" });
        if (it.EquipDegradePerHour > 0) lossRates.Add(new FieldRowDto { Label = Loc("Vis.PerHourEquipped"), Value = $"{it.EquipDegradePerHour:F3}", Color = "#C62828" });
        if (it.DegradePerUse > 0) lossRates.Add(new FieldRowDto { Label = Loc("Vis.PerUse"), Value = $"{it.DegradePerUse:F3}", Color = "#F57F17" });

        // R42: 寿命推演（损耗率 → "能用多久"）
        string? lifespan = null;
        if (it.Durability > 0 && it.Durability < 999)
        {
            var spans = new List<string>();
            if (it.DegradePerHour > 0) spans.Add($"{Loc("Vis.PerHour")} ≈{(it.Durability / it.DegradePerHour):F0}h");
            if (it.EquipDegradePerHour > 0) spans.Add($"{Loc("Vis.PerHourEquipped")} ≈{(it.Durability / it.EquipDegradePerHour):F0}h");
            if (it.DegradePerUse > 0) spans.Add($"{Loc("Vis.PerUse")} ≈{(it.Durability / it.DegradePerUse):F0}×");
            if (spans.Count > 0) lifespan = string.Join(" · ", spans);
        }

        // 破损产物（vDegradeTreasureIDs，3=空池去噪）
        var breakParts = new List<LootTreeDto>();
        foreach (var seg in SemanticsShared.Raw(it.DegradeTreasureIds, ",").Split(',').Select(s => s.Trim())
                     .Where(s => s.Length > 0 && s != "3"))
        {
            var tt = _shared.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.DegradeTreasureIds), seg);
            if (tt is null) continue;
            var tree = _lootTrees.Build(tt);
            if (tree is not null) breakParts.Add(tree);
        }

        // 弹药（strChargeProfiles）
        var charges = new List<BadgeDto>();
        foreach (var seg in SemanticsShared.Raw(it.ChargeProfiles, ",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cp = _shared.Resolver.LookupRef<ChargeProfile>(it, nameof(ItemType.ChargeProfiles), seg);
            charges.Add(cp is not null
                ? new BadgeDto { Text = cp.Subject ?? cp.Name ?? $"CP#{cp.Id}", Bg = "#E0F7FA", Fg = "#006064", TargetType = "ChargeProfile", TargetId = cp.EntityId }
                : new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
        }

        if (it.Durability <= 0 && lossRates.Count == 0 && lifespan is null && breakParts.Count == 0 && charges.Count == 0)
            return null;

        StatBarDto? durability = null;
        if (it.Durability > 0)
        {
            var dt = it.Durability >= 999 ? "∞" : $"{it.Durability * 100:F0}%";
            var ratio = it.Durability >= 999 ? 1.0 : Math.Clamp(it.Durability, 0.05, 1.0);
            var color = it.Durability >= 999 ? "#90A4AE" : ratio > 0.5 ? "#66BB6A" : ratio > 0.25 ? "#FFB74D" : "#E57373";
            durability = new StatBarDto
            {
                Mode = "centered",
                Segments = { new StatSegmentDto { Value = ratio, Color = color } },
                Max = 1.0,
                Text = $"{Loc("Vis.Durability")} {dt}",
            };
        }

        return new LifecycleDto
        {
            Durability = durability,
            LossRates = lossRates,
            Lifespan = lifespan,
            BreakParts = breakParts,
            ChargeProfiles = charges,
        };
    }

    // ═══════════════ 📦 容器（D04 §容器）═══════════════

    private ContainerDto? BuildContainer(ItemType it)
    {
        var contentIds = new List<BadgeDto>();
        foreach (var seg in SemanticsShared.Raw(it.ContentIds, ",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var ct = _shared.Resolver.LookupRef<ContainerType>(it, nameof(ItemType.ContentIds), seg);
            contentIds.Add(ct is not null
                ? new BadgeDto { Text = ct.Name, Bg = "#E8EAF6", Fg = "#283593", TargetType = "ContainerType", TargetId = ct.EntityId }
                : new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
        }

        string? format = null;
        if (!string.IsNullOrWhiteSpace(SemanticsShared.Raw(it.FormatId, null)))
        {
            var ct = _shared.Resolver.LookupRef<ContainerType>(it, nameof(ItemType.FormatId), it.FormatId);
            format = ct?.Name ?? SemanticsShared.Raw(it.FormatId, null);
        }

        if (string.IsNullOrWhiteSpace(it.Capacities) && contentIds.Count == 0 && format is null)
            return null;

        return new ContainerDto
        {
            Capacity = string.IsNullOrWhiteSpace(it.Capacities) ? null : it.Capacities,
            ContentIds = contentIds,
            Format = format,
        };
    }

    // ═══════════════ 🔗 来源产出（D04 §关联）═══════════════

    private AssociationsDto? BuildAssociations(ItemType it)
    {
        var switches = new List<BadgeDto>();
        foreach (var seg in SemanticsShared.Raw(it.SwitchIds, ",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var sw = _shared.Resolver.LookupRef<ItemType>(it, nameof(ItemType.SwitchIds), seg);
            if (sw is not null)
            {
                var descShort = string.IsNullOrWhiteSpace(sw.Description) ? ""
                    : sw.Description.Length > 10 ? sw.Description[..10] : sw.Description;
                var display = string.IsNullOrEmpty(descShort) ? sw.Name : $"{sw.Name}({descShort})";
                switches.Add(new BadgeDto
                {
                    Text = $"{sw.GroupId}.{sw.SubgroupId} {display}",
                    Bg = "#F3E5F5", Fg = "#6A1B9A",
                    TargetType = "ItemType", TargetId = sw.EntityId,
                });
            }
            else
            {
                switches.Add(new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
            }
        }

        var trees = new List<LootTreeDto>();
        if (!string.IsNullOrWhiteSpace(SemanticsShared.Raw(it.TreasureId, null)))
        {
            var tt = _shared.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.TreasureId), it.TreasureId);
            if (tt is not null)
            {
                var tree = _lootTrees.Build(tt);
                if (tree is not null) trees.Add(tree);
            }
        }
        if (!string.IsNullOrWhiteSpace(SemanticsShared.Raw(it.ComponentId, null)))
        {
            var comp = _shared.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.ComponentId), it.ComponentId);
            if (comp is not null)
            {
                var tree = _lootTrees.Build(comp);
                if (tree is not null) trees.Add(tree);
            }
        }

        if (switches.Count == 0 && trees.Count == 0) return null;
        return new AssociationsDto { Switches = switches, LootTrees = trees };
    }
}
