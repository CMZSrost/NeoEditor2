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
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.Services;

namespace NeoEditor.Plugins.EntityEditor.Visualizers;

public class AttackModeEntityVisualizer : IEntityVisualizer
{
    public Type EntityType => typeof(AttackMode);

    private readonly VisHelperService _vis;
    private readonly Services.RefNode _refNode;

    // ═══════════════ Detail ═══════════════

    public AttackModeEntityVisualizer(VisHelperService vis, Services.RefNode? refNode)
    {
        _vis = vis;
        _refNode = refNode ?? new Services.RefNode(
            vis.Resolver,
            vis.Router,
            vis.BuildRefTooltip);
    }

    public Control BuildDetail(IEntity entity)
    {
        if (entity is not AttackMode am) return new TextBlock { Text = "Invalid" };
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(16) };

        root.Children.Add(_vis.BuildRawData(am));

        root.Children.Add(BuildHeroHeader(am));
        root.Children.Add(BuildCombatPanel(am));

        if (!string.IsNullOrWhiteSpace(am.ChargeProfiles))
            root.Children.Add(BuildChargePanel(am));

        if (!string.IsNullOrWhiteSpace(am.AttackerConditions))
            root.Children.Add(BuildConditionsPanel(am));

        if (!string.IsNullOrWhiteSpace(am.AttackPhrases))
            root.Children.Add(BuildAttackPhrasesPanel(am));

        root.Children.Add(BuildReverseRefsPanel(am));

