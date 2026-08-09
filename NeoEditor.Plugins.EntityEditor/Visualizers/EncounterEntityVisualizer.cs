using System;
using System.Collections.Generic;
using System.Globalization;
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

public class EncounterEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(Encounter);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    public EncounterEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(vis.Resolver, vis.Router, vis.BuildRefTooltip);
        _dataTable = dataTable!;
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not Encounter enc) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(enc));

        // D08 §一/§二: the page is one story-node profile ordered by the modder's
        // decision flow — ① 身份 → ② 场景流转（主视图）→ ③ 如何进入 → ④ 内容与效果 → 被引用.
        var predecessors = FindPredecessors(enc);
        var (branches, _) = PrepareBranches(enc, new HashSet<string>());
        var (isEntry, isTerminal) = DetermineEntryTerminal(predecessors.Count, branches);

        root.Children.Add(BuildHeroHeader(enc, isEntry, isTerminal));
        // R64: 内容与效果放场景流转上方——先看"这个剧情是什么/做了什么"（两栏），
        // 再进入流转上下文（前后场景）。
        if (BuildContentEffectsSection(enc) is { } content) root.Children.Add(content);
        root.Children.Add(BuildFlowView(enc, predecessors, branches));
        if (BuildEntrySection(enc) is { } entry) root.Children.Add(entry);
        root.Children.Add(BuildReverseRefsPanel(enc));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Encounter enc, bool isEntry, bool isTerminal)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };
        var bmp = _vis.LoadImage(enc.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132, CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), VerticalAlignment = VerticalAlignment.Top
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
            // R30 (Doc 21 §10): click the hero image to zoom.
            var capturedBmp = bmp;
            imageArea.Cursor = new Cursor(StandardCursorType.Hand);
            imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(capturedBmp, enc.Subject ?? enc.Name);
        }
        else
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.BookOpen, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {enc.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        _vis.AddModBadge(enc, idRow);
        identity.Children.Add(idRow);

        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        // D06 §4.2: shared type-chip mapping (raw value 0-3, grey fallback).
        var (typeLabel, typeBg, typeFg) = TypeChip((int)enc.Type);
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });
        // D08 §三: entry/terminal topology badges (same source as the flow view —
        // no in-edges → ⛳ 入口; all out-edges terminal → ⏹ 终点; both → plain middle node).
        if (isEntry && !isTerminal)
            infoRow.Children.Add(_vis.MiniBadge(_vis.Loc("Vis.EntryPoint"), "#E8F5E9", "#2E7D32"));
        if (isTerminal && !isEntry)
            infoRow.Children.Add(_vis.MiniBadge(_vis.Loc("Vis.TerminalPoint"), "#ECEFF1", "#546E7A"));
        if (enc.RemoveCreatures)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "RemoveCreatures", FontSize = 10, Foreground = Brush.Parse("#C62828") }
            });
        if (enc.RemoveUsed)
            infoRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#FFEBEE"), Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = "RemoveUsed", FontSize = 10, Foreground = Brush.Parse("#C62828") }
            });
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
        {
            Text = enc.Subject ?? enc.Name, FontSize = 18, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        var chanceRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 2, 0, 0) };
        if (enc.Price != 0)
            chanceRow.Children.Add(new TextBlock
                { Text = $"Price: ${enc.Price:F2}", FontSize = 11, Foreground = Brush.Parse("#666") });
        if (enc.LootChance > 0)
            chanceRow.Children.Add(new TextBlock
                { Text = $"Loot: {enc.LootChance:P0}", FontSize = 11, Foreground = Brush.Parse("#2E7D32") });
        if (enc.AccidentChance > 0)
            chanceRow.Children.Add(new TextBlock
                { Text = $"Accident: {enc.AccidentChance:P0}", FontSize = 11, Foreground = Brush.Parse("#C62828") });
        // R48: creature ambush chance — same scavenge-odds family as loot/accident.
        if (enc.CreatureChance > 0)
            chanceRow.Children.Add(new TextBlock
                { Text = $"Creature: {enc.CreatureChance:P0}", FontSize = 11, Foreground = Brush.Parse("#283593") });
        if (chanceRow.Children.Count > 0) identity.Children.Add(chanceRow);
        Grid.SetColumn(identity, 1);
        grid.Children.Add(identity);

        return _vis.Card(grid);
    }

    /// <summary>
    /// D08 §5.1: book-style description — 96px thumbnail (strImg, click to zoom)
    /// on the left, description (2000-char truncation) on the right. Hidden when
    /// the description is empty.
    /// </summary>
    private Control? BuildStoryPagePanel(Encounter enc)
    {
        if (string.IsNullOrWhiteSpace(enc.Description)) return null;

        // R64: 内容只放描述——图片已在 Hero（记忆锚点），这里不重复；
        // 文本 Wrap 收进 Card（不设 MaxWidth，随 Card 列宽自适应，不会超出）。
        var desc = enc.Description.Length > 2000 ? enc.Description[..2000] + "..." : enc.Description;
        return _vis.Card(new TextBlock
        {
            Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333")
        });
    }

    // One item parsed from the left side of a response segment (D07 §四/§五).
    private sealed record ParsedItem(
        string? ItemId, double ItemMult, ItemType? Item, Ingredient? Ing,
        bool DestroyOnUse, double SuccessProb);

    // Response entry: optional item prefix (possibly multiple AND items) + target encounter
    private sealed record ResponseEntry(
        List<ParsedItem> Items,
        int TargetId, double Weight, double Probability, Encounter? TargetEncounter);

    private List<ResponseEntry> ParseResponseEntries(string raw, Encounter sourceEnc,
        Dictionary<string, ItemType>? precomputedItemTypes = null)
    {
        var result = new List<ResponseEntry>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        // Build itemTypes lookup for G.S composite item prefix resolution
        var itemTypes = precomputedItemTypes;
        if (itemTypes is null)
        {
            try
            {
                itemTypes = _dataTable?.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", sourceEnc.ModId);
            }
            catch { /* ignore if not available */ }
        }

        // Format (Doc 37 §5.1): [itemId]x[mult]+[itemId2]x[mult]=[encId]x[p1]x[p2]x[p3]x[p4]
        //   or just: =[encId]x[p1]x[p2]x[p3]x[p4]  (no item needed = default response)
        //   left side: 'G.S' composite → ItemType; pure number → Ingredient nID
        //     (ItemType pk fallback); '+' joins items of ONE segment = AND (must own all);
        //   right side: p1 = weight (probability = weight / Σweights, D06),
        //     p2 = destroy-on-use flag, p3 = success probability
        //     (0 = padding/no info → treated as 1.0 default, D07 §五), p4 unused.
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
                // No '=' => treat whole thing as encounter reference for backward compat
                var parts = s.Split('x');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out targetId)) continue;
                weight = double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p1) ? p1 : 1.0;
            }
            else
            {
                // Parse optional item prefix (before '=') — D07 §六: '+' joins AND items
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
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var im) ? im : 1.0;
                        parsedItems.Add(ResolveResponseItem(sourceEnc, id, mult, itemTypes));
                    }
                }

                // Parse encounter suffix (after '='): encId x p1(weight) x p2 x p3 x p4
                var encPart = s[(eqIdx + 1)..].Trim();
                var encParts = encPart.Split('x');
                if (encParts.Length < 2) continue;
                if (!int.TryParse(encParts[0], out targetId)) continue;
                // encParts[1] is the weight (not direct probability)
                weight = double.TryParse(encParts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p2) ? p2 : 1.0;
                // D07 §五 (Doc 37 §5.1): p2 = destroy-on-use (1); p3 = success probability
                //   (0 = padding/no info → keep default 1.0 so nothing renders)
                destroyOnUse = encParts.Length >= 3 && encParts[2] == "1";
                if (encParts.Length >= 4 && double.TryParse(encParts[3],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p3) && p3 > 0)
                    successProb = p3;
            }

            // Segment-level p2/p3 apply to every item of the segment; an empty
            // segment keeps one null placeholder (default response, D06).
            var items = parsedItems
                .Select(pi => new ParsedItem(pi.ItemId, pi.ItemMult, pi.Item, pi.Ing, destroyOnUse, successProb))
                .ToList();
            if (items.Count == 0)
                items.Add(new ParsedItem(null, 1.0, null, null, destroyOnUse, successProb));

            totalWeight += weight;
            Encounter? targetEnc =
                _vis.Resolver.LookupRef<Encounter>(sourceEnc, nameof(Encounter.Responses),
                    targetId.ToString());

            rawEntries.Add((items, targetId, weight, targetEnc));
        }

        // Calculate probability from weights
        foreach (var (items, targetId, weight, targetEnc) in rawEntries)
        {
            var prob = totalWeight > 0 ? weight / totalWeight : 1.0 / rawEntries.Count;
            result.Add(new ResponseEntry(items, targetId, weight, prob, targetEnc));
        }

        return result;
    }

    /// <summary>
    /// D07 §四 (Doc 37 §5.1): resolve one left-side item of a response segment —
    /// "G.S" composite → ItemType; pure number → Ingredient nID first (52=撬棍…),
    /// ItemType primary-key fallback; neither resolves → unresolved (grey "Item #id").
    /// </summary>
    private (string? ItemId, double ItemMult, ItemType? Item, Ingredient? Ing) ResolveResponseItem(
        Encounter sourceEnc, string id, double mult, Dictionary<string, ItemType> itemTypes)
    {
        ItemType? item = null;
        Ingredient? ing = null;
        if (id.Length > 0 && !int.TryParse(id, out _))
        {
            // "90.3" style composite → ItemType
            if (itemTypes.TryGetValue(id, out var found))
                item = found;
        }
        else if (id.Length > 0)
        {
            // pure number → Ingredient nID first, ItemType pk fallback
            ing = _vis.Resolver.LookupRef<Ingredient>(sourceEnc, nameof(Encounter.Responses), id);
            if (ing is null)
                item = _vis.Resolver.LookupRef<ItemType>(sourceEnc, nameof(Encounter.Responses), id);
        }
        return (id.Length > 0 ? id : null, mult, item, ing);
    }

    // ═══════════════ Story Branch Diagram (D06: single node card + shared data model) ═══════════════

    /// <summary>
    /// D06 §4.5: the single branch data model — both the node cards and the
    /// Mermaid source are generated from it, so the two renderings cannot drift.
    /// <c>EffectiveProb == 0</c> means the branch is filtered out by the active
    /// pre-condition checkboxes. <c>PreConds</c> is the target encounter's
    /// precondition list resolved for display (Raw preserves the ¬ prefix).
    /// </summary>
    /// <summary>
    /// D07 §3.1: end-of-story semantics of a response target — derived purely from
    /// data (TargetId vs the current Encounter.Id), rendered as a capsule, never a card.
    /// </summary>
    internal enum BranchEndKind
    {
        None,   // normal branch (points at a real Encounter)
        Stay,   // self-reference (TargetId == current Id) → 「⏹ 停留原地」
        Blank   // points at id=1 (Blank empty story) → 「☰ 无后续」
    }

    /// <summary>
    /// One item trigger of a branch: raw id, multiplier, resolved ItemType and/or
    /// Ingredient (D07 §四: pure-numeric ids are Ingredient nIDs first, ItemType pk
    /// fallback), p2 destroy flag and p3 success probability (D07 §五). IsAnd marks
    /// items joined by "+" within one response segment (D07 §六: must own ALL).
    /// </summary>
    internal sealed record BranchItem(
        string? ItemId,
        double ItemMult,
        ItemType? Item,
        Ingredient? Ing = null,
        bool DestroyOnUse = false,
        double SuccessProb = 1.0,
        bool IsAnd = false);

    internal sealed record BranchData(
        int TargetId,
        Encounter? Target,
        List<BranchItem> Items,              // multiple response segments may target the same Encounter
        double Weight,                       // summed weight of all segments for this target
        double EffectiveProb,
        bool IsSatisfied,
        List<(string Raw, bool IsNeg, Condition? Resolved)> PreConds,
        BranchEndKind EndKind = BranchEndKind.None,  // D07 §3.1
        double? SuccessProb = null);                 // D07 §5.1: max p3<1 across items; null when all 1.0

    /// <summary>
    /// D06 §四 (revised): node-card options. The slim card = 52px image +
    /// title + probability pill (+ ID/type chips); complex info (description,
    /// pre-conditions, item) lives in the hover tooltip, never in the layout.
    /// </summary>
    private sealed record NodeCardOptions(
        bool IsCurrent = false,
        double Weight = 0,
        double EffectiveProb = 0,
        bool Filtered = false,
        bool Resolved = true,
        BranchData? Branch = null,
        ISet<string>? ActivePreConds = null,
        Encounter? Source = null,
        bool NoProbabilityPill = false, // D08 §二: predecessor cards carry no probability pill
        string? Annotation = null);     // R64: 底部行中间标注（前驱的来路 / 后继的去路）

    /// <summary>
    /// D06 §4.5: derive the branch model from the parsed responses (pure over
    /// <see cref="ParseResponseEntries"/> output). ValidTotalWeight sums only the
    /// branches whose pre-conditions are satisfied under <paramref name="activePreConds"/>.
    /// D07 §3.1: end-of-story semantics (Stay/Blank) is resolved here — pure data,
    /// no UI.
    /// </summary>
    internal (List<BranchData> Branches, double ValidTotalWeight) PrepareBranches(Encounter enc, ISet<string> activePreConds)
    {
        var responses = ParseResponseEntries(enc.Responses, enc);
        var (branches, validTotal) = PrepareBranches(responses, activePreConds);
        // D07 §3.1: self-reference wins over Blank — viewing id=1 (Blank itself)
        // still shows a normal story (Stay, not Blank).
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

    /// <summary>
    /// D07 §3.1: pure end-of-story predicate — target == current → Stay;
    /// target == 1 (Blank) while viewing anything else → Blank; else None.
    /// </summary>
    internal static BranchEndKind DetermineEndKind(int targetId, int currentId)
    {
        if (targetId == currentId) return BranchEndKind.Stay;
        if (targetId == 1 && currentId != 1) return BranchEndKind.Blank;
        return BranchEndKind.None;
    }

    /// <summary>
    /// D08 §三: entry/terminal topology of an Encounter in the story graph —
    /// no incoming story edge → entry; every outgoing branch is an end
    /// (Stay/Blank, incl. no branches at all) → terminal. Pure data, no UI.
    /// Same source feeds the Hero badges and the flow-view ⛳/⏹ markers.
    /// </summary>
    internal static (bool IsEntry, bool IsTerminal) DetermineEntryTerminal(
        int inDegree, IReadOnlyList<BranchData> branches)
    {
        var isEntry = inDegree == 0;
        var isTerminal = branches.All(b => b.EndKind != BranchEndKind.None);
        return (isEntry, isTerminal);
    }

    /// <summary>
    /// D08 §二: predecessors = Encounters whose aResponses point at this one.
    /// aResponses has no [ReferenceField] so it is not part of the reference
    /// index — the Encounter collection is scanned and each Responses field is
    /// parsed with the same ParseResponseEntries used by the branch diagram.
    /// One entry per source Encounter (segments merged); <c>ItemDesc</c> translates
    /// the incoming segment semantics (🛡 item ×n / 🛠 ingredient / 需同时拥有 A+B);
    /// <c>Weight</c> = summed segment weights.
    /// </summary>
    internal List<(Encounter Source, string? ItemDesc, double Weight)> FindPredecessors(Encounter enc)
    {
        var result = new List<(Encounter Source, string? ItemDesc, double Weight)>();
        if (!(_dataTable?.ReferenceLookups ?? []).TryGetValue(typeof(Encounter), out var list) || list is null)
            return result;

        // One composite-key dictionary for the whole scan (2264 encounters would
        // otherwise rebuild it per parse).
        Dictionary<string, ItemType>? itemTypes = null;
        var merged = new Dictionary<string, (Encounter Source, List<string> Descs, double Weight)>();
        var order = new List<string>();
        foreach (var obj in list)
        {
            if (obj is not Encounter src || src.EntityId == enc.EntityId) continue;
            itemTypes ??= BuildItemTypes(src.ModId);
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

    private Dictionary<string, ItemType>? BuildItemTypes(int sourceModId)
    {
        try
        {
            return _dataTable?.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", sourceModId);
        }
        catch { return null; }
    }

    /// <summary>
    /// D08 §二: translate one incoming response segment's item semantics —
    /// one item → 🛡/🛠 label (×qty always, mirroring the raw syntax the UI
    /// translates: 90.1x1 → 🛡 撬棍 ×1); '+' joined items (one segment) →
    /// 需同时拥有：A + B (D07 §六, ×1 省略以免噪音).
    /// </summary>
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
        return $"{_vis.Loc("Vis.RequireAll")}{string.Join(" + ", labels)}";
    }

    /// <summary>D07-style item label: 🛡 ItemType / 🛠 Ingredient / grey Item #id + （消耗）.</summary>
    private string ItemLabel(ItemType? item, Ingredient? ing, string? itemId, double mult, bool destroyOnUse,
        bool forceQty = false)
    {
        var qty = forceQty || mult > 1 ? $" ×{mult.ToString("0.##", CultureInfo.InvariantCulture)}" : "";
        var consumed = destroyOnUse ? _vis.Loc("Vis.Consumed") : "";
        if (ing is not null) return $"🛠 {ing.Name}{qty}{consumed}";
        if (item is not null) return $"🛡 {item.Name}{qty}{consumed}";
        return itemId is not null ? $"Item #{itemId}{qty}{consumed}" : "";
    }

    /// <summary>
    /// R64: 后继卡底部行中间的去路标注——各触发段物品（同段 AND 用 需同时拥有，
    /// 多段用 ｜ 分隔）。无物品则返回 null（卡片不显示标注行）。
    /// </summary>
    private string? BranchAnnotation(BranchData b)
    {
        var parts = new List<string>();
        foreach (var item in b.Items)
        {
            // 单物品段 → 物品标签；无物品段（=开头）→ 跳过（默认选项无标注）
            var items = new List<ParsedItem>();
            if (item.Item is not null || item.Ing is not null || item.ItemId is not null)
                items.Add(new ParsedItem(item.ItemId, item.ItemMult, item.Item, item.Ing,
                    item.DestroyOnUse, item.SuccessProb));
            var lbl = DescribeSegmentItems(items);
            if (lbl is not null) parts.Add(lbl);
        }
        return parts.Count > 0 ? string.Join(" ｜ ", parts) : null;
    }

    private (List<BranchData> Branches, double ValidTotalWeight) PrepareBranches(
        List<ResponseEntry> responses, ISet<string> activePreConds)
    {
        // Merge response segments that target the SAME Encounter into one branch
        // (real data has 31 such cases, e.g. "91.4x1=941x1x0x0x0,103.8x1=941x1x0x0x0"
        // — several items can trigger the same encounter). The branch card is
        // rendered once per target; its tooltip lists every trigger item.
        var merged = new Dictionary<int, (Encounter? Target, List<BranchItem> Items, double Weight)>();
        var order = new List<int>();
        foreach (var resp in responses)
        {
            if (!merged.TryGetValue(resp.TargetId, out var m))
            {
                m = (resp.TargetEncounter, new List<BranchItem>(), 0);
                order.Add(resp.TargetId);
            }
            // D07 §六: items of ONE segment joined by '+' are AND (IsAnd = true
            // except the first); separate segments stay parallel ("any of").
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
            // D07 §5.1: max p3 < 1 across the branch's items (all 1.0 → null → nothing rendered)
            double? successProb = null;
            foreach (var item in b.Items)
                if (item.SuccessProb < 1.0)
                    successProb = successProb is null ? item.SuccessProb : Math.Max(successProb.Value, item.SuccessProb);
            branches[i] = b with { EffectiveProb = effective, SuccessProb = successProb };
        }
        return (branches, validTotalWeight);
    }

    /// <summary>Resolve the target's precondition list (¬ prefix preserved in Raw).</summary>
    private List<(string Raw, bool IsNeg, Condition? Resolved)> ResolvePreConds(Encounter? target)
    {
        var result = new List<(string Raw, bool IsNeg, Condition? Resolved)>();
        if (target is null || target.PreConditions.Count == 0) return result;
        foreach (var raw in target.PreConditions.Select(e => e.ToRawString()).Where(s => s.Length > 0))
        {
            var isNeg = raw.StartsWith("-");
            var cond = _vis.Resolver.LookupRef<Condition>(target, nameof(Encounter.PreConditions), raw);
            result.Add((raw, isNeg, cond));
        }
        return result;
    }

    /// <summary>
    /// Y/N polarity: a positive precondition "5" is satisfied when the checkbox IS
    /// checked (player has the condition); a negated one "-5" when it is NOT checked.
    /// No active filter → everything is considered satisfied (existing semantics).
    /// </summary>
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

    /// <summary>
    /// Percent without the culture-dependent "50 %" space: the "P2" format inserts a
    /// space between the digits and the % sign in several cultures; the custom
    /// "0.##%" format does not (CreatureVisualizerTests lesson).
    /// </summary>
    internal static string FormatProbability(double p)
        => Math.Clamp(p, 0.0, 1.0).ToString("0.##%", CultureInfo.InvariantCulture);

    /// <summary>
    /// D06 §4.2: shared type-chip mapping (Hero / branch card / chain tree).
    /// EncounterType only models Normal/Scavenge; raw values 2/3 render by their
    /// integer value (field_descriptions.json: 0=剧情 1=搜刮 2=战斗 3=破解).
    /// </summary>
    private (string Label, string Bg, string Fg) TypeChip(int rawType) => rawType switch
    {
        0 => (_vis.Loc("Vis.TypeStory"), "#E3F2FD", "#1565C0"),
        1 => (_vis.Loc("Vis.TypeScavenge"), "#FFF3E0", "#E65100"),
        2 => (_vis.Loc("Vis.TypeCombat"), "#FFEBEE", "#C62828"),
        3 => (_vis.Loc("Vis.TypeHack"), "#F3E5F5", "#6A1B9A"),
        _ => (_vis.Loc("Vis.TypeUnknown", rawType), "#F5F5F5", "#999"),
    };

    /// <summary>
    /// D06 §四 (revised): the shared node component — one Encounter = one card.
    /// Slim card: 52px thumbnail + title + probability pill (+ ID/type chips).
    /// Branch cards carry navigation (Ctrl+Click / Ctrl+RMB peek) and a hover
    /// info tooltip (description / pre-conditions / item); the current card is
    /// highlighted and never navigates.
    /// </summary>
    private Control BuildEncounterNodeCard(Encounter e, NodeCardOptions opts)
    {
        var card = new Border
        {
            Width = 240,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Background = Brush.Parse(opts.IsCurrent ? "#E3F2FD" : "#FAFAFA"),
            BorderBrush = Brush.Parse(opts.IsCurrent ? "#1565C0" : "#E0E0E0"),
            BorderThickness = new Thickness(opts.IsCurrent ? 2 : 1),
            Opacity = opts.Filtered ? 0.5 : 1.0,
            Child = new StackPanel { Spacing = 6 }
        };
        var body = (StackPanel)card.Child;

        // R59 v2: title first (row 1), image second as the main body (~70% of card
        // width), then a chips row (ID left/center, probability right).
        // R64: 类型 badge（剧情/搜刮…）移到标题左边——每个节点都有的属性，放标题旁
        // 一眼表明"这是什么类型的场景"。
        var titleRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        if (opts.Resolved)
        {
            var (typeLabel, typeBg, typeFg) = TypeChip((int)e.Type);
            titleRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(5, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = typeLabel, FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg)
                }
            });
        }
        if (opts.IsCurrent)
            titleRow.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.CurrentEncounter"), FontSize = 8, Foreground = Brush.Parse("#1565C0"),
                VerticalAlignment = VerticalAlignment.Center
            });
        titleRow.Children.Add(new TextBlock
        {
            Text = e.Subject ?? $"Enc #{e.Id}", FontSize = 12, FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#333"), TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(titleRow);

        var bmp = _vis.LoadImage(e.Image);
        var imageArea = new Border
        {
            Width = 168, Height = 110, CornerRadius = new CornerRadius(6), ClipToBounds = true,
            Background = Brush.Parse("#0A000000"), HorizontalAlignment = HorizontalAlignment.Center
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 168, Height = 110 };
            var capturedBmp = bmp;
            imageArea.Cursor = new Cursor(StandardCursorType.Hand);
            imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(capturedBmp, e.Subject ?? e.Name);
        }
        else
            imageArea.Child = new SymbolIcon
            {
                Symbol = Symbol.BookOpen, FontSize = 24, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        body.Children.Add(imageArea);

        // Row 3: chips (ID) | probability (right)  —— R64: 类型已移到标题左边
        var chipsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        chipsRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(5, 1),
            Child = new TextBlock
            {
                Text = $"ID: {e.Id}", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });

        // Probability pill (branch cards only) — right side of the chips row
        if (!opts.IsCurrent && !opts.NoProbabilityPill)
        {
            var probColor = opts.EffectiveProb >= 0.5 ? "#2E7D32" : opts.EffectiveProb >= 0.1 ? "#E65100" : "#999";
            var probPill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Brush.Parse(probColor),
                Padding = new Thickness(8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"{opts.Weight.ToString("F1", CultureInfo.InvariantCulture)}({FormatProbability(opts.EffectiveProb)})",
                    FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brushes.White
                }
            };
            var bottomGrid = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star), new(GridLength.Auto) }, Margin = new Thickness(0, 2, 0, 0) };
            Grid.SetColumn(chipsRow, 0);
            bottomGrid.Children.Add(chipsRow);
            Grid.SetColumn(probPill, 1);
            bottomGrid.Children.Add(probPill);
            body.Children.Add(bottomGrid);
        }
        else
        {
            body.Children.Add(chipsRow);
        }

        // R64: 底部行中间标注（前驱的来路 / 后继的去路）——进入/离开场景需要的东西
        // 放节点内部底部居中，不放在卡片外部（去掉外部箭头文本）。
        if (!string.IsNullOrWhiteSpace(opts.Annotation))
        {
            body.Children.Add(new TextBlock
            {
                Text = opts.Annotation, FontSize = 9, Foreground = Brush.Parse("#00695C"),
                HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        // Branch cards: navigation + hover info tooltip (complex info lives here, not in the card)
        if (opts.Branch is { } branch)
        {
            if (branch.Target is not null)
                _refNode.WireNavigation(card, typeof(Encounter), branch.Target.EntityId, opts.Source, branch.Target);
            ToolTip.SetTip(card, BuildBranchTooltip(branch, opts.Source ?? e, opts.ActivePreConds ?? new HashSet<string>()));
        }

        return card;
    }

    /// <summary>
    /// D06 §四 (revised): branch hover info card — description (truncated ~200 chars),
    /// pre-condition satisfaction under the current filter (✓/✗, ¬ styling),
    /// item trigger info and the probability.
    /// </summary>
    private Control BuildBranchTooltip(BranchData b, Encounter source, ISet<string> activePreConds)
    {
        var sp = new StackPanel { Spacing = 4, MaxWidth = 280 };
        sp.Children.Add(new TextBlock
        {
            Text = b.Target?.Subject ?? $"Enc #{b.TargetId}",
            FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#333"), TextWrapping = TextWrapping.Wrap
        });

        // R59 v2: item triggers go right after the title (row 2), using Name
        // (not Description). R59 v4: one badge per trigger item — several
        // response segments can target the same Encounter. D07 §六: items of
        // one segment joined by '+' are AND → "需同时拥有：" prefix + "+" connectors.
        AddItemBadges(sp, source, b.Items);

        // D07 §5.2: success probability (p3 < 1) — the hack-scene core mechanic
        if (b.SuccessProb is { } sp3)
        {
            sp.Children.Add(new TextBlock
            {
                Text = $"⚡ {_vis.Loc("Vis.SuccessProb")} {FormatProbability(sp3)}",
                FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#E65100")
            });
        }

        if (b.Target is not null && !string.IsNullOrWhiteSpace(b.Target.Description))
        {
            var desc = b.Target.Description.Length > 200 ? b.Target.Description[..200] + "…" : b.Target.Description;
            sp.Children.Add(new TextBlock
            {
                Text = desc, FontSize = 10, Foreground = Brush.Parse("#555"), TextWrapping = TextWrapping.Wrap
            });
        }

        if (b.PreConds.Count > 0)
        {
            sp.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.PreConditions"), FontSize = 9, Foreground = Brush.Parse("#999"),
                FontWeight = FontWeight.SemiBold
            });
            var wp = new WrapPanel();
            foreach (var (raw, isNeg, cond) in b.PreConds)
            {
                var satisfied = IsPreCondSatisfied(raw, activePreConds);
                var label = (isNeg ? "¬" : "") + (cond?.Subject ?? raw);
                var bg = satisfied ? "#E8F5E9" : "#FFEBEE";
                var fg = satisfied ? "#2E7D32" : "#C62828";
                wp.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Background = Brush.Parse(bg),
                    Padding = new Thickness(4, 1),
                    Margin = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = $"{(satisfied ? "✓ " : "✗ ")}{label}", FontSize = 9, Foreground = Brush.Parse(fg),
                        TextDecorations = isNeg ? TextDecorations.Strikethrough : null
                    }
                });
            }
            sp.Children.Add(wp);
        }

        sp.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.Probability")}: {b.Weight.ToString("F1", CultureInfo.InvariantCulture)}({FormatProbability(b.EffectiveProb)})",
            FontSize = 10, Foreground = Brush.Parse("#555")
        });

        return new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8), Child = sp
        };
    }

    /// <summary>
    /// D07 §四/§五/§六: item trigger badges — Ingredient (🛠, tool palette
    /// #E8EAF6/#283593) vs ItemType (🛡 #E3F2FD/#1565C0); p2=1 appends （消耗）.
    /// AND groups (IsAnd items following the group head) render as
    /// 「需同时拥有：A + B」; separate segments stay parallel badges ("any of").
    /// </summary>
    private void AddItemBadges(StackPanel sp, Encounter source, List<BranchItem> items)
    {
        // Group consecutive items: an IsAnd item belongs to the previous group.
        var groups = new List<List<BranchItem>>();
        foreach (var item in items)
        {
            if (item.IsAnd && groups.Count > 0)
                groups[^1].Add(item);
            else
                groups.Add(new List<BranchItem> { item });
        }

        foreach (var group in groups)
        {
            var badges = group.Select(i => BuildItemBadge(source, i))
                .Where(b => b is not null).Cast<Control>().ToList();
            if (badges.Count == 0) continue;
            if (badges.Count == 1)
            {
                sp.Children.Add(badges[0]);
                continue;
            }
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            row.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.RequireAll"),
                FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#555"),
                VerticalAlignment = VerticalAlignment.Center
            });
            for (int i = 0; i < badges.Count; i++)
            {
                if (i > 0)
                    row.Children.Add(new TextBlock
                    {
                        Text = "+", FontSize = 10, Foreground = Brush.Parse("#888"),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                row.Children.Add(badges[i]);
            }
            sp.Children.Add(row);
        }
    }

    /// <summary>
    /// One trigger-item badge: 🛠 Ingredient (tool palette) / 🛡 ItemType /
    /// grey "Item #id" fallback; p2=1 appends （消耗）(D07 §5.2). Null placeholder
    /// (default response, no item) renders nothing.
    /// </summary>
    private Control? BuildItemBadge(Encounter source, BranchItem item)
    {
        if (item.Ing is null && item.Item is null && item.ItemId is null) return null;
        var qty = item.ItemMult > 1 ? $" ×{item.ItemMult}" : "";
        var consumed = item.DestroyOnUse ? _vis.Loc("Vis.Consumed") : "";
        if (item.Ing is not null)
            return _refNode.BadgeForEntity(source, item.Ing,
                $"🛠 {item.Ing.Name}{qty}{consumed}", "#E8EAF6", "#283593");
        if (item.Item is not null)
            return _refNode.BadgeForEntity(source, item.Item,
                $"🛡 {item.Item.Name}{qty}{consumed}", "#E3F2FD", "#1565C0");
        return _vis.MiniBadge($"Item #{item.ItemId}{consumed}", "#F5F5F5", "#999");
    }

    /// <summary>
    /// D07 §3.2: end-of-story marker — a grey rounded capsule (never a card):
    /// 「⏹ 停留」/「☰ 无后续」+ effective probability. No image, no title card,
    /// no navigation; hover shows weight/probability only (§3.3).
    /// </summary>
    private Control BuildEndCapsule(BranchData b)
    {
        var isStay = b.EndKind == BranchEndKind.Stay;
        var capsule = _vis.MiniBadge(isStay ? _vis.Loc("Vis.StayEnd") : _vis.Loc("Vis.BlankEnd"),
            "#ECEFF1", "#546E7A");
        ToolTip.SetTip(capsule, BuildEndTooltip(b));
        var row = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(capsule);
        row.Children.Add(new TextBlock
        {
            Text = FormatProbability(b.EffectiveProb),
            FontSize = 10, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#546E7A"),
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    /// <summary>D07 §3.3: end-branch hover info — weight / effective probability only.</summary>
    private Control BuildEndTooltip(BranchData b)
    {
        var sp = new StackPanel { Spacing = 4, MaxWidth = 240 };
        sp.Children.Add(new TextBlock
        {
            Text = b.EndKind == BranchEndKind.Stay ? _vis.Loc("Vis.StayEnd") : _vis.Loc("Vis.BlankEnd"),
            FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#333")
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Vis.Probability")}: {b.Weight.ToString("F1", CultureInfo.InvariantCulture)}({FormatProbability(b.EffectiveProb)})",
            FontSize = 10, Foreground = Brush.Parse("#555")
        });
        return new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8), Child = sp
        };
    }

    /// <summary>
    /// D08 §二 (v1.1/v1.2): 场景流转主视图 — the one citizen view of an Encounter
    /// page. Horizontal three segments: 前驱层（谁通向这里）→ 当前卡 → 后继层
    /// (D06/D07 branch cards unchanged). Complex aResponses syntax is translated
    /// into intuitive edge annotations (🛡 物品 ×1 → / 需同时拥有：A + B / 权重).
    /// v1.2: predecessor & successor cards are fully wired via
    /// <see cref="RefNode.WireNavigation"/> (Ctrl+LMB navigate / Ctrl+RMB peek);
    /// the section header carries the「⏎ 回到当前」anchor button.
    /// The TabControl structure is preserved: 场景流转 | 剧情链 | Mermaid源码.
    /// </summary>
    private Control BuildFlowView(Encounter enc,
        List<(Encounter Source, string? ItemDesc, double Weight)> predecessors,
        List<BranchData> initialBranches)
    {
        var sp = new StackPanel { Spacing = 6 };

        // R64: 焦点场景（初始 = 页面实体；左键点击前驱/后继卡切换，重算其前后文）。
        // 原设计假设只能在页面间跳转，用户澄清意图 = 流转组件内 navigation，
        // 「回到当前」= 焦点复位到最初查看的场景。
        var current = enc;
        var selectedPreConds = new HashSet<string>();

        // ── 可重建容器（RebuildFlow 清空后重算前后文）──────────────────────
        // R64: 三行布局——前驱层一行（横向排开）、当前场景第二行（居中）、
        // 后继层第三行（横向排开）。每层内部横向一字排开（横向 ScrollViewer
        // 滚动查看），而不是把前驱/当前/后继全部塞进同一行（那样更放不全）。
        var predCol = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
        };
        var currentHolder = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
        };
        var branchesPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
        };
        var preCondPanel = new StackPanel();
        // ── 三行容器 + 滚动视图（提前声明：backBtn.Click / RebuildFlow /
        //    CenterOnCurrent 都要引用，C# 不允许局部函数引用声明在其后的变量）──
        var flowRow = new StackPanel
        {
            Orientation = Orientation.Vertical, Spacing = 12, VerticalAlignment = VerticalAlignment.Center
        };
        // 行1：前驱层（横向）→ 行2：当前场景（居中）→ 行3：后继层（横向）
        flowRow.Children.Add(predCol);
        flowRow.Children.Add(currentHolder);
        flowRow.Children.Add(branchesPanel);
        var flowScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            // R64: 三行布局需要足够高度（前驱行/当前行/后继行各 ~170px + 间距），
            // 500 只能看到 2.5 行 → 拉高到 900 完整展示三行。
            MaxHeight = 900,
            Content = _vis.Card(flowRow)
        };
        // R64: 焦点切换（左键点击/回到当前）后把当前场景卡滚动居中——
        // 前驱层/后继层可能很长，切换后当前卡会滚出视野。
        // 延迟到布局完成（Dispatcher.Post）后才能拿到 Bounds/Viewport。
        void CenterOnCurrent()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (currentHolder.Bounds.Width <= 0 || flowScroll.Viewport.Width <= 0) return;
                var targetX = currentHolder.Bounds.X + currentHolder.Bounds.Width / 2
                              - flowScroll.Viewport.Width / 2;
                flowScroll.Offset = new Vector(Math.Max(0, targetX), flowScroll.Offset.Y);
            });
        }

        // ── 节标题 + 「⏎ 回到当前」按钮（R64：组件内焦点复位，不再是页面跳转）──
        var headerRow = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star), new(GridLength.Auto) } };
        var header = _vis.SectionHeader(_vis.Loc("Vis.FlowView"), Symbol.Map, accent: "#00695C");
        Grid.SetColumn(header, 0);
        headerRow.Children.Add(header);
        var backBtn = new Button
        {
            Content = _vis.Loc("Vis.BackToCurrent"), FontSize = 9, Padding = new Thickness(8, 2), MinHeight = 0,
            Background = Brush.Parse("#E3F2FD"), Foreground = Brush.Parse("#1565C0"),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center, Cursor = new Cursor(StandardCursorType.Hand)
        };
        backBtn.Click += (_, _) => { current = enc; RebuildFlow(); };
        Grid.SetColumn(backBtn, 1);
        headerRow.Children.Add(backBtn);
        sp.Children.Add(headerRow);

        // Response format hint — kept from the merged ResponsesPanel (D06 §六)
        sp.Children.Add(new TextBlock
        {
            Text = "格式: [物品ID]x[数量]=[剧情ID]x[权重]  ·  空物品(=开头)=无需物品的选项  ·  概率=权重/权重和",
            FontSize = 9, Foreground = Brush.Parse("#AAA"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, -4, 0, 4)
        });

        // ── 重建整个流转视图（焦点切换 / 过滤刷新 / 回到当前共用）────────────
        void RebuildFlow()
        {
            predCol.Children.Clear();
            currentHolder.Children.Clear();
            branchesPanel.Children.Clear();
            preCondPanel.Children.Clear();

            var preds = FindPredecessors(current);
            var (branches, _) = PrepareBranches(current, selectedPreConds);

            // ── 前驱层（左）：谁通向这里（D08 §二）──────────────────────────
            if (preds.Count == 0)
            {
                // 无入边 → ⛳ 入口 徽章（与 Hero 入口标记同源）
                predCol.Children.Add(_vis.MiniBadge(_vis.Loc("Vis.EntryPoint"), "#E8F5E9", "#2E7D32"));
            }
            else
            {
                foreach (var (src, itemDesc, weight) in preds)
                {
                    // R64: 无外部箭头/标注——来路作为 Annotation 放卡片底部行中间
                    // （布局已暗示流向：前驱行 → 当前行 → 后继行）。
                    var ann = itemDesc is not null
                        ? itemDesc
                        : $"{weight.ToString("F1", CultureInfo.InvariantCulture)}";
                    var predCard = BuildEncounterNodeCard(src, new NodeCardOptions(
                        IsCurrent: false, Weight: weight, EffectiveProb: 0,
                        Resolved: true, Source: current, NoProbabilityPill: true,
                        Annotation: ann));
                    // Ctrl+LMB 跳转 / Ctrl+RMB Peek（全编辑器引用导航）
                    _refNode.WireNavigation(predCard, typeof(Encounter), src.EntityId, current, src);
                    // R64: 左键点击 → 组件内焦点切换（加载该场景及其前后文）
                    var capturedSrc = src;
                    predCard.PointerPressed += (_, e) =>
                    {
                        if ((e.KeyModifiers & KeyModifiers.Control) != 0) return; // Ctrl = WireNavigation
                        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                        {
                            current = capturedSrc;
                            RebuildFlow();
                        }
                    };
                    predCol.Children.Add(predCard);
                }
            }

            // ── 当前卡（中）：高亮，不接导航 ─────────────────────────────────
            currentHolder.Children.Add(BuildEncounterNodeCard(current, new NodeCardOptions(IsCurrent: true)));

            // ── 后继层（右）：D06/D07 分支卡复用 ─────────────────────────────
            if (branches.Count == 0)
            {
                // 无后继 → ⏹ 终点 徽章 + 空提示（D08 §二）
                var emptyCol = new StackPanel { Spacing = 6 };
                emptyCol.Children.Add(_vis.MiniBadge(_vis.Loc("Vis.TerminalPoint"), "#ECEFF1", "#546E7A"));
                emptyCol.Children.Add(new TextBlock
                {
                    Text = _vis.Loc("Vis.NoBranches"), FontSize = 10, Foreground = Brush.Parse("#999"),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                branchesPanel.Children.Add(emptyCol);
            }
            else
            {
                foreach (var b in branches)
                {
                    // R64: 去掉 → 箭头——布局已暗示流向；去路（物品触发）作为
                    // Annotation 放卡片底部行中间。
                    // D07 §3.2: Stay/Blank are end-of-story markers (grey capsules)
                    if (b.EndKind == BranchEndKind.None)
                    {
                        var cardEnc = b.Target ?? new Encounter { Id = b.TargetId, Name = $"Enc #{b.TargetId}" };
                        var succCard = BuildEncounterNodeCard(cardEnc, new NodeCardOptions(
                            IsCurrent: false,
                            Weight: b.Weight,
                            EffectiveProb: b.EffectiveProb,
                            Filtered: !b.IsSatisfied && selectedPreConds.Count > 0,
                            Resolved: b.Target is not null,
                            Branch: b,
                            ActivePreConds: selectedPreConds,
                            Source: current,
                            Annotation: BranchAnnotation(b)));
                        if (b.Target is not null)
                        {
                            _refNode.WireNavigation(succCard, typeof(Encounter), b.Target.EntityId, current, b.Target);
                            // R64: 左键点击 → 组件内焦点切换
                            var capturedTarget = b.Target;
                            succCard.PointerPressed += (_, e) =>
                            {
                                if ((e.KeyModifiers & KeyModifiers.Control) != 0) return;
                                if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                                {
                                    current = capturedTarget;
                                    RebuildFlow();
                                }
                            };
                        }
                        branchesPanel.Children.Add(succCard);
                    }
                    else
                    {
                        branchesPanel.Children.Add(BuildEndCapsule(b));
                    }
                }
            }

            // ── Pre-condition filter checkboxes（随焦点场景重建）──────────────
            var allPreConds = new List<(string RawId, string Display, bool IsNeg)>();
            var seenPre = new HashSet<string>();
            foreach (var b in branches)
            {
                if (b.Target is null) continue;
                foreach (var (raw, isNeg, cond) in b.PreConds)
                {
                    var rawId = isNeg ? raw[1..] : raw;
                    if (!seenPre.Add(rawId)) continue;
                    allPreConds.Add((rawId, cond?.Subject ?? rawId, isNeg));
                }
            }
            if (allPreConds.Count > 0)
            {
                preCondPanel.Children.Add(new TextBlock
                {
                    Text = _vis.Loc("Vis.PreConditions"), FontSize = 9, Foreground = Brush.Parse("#999"),
                    FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4)
                });
                var cbPanel = new WrapPanel();
                foreach (var (rawId, display, isNeg) in allPreConds)
                {
                    var cbContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                    if (isNeg)
                        cbContent.Children.Add(new TextBlock
                        {
                            Text = "¬", FontSize = 9, Foreground = Brush.Parse("#C62828"),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    cbContent.Children.Add(new TextBlock
                    {
                        Text = display, FontSize = 10,
                        TextDecorations = isNeg ? TextDecorations.Strikethrough : null,
                        Foreground = isNeg ? Brush.Parse("#888") : Brush.Parse("#333")
                    });
                    var cb = new CheckBox
                    {
                        Content = cbContent, FontSize = 10, IsChecked = selectedPreConds.Contains(rawId),
                        Margin = new Thickness(0, 0, 8, 2)
                    };
                    cb.IsCheckedChanged += (_, _) =>
                    {
                        if (cb.IsChecked == true) selectedPreConds.Add(rawId);
                        else selectedPreConds.Remove(rawId);
                        RebuildFlow();
                    };
                    cbPanel.Children.Add(cb);
                }
                preCondPanel.Children.Add(cbPanel);
            }

            // R64: 任何一次重建（初始/左键切换/过滤/回到当前）后都把当前场景卡滚动居中
            CenterOnCurrent();
        }

        RebuildFlow();

        // ── 场景流转内容（R64: 去掉 TabControl——剧情链/Mermaid 源码两个 Tab 没必要，
        //    流转主视图已覆盖核心认知；直接内联渲染）──
        var flowContent = new StackPanel();
        if (preCondPanel.Children.Count > 0)
            flowContent.Children.Add(preCondPanel);
        flowContent.Children.Add(flowScroll);

        sp.Children.Add(flowContent);
        return sp;
    }

    // ═══════════════ ③ 如何进入（D08 §四）：触发条件 + 自身前置条件 + 触发器摘要 ═══════════════

    /// <summary>
    /// D08 §四: the "how does this story appear" block — aConditions badge row
    /// ("1"/"0" no-condition placeholders hidden, semantic colors, hover = effect
    /// translation), the encounter's own PreConditions beside it, and the
    /// EncounterTrigger summaries (📍 area / 📅 date / 🧱 hex types / ♻ repeatable).
    /// Hidden entirely when nothing meaningful is present.
    /// </summary>
    private Control? BuildEntrySection(Encounter enc)
    {
        var inner = new StackPanel { Spacing = 8 };
        var hasContent = false;

        // 4.1 触发条件（aConditions）——"1"/"0" 无条件占位去噪（1116/2264 条）
        var condSegs = enc.Conditions.Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s != "1" && s != "0").ToList();
        if (condSegs.Count > 0)
        {
            inner.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.TriggerLabel")));
            var wp = new WrapPanel();
            foreach (var seg in condSegs)
            {
                var cond = _vis.Resolver.LookupRef<Condition>(enc, nameof(Encounter.Conditions), seg);
                var (bg, fg) = cond is null
                    ? ("#F5F5F5", "#999")
                    : cond.Fatal ? ("#FFEBEE", "#C62828")          // Fatal 红
                    : cond.Duration <= 0 ? ("#FFF3E0", "#E65100") // Instant 橙
                    : cond.Stackable ? ("#E8F5E9", "#2E7D32")     // Stackable 绿
                    : ("#E3F2FD", "#1565C0");                     // 时长 蓝
                var badge = cond is not null
                    ? _refNode.BadgeForEntity(enc, cond, cond.Subject!, bg, fg)
                    : _vis.MiniBadge(seg, bg, fg);
                if (cond is not null)
                {
                    // hover = 条件效果翻译（R43 BuildConditionEffectText 语义）
                    var effect = ConditionEffectText(cond);
                    if (effect.Length > 0)
                    {
                        var tip = new StackPanel { Spacing = 2, MaxWidth = 280 };
                        tip.Children.Add(new TextBlock
                        {
                            Text = cond.Subject ?? seg, FontSize = 10, FontWeight = FontWeight.SemiBold,
                            Foreground = Brush.Parse("#333")
                        });
                        tip.Children.Add(new TextBlock
                        {
                            Text = $"⚡ {effect}", FontSize = 10, Foreground = Brush.Parse("#546E7A"),
                            TextWrapping = TextWrapping.Wrap
                        });
                        ToolTip.SetTip(badge, new Border
                        {
                            Background = Brushes.White, CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(8), Child = tip
                        });
                    }
                }
                wp.Children.Add(badge);
            }
            inner.Children.Add(_vis.Card(wp));
            hasContent = true;
        }

        // 自身前置条件（当前剧情的 aPreConditions）——与触发条件并列
        if (!string.IsNullOrWhiteSpace(enc.PreConditions))
        {
            inner.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.PreConditions")));
            var wp = new WrapPanel();
            foreach (var seg in enc.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var isNeg = seg.StartsWith("-");
                var rawId = isNeg ? seg[1..] : seg;
                var cond = _vis.Resolver.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), seg);
                if (cond is not null)
                    wp.Children.Add(_refNode.BadgeForEntity(enc, cond,
                        (isNeg ? "NOT " : "") + cond.Subject,
                        isNeg ? "#FFEBEE" : "#E8F5E9",
                        isNeg ? "#C62828" : "#2E7D32"));
                else
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            inner.Children.Add(_vis.Card(wp));
            hasContent = true;
        }

        // 4.2 触发器摘要（EncounterTrigger 反查，每行 名称 + 触发方式摘要）
        var triggers = FindTriggers(enc.Id);
        if (triggers.Count > 0)
        {
            inner.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Triggers")));
            var rows = new StackPanel { Spacing = 4 };
            foreach (var trigger in triggers)
            {
                var row = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(_refNode.BadgeForEntity(enc, trigger, trigger.Name!,
                    "#F3E5F5", "#6A1B9A"));
                var summary = TriggerSummary(trigger);
                if (summary.Length > 0)
                    row.Children.Add(new TextBlock
                    {
                        Text = summary, FontSize = 10, Foreground = Brush.Parse("#666"),
                        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
                    });
                rows.Children.Add(row);
            }
            inner.Children.Add(_vis.Card(rows));
            hasContent = true;
        }

        if (!hasContent) return null;
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(_vis.SectionHeader(_vis.Loc("Vis.HowToEnter"), Symbol.Question, accent: "#1565C0"));
        section.Children.Add(inner);
        return section;
    }

    /// <summary>D08 §4.2: one trigger's summary line — 📍 area / 📅 date / 🧱 hex types / ♻ repeatable.</summary>
    private string TriggerSummary(EncounterTrigger trigger)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(trigger.Area))
        {
            var (x, y, r) = TryParseArea(trigger.Area);
            parts.Add(r is { } d
                ? $"📍 ({x},{y},r={d})"
                : $"📍 ({x},{y})");
        }
        if (!string.IsNullOrWhiteSpace(trigger.DateMin) || !string.IsNullOrWhiteSpace(trigger.DateMax))
            parts.Add($"📅 {trigger.DateMin}~{trigger.DateMax}");
        if (trigger.HexTypes.Count > 0)
            parts.Add(_vis.Loc("Vis.HexTypesShort"));
        if (!trigger.Unique)
            parts.Add(_vis.Loc("Vis.Repeatable"));
        return string.Join("  ", parts);
    }

    /// <summary>Parse "x,y,r" (also ";"/space separated) into numeric parts; null when unparsable.</summary>
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

    /// <summary>
    /// R43 (Doc 38 §5): aFieldNames/aModifiers pairs → "m_fMoveCost +0.5 · m_fVisibility -0.2".
    /// Mirrors VisHelperService.BuildConditionEffectText (private there) so the
    /// Encounter page can show the real effect of a trigger condition on hover.
    /// </summary>
    private static string ConditionEffectText(Condition c)
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

    // ═══════════════ ④ 内容与效果（D08 §五）：描述书页式 + ✨ 效果区 + 地图标注 ═══════════════
    private Control? BuildContentEffectsSection(Encounter enc)
    {
        // R64: 两栏——左 = 内容（仅描述书页式），右 = 效果（行为清单）。
        // 放场景流转上方：先看"这个剧情是什么/做了什么"，再进入流转上下文。
        var left = new StackPanel { Spacing = 8 };
        var storyPage = BuildStoryPagePanel(enc);
        if (storyPage is not null) left.Children.Add(storyPage);

        var right = new StackPanel { Spacing = 8 };
        var effects = BuildEffectsPanel(enc);
        if (effects is not null) right.Children.Add(effects);

        if (left.Children.Count == 0 && right.Children.Count == 0) return null;

        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(_vis.SectionHeader(_vis.Loc("Vis.ContentEffects"), Symbol.Gift, accent: "#E65100"));
        if (left.Children.Count > 0 && right.Children.Count > 0)
        {
            var grid = new Grid
            {
                ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star) },
                ColumnSpacing = 14
            };
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            section.Children.Add(grid);
        }
        else if (left.Children.Count > 0)
            section.Children.Add(left);
        else
            section.Children.Add(right);
        return section;
    }

    /// <summary>
    /// R64: 效果区重构——紧凑"行为清单"而非散落的徽章行。单 Card 内每行 =
    /// 语义徽章 + 值（可跳转）；战利品树（vLoot/nTreasureID）默认折叠（Expander
    /// 点击展开）；地图标注并入最后一行（不再是独立小节）。无任何效果 → null。
    /// </summary>
    private Control? BuildEffectsPanel(Encounter enc)
    {
        var rows = new List<Control>();

        // 🎁 获得（vLoot + nTreasureID 同类合并：都是"进入后获得物品池"）
        // 实测：vLoot 真值 21 条、nTreasureID≠3 240 条；战利品树折叠。
        var lootTts = new List<(string Label, TreasureTable? Tt, string RawId)>();
        if (!string.IsNullOrWhiteSpace(enc.Loot) && enc.Loot != "0" && enc.Loot != "3")
            lootTts.Add((_vis.Loc("Vis.GiveLoot"),
                _vis.Resolver.LookupRef<TreasureTable>(enc, nameof(Encounter.Loot), enc.Loot), enc.Loot));
        if (!string.IsNullOrWhiteSpace(enc.TreasureId) && enc.TreasureId != "3")
            lootTts.Add((_vis.Loc("Vis.LootPool"),
                _vis.Resolver.LookupRef<TreasureTable>(enc, nameof(Encounter.TreasureId), enc.TreasureId),
                enc.TreasureId));
        if (lootTts.Count > 0)
        {
            // R64: 每池一行——直接复用 TreasureTableEntityVisualizer.BuildNestedItems
            // （现成的嵌套组件：概率归一 + 嵌套 TT 递归 + 未解析兜底），不手搓树。
            foreach (var (label, tt, rawId) in lootTts)
            {
                var valueRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                valueRow.Children.Add(_refNode.Badge<TreasureTable>(enc, nameof(Encounter.TreasureId), rawId,
                    resolvedBg: "#E8F5E9", resolvedFg: "#2E7D32",
                    unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
                var rowSp = new StackPanel { Spacing = 4 };
                rowSp.Children.Add(BuildEffectRow(
                    _vis.MiniBadge(label, "#E8F5E9", "#2E7D32"), valueRow, null));
                if (tt is not null && BuildLootInlineTree(tt) is { } lootTree)
                    rowSp.Children.Add(lootTree);
                rows.Add(rowSp);
            }
        }

        // 📦 给予物品（nItemsID ≠ "3"）
        if (!string.IsNullOrWhiteSpace(enc.ItemsId) && enc.ItemsId != "3")
        {
            rows.Add(BuildEffectRow(
                _vis.MiniBadge(_vis.Loc("Vis.GiveItem"), "#E3F2FD", "#1565C0"),
                _refNode.Badge<ItemType>(enc, nameof(Encounter.ItemsId), enc.ItemsId,
                    resolvedBg: "#E3F2FD", resolvedFg: "#1565C0",
                    unresolvedBg: "#F5F5F5", unresolvedFg: "#999"),
                null));
        }

        // 💰 费用（实测全正=扣钱）
        if (enc.Price != 0)
        {
            rows.Add(BuildEffectRow(
                _vis.MiniBadge(_vis.Loc("Vis.Cost"), "#F3E5F5", "#6A1B9A"),
                new TextBlock
                {
                    Text = "$" + enc.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#6A1B9A"),
                    VerticalAlignment = VerticalAlignment.Center
                },
                null));
        }

        // 🗑 移除（nRemoveTreasureID ≠ "3"）
        if (!string.IsNullOrWhiteSpace(enc.RemoveTreasureId) && enc.RemoveTreasureId != "3")
        {
            rows.Add(BuildEffectRow(
                _vis.MiniBadge(_vis.Loc("Vis.RemoveLoot"), "#FFEBEE", "#C62828"),
                _refNode.Badge<TreasureTable>(enc, nameof(Encounter.RemoveTreasureId), enc.RemoveTreasureId,
                    resolvedBg: "#FFEBEE", resolvedFg: "#C62828",
                    unresolvedBg: "#F5F5F5", unresolvedFg: "#999"),
                null));
        }

        // 📍 传送（ptTeleport ≠ "0,0"）
        if (!string.IsNullOrWhiteSpace(enc.Teleport) && enc.Teleport != "0,0")
        {
            rows.Add(BuildEffectRow(
                _vis.MiniBadge(_vis.Loc("Vis.TeleportTo"), "#E0F2F1", "#00695C"),
                new TextBlock
                {
                    Text = "(" + enc.Teleport + ")", FontSize = 11, FontWeight = FontWeight.SemiBold,
                    Foreground = Brush.Parse("#00695C"), VerticalAlignment = VerticalAlignment.Center
                },
                null));
        }

        // 🐾 刷出（nCreatureID ≠ "0"，带半径）
        if (!string.IsNullOrWhiteSpace(enc.CreatureId) && enc.CreatureId != "0")
        {
            var creatureBadge = _refNode.Badge<Creature>(enc, nameof(Encounter.CreatureId), enc.CreatureId,
                resolvedBg: "#E8EAF6", resolvedFg: "#283593",
                unresolvedBg: "#F5F5F5", unresolvedFg: "#999");
            if (!string.IsNullOrWhiteSpace(enc.CreatureHex) && enc.CreatureHex != "0,0")
            {
                var radius = new TextBlock
                {
                    Text = "(半径 " + enc.CreatureHex + ")", FontSize = 10, Foreground = Brush.Parse("#999"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                rows.Add(BuildEffectRow(
                    _vis.MiniBadge(_vis.Loc("Vis.SpawnOut"), "#FFF3E0", "#E65100"),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 6,
                        VerticalAlignment = VerticalAlignment.Center, Children = { creatureBadge, radius }
                    },
                    null));
            }
            else
            {
                rows.Add(BuildEffectRow(
                    _vis.MiniBadge("🐾 " + _vis.Loc("Vis.SpawnOut"), "#FFF3E0", "#E65100"),
                    creatureBadge, null));
            }
        }

        // 💥 意外事件（vAccidents 真值才显示）
        if (!string.IsNullOrWhiteSpace(enc.Accidents) && enc.Accidents != "1")
        {
            var wp = new WrapPanel();
            foreach (var seg in enc.Accidents.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var accident = _vis.Resolver.LookupRef<Encounter>(enc, nameof(Encounter.Accidents), seg);
                if (accident is not null)
                    wp.Children.Add(_refNode.BadgeForEntity(enc, accident, accident.Subject!,
                        "#FFEBEE", "#C62828"));
                else
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            rows.Add(BuildEffectRow(_vis.MiniBadge(_vis.Loc("Vis.Accidents"), "#FFEBEE", "#C62828"), wp, null));
        }

        // 🗺 地图标注（并入效果区最后一行，不再是独立小节）
        var mapWp = new WrapPanel();
        if (!string.IsNullOrWhiteSpace(enc.MinimapHexes))
        {
            foreach (var seg in enc.MinimapHexes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var eqIdx = seg.IndexOf('=');
                var pos = eqIdx > 0 ? seg[..eqIdx].Trim() : seg;
                var label = eqIdx > 0 ? seg[(eqIdx + 1)..].Trim() : null;
                var text = string.IsNullOrEmpty(label) ? "📍(" + pos + ")" : "📍(" + pos + ") " + label;
                mapWp.Children.Add(_vis.MiniBadge(text, "#FFF8E1", "#F57F17"));
            }
        }
        if (!string.IsNullOrWhiteSpace(enc.Editor) && enc.Editor != "0,0")
            mapWp.Children.Add(_vis.MiniBadge("✏️(" + enc.Editor + ")", "#F5F5F5", "#999"));
        if (mapWp.Children.Count > 0)
            rows.Add(BuildEffectRow(_vis.MiniBadge(_vis.Loc("Vis.MapNotes"), "#ECEFF1", "#546E7A"), mapWp, null));

        if (rows.Count == 0) return null;
        // R64: 不加"效果"标题头——每行自带语义徽章（🎁/📦/💰…），看内容自然能猜出来
        var rowsPanel = new StackPanel { Spacing = 6 };
        foreach (var row in rows) rowsPanel.Children.Add(row);
        return _vis.Card(rowsPanel);
    }

    /// <summary>One ✨ effect row: label badge + value(s) + optional inline content below.</summary>
    private static Control BuildEffectRow(Control label, Control value, Control? below)
    {
        var col = new StackPanel { Spacing = 4 };
        var row = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(label);
        row.Children.Add(value);
        col.Children.Add(row);
        if (below is not null)
            col.Children.Add(below);
        return col;
    }

    /// <summary>
    /// R64: the vLoot/nTreasureID inline treasure tree — reuses the TreasureTable
    /// visualizer row/nested helpers (same probability normalization). Rendered
    /// R64: 战利品树直接复用 TreasureTableEntityVisualizer.BuildNestedItems
    /// （现成嵌套组件：概率归一 + 嵌套 TT 递归 + 未解析兜底），不手搓。
    /// </summary>
    private Control? BuildLootInlineTree(TreasureTable tt)
    {
        var itemTypes = BuildItemTypes(tt.ModId) ?? new Dictionary<string, ItemType>();
        return TreasureTableEntityVisualizer.BuildNestedItems(_vis, _dataTable, tt, itemTypes, 1, _refNode);
    }
    private List<EncounterTrigger> FindTriggers(int encounterId)
    {
        if (!(_dataTable?.ReferenceLookups ?? []).TryGetValue(typeof(EncounterTrigger), out var list) || list is null)
            return [];
        // R30: ToRawString derives from the entries (RawText can be stale after mutation).
        return list.OfType<EncounterTrigger>()
            .Where(t => t.EncounterId.ToRawString(null) == encounterId.ToString()).ToList();
    }

    private Control BuildReverseRefsPanel(Encounter enc)
        => _vis.BuildReverseRefsPanel(enc.EntityId);
}
