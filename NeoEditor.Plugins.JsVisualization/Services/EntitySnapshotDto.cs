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
}
