using System;
using System.Collections.Generic;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// D09 §3.1: the entity snapshot contract served by /viz/data — C#-side semantic
/// extraction (display strings already localized) + rawXml for debug/export.
/// The JS page renders this JSON as-is; it never re-derives game semantics.
/// </summary>
public sealed record EntitySnapshotDto
{
    public string Type { get; init; } = "";
    public string Id { get; init; } = "";
    public string? ModId { get; init; }
    public string? DisplayName { get; init; }
    /// <summary>URL path for /viz/assets, or null when the entity has no image.</summary>
    public string? Image { get; init; }
    /// <summary>IXmlParser.Export-style raw XML fragment (debug / export round-trip).</summary>
    public string? RawXml { get; init; }
    /// <summary>Type-specific semantic payload (EncounterSemantics for Encounter).</summary>
    public object? Semantics { get; init; }
    /// <summary>TopBar 审计统计（D10 §3.3：N 字段 · M 有值 · K 未解析，Text 已本地化）。</summary>
    public AuditSummaryDto? Audit { get; init; }
}

// ═══════════ 公共组件 DTO（D10 §二 组件清单的数据形态）═══════════

/// <summary>键值行（ValueGrid / 数值格 / Hero 关键数字行）。</summary>
public sealed class FieldRowDto
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public string? Color { get; init; }
}

/// <summary>StatBar 一个段。</summary>
public sealed class StatSegmentDto
{
    public double Value { get; init; }
    public string Color { get; init; } = "#9E9E9E";
}

/// <summary>
/// 条形图（D04/D05）：mode=stacked 段占比条（总伤害）；mode=centered 相对 Max 的
/// 填充条 + 文本（阵营关系）；mode=bipolar 零中心双向条（Condition 修饰值 / Faction
/// 声望 / BattleMove 属性，正值向右负值向左）。CSS/JS 只按值渲染，语义色由 C# 定。
/// </summary>
public sealed class StatBarDto
{
    public string Mode { get; init; } = "stacked";
    public List<StatSegmentDto> Segments { get; init; } = [];
    /// <summary>centered/bipolar 模式的基准（如 100）；null = 条不渲染只出文本。</summary>
    public double? Max { get; init; }
    /// <summary>行内显示文本（如 "−100 (敌对)"）。</summary>
    public string? Text { get; init; }
    /// <summary>centered 正值的填充色；bipolar 负值色（缺省 #C62828）。</summary>
    public string? PosColor { get; init; }
    public string? NegativeColor { get; init; }
}

/// <summary>
/// 条件徽章（D04/D05 语义色：Fatal 红 / Instant 橙 / Stackable 绿 / 时长蓝；
/// 槽位前缀与 ¬ 否定已拼入 Label）。
/// </summary>
public sealed class ConditionChipDto
{
    public string Label { get; init; } = "";
    public string Bg { get; init; } = "#E3F2FD";
    public string Fg { get; init; } = "#1565C0";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public string? Tooltip { get; init; }
}

/// <summary>战利品树节点（D04 BuildTreasureLootTree 语义：概率 = 权重/Σ权重，嵌套 TT 递归展开）。</summary>
public sealed class LootNodeDto
{
    public string Label { get; init; } = "";
    /// <summary>item | table | unknown。</summary>
    public string Kind { get; init; } = "item";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public double Weight { get; init; }
    public double Prob { get; init; }
    /// <summary>数量区间；"1" 由页面省略。</summary>
    public string? Qty { get; init; }
    public List<LootNodeDto> Children { get; init; } = [];
}

/// <summary>战利品树（池/表）：TT 名徽章（可跳转）+ 条目。</summary>
public sealed class LootTreeDto
{
    public string? Title { get; init; }
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public List<LootNodeDto> Items { get; init; } = [];
}

/// <summary>战利品池（Creature 携带/尸体）：池标签 + 树；未解析池显示灰色 id。</summary>
public sealed class LootPoolDto
{
    public string Label { get; init; } = "";
    public LootTreeDto? Tree { get; init; }
    public string? UnresolvedId { get; init; }
}