        return new ScrollViewer { Content = root, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    // ═══════════════ Hero Header ═══════════════

    private Control BuildHeroHeader(AttackMode am)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new(140, GridUnitType.Pixel), new(1, GridUnitType.Star) },
            Margin = new Thickness(0, 0, 0, 4)
        };

        var bmp = _vis.LoadImage(am.Image);
        var imageArea = new Border
        {
            Width = 132, Height = 132,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Background = Brush.Parse("#0A000000"),
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        if (bmp is not null)
        {
            imageArea.Child = new Image { Source = bmp, Stretch = Stretch.Uniform, Width = 132, Height = 132 };
            var capturedBmp = bmp;
            imageArea.PointerPressed += (_, _) => _vis.OpenZoomableImage(capturedBmp, am.Subject ?? am.Name);
        }
        else
        {
            var iconSymbol = GetSoundSymbol(am.Sound) ?? (am.Type == AttackType.Ranged ? Symbol.Target : Symbol.Flash);
            imageArea.Child = new SymbolIcon
            {
                Symbol = iconSymbol, FontSize = 40, Foreground = Brush.Parse("#999"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        }

        Grid.SetColumn(imageArea, 0);
        grid.Children.Add(imageArea);

        var identity = new StackPanel
            { Spacing = 4, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

        var idRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse("#E3F2FD"),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = $"ID: {am.Id}", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse("#1565C0")
            }
        });
        var rangeStr = am.Range <= 1 ? "1 tile" : $"{am.Range} tiles";
        var typeLabel = am.Type == AttackType.Ranged ? $"Ranged ({rangeStr})" : $"Melee ({rangeStr})";
        var typeBg = am.Type == AttackType.Ranged ? "#FCE4EC" : "#E8F5E9";
        var typeFg = am.Type == AttackType.Ranged ? "#C62828" : "#2E7D32";
        idRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = Brush.Parse(typeBg),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
                { Text = typeLabel, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush.Parse(typeFg) }
        });
        _vis.AddModBadge(am, idRow);
        identity.Children.Add(idRow);
        var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        identity.Children.Add(infoRow);

        identity.Children.Add(new TextBlock
            { Text = am.Name, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(am.WieldPhrase))
        {
            var quote = am.WieldPhrase.Length > 120 ? am.WieldPhrase[..120] + "..." : am.WieldPhrase;
            identity.Children.Add(new TextBlock
            {
                Text = $"\"{quote}\"", FontSize = 12, FontStyle = FontStyle.Italic, Foreground = Brush.Parse("#666"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(am.Notes))
            identity.Children.Add(new TextBlock
            {
                Text = am.Notes, FontSize = 12, Foreground = Brush.Parse("#888888"), TextWrapping = TextWrapping.Wrap
            });

        Grid.SetColumn(identity, 1);
        Grid.SetRow(identity, 0);
        grid.Children.Add(identity);

        return _vis.Card(grid);
    }

    // ═══════════════ Combat Panel ═══════════════

    private Control BuildCombatPanel(AttackMode am)
    {
        var sp = new StackPanel();

        var isRanged = am.Type == AttackType.Ranged;
        var iconSymbol = isRanged ? Symbol.Target : Symbol.Flash;
        var iconBg = isRanged ? "#FCE4EC" : "#E8F5E9";
        var iconFg = isRanged ? "#C62828" : "#2E7D32";
        var headerRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        headerRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12), Width = 24, Height = 24,
            Background = Brush.Parse(iconBg),
            Child = new SymbolIcon
            {
                Symbol = iconSymbol, FontSize = 14, Foreground = Brush.Parse(iconFg),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        });
        headerRow.Children.Add(new TextBlock
        {
            Text = isRanged ? _vis.Loc("Vis.CombatRanged") : _vis.Loc("Vis.CombatMelee"), FontSize = 13,
            FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#555"),
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(headerRow);

        var bars = new StackPanel { Spacing = 6 };

        var rangeMax = Math.Max(am.Range, 10);
        bars.Children.Add(_vis.StatBar(_vis.Loc("Range"), $"{am.Range} {_vis.Loc("Vis.Tiles")}",
            am.Range / (double)rangeMax, "#607D8B"));

        var maxDmg = Math.Max(am.DamageCut, Math.Max(am.DamageBlunt, 2.0));
        if (am.DamageCut > 0)
            bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Cut"), $"{am.DamageCut:F1}", am.DamageCut / maxDmg,
                "#E53935"));
        if (am.DamageBlunt > 0)
            bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Blunt"), $"{am.DamageBlunt:F1}",
                am.DamageBlunt / maxDmg, "#FB8C00"));

        var totalDmg = am.DamageCut + am.DamageBlunt;
        var moralePct = (int)(am.Morale * 100);
        var moraleLabel = moralePct == 25 ? $"{moralePct}% (base)" : $"{moralePct}%";
        var moraleColor = am.Morale > 0.25 ? "#2E7D32" : am.Morale < 0.25 ? "#C62828" : "#78909C";
        bars.Children.Add(_vis.StatBar(_vis.Loc("Morale"), moraleLabel, am.Morale, moraleColor));

        if (totalDmg > 0)
        {
            var effectiveDmg = totalDmg * (1 + am.Morale);
            var effLabel = $"{effectiveDmg:F1} ({1 + am.Morale:F2} × {totalDmg:F1})";
            bars.Children.Add(_vis.StatBar(_vis.Loc("Vis.Effective"), effLabel,
                Math.Clamp(effectiveDmg / 8.0, 0.05, 1.0), "#6A1B9A"));
        }

        if (am.Penetration > 0)
        {
            var penRow = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            penRow.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Penetration"), FontSize = 11, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center
            });
            var penDots = new string('●', am.Penetration) + new string('○', Math.Max(0, 4 - am.Penetration));
            penRow.Children.Add(new TextBlock
            {
                Text = $"{penDots}  Lv.{am.Penetration}", FontSize = 11, Foreground = Brush.Parse("#6A1B9A"),
                VerticalAlignment = VerticalAlignment.Center
            });
            bars.Children.Add(penRow);
        }

        if (!string.IsNullOrWhiteSpace(am.Sound))
        {
            var sndRow = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            sndRow.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Sound"), FontSize = 11, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center
            });
            sndRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = Brush.Parse("#F3E5F5"),
                Padding = new Thickness(8, 2),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = "▶", FontSize = 9, Foreground = Brush.Parse("#7B1FA2") },
                        new TextBlock { Text = am.Sound, FontSize = 10, Foreground = Brush.Parse("#7B1FA2") }
                    }
                }
            });
            // R48: play the attack sound right from the row (when extracted assets exist).
            var sndBtn = _vis.PlaySoundButton(am.Sound);
            if (sndBtn is not null)
                sndRow.Children.Add(sndBtn);
            bars.Children.Add(sndRow);
        }

        if (am.Transfer)
        {
            var tRow = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 2, 0, 0) };
            tRow.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Transfer"), FontSize = 11, Foreground = Brush.Parse("#999"),
                VerticalAlignment = VerticalAlignment.Center
            });
            tRow.Children.Add(new TextBlock
            {
                Text = _vis.Loc("Vis.TransferDesc"), FontSize = 11, Foreground = Brush.Parse("#558B2F"),
                VerticalAlignment = VerticalAlignment.Center
            });
            bars.Children.Add(tRow);
        }

        sp.Children.Add(_vis.Card(bars));
        return sp;
    }

    // ═══════════════ Charge Profiles ═══════════════

    private Control BuildChargePanel(AttackMode am)
    {
        var sp = new StackPanel();
        var parts = am.ChargeProfiles.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        sp.Children.Add(
            _vis.SectionLabel($"{_vis.Loc("Vis.Ammo")} ({parts.Count} type{(parts.Count > 1 ? "s" : "")})"));

        var wp = new WrapPanel();
        foreach (var raw in parts)
            wp.Children.Add(_refNode.Badge<ChargeProfile>(am, nameof(AttackMode.ChargeProfiles), raw,
                "#E0F7FA", "#006064", unresolvedFg: "#999"));

        sp.Children.Add(_vis.Card(wp));
        return sp;
    }

    // ═══════════════ Attacker Conditions ═══════════════

    private Control BuildConditionsPanel(AttackMode am)
    {
        var sp = new StackPanel();
        sp.Children.Add(_vis.SectionLabel(_vis.Loc("Vis.AttackerConditions")));

        var pattern = ReferencePattern.FromName("{id}x{mult}");
        var wp = new WrapPanel();
        foreach (var seg in am.AttackerConditions.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            var cond = _vis.Resolver.LookupRef<Condition>(am, nameof(AttackMode.AttackerConditions), seg);
            if (cond is not null)
            {
                var extra = pattern.FormatExtraInfo(seg);
                var display = string.IsNullOrEmpty(extra) ? cond.Subject : $"{cond.Subject} {extra}";
                wp.Children.Add(_refNode.BadgeForEntity<Condition>(am, cond, display, "#FCE4EC", "#C62828"));
            }
            else
            {
                wp.Children.Add(_vis.MiniBadge(seg, "#F5F5F5", "#999"));
            }
        }

        sp.Children.Add(_vis.Card(wp));
        return sp;
    }

    // ═══════════════ Attack Phrases ═══════════════

    private Control BuildAttackPhrasesPanel(AttackMode am)
    {
        var sp = new StackPanel();
        var phrases = am.AttackPhrases.Split(',', '，').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        sp.Children.Add(_vis.SectionLabel($"{_vis.Loc("Vis.AttackPhrases")} ({phrases.Count})"));

        var wp = new WrapPanel();
        foreach (var p in phrases)
        {
            var display = p.Length > 60 ? p[..60] + "..." : p;
            wp.Children.Add(_vis.MiniBadge(display, "#E3F2FD", "#1565C0"));
        }

        sp.Children.Add(_vis.Card(wp));
        return sp;
    }

    // ═══════════════ Reverse References (via store's pre-built ReferenceIndex) ═══════════════

    private Control BuildReverseRefsPanel(AttackMode am)
        => _vis.BuildReverseRefsPanel(am.EntityId);

    // ═══════════════ Sound icon helpers ═══════════════

    private static Symbol? GetSoundSymbol(string? sound) => sound?.ToLowerInvariant() switch
    {
        "punch" or "claws" or "grasp" => Symbol.Flash,
        "club" => Symbol.Flash,
        "blade" => Symbol.Cut,
        "rifle" or "pistol" => Symbol.Target,
        "bow" or "throw" => Symbol.Target,
        "laser" => Symbol.Flash,
        "bite" => Symbol.Warning,
        "choke" => Symbol.Dismiss,
        _ => null
    };
}
