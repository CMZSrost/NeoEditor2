using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.EntityEditor.Services;

/// <summary>
/// Injectable reference node renderer (M3).
/// M10: moved from App/Helper to EntityEditor Plugin.
/// Dependencies (IReferenceResolver, INavigationRouter) are Core/Infra interfaces — safe for Plugin layer.
/// R30: optional tooltipBuilder (VisHelperService.BuildRefTooltip) attaches a hover
/// preview panel to resolved badges (Doc 21 §7 P6).
/// </summary>
public class RefNode
{
    private readonly IReferenceResolver? _resolver;
    private readonly INavigationRouter? _router;
    private readonly Func<IEntity, Control?>? _tooltipBuilder;

    public RefNode(IReferenceResolver? resolver, INavigationRouter? router,
        Func<IEntity, Control?>? tooltipBuilder = null)
    {
        _resolver = resolver;
        _router = router;
        _tooltipBuilder = tooltipBuilder;
    }

    /// <summary>
    /// Build a clickable badge for a raw reference segment.
    /// Resolves the reference via IReferenceResolver and wires Ctrl+Click / Ctrl+RMB navigation.
    /// </summary>
    public Control Badge<T>(IEntity sourceEntity, string propertyName, string rawSegment,
        string resolvedBg, string resolvedFg, string? unresolvedBg = null, string? unresolvedFg = null) where T : IEntity
    {
        var entity = _resolver?.LookupRef<T>(sourceEntity, propertyName, rawSegment);
        var label = entity?.Subject ?? rawSegment;
        var isResolved = entity is not null;
        var bg = isResolved ? ColorFromHex(resolvedBg) : ColorFromHex(unresolvedBg ?? "#E0E0E0");
        var fg = isResolved ? ColorFromHex(resolvedFg) : ColorFromHex(unresolvedFg ?? "#9E9E9E");

        var badge = BuildBadge(label, bg, fg);
        if (isResolved && entity is not null)
        {
            WireNavigation(badge, typeof(T), entity.EntityId, sourceEntity);
            AttachTooltip(badge, entity);
        }

        return badge;
    }

    /// <summary>
    /// Build a clickable badge with an extra slot label prefix.
    /// </summary>
    public Control BadgeWithSlot<T>(IEntity sourceEntity, string propertyName, string rawSegment,
        string slotName, string resolvedBg, string resolvedFg,
        string? unresolvedBg = null, string? unresolvedFg = null) where T : IEntity
    {
        var entity = _resolver?.LookupRef<T>(sourceEntity, propertyName, rawSegment);
        var label = entity?.Subject ?? rawSegment;
        var isResolved = entity is not null;
        var bg = isResolved ? ColorFromHex(resolvedBg) : ColorFromHex(unresolvedBg ?? "#E0E0E0");
        var fg = isResolved ? ColorFromHex(resolvedFg) : ColorFromHex(unresolvedFg ?? "#9E9E9E");

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = slotName, FontSize = 10, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(fg), VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(BuildBadge(label, bg, fg));
        if (isResolved && entity is not null)
        {
            WireNavigation(panel, typeof(T), entity.EntityId, sourceEntity);
            AttachTooltip(panel, entity);
        }

        return panel;
    }

    /// <summary>
    /// Build a clickable badge for an already-resolved entity (generic — preserves entity type).
    /// </summary>
    public Control BadgeForEntity<T>(IEntity sourceEntity, IEntity targetEntity, string label,
        string bgHex, string fgHex) where T : IEntity
    {
        var bg = ColorFromHex(bgHex);
        var fg = ColorFromHex(fgHex);
        var badge = BuildBadge(label, bg, fg);
        WireNavigation(badge, typeof(T), targetEntity.EntityId, sourceEntity);
        AttachTooltip(badge, targetEntity);
        return badge;
    }

    /// <summary>
    /// Build a clickable badge for an already-resolved entity (non-generic — type from targetEntity).
    /// </summary>
    public Control BadgeForEntity(IEntity sourceEntity, IEntity targetEntity, string label,
        string bgHex, string fgHex)
    {
        var bg = ColorFromHex(bgHex);
        var fg = ColorFromHex(fgHex);
        var badge = BuildBadge(label, bg, fg);
        WireNavigation(badge, targetEntity.GetType(), targetEntity.EntityId, sourceEntity);
        AttachTooltip(badge, targetEntity);
        return badge;
    }

    /// <summary>
    /// Wire Ctrl+Click (navigate) and Ctrl+RMB (peek) to a control.
    /// </summary>
    public void WireNavigation(Control control, Type targetType, string targetEntityId,
        IEntity? sourceEntity = null)
    {
        control.PointerPressed += (_, e) =>
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
            e.Handled = true;
            if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed)
                _router?.RequestPeek(targetType, targetEntityId, sourceEntity);
            else
                _router?.NavigateToEntity(targetType, targetEntityId);
        };
    }

    /// <summary>
    /// Create a deferred navigation action for use in buttons/menus.
    /// </summary>
    public Action NavAction(Type targetType, string targetEntityId)
    {
        return () => _router?.NavigateToEntity(targetType, targetEntityId);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>R30 (Doc 21 §7 P6): attach the hover preview panel to a resolved badge.</summary>
    private void AttachTooltip(Control control, IEntity targetEntity)
    {
        if (_tooltipBuilder is null) return;
        var tip = _tooltipBuilder(targetEntity);
        if (tip is not null)
            ToolTip.SetTip(control, tip);
    }

    private static Border BuildBadge(string label, Color bg, Color fg)
    {
        return new Border
        {
            Background = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(1),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(fg),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Color ColorFromHex(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length == 6)
            return Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        if (hex.Length == 8)
            return Color.FromArgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16));
        return Colors.Gray;
    }
}
