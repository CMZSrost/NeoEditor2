using System;
using System.Collections.Generic;
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
/// D09 principle ② (semantics stay in C#) — shared building blocks for the
/// P1 extractors (ItemType/Creature/Recipe/薄类型): condition semantic colors
/// (D04/D05 同款)、攻击模式行/展开详情（D04 BuildAttackModeRow/Expanded 纯数据版）、
/// 反向引用聚合摘要（D10 §3.6 P1 静态版）、TopBar 审计统计（D10 §3.3，
/// 与 Avalonia RawData 折叠头同口径）。全部输出 DTO，无 Avalonia 控件。
/// </summary>
public sealed class SemanticsShared
{
    private static readonly MethodInfo LookupRefMethod = typeof(IReferenceResolver).GetMethod(
        nameof(IReferenceResolver.LookupRef))!;

    private readonly IEntityLookupService _dataTable;
    private readonly IReferenceResolver _resolver;
    private readonly ILocalizationService _loc;
    private readonly Func<string, string?> _findImage;

    public SemanticsShared(
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

    public string Loc(string key) => _loc[key];
    public string Loc(string key, params object[] args) => _loc[key, args];

    /// <summary>提取器经此访问引用解析（LookupRef&lt;T&gt; 规范路径）。</summary>
    public IReferenceResolver Resolver => _resolver;

    /// <summary>提取器经此访问全表反查（ReferenceLookups / GetCompositeEntities）。</summary>
    public IEntityLookupService DataTable => _dataTable;

    public string? ImageUrl(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : ImageUrl(raw, _findImage);

    /// <summary>
    /// 图片引用 → /viz/assets URL。候选链与 Avalonia LoadImage 完全对齐（这是
    /// "JS 可视化经常找不到图片"的根因修复）：① StripNs 去 "NSE:" 前缀；
    /// ② 子目录引用（img/scenario/x.png）退化为纯文件名搜索（FindImage 的
    /// Directory.GetFiles 精确全名匹配不支持路径分隔符，Windows 上必然 miss）；
    /// ③ 无扩展名引用补 ".png" 兜底（游戏数据常见）。
    /// </summary>
    public static string? ImageUrl(string raw, Func<string, string?> findImage)
    {
        var name = StripNs(raw.Trim());
        if (name.Length == 0) return null;
        var baseName = name.Contains('/') || name.Contains('\\')
            ? System.IO.Path.GetFileName(name) : name;
        var candidates = new List<string> { baseName };
        if (System.IO.Path.GetExtension(baseName).Length == 0)
            candidates.Add(baseName + ".png");
        foreach (var c in candidates)
        {
            var path = findImage(c);
            if (string.IsNullOrWhiteSpace(path)) continue;
            return "/viz/assets?path=" + Uri.EscapeDataString(path);
        }
        return null;
    }

    /// <summary>"NSE:img/x.png" → "img/x.png"（Avalonia LoadImage 同款）。</summary>
    public static string StripNs(string name)
    {
        var c = name.IndexOf(':');
        return c > 0 ? name[(c + 1)..] : name;
    }

    /// <summary>
    /// 读取引用列原文：优先解析条目（ToRawString —— 应用内实体编辑后 RawText 失效，
    /// 条目仍在），RawText-only 构造（测试桩/导入）回退原文。null 分隔符 = 单值列。
    /// </summary>
    public static string Raw(ReferenceList<IReferenceEntry> list, string? sep = ",")
    {
        var s = list.ToRawString(sep);
        if (s.Length > 0) return s;
        var rt = list.RawText ?? "";
        if (rt.Length == 0) return "";
        if (sep is null) return rt;
        return string.Join(sep, rt.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public BadgeDto ToBadge(IEntity? resolved, string rawId, string bg, string fg)
        => resolved is not null
            ? new BadgeDto
            {
                Text = resolved.Subject ?? rawId, Bg = bg, Fg = fg,
                TargetType = resolved.GetType().Name, TargetId = resolved.EntityId,
            }
            : new BadgeDto { Text = rawId, Bg = "#F5F5F5", Fg = "#999" };

    // ═══════════════ 条件语义色（R36 / D04 / D05 同款）═══════════════

    /// <summary>Doc 21 §4-C: Fatal 红 / Permanent(Instant) 橙 / Stackable 绿 / 时长蓝。</summary>
    public (string Bg, string Fg) ConditionColors(Condition c)
        => c.Fatal ? ("#FFEBEE", "#C62828")
            : c.Permanent ? ("#FFF3E0", "#E65100")
            : c.Stackable ? ("#E8F5E9", "#2E7D32")
            : ("#E3F2FD", "#1565C0");

    /// <summary>"FATAL" / "Instant" / "Stackable" / "12h" —— 严重性后缀（D04/D05 同款）。</summary>
    public string ConditionSuffix(Condition c)
        => c.Fatal ? "FATAL"
            : c.Permanent ? "Instant"
            : c.Stackable ? "Stackable"
            : $"{c.Duration:F0}h";

    /// <summary>"Bleeding · FATAL" / "WellFed · 12h" — 不开条件详情即可判断严重性。</summary>
    public string ConditionLabel(Condition c, string? extra)
    {
        var label = $"{c.Subject} · {ConditionSuffix(c)}";
        return string.IsNullOrEmpty(extra) ? label : $"{label} ({extra})";
    }

    /// <summary>条件徽章 DTO（可跳转 Condition；extra 如 "{id}x{mult}" 的 "x2"）。</summary>
    public ConditionChipDto ConditionChip(IEntity source, string propName, Condition c, string? extra = null)
    {
        var (bg, fg) = ConditionColors(c);
        return new ConditionChipDto
        {
            Label = ConditionLabel(c, extra),
            Bg = bg,
            Fg = fg,
            TargetType = "Condition",
            TargetId = c.EntityId,
            Tooltip = ConditionEffectText(c),
        };
    }

    /// <summary>条件效果翻译（field 修正值列表，Encounter 入口区同款）。</summary>
    public string ConditionEffectText(Condition c)
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

    // ═══════════════ 攻击模式（D04/D05 共用：行 + 展开详情）═══════════════

    /// <summary>一个攻击模式 → 行数据 + 展开详情（对照 BuildAttackModeRow/Expanded）。</summary>
    public AttackModeDto BuildAttackMode(AttackMode am, string? slotName = null, bool unresolved = false)
    {
        var name = string.IsNullOrEmpty(slotName)
            ? (am.Subject ?? am.Name)
            : $"{slotName}: {am.Subject ?? am.Name}";
        var isRanged = am.Type == AttackType.Ranged;

        var metaParts = new List<string?>();
        if (am.Range > 1 || isRanged) metaParts.Add($"{Loc("Vis.Range")} {am.Range}");
        if (am.Penetration > 0) metaParts.Add($"{Loc("Vis.Penetration")} {am.Penetration}");
        if (am.Morale != 0) metaParts.Add($"{Loc("Morale")} {am.Morale:+0%;-0%;0}");
        if (!string.IsNullOrWhiteSpace(am.Sound) && am.Sound != "cueNone") metaParts.Add(am.Sound);

        var moralePct = (int)(am.Morale * 100);
        var moraleLabel = moralePct == 25 ? $"{moralePct}% (base)" : $"{moralePct}%";
        var moraleColor = am.Morale > 0.25 ? "#66BB6A" : am.Morale < 0.25 ? "#E57373" : "#78909C";

        var totalDmg = am.DamageCut + am.DamageBlunt;

        // 数值格：射程 / 穿透 / Transfer
        var cells = new List<FieldRowDto>();
        if (am.Range > 1 || isRanged) cells.Add(new FieldRowDto { Label = Loc("Vis.Range"), Value = $"{am.Range}", Color = "#1565C0" });
        if (am.Penetration > 0) cells.Add(new FieldRowDto { Label = Loc("Vis.Penetration"), Value = $"{am.Penetration}", Color = "#6A1B9A" });
        if (am.Transfer) cells.Add(new FieldRowDto { Label = "Transfer", Value = Loc("Vis.Yes"), Color = "#546E7A" });

        return new AttackModeDto
        {
            Name = name,
            Resolved = !unresolved,
            DamageBar = new StatBarDto
            {
                Mode = "stacked",
                Segments =
                {
                    new StatSegmentDto { Value = am.DamageCut, Color = "#E57373" },
                    new StatSegmentDto { Value = am.DamageBlunt, Color = "#64B5F6" },
                },
            },
            Meta = metaParts.Count > 0 ? string.Join(" · ", metaParts) : null,
            Image = ImageUrl(am.Image.ToRawString(",")),
            TypeLabel = isRanged ? Loc("Vis.CombatRanged") : Loc("Vis.CombatMelee"),
            MoraleText = $"{Loc("Morale")} {moraleLabel}",
            MoraleColor = moraleColor,
            EffectiveText = totalDmg > 0
                ? $"{Loc("Vis.Effective")} {totalDmg * (1 + am.Morale):F1} (×{1 + am.Morale:F2})"
                : null,
            FormulaNote = totalDmg > 0 ? Loc("Vis.DamageFormula") : null,
            StatCells = cells,
            ChargeBadges = BuildChargeBadges(am),
            AttackerConditions = BuildAttackerConditions(am),
            WieldPhrase = string.IsNullOrWhiteSpace(am.WieldPhrase) ? null : am.WieldPhrase,
            AttackPhrases = am.AttackPhrases.Split(',', '，').Select(p => p.Trim())
                .Where(p => p.Length > 0).Select(p => p.Length > 60 ? p[..60] + "..." : p).ToList(),
            Notes = string.IsNullOrWhiteSpace(am.Notes) ? null : am.Notes,
            Sound = !string.IsNullOrWhiteSpace(am.Sound) && am.Sound != "cueNone" ? am.Sound : null,
        };
    }

    private List<BadgeDto> BuildChargeBadges(AttackMode am)
    {
        var result = new List<BadgeDto>();
        var raw = am.ChargeProfiles.ToRawString(",");
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cp = _resolver.LookupRef<ChargeProfile>(am, nameof(AttackMode.ChargeProfiles), seg);
            if (cp is null)
            {
                result.Add(new BadgeDto { Text = seg, Bg = "#F5F5F5", Fg = "#999" });
                continue;
            }
            var rates = new List<string>();
            if (cp.PerUse > 0) rates.Add($"{Loc("Vis.PerUse")} {cp.PerUse:F2}");
            if (cp.PerHour > 0) rates.Add($"{Loc("Vis.PerHour")} {cp.PerHour:F2}");
            if (cp.PerHourEquipped > 0) rates.Add($"{Loc("Vis.PerHourEquipped")} {cp.PerHourEquipped:F2}");
            if (cp.PerHex > 0) rates.Add($"{Loc("Vis.PerHex")} {cp.PerHex:F2}");
            var label = cp.Subject ?? cp.Name ?? $"CP#{cp.Id}";
            if (rates.Count > 0) label += $" ({string.Join(" · ", rates)})";
            if (cp.Degrade) label += " ⚠";
            result.Add(new BadgeDto
            {
                Text = label, Bg = "#E0F7FA", Fg = "#006064",
                TargetType = "ChargeProfile", TargetId = cp.EntityId,
            });
        }
        return result;
    }

    private List<ConditionChipDto> BuildAttackerConditions(AttackMode am)
    {
        var result = new List<ConditionChipDto>();
        var raw = am.AttackerConditions.ToRawString(",");
        if (string.IsNullOrWhiteSpace(raw)) return result;
        var pattern = ReferencePattern.FromName("{id}x{mult}");
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cond = _resolver.LookupRef<Condition>(am, nameof(AttackMode.AttackerConditions), seg);
            if (cond is null)
            {
                result.Add(new ConditionChipDto { Label = seg, Bg = "#F5F5F5", Fg = "#999" });
                continue;
            }
            var extra = pattern.FormatExtraInfo(seg);
            result.Add(ConditionChip(am, nameof(AttackMode.AttackerConditions), cond,
                string.IsNullOrEmpty(extra) ? null : extra));
        }
        return result;
    }

    // ═══════════════ 反向引用聚合摘要（D10 §3.6 P1 静态版）═══════════════

    /// <summary>类型分组 + 每组前 N 徽章（+more 计数）；store 不可用/无引用 → null。
    /// 静态方法：只需 dataTable（store 反向索引 + 合并表），提取器随处可调。
    /// P2: cap 提高到 100 —— RefPanel 过滤 + 滚动加载的数据基础（D10 §3.6）。</summary>
    public static RefSummaryDto? BuildRefSummary(IEntityLookupService dataTable, string entityId,
        int capPerGroup = 100)
    {
        var store = dataTable.BrowserStore ?? dataTable.ActiveMergeStore;
        if (store is null) return null;
        var rawRefs = store.IndexService?.ReverseLookup(entityId) ?? [];
        if (rawRefs.Count == 0) return null;

        // 来源实体索引（store 合并表；与 Avalonia BuildReverseRefsPanel 同源）
        var eidMap = new Dictionary<string, (Type SrcType, IEntity Entity)>();
        foreach (var (t, entities) in store.ReferenceLookups)
            foreach (var e in entities)
                if (e is IEntity ie)
                    eidMap.TryAdd(ie.EntityId, (t, ie));

        var resolved = new List<(Type SrcType, IEntity Entity, string PropName)>();
        foreach (var (srcEid, propName, _) in rawRefs)
            if (eidMap.TryGetValue(srcEid, out var info))
                resolved.Add((info.SrcType, info.Entity, propName));

        var groups = new List<RefGroupDto>();
        foreach (var g in resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()))
        {
            var items = g.Select(r => new BadgeDto
            {
                Text = r.Entity.Subject ?? r.Entity.EntityId,
                Bg = RefTypeBg(g.Key), Fg = RefTypeFg(g.Key),
                TargetType = g.Key.Name, TargetId = r.Entity.EntityId,
                Tooltip = string.IsNullOrEmpty(r.PropName) ? null : r.PropName,
            }).Take(capPerGroup).ToList();
            groups.Add(new RefGroupDto
            {
                TypeName = g.Key.Name,
                Count = g.Count(),
                Items = items,
                More = Math.Max(0, g.Count() - items.Count),
            });
        }
        return new RefSummaryDto { Groups = groups, Total = rawRefs.Count };
    }

    /// <summary>Avalonia BuildRefList 同款类型色：Creature/ItemType/Recipe/Condition 特例，其余灰。</summary>
    private static string RefTypeBg(Type t) => t switch
    {
        _ when t == typeof(Creature) => "#E8EAF6",
        _ when t == typeof(ItemType) => "#E3F2FD",
        _ when t == typeof(Recipe) => "#F3E5F5",
        _ when t == typeof(Condition) => "#FCE4EC",
        _ => "#F5F5F5",
    };

    private static string RefTypeFg(Type t) => t switch
    {
        _ when t == typeof(Creature) => "#283593",
        _ when t == typeof(ItemType) => "#1565C0",
        _ when t == typeof(Recipe) => "#6A1B9A",
        _ when t == typeof(Condition) => "#C62828",
        _ => "#666",
    };

    // ═══════════════ TopBar 审计统计（D10 §3.3）═══════════════

    /// <summary>N 字段 · M 有值 · K 未解析 —— 与 Avalonia RawData 折叠头同口径
    /// （VisHelperService.RawData ComputeRawDataStats 的纯数据移植）。</summary>
    public AuditSummaryDto BuildAudit(IEntity entity)
    {
        var props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>() != null
                        && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => p.MetadataToken)
            .ToList();

        var rows = new List<(bool HasValue, ReferenceFieldAttribute? RefAttr, string RawValue, string PropName)>();
        foreach (var p in props)
        {
            var val = p.GetValue(entity);
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            var strVal = val is bool b ? (b ? "1" : "0")
                : val is ReferenceList<IReferenceEntry> rl ? Raw(rl, refAttr?.Separator)
                : ReferenceText.GetRawString(val, refAttr);
            rows.Add((!string.IsNullOrWhiteSpace(strVal), refAttr, strVal, p.Name));
        }

        var unresolved = 0;
        foreach (var (hasValue, refAttr, rawValue, propName) in rows)
        {
            if (refAttr is null || !hasValue) continue;
            // R38: 非实体目标（ImageAsset）是原始文本引用 —— 永不"未解析"
            if (!typeof(IEntity).IsAssignableFrom(refAttr.TargetEntityType)
                && (refAttr.SecondaryTargetEntityType is null
                    || !typeof(IEntity).IsAssignableFrom(refAttr.SecondaryTargetEntityType)))
                continue;
            var sep = refAttr.Separator;
            var segments = sep is null
                ? new[] { rawValue }
                : rawValue.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                var s = seg.Trim();
                if (s.Length == 0) continue;
                var rawId = ReferenceParser.ExtractRawId(s, refAttr.Pattern);
                if (string.IsNullOrEmpty(rawId)) continue;
                if (ResolveRawSegment(entity, propName, refAttr.TargetEntityType, rawId) is null
                    && (refAttr.SecondaryTargetEntityType is null
                        || ResolveRawSegment(entity, propName, refAttr.SecondaryTargetEntityType, rawId) is null))
                    unresolved++;
            }
        }

        var text = $"{Loc("Vis.RawFields", rows.Count, rows.Count(r => r.HasValue))}"
                   + (unresolved > 0 ? Loc("Vis.RawUnresolved", unresolved) : "");
        return new AuditSummaryDto
        {
            Fields = rows.Count,
            Filled = rows.Count(r => r.HasValue),
            Unresolved = unresolved,
            Text = text,
        };
    }

    /// <summary>ResolveRawSegment 同款：LookupRef&lt;T&gt; 反射（上下文感知规范路径）。</summary>
    private IEntity? ResolveRawSegment(IEntity source, string propertyName, Type targetType, string rawId)
    {
        if (!typeof(IEntity).IsAssignableFrom(targetType)) return null;
        try
        {
            var m = LookupRefMethod.MakeGenericMethod(targetType);
            return m.Invoke(_resolver, new object?[] { source, propertyName, rawId }) as IEntity;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
