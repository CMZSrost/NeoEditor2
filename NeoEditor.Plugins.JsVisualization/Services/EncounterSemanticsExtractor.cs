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
/// D09 principle ②: semantic extraction stays in C# (xUnit-covered), the JS page
/// only renders. This class is the pure-data port of the Encounter page semantics
/// specified in D06 (branch model), D07 (end kinds / ingredients / p2·p3 / AND)
/// and D08 (page order: Hero → 内容与效果 → 场景流转 → 如何进入) — no Avalonia
/// controls, all display strings pre-localized.
/// </summary>
public sealed class EncounterSemanticsExtractor
{
    private readonly IEntityLookupService _dataTable;
    private readonly IReferenceResolver _resolver;
    private readonly ILocalizationService _loc;
    private readonly Func<string, string?> _findImage;

    public EncounterSemanticsExtractor(
        IEntityLookupService dataTable,
        IReferenceResolver resolver,
        ILocalizationService localization,
        Func<string, string?> findImage)
    {
        _dataTable = dataTable;
        _resolver = resolver;
        _loc = localization;
        _findImage = findImage;
    }

    private string Loc(string key) => _loc[key];
    private string Loc(string key, params object[] args) => _loc[key, args];

    public EncounterSemantics Extract(Encounter enc, ISet<string>? activePreConds = null)
    {
        var pre = activePreConds ?? new HashSet<string>();
        var predecessors = FindPredecessors(enc);
        var (branches, _) = PrepareBranches(enc, pre);
        var (isEntry, isTerminal) = DetermineEntryTerminal(predecessors.Count, branches);

        return new EncounterSemantics
        {
            TypeChip = TypeChip((int)enc.Type),
            IsEntry = isEntry,
            IsTerminal = isTerminal,
            RemoveCreatures = enc.RemoveCreatures,
            RemoveUsed = enc.RemoveUsed,
            Price = enc.Price,
            LootChance = enc.LootChance,
            AccidentChance = enc.AccidentChance,
            CreatureChance = enc.CreatureChance,
            Description = Truncate(enc.Description, 2000),
            FormatHint = "格式: [物品ID]x[数量]=[剧情ID]x[权重]  ·  空物品(=开头)=无需物品的选项  ·  概率=权重/权重和",
            Flow = BuildFlow(enc, predecessors, branches, pre),
            Effects = BuildEffects(enc),
            Entry = BuildEntry(enc),
        };
    }