/// <summary>TopBar 审计统计（D10 §3.3：N 字段 · M 有值 · K 未解析；Text 预本地化）。</summary>
public sealed class AuditSummaryDto
{
    public int Fields { get; init; }
    public int Filled { get; init; }
    public int Unresolved { get; init; }
    /// <summary>"24 字段 · 12 有值 · 2 未解析"（与 Avalonia RawData 折叠头同文案）。</summary>
    public string? Text { get; init; }
}

/// <summary>反向引用聚合摘要（P1 静态版：类型分组 + 前 N 徽章；过滤/滚动加载 P2 §3.6）。</summary>
public sealed class RefSummaryDto
{
    public List<RefGroupDto> Groups { get; init; } = [];
    public int Total { get; init; }
}

public sealed class RefGroupDto
{
    public string TypeName { get; init; } = "";
    public int Count { get; init; }
    public List<BadgeDto> Items { get; init; } = [];
    public int More { get; init; }
}

// ═══════════ 战斗（D04/D05 三层，ItemType/Creature 共用）═══════════

/// <summary>战斗区块：Σ 总伤害条 → Σ 有效指标 → 逐攻击模式行（可展开）。</summary>
public sealed class CombatDto
{
    public StatBarDto? TotalBar { get; init; }
    /// <summary>"23.4 (×1.25)"。</summary>
    public string? TotalEffective { get; init; }
    /// <summary>Creature 专有：全部拳头时去噪注释。</summary>
    public string? FistsOnlyNote { get; init; }
    public List<AttackModeDto> Modes { get; init; } = [];
}

/// <summary>一个攻击模式行 + 展开详情（D04 BuildAttackModeRow/Expanded 的纯数据版）。</summary>
public sealed class AttackModeDto
{
    public string Name { get; init; } = "";
    public bool Resolved { get; init; } = true;
    public StatBarDto? DamageBar { get; init; }
    /// <summary>"射程 2 · 穿透 1 · 士气 +25% · cueHit"。</summary>
    public string? Meta { get; init; }
    public string? Image { get; init; }
    public string? TypeLabel { get; init; }
    public string? MoraleText { get; init; }
    public string? MoraleColor { get; init; }
    public string? EffectiveText { get; init; }
    public string? FormulaNote { get; init; }
    public List<FieldRowDto> StatCells { get; init; } = [];
    public List<BadgeDto> ChargeBadges { get; init; } = [];
    public List<ConditionChipDto> AttackerConditions { get; init; } = [];
    public string? WieldPhrase { get; init; }
    public List<string> AttackPhrases { get; init; } = [];
    public string? Notes { get; init; }
    public string? Sound { get; init; }
}

// ═══════════ ItemType（D04）═══════════

/// <summary>D04 页面语义：Hero → ⚔ 战斗 | 🧍 装备 → ✨ 效果 | ⏳ 生命周期 → 📦 容器 | 🔗 来源产出。</summary>
public sealed class ItemTypeSemantics
{
    public string Gs { get; init; } = "";
    public string? Description { get; init; }
    public string? IdentifiedLabel { get; init; }
    public string? IdentifiedDesc { get; init; }
    public List<FieldRowDto> HeroStats { get; init; } = [];
    public List<string> GalleryImages { get; init; } = [];
    public CombatDto? Combat { get; init; }
    public EquipmentDto? Equipment { get; init; }
    public List<ConditionGroupDto> ConditionGroups { get; init; } = [];
    public List<BadgeDto> Properties { get; init; } = [];
    public LifecycleDto? Lifecycle { get; init; }
    public ContainerDto? Container { get; init; }
    public AssociationsDto? Associations { get; init; }
    public RefSummaryDto? Refs { get; init; }
}

/// <summary>🧍 装备：槽位徽章 + UseSlots + SocketLocked + 交互音效。</summary>
public sealed class EquipmentDto
{
    public List<BadgeDto> Slots { get; init; } = [];
    public List<BadgeDto> UseSlots { get; init; } = [];
    public bool SocketLocked { get; init; }
    /// <summary>非默认音效（cuePickup,cuePutdown 去噪）。</summary>
    public string? Sound { get; init; }
}

