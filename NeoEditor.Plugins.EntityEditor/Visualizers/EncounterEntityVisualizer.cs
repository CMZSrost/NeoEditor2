using System;
using System.Collections.Generic;
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

        root.Children.Add(BuildHeroHeader(enc));
        if (!string.IsNullOrWhiteSpace(enc.Description))
            root.Children.Add(BuildStoryPanel(enc));
        if (!string.IsNullOrWhiteSpace(enc.Responses))
        {
            root.Children.Add(BuildStoryBranchDiagram(enc));
            root.Children.Add(BuildResponsesPanel(enc));
        }

        root.Children.Add(BuildRefsPanel(enc));
        var triggers = FindTriggers(enc.Id);
        if (triggers.Count > 0)
            root.Children.Add(BuildTriggersPanel(triggers));
        root.Children.Add(BuildReverseRefsPanel(enc));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Control BuildHeroHeader(Encounter enc)
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
        var typeLabel = enc.Type == EncounterType.Scavenge ? "Scavenge" : "Normal";
        var typeBg = enc.Type == EncounterType.Scavenge ? "#FFF3E0" : "#E3F2FD";
        var typeFg = enc.Type == EncounterType.Scavenge ? "#E65100" : "#1565C0";
        infoRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });
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

    private Control BuildStoryPanel(Encounter enc)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.StoryText")));
        var desc = enc.Description.Length > 2000 ? enc.Description[..2000] + "..." : enc.Description;
        sp.Children.Add(_vis.Card(new TextBlock
            { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#333") }));
        return sp;
    }

    // Response entry: optional item prefix + target encounter
    private sealed record ResponseEntry(
        string? ItemId, double ItemMult, ItemType? Item,
        int TargetId, double Weight, double Probability, Encounter? TargetEncounter);

    private Control BuildResponsesPanel(Encounter enc)
    {
        var sp = new StackPanel();
        var responseList = ParseResponseEntries(enc.Responses, enc);
        sp.Children.Add(_vis.SectionLabel(
            $"{_vis.Loc("Vis.Responses")} ({responseList.Count} {(responseList.Count > 1 ? _vis.Loc("Vis.Options") : _vis.Loc("Vis.Option"))})"));

        // Response format hint (from Comment attribute)
        sp.Children.Add(new TextBlock
        {
            Text = "格式: [物品ID]x[数量]=[剧情ID]x[权重]  ·  空物品(=开头)=无需物品的选项  ·  概率=权重/权重和",
            FontSize = 9, Foreground = Brush.Parse("#AAA"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, -4, 0, 4)
        });

        if (responseList.Count == 0)
        {
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = _vis.Loc("Vis.NoResponses"), FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        var cardStack = new StackPanel { Spacing = 8 };
        foreach (var resp in responseList)
        {
            var row = new StackPanel { Spacing = 4 };

            // Row 1: item usage hint (if applicable)
            if (resp.Item is not null)
            {
                var itemRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                itemRow.Children.Add(new TextBlock
                {
                    Text = "使用物品:", FontSize = 9, Foreground = Brush.Parse("#888"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                var qtyText = resp.ItemMult > 1 ? $" ×{resp.ItemMult}" : "";
                itemRow.Children.Add(_refNode.BadgeForEntity(enc, resp.Item,
                    $"{resp.Item.Description}{qtyText}", "#E3F2FD", "#1565C0"));
                itemRow.Children.Add(new TextBlock
                {
                    Text = "→ 触发:", FontSize = 9, Foreground = Brush.Parse("#888"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(itemRow);
            }
            else if (resp.ItemId is not null)
            {
                var itemRow = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                itemRow.Children.Add(new TextBlock
                {
                    Text = $"使用物品 #{resp.ItemId}", FontSize = 9, Foreground = Brush.Parse("#999"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                if (resp.ItemMult > 1)
                    itemRow.Children.Add(new TextBlock
                    {
                        Text = $"×{resp.ItemMult}", FontSize = 9, Foreground = Brush.Parse("#999"),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                itemRow.Children.Add(new TextBlock
                {
                    Text = "→", FontSize = 9, Foreground = Brush.Parse("#888"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(itemRow);
            }

            // Row 2: target encounter + probability bar
            var targetRow = new Grid { ColumnDefinitions = { new(1, GridUnitType.Star), new(100, GridUnitType.Pixel) } };

            var leftStack = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            if (resp.TargetEncounter is not null)
            {
                leftStack.Children.Add(_refNode.BadgeForEntity(enc, resp.TargetEncounter,
                    resp.TargetEncounter.Subject!,
                    "#E8F5E9", "#2E7D32"));
                if (resp.TargetEncounter.Type == EncounterType.Scavenge)
                    leftStack.Children.Add(_vis.MiniBadge("Scavenge", "#FFF3E0", "#E65100"));
            }
            else
                leftStack.Children.Add(_vis.MiniBadge($"Enc #{resp.TargetId}", "#F5F5F5", "#999"));

            Grid.SetColumn(leftStack, 0);
            targetRow.Children.Add(leftStack);

            // Right: probability bar from calculated probability
            var probPct = Math.Clamp(resp.Probability, 0.0, 1.0);
            var probColor = probPct >= 0.5 ? "#2E7D32" : probPct >= 0.1 ? "#E65100" : "#999";
            var probBar = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background = Brush.Parse(probColor),
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = $"{resp.Weight:F1}({resp.Probability:P2})",
                    FontSize = 9,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(6, 0)
                },
                Width = Math.Max(probPct * 100, 50)
            };
            Grid.SetColumn(probBar, 1);
            targetRow.Children.Add(probBar);

            row.Children.Add(targetRow);
            cardStack.Children.Add(row);
        }

        sp.Children.Add(_vis.Card(cardStack));
        return sp;
    }

    private List<ResponseEntry> ParseResponseEntries(string raw, Encounter sourceEnc)
    {
        var result = new List<ResponseEntry>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        // Build itemTypes lookup for item prefix resolution
        var itemTypes = new Dictionary<string, ItemType>();
        try
        {
            itemTypes = _dataTable?.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", sourceEnc.ModId);
        }
        catch { /* ignore if not available */ }

        // Format: [itemId]x[mult]=[encounterId]x[weight]x0x0x0
        //   or just: =[encounterId]x[weight]x0x0x0  (no item needed)
        // weight is used to calculate probability: thisWeight / sumOfAllWeights
        var rawEntries = new List<(string? itemId, double itemMult, ItemType? item, int targetId, double weight, Encounter? targetEnc)>();
        double totalWeight = 0;

        foreach (var seg in raw.Split(','))
        {
            var s = seg.Trim();
            if (s.Length == 0) continue;

            string? itemId = null;
            double itemMult = 1.0;
            ItemType? item = null;
            int targetId;
            double weight = 1.0;

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
                // Parse optional item prefix (before '=')
                if (eqIdx > 0)
                {
                    var itemPart = s[..eqIdx].Trim();
                    if (itemPart.EndsWith('x')) itemPart = itemPart[..^1];
                    var itemParts = itemPart.Split('x');
                    if (itemParts.Length >= 1)
                    {
                        itemId = itemParts[0].Trim();
                        if (!string.IsNullOrEmpty(itemId) && !int.TryParse(itemId, out _))
                        {
                            // itemId is like "90.3" or "87.1"
                            if (itemParts.Length >= 2)
                            {
                                itemMult = double.TryParse(itemParts[1], System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var im) ? im : 1.0;
                            }

                            if (itemTypes.TryGetValue(itemId, out var found))
                                item = found;
                        }
                    }
                }

                // Parse encounter suffix (after '=')
                var encPart = s[(eqIdx + 1)..].Trim();
                var encParts = encPart.Split('x');
                if (encParts.Length < 2) continue;
                if (!int.TryParse(encParts[0], out targetId)) continue;
                // encParts[1] is the weight (not direct probability)
                weight = double.TryParse(encParts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p2) ? p2 : 1.0;
            }

            totalWeight += weight;
            Encounter? targetEnc =
                _vis.Resolver.LookupRef<Encounter>(sourceEnc, nameof(Encounter.Responses),
                    targetId.ToString());

            rawEntries.Add((itemId, itemMult, item, targetId, weight, targetEnc));
        }

        // Calculate probability from weights
        foreach (var (itemId, itemMult, item, targetId, weight, targetEnc) in rawEntries)
        {
            var prob = totalWeight > 0 ? weight / totalWeight : 1.0 / rawEntries.Count;
            result.Add(new ResponseEntry(itemId, itemMult, item, targetId, weight, prob, targetEnc));
        }

        return result;
    }

    // ═══════════════ Story Branch Diagram (Mermaid-style) ═══════════════

    private Control BuildStoryBranchDiagram(Encounter enc)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.StoryBranch")));

        var responseList = ParseResponseEntries(enc.Responses, enc);
        if (responseList.Count == 0)
        {
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = _vis.Loc("Vis.NoBranches"), FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        // ── Collect all unique PreConditions for checkbox filtering ──
        var allPreConds = new List<(string RawId, string Display, bool IsNeg)>();
        var seenPre = new HashSet<string>();
        void AddPreConds(string? preStr, Encounter ctx)
        {
            if (string.IsNullOrWhiteSpace(preStr)) return;
            foreach (var seg in preStr.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var isNeg = seg.StartsWith("-");
                var rawId = isNeg ? seg[1..] : seg;
                if (!seenPre.Add(rawId)) continue;
                var cond = _vis.Resolver.LookupRef<Condition>(ctx, nameof(Encounter.PreConditions), seg);
                allPreConds.Add((rawId, cond?.Subject ?? rawId, isNeg));
            }
        }
        // Only collect pre-conditions of NEXT encounters (not current step)
        foreach (var resp in responseList)
        {
            if (resp.TargetEncounter is not null)
                AddPreConds(resp.TargetEncounter.PreConditions, resp.TargetEncounter);
        }

        // ── Collect reverse references (previous encounters → current) ──
        // Scan Encounter Responses directly (not indexed via ReferenceField)
        var reverseRefs = new List<(Encounter Src, string? ItemDesc, double ItemMult, double Weight)>();
        var revSeen = new HashSet<string>();
        {
            if ((_dataTable?.ReferenceLookups ?? []).TryGetValue(typeof(Encounter), out var allEncs) && allEncs is not null)
            {
                var itemTypes = new Dictionary<string, ItemType>();
                try { itemTypes = _dataTable?.GetCompositeEntities<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}", enc.ModId); }
                catch (Exception ex) { Serilog.Log.Logger.Verbose(ex, "[EncounterVis] GetCompositeEntities<ItemType> failed"); }
                foreach (var obj in allEncs)
                {
                    if (obj is not Encounter parentEnc || parentEnc.EntityId == enc.EntityId) continue;
                    if (string.IsNullOrWhiteSpace(parentEnc.Responses)) continue;
                    foreach (var seg in parentEnc.Responses.Split(','))
                    {
                        var s = seg.Trim();
                        if (s.Length == 0) continue;
                        var eqIdx = s.IndexOf('=');
                        if (eqIdx < 0) continue;
                        // Parse item part (before =)
                        string? itemDesc = null;
                        double itemMult = 1.0;
                        if (eqIdx > 0)
                        {
                            var itemPart = s[..eqIdx].Trim();
                            var itemParts = itemPart.Split('x');
                            var itemIdRaw = itemParts[0].Trim();
                            if (!string.IsNullOrEmpty(itemIdRaw) && !int.TryParse(itemIdRaw, out _))
                            {
                                if (itemParts.Length >= 2)
                                    double.TryParse(itemParts[1], System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out itemMult);
                                itemDesc = itemTypes.TryGetValue(itemIdRaw, out var fi) ? fi.Description : itemIdRaw;
                            }
                        }
                        // Parse encounter target (after =)
                        var encPart = s[(eqIdx + 1)..].Trim();
                        var encParts = encPart.Split('x');
                        if (encParts.Length < 2) continue;
                        if (!int.TryParse(encParts[0], out var targetId) || targetId != enc.Id) continue;
                        double weight = double.TryParse(encParts[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 1.0;
                        var key = $"{parentEnc.EntityId}|{s}";
                        if (!revSeen.Add(key)) continue;
                        reverseRefs.Add((parentEnc, itemDesc, itemMult, weight));
                    }
                }
            }
        }

        // Shared state for preCondition checkbox → Mermaid refresh
        var selectedPreConds = new HashSet<string>();

        // ── Helper: check if a single preCondition is satisfied (handles Y/N polarity) ──
        bool IsPreCondSatisfied(string preStr, HashSet<string> activeSet)
        {
            if (activeSet.Count == 0) return true; // no active filter — all conditions considered satisfied
            var isNeg = preStr.StartsWith("-");
            var rid = isNeg ? preStr[1..] : preStr;
            // Positive preCond "5": satisfied if checkbox IS checked (player has condition)
            // Negative preCond "-5": satisfied if checkbox is NOT checked (player does NOT have condition)
            return isNeg ? !activeSet.Contains(rid) : activeSet.Contains(rid);
        }

        // ── Helper: check if ALL target encounter's preConditions are satisfied ──
        bool AreAllPreCondsSatisfied(Encounter? target, HashSet<string> activeSet)
        {
            if (target is null || string.IsNullOrWhiteSpace(target.PreConditions)) return true;
            var pres = target.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
            return pres.All(p => IsPreCondSatisfied(p, activeSet));
        }

        // ── Helper: build context label (treasure, creature, pre) ──
        string BuildCtxLabel(Encounter e)
        {
            var ctx = "";
            if (!string.IsNullOrWhiteSpace(e.TreasureId) && e.TreasureId != "3")
            {
                var tt = _vis.Resolver.LookupRef<TreasureTable>(e, nameof(Encounter.TreasureId), e.TreasureId);
                if (tt is not null) ctx += $"🎒{tt.Name} ";
            }
            if (e.CreatureId != "0") ctx += "🐾 ";
            if (!string.IsNullOrWhiteSpace(e.PreConditions))
            {
                var preCount = e.PreConditions.Split(',').Length;
                ctx += $"📋pre:{preCount} ";
            }
            return ctx;
        }

        // ── Mermaid text builder ──
        string BuildMermaid()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("flowchart LR");

            // ── Reverse refs: previous encounters → current ──
            var seenRev = new HashSet<int>();
            int revIdx = 0;
            foreach (var (src, itemDesc, itemMult, weight) in reverseRefs)
            {
                if (!seenRev.Add(src.Id)) continue;
                var revNodeId = $"R{revIdx}";
                var revName = (src.Subject ?? $"Enc #{src.Id}").Replace("\"", "\\\"");
                var viaLabel = itemDesc is not null
                    ? $"{itemDesc}{(itemMult > 1 ? $" x{itemMult}" : "")} | {weight:F1}"
                    : $"{weight:F1}";
                sb.AppendLine($"    {revNodeId}[\"← {revName}\"]");
                sb.AppendLine($"    {revNodeId} -->|\"{viaLabel}\"| A");
                revIdx++;
            }

            // ── Current node ──
            var currentCtx = BuildCtxLabel(enc);
            var currentName = (enc.Subject ?? $"Enc #{enc.Id}").Replace("\"", "\\\"");
            var currentLabel = string.IsNullOrEmpty(currentCtx)
                ? $"📍 {currentName}"
                : $"📍 {currentName}<br/>{currentCtx.Trim()}";
            sb.AppendLine($"    A[\"{currentLabel}\"]");

            // Calculate effective probability: only count valid branches (Y/N matching)
            double validTotalWeight = 0;
            foreach (var r in responseList)
            {
                if (AreAllPreCondsSatisfied(r.TargetEncounter, selectedPreConds))
                    validTotalWeight += r.Weight;
            }

            // ── Forward edges ──
            for (int i = 0; i < responseList.Count; i++)
            {
                var resp = responseList[i];
                var nodeId = (char)('B' + i);
                var targetCtx = resp.TargetEncounter is not null ? BuildCtxLabel(resp.TargetEncounter) : "";
                var targetName = (resp.TargetEncounter?.Subject ?? $"Enc #{resp.TargetId}").Replace("\"", "\\\"");
                var targetLabel = string.IsNullOrEmpty(targetCtx) ? targetName : $"{targetName}<br/>{targetCtx.Trim()}";

                // PreCondition match info for edge label (respects Y/N polarity)
                var matchInfo = "";
                if (resp.TargetEncounter is not null && !string.IsNullOrWhiteSpace(resp.TargetEncounter.PreConditions) && selectedPreConds.Count > 0)
                {
                    var targetPres = resp.TargetEncounter.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    int matched = 0;
                    foreach (var tp in targetPres)
                    {
                        if (IsPreCondSatisfied(tp, selectedPreConds)) matched++;
                    }
                    if (matched < targetPres.Count)
                        matchInfo = $" ⚠{matched}/{targetPres.Count}";
                }

                var isBranchValid = AreAllPreCondsSatisfied(resp.TargetEncounter, selectedPreConds);
                var effectiveProb = validTotalWeight > 0 && isBranchValid ? resp.Weight / validTotalWeight : 0.0;

                var edgeLabel = resp.Item is not null
                    ? $"{resp.Item.Description}{(resp.ItemMult > 1 ? $" x{resp.ItemMult}" : "")} | {resp.Weight:F1}({effectiveProb:P2}){matchInfo}"
                    : resp.ItemId is not null
                        ? $"#{resp.ItemId}{(resp.ItemMult > 1 ? $" x{resp.ItemMult}" : "")} | {resp.Weight:F1}({effectiveProb:P2}){matchInfo}"
                        : $"{resp.Weight:F1}({effectiveProb:P2}){matchInfo}";
                edgeLabel = edgeLabel.Replace("\"", "'");
                sb.AppendLine($"    A -->|\"{edgeLabel}\"| {nodeId}[\"{targetLabel}\"]");
            }

            return sb.ToString();
        }

        // ── Mermaid display block ──
        var mermaidTextBlock = new TextBlock
        {
            Text = BuildMermaid(), FontSize = 10, FontFamily = new FontFamily("Consolas, Menlo, monospace"),
            Foreground = Brush.Parse("#555"), TextWrapping = TextWrapping.NoWrap
        };
        var mermaidBlock = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#FAFAFA"),
            BorderBrush = Brush.Parse("#E0E0E0"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = mermaidTextBlock
            }
        };

        // ── PreCondition checkbox panel ──
        var preCondPanel = new StackPanel();
        var branchesPanel = new StackPanel
            { Orientation = Orientation.Vertical, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
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
                // Show negative precond with strikethrough style instead of "NOT " prefix
                var cbContent = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 4 };
                if (isNeg)
                {
                    cbContent.Children.Add(new TextBlock
                        { Text = "¬", FontSize = 9, Foreground = Brush.Parse("#C62828"),
                          VerticalAlignment = VerticalAlignment.Center });
                }
                cbContent.Children.Add(new TextBlock
                {
                    Text = display, FontSize = 10,
                    TextDecorations = isNeg ? TextDecorations.Strikethrough : null,
                    Foreground = isNeg ? Brush.Parse("#888") : Brush.Parse("#333")
                });
                var cb = new CheckBox
                {
                    Content = cbContent,
                    FontSize = 10, IsChecked = false, Margin = new Thickness(0, 0, 8, 2)
                };
                cb.IsCheckedChanged += (_, _) =>
                {
                    if (cb.IsChecked == true) selectedPreConds.Add(rawId);
                    else selectedPreConds.Remove(rawId);
                    mermaidTextBlock.Text = BuildMermaid();
                    // Rebuild visual tree branches to reflect preCondition changes
                    branchesPanel.Children.Clear();
                    BuildBranchNodes(branchesPanel, selectedPreConds);
                };
                cbPanel.Children.Add(cb);
            }
            preCondPanel.Children.Add(cbPanel);
        }

        // ── Local function: build branch nodes reflecting preCondition selection ──
        void BuildBranchNodes(StackPanel panel, HashSet<string> selPreConds)
        {
            // Recalculate valid-total weight based on Y/N condition matching
            double validTotalWeight = 0;
            foreach (var r in responseList)
            {
                if (AreAllPreCondsSatisfied(r.TargetEncounter, selPreConds))
                    validTotalWeight += r.Weight;
            }

            foreach (var resp in responseList)
            {
                var isBranchValid = AreAllPreCondsSatisfied(resp.TargetEncounter, selPreConds);
                // Effective probability: weight / validTotalWeight (or 0 if branch invalid)
                var effectiveProb = validTotalWeight > 0 && isBranchValid ? resp.Weight / validTotalWeight : 0.0;
                var probRatio = Math.Clamp(effectiveProb, 0.0, 1.0);
                var branchColor = probRatio >= 0.5 ? "#2E7D32" : probRatio >= 0.1 ? "#E65100" : "#999";
                var branchBg = probRatio >= 0.5 ? "#E8F5E9" : probRatio >= 0.1 ? "#FFF3E0" : "#F5F5F5";

                var branchOpacity = (!isBranchValid && selPreConds.Count > 0) ? 0.5 : 1.0;

                var branchNode = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, Opacity = branchOpacity };

                // Item usage hint badge (if applicable)
                if (resp.Item is not null)
                {
                    var qtyText = resp.ItemMult > 1 ? $" ×{resp.ItemMult}" : "";
                    branchNode.Children.Add(_vis.MiniBadge(
                        $"🛡 {resp.Item.Description}{qtyText}", "#E3F2FD", "#1565C0"));
                }
                else if (resp.ItemId is not null)
                {
                    branchNode.Children.Add(_vis.MiniBadge(
                        $"Item #{resp.ItemId}", "#F5F5F5", "#999"));
                }

                // Probability badge: weight(effective%)
                branchNode.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = Brush.Parse(branchColor),
                    Padding = new Thickness(8, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = $"{resp.Weight:F1}({effectiveProb:P2})", FontSize = 9, FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    }
                });

                // PreCondition badges (always shown, expanded)
                if (resp.TargetEncounter is not null && !string.IsNullOrWhiteSpace(resp.TargetEncounter.PreConditions))
                {
                    var targetPres = resp.TargetEncounter.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                    if (targetPres.Count > 0)
                    {
                        var preBadgesPanel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        foreach (var tp in targetPres)
                        {
                            var isNeg = tp.StartsWith("-");
                            var rid = isNeg ? tp[1..] : tp;
                            var isOn = selPreConds.Count == 0 || IsPreCondSatisfied(tp, selPreConds);
                            var cond = _vis.Resolver.LookupRef<Condition>(
                                resp.TargetEncounter, nameof(Encounter.PreConditions), tp);
                            var label = (isNeg ? "¬" : "") + (cond?.Subject ?? rid);
                            var bg = isNeg ? "#FFF3E0" : "#E8F5E9";
                            var fg = isNeg ? "#E65100" : "#2E7D32";
                            if (selPreConds.Count > 0 && !isOn) { bg = "#F5F5F5"; fg = "#CCC"; }
                            preBadgesPanel.Children.Add(new Border
                            {
                                CornerRadius = new CornerRadius(3),
                                Background = Brush.Parse(bg),
                                Padding = new Thickness(4, 1),
                                Margin = new Thickness(1),
                                Child = new TextBlock
                                {
                                    Text = label, FontSize = 7,
                                    Foreground = Brush.Parse(fg),
                                    TextDecorations = isNeg ? TextDecorations.Strikethrough : null
                                }
                            });
                        }
                        branchNode.Children.Add(preBadgesPanel);
                    }
                }

                // Target encounter badge
                var targetBadge = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    Background = Brush.Parse(branchBg),
                    Padding = new Thickness(8, 4),
                    Cursor = resp.TargetEncounter is not null
                        ? new Cursor(StandardCursorType.Hand)
                        : new Cursor(StandardCursorType.Arrow),
                    Child = new TextBlock
                    {
                        Text = resp.TargetEncounter?.Subject ?? $"Enc #{resp.TargetId}",
                        FontSize = 11,
                        Foreground = Brush.Parse(branchColor),
                        TextAlignment = TextAlignment.Center,
                        FontWeight = FontWeight.Medium
                    }
                };
                if (resp.TargetEncounter is not null)
                {
                    _refNode.WireNavigation(targetBadge, typeof(Encounter),
                        resp.TargetEncounter.EntityId, enc);
                }

                branchNode.Children.Add(targetBadge);
                panel.Children.Add(branchNode);
            }
        }

        // ── Visual tree diagram (horizontal: reverse ← current → branches) ──
        var treePanel = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };

        // LEFT column: Reverse refs (stacked vertically)
        var leftColumn = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };

        // Reverse refs: previous encounters → current (shown on the left)
        {
            if (reverseRefs.Count > 0)
            {
                foreach (var (src, itemDesc, itemMult, weight) in reverseRefs)
                {
                    var refInfo = itemDesc is not null
                        ? $"{itemDesc}{(itemMult > 1 ? $" ×{itemMult}" : "")} （权重 {weight:F0}）"
                        : $"权重 {weight:F0}";
                    var revBadge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Background = Brush.Parse("#FFF3E0"),
                        BorderBrush = Brush.Parse("#E65100"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 3),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Child = new StackPanel
                        {
                            Spacing = 1, Children =
                            {
                                new TextBlock
                                {
                                    Text = src.Subject ?? $"Enc #{src.Id}", FontSize = 10,
                                    FontWeight = FontWeight.Medium,
                                    Foreground = Brush.Parse("#BF360C"), TextAlignment = TextAlignment.Center
                                },
                                new TextBlock
                                {
                                    Text = refInfo, FontSize = 7,
                                    Foreground = Brush.Parse("#E65100"), TextAlignment = TextAlignment.Center
                                }
                            }
                        }
                    };
                    _refNode.WireNavigation(revBadge, typeof(Encounter),
                        src.EntityId, enc);
                    leftColumn.Children.Add(revBadge);
                }
            }
            else
            {
                // No previous encounter — this is a root event
                var rootIndicatorPanel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
                rootIndicatorPanel.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = Brush.Parse("#E8F5E9"),
                    BorderBrush = Brush.Parse("#2E7D32"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 3),
                    Child = new TextBlock
                    {
                        Text = _vis.Loc("Vis.RootEncounter"), FontSize = 9,
                        Foreground = Brush.Parse("#2E7D32"), TextAlignment = TextAlignment.Center,
                        FontWeight = FontWeight.Medium
                    }
                });

                // Show current encounter's preconditions for easy reference
                if (enc.PreConditions.Count > 0)
                {
                    var preList = enc.PreConditions.Select(e => e.ToRawString()).Where(s => s.Length > 0).ToList();
                    if (preList.Count > 0)
                    {
                        var preBadgesPanel = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        foreach (var p in preList)
                        {
                            var isNeg = p.StartsWith("-");
                            var rid = isNeg ? p[1..] : p;
                            var cond = _vis.Resolver.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), p);
                            var label = (isNeg ? "¬" : "") + (cond?.Subject ?? rid);
                            var bg = isNeg ? "#FFF3E0" : "#E8F5E9";
                            var fg = isNeg ? "#E65100" : "#2E7D32";
                            preBadgesPanel.Children.Add(new Border
                            {
                                CornerRadius = new CornerRadius(3),
                                Background = Brush.Parse(bg),
                                Padding = new Thickness(4, 1),
                                Margin = new Thickness(1),
                                Child = new TextBlock
                                {
                                    Text = label, FontSize = 7, Foreground = Brush.Parse(fg),
                                    TextDecorations = isNeg ? TextDecorations.Strikethrough : null
                                }
                            });
                        }
                        rootIndicatorPanel.Children.Add(preBadgesPanel);
                    }
                }
                leftColumn.Children.Add(rootIndicatorPanel);
            }
        }
        treePanel.Children.Add(leftColumn);

        // Arrow: left → center
        treePanel.Children.Add(new TextBlock
        {
            Text = "→", FontSize = 16,
            Foreground = Brush.Parse(reverseRefs.Count > 0 ? "#E65100" : "#2E7D32"),
            VerticalAlignment = VerticalAlignment.Center
        });

        // CENTER column: Current encounter (highlighted)
        var centerColumn = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        {
            var rootNode = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brush.Parse("#E3F2FD"),
                BorderBrush = Brush.Parse("#1565C0"),
                BorderThickness = new Thickness(3),
                Padding = new Thickness(12, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var rootSp = new StackPanel { Spacing = 2 };
            rootSp.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.CurrentEncounter"), FontSize = 8, Foreground = Brush.Parse("#1565C0"),
                TextAlignment = TextAlignment.Center
            });
            rootSp.Children.Add(new TextBlock
            {
                Text = enc.Subject ?? $"Enc #{enc.Id}", FontSize = 13, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#0D47A1"), TextAlignment = TextAlignment.Center
            });

            // Show current encounter's preconditions below the title
            if (!string.IsNullOrWhiteSpace(enc.PreConditions))
            {
                var preList = enc.PreConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                if (preList.Count > 0)
                {
                    var preWrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    foreach (var p in preList)
                    {
                        var isNeg = p.StartsWith("-");
                        var rid = isNeg ? p[1..] : p;
                        var cond = _vis.Resolver.LookupRef<Condition>(enc, nameof(Encounter.PreConditions), p);
                        var label = (isNeg ? "¬" : "") + (cond?.Subject ?? rid);
                        var bg = isNeg ? "#FFF3E0" : "#E8F5E9";
                        var fg = isNeg ? "#E65100" : "#2E7D32";
                        preWrap.Children.Add(new Border
                        {
                            CornerRadius = new CornerRadius(3),
                            Background = Brush.Parse(bg),
                            Padding = new Thickness(4, 1),
                            Margin = new Thickness(1),
                            Child = new TextBlock
                            {
                                Text = label, FontSize = 7, Foreground = Brush.Parse(fg),
                                TextDecorations = isNeg ? TextDecorations.Strikethrough : null
                            }
                        });
                    }
                    rootSp.Children.Add(preWrap);
                }
            }

            rootNode.Child = rootSp;
            centerColumn.Children.Add(rootNode);
        }
        treePanel.Children.Add(centerColumn);

        // Arrow: center → right
        treePanel.Children.Add(new TextBlock
        {
            Text = "→", FontSize = 16, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center
        });

        // RIGHT column: Branch nodes (stacked vertically)
        // Build initial branch nodes
        BuildBranchNodes(branchesPanel, selectedPreConds);
        treePanel.Children.Add(branchesPanel);

        // ── Combine story branch content ──
        var storyBranchContent = new StackPanel();

        // PreCondition checkbox panel (at top of story branch tab)
        if (preCondPanel.Children.Count > 0)
            storyBranchContent.Children.Add(preCondPanel);

        // Visual tree diagram (horizontally scrollable, left→right layout)
        storyBranchContent.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
            Content = _vis.Card(treePanel)
        });

        // ── Build recursive encounter chain (depth-limited, dedup) ──
        var forwardChain = BuildEncounterChainTree(enc, new HashSet<int>(), 0, 6);
        var reverseChain = BuildReverseChainPanel(enc);

        // Reverse chain (previous encounters → current, with dedup and position mark)
        if (reverseChain is not null)
            storyBranchContent.Children.Add(reverseChain);

        // ── TabControl: 剧情分支 | 剧情链 | Mermaid源码 ──
        var tabControl = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        var storyTab = new TabItem
        {
            Header = _vis.Loc("Vis.StoryBranch"),
            Content = storyBranchContent
        };
        var chainTab = new TabItem { Header = _vis.Loc("Vis.EncounterChain"), Content = forwardChain };

        // Mermaid tab: only raw source code
        var mermaidTabContent = new StackPanel { Spacing = 6 };
        mermaidTabContent.Children.Add(mermaidBlock);
        var mermaidTab = new TabItem
        {
            Header = _vis.Loc("Vis.MermaidSource"),
            Content = mermaidTabContent
        };
        tabControl.Items.Add(storyTab);
        tabControl.Items.Add(chainTab);
        tabControl.Items.Add(mermaidTab);
        tabControl.SelectedIndex = 0;

        sp.Children.Add(tabControl);
        return sp;
    }

    // ═══ Recursive encounter chain tree (dedup, depth-limited) ═══
    private Control BuildEncounterChainTree(Encounter root, HashSet<int> visited, int depth, int maxDepth)
    {
        if (depth > maxDepth || !visited.Add(root.Id))
            return new StackPanel(); // dedup: already visited

        var sp = new StackPanel();
        if (depth == 0)
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.EncounterChain")));
        }

        var responseList = ParseResponseEntries(root.Responses, root);
        var marginLeft = depth * 24;

        // Current node
        var nodePanel = new StackPanel { Spacing = 4, Margin = new Thickness(marginLeft, 4, 0, 4) };
        var isCurrent = depth == 0;

        // Collect entity context for this node
        var contextBadges = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
        if (!string.IsNullOrWhiteSpace(root.TreasureId) && root.TreasureId != "3")
        {
            var tt = _vis.Resolver.LookupRef<TreasureTable>(root, nameof(Encounter.TreasureId), root.TreasureId);
            if (tt is not null)
                contextBadges.Children.Add(_vis.MiniBadge($"🎒{tt.Name}", "#E8F5E9", "#2E7D32"));
        }
        if (root.CreatureId != "0")
        {
            // R30: nCreatureID → Creature (Doc 37 §4.11 / model annotation), not CreatureSource.
            var cs = _vis.Resolver.LookupRef<Creature>(root, nameof(Encounter.CreatureId), root.CreatureId);
            if (cs is not null)
                contextBadges.Children.Add(_vis.MiniBadge($"🐾{cs.Subject}", "#E8EAF6", "#283593"));
        }
        if (!string.IsNullOrWhiteSpace(root.Conditions) && root.Conditions != "1")
        {
            var condCount = root.Conditions.Split(',').Length;
            contextBadges.Children.Add(_vis.MiniBadge($"⚡{condCount} conditions", "#FCE4EC", "#C62828"));
        }
        if (!string.IsNullOrWhiteSpace(root.PreConditions))
        {
            var preCount = root.PreConditions.Split(',').Length;
            contextBadges.Children.Add(_vis.MiniBadge($"📋pre:{preCount}", "#E8F5E9", "#2E7D32"));
        }

        var nodeBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse(isCurrent ? "#E3F2FD" : "#F5F5F5"),
            BorderBrush = Brush.Parse(isCurrent ? "#1565C0" : "#E0E0E0"),
            BorderThickness = new Thickness(isCurrent ? 3 : 1),
            Padding = new Thickness(10, 4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    isCurrent
                        ? new TextBlock { Text = "📍 " + _vis.Loc("Vis.CurrentPosition"), FontSize = 8, Foreground = Brush.Parse("#1565C0") }
                        : new TextBlock(),
                    new TextBlock
                    {
                        Text = root.Subject ?? $"Enc #{root.Id}",
                        FontSize = isCurrent ? 12 : 11,
                        FontWeight = isCurrent ? FontWeight.Bold : FontWeight.Medium,
                        Foreground = Brush.Parse(isCurrent ? "#0D47A1" : "#555"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"ID: {root.Id} · Type: {(root.Type == EncounterType.Scavenge ? "Scavenge" : "Normal")}",
                        FontSize = 8, Foreground = Brush.Parse("#999")
                    },
                    contextBadges.Children.Count > 0 ? contextBadges : new TextBlock()
                }
            }
        };
        _refNode.WireNavigation(nodeBorder, typeof(Encounter), root.EntityId, root);
        nodePanel.Children.Add(nodeBorder);

        // Children (response targets)
        if (responseList.Count > 0 && depth < maxDepth)
        {
            var childrenPanel = new StackPanel { Spacing = 2 };
            foreach (var resp in responseList)
            {
                if (resp.TargetEncounter is null) continue;
                // Edge label: item + weight(probability)
                var edgeLabel = "";
                if (resp.Item is not null)
                    edgeLabel = $"🛡 {resp.Item.Description}{(resp.ItemMult > 1 ? $" ×{resp.ItemMult}" : "")} ";
                edgeLabel += $"→ {resp.Weight:F1}({resp.Probability:P2})";
                childrenPanel.Children.Add(new TextBlock
                {
                    Text = edgeLabel, FontSize = 9, Foreground = Brush.Parse("#888"),
                    Margin = new Thickness(marginLeft + 20, 1, 0, 1)
                });
                var childTree = BuildEncounterChainTree(resp.TargetEncounter, visited, depth + 1, maxDepth);
                if (childTree is StackPanel childSp && childSp.Children.Count > 0)
                    childrenPanel.Children.Add(childTree);
            }
            nodePanel.Children.Add(childrenPanel);
        }
        else if (responseList.Count == 0)
        {
            nodePanel.Children.Add(new TextBlock
            {
                Text = "(leaf)", FontSize = 9, Foreground = Brush.Parse("#CCC"),
                Margin = new Thickness(marginLeft + 20, 0, 0, 0)
            });
        }

        sp.Children.Add(nodePanel);
        return depth == 0
            ? new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 500,
                Content = _vis.Card(sp)
            }
            : sp;
    }

    // ═══ Reverse encounter chain (who references me) ═══
    private Control? BuildReverseChainPanel(Encounter enc)
    {
        var store = _dataTable?.ActiveMergeStore ?? _dataTable?.BrowserStore;
        if (store == null) return null;

        var rawRefs = store.IndexService?.ReverseLookup(enc.EntityId) ?? [];
        var refs = new List<(Encounter Source, string ViaItem)>();

        foreach (var (srcEid, propName, rawId) in rawRefs)
        {
            if (propName != nameof(Encounter.Responses)) continue;
            if (!(_dataTable?.ReferenceLookups ?? []).TryGetValue(typeof(Encounter), out var list) || list is null)
                continue;
            var src = list.OfType<Encounter>().FirstOrDefault(e => e.EntityId == srcEid);
            if (src is null) continue;
            refs.Add((src, rawId));
        }

        if (refs.Count == 0) return null;

        var sp = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        sp.Children.Add(_vis.SectionLabel($"👈 Referenced By ({refs.Count})"));
        var tree = BuildReverseChainTree(enc, refs, new HashSet<int>(), 0, 4);
        if (tree is StackPanel tsp && tsp.Children.Count > 0)
            sp.Children.Add(_vis.Card(tree));
        return sp;
    }

    private Control BuildReverseChainTree(Encounter target, List<(Encounter Source, string ViaItem)> refs,
        HashSet<int> visited, int depth, int maxDepth)
    {
        if (depth > maxDepth) return new StackPanel();

        var sp = new StackPanel { Spacing = 2 };
        var marginLeft = depth * 24;

        foreach (var (src, viaItem) in refs)
        {
            if (!visited.Add(src.Id)) continue;

            // Edge label (above node)
            sp.Children.Add(new TextBlock
            {
                Text = $"← {src.Subject ?? $"Enc #{src.Id}"} via Responses", FontSize = 9, Foreground = Brush.Parse("#888"),
                Margin = new Thickness(marginLeft + 12, 2, 0, 2)
            });

            // Source node
            var contextBadges = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
            if (!string.IsNullOrWhiteSpace(src.TreasureId) && src.TreasureId != "3")
            {
                var tt = _vis.Resolver.LookupRef<TreasureTable>(src, nameof(Encounter.TreasureId), src.TreasureId);
                if (tt is not null)
                    contextBadges.Children.Add(_vis.MiniBadge($"🎒{tt.Name}", "#E8F5E9", "#2E7D32"));
            }
            if (src.CreatureId != "0")
            {
                contextBadges.Children.Add(_vis.MiniBadge("🐾", "#E8EAF6", "#283593"));
            }

            var capturedSrc = src;
            var nodeBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = Brush.Parse(depth == 0 ? "#FFF8E1" : "#F5F5F5"),
                BorderBrush = Brush.Parse(depth == 0 ? "#F9A825" : "#E0E0E0"),
                BorderThickness = new Thickness(depth == 0 ? 2 : 1),
                Padding = new Thickness(10, 4),
                Margin = new Thickness(marginLeft, 0, 0, 0),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = src.Subject ?? $"Enc #{src.Id}",
                            FontSize = 11, FontWeight = FontWeight.Medium,
                            Foreground = Brush.Parse("#555"), TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = $"ID: {src.Id} · {(src.Type == EncounterType.Scavenge ? "Scavenge" : "Normal")}",
                            FontSize = 8, Foreground = Brush.Parse("#999")
                        },
                        contextBadges.Children.Count > 0 ? contextBadges : new TextBlock()
                    }
                }
            };
            _refNode.WireNavigation(nodeBorder, typeof(Encounter), src.EntityId, target);
            sp.Children.Add(nodeBorder);

            // Recursively find who references this source
            var store = _dataTable?.ActiveMergeStore ?? _dataTable?.BrowserStore;
            if (store is not null && depth < maxDepth)
            {
                var subRefs = new List<(Encounter Source, string ViaItem)>();
                var rawSubRefs = store.IndexService?.ReverseLookup(src.EntityId) ?? [];
                foreach (var (subSrcEid, subPropName, subRawId) in rawSubRefs)
                {
                    if (subPropName != nameof(Encounter.Responses)) continue;
                    if (!(_dataTable?.ReferenceLookups ?? []).TryGetValue(typeof(Encounter), out var elist) || elist is null)
                        continue;
                    var subSrc = elist.OfType<Encounter>().FirstOrDefault(e => e.EntityId == subSrcEid);
                    if (subSrc is null) continue;
                    subRefs.Add((subSrc, subRawId));
                }
                if (subRefs.Count > 0)
                {
                    var childTree = BuildReverseChainTree(src, subRefs, visited, depth + 1, maxDepth);
                    if (childTree is StackPanel csp && csp.Children.Count > 0)
                        sp.Children.Add(childTree);
                }
            }
        }

        return sp;
    }

    private Control BuildRefsPanel(Encounter enc)
    {
        var sp = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(enc.TreasureId) && enc.TreasureId != "3")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.LootTable")));
            var wp = new WrapPanel();
            // M3: TreasureTable reference → RefNode badge
            wp.Children.Add(_refNode.Badge<TreasureTable>(enc, nameof(Encounter.TreasureId), enc.TreasureId,
                resolvedBg: "#E8F5E9", resolvedFg: "#2E7D32",
                unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.RemoveTreasureId) && enc.RemoveTreasureId != "3")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.RemoveSubmit")));
            var wp = new WrapPanel();
            // M3: RemoveTreasureTable reference → RefNode badge
            wp.Children.Add(_refNode.Badge<TreasureTable>(enc, nameof(Encounter.RemoveTreasureId), enc.RemoveTreasureId,
                resolvedBg: "#FFEBEE", resolvedFg: "#C62828",
                unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.ItemsId) && enc.ItemsId != "0")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.GiveItem")));
            var wp = new WrapPanel();
            // R30: nItemsID → ItemType {Id}（Doc 37 §4.11；round28 已改默认 {Id}）
            wp.Children.Add(_refNode.Badge<ItemType>(enc, nameof(Encounter.ItemsId), enc.ItemsId,
                resolvedBg: "#E3F2FD", resolvedFg: "#1565C0",
                unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.Loot) && enc.Loot != "0")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Loot")));
            var wp = new WrapPanel();
            // R30: vLoot → TreasureTable {Id}（Doc 37 §4.11）
            wp.Children.Add(_refNode.Badge<TreasureTable>(enc, nameof(Encounter.Loot), enc.Loot,
                resolvedBg: "#E8F5E9", resolvedFg: "#2E7D32",
                unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.Conditions) && enc.Conditions != "1")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Conditions")));
            var wp = new WrapPanel();
            foreach (var seg in enc.Conditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = _vis.Resolver.LookupRef<Condition>(enc, nameof(Encounter.Conditions), seg);
                if (cond is not null)
                    wp.Children.Add(_refNode.BadgeForEntity(enc, cond, cond.Subject!,
                        "#FCE4EC", "#C62828"));
                else
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
            }

            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.PreConditions))
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.PreConditions")));
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

            sp.Children.Add(_vis.Card(wp));
        }

        if (enc.CreatureId != "0")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.SpawnCreature")));
            var wp = new WrapPanel();
            // R30: nCreatureID → Creature (Doc 37 §4.11 / model annotation), not CreatureSource.
            wp.Children.Add(_refNode.Badge<Creature>(enc, nameof(Encounter.CreatureId), enc.CreatureId,
                resolvedBg: "#E8EAF6", resolvedFg: "#283593",
                unresolvedBg: "#F5F5F5", unresolvedFg: "#999"));
            if (!string.IsNullOrWhiteSpace(enc.CreatureHex) && enc.CreatureHex != "0,0")
                wp.Children.Add(new TextBlock
                {
                    Text = $" at {enc.CreatureHex}", FontSize = 10, Foreground = Brush.Parse("#999"),
                    VerticalAlignment = VerticalAlignment.Center
                });

            sp.Children.Add(_vis.Card(wp));
        }

        if (!string.IsNullOrWhiteSpace(enc.Teleport) && enc.Teleport != "0,0")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Teleport")));
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = $"Destination: ({enc.Teleport})", FontSize = 11, Foreground = Brush.Parse("#6A1B9A") }));
        }

        if (!string.IsNullOrWhiteSpace(enc.Accidents) && enc.Accidents != "1")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.Accidents")));
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

            sp.Children.Add(_vis.Card(wp));
        }

        // R48: minimap markers "x,y=label" — where this encounter pins the minimap.
        if (!string.IsNullOrWhiteSpace(enc.MinimapHexes))
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.MinimapHexes")));
            var wp = new WrapPanel();
            foreach (var seg in enc.MinimapHexes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var eqIdx = seg.IndexOf('=');
                var pos = eqIdx > 0 ? seg[..eqIdx].Trim() : seg;
                var label = eqIdx > 0 ? seg[(eqIdx + 1)..].Trim() : null;
                var text = string.IsNullOrEmpty(label) ? $"({pos})" : $"({pos}) {label}";
                wp.Children.Add(_vis.MiniBadge(text, "#FFF8E1", "#F57F17"));
            }
            sp.Children.Add(_vis.Card(wp));
        }

        // R48: editor-only placement (game ignores) — keep visible but de-emphasized.
        if (!string.IsNullOrWhiteSpace(enc.Editor) && enc.Editor != "0,0")
        {
            sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.EditorPos")));
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = $"({enc.Editor})", FontSize = 11, Foreground = Brush.Parse("#999") }));
        }

        return sp;
    }

    private Control BuildTriggersPanel(List<EncounterTrigger> triggers)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel($"{_vis.Loc("Vis.TriggeredBy")} ({triggers.Count})"));
        var wp = new WrapPanel();
        foreach (var trigger in triggers)
            wp.Children.Add(_refNode.BadgeForEntity(default!, trigger, trigger.Name!,
                "#F3E5F5", "#6A1B9A"));
        sp.Children.Add(_vis.Card(wp));
        return sp;
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
