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

public class ItemTypeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(ItemType);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;
    private readonly IEntityLookupService _dataTable;

    /// <summary>Create with injected services.</summary>
    public ItemTypeEntityVisualizer(VisHelperService vis, Services.RefNode? refNode, IEntityLookupService? dataTable)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            _vis.Resolver,
            _vis.Router,
            _vis.BuildRefTooltip);
        _dataTable = dataTable!;
    }

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

    /// <summary>Get hand coordinate (cx, cy) for a hand-held slot.</summary>
    private static (int, int) GetHandPos(int slot)
        => slot == 20 || slot == 5 ? (180, 150) : slot == 21 || slot == 6 ? (60, 150) : (0, 0);

    /// <summary>Returns a human-readable slot name, treating 100~112 as wounds.</summary>
    private static string GetSlotName(int slotId)
    {
        if (SlotNames.TryGetValue(slotId, out var name)) return name;
        if (WoundNames.TryGetValue(slotId, out var wname)) return wname;
        return slotId.ToString();
    }

    // ═══════════════ Detail ═══════════════

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not ItemType it) return new TextBlock { Text = "Invalid" };
        // R40 layout: user mental model — "what it is" → "what I do with it
        // (equip / use / fight → damage, effects, SOUNDS)" → "how long it lasts"
        // → "what it holds / where it comes from". Blocks are grouped in a
        // two-column grid (no endless single-column stacking), each with an icon.
        var root = new StackPanel { Spacing = 14, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(it));
        root.Children.Add(BuildHeroHeader(it));

        // 情境 1（两列）：战斗 ⚔ | 装备 🧍
        AddRow(root,
            Section(_vis.Loc("Vis.Combat"), BuildCombatBody(it), Symbol.Flash, "#C62828"),
            Section(_vis.Loc("Vis.Equipment"), BuildEquipmentBody(it), Symbol.Person, "#1565C0"));

        // 情境 2（两列）：使用效果 ✨ | 耐久与弹药 ⏳
        AddRow(root,
            Section(_vis.Loc("Vis.Effects"), BuildEffectsBody(it), Symbol.Beaker, "#E65100"),
            Section(_vis.Loc("Vis.Lifecycle"), BuildLifecycleBody(it), Symbol.Timer, "#6A1B9A"));

        // 情境 3（两列）：容器 📦 | 来源与产出 🔗
        AddRow(root,
            Section(_vis.Loc("Vis.Container"), BuildContainerBody(it), Symbol.Box, "#00695C"),
            Section(_vis.Loc("Vis.Associations"), BuildAssociationsBody(it), Symbol.Link, "#283593"));

        // 被引用（横贯底部）
        root.Children.Add(BuildReverseRefsPanel(it));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    /// <summary>R40: one detail block = icon SectionHeader + Card body; null body skips the block.</summary>
    private Control? Section(string title, Control? body, Symbol icon, string accent)
        => body is null ? null
            : new StackPanel { Spacing = 8, Children = { _vis.SectionHeader(title, icon, accent: accent), _vis.Card(body) } };

    /// <summary>R40: place left/right blocks side by side; a missing block lets the other span the row.</summary>
    private static void AddRow(StackPanel root, Control? left, Control? right)
    {
        if (left is null && right is null) return;
        if (left is null) { root.Children.Add(right!); return; }
        if (right is null) { root.Children.Add(left); return; }
        var row = new Grid
        {
            ColumnDefinitions = { new(1, GridUnitType.Star), new(1, GridUnitType.Star) },
            ColumnSpacing = 14
        };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        row.Children.Add(left);
        row.Children.Add(right);
        root.Children.Add(row);
    }

    // ═══════════════ Hero header: switchable image gallery (left) + identity (right) ═══════════════

    private Control BuildHeroHeader(ItemType it)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var imageListRaw = it.ImageList.ToRawString(",");
        var imageNames = imageListRaw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        var isImageList = imageListRaw.Contains(',');

        // ── Image area (top-left) ──
        var imageArea = new Border
        {
            Width = 132, Height = 132,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Background = Brush.Parse("#0A000000"),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (!isImageList && imageNames.Count == 1)
        {
            var bmp = _vis.LoadImage(imageNames[0]);
            if (bmp is not null)
            {
                imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
                var b = bmp;
                imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(b, it.Name);
                // Pixel size badge — top-right
                var sizeBadge = new TextBlock
                {
                    Text = $"{bmp.PixelSize.Width}×{bmp.PixelSize.Height}",
                    FontSize = 8, Foreground = Brush.Parse("#aaa"),
                    Margin = new Thickness(0, 2, 4, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top
                };
                // Wrap image + badge in a Grid overlay
                var overlay = new Grid();
                overlay.Children.Add(imageArea);
                overlay.Children.Add(sizeBadge);
                imageArea = new Border
                {
                    Width = 132, Height = 132,
                    CornerRadius = new CornerRadius(10),
                    ClipToBounds = true,
                    Background = Brush.Parse("#0A000000"),
                    VerticalAlignment = VerticalAlignment.Top,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = overlay
                };
            }
        }
        else if (imageNames.Count > 0)
            imageArea.Child = BuildImageGallery(imageNames);

        Grid.SetColumn(imageArea, 0);
        Grid.SetRowSpan(imageArea, 2);
        grid.Children.Add(imageArea);

        // ── Identity (right) ──
        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = $"{it.GroupId}.{it.SubgroupId}", FontSize = 11, FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#1565C0")
            }
        });
        _vis.AddModBadge(it, idRow);
        identity.Children.Add(idRow);
        identity.Children.Add(new TextBlock
            { Text = it.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(it.Description) && it.Description != it.Name)
            identity.Children.Add(new TextBlock
            {
                Text = it.Description, FontSize = 12, Foreground = Brush.Parse("#666666"),
                TextWrapping = TextWrapping.Wrap
            });
        if (!string.IsNullOrWhiteSpace(it.DescriptionAlt))
            identity.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#FFF3E0"),
                Padding = new Thickness(8, 3),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new StackPanel
                {
                    Spacing = 1, Children =
                    {
                        new TextBlock
                        {
                            Text = $"✦ {_vis.Loc("Vis.Identified")}", FontSize = 9,
                            Foreground = Brush.Parse("#E65100")
                        },
                        new TextBlock
                        {
                            Text = it.DescriptionAlt, FontSize = 11, Foreground = Brush.Parse("#BF360C"),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            });

        // R40: key numbers live INSIDE the identity column (no cross-row placement,
        // which misaligned under implicit grid rows) — weight / value / stack / flags.
        var statRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
        if (it.Weight > 0)
            statRow.Children.Add(StatChip($"{it.Weight:F1} kg", "#4CAF50"));
        if (it.MonetaryValue > 0)
        {
            var vt = it.MonetaryValueAlt > 0 && it.MonetaryValueAlt != it.MonetaryValue
                ? $"${it.MonetaryValue:F2} → ${it.MonetaryValueAlt:F2}"
                : $"${it.MonetaryValue:F2}";
            statRow.Children.Add(StatChip(vt, "#9C27B0"));
        }
        if (it.StackLimit > 0)
            statRow.Children.Add(StatChip($"×{it.StackLimit}", "#2196F3"));
        if (it.Mirrored)
            statRow.Children.Add(StatChip(_vis.Loc("Vis.Mirrored"), "#607D8B"));
        if (it.SlotDepth > 0)
            statRow.Children.Add(StatChip($"{_vis.Loc("Vis.SlotDepth")} {it.SlotDepth}", "#546E7A"));
        if (statRow.Children.Count > 0)
            identity.Children.Add(statRow);

        Grid.SetColumn(identity, 1);
        Grid.SetRow(identity, 0);
        Grid.SetRowSpan(identity, 2);
        grid.Children.Add(identity);

        return _vis.Card(grid);
    }

    /// <summary>R39: compact stat chip for the hero key-number row.</summary>
    private static Control StatChip(string text, string color)
        => new TextBlock
        {
            Text = text, FontSize = 11, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(color), VerticalAlignment = VerticalAlignment.Center
        };

    private Control BuildImageGallery(List<string> names)
    {
        var idx = 0;
        var bmps = names.Select(_vis.LoadImage).Where(b => b is not null).Cast<Bitmap>().ToList();
        if (bmps.Count == 0)
            return new TextBlock
            {
                Text = "No images", FontSize = 10, Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
            };

        var imageView = new Image { Source = bmps[0], Stretch = Stretch.Uniform, Width = 132, Height = 106 };

        // Navigation dots + prev/next
        var nav = new DockPanel { Height = 26, Background = Brush.Parse("#14000000"), LastChildFill = true };
        var prevBtn = new Button
        {
            Content = "◀", FontSize = 9, Padding = new Thickness(4, 0), Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        var nextBtn = new Button
        {
            Content = "▶", FontSize = 9, Padding = new Thickness(4, 0), Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        var dotPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Spacing = 4
        };
        var dots = new List<Border>();
        for (int i = 0; i < bmps.Count; i++)
        {
            var dot = new Border
            {
                Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                Background = i == 0 ? Brush.Parse("#666") : Brush.Parse("#CCC")
            };
            dots.Add(dot);
            dotPanel.Children.Add(dot);
        }

        var sizeLabel = new TextBlock
            { FontSize = 8, Foreground = Brush.Parse("#aaa"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0) };
        sizeLabel.Text = $"{bmps[0].PixelSize.Width}×{bmps[0].PixelSize.Height}";

        void UpdateView(int newIdx)
        {
            idx = ((newIdx % bmps.Count) + bmps.Count) % bmps.Count;
            imageView.Source = bmps[idx];
            for (int i = 0; i < dots.Count; i++) dots[i].Background = Brush.Parse(i == idx ? "#666" : "#CCC");
            sizeLabel.Text = $"{bmps[idx].PixelSize.Width}×{bmps[idx].PixelSize.Height}";
        }

        prevBtn.Click += (_, _) => UpdateView(idx - 1);
        nextBtn.Click += (_, _) => UpdateView(idx + 1);
        DockPanel.SetDock(prevBtn, Avalonia.Controls.Dock.Left);
        DockPanel.SetDock(nextBtn, Avalonia.Controls.Dock.Right);
        nav.Children.Add(prevBtn);
        nav.Children.Add(nextBtn);
        nav.Children.Add(sizeLabel);
        nav.Children.Add(dotPanel);

        imageView.Cursor = new Cursor(StandardCursorType.Hand);
        imageView.PointerPressed += (_, _) =>
        {
            if (bmps.Count > 0 && idx < bmps.Count)
                _vis.OpenZoomableImage(bmps[idx]);
        };

        var gallery = new DockPanel();
        var imgCapture = new Avalonia.Controls.DockPanel();
        imgCapture.Children.Add(imageView);
        gallery.Children.Add(nav);
        DockPanel.SetDock(nav, Avalonia.Controls.Dock.Bottom);
        gallery.Children.Add(imgCapture);

        return gallery;
    }

    // ═══════════════ Lifecycle body: durability + loss rates + break parts + ammo ═══════════════
    // R39: merged from old BasicCard (durability/break parts) + CombatCard (charge profiles).

    private Control BuildLifecycleBody(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        // Durability — R36: bar first (how much is left), then loss rates.
        // R41: low-saturation fills (Material 300-400) instead of jarring full reds/greens.
        if (it.Durability > 0)
        {
            var dt = it.Durability >= 999 ? "∞" : $"{it.Durability * 100:F0}%";
            var ratio = it.Durability >= 999 ? 1.0 : Math.Clamp(it.Durability, 0.05, 1.0);
            var color = it.Durability >= 999 ? "#90A4AE" : ratio > 0.5 ? "#66BB6A" : ratio > 0.25 ? "#FFB74D" : "#E57373";
            body.Children.Add(_vis.StatBar(_vis.Loc("Vis.Durability"), dt, ratio, color));
            hasAny = true;
        }
        if (it.DegradePerHour > 0)
            body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.PerHour"), $"{it.DegradePerHour:F3}", "#E65100"));
        if (it.EquipDegradePerHour > 0)
            body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.PerHourEquipped"), $"{it.EquipDegradePerHour:F3}", "#C62828"));
        if (it.DegradePerUse > 0)
            body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.PerUse"), $"{it.DegradePerUse:F3}", "#F57F17"));

        // R42: lifespan projection — translate loss RATES into "how long it lasts",
        // the question a modder actually asks when balancing.
        if (it.Durability > 0 && it.Durability < 999)
        {
            var spans = new List<string>();
            if (it.DegradePerHour > 0)
                spans.Add($"{_vis.Loc("Vis.PerHour")} ≈{(it.Durability / it.DegradePerHour):F0}h");
            if (it.EquipDegradePerHour > 0)
                spans.Add($"{_vis.Loc("Vis.PerHourEquipped")} ≈{(it.Durability / it.EquipDegradePerHour):F0}h");
            if (it.DegradePerUse > 0)
                spans.Add($"{_vis.Loc("Vis.PerUse")} ≈{(it.Durability / it.DegradePerUse):F0}×");
            if (spans.Count > 0)
                body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.Lifespan"),
                    string.Join(" · ", spans), "#546E7A"));
        }

        // Break parts — what falls out when the item breaks.
        var ttIds = it.DegradeTreasureIds.ToRawString(",").Split(',').Select(s => s.Trim())
            .Where(s => s.Length > 0 && s != "3").ToList();
        if (ttIds.Count > 0)
        {
            body.Children.Add(new Border { Height = 1, Background = Brush.Parse("#10000000"), Margin = new Thickness(0, 2) });
            var breakBody = new StackPanel { Spacing = 6 };
            breakBody.Children.Add(new TextBlock
                { Text = _vis.Loc("Vis.BreakParts"), FontSize = 10, Foreground = Brush.Parse("#999") });
            foreach (var seg in ttIds)
            {
                var tt = _vis.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.DegradeTreasureIds), seg);
                if (tt is null) continue;
                var itemBody = new StackPanel { Spacing = 2 };
                var t = tt;
                var breakHeader = new TextBlock
                {
                    Text = t.Subject ?? t.Name ?? $"TT#{t.Id}", FontSize = 11,
                    Foreground = Brush.Parse("#795548")
                };
                _refNode.WireNavigation(breakHeader, typeof(TreasureTable), t.EntityId, it);
                itemBody.Children.Add(breakHeader);
                if (!string.IsNullOrWhiteSpace(tt.Treasures))
                {
                    var lt = BuildTreasureLootTree(tt);
                    lt.Margin = new Thickness(8, 0, 0, 0);
                    itemBody.Children.Add(lt);
                }
                breakBody.Children.Add(itemBody);
            }
            body.Children.Add(breakBody);
            hasAny = true;
        }

        // Ammo — charge profiles the attack modes consume.
        if (it.ChargeProfiles.Count > 0)
        {
            body.Children.Add(new Border { Height = 1, Background = Brush.Parse("#10000000"), Margin = new Thickness(0, 2) });
            var wp = new WrapPanel();
            foreach (var seg in it.ChargeProfiles.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<ChargeProfile>(it,
                    nameof(ItemType.ChargeProfiles), seg,
                    "#E0F7FA", "#006064"));
            }
            body.Children.Add(wp);
            hasAny = true;
        }

        return hasAny ? body : new StackPanel();
    }

    // ═══════════════ Combat body: total damage stack + attack-mode rows ═══════════════
    // R36: Doc 21 §7 P3 stacked damage bar + inline AttackMode expansion.
    // R39: Properties → Effects block, ChargeProfiles → Lifecycle block.

    private static bool HasCombat(ItemType it) => it.AttackModes.Count > 0;

    private Control BuildCombatBody(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };

        // AttackModes (R04: RefNode handles resolution + navigation + peek)
        var modes = ParseAttackModes(it);
        if (modes.Count > 0)
        {
            // R36: total damage composition first — "slashing or blunt weapon?"
            // is answerable without opening any row. The bar shows the CUT:BLUNT
            // RATIO (meaningful proportion); numbers carry the values.
            var totalCut = modes.Sum(m => m.Mode.DamageCut);
            var totalBlunt = modes.Sum(m => m.Mode.DamageBlunt);
            body.Children.Add(_vis.StackedDamageBar(_vis.Loc("Vis.TotalDamage"), totalCut, totalBlunt));

            // R41: morale-adjusted total as a METRIC VALUE (no bar — a fill ratio
            // without a comparison baseline is meaningless).
            var totalBase = totalCut + totalBlunt;
            var totalEffective = modes.Sum(m => (m.Mode.DamageCut + m.Mode.DamageBlunt) * (1 + m.Mode.Morale));
            if (totalEffective > totalBase + 0.001)
            {
                body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.Effective"),
                    $"{totalEffective:F1} (×{totalEffective / Math.Max(totalBase, 0.01):F2})", "#9575CD"));
            }

            foreach (var (slotName, am) in modes)
                body.Children.Add(BuildAttackModeRow(it, slotName, am));
        }

        return body;
    }

    private sealed record AttackModeSlot(string SlotName, AttackMode Mode);

    /// <summary>Parse aAttackModes segments like "20=14" or "14" → (slot name, AttackMode).</summary>
    private List<AttackModeSlot> ParseAttackModes(ItemType it)
    {
        var result = new List<AttackModeSlot>();
        if (it.AttackModes.Count == 0) return result;

        foreach (var seg in it.AttackModes.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var eqIdx = seg.IndexOf('=');
            var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
            var slotName = int.TryParse(slotPart, out var sn) ? GetSlotName(sn) : slotPart;

            var am = _vis.Resolver.LookupRef<AttackMode>(it, nameof(ItemType.AttackModes), seg);
            if (am is null)
            {
                // unresolved — keep the raw segment visible (grey row, no expand)
                result.Add(new AttackModeSlot(slotName, new AttackMode
                {
                    EntityId = $"__unresolved_{seg}",
                    Name = seg,
                    DamageCut = 0,
                    DamageBlunt = 0
                }));
                continue;
            }
            result.Add(new AttackModeSlot(slotName, am));
        }
        return result;
    }

    /// <summary>One attack-mode row: name | stacked damage | range/pen/sound | expand toggle.</summary>
    private Control BuildAttackModeRow(ItemType it, string slotName, AttackMode am)
    {
        var detail = BuildAttackModeExpanded(it, am);
        detail.IsVisible = false;
        var arrow = new TextBlock
        {
            Text = "▶", FontSize = 10, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var isUnresolved = am.EntityId.StartsWith("__unresolved_", StringComparison.Ordinal);

        var nameTb = new TextBlock
        {
            Text = string.IsNullOrEmpty(slotName) ? (am.Subject ?? am.Name) : $"{slotName}: {am.Subject ?? am.Name}",
            FontSize = 12,
            FontWeight = isUnresolved ? FontWeight.Normal : FontWeight.Medium,
            Foreground = Brush.Parse(isUnresolved ? "#999" : "#333"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 220
        };
        if (!isUnresolved)
        {
            _refNode.WireNavigation(nameTb, typeof(AttackMode), am.EntityId, am);
            ToolTip.SetTip(nameTb, _vis.BuildRefTooltip(am));
        }

        var meta = new TextBlock
        {
            FontSize = 10, Foreground = Brush.Parse("#777"), VerticalAlignment = VerticalAlignment.Center,
            Text = string.Join(" · ",
                new[]
                {
                    am.Range > 1 || am.Type == AttackType.Ranged ? $"{_vis.Loc("Vis.Range")} {am.Range}" : null,
                    am.Penetration > 0 ? $"{_vis.Loc("Vis.Penetration")} {am.Penetration}" : null,
                    // R37: weapon morale modifier — affects actual damage
                    // ((1+士气+此值)×(1+加成)×武器伤害, Doc 38) — show it right on the row.
                    am.Morale != 0 ? $"{_vis.Loc("Morale")} {am.Morale:+0%;-0%;0}" : null,
                    !string.IsNullOrWhiteSpace(am.Sound) && am.Sound != "cueNone" ? $"{am.Sound}" : null
                }.Where(s => s is not null))
        };

        // R42: play the attack sound right from the row (when extracted assets exist).
        var playBtn = !string.IsNullOrWhiteSpace(am.Sound) && am.Sound != "cueNone"
            ? _vis.PlaySoundButton(am.Sound)
            : null;

        var row = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(1, GridUnitType.Star), new(GridLength.Auto) },
            Margin = new Thickness(0, 1)
        };
        Grid.SetColumn(nameTb, 0);
        row.Children.Add(nameTb);
        var bar = _vis.StackedDamageBar("", am.DamageCut, am.DamageBlunt);
        Grid.SetColumn(bar, 1);
        row.Children.Add(bar);
        var rightPanel = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        rightPanel.Children.Add(meta);
        if (playBtn is not null)
            rightPanel.Children.Add(playBtn);
        rightPanel.Children.Add(arrow);
        Grid.SetColumn(rightPanel, 2);
        row.Children.Add(rightPanel);

        var expanded = false;
        var header = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#0A000000"),
            Padding = new Thickness(10, 5),
            Cursor = isUnresolved ? null : new Cursor(StandardCursorType.Hand),
            Child = row
        };
        if (!isUnresolved)
        {
            header.PointerPressed += (_, e) =>
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0) return; // Ctrl+Click = navigate (WireNavigation on name)
                expanded = !expanded;
                arrow.Text = expanded ? "▼" : "▶";
                detail.IsVisible = expanded;
            };
        }

        var sp = new StackPanel { Spacing = 2, Children = { header, detail } };
        return sp;
    }

    /// <summary>Inline AttackMode detail: icon, damage (incl. morale), ammo, conditions, phrases, notes.</summary>
    private Control BuildAttackModeExpanded(ItemType it, AttackMode am)
    {
        var sp = new StackPanel { Spacing = 8, Margin = new Thickness(14, 4, 4, 2) };

        // ── R41: one compact top row — small icon (never a full row of its own) +
        //     type + morale modifier + effective damage, all as METRIC VALUES
        //     (fill-ratio bars without a comparison baseline are meaningless).
        var topRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var imgRaw = am.Image.ToRawString(",");
        if (!string.IsNullOrWhiteSpace(imgRaw))
        {
            var bmp = _vis.LoadImage(imgRaw);
            if (bmp is not null)
            {
                topRow.Children.Add(new Border
                {
                    Width = 36, Height = 36, CornerRadius = new CornerRadius(5),
                    Background = Brush.Parse("#0A000000"), ClipToBounds = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new Image { Source = bmp, Stretch = Stretch.Uniform }
                });
            }
        }
        topRow.Children.Add(new TextBlock
        {
            Text = am.Type == AttackType.Ranged ? _vis.Loc("Vis.CombatRanged") : _vis.Loc("Vis.CombatMelee"),
            FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#555"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var moralePct = (int)(am.Morale * 100);
        var moraleLabel = moralePct == 25 ? $"{moralePct}% (base)" : $"{moralePct}%";
        var moraleColor = am.Morale > 0.25 ? "#66BB6A" : am.Morale < 0.25 ? "#E57373" : "#78909C";
        topRow.Children.Add(new TextBlock
        {
            Text = $"{_vis.Loc("Morale")} {moraleLabel}", FontSize = 11, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(moraleColor), VerticalAlignment = VerticalAlignment.Center
        });

        var totalDmg = am.DamageCut + am.DamageBlunt;
        if (totalDmg > 0)
        {
            var effectiveDmg = totalDmg * (1 + am.Morale);
            topRow.Children.Add(new TextBlock
            {
                Text = $"{_vis.Loc("Vis.Effective")} {effectiveDmg:F1} (×{1 + am.Morale:F2})",
                FontSize = 11, FontWeight = FontWeight.Medium,
                Foreground = Brush.Parse("#9575CD"), VerticalAlignment = VerticalAlignment.Center
            });
        }
        sp.Children.Add(topRow);

        // R37 formula note — actual damage = (1 + character morale + weapon morale)
        // × (1 + melee/ranged bonus) × weapon damage (Doc 38 fMorale).
        if (totalDmg > 0)
        {
            sp.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.DamageFormula"),
                FontSize = 9, Foreground = Brush.Parse("#999"), TextWrapping = TextWrapping.Wrap
            });
        }

        // numeric row: Range / Penetration / Transfer
        var cells = new List<(string, string, string?)>();
        if (am.Range > 1 || am.Type == AttackType.Ranged)
            cells.Add((_vis.Loc("Vis.Range"), $"{am.Range}", "#1565C0"));
        if (am.Penetration > 0)
            cells.Add((_vis.Loc("Vis.Penetration"), $"{am.Penetration}", "#6A1B9A"));
        if (am.Transfer)
            cells.Add(("Transfer", _vis.Loc("Vis.Yes"), "#546E7A"));
        if (cells.Count > 0)
            sp.Children.Add(_vis.CreatureStatGrid(cells));

        // ammo: charge profiles with per-mode consumption
        if (!string.IsNullOrWhiteSpace(am.ChargeProfiles))
        {
            var wp = new WrapPanel();
            foreach (var seg in am.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cp = _vis.Resolver.LookupRef<ChargeProfile>(am, nameof(AttackMode.ChargeProfiles), seg);
                if (cp is not null)
                {
                    var rates = new List<string>();
                    if (cp.PerUse > 0) rates.Add($"{_vis.Loc("Vis.PerUse")} {cp.PerUse:F2}");
                    if (cp.PerHour > 0) rates.Add($"{_vis.Loc("Vis.PerHour")} {cp.PerHour:F2}");
                    if (cp.PerHourEquipped > 0) rates.Add($"{_vis.Loc("Vis.PerHourEquipped")} {cp.PerHourEquipped:F2}");
                    if (cp.PerHex > 0) rates.Add($"{_vis.Loc("Vis.PerHex")} {cp.PerHex:F2}");
                    var label = cp.Subject ?? cp.Name ?? $"CP#{cp.Id}";
                    if (rates.Count > 0) label += $" ({string.Join(" · ", rates)})";
                    if (cp.Degrade) label += " ⚠";
                    wp.Children.Add(_refNode.BadgeForEntity(am, cp, label, "#E0F7FA", "#006064"));
                }
                else
                {
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
                }
            }
            sp.Children.Add(LabeledSection(_vis.Loc("Vis.ChargeAmmo"), wp));
        }

        // attacker conditions (semantic colors)
        if (!string.IsNullOrWhiteSpace(am.AttackerConditions))
        {
            var wp = new WrapPanel();
            foreach (var seg in am.AttackerConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var cond = _vis.Resolver.LookupRef<Condition>(am, nameof(AttackMode.AttackerConditions), seg);
                if (cond is not null)
                {
                    var extra = ReferencePattern.FromName("{id}x{mult}").FormatExtraInfo(seg);
                    wp.Children.Add(_refNode.BadgeForEntity(am, cond,
                        ConditionLabel(cond, extra), ConditionBg(cond), ConditionFg(cond)));
                }
                else
                {
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
                }
            }
            sp.Children.Add(LabeledSection(_vis.Loc("Vis.AttackerConditions"), wp));
        }

        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
            sp.Children.Add(new TextBlock
            {
                Text = $"“{am.WieldPhrase}”", FontSize = 11, FontStyle = FontStyle.Italic,
                Foreground = Brush.Parse("#666"), TextWrapping = TextWrapping.Wrap
            });

        if (!string.IsNullOrWhiteSpace(am.AttackPhrases))
        {
            var wp = new WrapPanel();
            foreach (var p in am.AttackPhrases.Split(',', '，').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var display = p.Length > 60 ? p[..60] + "..." : p;
                wp.Children.Add(_vis.MiniBadge(display, "#E3F2FD", "#1565C0"));
            }
            sp.Children.Add(LabeledSection(_vis.Loc("Vis.AttackPhrases"), wp));
        }

        if (!string.IsNullOrWhiteSpace(am.Notes))
            sp.Children.Add(new TextBlock
            {
                Text = am.Notes, FontSize = 10, Foreground = Brush.Parse("#888"),
                TextWrapping = TextWrapping.Wrap
            });

        sp.Children.Add(new TextBlock
        {
            Text = _vis.Loc("Vis.CtrlClickHint"), FontSize = 9, Foreground = Brush.Parse("#999")
        });
        return sp;
    }

    // ═══════════════ Condition semantic colors (R36) ═══════════════

    /// <summary>Doc 21 §4-C color semantics: Fatal red / Permanent orange / Stackable green / Duration blue.</summary>
    private static string ConditionBg(Condition c)
        => c.Fatal ? "#FFEBEE" : c.Permanent ? "#FFF3E0" : c.Stackable ? "#E8F5E9" : "#E3F2FD";

    private static string ConditionFg(Condition c)
        => c.Fatal ? "#C62828" : c.Permanent ? "#E65100" : c.Stackable ? "#2E7D32" : "#1565C0";

    /// <summary>"Bleeding · FATAL" / "WellFed · 12h" — severity readable without opening the condition.</summary>
    private static string ConditionLabel(Condition c, string? extra)
    {
        var suffix = c.Fatal ? "FATAL"
            : c.Permanent ? "Instant"
            : c.Stackable ? "Stackable"
            : $"{c.Duration:F0}h";
        var label = $"{c.Subject} · {suffix}";
        return string.IsNullOrEmpty(extra) ? label : $"{label} ({extra})";
    }

    private static Control LabeledSection(string label, Control content)
    {
        return new StackPanel
        {
            Spacing = 2, Children =
            {
                new TextBlock { Text = label, FontSize = 10, Foreground = Brush.Parse("#999") },
                content
            }
        };
    }

    // ═══════════════ Equipment body — left: slots, right: wear preview ═══════════════
    // R39: conditions moved to the Effects block; preview stays with its slots.

    private static bool HasEquipment(ItemType it)
        => !string.IsNullOrWhiteSpace(it.EquipSlots) || !string.IsNullOrWhiteSpace(it.UseSlots) || it.SocketLocked;

    private Control BuildEquipmentBody(ItemType it)
    {
        // Parse equip slots
        var equipEntries = new List<(int Slot, int ImgIdx, int SpriteIdx, bool IsHandHeld)>();
        var equipBadges = new WrapPanel();
        if (!string.IsNullOrWhiteSpace(it.EquipSlots))
        {
            foreach (var seg in it.EquipSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                var parts = seg.Split('=');
                if (parts.Length >= 1 && int.TryParse(parts[0], out var slotNum))
                {
                    if (slotNum == -1) continue;
                    var hasSuffix = parts.Length >= 2;
                    var imgIdx = parts.Length >= 2 && int.TryParse(parts[1], out var i) ? i : 0;
                    var spriteIdx = parts.Length >= 3 && int.TryParse(parts[2], out var s) ? s : 0;
                    equipEntries.Add((slotNum, imgIdx, spriteIdx, isHandHeld: !hasSuffix));
                    equipBadges.Children.Add(_vis.MiniBadge(GetSlotName(slotNum), "#E3F2FD", "#1565C0"));
                }
            }
        }

        // Left column: slots
        var leftPanel = new StackPanel { Spacing = 8 };
        if (equipBadges.Children.Count > 0)
            leftPanel.Children.Add(LabeledSection(_vis.Loc("Vis.EquipSlots"), equipBadges));

        if (!string.IsNullOrWhiteSpace(it.UseSlots))
        {
            var wp = new WrapPanel();
            foreach (var s in it.UseSlots.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                wp.Children.Add(_vis.MiniBadge(s == "211" ? "Self" : s, "#E8EAF6", "#283593"));
            leftPanel.Children.Add(LabeledSection(_vis.Loc("Vis.UseSlots"), wp));
        }

        if (it.SocketLocked)
            leftPanel.Children.Add(LabeledSection(_vis.Loc("Vis.SocketLocked"),
                _vis.MiniBadge(_vis.Loc("Vis.SocketLockedDesc"), "#FFEBEE", "#C62828")));

        // R40: interaction sounds (pickup/putdown) belong to "what I do with it" —
        // the same mental context as equipping/using, not "associations".
        // R42: play button next to the cue names.
        if (!string.IsNullOrWhiteSpace(it.Sounds) && it.Sounds != "cuePickup,cuePutdown")
        {
            var sndRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            sndRow.Children.Add(_vis.MiniBadge(it.Sounds, "#ECEFF1", "#546E7A"));
            var sndBtn = _vis.PlaySoundButton(it.Sounds.Split(',')[0].Trim());
            if (sndBtn is not null)
            {
                sndRow.Children.Add(sndBtn);
                leftPanel.Children.Add(LabeledSection(_vis.Loc("Vis.Sound"), sndRow));
            }
            else
            {
                leftPanel.Children.Add(LabeledSection(_vis.Loc("Vis.Sound"),
                    _vis.MiniBadge(it.Sounds, "#ECEFF1", "#546E7A")));
            }
        }

        // Wear preview next to the slots when the item has equip entries.
        if (equipEntries.Count > 0)
        {
            var preview = BuildEquipSlotOverlay(it, equipEntries);
            var grid = new Grid
            {
                ColumnDefinitions = { new(1, GridUnitType.Star), new(GridLength.Auto) },
                Margin = new Thickness(0)
            };
            Grid.SetColumn(leftPanel, 0);
            grid.Children.Add(leftPanel);
            Grid.SetColumn(preview, 1);
            grid.Children.Add(preview);
            return grid;
        }

        return leftPanel;
    }

    /// <summary>Build a tabbed overlay preview (Image UI / Sprite UI) with checkbox toggles.</summary>
    private Control BuildEquipSlotOverlay(ItemType it, List<(int Slot, int ImgIdx, int SpriteIdx, bool IsHandHeld)> entries)
    {
        var findImage = _vis.FindImageFunc;

        // ImageList: comma-separated filenames
        var imageNames = it.ImageList.ToRawString(",").Split(',').Select(s => s.Trim())
            .Where(s => s.Length > 0).ToList();

        // SpriteList: slot=filename pairs (e.g. "1=HumanHead.png,2=HumanBody.png")
        var spriteSlotMap = new Dictionary<int, string>();
        var freeSpriteFiles = new List<string>();
        foreach (var seg in it.SpriteList.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var eqIdx = seg.IndexOf('=');
            if (eqIdx > 0 && int.TryParse(seg[..eqIdx].Trim(), out var sl))
                spriteSlotMap[sl] = VisHelperService.StripNs(seg[(eqIdx + 1)..].Trim());
            else
                freeSpriteFiles.Add(VisHelperService.StripNs(seg));
        }

        var invBasePath = findImage("btn_inv_body.png");
        var spriteBasePath = findImage("CreHuman.png")
            ?? findImage("Person.png");

        Bitmap? invBmp = null, spriteBmp = null;
        if (invBasePath is not null) try { invBmp = new Bitmap(invBasePath); } catch (Exception ex) { Serilog.Log.Logger.Verbose(ex, "[ItemTypeVis] Failed to load inv bitmap from {Path}", invBasePath); }
        if (spriteBasePath is not null) try { spriteBmp = new Bitmap(spriteBasePath); } catch (Exception ex) { Serilog.Log.Logger.Verbose(ex, "[ItemTypeVis] Failed to load sprite bitmap from {Path}", spriteBasePath); }
        int cw = invBmp?.PixelSize.Width ?? 132, ch = invBmp?.PixelSize.Height ?? 165;

        // Track used image indices to compute free indices for bare slots
        var usedImgIdx = new HashSet<int>();
        foreach (var (slot, imgIdx, _, isHandHeld) in entries)
        {
            if (!isHandHeld && imgIdx >= 0 && imgIdx < imageNames.Count)
                usedImgIdx.Add(imgIdx);
        }
        var freeImgIdx = Enumerable.Range(0, imageNames.Count).Where(i => !usedImgIdx.Contains(i)).ToList();

        // Body-worn sprite slots: mark which slot numbers have a body-worn equip entry + matching sprite
        var usedBodySpriteSlots = new HashSet<int>();
        foreach (var (slot, _, _, isHandHeld) in entries)
        {
            if (!isHandHeld && spriteSlotMap.ContainsKey(slot))
                usedBodySpriteSlots.Add(slot);
        }
        // Add sprites from body-worn slots NOT used by any equip entry → available for hand-held
        foreach (var kv in spriteSlotMap)
            if (!usedBodySpriteSlots.Contains(kv.Key))
                freeSpriteFiles.Add(kv.Value);

        // Per-slot enabled state + selected image/sprite index for bare (hand-held) slots
        var enabled = new Dictionary<int, bool>();
        var bareImgSel = new Dictionary<int, int>(); // slot -> selected imgIdx for bare slots
        var bareSprSel = new Dictionary<int, int>(); // slot -> selected sprite file idx in freeSpriteFiles
        foreach (var (slot, _, _, isHandHeld) in entries)
        {
            enabled[slot] = true;
            if (isHandHeld && freeImgIdx.Count > 0)
                bareImgSel[slot] = freeImgIdx[0];
            if (isHandHeld && freeSpriteFiles.Count > 0)
                bareSprSel[slot] = 0;
        }

        // ── Build overlay canvas ──
        Control BuildCanvas(bool isSprite)
        {
            var baseBmp = isSprite ? spriteBmp : invBmp;
            var bw = baseBmp?.PixelSize.Width ?? cw;
            var bh = baseBmp?.PixelSize.Height ?? ch;
            var canvas = new Canvas { Width = bw, Height = bh, Background = Brushes.Transparent };

            void Refresh()
            {
                canvas.Children.Clear();
                var baseBmp = isSprite ? spriteBmp : invBmp;
                if (baseBmp is not null)
                    canvas.Children.Add(new Image { Source = baseBmp, Stretch = Stretch.None });

                foreach (var (slot, imgIdx, spriteIdx, isHandHeld) in entries)
                {
                    if (!enabled.GetValueOrDefault(slot, true)) continue;
                    if (isSprite && isHandHeld && freeSpriteFiles.Count == 0) continue;

                    string? imgPath = null;
                    bool shouldMirror = false;

                    if (isSprite)
                    {
                        if (isHandHeld)
                        {
                            // Bare slot sprite: use free sprite file by index
                            var selSprIdx = bareSprSel.GetValueOrDefault(slot, 0);
                            if (selSprIdx >= 0 && selSprIdx < freeSpriteFiles.Count)
                                imgPath = findImage(freeSpriteFiles[selSprIdx]);
                        }
                        else
                        {
                            // Body-worn slot sprite: look up by equip slot number
                            if (spriteSlotMap.TryGetValue(slot, out var sf))
                                imgPath = findImage(sf);
                        }
                    }
                    else // Image UI
                    {
                        if (isHandHeld)
                        {
                            var selIdx = bareImgSel.GetValueOrDefault(slot, 0);
                            if (selIdx >= 0 && selIdx < imageNames.Count)
                                imgPath = findImage(imageNames[selIdx]);
                        }
                        else if (imgIdx >= 0 && imgIdx < imageNames.Count)
                        {
                            imgPath = findImage(imageNames[imgIdx]);
                        }
                    }

                    if (imgPath is null) continue;

                    // Mirror: only for Image UI, never for Sprite UI
                    shouldMirror = !isSprite && it.Mirrored && (slot == 21 || slot == 6 || slot == 3);

                    try
                    {
                        var bmp = new Bitmap(imgPath);
                        var img = new Image { Source = bmp, Stretch = Stretch.None };

                        if (isHandHeld)
                        {
                            // Center at hand position
                            var (hx, hy) = GetHandPos(slot);
                            Canvas.SetLeft(img, hx - bmp.PixelSize.Width / 2.0);
                            Canvas.SetTop(img, hy - bmp.PixelSize.Height / 2.0);
                            if (shouldMirror)
                            {
                                img.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                                img.RenderTransform = new ScaleTransform(-1, 1);
                            }
                        }
                        else
                        {
                            if (isSprite)
                            {
                                // Sprite overlays share same coordinate origin as base image (top-left 0,0)
                                Canvas.SetLeft(img, 0);
                                Canvas.SetTop(img, 0);
                            }
                            else
                            {
                                // Center body-worn on canvas center point (Image UI)
                                Canvas.SetLeft(img, (bw - bmp.PixelSize.Width) / 2.0);
                                Canvas.SetTop(img, (bh - bmp.PixelSize.Height) / 2.0);
                                if (shouldMirror)
                                {
                                    img.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                                    img.RenderTransform = new ScaleTransform(-1, 1);
                                }
                            }
                        }
                        canvas.Children.Add(img);
                    }
                    catch (Exception ex) { Serilog.Log.Logger.Verbose(ex, "[ItemTypeVis] Failed to add slot image to canvas"); }
                }
            }
            Refresh();

            // Pan & zoom
            var zoom = 1.0; var panX = 0.0; var panY = 0.0; var isPanning = false;
            var panStart = new Point(); var panStartX = 0.0; var panStartY = 0.0;
            var scale = new ScaleTransform(1, 1);
            var translate = new TranslateTransform(0, 0);
            var group = new TransformGroup(); group.Children.Add(scale); group.Children.Add(translate);
            canvas.RenderTransform = group;
            canvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

            var scroll = new ScrollViewer
            {
                Content = new Border { Child = canvas, Background = Brush.Parse("#1A000000") },
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                MinWidth = 280,
                MinHeight = 350
            };

            // Initial zoom to make content reasonably visible
            var initZoom = Math.Min(280.0 / Math.Max(bw, 1), 350.0 / Math.Max(bh, 1)) * 0.75;
            zoom = Math.Clamp(initZoom, 0.5, 3.0);
            scale.ScaleX = zoom; scale.ScaleY = zoom;

            scroll.PointerWheelChanged += (_, e) =>
            {
                var oldZoom = zoom;
                zoom *= e.Delta.Y > 0 ? 1.15 : 0.87;
                zoom = Math.Clamp(zoom, 0.1, 20.0);
                var pos = e.GetPosition(scroll);
                var ratio = zoom / oldZoom;
                panX = pos.X - ratio * (pos.X - panX);
                panY = pos.Y - ratio * (pos.Y - panY);
                scale.ScaleX = zoom; scale.ScaleY = zoom;
                translate.X = panX; translate.Y = panY;
                e.Handled = true;
            };
            scroll.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(scroll).Properties.IsLeftButtonPressed)
                {
                    isPanning = true; panStart = e.GetPosition(scroll);
                    panStartX = panX; panStartY = panY;
                    e.Pointer.Capture(scroll); e.Handled = true;
                }
            };
            scroll.PointerMoved += (_, e) =>
            {
                if (!isPanning) return;
                var pos = e.GetPosition(scroll);
                panX = panStartX + (pos.X - panStart.X);
                panY = panStartY + (pos.Y - panStart.Y);
                translate.X = panX; translate.Y = panY;
            };
            scroll.PointerReleased += (_, _) => { isPanning = false; };

            canvas.Tag = (Action)Refresh;
            return scroll;
        }

        var imageCanvas = BuildCanvas(isSprite: false);
        var spriteCanvas = BuildCanvas(isSprite: true);

        // Checkboxes — for bare slots, clicking cycles to next free image/sprite
        var cbPanel = new WrapPanel();
        foreach (var (slot, imgIdx, spriteIdx, isHandHeld) in entries)
        {
            var displayImgIdx = isHandHeld ? bareImgSel.GetValueOrDefault(slot, 0) : imgIdx;
            var displaySpr = isHandHeld
                ? (bareSprSel.TryGetValue(slot, out var bi) && bi >= 0 && bi < freeSpriteFiles.Count ? freeSpriteFiles[bi] : "—")
                : (spriteSlotMap.TryGetValue(slot, out var sf) ? sf : $"?{spriteIdx}");
            var cb = new CheckBox
            {
                Content = $"{GetSlotName(slot)} [I:{displayImgIdx} S:{displaySpr}]",
                IsChecked = enabled[slot],
                FontSize = 10, Margin = new Thickness(0, 0, 8, 0)
            };
            var capturedSlot = slot;
            cb.IsCheckedChanged += (_, _) =>
            {
                if (isHandHeld && cb.IsChecked == true)
                {
                    // Cycle to next free image
                    if (freeImgIdx.Count > 1)
                    {
                        var cur = bareImgSel.GetValueOrDefault(capturedSlot, 0);
                        var curIdx = freeImgIdx.IndexOf(cur);
                        var nextIdx = (curIdx + 1) % freeImgIdx.Count;
                        bareImgSel[capturedSlot] = freeImgIdx[nextIdx];
                    }
                    // Cycle sprite too
                    if (freeSpriteFiles.Count > 1)
                    {
                        var curSprIdx = bareSprSel.GetValueOrDefault(capturedSlot, 0);
                        var nextSprIdx = (curSprIdx + 1) % freeSpriteFiles.Count;
                        bareSprSel[capturedSlot] = nextSprIdx;
                    }
                }
                // Update label with current indices
                var dImg = isHandHeld ? bareImgSel.GetValueOrDefault(capturedSlot, 0) : imgIdx;
                var dSpr = isHandHeld
                    ? (bareSprSel.TryGetValue(capturedSlot, out var bi2) && bi2 >= 0 && bi2 < freeSpriteFiles.Count ? freeSpriteFiles[bi2] : "—")
                    : (spriteSlotMap.TryGetValue(capturedSlot, out var sf) ? sf : $"?{spriteIdx}");
                cb.Content = $"{GetSlotName(capturedSlot)} [I:{dImg} S:{dSpr}]";
                enabled[capturedSlot] = cb.IsChecked == true;
                if (imageCanvas is ScrollViewer isv && isv.Content is Border ib && ib.Child is Canvas ic && ic.Tag is Action ira) ira();
                if (spriteCanvas is ScrollViewer ssv && ssv.Content is Border sb && sb.Child is Canvas sc && sc.Tag is Action sra) sra();
            };
            cbPanel.Children.Add(cb);
        }

        var tabs = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
        tabs.Items.Add(new TabItem { Header = "Image UI", Content = imageCanvas });
        tabs.Items.Add(new TabItem { Header = "Sprite UI", Content = spriteCanvas });

        var overlayPanel = new StackPanel { Spacing = 6 };
        overlayPanel.Children.Add(new TextBlock
            { Text = "Wear Preview", FontSize = 10, Foreground = Brush.Parse("#999") });
        overlayPanel.Children.Add(cbPanel);
        overlayPanel.Children.Add(tabs);

        return overlayPanel;
    }

    private Control ConditionSection(string label, string raw, ItemType it, string propName)
    {
        var wp = new WrapPanel();
        foreach (var seg in raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var c = _vis.Resolver.LookupRef<Condition>(it, propName, seg);
            if (c is null)
            {
                wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
                continue;
            }

            // Parse slot prefix and negation from {value}={id} pattern
            var eqIdx = seg.IndexOf('=');
            var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
            var isNeg = slotPart.StartsWith('-');
            var slotNumStr = isNeg ? slotPart[1..] : slotPart;
            var slotName = int.TryParse(slotNumStr, out var sn) ? GetSlotName(sn) : slotNumStr;

            // R36: semantic severity colors (Fatal red / Permanent orange / Stackable green / Duration blue).
            var text = string.IsNullOrEmpty(slotName) ? c.Subject : $"{slotName}: {(isNeg ? "~" : "")}{c.Subject}";
            var (bg, fg) = isNeg ? ("#F5F5F5", "#999") : (ConditionBg(c), ConditionFg(c));
            var display = $"{text} · {(c.Fatal ? "FATAL" : c.Permanent ? "Instant" : c.Stackable ? "Stackable" : $"{c.Duration:F0}h")}";
            wp.Children.Add(_refNode.BadgeForEntity(it, c, display, bg, fg));
        }
        return wp.Children.Count > 0 ? LabeledSection(label, wp) : new TextBlock();
    }

    // ═══════════════ Effects body: conditions + required condition + item properties ═══════════════
    // R39: merged from EquipmentCardV2 conditions + LinkedDataCard CondId + CombatCard Properties.

    private Control BuildEffectsBody(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        if (it.PossessConditions.Count > 0)
        {
            body.Children.Add(ConditionSection(_vis.Loc("Vis.WhenCarried"),
                it.PossessConditions.ToRawString(","), it, nameof(ItemType.PossessConditions)));
            hasAny = true;
        }
        if (it.UseConditions.Count > 0)
        {
            body.Children.Add(ConditionSection(_vis.Loc("Vis.WhenUsed"),
                it.UseConditions.ToRawString(","), it, nameof(ItemType.UseConditions)));
            hasAny = true;
        }
        if (it.EquipConditions.Count > 0)
        {
            body.Children.Add(ConditionSection(_vis.Loc("Vis.WhenEquipped"),
                it.EquipConditions.ToRawString(","), it, nameof(ItemType.EquipConditions)));
            hasAny = true;
        }

        // Required condition (CondId) — semantic colors
        if (it.CondId.Count > 0)
        {
            var cond = _vis.Resolver.LookupRef<Condition>(it, nameof(ItemType.CondId), it.CondId);
            if (cond is not null)
            {
                body.Children.Add(LabeledSection(_vis.Loc("Vis.RequiredCondition"),
                    _refNode.BadgeForEntity(it, cond,
                        ConditionLabel(cond, null), ConditionBg(cond), ConditionFg(cond))));
                hasAny = true;
            }
        }

        // Item properties (ItemProp)
        if (it.Properties.Count > 0)
        {
            var wp = new WrapPanel();
            foreach (var s in it.Properties.ToRawString(",").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<ItemProp>(it, nameof(ItemType.Properties), s,
                    "#E8F5E9", "#2E7D32"));
            }
            body.Children.Add(LabeledSection(_vis.Loc("Vis.Properties"), wp));
            hasAny = true;
        }

        return hasAny ? body : null;
    }

    // ═══════════════ Associations body: container + switches + loot/component + sounds ═══════════════
    // R39: merged from ContainerCardV2 + SwitchesCard + LinkedDataCard (Treasure/Component).

    // ═══════════════ Container body: capacity + accepted content + format ═══════════════
    // R40: "what it holds" is its own mental-model block, not buried in associations.

    private Control BuildContainerBody(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        if (!string.IsNullOrWhiteSpace(it.Capacities))
        {
            body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.Capacity"), it.Capacities, "#546E7A"));
            hasAny = true;
        }
        if (it.ContentIds.Count > 0)
        {
            var wp = new WrapPanel();
            foreach (var seg in it.ContentIds.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var ct = _vis.Resolver.LookupRef<ContainerType>(it, nameof(ItemType.ContentIds), seg);
                if (ct is not null)
                    wp.Children.Add(_refNode.BadgeForEntity(it, ct, ct.Name!, "#E8EAF6", "#283593"));
            }
            if (wp.Children.Count > 0)
            {
                body.Children.Add(LabeledSection(_vis.Loc("Vis.AcceptsContent"), wp));
                hasAny = true;
            }
        }
        if (it.FormatId.Count > 0)
        {
            var ct = _vis.Resolver.LookupRef<ContainerType>(it, nameof(ItemType.FormatId), it.FormatId);
            if (ct is not null)
            {
                body.Children.Add(_vis.ValueRow(_vis.Loc("Vis.Format"), ct.Name ?? it.FormatId.ToString(), "#666"));
                hasAny = true;
            }
        }

        return hasAny ? body : null;
    }

    // ═══════════════ Associations body: switches + loot table + component ═══════════════
    // R40: "where it comes from / what it turns into" — sounds moved to the
    // Equipment block (interaction context), container moved to its own block.

    private Control BuildAssociationsBody(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasAny = false;

        // Switches — what clicking the item toggles
        if (it.SwitchIds.Count > 0)
        {
            var wp = new WrapPanel();
            foreach (var seg in it.SwitchIds.ToRawString(",").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var sw = _vis.Resolver.LookupRef<ItemType>(it, nameof(ItemType.SwitchIds), seg);
                if (sw is not null)
                {
                    var descShort = string.IsNullOrWhiteSpace(sw.Description) ? ""
                        : sw.Description.Length > 10 ? sw.Description[..10] : sw.Description;
                    var display = string.IsNullOrEmpty(descShort) ? sw.Name! : $"{sw.Name}({descShort})";
                    var fullDisplay = $"{sw.GroupId}.{sw.SubgroupId} {display}";
                    wp.Children.Add(_refNode.BadgeForEntity(it, sw, fullDisplay, "#F3E5F5", "#6A1B9A"));
                }
                else
                    wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
            }
            body.Children.Add(LabeledSection(_vis.Loc("Vis.SwitchStates"), wp));
            hasAny = true;
        }

        // Loot table + component (TreasureId / ComponentId)
        if (it.TreasureId.Count > 0)
        {
            var tt = _vis.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.TreasureId), it.TreasureId);
            if (tt is not null)
            {
                body.Children.Add(BuildTreasureLinkedSection(_vis.Loc("Vis.TreasureTable"), tt));
                hasAny = true;
            }
        }
        if (it.ComponentId.Count > 0)
        {
            var comp = _vis.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.ComponentId), it.ComponentId);
            if (comp is not null)
            {
                body.Children.Add(BuildTreasureLinkedSection(_vis.Loc("Vis.Component"), comp));
                hasAny = true;
            }
        }

        return hasAny ? body : null;
    }

    private Control BuildTreasureLinkedSection(string label, TreasureTable tt)
    {
        var section = new StackPanel { Spacing = 2 };
        var t = tt;
        var header = new TextBlock
        {
            Text = $"{label}: {t.Subject ?? t.Name ?? $"TT#{t.Id}"}",
            FontSize = 11, Foreground = Brush.Parse("#1565C0")
        };
        _refNode.WireNavigation(header, typeof(TreasureTable), t.EntityId, t);
        section.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(tt.Treasures))
        {
            var lt = BuildTreasureLootTree(tt);
            lt.Margin = new Thickness(12, 2, 0, 0);
            section.Children.Add(lt);
        }

        return section;
    }

    /// <summary>Build the loot tree for a TreasureTable, reusing TT visualizer helpers.</summary>
    private Control BuildTreasureLootTree(TreasureTable tt)
    {
        var sp = new StackPanel { Spacing = 2 };
        if (string.IsNullOrWhiteSpace(tt.Treasures))
        {
            sp.Children.Add(new TextBlock
                { Text = _vis.Loc("Vis.Empty"), FontSize = 10, Foreground = Brush.Parse("#999") });
            return sp;
        }

        var itemTypes = _dataTable!.GetCompositeEntities<ItemType>(
            it => $"{it.GroupId}.{it.SubgroupId}", tt.ModId);

        var allSegs = tt.Treasures.Split('|', ',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && s.Contains('x'))
            .ToList();

        if (allSegs.Count == 0)
        {
            sp.Children.Add(new TextBlock
                { Text = "(no loot entries)", FontSize = 10, Foreground = Brush.Parse("#999") });
            return sp;
        }

        var allParsed = new List<(string itemId, double weight, string qtyRange)>();
        double totalWeight = 0;
        foreach (var seg in allSegs)
        {
            var parts = seg.Split('x');
            if (parts.Length < 2) continue;
            var itemId = parts[0].Trim();
            var weightStr = parts[1].Trim();
            var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
            var weight = double.TryParse(weightStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 1.0;
            totalWeight += weight;
            allParsed.Add((itemId, weight, qtyRange));
        }

        foreach (var (itemId, weight, qtyRange) in allParsed)
        {
            var actualProb = totalWeight > 0 ? weight / totalWeight : 1.0 / allParsed.Count;

            if (itemTypes.TryGetValue(itemId, out var matched))
            {
                sp.Children.Add(TreasureTableEntityVisualizer.BuildItemRow(
                    _vis,
                    matched.Description ?? matched.Name ?? itemId, "ItemType", "#E0F2F1", "#00695C",
                    _refNode.NavAction(typeof(ItemType), matched.EntityId),
                    weight, actualProb, qtyRange));
            }
            else
            {
                var nested = _vis.Resolver.LookupRef<TreasureTable>(tt,
                    nameof(TreasureTable.Treasures), itemId);
                if (nested is not null)
                {
                    var row = TreasureTableEntityVisualizer.BuildItemRow(
                        _vis,
                        nested.Name ?? $"TT#{nested.Id}", "TT", "#E8EAF6", "#283593",
                        _refNode.NavAction(typeof(TreasureTable), nested.EntityId),
                        weight, actualProb, qtyRange);
                    var sub = TreasureTableEntityVisualizer.BuildNestedItems(
                        _vis, _dataTable, nested, itemTypes, 1, _refNode);
                    sp.Children.Add(row);
                    if (sub is not null)
                    {
                        var subExpanded = true;
                        sub.IsVisible = true;
                        row.Cursor = new Cursor(StandardCursorType.Hand);
                        row.PointerPressed += (_, e) =>
                        {
                            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
                            {
                                subExpanded = !subExpanded;
                                sub.IsVisible = subExpanded;
                            }
                        };
                        sp.Children.Add(sub);
                    }
                }
                else
                {
                    sp.Children.Add(TreasureTableEntityVisualizer.BuildItemRow(
                        _vis,
                        itemId, null, "#F5F5F5", "#999", null,
                        weight, actualProb, qtyRange));
                }
            }
        }

        return sp;
    }

    // ═══════════════ Reverse references ═══════════════

    private Control BuildReverseRefsPanel(ItemType it)
        => _vis.BuildReverseRefsPanel(it.EntityId);

}