public sealed class ConditionGroupDto
{
    public string Label { get; init; } = "";
    public List<ConditionChipDto> Conditions { get; init; } = [];
}

public sealed class LifecycleDto
{
    public StatBarDto? Durability { get; init; }
    public List<FieldRowDto> LossRates { get; init; } = [];
    public string? Lifespan { get; init; }
    public List<LootTreeDto> BreakParts { get; init; } = [];
    public List<BadgeDto> ChargeProfiles { get; init; } = [];
}

public sealed class ContainerDto
{
    public string? Capacity { get; init; }
    public List<BadgeDto> ContentIds { get; init; } = [];
    public string? Format { get; init; }
}

public sealed class AssociationsDto
{
    public List<BadgeDto> Switches { get; init; } = [];
    public List<LootTreeDto> LootTrees { get; init; } = [];
}

// ═══════════ Creature（D05）═══════════

/// <summary>D05 页面语义：Hero → ⚔ 战斗 | 🧬 属性与出场状态 → 🎁 战利品 | 📍 遭遇。</summary>
public sealed class CreatureSemantics
{
    public string? NamePublic { get; init; }
    public string? Notes { get; init; }
    public List<BadgeDto> HeroBadges { get; init; } = [];
    public CombatDto? Combat { get; init; }
    public StatBarDto? FactionRelation { get; init; }
    public List<FieldRowDto> AttributeCells { get; init; } = [];
    public List<BadgeDto> SpawnStatus { get; init; } = [];
    public List<string> Activities { get; init; } = [];
    public List<LootPoolDto> LootPools { get; init; } = [];
    public List<BadgeDto> EncounterChain { get; init; } = [];
    public List<BadgeDto> AppearsIn { get; init; } = [];
    public List<SpawnPointDto> SpawnPoints { get; init; } = [];
    public RefSummaryDto? Refs { get; init; }
}

/// <summary>刷新点一行（CreatureSource 反查，同点权重归一）。</summary>
public sealed class SpawnPointDto
{
    public string Name { get; init; } = "";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    /// <summary>"(x, y)"。</summary>
    public string Position { get; init; } = "";
    /// <summary>"2–4 只"。</summary>
    public string CountText { get; init; } = "";
    /// <summary>"权重 0.50（占同点 45%）"。</summary>
    public string WeightText { get; init; } = "";
}

// ═══════════ Recipe ═══════════

/// <summary>Recipe 页面语义：Hero → 原料三组 → 产物 → AlsoTry/Hidden。</summary>
public sealed class RecipeSemantics
{
    public string? Type { get; init; }
    public List<string> Flags { get; init; } = [];
    public string? SecretName { get; init; }
    public List<FieldRowDto> HeroStats { get; init; } = [];
    public List<IngredientGroupDto> IngredientGroups { get; init; } = [];
    public LootTreeDto? Product { get; init; }
    public List<BadgeDto> TempProduct { get; init; } = [];
    public List<BadgeDto> AlsoTry { get; init; } = [];
    public List<BadgeDto> Hidden { get; init; } = [];
    public RefSummaryDto? Refs { get; init; }
}

public sealed class IngredientGroupDto
{
    public string Label { get; init; } = "";
    public string Bg { get; init; } = "#FFF3E0";
    public string Fg { get; init; } = "#E65100";
    public List<IngredientDto> Items { get; init; } = [];
}

/// <summary>原料卡：名称 + ×N + Required/Forbidden 属性徽章。</summary>
public sealed class IngredientDto
{
    public string Name { get; init; } = "";
    public bool Resolved { get; init; } = true;
    /// <summary>数量；"1" 由页面省略。</summary>
    public string? Qty { get; init; }
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public List<BadgeDto> Required { get; init; } = [];
    public List<BadgeDto> Forbidden { get; init; } = [];
}

// ═══════════ C 级薄模板（D10 §3.8：模板组合 + 轻量增强）═══════════

