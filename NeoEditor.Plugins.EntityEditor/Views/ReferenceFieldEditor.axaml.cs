using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using Serilog;

namespace NeoEditor.Plugins.EntityEditor.Views;

/// <summary>
/// Inline editor control for reference fields in the KeyValueEditor.
/// Replaces the plain TextBox with resolved entity name badges,
/// an edit button (opens <see cref="ReferencePickerDialog"/>),
/// and a peek button.
/// </summary>
public partial class ReferenceFieldEditor : UserControl
{
    private FieldRow? _fieldRow;
    private ReferenceFieldAttribute? _refAttr;

    private static T GetService<T>() where T : notnull
        => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

    public ReferenceFieldEditor()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous FieldRow
        if (_fieldRow is not null)
            _fieldRow.PropertyChanged -= OnFieldRowPropertyChanged;

        _fieldRow = DataContext as FieldRow;

        if (_fieldRow is not null)
        {
            _fieldRow.PropertyChanged += OnFieldRowPropertyChanged;
            RefreshBadges();
        }
        else
        {
            BadgePanel.Children.Clear();
            BadgePanel.Children.Add(EmptyPlaceholder);
            EmptyPlaceholder.IsVisible = true;
        }
    }

    private void OnFieldRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FieldRow.CurrentValue))
            RefreshBadges();
    }

    /// <summary>
    /// Rebuild the badge display from the current field value.
    /// Resolves each reference entry to its target entity's display name.
    /// </summary>
    private void RefreshBadges()
    {
        BadgePanel.Children.Clear();
        BadgePanel.Children.Add(EmptyPlaceholder);
        EmptyPlaceholder.IsVisible = true;

        if (_fieldRow?.Property is null) return;
        if (string.IsNullOrWhiteSpace(_fieldRow.CurrentValue)) return;

        // Cache the ReferenceFieldAttribute
        _refAttr = _fieldRow.Property.GetCustomAttribute<ReferenceFieldAttribute>();
        if (_refAttr is null) return;

        try
        {
            var serializer = GetService<IReferenceListSerializer>();
            var lookup = GetService<IEntityLookupService>();
            var refList = serializer.Deserialize(_fieldRow.CurrentValue, _refAttr);

            if (refList.Count == 0)
            {
                EmptyPlaceholder.Text = "(empty)";
                return;
            }

            EmptyPlaceholder.IsVisible = false;

            foreach (var entry in refList)
            {
                var badge = CreateBadge(entry, lookup);
                BadgePanel.Children.Add(badge);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[RefFieldEditor] Failed to parse reference value '{Val}'", _fieldRow.CurrentValue);
            EmptyPlaceholder.Text = _fieldRow.CurrentValue;
        }
    }

    /// <summary>
    /// Build a badge Border for one reference entry.
    /// Shows the resolved entity Subject with EntityId as subtitle.
    /// R30: Ctrl+Click navigates to the target detail (same semantics as the DataGrid),
    /// Ctrl+RMB opens the Peek panel, hover shows the P6 preview panel.
    /// </summary>
    private Border CreateBadge(IReferenceEntry entry, IEntityLookupService lookup)
    {
        // Drill to base EntityRef
        var baseRef = GetBaseEntityRef(entry);
        var rawId = entry.ToRawString();
        var displayName = rawId;
        IEntity? resolved = null;

        if (baseRef is not null && _refAttr is not null)
        {
            // ToRawString() preserves the namespace prefix ("NSE:42") and composite
            // key ("86.6") — FindBestMatch must NOT receive a namespace-stripped key,
            // or mod references resolve against the game namespace instead.
            var lookupKey = baseRef.ToRawString();

            // R30: primary type first, then secondary (aTreasures → nested TreasureTable).
            resolved = lookup.FindBestMatch(
                _refAttr.TargetEntityType, lookupKey, _refAttr.TargetKey);
            if (resolved is null && _refAttr.SecondaryTargetEntityType is not null)
                resolved = lookup.FindBestMatch(
                    _refAttr.SecondaryTargetEntityType, lookupKey, _refAttr.SecondaryTargetKey);
            if (resolved is not null)
                displayName = resolved.Subject ?? lookupKey;
        }

        var stack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        stack.Children.Add(new TextBlock
        {
            Text = displayName,
            FontWeight = FontWeight.SemiBold,
            FontSize = 10,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = rawId,
            FontSize = 8,
            Foreground = Brushes.Gray,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        });

        var badge = new Border
        {
            Background = Brushes.LightGray,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(0, 0, 4, 2),
            Child = stack
        };

        if (resolved is not null && _refAttr is not null)
        {
            var targetType = _refAttr.TargetEntityType;
            var targetEid = resolved.EntityId;
            var targetEntity = resolved;

            badge.Cursor = new Cursor(StandardCursorType.Hand);
            badge.PointerPressed += (_, e) =>
            {
                var action = ResolveClickAction(e.KeyModifiers,
                    e.GetCurrentPoint(null).Properties.IsRightButtonPressed);
                if (!action.Navigate && !action.Peek) return;
                e.Handled = true;
                var nav = GetService<INavigationRouter>();
                if (action.Peek)
                    nav.RequestPeek(targetType, targetEid, targetEntity);
                else
                    nav.NavigateToEntity(targetType, targetEid, targetEntity);
            };

            // P6: hover preview of the resolved target entity.
            var vis = GetService<VisHelperService>();
            var preview = vis.BuildRefTooltip(resolved);
            if (preview is not null)
                ToolTip.SetTip(badge, preview);
        }

        return badge;
    }

    /// <summary>
    /// R30 (C1): same Ctrl semantics as the DataGrid — plain click does nothing,
    /// Ctrl+LeftClick navigates to the target detail, Ctrl+RMB opens the Peek panel.
    /// Extracted so the decision logic is unit-testable without pointer input simulation.
    /// </summary>
    internal static (bool Navigate, bool Peek) ResolveClickAction(
        KeyModifiers modifiers, bool isRightButtonPressed)
    {
        if ((modifiers & KeyModifiers.Control) == 0) return (false, false);
        return isRightButtonPressed ? (false, true) : (true, false);
    }

    private static EntityRef? GetBaseEntityRef(IReferenceEntry entry)
    {
        var current = entry;
        if (current is NegatedRefFormat neg)
            current = neg.Inner;

        return current switch
        {
            PureRefFormat p => p.Entity,
            IdXMultFormat i => i.Entity,
            IdXMultXQtyFormat q => q.Entity,
            MultXIdFormat m => m.Entity,
            AssignFormat a => a.Entity,
            BracketFormat b => b.Entity,
            OrGroupFormat o => o.Alternatives.FirstOrDefault() is { } first ? GetBaseEntityRef(first) : null,
            _ => null,
        };
    }

    // ── Event handlers ───────────────────────────────────────────────────

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (_fieldRow?.Property is null) return;
        if (_refAttr is null)
            _refAttr = _fieldRow.Property.GetCustomAttribute<ReferenceFieldAttribute>();
        if (_refAttr is null) return;

        // Find the owner window
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var result = await ReferencePickerDialog.ShowAsync(
            owner,
            _refAttr.TargetEntityType,
            _refAttr.SecondaryTargetEntityType,
            _refAttr.Separator,
            _refAttr.Pattern,
            _refAttr.TargetKey,
            _fieldRow.CurrentValue);

        if (result is not null)
        {
            _fieldRow.CurrentValue = result.RawText;
        }
    }

    private void OnPeekClick(object? sender, RoutedEventArgs e)
    {
        if (_fieldRow?.Property is null) return;
        if (_refAttr is null)
            _refAttr = _fieldRow.Property.GetCustomAttribute<ReferenceFieldAttribute>();
        if (_refAttr is null) return;

        // Resolve the first reference entry to its target entity — the same path the badges
        // use — then peek. Decoupled from the (possibly null) source-entity visual-tree walk.
        try
        {
            var serializer = GetService<IReferenceListSerializer>();
            var lookup = GetService<IEntityLookupService>();
            var refList = serializer.Deserialize(_fieldRow.CurrentValue, _refAttr);
            if (refList.Count == 0) return;

            var baseRef = GetBaseEntityRef(refList[0]);
            if (baseRef is null) return;

            // Keep the namespace prefix (and composite key) — see CreateBadge.
            var lookupKey = baseRef.ToRawString();

            // R30: primary type first, then secondary (aTreasures → nested TreasureTable).
            var target = lookup.FindBestMatch(_refAttr.TargetEntityType, lookupKey, _refAttr.TargetKey);
            var targetType = _refAttr.TargetEntityType;
            if (target is null && _refAttr.SecondaryTargetEntityType is not null)
            {
                target = lookup.FindBestMatch(_refAttr.SecondaryTargetEntityType, lookupKey,
                    _refAttr.SecondaryTargetKey);
                if (target is not null) targetType = _refAttr.SecondaryTargetEntityType;
            }

            if (target is not null)
            {
                WeakReferenceMessenger.Default.Send(
                    new PeekEntityMessage(targetType, target.EntityId, target));
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[RefFieldEditor] Peek failed for '{Val}'", _fieldRow.CurrentValue);
        }
    }
}