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
            _vis.Router);
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
        var root = new StackPanel { Spacing = 12, Margin = new Thickness(16) };

        var rawBody = new Border
            { IsVisible = false, Child = _vis.BuildRawDataTable(it), Padding = new Thickness(8) };
        root.Children.Add(_vis.BuildExpander(_vis.Loc("Vis.RawData"), rawBody));
        root.Children.Add(rawBody);

        root.Children.Add(BuildHeroHeader(it));

        // ═══ Card 1: 基础属性 (Stats + Durability + Break Parts) ═══
        var hasBasic = it.Weight > 0 || it.StackLimit > 0 || it.MonetaryValue > 0
            || it.Mirrored || it.SlotDepth > 0
            || it.Durability > 0 || it.DegradePerHour > 0 || it.EquipDegradePerHour > 0 || it.DegradePerUse > 0
            || (!string.IsNullOrWhiteSpace(it.DegradeTreasureIds) && it.DegradeTreasureIds != "3,3");
        if (hasBasic)
            root.Children.Add(BuildBasicCard(it));

        // ═══ Card 2: 装备与状态 ═══
        var hasEquip = !string.IsNullOrWhiteSpace(it.EquipSlots) || !string.IsNullOrWhiteSpace(it.UseSlots)
            || it.SocketLocked || !string.IsNullOrWhiteSpace(it.EquipConditions)
            || !string.IsNullOrWhiteSpace(it.UseConditions) || !string.IsNullOrWhiteSpace(it.PossessConditions);
        if (hasEquip)
            root.Children.Add(BuildEquipmentCardV2(it));

        // ═══ Card 3: 属性与战斗 (Properties + AttackModes + Charge) ═══
        var hasCombat = !string.IsNullOrWhiteSpace(it.Properties)
            || !string.IsNullOrWhiteSpace(it.AttackModes)
            || !string.IsNullOrWhiteSpace(it.ChargeProfiles);
        if (hasCombat)
            root.Children.Add(BuildCombatCard(it));

        // ═══ Card 4: 容器 ═══
        var hasContainer = !string.IsNullOrWhiteSpace(it.Capacities) || !string.IsNullOrWhiteSpace(it.ContentIds)
            || (!string.IsNullOrWhiteSpace(it.FormatId) && it.FormatId != "3");
        if (hasContainer)
            root.Children.Add(BuildContainerCardV2(it));

        // ═══ Card 5: 开关 ═══
        if (!string.IsNullOrWhiteSpace(it.SwitchIds))
            root.Children.Add(BuildSwitchesCard(it));

        // ═══ Card 6: 关联数据 (TreasureTable, Component, CondId) ═══
        root.Children.Add(BuildLinkedDataCard(it));

        // ═══ Card 7: 被引用 ═══
        root.Children.Add(BuildReverseRefsPanel(it));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Hero header: switchable image gallery (left) + identity (right) ═══════════════

    private Control BuildHeroHeader(ItemType it)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var imageNames = (it.ImageList ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        var isImageList = (it.ImageList ?? "").Contains(',');

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
        Grid.SetColumn(identity, 1);
        Grid.SetRow(identity, 0);
        Grid.SetRowSpan(identity, 2);
        grid.Children.Add(identity);

        return _vis.Card(grid);
    }

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

    // ═══════════════ Basic Card: Stats + Durability + Break Parts ═══════════════

    private Control BuildBasicCard(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };

        // Stats: Weight | StackLimit | Value | Flags
        var cells = new List<(string, string, string?)>();
        if (it.Weight > 0)
            cells.Add((_vis.Loc("Vis.Weight"), $"{it.Weight:F1} kg", "#4CAF50"));
        if (it.StackLimit > 0)
            cells.Add((_vis.Loc("Vis.StackLimit"), $"×{it.StackLimit}", "#2196F3"));
        if (it.MonetaryValue > 0)
        {
            var vt = it.MonetaryValueAlt > 0 && it.MonetaryValueAlt != it.MonetaryValue
                ? $"${it.MonetaryValue:F2} → ${it.MonetaryValueAlt:F2}"
                : $"${it.MonetaryValue:F2}";
            cells.Add((_vis.Loc("Vis.Value"), vt, "#9C27B0"));
        }
        if (it.Mirrored)
            cells.Add(("", _vis.Loc("Vis.Mirrored"), "#607D8B"));
        if (it.SlotDepth > 0)
            cells.Add((_vis.Loc("Vis.SlotDepth"), $"{it.SlotDepth}", "#546E7A"));
        if (cells.Count > 0)
            body.Children.Add(_vis.CreatureStatGrid(cells));

        // Durability
        var durCells = new List<(string, string, string?)>();
        if (it.Durability > 0)
        {
            var dt = it.Durability >= 999 ? "∞" : $"{it.Durability * 100:F0}%";
            durCells.Add((_vis.Loc("Vis.Durability"), dt, it.Durability >= 999 ? "#607D8B" : "#FF9800"));
        }
        if (it.DegradePerHour > 0)
            durCells.Add((_vis.Loc("Vis.PerHour"), $"{it.DegradePerHour:F3}", "#E65100"));
        if (it.EquipDegradePerHour > 0)
            durCells.Add((_vis.Loc("Vis.PerHourEquipped"), $"{it.EquipDegradePerHour:F3}", "#C62828"));
        if (it.DegradePerUse > 0)
            durCells.Add((_vis.Loc("Vis.PerUse"), $"{it.DegradePerUse:F3}", "#F57F17"));
        if (durCells.Count > 0)
        {
            body.Children.Add(new Border { Height = 1, Background = Brush.Parse("#10000000"), Margin = new Thickness(0, 2) });
            body.Children.Add(_vis.CreatureStatGrid(durCells));
        }

        // Break Parts — inline below durability
        var ttIds = it.DegradeTreasureIds.Split(',').Select(s => s.Trim())
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
        }

        return _vis.Card(body, _vis.Loc("Vis.BasicStats"));
    }

    // ═══════════════ Combat Card: Properties + AttackModes + Charge ═══════════════
    // M1.4: uses RefNode for declarative reference rendering (R03/R04)

    private Control BuildCombatCard(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };

        // Properties → ItemProp badges (R04: RefNode declares "reference to ItemProp")
        if (!string.IsNullOrWhiteSpace(it.Properties))
        {
            var wp = new WrapPanel();
            foreach (var s in it.Properties.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<ItemProp>(it, nameof(ItemType.Properties), s,
                    "#E8F5E9", "#2E7D32"));
            }
            body.Children.Add(LabeledSection(_vis.Loc("Vis.Properties"), wp));
        }

        // AttackModes (R04: RefNode handles resolution + navigation + peek)
        if (!string.IsNullOrWhiteSpace(it.AttackModes))
        {
            var wp = new WrapPanel();
            foreach (var seg in it.AttackModes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var eqIdx = seg.IndexOf('=');
                var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
                var slotName = int.TryParse(slotPart, out var sn) ? GetSlotName(sn) : slotPart;

                if (!string.IsNullOrEmpty(slotName))
                {
                    // Has slot prefix: use BadgeWithSlot
                    wp.Children.Add(_refNode.BadgeWithSlot<AttackMode>(it,
                        nameof(ItemType.AttackModes), seg, slotName,
                        "#FFEBEE", "#C62828", "#F5F5F5", "#999"));
                }
                else
                {
                    wp.Children.Add(_refNode.Badge<AttackMode>(it,
                        nameof(ItemType.AttackModes), seg,
                        "#FFEBEE", "#C62828", "#F5F5F5", "#999"));
                }
            }
            body.Children.Add(LabeledSection(_vis.Loc("Vis.AttackModes"), wp));
        }

        // Charge profiles (R04: RefNode)
        if (!string.IsNullOrWhiteSpace(it.ChargeProfiles))
        {
            var wp = new WrapPanel();
            foreach (var seg in it.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                wp.Children.Add(_refNode.Badge<ChargeProfile>(it,
                    nameof(ItemType.ChargeProfiles), seg,
                    "#E0F7FA", "#006064"));
            }
            body.Children.Add(LabeledSection(_vis.Loc("Vis.ChargeAmmo"), wp));
        }

        return _vis.Card(body, _vis.Loc("Vis.Combat"));
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

    // ═══════════════ Equipment Card V2 — left: properties, right: preview ═══════════════

    private Control BuildEquipmentCardV2(ItemType it)
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

        // Left column: properties
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

        if (!string.IsNullOrWhiteSpace(it.PossessConditions))
            leftPanel.Children.Add(ConditionSection(_vis.Loc("Vis.WhenCarried"), it.PossessConditions, it, nameof(ItemType.PossessConditions)));
        if (!string.IsNullOrWhiteSpace(it.UseConditions))
            leftPanel.Children.Add(ConditionSection(_vis.Loc("Vis.WhenUsed"), it.UseConditions, it, nameof(ItemType.UseConditions)));
        if (!string.IsNullOrWhiteSpace(it.EquipConditions))
            leftPanel.Children.Add(ConditionSection(_vis.Loc("Vis.WhenEquipped"), it.EquipConditions, it, nameof(ItemType.EquipConditions)));

        // Build card body
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
            return _vis.Card(grid, _vis.Loc("Vis.Equipment"));
        }

        return _vis.Card(leftPanel, _vis.Loc("Vis.Equipment"));
    }

    /// <summary>Build a tabbed overlay preview (Image UI / Sprite UI) with checkbox toggles.</summary>
    private Control BuildEquipSlotOverlay(ItemType it, List<(int Slot, int ImgIdx, int SpriteIdx, bool IsHandHeld)> entries)
    {
        var findImage = _vis.FindImageFunc;

        // ImageList: comma-separated filenames
        var imageNames = (it.ImageList ?? "").Split(',').Select(s => s.Trim())
            .Where(s => s.Length > 0).ToList();

        // SpriteList: slot=filename pairs (e.g. "1=HumanHead.png,2=HumanBody.png")
        var spriteSlotMap = new Dictionary<int, string>();
        var freeSpriteFiles = new List<string>();
        foreach (var seg in (it.SpriteList ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
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
            if (c is null) continue;

            // Parse slot prefix and negation from {value}={id} pattern
            var eqIdx = seg.IndexOf('=');
            var slotPart = eqIdx > 0 ? seg[..eqIdx].Trim() : "";
            var isNeg = slotPart.StartsWith('-');
            var slotNumStr = isNeg ? slotPart[1..] : slotPart;
            var slotName = int.TryParse(slotNumStr, out var sn) ? GetSlotName(sn) : slotNumStr;

            var text = string.IsNullOrEmpty(slotName) ? c.Subject : $"{slotName}: {(isNeg ? "~" : "")}{c.Subject}";
            var (bg, fg) = isNeg ? ("#F5F5F5", "#999") : ("#FCE4EC", "#C62828");
            wp.Children.Add(_refNode.BadgeForEntity(it, c, text, bg, fg));
        }
        return wp.Children.Count > 0 ? LabeledSection(label, wp) : new TextBlock();
    }

    // ═══════════════ Container Card V2 ═══════════════

    private Control BuildContainerCardV2(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };

        if (!string.IsNullOrWhiteSpace(it.Capacities))
            body.Children.Add(new TextBlock
                { Text = $"{_vis.Loc("Vis.Capacity")}: {it.Capacities}", FontSize = 12, FontWeight = FontWeight.SemiBold });

        if (!string.IsNullOrWhiteSpace(it.ContentIds))
        {
            var wp = new WrapPanel();
            foreach (var seg in it.ContentIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var ct = _vis.Resolver.LookupRef<ContainerType>(it, nameof(ItemType.ContentIds), seg);
                if (ct is not null)
                    wp.Children.Add(_refNode.BadgeForEntity(it, ct, ct.Name!,
                        "#E8EAF6", "#283593"));
            }
            if (wp.Children.Count > 0)
                body.Children.Add(LabeledSection(_vis.Loc("Vis.AcceptsContent"), wp));
        }

        // FormatId as simple stat
        if (!string.IsNullOrWhiteSpace(it.FormatId) && it.FormatId != "3")
        {
            var ct = _vis.Resolver.LookupRef<ContainerType>(it, nameof(ItemType.FormatId), it.FormatId);
            if (ct is not null)
                body.Children.Add(new TextBlock
                {
                    Text = $"{_vis.Loc("Vis.Format")}: {ct.Name}", FontSize = 11, Foreground = Brush.Parse("#666"),
                    Cursor = new Cursor(StandardCursorType.Hand)
                });
        }

        return _vis.Card(body, _vis.Loc("Vis.Container"));
    }

    // ═══════════════ Switches Card ═══════════════

    private Control BuildSwitchesCard(ItemType it)
    {
        var body = new StackPanel { Spacing = 4 };
        var wp = new WrapPanel();
        foreach (var seg in it.SwitchIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
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
        body.Children.Add(wp);
        return _vis.Card(body, _vis.Loc("Vis.SwitchStates"));
    }

    // ═══════════════ Linked Data Card (TreasureTable + Component + CondId) ═══════════════

    private Control BuildLinkedDataCard(ItemType it)
    {
        var body = new StackPanel { Spacing = 8 };
        var hasContent = false;

        // TreasureId → expandable loot tree
        if (!string.IsNullOrWhiteSpace(it.TreasureId) && it.TreasureId != "3")
        {
            var tt = _vis.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.TreasureId), it.TreasureId);
            if (tt is not null)
            {
                hasContent = true;
                body.Children.Add(BuildTreasureLinkedSection(_vis.Loc("Vis.TreasureTable"), tt));
            }
        }

        // ComponentId
        if (!string.IsNullOrWhiteSpace(it.ComponentId) && it.ComponentId != "0")
        {
            var comp = _vis.Resolver.LookupRef<TreasureTable>(it, nameof(ItemType.ComponentId), it.ComponentId);
            if (comp is not null)
            {
                hasContent = true;
                body.Children.Add(BuildTreasureLinkedSection(_vis.Loc("Vis.Component"), comp));
            }
        }

        // CondId
        if (!string.IsNullOrWhiteSpace(it.CondId) && it.CondId != "1")
        {
            var cond = _vis.Resolver.LookupRef<Condition>(it, nameof(ItemType.CondId), it.CondId);
            if (cond is not null)
            {
                hasContent = true;
                var severity = cond.Fatal ? "FATAL" : cond.Permanent ? "Instant" : cond.Stackable ? "Stackable" : "Duration";
                var sevBg = cond.Fatal ? "#FFEBEE" : cond.Permanent ? "#FFF3E0" : cond.Stackable ? "#E8F5E9" : "#E3F2FD";
                var sevFg = cond.Fatal ? "#C62828" : cond.Permanent ? "#E65100" : cond.Stackable ? "#2E7D32" : "#1565C0";
                var durText = cond.Permanent ? "Instant" : $"{cond.Duration}h";
                var label = string.IsNullOrEmpty(cond.Subject) ? $"Condition#{cond.Id}" : cond.Subject;
                body.Children.Add(LabeledSection(_vis.Loc("Vis.RequiredCondition"),
                    _refNode.BadgeForEntity(it, cond, $"{label} · {severity} · {durText}",
                        sevBg, sevFg)));
            }
        }

        return hasContent ? _vis.Card(body, _vis.Loc("Vis.LinkedData")) : new TextBlock();
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
