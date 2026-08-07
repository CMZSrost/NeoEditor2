using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Views;

namespace NeoEditor.Plugins.EntityEditor.Services;

/// <summary>
/// DI singleton — shared visualization helpers for entity detail views.
/// M10: migrated from static VisHelper in App. Constructor-injected services replace
/// SetServices() + DataTableService.Instance + ViewServices.Loc static access.
/// </summary>
public partial class VisHelperService
{
    private readonly Func<string, string?> _findImage;
    private readonly IReferenceResolver _resolver;
    private readonly INavigationRouter _router;
    private readonly IEntityLookupService _dataTable;
    private readonly ILocalizationService _loc;
    private readonly IAudioPlaybackService? _audio;

    public VisHelperService(
        Func<string, string?> findImage,
        IReferenceResolver resolver,
        INavigationRouter router,
        IEntityLookupService dataTable,
        ILocalizationService localization,
        IAudioPlaybackService? audio = null)
    {
        _findImage = findImage;
        _resolver = resolver;
        _router = router;
        _dataTable = dataTable;
        _loc = localization;
        _audio = audio;
    }

    public Func<string, string?> FindImageFunc => _findImage;
    public IReferenceResolver Resolver => _resolver;
    public INavigationRouter Router => _router;

    /// <summary>Localization shortcut.</summary>
    public string Loc(string key) => _loc[key];

    /// <summary>Localization shortcut with format arguments.</summary>
    public string Loc(string key, params object[] args) => _loc[key, args];

    /// <summary>
    /// R42: play button for a game cue name (aSounds / strSnd) — hidden when the
    /// sound index is unavailable (sounds not extracted yet). Click plays the
    /// matched asset via IAudioPlaybackService.
    /// </summary>
    public Control? PlaySoundButton(string cueName)
    {
        if (_audio is null || !_audio.IsAvailable || string.IsNullOrWhiteSpace(cueName)) return null;
        var btn = new Button
        {
            Content = "▶", FontSize = 9, Padding = new Thickness(6, 1),
            MinHeight = 0, VerticalAlignment = VerticalAlignment.Center,
            Background = Brush.Parse("#ECEFF1"), BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btn, $"{Loc("Vis.PlaySound")}: {cueName}");
        var cue = cueName;
        btn.Click += (_, _) => _audio.Play(cue);
        return btn;
    }

    public TreeViewItem Section(string text, IBrush? fg = null)
    {
        var tb = new TextBlock { Text = text, FontWeight = FontWeight.Bold, Foreground = fg ?? Brushes.DodgerBlue };
        return new TreeViewItem { IsExpanded = true, Header = tb };
    }

    public TreeViewItem Leaf(string text, IBrush? fg = null)
    {
        var tb = new TextBlock { Text = text, Foreground = fg ?? Brushes.Black, TextWrapping = TextWrapping.Wrap };
        return new TreeViewItem { IsExpanded = true, Header = tb };
    }

