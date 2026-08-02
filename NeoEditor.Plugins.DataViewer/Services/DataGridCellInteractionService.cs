using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;


namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Extracted Ctrl+Click / Ctrl+Hover / context-menu interaction logic from
/// GenericDataGridHelper.ConfigureColumn. Registered as a DI singleton so
/// all dependencies are constructor-injected (R07).
///
/// The service creates and attaches Avalonia event handlers to the UI elements
/// produced by ConfigureColumn — it is a View-layer service by design.
/// </summary>
public class DataGridCellInteractionService : IDataGridCellInteractionService
{
    private readonly IDataGridNavigationService _nav;
    private readonly INavigationRouter _router;
    private readonly IReferenceResolver _resolver;
    private readonly DataGridInteractionState _state;

    public DataGridCellInteractionService(
        IDataGridNavigationService nav,
        INavigationRouter router,
        IReferenceResolver resolver,
        DataGridInteractionState state)
    {
        _nav = nav;
        _router = router;
        _resolver = resolver;
        _state = state;
    }

    // ── Single-value reference cell ──────────────────────────────────────

    public void AttachSingleRefHandlers(Grid grid, object rowItem, PropertyInfo property,
        Type targetType, ReferenceFieldAttribute refAttr, string pattern,
        string propertyName, string refColName)
    {
        // Ctrl+Hover: show jump target tooltip
        grid.AddHandler(InputElement.PointerMovedEvent, (_, pmArgs) =>
        {
            try
            {
                if (pmArgs is PointerEventArgs pe && (pe.KeyModifiers & KeyModifiers.Control) != 0)
                {
                    var currentRaw = ReferenceText.GetRawString(property.GetValue(rowItem), refAttr);
                    var rawId = ReferenceParser.ExtractRawId(currentRaw, pattern);
                    var subject = LookupSubjectByRawId(targetType, rawId,
                        (rowItem as IEntity)?.EntityId ?? "", propertyName,
                        refAttr.TargetKey, refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey);
                    if (!string.IsNullOrEmpty(subject))
                        ToolTip.SetTip(grid, $"{targetType.Name}: {subject} ({rawId})");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Verbose(ex, "[CellInteraction:Hover] Single-val hover threw for {TargetType}",
                    targetType.Name);
            }
        }, RoutingStrategies.Bubble, true);

        // Ctrl+LeftClick = navigate  |  Ctrl+RightClick = peek
        grid.AddHandler(InputElement.PointerPressedEvent, (_, args) =>
        {
            try
            {
                if (args is PointerPressedEventArgs pp && (pp.KeyModifiers & KeyModifiers.Control) != 0)
                {
                    _state.CtrlWasPressed = true;
                    _state.SuppressNextSelectionChanged = true;
                    var currentRaw = ReferenceText.GetRawString(property.GetValue(rowItem), refAttr);
                    var rawId = ReferenceParser.ExtractRawId(currentRaw, pattern);
                    if (string.IsNullOrWhiteSpace(rawId)) return;
                    pp.Handled = true;

                    var point = pp.GetCurrentPoint(grid);
                    if (point.Properties.IsRightButtonPressed)
                    {
                        var srcEid = (rowItem as IEntity)?.EntityId ?? "";
                        var target = _nav.FindBestMatch(targetType, rawId, refAttr.TargetKey, srcEid, propertyName)
                                     ?? (refAttr.SecondaryTargetEntityType is not null
                                         ? _nav.FindBestMatch(refAttr.SecondaryTargetEntityType, rawId,
                                             refAttr.SecondaryTargetKey, srcEid, propertyName)
                                         : null)
                                     ?? (int.TryParse(rawId, out var intId) && intId >= 0
                                         ? _nav.FindBestMatch(targetType, intId.ToString(), null, srcEid, propertyName)
                                         : null);
                        _router.RequestPeek(targetType, target?.EntityId ?? rawId, target);
                        _state.SuppressNextSelectionChanged = true;
                    }
                    else
                    {
                        _nav.NavigateToReference(targetType, rawId, refAttr.TargetKey,
                            refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey,
                            (rowItem as IEntity)?.EntityId ?? "", propertyName);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Error(ex, "[CellInteraction:PtrPressed] Single-val handler threw for {TargetType}",
                    targetType.Name);
            }
        }, RoutingStrategies.Tunnel, true);

        // Suppress right-click context menu after Ctrl+Click
        grid.AddHandler(Control.ContextRequestedEvent, (_, ctxArgs) =>
        {
            if (_state.CtrlWasPressed)
            {
                ctxArgs.Handled = true;
                _state.CtrlWasPressed = false;
            }
        }, RoutingStrategies.Bubble, true);
    }

    // ── Multi-value reference cell: per-segment handlers ─────────────────

    public void AttachMultiRefSegmentHandlers(Border segBorder, string rawSegment,
        object rowItem, Type targetType, ReferenceFieldAttribute refAttr,
        string pattern, string propertyName, string refColName)
    {
        // Ctrl+Hover: show subject + rawId tooltip
        segBorder.AddHandler(InputElement.PointerMovedEvent, (_, pmArgs) =>
        {
            try
            {
                if (pmArgs is PointerEventArgs pe && (pe.KeyModifiers & KeyModifiers.Control) != 0)
                {
                    var partRawId = ReferenceParser.ExtractRawId(rawSegment, pattern);
                    var subject = LookupSubjectByRawId(targetType, partRawId,
                        (rowItem as IEntity)?.EntityId ?? "", propertyName,
                        refAttr.TargetKey, refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey);
                    var label = string.IsNullOrEmpty(subject) ? rawSegment : $"{subject} ({partRawId})";
                    ToolTip.SetTip(segBorder, $"{targetType.Name}: {label}");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Verbose(ex, "[CellInteraction:Hover] Multi-seg hover threw for {TargetType}",
                    targetType.Name);
            }
        }, RoutingStrategies.Bubble, true);

        // Suppress context menu (checked by wrapPanel-level handler)
        segBorder.AddHandler(Control.ContextRequestedEvent, (_, ctxArgs) =>
        {
            if (_state.CtrlWasPressed)
            {
                ctxArgs.Handled = true;
                _state.CtrlWasPressed = false;
            }
        }, RoutingStrategies.Bubble, true);
    }

    // ── Multi-value reference cell: cell-wide Ctrl+Click ─────────────────

    public void AttachMultiRefCellHandler(WrapPanel wrapPanel, object rowItem,
        Type targetType, ReferenceFieldAttribute refAttr, string pattern,
        string propertyName)
    {
        wrapPanel.AddHandler(InputElement.PointerPressedEvent, (_, args) =>
        {
            try
            {
                if (args is not PointerPressedEventArgs pp
                    || (pp.KeyModifiers & KeyModifiers.Control) == 0) return;
                _state.CtrlWasPressed = true;
                _state.SuppressNextSelectionChanged = true;

                // Find which segment was clicked: look for TextBlock with Tag
                var clickedVisual = pp.Source as Avalonia.Visual;
                TextBlock? clickedTb = clickedVisual as TextBlock;
                if (clickedTb?.Tag is not string)
                {
                    if (clickedVisual is Border { Child: TextBlock { Tag: string } bt })
                        clickedTb = bt;
                }

                var rawSegment = clickedTb?.Tag as string;
                if (string.IsNullOrWhiteSpace(rawSegment)) return;

                var clickedRawId = ReferenceParser.ExtractRawId(rawSegment, pattern);
                if (string.IsNullOrWhiteSpace(clickedRawId)) return;

                pp.Handled = true;
                var point = pp.GetCurrentPoint(wrapPanel);

                if (point.Properties.IsRightButtonPressed)
                {
                    var srcEid = (rowItem as IEntity)?.EntityId ?? "";
                    var target = _nav.FindBestMatch(targetType, clickedRawId, refAttr.TargetKey, srcEid, propertyName)
                                 ?? (refAttr.SecondaryTargetEntityType is not null
                                     ? _nav.FindBestMatch(refAttr.SecondaryTargetEntityType, clickedRawId,
                                         refAttr.SecondaryTargetKey, srcEid, propertyName)
                                     : null)
                                 ?? (int.TryParse(clickedRawId, out var intId) && intId >= 0
                                     ? _nav.FindBestMatch(targetType, intId.ToString(), null, srcEid, propertyName)
                                     : null);
                    _router.RequestPeek(targetType, target?.EntityId ?? clickedRawId, target);
                    _state.SuppressNextSelectionChanged = true;
                }
                else
                {
                    _nav.NavigateToReference(targetType, clickedRawId, refAttr.TargetKey,
                        refAttr.SecondaryTargetEntityType, refAttr.SecondaryTargetKey,
                        (rowItem as IEntity)?.EntityId ?? "", propertyName);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Logger.Error(ex,
                    "[CellInteraction:PtrPressed] Multi-val wrap handler threw for {TargetType}", targetType.Name);
            }
        }, RoutingStrategies.Tunnel, true);
    }

    // ── Display formatting ───────────────────────────────────────────────

    public string FormatSegmentDisplay(string segment, Type targetType, string? pattern,
        string sourceEntityId, string propertyName, string? targetKey)
    {
        if (string.IsNullOrWhiteSpace(segment)) return segment;

        var pat = ReferencePattern.FromName(pattern);
        var rawId = pat.ExtractRawId(segment);
        var parsed = ReferenceParser.ParseWithPattern(segment, pattern);

        var subject = LookupSubjectByRawId(targetType, rawId, sourceEntityId, propertyName, targetKey);
        if (string.IsNullOrEmpty(subject)) return segment;

        return pat.FormatDisplay(segment, subject, parsed.ModName);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private string? LookupSubjectByRawId(Type entityType, string rawId,
        string sourceEntityId, string propertyName,
        string? targetKey = null,
        Type? secondaryEntityType = null, string? secondaryTargetKey = null)
    {
        return _resolver.LookupSubject(sourceEntityId, propertyName, entityType, rawId, secondaryEntityType);
    }
}