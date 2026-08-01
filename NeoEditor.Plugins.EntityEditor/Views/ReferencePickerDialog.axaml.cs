using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;

namespace NeoEditor.Plugins.EntityEditor.Views;

/// <summary>
/// Modal dialog for picking target entities for reference fields.
/// Supports single-value and multi-value selection with decoration editing
/// (multiplier, negation, value assignment).
/// Follows Pattern A: Window + static ShowAsync factory.
/// </summary>
public partial class ReferencePickerDialog : Window
{
    private ReferencePickerViewModel? _vm;

    private static T GetService<T>() where T : notnull
        => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

    public ReferencePickerDialog()
    {
        InitializeComponent();
    }

    public ReferencePickerDialog(
        Type targetEntityType,
        Type? secondaryTargetEntityType,
        string? separator,
        string? pattern,
        string? targetKey,
        string currentRawValue) : this()
    {
        var lookup = GetService<IEntityLookupService>();
        var serializer = GetService<IReferenceListSerializer>();

        _vm = new ReferencePickerViewModel(
            targetEntityType, secondaryTargetEntityType,
            separator, pattern, targetKey,
            currentRawValue, lookup, serializer);

        DataContext = _vm;

        // Wire up visibility for single/multi controls after DataContext is set
        UpdateUIMode();
    }

    private void UpdateUIMode()
    {
        if (_vm is null) return;

        // Multi-value: show center action buttons, hide single-select button
        var isMulti = _vm.IsMultiValue;
        MultiValueActions.IsVisible = isMulti;
        SingleSelectButton.IsVisible = !isMulti;
    }

    // ── Button handlers ──────────────────────────────────────────────────

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        _vm?.AddSelectedEntityCommand.Execute(null);
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        // Remove the first selected entry (simplified — user can use per-entry ✕)
        if (_vm?.SelectedEntries.Count > 0)
            _vm.RemoveEntryCommand.Execute(_vm.SelectedEntries[^1]);
    }

    private void OnRemoveEntryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ReferenceEntryViewModel entry)
            _vm?.RemoveEntryCommand.Execute(entry);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        _vm?.ConfirmCommand.Execute(null);
        if (_vm?.ResultRawText is not null)
            Close(true);
        else
            Close(false);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _vm?.CancelCommand.Execute(null);
        Close(false);
    }

    // ── Static factory ───────────────────────────────────────────────────

    /// <summary>
    /// Open the reference picker dialog and return the result.
    /// </summary>
    /// <param name="owner">Parent window.</param>
    /// <param name="targetEntityType">Type of entities to pick from.</param>
    /// <param name="secondaryTargetEntityType">Optional fallback target type for mixed-ref fields.</param>
    /// <param name="separator">Separator for multi-value fields (null = single-value).</param>
    /// <param name="pattern">Parse pattern string from ReferenceFieldAttribute.</param>
    /// <param name="targetKey">Target key format (e.g. "{GroupId}.{SubgroupId}").</param>
    /// <param name="currentRawValue">Current raw field value to pre-populate.</param>
    public static async Task<ReferencePickerResult?> ShowAsync(
        Window owner,
        Type targetEntityType,
        Type? secondaryTargetEntityType,
        string? separator,
        string? pattern,
        string? targetKey,
        string currentRawValue)
    {
        var dialog = new ReferencePickerDialog(
            targetEntityType, secondaryTargetEntityType,
            separator, pattern, targetKey, currentRawValue);

        var confirmed = await dialog.ShowDialog<bool?>(owner);
        if (confirmed == true && dialog._vm is not null)
        {
            return new ReferencePickerResult(
                dialog._vm.ResultRawText ?? currentRawValue,
                dialog._vm.ResultReferenceList ?? new ReferenceList<IReferenceEntry>());
        }
        return null;
    }
}