    public TreeViewItem NavLeaf(string text, Action nav, IBrush? fg = null,
        Type? peekType = null, string? peekEid = null)
    {
        var item = Leaf(text, fg);
        item.Cursor = new Cursor(StandardCursorType.Hand);
        item.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
            e.Handled = true;
            if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
            {
                if (peekType != null && peekEid != null)
                    WeakReferenceMessenger.Default.Send(new PeekEntityMessage(peekType, peekEid, null));
                return;
            }
            nav();
        };
        return item;
    }

    public TreeViewItem NavLeafWithPeek(string text, Type targetType, string targetEntityId, IBrush? fg = null)
    {
        var item = Leaf(text, fg);
        item.Cursor = new Cursor(StandardCursorType.Hand);
        item.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
            e.Handled = true;
            if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
                WeakReferenceMessenger.Default.Send(new PeekEntityMessage(targetType, targetEntityId, null));
            else
                _router.NavigateToEntity(targetType, targetEntityId);
        };
        return item;
    }

    public TreeViewItem RefNode<T>(string raw, string? separator, string? pattern, string? targetKey,
        string label, IBrush fg) where T : IEntity
    {
        var node = Section(label, fg);
        if (string.IsNullOrWhiteSpace(raw))
        {
            node.Items.Add(Leaf("(None)", Brushes.Gray));
            return node;
        }

        if (!(_dataTable.ReferenceLookups.TryGetValue(typeof(T), out var list) && list is not null))
        {
            node.Items.Add(Leaf(raw, Brushes.Gray));
            return node;
        }

        var parts = separator is not null ? raw.Split(separator) : [raw];
        foreach (var seg in parts)
        {
            var s = seg.Trim();
            if (string.IsNullOrEmpty(s)) continue;
            var idStr = ReferenceParser.ExtractRawId(s, pattern);
            var match = _dataTable.FindBestMatch(typeof(T), idStr, targetKey);
            var display = match?.Subject ?? idStr;
            var extra = ReferencePattern.FromName(pattern).FormatExtraInfo(s);
            if (!string.IsNullOrEmpty(extra)) display += $" ({extra})";
            var leaf = match is not null
                ? NavLeaf(display, () => _router.NavigateToEntity(typeof(T), match.EntityId, match), fg,
                    typeof(T), match.EntityId)
                : Leaf(display, Brushes.Gray);
            node.Items.Add(leaf);
        }

        return node;
    }

    public Bitmap? LoadImage(string? imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName)) return null;
        var name = StripNs(imageName.Trim());
        var candidates = name.Contains('.') ? new[] { name } : new[] { name + ".png", name };
        string? path = null;
        foreach (var c in candidates)
        {
            path = _findImage(c);
            if (path is not null) break;
        }

        if (path is null) return null;
        try { return new Bitmap(path); }
        catch { return null; }
    }

    public static string StripNs(string name)
    {
        var c = name.IndexOf(':');
        return c > 0 ? name[(c + 1)..] : name;
    }

    public StackPanel OverviewHeader(IEntity entity, Bitmap? thumb = null, string? subtitle = null)
    {
        var sp = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (thumb is not null)
            header.Children.Add(new Image { Source = thumb, MaxWidth = 48, MaxHeight = 48, Stretch = Stretch.Uniform });

        var textCol = new StackPanel { Spacing = 2 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        titleRow.Children.Add(new TextBlock
        {
            Text = entity.Subject ?? $"[{entity.GetType().Name}]", FontSize = 14, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        textCol.Children.Add(titleRow);

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var modName = _dataTable.EntityModNames.TryGetValue(entity.EntityId, out var mn)
            ? mn : $"mod_{entity.ModId}";
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#20000000"),
            Padding = new Thickness(5, 1),
            Child = new TextBlock { Text = $"{entity.ModId}:{modName}", FontSize = 9, Foreground = Brush.Parse("#888") }
        });
        var mergedId = _dataTable.EntityMergedIds.TryGetValue(entity.EntityId, out var mid) ? mid : 0;
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3), Background = Brush.Parse("#E65100"), Padding = new Thickness(4, 1),
            Child = new TextBlock { Text = $"mid={mergedId}", FontSize = 8, Foreground = Brushes.White }
        });
        var pkProp = EntityHelper.ResolveKeyProperty(entity.GetType());
        var pkVal = pkProp?.GetValue(entity) is int pk ? pk : -1;
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3), Background = Brush.Parse("#6A1B9A"), Padding = new Thickness(4, 1),
            Child = new TextBlock { Text = $"pk={pkVal}", FontSize = 8, Foreground = Brushes.White }
        });
        var eidShort = entity.EntityId.Length > 10 ? entity.EntityId[..10] + "…" : entity.EntityId;
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3), Background = Brush.Parse("#37474F"), Padding = new Thickness(4, 1),
            Child = new TextBlock { Text = eidShort, FontSize = 8, Foreground = Brushes.White }
        });
        textCol.Children.Add(idRow);
        if (subtitle is not null)
            textCol.Children.Add(new TextBlock
                { Text = subtitle, FontSize = 10, Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
        header.Children.Add(textCol);
        sp.Children.Add(header);
        return sp;
    }

    public TextBox Kv(string key, string value, int keyWidth = 90)
    {
        var tb = EditorUIFactory.SelectableText($"{key}: {value}", fontSize: 11);
        tb.Margin = new Thickness(0, 1);
        return tb;
    }

    public ScrollViewer Wrap(Control content)
        => new() { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };

    public Border Card(Control content, string? title = null)
    {
        var child = content;
        if (title is not null)
        {
            var sp = new StackPanel { Spacing = 4 };
            sp.Children.Add(new TextBlock
            {
                Text = title, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 6)
            });
            sp.Children.Add(content);
            child = sp;
        }
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Brush.Parse("#08000000"),
            BorderBrush = Brush.Parse("#18000000"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14),
            Child = child
        };
    }

    public TextBlock SectionLabel(string text) => new()
    {
        Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold,
        Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 8)
    };

    /// <summary>
    /// R40: unified detail-section header — icon + accent bar + title (+ optional
    /// count badge). One visual language for every detail block, replacing the
    /// mixed Card(title)/SectionLabel/LabeledSection conventions.
    /// </summary>
    public Control SectionHeader(string title, Symbol? icon = null, string? badge = null, string? accent = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (icon is { } ic)
            row.Children.Add(new SymbolIcon { Symbol = ic, FontSize = 13, Foreground = Brush.Parse("#555555") });
        row.Children.Add(new Border
        {
            Width = 3, Height = 15, CornerRadius = new CornerRadius(1.5),
            Background = Brush.Parse(accent ?? "#1565C0"),
            VerticalAlignment = VerticalAlignment.Center
        });
        row.Children.Add(new TextBlock
        {
            Text = title, FontSize = 13, FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#333333"), VerticalAlignment = VerticalAlignment.Center
        });
        if (!string.IsNullOrEmpty(badge))
            row.Children.Add(new TextBlock
            {
                Text = badge, FontSize = 10, Foreground = Brush.Parse("#888888"),
                VerticalAlignment = VerticalAlignment.Center
            });
        return new Border
        {
            BorderBrush = Brush.Parse("#18000000"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(2, 0, 2, 6),
            Child = row
        };
    }

    /// <summary>
    /// R39: one-line key/value row (90px label + value) — single visual language
    /// for scalar stats across all detail cards.
    /// </summary>
    public Control ValueRow(string label, string value, string? color = null)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(90, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(4, 1)
        };
        var lbl = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999999"),
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var val = new TextBlock
        {
            Text = value, FontSize = 11, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(color ?? "#333333"),
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(val, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(val);
        return grid;
    }

    public Border Separator() => new()
    {
        Height = 1, Background = Brush.Parse("#18000000"), Margin = new Thickness(4, 2)
    };

    public Border MiniBadge(string text, string bg, string fg, Action? onClick = null)
    {
        var tb = new TextBlock
            { Text = text, FontSize = 10, Foreground = Brush.Parse(fg), Padding = new Thickness(7, 2) };
        var badge = new Border { CornerRadius = new CornerRadius(9), Background = Brush.Parse(bg), Child = tb };
        if (onClick is not null)
        {
            badge.Cursor = new Cursor(StandardCursorType.Hand);
            badge.PointerPressed += (_, e) =>
            {
                if ((e.KeyModifiers & KeyModifiers.Control) != 0) onClick();
            };
        }
        return badge;
    }

    /// <summary>
    /// R30 (Doc 21 §7 P6): hover preview panel for a resolved reference entity —
    /// a compact type-specific stat summary. Falls back to identity info for
    /// entity types without a dedicated preview. Used by RefNode badges and the
    /// Value Editor reference badges.
    /// </summary>
    public Control BuildRefTooltip(IEntity entity)
    {
        var sp = new StackPanel { Spacing = 2, MaxWidth = 280 };
        sp.Children.Add(new TextBlock
        {
            Text = $"{entity.GetType().Name}: {entity.Subject ?? entity.EntityId}",
            FontSize = 11, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap
        });

        List<(string, string)> rows = entity switch
        {
            ChargeProfile cp =>
            [
                ("PerUse", $"{cp.PerUse:F2}"),
                ("PerHour", $"{cp.PerHour:F2}"),
                ("PerHrEquip", $"{cp.PerHourEquipped:F2}"),
                ("PerHex", $"{cp.PerHex:F2}"),
                ("Degrade", cp.Degrade ? "Yes" : "No"),
            ],
            AttackMode am =>
            [
                ("Type", am.Type == AttackType.Ranged ? "Ranged" : "Melee"),
                ("Range", am.Range.ToString()),
                ("Cut", $"{am.DamageCut:F1}"),
                ("Blunt", $"{am.DamageBlunt:F1}"),
                ("Penetration", am.Penetration.ToString()),
            ],
            Condition c =>
            [
                ("Duration", $"{c.Duration:F1}"),
                ("Fatal", c.Fatal ? "Yes" : "No"),
                ("Stackable", c.Stackable ? "Yes" : "No"),
                ("Color", c.Color.ToString()),
                // R42: translate aFieldNames/aModifiers pairs — the actual game
                // effect of the condition ("m_fMoveCost +0.5") — custom conditions
                // are unreadable from the name alone.
                ("Effect", BuildConditionEffectText(c)),
            ],
            ItemType it =>
            [
                ("Weight", $"{it.Weight:F2}"),
                ("Value", $"{it.MonetaryValue:F2}"),
                ("StackLimit", it.StackLimit.ToString()),
                ("Capacities", it.Capacities),
            ],
            Creature cr =>
            [
                ("NamePublic", cr.NamePublic),
                ("Moves/Turn", cr.MovesPerTurn.ToString()),
                ("Notes", cr.Notes),
            ],
            TreasureTable tt =>
            [
                ("Entries", tt.Treasures.Count.ToString()),
                ("Nested", tt.Nested ? "Yes" : "No"),
                ("Suppress", tt.Suppress ? "Yes" : "No"),
            ],
            Encounter e =>
            [
                ("Type", e.Type.ToString()),
                ("Price", $"{e.Price:F2}"),
                ("LootChance", $"{e.LootChance:F2}"),
            ],
            Recipe r =>
            [
                ("Hours", r.Hours.ToString()),
                ("Identify", r.Identify ? "Yes" : "No"),
                ("Scrap", r.Scrap ? "Yes" : "No"),
            ],
            _ => [("EntityId", entity.EntityId), ("ModId", entity.ModId.ToString())],
        };

        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            sp.Children.Add(new TextBlock
            {
                Text = $"{label}: {value}",
                FontSize = 10,
                Foreground = Brush.Parse("#555555"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        sp.Children.Add(new TextBlock
        {
            Text = "Ctrl+Click → open detail",
            FontSize = 9,
            Foreground = Brush.Parse("#999999")
        });

        return new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            Child = sp
        };
    }

    /// <summary>
    /// R42: aFieldNames/aModifiers are comma-paired field→modifier pairs (Doc 38 §5).
    /// Renders "m_fMoveCost +0.5 · m_fVisibility -0.2" — the condition's real effect.
    /// </summary>
    private static string BuildConditionEffectText(Condition c)
    {
        var fields = c.FieldNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mods = c.Modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length == 0) return "";

        var parts = new List<string>(fields.Length);
        for (int i = 0; i < fields.Length; i++)
        {
            var mod = i < mods.Length && double.TryParse(mods[i],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 0;
            parts.Add($"{fields[i]} {mod:+#0.###;-#0.###;0}");
        }
        var text = string.Join(" · ", parts);
        return text.Length > 80 ? text[..80] + "…" : text;
    }

    /// <summary>
    /// R36 (Doc 21 §7 P3): stacked damage bar — Cut/Blunt share ONE bar so the
    /// damage composition (slashing vs blunt weapon) is readable at a glance.
    /// </summary>
    public Control StackedDamageBar(string label, double cut, double blunt, string? rightText = null)
    {
        var total = cut + blunt;
        var grid = new Grid { MinHeight = 24 };
        if (!string.IsNullOrEmpty(label))
            grid.ColumnDefinitions.Add(new(80, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new(120, GridUnitType.Pixel));

        if (!string.IsNullOrEmpty(label))
        {
            var labelTb = new TextBlock
            {
                Text = label, FontSize = 11, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(labelTb, 0);
            grid.Children.Add(labelTb);
        }

        var barCol = string.IsNullOrEmpty(label) ? 0 : 1;
        var valCol = string.IsNullOrEmpty(label) ? 1 : 2;

        if (total <= 0)
        {
            var empty = new Border
            {
                CornerRadius = new CornerRadius(4), Background = Brush.Parse("#14000000"),
                Margin = new Thickness(0, 2), Height = 14
            };
            Grid.SetColumn(empty, barCol);
            grid.Children.Add(empty);
        }
        else
        {
            var cutStar = Math.Clamp((int)(cut / total * 100), 4, 96);
            var bluntStar = 100 - cutStar;
            var bar = new Grid
            {
                ColumnDefinitions =
                {
                    new(cutStar, GridUnitType.Star),
                    new(bluntStar, GridUnitType.Star)
                }
            };
            // R41: low-saturation segments (Material 300) — the raw #C62828/#1565C0
            // were jarring against the grey UI; the bar's job is showing the
            // CUT:BLUNT RATIO (a meaningful proportion), numbers carry the values.
            var cutSeg = new Border
            {
                CornerRadius = new CornerRadius(4, 0, 0, 4),
                Background = Brush.Parse("#E57373"), Margin = new Thickness(0, 2)
            };
            Grid.SetColumn(cutSeg, 0);
            bar.Children.Add(cutSeg);
            var bluntSeg = new Border
            {
                CornerRadius = new CornerRadius(0, 4, 4, 0),
                Background = Brush.Parse("#64B5F6"), Margin = new Thickness(0, 2)
            };
            Grid.SetColumn(bluntSeg, 1);
            bar.Children.Add(bluntSeg);
            Grid.SetColumn(bar, barCol);
            grid.Children.Add(bar);
        }

        var valTb = new TextBlock
        {
            Text = rightText ?? (total > 0
                ? $"{total:F1} · {Loc("Vis.Cut")} {cut:F1} + {Loc("Vis.Blunt")} {blunt:F1}"
                : "—"),
            FontSize = 10, Foreground = Brush.Parse("#666"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(valTb, valCol);
        grid.Children.Add(valTb);
        return grid;
    }

    public Control StatBar(string label, string valueText, double fillRatio, string colorHex)
    {        fillRatio = Math.Clamp(fillRatio, 0.05, 1.0);
        var grid = new Grid { MinHeight = 26 };
        grid.ColumnDefinitions.Add(new(80, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(2, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new(3, GridUnitType.Star));

        var labelTb = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        var fillStar = Math.Max((int)(fillRatio * 100), 6);
        var emptyStar = Math.Max(100 - fillStar, 0);
        grid.ColumnDefinitions[1] = new(fillStar, GridUnitType.Star);
        grid.ColumnDefinitions[2] = new(emptyStar, GridUnitType.Star);

        var fill = new Border
        {
            CornerRadius = new CornerRadius(5),
            Background = Brush.Parse(colorHex),
            Margin = new Thickness(0, 1)
        };
        Grid.SetColumn(fill, 1);
        grid.Children.Add(fill);

        var textOverlay = new TextBlock
        {
            Text = valueText, FontSize = 10, Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0),
            Background = Brushes.Transparent
        };
        Grid.SetColumn(textOverlay, 1);
        Grid.SetColumnSpan(textOverlay, 2);
        grid.Children.Add(textOverlay);
        return grid;
    }

    public Control CenteredStatBar(string label, string valueText, double value, double maxAbs,
        string? posColor = null, string? negColor = null)
    {
        posColor ??= "#2E7D32";
        negColor ??= "#C62828";
        var absRatio = Math.Clamp(Math.Abs(value) / Math.Max(maxAbs, 0.01), 0.08, 1.0);
        var isNeg = value < 0;

        var grid = new Grid { Height = 26 };
        grid.ColumnDefinitions.Add(new(80, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(56, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new(3, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));

        var labelTb = new TextBlock
        {
            Text = label, FontSize = 11, Foreground = Brush.Parse("#999"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(labelTb, 0);
        grid.Children.Add(labelTb);

        var valTb = new TextBlock
        {
            Text = valueText, FontSize = 10, FontWeight = FontWeight.Medium,
            Foreground = Brush.Parse(isNeg ? negColor : value > 0 ? posColor : "#999"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0)
        };
        Grid.SetColumn(valTb, 1);
        grid.Children.Add(valTb);

        var center = new Border { Background = Brush.Parse("#20000000"), Margin = new Thickness(0, 4) };
        Grid.SetColumn(center, 3);
        grid.Children.Add(center);

        if (isNeg)
        {
            var fill = new Border
            {
                CornerRadius = new CornerRadius(4, 0, 0, 4),
                Background = Brush.Parse(negColor),
                Margin = new Thickness(0, 1, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = absRatio * 160, MaxWidth = 160
            };
            Grid.SetColumn(fill, 2);
            grid.Children.Add(fill);
        }
        else if (value > 0)
        {
            var fill = new Border
            {
                CornerRadius = new CornerRadius(0, 4, 4, 0),
                Background = Brush.Parse(posColor),
                Margin = new Thickness(0, 1, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = absRatio * 160, MaxWidth = 160
            };
            Grid.SetColumn(fill, 4);
            grid.Children.Add(fill);
        }

        return grid;
    }

    public Control CreatureStatGrid(List<(string label, string value, string? color)> cells, int cols = 2)
    {
        var grid = new Grid { Margin = new Thickness(4, 0) };
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new(1, GridUnitType.Star));
        int rows = (cells.Count + cols - 1) / cols;
        for (int r = 0; r < rows; r++) grid.RowDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < cells.Count; i++)
        {
            int r = i / cols, c = i % cols;
            var (label, value, color) = cells[i];
            var cell = new StackPanel { Margin = new Thickness(4, 3) };
            cell.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = Brush.Parse("#999") });
            cell.Children.Add(new TextBlock
            {
                Text = value, FontSize = 13, FontWeight = FontWeight.SemiBold,
                Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333")
            });
            Grid.SetRow(cell, r);
            Grid.SetColumn(cell, c);
            grid.Children.Add(cell);
        }
        return Card(grid);
    }

    public Control BuildReverseRefsPanel(string entityId)
    {
        var store = _dataTable.BrowserStore ?? _dataTable.ActiveMergeStore;
        if (store == null) return new StackPanel();
        var rawRefs = store.IndexService?.ReverseLookup(entityId) ?? [];
        if (rawRefs.Count == 0) return new StackPanel();

        var sp = new StackPanel();
        var loadingTb = SectionLabel($"{Loc("Vis.ReferencedBy")} … ({rawRefs.Count} refs)");
        sp.Children.Add(loadingTb);

        var eidMap = new Dictionary<string, (Type SrcType, string SrcSubject)>();
        foreach (var (t, entities) in store.ReferenceLookups)
            foreach (var e in entities)
                if (e is IEntity ie)
                    eidMap[ie.EntityId] = (t, ie.Subject ?? "");

        var capturedRawRefs = rawRefs;
        Task.Run(() =>
        {
            var resolved = new List<(Type SrcType, string SrcSubject, string SrcEid, string PropName)>();
            foreach (var (srcEid, propName, _) in capturedRawRefs)
                if (eidMap.TryGetValue(srcEid, out var info))
                    resolved.Add((info.SrcType, info.SrcSubject, srcEid, propName));

            if (resolved.Count == 0) return;
            var byType = resolved.GroupBy(r => r.SrcType).OrderByDescending(g => g.Count()).ToList();

            Dispatcher.UIThread.Post(() =>
            {
                sp.Children.Clear();
                var typeLabels = byType.Select(g => $"{g.Count()} {g.Key.Name}").ToList();
                sp.Children.Add(SectionLabel($"{Loc("Vis.ReferencedBy")} ({string.Join(", ", typeLabels)})"));

                var tabControl = new TabControl { Margin = new Thickness(0, 4, 0, 0) };
                foreach (var g in byType)
                {
                    var tabItem = new TabItem
                    {
                        Header = $"{g.Key.Name} ({g.Count()})",
                        Content = BuildRefList(g.ToList())
                    };
                    tabControl.Items.Add(tabItem);
                }
                if (tabControl.Items.Count > 0)
                    tabControl.SelectedIndex = 0;
                sp.Children.Add(tabControl);
            });
        });

        return sp;
    }

    private Control BuildRefList(IReadOnlyList<(Type SrcType, string SrcSubject, string SrcEid, string PropName)> items)
    {
        const int pageSize = 15;
        int totalPages = (items.Count + pageSize - 1) / pageSize;
        int currentPage = 0;

        var container = new StackPanel();
        var list = new StackPanel { Spacing = 3 };

        void RefreshPage()
        {
            list.Children.Clear();
            var page = items.Skip(currentPage * pageSize).Take(pageSize).ToList();
            foreach (var (srcType, srcSubject, srcEid, propName) in page)
            {
                var tc = srcType == typeof(Creature) ? ("#E8EAF6", "#283593")
                    : srcType == typeof(ItemType) ? ("#E3F2FD", "#1565C0")
                    : srcType == typeof(Recipe) ? ("#F3E5F5", "#6A1B9A")
                    : srcType == typeof(Condition) ? ("#FCE4EC", "#C62828")
                    : ("#F5F5F5", "#666");
                var row = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = Brush.Parse("#0D000000"),
                    Padding = new Thickness(8, 3),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 6,
                        Children =
                        {
                            new Border
                            {
                                CornerRadius = new CornerRadius(3), Background = Brush.Parse(tc.Item1),
                                Padding = new Thickness(5, 1),
                                Child = new TextBlock { Text = srcType.Name, FontSize = 9, Foreground = Brush.Parse(tc.Item2) }
                            },
                            new TextBlock { Text = srcSubject, FontSize = 11, Foreground = Brush.Parse("#333"), VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock { Text = $"({propName})", FontSize = 9, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center }
                        }
                    }
                };
                var ct = srcType;
                var ci = srcEid;
                row.PointerPressed += (_, e) =>
                {
                    if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
                    if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
                        _router.RequestPeek(ct, ci, null);
                    else
                        _router.NavigateToEntity(ct, ci);
                };
                list.Children.Add(row);
            }
        }

        RefreshPage();
        container.Children.Add(Card(list));

        if (totalPages > 1)
        {
            var pager = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8, Margin = new Thickness(0, 8, 0, 0)
            };
            var prevBtn = new Button
            {
                Content = "←", FontSize = 12, Padding = new Thickness(8, 4),
                Background = Brush.Parse("#0D000000"), BorderThickness = new Thickness(1),
                BorderBrush = Brush.Parse("#18000000"), Cursor = new Cursor(StandardCursorType.Hand)
            };
            var pageLabel = new TextBlock
            {
                Text = $"{Loc("Vis.Page")} 1 / {totalPages}", FontSize = 11,
                Foreground = Brush.Parse("#666"), VerticalAlignment = VerticalAlignment.Center
            };
            var nextBtn = new Button
            {
                Content = "→", FontSize = 12, Padding = new Thickness(8, 4),
                Background = Brush.Parse("#0D000000"), BorderThickness = new Thickness(1),
                BorderBrush = Brush.Parse("#18000000"), Cursor = new Cursor(StandardCursorType.Hand)
            };
            prevBtn.IsEnabled = false;
            prevBtn.Click += (_, _) =>
            {
                if (currentPage > 0)
                {
                    currentPage--;
                    RefreshPage();
                    pageLabel.Text = $"{Loc("Vis.Page")} {currentPage + 1} / {totalPages}";
                    prevBtn.IsEnabled = currentPage > 0;
                    nextBtn.IsEnabled = true;
                }
            };
            nextBtn.Click += (_, _) =>
            {
                if (currentPage < totalPages - 1)
                {
                    currentPage++;
                    RefreshPage();
                    pageLabel.Text = $"{Loc("Vis.Page")} {currentPage + 1} / {totalPages}";
                    nextBtn.IsEnabled = currentPage < totalPages - 1;
                    prevBtn.IsEnabled = true;
                }
            };
            pager.Children.Add(prevBtn);
            pager.Children.Add(pageLabel);
            pager.Children.Add(nextBtn);
            container.Children.Add(pager);
        }

        return container;
    }

    public Control BuildExpander(string label, Border body)
    {
        var arrow = new TextBlock
        {
            Text = "▶", FontSize = 10, Foreground = Brush.Parse("#999"), VerticalAlignment = VerticalAlignment.Center
        };
        var labelTb = new TextBlock
        {
            Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#888"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var expanded = false;
        var header = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#06000000"),
            Padding = new Thickness(12, 6),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { arrow, labelTb } }
        };
        header.PointerPressed += (_, _) =>
        {
            expanded = !expanded;
            arrow.Text = expanded ? "▼" : "▶";
            body.IsVisible = expanded;
        };
        return header;
    }

    public void OpenZoomableImage(Bitmap? bitmap, string? title = null)
    {
        if (bitmap is null) return;
        var zoomView = new ZoomableImageView { Source = bitmap, Width = 600, Height = 480 };
        var headerBorder = new Border
        {
            Background = Brush.Parse("#06000000"),
            Padding = new Thickness(16, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8, Children =
                {
                    new TextBlock { Text = title ?? "Image Preview", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#555"), VerticalAlignment = VerticalAlignment.Center },
                    new Button { Content = "✕", FontSize = 14, Padding = new Thickness(8, 2), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brush.Parse("#999"), HorizontalAlignment = HorizontalAlignment.Right, Cursor = new Cursor(StandardCursorType.Hand) }
                }
            }
        };
        DockPanel.SetDock(headerBorder, Avalonia.Controls.Dock.Top);
        var closeBtn = (Button)((StackPanel)headerBorder.Child).Children[1];
        var popup = new Popup
        {
            PlacementTarget = null,
            Placement = PlacementMode.Center,
            Child = new Border
            {
                Width = 640, Height = 520, CornerRadius = new CornerRadius(12),
                Background = Brush.Parse("#F8F8F8"), BorderBrush = Brush.Parse("#20000000"),
                BorderThickness = new Thickness(1), ClipToBounds = true,
                Child = new DockPanel
                {
                    Children = { headerBorder, new Border { Child = zoomView, Margin = new Thickness(8, 0, 8, 8) } }
                }
            },
            IsOpen = true
        };
        closeBtn.Click += (_, _) => popup.IsOpen = false;
    }

    public TextBlock OvSectionLabel(string text) => new()
    {
        Text = text, FontSize = 10, FontWeight = FontWeight.SemiBold,
        Foreground = Brush.Parse("#888888"), Margin = new Thickness(0, 0, 0, 4)
    };

    public void AddModBadge(IEntity entity, StackPanel row)
    {
        var modName = _dataTable.EntityModNames.TryGetValue(entity.EntityId, out var mn) ? mn : $"mod_{entity.ModId}";
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse(entity.ModId >= 10000 ? "#1B5E20" : "#1565C0"),
            Padding = new Thickness(6, 2),
            Child = new TextBlock { Text = $"{entity.ModId}:{modName}", FontSize = 10, Foreground = Brushes.White }
        });
        var mergedId = _dataTable.EntityMergedIds.TryGetValue(entity.EntityId, out var mid) ? mid : 0;
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#E65100"),
            Padding = new Thickness(5, 2),
            Child = new TextBlock { Text = $"mid={mergedId}", FontSize = 10, Foreground = Brushes.White }
        });
        var pkProp = EntityHelper.ResolveKeyProperty(entity.GetType());
        var pkVal = pkProp?.GetValue(entity) is int pk ? pk : -1;
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#6A1B9A"),
            Padding = new Thickness(5, 2),
            Child = new TextBlock { Text = $"pk={pkVal}", FontSize = 10, Foreground = Brushes.White }
        });
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Background = Brush.Parse("#37474F"),
            Padding = new Thickness(5, 2),
            Child = new TextBlock { Text = entity.EntityId.Length > 10 ? entity.EntityId[..10] : entity.EntityId, FontSize = 9, Foreground = Brushes.White }
        });
    }

    public Control BuildStatCard(List<(string label, string value, string? color)> rows)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(90, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(4, 0)
        };
        for (int i = 0; i < rows.Count; i++)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));
            var (label, value, color) = rows[i];
            var lbl = new TextBlock
            {
                Text = label, FontSize = 10, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 1)
            };
            var val = new TextBlock
            {
                Text = value, FontSize = 10, FontWeight = FontWeight.Medium,
                Foreground = color is not null ? Brush.Parse(color) : Brush.Parse("#333"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1)
            };
            Grid.SetRow(lbl, i); Grid.SetColumn(lbl, 0);
            Grid.SetRow(val, i); Grid.SetColumn(val, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(val);
        }
        return Card(grid);
    }
}
