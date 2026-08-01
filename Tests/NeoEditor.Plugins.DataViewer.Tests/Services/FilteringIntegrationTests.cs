using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.DataGridFiltering;
using NeoEditor.Plugins.DataViewer.Services;
using Xunit;

namespace NeoEditor.Plugins.DataViewer.Tests.Services;

/// <summary>
/// Tests for the ProDataGrid column filtering integration.
/// Verifies FilteringModel API behavior and SearchableDataGrid wiring.
/// </summary>
public class FilteringIntegrationTests
{
    // ═══════════════════════════════════════════════════════════════════════
    //  FilteringModel basics
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void FilteringModel_DefaultsToEmptyDescriptors()
    {
        var model = new FilteringModel();
        Assert.Empty(model.Descriptors);
    }

    [Fact]
    public void FilteringModel_OwnsViewFilter_DefaultsToTrue()
    {
        var model = new FilteringModel();
        Assert.True(model.OwnsViewFilter);
    }

    [Fact]
    public void SetOrUpdate_AddsDescriptor()
    {
        var model = new FilteringModel();
        var desc = new FilteringDescriptor("Name", FilteringOperator.Contains, "Name", "sword");

        model.SetOrUpdate(desc);

        Assert.Single(model.Descriptors);
        Assert.Equal("Name", model.Descriptors[0].ColumnId);
        Assert.Equal(FilteringOperator.Contains, model.Descriptors[0].Operator);
    }

    [Fact]
    public void SetOrUpdate_ReplacesExistingDescriptor()
    {
        var model = new FilteringModel();
        var desc1 = new FilteringDescriptor("Name", FilteringOperator.Contains, "Name", "sword");
        var desc2 = new FilteringDescriptor("Name", FilteringOperator.StartsWith, "Name", "axe");

        model.SetOrUpdate(desc1);
        model.SetOrUpdate(desc2);

        Assert.Single(model.Descriptors);
        Assert.Equal(FilteringOperator.StartsWith, model.Descriptors[0].Operator);
        Assert.Equal("axe", model.Descriptors[0].Value);
    }

    [Fact]
    public void Clear_RemovesAllDescriptors()
    {
        var model = new FilteringModel();
        model.SetOrUpdate(new FilteringDescriptor("A", FilteringOperator.Contains, "A", "x"));
        model.SetOrUpdate(new FilteringDescriptor("B", FilteringOperator.Equals, "B", "y"));

        Assert.Equal(2, model.Descriptors.Count);

        model.Clear();

        Assert.Empty(model.Descriptors);
    }

    [Fact]
    public void Remove_RemovesSpecificDescriptor()
    {
        var model = new FilteringModel();
        model.SetOrUpdate(new FilteringDescriptor("A", FilteringOperator.Contains, "A", "x"));
        model.SetOrUpdate(new FilteringDescriptor("B", FilteringOperator.Equals, "B", "y"));

        var removed = model.Remove("A");

        Assert.True(removed);
        Assert.Single(model.Descriptors);
        Assert.Equal("B", model.Descriptors[0].ColumnId);
    }

    [Fact]
    public void Remove_ReturnsFalseForMissingColumn()
    {
        var model = new FilteringModel();
        var removed = model.Remove("Nonexistent");

        Assert.False(removed);
    }

    [Fact]
    public void Apply_ReplacesAllDescriptors()
    {
        var model = new FilteringModel();
        model.SetOrUpdate(new FilteringDescriptor("Old", FilteringOperator.Contains, "Old", "old"));

        var newDescs = new[]
        {
            new FilteringDescriptor("A", FilteringOperator.Equals, "A", "a"),
            new FilteringDescriptor("B", FilteringOperator.GreaterThan, "B", 10),
        };
        model.Apply(newDescs);

        Assert.Equal(2, model.Descriptors.Count);
        Assert.Contains(model.Descriptors, d => d.ColumnId.Equals("A"));
        Assert.Contains(model.Descriptors, d => d.ColumnId.Equals("B"));
    }

