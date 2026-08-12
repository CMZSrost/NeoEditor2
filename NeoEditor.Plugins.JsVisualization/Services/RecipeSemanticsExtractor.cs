using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// Recipe 页面语义：Hero（ID/Type 徽章/flags/耗时/可逆/降级产物）→ 原料三组
/// （Tools 橙 / Consumed 红 / Destroyed 粉，逐 `+` 段卡 + Required/Forbidden 属性徽章）→
/// 产物（TT 树 + 前 6 ItemType 预览）→ Temp Product / AlsoTry / Hidden。
/// 纯数据移植自 RecipeEntityVisualizer（417 行 Avalonia 版）。
/// </summary>
public sealed class RecipeSemanticsExtractor
{
    private readonly SemanticsShared _shared;
    private readonly LootTreeBuilder _lootTrees;

    public RecipeSemanticsExtractor(SemanticsShared shared, LootTreeBuilder lootTrees)
    {
        _shared = shared;
        _lootTrees = lootTrees;
    }

    public string Loc(string key) => _shared.Loc(key);

    public RecipeSemantics Extract(Recipe r)
    {
        return new RecipeSemantics
        {
            Type = string.IsNullOrWhiteSpace(r.Type) ? null : r.Type,
            Flags = BuildFlags(r),
            SecretName = string.IsNullOrWhiteSpace(r.SecretName) ? null : $"{Loc("Vis.Secret")}: {r.SecretName}",
            HeroStats =
            [
                new FieldRowDto { Label = Loc("Vis.Hours"), Value = $"{r.Hours:F1}", Color = "#666" },
                new FieldRowDto { Label = Loc("Vis.Reverse"), Value = r.Reverse > 0 ? Loc("Vis.Yes") : Loc("Vis.No"), Color = "#666" },
                new FieldRowDto
                {
                    Label = Loc("Vis.DegradeOutput"),
                    Value = r.DegradeOutput ? Loc("Vis.Yes") : Loc("Vis.No"),
                    Color = r.DegradeOutput ? "#2E7D32" : "#999",
                },
            ],
            IngredientGroups = BuildIngredientGroups(r),
            Product = BuildProduct(r),
            TempProduct = BuildTempProduct(r),
            AlsoTry = BuildBadgeList(r.AlsoTry, r, nameof(Recipe.AlsoTry), "#F3E5F5", "#6A1B9A", "Recipe"),
            Hidden = BuildBadgeList(r.HiddenId, r, nameof(Recipe.HiddenId), "#FFF3E0", "#E65100", "Recipe"),
            Refs = SemanticsShared.BuildRefSummary(_shared.DataTable, r.EntityId),
        };
    }

    private static List<string> BuildFlags(Recipe r)
    {
        var flags = new List<string>();
        if (r.Scrap) flags.Add("Scrap");
        if (r.Identify) flags.Add("Identify");
        if (r.DegradeOutput) flags.Add("DegradeOutput");
        if (r.TransferComponents) flags.Add("TransferComponents");
        return flags;
    }

    // ═══════════════ 原料三组 ═══════════════

    private List<IngredientGroupDto> BuildIngredientGroups(Recipe r)
    {
        var groups = new List<IngredientGroupDto>();
        AddGroup(groups, Loc("Vis.Tools"), r.Tools, r, nameof(Recipe.Tools), "#FFF3E0", "#E65100");
        AddGroup(groups, Loc("Vis.Consumed"), r.Consumed, r, nameof(Recipe.Consumed), "#FFEBEE", "#C62828");
        AddGroup(groups, "Destroyed", r.Destroyed, r, nameof(Recipe.Destroyed), "#FCE4EC", "#880E4F");
        return groups;
    }

