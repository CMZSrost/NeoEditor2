using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Microsoft.Extensions.Logging;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// F4.2 — Factory that creates a type-aware filter flyout for a DataGrid column.
///
/// Uses ProDataGrid's built-in filter editor DataTemplates
/// (DataGridFilterTextEditorTemplate, DataGridFilterNumberEditorTemplate,
/// DataGridFilterEnumEditorTemplate) with our FilterContext implementations
/// (TextFilterContext, NumberFilterContext, EnumFilterContext).
///
/// Falls back to ColumnFilterFlyout when built-in templates are unavailable
/// (e.g. test environment where Application.Current is null).
/// </summary>
public static class FilterFlyoutFactory
{
    public static Flyout Create(
        Type propertyType,
        string columnKey,
        string propertyPath,
        IFilteringModel model,
        ILogger? logger = null)
    {
        // Resolve built-in templates from ProDataGrid Generic.xaml.
        // These are null in test / headless environments (Application.Current == null).
        var theme = TryFindResource<Avalonia.Styling.ControlTheme>("DataGridFilterFlyoutPresenterTheme");
        var textTemplate = TryFindResource<Avalonia.Controls.Templates.IDataTemplate>(
            "DataGridFilterTextEditorTemplate");
        var numberTemplate = TryFindResource<Avalonia.Controls.Templates.IDataTemplate>(
            "DataGridFilterNumberEditorTemplate");
        var enumTemplate = TryFindResource<Avalonia.Controls.Templates.IDataTemplate>(
            "DataGridFilterEnumEditorTemplate");

        // Fall back to ColumnFilterFlyout when resources are unavailable
        if (theme is null || textTemplate is null || numberTemplate is null || enumTemplate is null)
        {
            logger?.LogWarning(
                "[FilterFlyout] ProDataGrid filter templates not found, falling back to ColumnFilterFlyout");
            return new ColumnFilterFlyout(model, columnKey, propertyPath);
        }

        object context;
        Avalonia.Controls.Templates.IDataTemplate contentTemplate;

        if (IsNumeric(propertyType))
        {
            context = new NumberFilterContext(
                $"Filter {columnKey}",
                apply: (min, max) => ApplyNumberFilter(model, columnKey, propertyPath, min, max),
                clear: () => model.Remove(columnKey));
            contentTemplate = numberTemplate;
        }
        else if (propertyType == typeof(bool))
        {
            context = new EnumFilterContext(
                $"Filter {columnKey}",
                allOptions: ["True", "False"],
                selected: null,
                apply: selected => ApplyEnumFilter(model, columnKey, propertyPath, selected),
                clear: () => model.Remove(columnKey));
            contentTemplate = enumTemplate;
        }
        else if (propertyType.IsEnum)
        {
            context = new EnumFilterContext(
                $"Filter {columnKey}",
                allOptions: Enum.GetNames(propertyType),
                selected: null,
                apply: selected => ApplyEnumFilter(model, columnKey, propertyPath, selected),
                clear: () => model.Remove(columnKey));
            contentTemplate = enumTemplate;
        }
        else
        {
            context = new TextFilterContext(
                $"Filter {columnKey}",
                apply: text => ApplyTextFilter(model, columnKey, propertyPath, text),
                clear: () => model.Remove(columnKey));
            contentTemplate = textTemplate;
        }

        return new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            ShowMode = FlyoutShowMode.Transient,
            Content = context,
            ContentTemplate = contentTemplate,
            FlyoutPresenterTheme = theme
        };
    }

    public static bool IsNumeric(Type t)
    {
        if (t == typeof(int) || t == typeof(long) || t == typeof(float)
            || t == typeof(double) || t == typeof(decimal)
            || t == typeof(short) || t == typeof(ushort) || t == typeof(uint)
            || t == typeof(ulong) || t == typeof(byte) || t == typeof(sbyte))
            return true;

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
            return IsNumeric(Nullable.GetUnderlyingType(t)!);

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static void ApplyTextFilter(
        IFilteringModel model, string columnKey, string propertyPath, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            model.Remove(columnKey);
            return;
        }

        model.SetOrUpdate(new FilteringDescriptor(
            columnKey, FilteringOperator.Contains, propertyPath, text,
            stringComparison: StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyNumberFilter(
        IFilteringModel model, string columnKey, string propertyPath,
        double? min, double? max)
    {
        if (min == null && max == null)
        {
            model.Remove(columnKey);
            return;
        }

        model.SetOrUpdate(new FilteringDescriptor(
            columnKey, FilteringOperator.Between, propertyPath,
            values: [min ?? double.MinValue, max ?? double.MaxValue]));
    }

    private static void ApplyEnumFilter(
        IFilteringModel model, string columnKey, string propertyPath,
        System.Collections.Generic.IReadOnlyList<string> selected)
    {
        if (selected.Count == 0)
        {
            model.Remove(columnKey);
            return;
        }

        model.SetOrUpdate(new FilteringDescriptor(
            columnKey, FilteringOperator.In, propertyPath,
            values: selected.Cast<object>().ToArray()));
    }

    private static T? TryFindResource<T>(string key) where T : class
    {
        try
        {
            return Application.Current?.FindResource(key) as T;
        }
        catch
        {
            return null;
        }
    }
}