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

        root.Children.Add(BuildHeroHeader(enc));
        if (!string.IsNullOrWhiteSpace(enc.Description))
            root.Children.Add(BuildStoryPanel(enc));
        // D06 §六: the standalone ResponsesPanel is merged into the story-branch
        // diagram — one render of the response data instead of two.
        if (!string.IsNullOrWhiteSpace(enc.Responses))
            root.Children.Add(BuildStoryBranchDiagram(enc));

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
        // D06 §4.2: shared type-chip mapping (raw value 0-3, grey fallback).
        var (typeLabel, typeBg, typeFg) = TypeChip((int)enc.Type);
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

    // ═══════════════ Story Branch Diagram (D06: single node card + shared data model) ═══════════════

    /// <summary>
    /// D06 §4.5: the single branch data model — both the node cards and the
    /// Mermaid source are generated from it, so the two renderings cannot drift.
    /// <c>EffectiveProb == 0</c> means the branch is filtered out by the active
    /// pre-condition checkboxes. <c>PreConds</c> is the target encounter's
    /// precondition list resolved for display (Raw preserves the ¬ prefix).
    /// </summary>
    internal sealed record BranchData(
        int TargetId,
        Encounter? Target,
        string? ItemId,
        double ItemMult,
        ItemType? Item,
        double Weight,
        double EffectiveProb,
        bool IsSatisfied,
        List<(string Raw, bool IsNeg, Condition? Resolved)> PreConds);

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
        Encounter? Source = null);

    /// <summary>
    /// D06 §4.5: derive the branch model from the parsed responses (pure over
    /// <see cref="ParseResponseEntries"/> output). ValidTotalWeight sums only the
    /// branches whose pre-conditions are satisfied under <paramref name="activePreConds"/>.
    /// </summary>
    internal (List<BranchData> Branches, double ValidTotalWeight) PrepareBranches(Encounter enc, ISet<string> activePreConds)
    {
        var responses = ParseResponseEntries(enc.Responses, enc);
        return PrepareBranches(responses, activePreConds);
    }

    private (List<BranchData> Branches, double ValidTotalWeight) PrepareBranches(
        List<ResponseEntry> responses, ISet<string> activePreConds)
    {
        var branches = new List<BranchData>(responses.Count);
        double validTotalWeight = 0;
        foreach (var resp in responses)
        {
            var preConds = ResolvePreConds(resp.TargetEncounter);
            var satisfied = AreAllPreCondsSatisfied(preConds, activePreConds);
            if (satisfied) validTotalWeight += resp.Weight;
            branches.Add(new BranchData(
                resp.TargetId, resp.TargetEncounter, resp.ItemId, resp.ItemMult, resp.Item,
                resp.Weight, 0.0, satisfied, preConds));
        }

        for (int i = 0; i < branches.Count; i++)
        {
            var b = branches[i];
            var effective = validTotalWeight > 0 && b.IsSatisfied ? b.Weight / validTotalWeight : 0.0;
            branches[i] = b with { EffectiveProb = effective };
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

        if (opts.IsCurrent)
            body.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.CurrentEncounter"), FontSize = 8, Foreground = Brush.Parse("#1565C0")
            });

        // R59 v2: title first (row 1), image second as the main body (~70% of card
        // width), then a chips row (ID + type left/center, probability right).
        body.Children.Add(new TextBlock
        {
            Text = e.Subject ?? $"Enc #{e.Id}", FontSize = 12, FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#333"), TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        });

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

        // Row 3: chips (left/center) | probability (right)
        var chipsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        chipsRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4), Background = Brush.Parse("#E3F2FD"), Padding = new Thickness(5, 1),
            Child = new TextBlock
            {
                Text = $"ID: {e.Id}", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        if (opts.Resolved)
        {
            var (typeLabel, typeBg, typeFg) = TypeChip((int)e.Type);
            chipsRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse(typeBg), Padding = new Thickness(5, 1),
                Child = new TextBlock
                {
                    Text = typeLabel, FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg)
                }
            });
        }

        // Probability pill (branch cards only) — right side of the chips row
        if (!opts.IsCurrent)
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

        // Branch cards: navigation + hover info tooltip (complex info lives here, not in the card)
        if (opts.Branch is { } branch)
        {
            if (branch.Target is not null)
                _refNode.WireNavigation(card, typeof(Encounter), branch.Target.EntityId, opts.Source);
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

        // R59 v2: item trigger goes right after the title (row 2), using Name
        // (not Description) — the name is the identity, description is flavor.
        if (b.Item is not null)
        {
            var qty = b.ItemMult > 1 ? $" ×{b.ItemMult}" : "";
            sp.Children.Add(_refNode.BadgeForEntity(source, b.Item,
                $"🛡 {b.Item.Name}{qty}", "#E3F2FD", "#1565C0"));
        }
        else if (b.ItemId is not null)
        {
            sp.Children.Add(_vis.MiniBadge($"Item #{b.ItemId}", "#F5F5F5", "#999"));
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
    /// D06 §七: Mermaid text built from the same BranchData source as the node
    /// cards. Nodes carry name+ID; edges carry item ×n | weight(effective%) with
    /// conditional [📋×n] / [⚠m/t] suffixes. No reverse nodes, no ctx labels.
    /// </summary>
    internal static string BuildMermaidText(IReadOnlyList<BranchData> branches, Encounter current, ISet<string> activePreConds)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("flowchart LR");

        var currentName = (current.Subject ?? $"Enc #{current.Id}").Replace("\"", "\\\"");
        sb.AppendLine($"    A[\"📍 {currentName} (#{current.Id})\"]");

        for (int i = 0; i < branches.Count; i++)
        {
            var b = branches[i];
            var targetName = (b.Target?.Subject ?? $"Enc #{b.TargetId}").Replace("\"", "\\\"");
            var edge = BuildMermaidEdgeLabel(b, activePreConds);
            sb.AppendLine($"    A -->|\"{edge}\"| B{i}[\"{targetName} (#{b.TargetId})\"]");
        }

        return sb.ToString();
    }

    private static string BuildMermaidEdgeLabel(BranchData b, ISet<string> activePreConds)
    {
        var label = b.Item is not null
            ? $"{b.Item.Description}{(b.ItemMult > 1 ? $" ×{b.ItemMult}" : "")}"
            : b.ItemId is not null
                ? $"#{b.ItemId}{(b.ItemMult > 1 ? $" ×{b.ItemMult}" : "")}"
                : "";
        if (label.Length > 0) label += " | ";
        label += $"{b.Weight.ToString("F1", CultureInfo.InvariantCulture)}({FormatProbability(b.EffectiveProb)})";
        if (b.PreConds.Count > 0)
            label += $"[📋{b.PreConds.Count}]";
        if (activePreConds.Count > 0 && b.PreConds.Count > 0)
        {
            var matched = b.PreConds.Count(p => IsPreCondSatisfied(p.Raw, activePreConds));
            if (matched < b.PreConds.Count)
                label += $"[⚠{matched}/{b.PreConds.Count}]";
        }
        return label.Replace("\"", "'");
    }

    private Control BuildStoryBranchDiagram(Encounter enc)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.StoryBranch")));

        // Response format hint — moved here from the merged ResponsesPanel (D06 §六)
        sp.Children.Add(new TextBlock
        {
            Text = "格式: [物品ID]x[数量]=[剧情ID]x[权重]  ·  空物品(=开头)=无需物品的选项  ·  概率=权重/权重和",
            FontSize = 9, Foreground = Brush.Parse("#AAA"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, -4, 0, 4)
        });

        var selectedPreConds = new HashSet<string>();

        // D06 §4.5: single data model shared by the cards and the Mermaid tab.
        var (branches, _) = PrepareBranches(enc, selectedPreConds);
        if (branches.Count == 0)
        {
            sp.Children.Add(_vis.Card(new TextBlock
                { Text = _vis.Loc("Vis.NoBranches"), FontSize = 11, Foreground = Brush.Parse("#999") }));
            return sp;
        }

        // ── Pre-condition filter checkboxes (union over branch targets) ──
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

        // ── Two-column diagram: current card | branch cards (one arrow per branch) ──
        // Declared before the checkbox panel so its handlers can call Refresh().
        var branchesPanel = new StackPanel
        {
            Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
        };

        var mermaidTextBlock = new TextBlock
        {
            Text = "", FontSize = 10, FontFamily = new FontFamily("Consolas, Menlo, monospace"),
            Foreground = Brush.Parse("#555"), TextWrapping = TextWrapping.NoWrap
        };

        var preCondPanel = new StackPanel();
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
                // Negated precond: ¬ prefix + strikethrough (existing style)
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
                    Content = cbContent, FontSize = 10, IsChecked = false, Margin = new Thickness(0, 0, 8, 2)
                };
                cb.IsCheckedChanged += (_, _) =>
                {
                    if (cb.IsChecked == true) selectedPreConds.Add(rawId);
                    else selectedPreConds.Remove(rawId);
                    Refresh();
                };
                cbPanel.Children.Add(cb);
            }
            preCondPanel.Children.Add(cbPanel);
        }

        // Rebuild branch cards + Mermaid from the same data model (filter refresh)
        void Refresh()
        {
            var (fresh, _) = PrepareBranches(enc, selectedPreConds);
            mermaidTextBlock.Text = BuildMermaidText(fresh, enc, selectedPreConds);
            branchesPanel.Children.Clear();
            foreach (var b in fresh)
            {
                var row = new StackPanel
                    { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(new TextBlock
                {
                    Text = "→", FontSize = 16, Foreground = Brush.Parse("#999"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                var cardEnc = b.Target ?? new Encounter { Id = b.TargetId, Name = $"Enc #{b.TargetId}" };
                row.Children.Add(BuildEncounterNodeCard(cardEnc, new NodeCardOptions(
                    IsCurrent: false,
                    Weight: b.Weight,
                    EffectiveProb: b.EffectiveProb,
                    Filtered: !b.IsSatisfied && selectedPreConds.Count > 0,
                    Resolved: b.Target is not null,
                    Branch: b,
                    ActivePreConds: selectedPreConds,
                    Source: enc)));
                branchesPanel.Children.Add(row);
            }
        }

        var treePanel = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        var leftCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        leftCol.Children.Add(BuildEncounterNodeCard(enc, new NodeCardOptions(IsCurrent: true)));
        treePanel.Children.Add(leftCol);
        treePanel.Children.Add(branchesPanel);

        Refresh();

        // ── Story branch content: filter panel + visual diagram ──
        var storyBranchContent = new StackPanel();
        if (preCondPanel.Children.Count > 0)
            storyBranchContent.Children.Add(preCondPanel);
        storyBranchContent.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 500,
            Content = _vis.Card(treePanel)
        });

        // ── TabControl: 剧情分支 | 剧情链 | Mermaid源码 ──
        var tabControl = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        var storyTab = new TabItem
        {
            Header = _vis.Loc("Vis.StoryBranch"),
            Content = storyBranchContent
        };
        var chainTab = new TabItem
        {
            Header = _vis.Loc("Vis.EncounterChain"),
            Content = BuildEncounterChainTree(enc, new HashSet<int>(), 0, 6)
        };

        // Mermaid tab: raw source only (refreshed together with the cards)
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
        var mermaidTab = new TabItem
        {
            Header = _vis.Loc("Vis.MermaidSource"),
            Content = new StackPanel { Spacing = 6, Children = { mermaidBlock } }
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
                    // D06 §4.2: chain tree uses the shared type-chip label too.
                    new TextBlock
                    {
                        Text = $"ID: {root.Id} · {TypeChip((int)root.Type).Label}",
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