    private void AddGroup(List<IngredientGroupDto> groups, string label, ReferenceList<IReferenceEntry> raw,
        Recipe r, string propName, string bg, string fg)
    {
        var rawText = SemanticsShared.Raw(raw, "+");
        if (string.IsNullOrWhiteSpace(rawText)) return;

        var itemProps = _shared.DataTable.GetEntities<ItemProp>();
        var pattern = ReferencePattern.FromName("{mult}x{id}");
        var items = new List<IngredientDto>();

        foreach (var part in rawText.Split('+'))
        {
            var seg = part.Trim();
            if (seg.Length == 0) continue;
            var ing = _shared.Resolver.LookupRef<Ingredient>(r, propName, seg);
            var extra = pattern.FormatExtraInfo(seg);
            // FormatExtraInfo 返回 "x{N}"（{mult}x{id} 模式）→ 去 x 得数量
            var qty = string.IsNullOrEmpty(extra) ? "1" : extra.Trim('x');

            IngredientDto dto;
            if (ing is not null)
            {
                dto = new IngredientDto
                {
                    Name = ing.Name ?? seg,
                    Qty = qty == "1" ? null : qty,
                    TargetType = "Ingredient",
                    TargetId = ing.EntityId,
                    Required = BuildProps(ing.RequiredProps, ing, nameof(Ingredient.RequiredProps),
                        itemProps, "#E8F5E9", "#2E7D32"),
                    Forbidden = BuildProps(ing.ForbidProps, ing, nameof(Ingredient.ForbidProps),
                        itemProps, "#FFEBEE", "#C62828"),
                };
            }
            else
            {
                dto = new IngredientDto { Name = seg, Resolved = false, Qty = qty == "1" ? null : qty };
            }
            items.Add(dto);
        }

        if (items.Count > 0)
            groups.Add(new IngredientGroupDto { Label = label, Bg = bg, Fg = fg, Items = items });
    }

    /// <summary>Required/Forbidden 属性徽章：LookupRef 优先，int 业务键兜底（Avalonia 同款）。</summary>
    private List<BadgeDto> BuildProps(ReferenceList<IReferenceEntry> raw, Ingredient ing, string propName,
        Dictionary<int, ItemProp> itemProps, string bg, string fg)
    {
        var result = new List<BadgeDto>();
        var rawText = SemanticsShared.Raw(raw, "&");
        if (string.IsNullOrWhiteSpace(rawText)) return result;
        foreach (var pid in rawText.Split('&').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var prop = _shared.Resolver.LookupRef<ItemProp>(ing, propName, pid);
            if (prop is null && int.TryParse(pid, out var pidi))
                itemProps.TryGetValue(pidi, out prop);
            result.Add(prop is not null
                ? new BadgeDto { Text = prop.PropertyName ?? $"#{pid}", Bg = bg, Fg = fg, TargetType = "ItemProp", TargetId = prop.EntityId }
                : new BadgeDto { Text = $"#{pid}", Bg = "#F5F5F5", Fg = "#999" });
        }
        return result;
    }

    // ═══════════════ 产物 ═══════════════

    private LootTreeDto? BuildProduct(Recipe r)
    {
        var tt = _shared.Resolver.LookupRef<TreasureTable>(r, nameof(Recipe.TreasureId), r.TreasureId);
        if (tt is null) return null;
        return _lootTrees.Build(tt);
    }

    private List<BadgeDto> BuildTempProduct(Recipe r)
    {
        if (r.TempTreasureId == "3" || r.TempTreasureId == SemanticsShared.Raw(r.TreasureId, null)) return [];
        var tt = _shared.Resolver.LookupRef<TreasureTable>(r, nameof(Recipe.TempTreasureId), r.TempTreasureId);
        return [tt is not null
            ? new BadgeDto { Text = tt.Subject ?? tt.Name ?? r.TempTreasureId, Bg = "#E3F2FD", Fg = "#1565C0", TargetType = "TreasureTable", TargetId = tt.EntityId }
            : new BadgeDto { Text = r.TempTreasureId, Bg = "#F5F5F5", Fg = "#999" }];
    }

    private List<BadgeDto> BuildBadgeList(ReferenceList<IReferenceEntry> raw, Recipe r, string propName,
        string bg, string fg, string targetType)
    {
        var result = new List<BadgeDto>();
        var rawText = SemanticsShared.Raw(raw, ",");
        if (string.IsNullOrWhiteSpace(rawText)) return result;
        foreach (var seg in rawText.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var target = _shared.Resolver.LookupRef<Recipe>(r, propName, seg);
            result.Add(target is not null
                ? new BadgeDto { Text = target.Subject ?? target.Name ?? seg, Bg = bg, Fg = fg, TargetType = targetType, TargetId = target.EntityId }
                : new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
        }
        return result;
    }
}