    private static string? Truncate(string s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Length > max ? s[..max] + "…" : s;

    private string? ImageUrl(ReferenceList<IReferenceEntry>? imageList)
    {
        var raw = imageList?.ToRawString(null);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var path = _findImage(raw);
        if (string.IsNullOrWhiteSpace(path)) return null;
        return "/viz/assets?path=" + Uri.EscapeDataString(path);
    }

    // ═══════════════ response parsing (D07 §四/§五/§六 — ported from the Encounter visualizer) ═══════════════

    private sealed record ParsedItem(
        string? ItemId, double ItemMult, ItemType? Item, Ingredient? Ing,
        bool DestroyOnUse, double SuccessProb);

    private sealed record ResponseEntry(
        List<ParsedItem> Items, int TargetId, double Weight, double Probability,
        Encounter? TargetEncounter);

    private List<ResponseEntry> ParseResponseEntries(string raw, Encounter sourceEnc,
        Dictionary<string, ItemType>? precomputedItemTypes = null)
    {
        var result = new List<ResponseEntry>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        var itemTypes = precomputedItemTypes ?? TryBuildItemTypes(sourceEnc.ModId);
        var rawEntries = new List<(List<ParsedItem> items, int targetId, double weight, Encounter? targetEnc)>();
        double totalWeight = 0;

        foreach (var seg in raw.Split(','))
        {
            var s = seg.Trim();
            if (s.Length == 0) continue;

            var parsedItems = new List<(string? ItemId, double ItemMult, ItemType? Item, Ingredient? Ing)>();
            int targetId;
            double weight = 1.0;
            bool destroyOnUse = false;
            double successProb = 1.0;

            var eqIdx = s.IndexOf('=');
            if (eqIdx < 0)
            {
                var parts = s.Split('x');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out targetId)) continue;
                weight = double.TryParse(parts[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var p1) ? p1 : 1.0;
            }
            else
            {
                if (eqIdx > 0)
                {
                    var itemPart = s[..eqIdx].Trim();
                    if (itemPart.EndsWith('x')) itemPart = itemPart[..^1];
                    foreach (var piece in itemPart.Split('+'))
                    {
                        var p = piece.Trim();
                        if (p.Length == 0) continue;
                        var itemParts = p.Split('x');
                        var id = itemParts[0].Trim();
                        var mult = itemParts.Length >= 2 && double.TryParse(itemParts[1],
                            NumberStyles.Float, CultureInfo.InvariantCulture, out var im) ? im : 1.0;
                        parsedItems.Add(ResolveResponseItem(sourceEnc, id, mult, itemTypes));
                    }
                }

                var encPart = s[(eqIdx + 1)..].Trim();
                var encParts = encPart.Split('x');
                if (encParts.Length < 2) continue;
                if (!int.TryParse(encParts[0], out targetId)) continue;
                weight = double.TryParse(encParts[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var p2) ? p2 : 1.0;
                destroyOnUse = encParts.Length >= 3 && encParts[2] == "1";
                if (encParts.Length >= 4 && double.TryParse(encParts[3],
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var p3) && p3 > 0)
                    successProb = p3;
            }

            var items = parsedItems
                .Select(pi => new ParsedItem(pi.ItemId, pi.ItemMult, pi.Item, pi.Ing, destroyOnUse, successProb))
                .ToList();
            if (items.Count == 0)
                items.Add(new ParsedItem(null, 1.0, null, null, destroyOnUse, successProb));

            totalWeight += weight;
            Encounter? targetEnc =
                _resolver.LookupRef<Encounter>(sourceEnc, nameof(Encounter.Responses), targetId.ToString());

            rawEntries.Add((items, targetId, weight, targetEnc));
        }

        foreach (var (items, targetId, weight, targetEnc) in rawEntries)
        {
            var prob = totalWeight > 0 ? weight / totalWeight : 1.0 / rawEntries.Count;
            result.Add(new ResponseEntry(items, targetId, weight, prob, targetEnc));
        }

        return result;
    }

    private (string? ItemId, double ItemMult, ItemType? Item, Ingredient? Ing) ResolveResponseItem(
        Encounter sourceEnc, string id, double mult, Dictionary<string, ItemType>? itemTypes)
    {
        ItemType? item = null;
        Ingredient? ing = null;
        if (id.Length > 0 && !int.TryParse(id, out _))
        {
            if (itemTypes is not null && itemTypes.TryGetValue(id, out var found))
                item = found;
        }
        else if (id.Length > 0)
        {
            ing = _resolver.LookupRef<Ingredient>(sourceEnc, nameof(Encounter.Responses), id);
            if (ing is null)
                item = _resolver.LookupRef<ItemType>(sourceEnc, nameof(Encounter.Responses), id);
        }
        return (id.Length > 0 ? id : null, mult, item, ing);
    }

    private Dictionary<string, ItemType>? TryBuildItemTypes(int sourceModId)
    {
        try
        {
            return _dataTable.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", sourceModId);
        }
        catch
        {
            return null;
        }
    }

    // ═══════════════ branch model (D06 §4.5 + D07 §3.1) ═══════════════

    public enum BranchEndKind { None, Stay, Blank }

    public sealed record BranchItem(
        string? ItemId, double ItemMult, ItemType? Item, Ingredient? Ing,
        bool DestroyOnUse = false, double SuccessProb = 1.0, bool IsAnd = false);

    public sealed record BranchData(
        int TargetId, Encounter? Target, List<BranchItem> Items, double Weight,
        double EffectiveProb, bool IsSatisfied, List<(string Raw, bool IsNeg, Condition? Resolved)> PreConds,
        BranchEndKind EndKind = BranchEndKind.None, double? SuccessProb = null);

    private (List<BranchData> Branches, double ValidTotalWeight) PrepareBranches(
        Encounter enc, ISet<string> activePreConds)
    {
        var responses = ParseResponseEntries(enc.Responses, enc);
        var (branches, validTotal) = PrepareBranches(responses, activePreConds);
        for (int i = 0; i < branches.Count; i++)
        {
            var b = branches[i];
            if (b.EndKind == BranchEndKind.None)
            {
                var kind = DetermineEndKind(b.TargetId, enc.Id);
                if (kind != BranchEndKind.None)
                    branches[i] = b with { EndKind = kind };
            }
        }
        return (branches, validTotal);
    }

    internal static BranchEndKind DetermineEndKind(int targetId, int currentId)
    {
        if (targetId == currentId) return BranchEndKind.Stay;
        if (targetId == 1 && currentId != 1) return BranchEndKind.Blank;
        return BranchEndKind.None;
    }

    internal static (bool IsEntry, bool IsTerminal) DetermineEntryTerminal(
        int inDegree, IReadOnlyList<BranchData> branches)
    {
        var isEntry = inDegree == 0;
        var isTerminal = branches.All(b => b.EndKind != BranchEndKind.None);
        return (isEntry, isTerminal);
    }

    private (List<BranchData> Branches, double ValidTotalWeight) PrepareBranches(
        List<ResponseEntry> responses, ISet<string> activePreConds)
    {
        var merged = new Dictionary<int, (Encounter? Target, List<BranchItem> Items, double Weight)>();
        var order = new List<int>();
        foreach (var resp in responses)
        {
            if (!merged.TryGetValue(resp.TargetId, out var m))
            {
                m = (resp.TargetEncounter, new List<BranchItem>(), 0);
                order.Add(resp.TargetId);
            }
            for (int i = 0; i < resp.Items.Count; i++)
            {
                var pi = resp.Items[i];
                m.Items.Add(new BranchItem(pi.ItemId, pi.ItemMult, pi.Item, pi.Ing,
                    pi.DestroyOnUse, pi.SuccessProb, IsAnd: i > 0));
            }
            m.Weight += resp.Weight;
            merged[resp.TargetId] = m;
        }

        var branches = new List<BranchData>(merged.Count);
        double validTotalWeight = 0;
        foreach (var targetId in order)
        {
            var (target, items, weight) = merged[targetId];
            var preConds = ResolvePreConds(target);
            var satisfied = AreAllPreCondsSatisfied(preConds, activePreConds);
            if (satisfied) validTotalWeight += weight;
            branches.Add(new BranchData(targetId, target, items, weight, 0.0, satisfied, preConds));
        }

        for (int i = 0; i < branches.Count; i++)
        {
            var b = branches[i];
            var effective = validTotalWeight > 0 && b.IsSatisfied ? b.Weight / validTotalWeight : 0.0;
            double? successProb = null;
            foreach (var item in b.Items)
                if (item.SuccessProb < 1.0)
                    successProb = successProb is null ? item.SuccessProb : Math.Max(successProb.Value, item.SuccessProb);
            branches[i] = b with { EffectiveProb = effective, SuccessProb = successProb };
        }
        return (branches, validTotalWeight);
    }

    private List<(string Raw, bool IsNeg, Condition? Resolved)> ResolvePreConds(Encounter? target)
    {
        var result = new List<(string Raw, bool IsNeg, Condition? Resolved)>();
        if (target is null || target.PreConditions.Count == 0) return result;
        foreach (var raw in target.PreConditions.Select(e => e.ToRawString()).Where(s => s.Length > 0))
        {
            var isNeg = raw.StartsWith("-");
            var cond = _resolver.LookupRef<Condition>(target, nameof(Encounter.PreConditions), raw);
            result.Add((raw, isNeg, cond));
        }
        return result;
    }

    internal static bool IsPreCondSatisfied(string preStr, ISet<string> activeSet)
    {
        if (activeSet.Count == 0) return true;
        var isNeg = preStr.StartsWith("-");
        var rid = isNeg ? preStr[1..] : preStr;
        return isNeg ? !activeSet.Contains(rid) : activeSet.Contains(rid);
    }

    private static bool AreAllPreCondsSatisfied(
        List<(string Raw, bool IsNeg, Condition? Resolved)> preConds, ISet<string> activeSet)
    {
        if (preConds.Count == 0) return true;
        return preConds.All(p => IsPreCondSatisfied(p.Raw, activeSet));
    }

    internal static string FormatProbability(double p)
        => Math.Clamp(p, 0.0, 1.0).ToString("0.##%", CultureInfo.InvariantCulture);

    // ═══════════════ predecessors (D08 §二) ═══════════════

    private List<(Encounter Source, string? ItemDesc, double Weight)> FindPredecessors(Encounter enc)
    {
        var result = new List<(Encounter Source, string? ItemDesc, double Weight)>();
        if (!_dataTable.ReferenceLookups.TryGetValue(typeof(Encounter), out var list) || list is null)
            return result;

        Dictionary<string, ItemType>? itemTypes = null;
        var merged = new Dictionary<string, (Encounter Source, List<string> Descs, double Weight)>();
        var order = new List<string>();
        foreach (var obj in list)
        {
            if (obj is not Encounter src || src.EntityId == enc.EntityId) continue;
            itemTypes ??= TryBuildItemTypes(src.ModId);
            foreach (var resp in ParseResponseEntries(src.Responses, src, itemTypes))
            {
                var targetsHere = resp.TargetEncounter?.EntityId == enc.EntityId
                                  || (resp.TargetEncounter is null && resp.TargetId == enc.Id);
                if (!targetsHere) continue;
                if (!merged.TryGetValue(src.EntityId, out var m))
                {
                    m = (src, new List<string>(), 0);
                    order.Add(src.EntityId);
                }
                var desc = DescribeSegmentItems(resp.Items);
                if (desc is not null) m.Descs.Add(desc);
                m.Weight += resp.Weight;
                merged[src.EntityId] = m;
            }
        }

        foreach (var eid in order)
        {
            var (src, descs, weight) = merged[eid];
            var itemDesc = descs.Count > 0 ? string.Join(" / ", descs) : null;
            result.Add((src, itemDesc, weight));
        }
        return result;
    }

    private string? DescribeSegmentItems(List<ParsedItem> items)
    {
        var single = items.Count == 1;
        var labels = new List<string>(items.Count);
        foreach (var i in items)
        {
            var lbl = ItemLabel(i.Item, i.Ing, i.ItemId, i.ItemMult, i.DestroyOnUse, forceQty: single);
            if (lbl.Length > 0) labels.Add(lbl);
        }
        if (labels.Count == 0) return null;
        if (labels.Count == 1) return labels[0];
        return $"{Loc("Vis.RequireAll")}{string.Join(" + ", labels)}";
    }

    private string ItemLabel(ItemType? item, Ingredient? ing, string? itemId, double mult, bool destroyOnUse,
        bool forceQty = false)
    {
        var qty = forceQty || mult > 1 ? $" ×{mult.ToString("0.##", CultureInfo.InvariantCulture)}" : "";
        var consumed = destroyOnUse ? Loc("Vis.Consumed") : "";
        if (ing is not null) return $"🛠 {ing.Name}{qty}{consumed}";
        if (item is not null) return $"🛡 {item.Name}{qty}{consumed}";
        return itemId is not null ? $"Item #{itemId}{qty}{consumed}" : "";
    }

    private string? BranchAnnotation(BranchData b)
    {
        var parts = new List<string>();
        foreach (var item in b.Items)
        {
            var items = new List<ParsedItem>();
            if (item.Item is not null || item.Ing is not null || item.ItemId is not null)
                items.Add(new ParsedItem(item.ItemId, item.ItemMult, item.Item, item.Ing,
                    item.DestroyOnUse, item.SuccessProb));
            var lbl = DescribeSegmentItems(items);
            if (lbl is not null) parts.Add(lbl);
        }
        return parts.Count > 0 ? string.Join(" ｜ ", parts) : null;
    }

    // ═══════════════ DTO builders ═══════════════

    private ChipDto TypeChip(int rawType) => rawType switch
    {
        0 => new ChipDto { Label = Loc("Vis.TypeStory"), Bg = "#E3F2FD", Fg = "#1565C0" },
        1 => new ChipDto { Label = Loc("Vis.TypeScavenge"), Bg = "#FFF3E0", Fg = "#E65100" },
        2 => new ChipDto { Label = Loc("Vis.TypeCombat"), Bg = "#FFEBEE", Fg = "#C62828" },
        3 => new ChipDto { Label = Loc("Vis.TypeHack"), Bg = "#F3E5F5", Fg = "#6A1B9A" },
        _ => new ChipDto { Label = Loc("Vis.TypeUnknown", rawType), Bg = "#F5F5F5", Fg = "#999" },
    };

    private NodeDto ToNode(Encounter e) => new()
    {
        Type = "Encounter",
        Id = e.EntityId,
        DisplayName = e.Subject ?? $"Enc #{e.Id}",
        Image = ImageUrl(e.Image),
        TypeChip = TypeChip((int)e.Type),
    };

    private FlowDto BuildFlow(Encounter enc, List<(Encounter Source, string? ItemDesc, double Weight)> predecessors,
        List<BranchData> branches, ISet<string> activePreConds)
    {
        var preds = predecessors
            .Select(p => new NodeDto
            {
                Type = "Encounter",
                Id = p.Source.EntityId,
                DisplayName = p.Source.Subject ?? $"Enc #{p.Source.Id}",
                Image = ImageUrl(p.Source.Image),
                TypeChip = TypeChip((int)p.Source.Type),
                // R64: 来路标注放卡片底部行中间（无物品时显示权重）
                Annotation = p.ItemDesc
                    ?? p.Weight.ToString("F1", CultureInfo.InvariantCulture),
            })
            .ToList();

        var branchDtos = branches.Select(b => new BranchDto
        {
            TargetId = b.TargetId,
            EntityId = b.Target?.EntityId,           // 解析成功才可导航（缓存/查找键）
            DisplayName = b.Target?.Subject ?? $"Enc #{b.TargetId}",
            Image = b.Target is null ? null : ImageUrl(b.Target.Image),
            TypeChip = b.Target is null ? new ChipDto { Label = $"Enc #{b.TargetId}", Bg = "#F5F5F5", Fg = "#999" }
                : TypeChip((int)b.Target.Type),
            Resolved = b.Target is not null,
            EndKind = b.EndKind switch
            {
                BranchEndKind.Stay => "stay",
                BranchEndKind.Blank => "blank",
                _ => "none",
            },
            Weight = b.Weight,
            EffectiveProb = b.EffectiveProb,
            SuccessProb = b.SuccessProb,
            Annotation = BranchAnnotation(b),
            ItemBadges = BuildItemBadges(enc, b),
            PreConds = BuildPreCondChips(b.PreConds, activePreConds),
        }).ToList();

        // D08 v1.3: pre-condition filter checkboxes (positive ids only, one per raw id).
        var seenPre = new HashSet<string>();
        var filters = new List<PreCondFilterDto>();
        foreach (var b in branches)
        {
            if (b.Target is null) continue;
            foreach (var (raw, isNeg, cond) in b.PreConds)
            {
                var rawId = isNeg ? raw[1..] : raw;
                if (!seenPre.Add(rawId)) continue;
                filters.Add(new PreCondFilterDto { RawId = rawId, Display = cond?.Subject ?? rawId, IsNeg = isNeg });
            }
        }

        return new FlowDto { Predecessors = preds, Branches = branchDtos, PreCondFilters = filters };
    }

    private List<BadgeDto> BuildItemBadges(Encounter source, BranchData b)
    {
        var result = new List<BadgeDto>();
        foreach (var item in b.Items)
        {
            if (item.Ing is null && item.Item is null && item.ItemId is null) continue;
            var qty = item.ItemMult > 1 ? $" ×{item.ItemMult.ToString("0.##", CultureInfo.InvariantCulture)}" : "";
            var consumed = item.DestroyOnUse ? Loc("Vis.Consumed") : "";
            if (item.Ing is not null)
                result.Add(new BadgeDto
                {
                    Icon = "🛠", Text = $"{item.Ing.Name}{qty}{consumed}",
                    Bg = "#E8EAF6", Fg = "#283593",
                    TargetType = "Ingredient", TargetId = item.Ing.EntityId,
                });
            else if (item.Item is not null)
                result.Add(new BadgeDto
                {
                    Icon = "🛡", Text = $"{item.Item.Name}{qty}{consumed}",
                    Bg = "#E3F2FD", Fg = "#1565C0",
                    TargetType = "ItemType", TargetId = item.Item.EntityId,
                });
            else
                result.Add(new BadgeDto { Text = $"Item #{item.ItemId}{consumed}", Bg = "#F5F5F5", Fg = "#999" });
        }
        return result;
    }

    private List<PreCondChipDto> BuildPreCondChips(
        List<(string Raw, bool IsNeg, Condition? Resolved)> preConds, ISet<string> activeSet)
    {
        var result = new List<PreCondChipDto>();
        foreach (var (raw, isNeg, cond) in preConds)
        {
            var satisfied = IsPreCondSatisfied(raw, activeSet);
            result.Add(new PreCondChipDto
            {
                Raw = raw,
                Label = (isNeg ? "¬" : "") + (cond?.Subject ?? raw),
                IsNeg = isNeg,
                Satisfied = satisfied,
                Bg = satisfied ? "#E8F5E9" : "#FFEBEE",
                Fg = satisfied ? "#2E7D32" : "#C62828",
            });
        }
        return result;
    }

    // ═══════════════ ④ 内容与效果 (D08 §五) ═══════════════

    private EffectsDto? BuildEffects(Encounter enc)
    {
        var rows = new List<EffectRowDto>();

        // 🎁 获得（vLoot + nTreasureID 同类合并）
        if (!string.IsNullOrWhiteSpace(enc.Loot) && enc.Loot != "0" && enc.Loot != "3")
            rows.Add(BuildTreasureRow(Loc("Vis.GiveLoot"), "#E8F5E9", "#2E7D32",
                _resolver.LookupRef<TreasureTable>(enc, nameof(Encounter.Loot), enc.Loot), enc.Loot));
        if (!string.IsNullOrWhiteSpace(enc.TreasureId) && enc.TreasureId != "3")
            rows.Add(BuildTreasureRow(Loc("Vis.LootPool"), "#E8F5E9", "#2E7D32",
                _resolver.LookupRef<TreasureTable>(enc, nameof(Encounter.TreasureId), enc.TreasureId), enc.TreasureId));

        // 📦 给予物品（nItemsID ≠ "3"）
        if (!string.IsNullOrWhiteSpace(enc.ItemsId) && enc.ItemsId != "3")
        {
            var item = _resolver.LookupRef<ItemType>(enc, nameof(Encounter.ItemsId), enc.ItemsId);
            rows.Add(new EffectRowDto
            {
                Label = new ChipDto { Label = Loc("Vis.GiveItem"), Bg = "#E3F2FD", Fg = "#1565C0" },
                Badges = [ToBadge(item, enc.ItemsId, "#E3F2FD", "#1565C0")],
            });
        }

        // 💰 费用（实测全正 = 扣钱）
        if (enc.Price != 0)
        {
            rows.Add(new EffectRowDto
            {
                Label = new ChipDto { Label = Loc("Vis.Cost"), Bg = "#F3E5F5", Fg = "#6A1B9A" },
                Text = "$" + enc.Price.ToString("F2", CultureInfo.InvariantCulture),
            });
        }

        // 🗑 移除（nRemoveTreasureID ≠ "3"）
        if (!string.IsNullOrWhiteSpace(enc.RemoveTreasureId) && enc.RemoveTreasureId != "3")
        {
            var tt = _resolver.LookupRef<TreasureTable>(enc, nameof(Encounter.RemoveTreasureId), enc.RemoveTreasureId);
            rows.Add(new EffectRowDto
            {
                Label = new ChipDto { Label = Loc("Vis.RemoveLoot"), Bg = "#FFEBEE", Fg = "#C62828" },
                Badges = [ToBadge(tt, enc.RemoveTreasureId, "#FFEBEE", "#C62828")],
            });
        }

        // 📍 传送（ptTeleport ≠ "0,0"）
        if (!string.IsNullOrWhiteSpace(enc.Teleport) && enc.Teleport != "0,0")
        {
            rows.Add(new EffectRowDto
            {
                Label = new ChipDto { Label = Loc("Vis.TeleportTo"), Bg = "#E0F2F1", Fg = "#00695C" },
                Text = "(" + enc.Teleport + ")",
            });
        }

        // 🐾 刷出（nCreatureID ≠ "0"，带半径）
        if (!string.IsNullOrWhiteSpace(enc.CreatureId) && enc.CreatureId != "0")
        {
            var creature = _resolver.LookupRef<Creature>(enc, nameof(Encounter.CreatureId), enc.CreatureId);
            var hasRadius = !string.IsNullOrWhiteSpace(enc.CreatureHex) && enc.CreatureHex != "0,0";
            rows.Add(new EffectRowDto
            {
                Label = new ChipDto
                {
                    Label = "🐾 " + Loc("Vis.SpawnOut"), Bg = "#FFF3E0", Fg = "#E65100",
                },
                Badges = [ToBadge(creature, enc.CreatureId, "#E8EAF6", "#283593")],
                Text = hasRadius ? "(半径 " + enc.CreatureHex + ")" : null,
            });
        }

        // 💥 意外事件（vAccidents 真值才显示）
        if (!string.IsNullOrWhiteSpace(enc.Accidents) && enc.Accidents != "1")
        {
            var badges = new List<BadgeDto>();
            foreach (var seg in enc.Accidents.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var accident = _resolver.LookupRef<Encounter>(enc, nameof(Encounter.Accidents), seg);
                if (accident is not null)
                    badges.Add(new BadgeDto
                    {
                        Text = accident.Subject ?? seg, Bg = "#FFEBEE", Fg = "#C62828",
                        TargetType = "Encounter", TargetId = accident.EntityId,
                    });
                else
                    badges.Add(new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
            }
            rows.Add(new EffectRowDto
            {
                Label = new ChipDto { Label = Loc("Vis.Accidents"), Bg = "#FFEBEE", Fg = "#C62828" },
                Badges = badges,
            });
        }

        // 🗺 地图标注（并入效果区最后一行）
        var mapBadges = new List<BadgeDto>();
        if (!string.IsNullOrWhiteSpace(enc.MinimapHexes))
        {
            foreach (var seg in enc.MinimapHexes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var eqIdx = seg.IndexOf('=');
                var pos = eqIdx > 0 ? seg[..eqIdx].Trim() : seg;
                var label = eqIdx > 0 ? seg[(eqIdx + 1)..].Trim() : null;
                var text = string.IsNullOrEmpty(label) ? "📍(" + pos + ")" : "📍(" + pos + ") " + label;
                mapBadges.Add(new BadgeDto { Text = text, Bg = "#FFF8E1", Fg = "#F57F17" });
            }
        }
        if (!string.IsNullOrWhiteSpace(enc.Editor) && enc.Editor != "0,0")
            mapBadges.Add(new BadgeDto { Text = "✏️(" + enc.Editor + ")", Bg = "#F5F5F5", Fg = "#999" });
        if (mapBadges.Count > 0)
            rows.Add(new EffectRowDto
            {
                Label = new ChipDto { Label = Loc("Vis.MapNotes"), Bg = "#ECEFF1", Fg = "#546E7A" },
                Badges = mapBadges,
            });

        return rows.Count > 0 ? new EffectsDto { Rows = rows } : null;
    }

    private EffectRowDto BuildTreasureRow(string label, string bg, string fg, TreasureTable? tt, string rawId)
        => new()
        {
            Label = new ChipDto { Label = label, Bg = bg, Fg = fg },
            // v1: 只出表徽章（战利品嵌套树 P1）。
            Badges = [ToBadge(tt, rawId, bg, fg)],
        };

    private static BadgeDto ToBadge(IEntity? resolved, string rawId, string bg, string fg)
        => resolved is not null
            ? new BadgeDto
            {
                Text = resolved.Subject ?? rawId, Bg = bg, Fg = fg,
                TargetType = resolved.GetType().Name, TargetId = resolved.EntityId,
            }
            : new BadgeDto { Text = rawId, Bg = "#F5F5F5", Fg = "#999" };

    // ═══════════════ ③ 如何进入 (D08 §四) ═══════════════

    private EntryDto? BuildEntry(Encounter enc)
    {
        var entry = new EntryDto();
        var hasContent = false;

        // 触发条件（aConditions）——"1"/"0" 无条件占位去噪
        var condSegs = enc.Conditions.Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s != "1" && s != "0").ToList();
        foreach (var seg in condSegs)
        {
            var cond = _resolver.LookupRef<Condition>(enc, nameof(Encounter.Conditions), seg);
            var (bg, fg) = cond is null
                ? ("#F5F5F5", "#999")
                : cond.Fatal ? ("#FFEBEE", "#C62828")
                : cond.Duration <= 0 ? ("#FFF3E0", "#E65100")
                : cond.Stackable ? ("#E8F5E9", "#2E7D32")
                : ("#E3F2FD", "#1565C0");
            entry.Conditions.Add(new BadgeDto
            {
                Text = cond?.Subject ?? seg, Bg = bg, Fg = fg,
                TargetType = cond is null ? null : "Condition",
                TargetId = cond?.EntityId,
                Tooltip = cond is null ? null : ConditionEffectText(cond),
            });
            hasContent = true;
        }

        // 自身前置条件（aPreConditions）
        foreach (var seg in enc.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var isNeg = seg.StartsWith("-");
            var rawId = isNeg ? seg[1..] : seg;
            var cond = _resolver.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), seg);
            entry.OwnPreConditions.Add(new BadgeDto
            {
                Text = (isNeg ? "NOT " : "") + (cond?.Subject ?? seg),
                Bg = isNeg ? "#FFEBEE" : "#E8F5E9",
                Fg = isNeg ? "#C62828" : "#2E7D32",
                TargetType = cond is null ? null : "Condition",
                TargetId = cond?.EntityId,
            });
            hasContent = true;
        }