/// <summary>薄类型模板语义：Hero + 问题区 Blocks（ValueGrid/徽章/树/文本/条/模式/分组）。</summary>
public sealed class TemplateSemantics
{
    public List<BadgeDto> HeroBadges { get; init; } = [];
    public List<FieldRowDto> HeroStats { get; init; } = [];
    public string? Subtitle { get; init; }
    public List<TemplateBlockDto> Blocks { get; init; } = [];
    public RefSummaryDto? Refs { get; init; }
}

/// <summary>模板区块（P4 全类型铺开：B 级语义聚合 + D 级字段表的统一载体）。</summary>
public sealed class TemplateBlockDto
{
    public string Title { get; init; } = "";
    public string Accent { get; init; } = "#1565C0";
    public List<FieldRowDto> Rows { get; init; } = [];
    public List<StatBarDto> Bars { get; init; } = [];
    public List<BadgeDto> Badges { get; init; } = [];
    public List<TemplateBadgeGroupDto> BadgeGroups { get; init; } = [];
    public List<LootTreeDto> Trees { get; init; } = [];
    /// <summary>单个攻击模式（AttackMode 实体页 / 战斗详情）——渲染为可展开行+详情。</summary>
    public AttackModeDto? Mode { get; init; }
    /// <summary>光照等级 6 时段热力格（HexType，与 Avalonia BuildLightPanel 同款：时段名 + 热力色块内数值）。</summary>
    public List<LightCellDto> LightCells { get; init; } = [];
    public string? Text { get; init; }
}

/// <summary>光照等级单格（从早到晚横排；Bg 为红→黄→绿热力插值，与 Avalonia 同公式）。</summary>
public sealed class LightCellDto
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public string Bg { get; init; } = "#F5F5F5";
    public string Fg { get; init; } = "#333";
}

/// <summary>徽章分组（BattleMove 条件组等：组标题 + 徽章行）。</summary>
public sealed class TemplateBadgeGroupDto
{
    public string Label { get; init; } = "";
    public List<BadgeDto> Badges { get; init; } = [];
}

/// <summary>Colored chip/badge (type chip, semantic badge) — text already localized.</summary>
public sealed class ChipDto
{
    public string Label { get; init; } = "";
    public string Bg { get; init; } = "#F5F5F5";
    public string Fg { get; init; } = "#999";
}

/// <summary>
/// Clickable reference badge — TargetType/TargetId when the reference resolved
/// (Ctrl+Click navigates, Ctrl+RMB peeks); a plain chip when unresolved.
/// </summary>
public sealed class BadgeDto
{
    public string? Icon { get; init; }          // emoji prefix (🛡 / 🛠 / ⚡ …)
    public string Text { get; init; } = "";
    public string Bg { get; init; } = "#F5F5F5";
    public string Fg { get; init; } = "#999";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    /// <summary>Optional hover tooltip text (e.g. condition effect translation).</summary>
    public string? Tooltip { get; init; }
}

/// <summary>One node card's identity — image URL + type chip + display name + id.</summary>
public sealed class NodeDto
{
    public string Type { get; init; } = "Encounter";
    public string Id { get; init; } = "";
    public string? DisplayName { get; init; }
    public string? Image { get; init; }
    public ChipDto TypeChip { get; init; } = new();
    public bool Resolved { get; init; } = true;
    /// <summary>R64: 卡片底部行中间标注（前驱的来路 / 后继的去路）。</summary>
    public string? Annotation { get; init; }
}

/// <summary>Pre-condition chip (D07 §6.2): ✓/✗ under the active filter, ¬ styling.</summary>
public sealed class PreCondChipDto
{
    public string Raw { get; init; } = "";
    public string Label { get; init; } = "";
    public bool IsNeg { get; init; }
    public bool Satisfied { get; init; } = true;
    public string Bg { get; init; } = "#E8F5E9";
    public string Fg { get; init; } = "#2E7D32";
}

