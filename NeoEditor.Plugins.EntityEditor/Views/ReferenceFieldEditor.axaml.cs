using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
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
    /// </summary>
    private Border CreateBadge(IReferenceEntry entry, IEntityLookupService lookup)
    {
        // Drill to base EntityRef
        var baseRef = GetBaseEntityRef(entry);
        var rawId = entry.ToRawString();
        var displayName = rawId;

        if (baseRef is not null && _refAttr is not null)
        {
            var lookupKey = baseRef.IsComposite
                ? $"{baseRef.GroupId}.{baseRef.SubgroupId}"
                : baseRef.Id;

            var resolved = lookup.FindBestMatch(
                _refAttr.TargetEntityType, lookupKey, _refAttr.TargetKey);
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

        return badge;
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
            MultXIdFormat m => m.Entity,
            AssignFormat a => a.Entity,
            BracketFormat b => b.Entity,
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

            var lookupKey = baseRef.IsComposite
                ? $"{baseRef.GroupId}.{baseRef.SubgroupId}"
                : baseRef.Id;

            var target = lookup.FindBestMatch(_refAttr.TargetEntityType, lookupKey, _refAttr.TargetKey);
            if (target is not null)
            {
                WeakReferenceMessenger.Default.Send(
                    new PeekEntityMessage(_refAttr.TargetEntityType, target.EntityId, target));
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[RefFieldEditor] Peek failed for '{Val}'", _fieldRow.CurrentValue);
        }
    }
}