        // 触发器摘要（EncounterTrigger 反查）
        var triggers = FindTriggers(enc.Id);
        foreach (var trigger in triggers)
        {
            entry.Triggers.Add(new TriggerDto
            {
                Name = trigger.Name ?? $"Trigger #{trigger.Id}",
                Summary = TriggerSummary(trigger),
            });
            hasContent = true;
        }

        return hasContent ? entry : null;
    }

    private List<EncounterTrigger> FindTriggers(int encounterId)
    {
        if (!_dataTable.ReferenceLookups.TryGetValue(typeof(EncounterTrigger), out var list) || list is null)
            return [];
        return list.OfType<EncounterTrigger>()
            .Where(t => t.EncounterId.ToRawString(null) == encounterId.ToString()).ToList();
    }

    private string TriggerSummary(EncounterTrigger trigger)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(trigger.Area))
        {
            var (x, y, r) = TryParseArea(trigger.Area);
            parts.Add(r is { } d ? $"📍 ({x},{y},r={d})" : $"📍 ({x},{y})");
        }
        if (!string.IsNullOrWhiteSpace(trigger.DateMin) || !string.IsNullOrWhiteSpace(trigger.DateMax))
            parts.Add($"📅 {trigger.DateMin}~{trigger.DateMax}");
        if (trigger.HexTypes.Count > 0)
            parts.Add(Loc("Vis.HexTypesShort"));
        if (!trigger.Unique)
            parts.Add(Loc("Vis.Repeatable"));
        return string.Join("  ", parts);
    }

    private static (double X, double Y, double? R) TryParseArea(string area)
    {
        var nums = new List<double>();
        foreach (var chunk in area.Split(',', ';', ' ', '|'))
        {
            var t = chunk.Trim();
            if (t.Length == 0) continue;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                nums.Add(d);
        }
        if (nums.Count >= 2)
            return (nums[0], nums[1], nums.Count >= 3 ? nums[2] : null);
        return (0, 0, null);
    }

    private string ConditionEffectText(Condition c)
    {
        var fields = c.FieldNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mods = c.Modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length == 0) return "";

        var parts = new List<string>(fields.Length);
        for (int i = 0; i < fields.Length; i++)
        {
            var mod = i < mods.Length && double.TryParse(mods[i],
                NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 0;
            parts.Add($"{fields[i]} {mod:+#0.###;-#0.###;0}");
        }
        var text = string.Join(" · ", parts);
        return text.Length > 80 ? text[..80] + "…" : text;
    }
}