    [Fact]
    public void FilteringChanged_EventFires_OnSetOrUpdate()
    {
        var model = new FilteringModel();
        FilteringChangedEventArgs? received = null;
        model.FilteringChanged += (_, args) => received = args;

        model.SetOrUpdate(new FilteringDescriptor("X", FilteringOperator.Contains, "X", "test"));

        Assert.NotNull(received);
        Assert.Single(received!.NewDescriptors);
    }

    [Fact]
    public void FilteringChanged_EventFires_OnClear()
    {
        var model = new FilteringModel();
        model.SetOrUpdate(new FilteringDescriptor("X", FilteringOperator.Contains, "X", "test"));

        FilteringChangedEventArgs? received = null;
        model.FilteringChanged += (_, args) => received = args;

        model.Clear();

        Assert.NotNull(received);
        Assert.Empty(received!.NewDescriptors);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FilteringOperator enum
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void FilteringOperator_HasExpectedValues()
    {
        var values = Enum.GetValues<FilteringOperator>();
        Assert.Contains(FilteringOperator.Equals, values);
        Assert.Contains(FilteringOperator.NotEquals, values);
        Assert.Contains(FilteringOperator.Contains, values);
        Assert.Contains(FilteringOperator.StartsWith, values);
        Assert.Contains(FilteringOperator.EndsWith, values);
        Assert.Contains(FilteringOperator.GreaterThan, values);
        Assert.Contains(FilteringOperator.GreaterThanOrEqual, values);
        Assert.Contains(FilteringOperator.LessThan, values);
        Assert.Contains(FilteringOperator.LessThanOrEqual, values);
        Assert.Contains(FilteringOperator.Between, values);
        Assert.Contains(FilteringOperator.In, values);
        Assert.Contains(FilteringOperator.Custom, values);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FilteringDescriptor construction
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void FilteringDescriptor_PreservesGivenValues()
    {
        var desc = new FilteringDescriptor("Score", FilteringOperator.GreaterThanOrEqual,
            "Score", 5);

        Assert.Equal("Score", desc.ColumnId);
        Assert.Equal(FilteringOperator.GreaterThanOrEqual, desc.Operator);
        Assert.Equal("Score", desc.PropertyPath);
        Assert.Equal(5, desc.Value);
    }

    [Fact]
    public void FilteringDescriptor_WithStringComparison()
    {
        var desc = new FilteringDescriptor(
            "Name", FilteringOperator.Contains, "Name", "sword",
            stringComparison: StringComparison.OrdinalIgnoreCase);

        Assert.Equal(StringComparison.OrdinalIgnoreCase, desc.StringComparisonMode);
    }

    [Fact]
    public void FilteringDescriptor_Equality_UsesAllFields()
    {
        // Equals considers ColumnId + Operator + Value + StringComparison
        var d1 = new FilteringDescriptor("A", FilteringOperator.Equals, "A", "x");
        var d2 = new FilteringDescriptor("A", FilteringOperator.Equals, "A", "x");
        var d3 = new FilteringDescriptor("A", FilteringOperator.Equals, "A", "y");
        var d4 = new FilteringDescriptor("A", FilteringOperator.Contains, "A", "x");
        var d5 = new FilteringDescriptor("B", FilteringOperator.Equals, "B", "x");

        Assert.True(d1.Equals(d2));  // identical → equal
        Assert.False(d1.Equals(d3)); // different value → not equal
        Assert.False(d1.Equals(d4)); // different op → not equal
        Assert.False(d1.Equals(d5)); // different column → not equal
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SearchableDataGrid integration (requires no UI for FilteringModel)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ClearFilter_ClearsAllDescriptors()
    {
        var model = new FilteringModel { OwnsViewFilter = true };
        model.SetOrUpdate(new FilteringDescriptor("Col", FilteringOperator.Contains, "Col", "val"));

        model.Clear();

        Assert.Empty(model.Descriptors);
    }

    [Fact]
    public void HasActiveFilter_True_WhenDescriptorsExist()
    {
        var model = new FilteringModel { OwnsViewFilter = true };
        model.SetOrUpdate(new FilteringDescriptor("Col", FilteringOperator.Contains, "Col", "test"));

        bool hasActive = model.Descriptors.Count > 0;
        Assert.True(hasActive);
    }

    [Fact]
    public void HasActiveFilter_False_AfterClear()
    {
        var model = new FilteringModel { OwnsViewFilter = true };
        model.SetOrUpdate(new FilteringDescriptor("Col", FilteringOperator.Contains, "Col", "test"));
        model.Clear();

        bool hasActive = model.Descriptors.Count > 0;
        Assert.False(hasActive);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DataGridAccessorFilteringAdapterFactory
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AdapterFactory_CreatesAdapter()
    {
        var factory = new DataGridAccessorFilteringAdapterFactory();
        Assert.NotNull(factory);
        // Can't test Create() without a real DataGrid, but factory should exist.
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FilterContext implementations (F4.1)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TextFilterContext_Apply_CallsCallbackWithText()
    {
        string? received = null;
        var ctx = new TextFilterContext("Test", apply: text => received = text, clear: () => { });
        ctx.Text = "hello";
        ctx.ApplyCommand.Execute(null);
        Assert.Equal("hello", received);
    }

    [Fact]
    public void TextFilterContext_Clear_CallsCallback()
    {
        var cleared = false;
        var ctx = new TextFilterContext("Test", apply: _ => { }, clear: () => cleared = true);
        ctx.ClearCommand.Execute(null);
        Assert.True(cleared);
    }

    [Fact]
    public void TextFilterContext_DefaultsToEmpty()
    {
        var ctx = new TextFilterContext("Test", apply: _ => { }, clear: () => { });
        Assert.Equal("", ctx.Text);
        Assert.Equal("Test", ctx.Label);
    }

    [Fact]
    public void NumberFilterContext_Apply_CallsCallbackWithValues()
    {
        double? receivedMin = null;
        double? receivedMax = null;
        var ctx = new NumberFilterContext("Num", apply: (min, max) => { receivedMin = min; receivedMax = max; }, clear: () => { });
        ctx.MinValue = 5;
        ctx.MaxValue = 100;
        ctx.ApplyCommand.Execute(null);
        Assert.Equal(5, receivedMin);
        Assert.Equal(100, receivedMax);
    }

    [Fact]
    public void NumberFilterContext_Clear_CallsCallback()
    {
        var cleared = false;
        var ctx = new NumberFilterContext("Num", apply: (_, _) => { }, clear: () => cleared = true);
        ctx.ClearCommand.Execute(null);
        Assert.True(cleared);
    }

    [Fact]
    public void NumberFilterContext_DefaultsToNull()
    {
        var ctx = new NumberFilterContext("Num", apply: (_, _) => { }, clear: () => { });
        Assert.Null(ctx.MinValue);
        Assert.Null(ctx.MaxValue);
        Assert.Equal("Num", ctx.Label);
    }

    [Fact]
    public void EnumFilterContext_Apply_ReturnsSelectedOnly()
    {
        IReadOnlyList<string>? received = null;
        var ctx = new EnumFilterContext("Enum",
            allOptions: ["A", "B", "C"],
            selected: null,
            apply: sel => received = sel,
            clear: () => { });
        // Select A and C
        ctx.Options[0].IsSelected = true;
        ctx.Options[2].IsSelected = true;
        ctx.ApplyCommand.Execute(null);
        Assert.NotNull(received);
        Assert.Equal(2, received!.Count);
        Assert.Contains("A", received);
        Assert.Contains("C", received);
        Assert.DoesNotContain("B", received);
    }

    [Fact]
    public void EnumFilterContext_Clear_CallsCallback()
    {
        var cleared = false;
        var ctx = new EnumFilterContext("Enum",
            allOptions: ["A", "B"],
            selected: null,
            apply: _ => { },
            clear: () => cleared = true);
        ctx.ClearCommand.Execute(null);
        Assert.True(cleared);
    }

    [Fact]
    public void EnumFilterContext_PreselectsOptions()
    {
        var ctx = new EnumFilterContext("Enum",
            allOptions: ["A", "B", "C"],
            selected: ["A", "C"],
            apply: _ => { },
            clear: () => { });
        Assert.True(ctx.Options[0].IsSelected);  // A
        Assert.False(ctx.Options[1].IsSelected); // B
        Assert.True(ctx.Options[2].IsSelected);  // C
    }

    [Fact]
    public void EnumFilterContext_NoSelection_ReturnsEmpty()
    {
        IReadOnlyList<string>? received = null;
        var ctx = new EnumFilterContext("Enum",
            allOptions: ["A", "B"],
            selected: null,
            apply: sel => received = sel,
            clear: () => { });
        // Nothing selected — apply returns empty list
        ctx.ApplyCommand.Execute(null);
        Assert.NotNull(received);
        Assert.Empty(received!);
    }

    [Fact]
    public void EnumOption_TogglesIsSelected()
    {
        var opt = new EnumOption("Test");
        Assert.False(opt.IsSelected);
        Assert.Equal("Test", opt.Display);

        opt.IsSelected = true;
        Assert.True(opt.IsSelected);

        opt.IsSelected = false;
        Assert.False(opt.IsSelected);
    }

    [Fact]
    public void EnumOption_RaisesPropertyChanged()
    {
        var opt = new EnumOption("X");
        string? changedProp = null;
        opt.PropertyChanged += (_, args) => changedProp = args.PropertyName;
        opt.IsSelected = true;
        Assert.Equal(nameof(EnumOption.IsSelected), changedProp);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FilterFlyoutFactory (F4.2)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Factory_CreatesFlyout_ForStringType()
    {
        var model = new FilteringModel();
        var flyout = FilterFlyoutFactory.Create(typeof(string), "col", "Col", model);
        Assert.NotNull(flyout);
        Assert.NotNull(flyout.Content);
    }

    [Fact]
    public void Factory_CreatesFlyout_ForIntType()
    {
        var model = new FilteringModel();
        var flyout = FilterFlyoutFactory.Create(typeof(int), "col", "Col", model);
        Assert.NotNull(flyout);
        Assert.NotNull(flyout.Content);
    }

    [Fact]
    public void Factory_CreatesFlyout_ForBoolType()
    {
        var model = new FilteringModel();
        var flyout = FilterFlyoutFactory.Create(typeof(bool), "col", "Col", model);
        Assert.NotNull(flyout);
        Assert.NotNull(flyout.Content);
    }

    [Fact]
    public void Factory_CreatesFlyout_ForEnumType()
    {
        var model = new FilteringModel();
        var flyout = FilterFlyoutFactory.Create(typeof(StringComparison), "col", "Col", model);
        Assert.NotNull(flyout);
        Assert.NotNull(flyout.Content);
    }

    [Fact]
    public void Factory_CreatesFlyout_ForNullableInt()
    {
        var model = new FilteringModel();
        var flyout = FilterFlyoutFactory.Create(typeof(int?), "col", "Col", model);
        Assert.NotNull(flyout);
        Assert.NotNull(flyout.Content);
    }

    [Theory]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(long), true)]
    [InlineData(typeof(float), true)]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(decimal), true)]
    [InlineData(typeof(short), true)]
    [InlineData(typeof(byte), true)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(double?), true)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(bool), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(object), false)]
    public void Factory_IsNumeric_DetectsCorrectly(Type type, bool expected)
    {
        Assert.Equal(expected, FilterFlyoutFactory.IsNumeric(type));
    }
}