/// <summary>
/// One branch (outgoing edge) of the current encounter — D06/D07 merged
/// semantics. EndKind: none = normal card; stay = ⏹ 停留原地; blank = ☰ 无后续.
/// </summary>
public sealed class BranchDto
{
    public int TargetId { get; init; }
    /// <summary>解析成功时的 EntityId（缓存/查找键）；未解析为 null → 页面回退 TargetId。</summary>
    public string? EntityId { get; init; }
    public string? DisplayName { get; init; }
    public string? Image { get; init; }
    public ChipDto TypeChip { get; init; } = new();
    public bool Resolved { get; init; } = true;
    public string EndKind { get; init; } = "none";   // none | stay | blank
    public double Weight { get; init; }
    public double EffectiveProb { get; init; }
    public double? SuccessProb { get; init; }        // D07 §5.1: max p3<1 across items
    public string? Annotation { get; init; }         // R64: 底部行中间标注（去路）
    public List<BadgeDto> ItemBadges { get; init; } = [];
    public List<PreCondChipDto> PreConds { get; init; } = [];
}

/// <summary>Pre-condition filter checkbox offered above the flow view.</summary>
public sealed class PreCondFilterDto
{
    public string RawId { get; init; } = "";
    public string Display { get; init; } = "";
    public bool IsNeg { get; init; }
}

/// <summary>
/// D08 §二: 场景流转主视图 — three rows: predecessors (who leads here) →
/// current (highlighted) → branches (where it goes). The current node is
/// derivable from the snapshot root; JS keeps its own focus state for
/// in-component navigation (R64) and re-fetches the target snapshot.
/// </summary>
public sealed class FlowDto
{
    public List<NodeDto> Predecessors { get; init; } = [];
    public List<BranchDto> Branches { get; init; } = [];
    public List<PreCondFilterDto> PreCondFilters { get; init; } = [];
}

/// <summary>One ✨ effect row: semantic label badge + values (badges and/or text).</summary>
public sealed class EffectRowDto
{
    public ChipDto Label { get; init; } = new();
    public List<BadgeDto> Badges { get; init; } = [];
    public string? Text { get; init; }
    /// <summary>P1: 战利品类效果行的内联可展开树（GiveLoot/LootPool）。</summary>
    public List<LootTreeDto> Trees { get; init; } = [];
}

/// <summary>
/// D08 §五: ✨ 效果区（行为清单，无标题头）— compact rows, one semantic badge per row.
/// Null when the encounter has no effects at all.
/// </summary>
public sealed class EffectsDto
{
    public List<EffectRowDto> Rows { get; init; } = [];
}

/// <summary>D08 §四: 如何进入 — trigger conditions + own pre-conditions + trigger summaries.</summary>
public sealed class EntryDto
{
    public List<BadgeDto> Conditions { get; init; } = [];
    public List<BadgeDto> OwnPreConditions { get; init; } = [];
    public List<TriggerDto> Triggers { get; init; } = [];
}

/// <summary>One EncounterTrigger summary: name badge + 📍/📅/🧱/♻ line (D08 §4.2).</summary>
public sealed class TriggerDto
{
    public string Name { get; init; } = "";
    public string? Summary { get; init; }
}

/// <summary>
/// D08 §一/§二/§五: the full Encounter page semantics. All display strings are
/// pre-localized by the extractor (D09 principle ②: JS renders, C# translates).
/// </summary>
public sealed class EncounterSemantics
{
    public ChipDto TypeChip { get; init; } = new();
    public bool IsEntry { get; init; }
    public bool IsTerminal { get; init; }
    public bool RemoveCreatures { get; init; }
    public bool RemoveUsed { get; init; }
    public double Price { get; init; }
    public double LootChance { get; init; }
    public double AccidentChance { get; init; }
    public double CreatureChance { get; init; }
    /// <summary>Description truncated to 2000 chars (book-style page).</summary>
    public string? Description { get; init; }
    /// <summary>响应格式提示（flow 标题下的小字）。</summary>
    public string? FormatHint { get; init; }
    public FlowDto Flow { get; init; } = new();
    public EffectsDto? Effects { get; init; }
    public EntryDto? Entry { get; init; }
    public RefSummaryDto? Refs { get; init; }
}
