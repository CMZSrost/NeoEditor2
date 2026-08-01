using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Layout;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// A simple filter flyout for ProDataGrid columns.
/// Provides text-based filtering (Contains operator) with Apply/Clear buttons.
/// Assigned per-column in SearchableDataGrid.OnAutoGeneratingColumn.
/// </summary>
public class ColumnFilterFlyout : Flyout
{
    private readonly IFilteringModel _model;
    private readonly string _columnKey;
    private readonly string _propertyPath;
    private TextBox? _textBox;
    private TextBlock? _statusText;

    public ColumnFilterFlyout(IFilteringModel model, string columnKey, string propertyPath)
    {
        _model = model;
        _columnKey = columnKey;
        _propertyPath = propertyPath;

        Placement = PlacementMode.BottomEdgeAlignedLeft;
        ShowMode = FlyoutShowMode.Transient;

        Content = BuildContent();
    }

    private Control BuildContent()
    {
        _textBox = new TextBox
        {
            Width = 180,
            MinWidth = 140,
            FontSize = 12,
            Padding = new Thickness(4),
            Watermark = "Filter value (Contains)..."
        };

        _statusText = new TextBlock
        {
            FontSize = 11,
            Margin = new Thickness(4, 2, 4, 0),
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap
        };

        var applyButton = new Button
        {
            Content = "Apply",
            FontSize = 11,
            Padding = new Thickness(8, 3),
            Margin = new Thickness(0, 2, 0, 0)
        };

        var clearButton = new Button
        {
            Content = "Clear",
            FontSize = 11,
            Padding = new Thickness(8, 3),
            Margin = new Thickness(4, 2, 0, 0)
        };

        applyButton.Click += OnApplyClick;
        clearButton.Click += OnClearClick;
        _textBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
                OnApplyClick(this, EventArgs.Empty);
            else if (e.Key == Avalonia.Input.Key.Escape)
                Hide();
        };

        RefreshStatus();

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(applyButton);
        buttonPanel.Children.Add(clearButton);

        var panel = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(6, 4)
        };
        panel.Children.Add(_statusText);
        panel.Children.Add(_textBox);
        panel.Children.Add(buttonPanel);

        return panel;
    }

    protected override void OnOpened()
    {
        base.OnOpened();
        RefreshStatus();
        _textBox?.Focus();
        _textBox?.SelectAll();
    }

    private void RefreshStatus()
    {
        if (_statusText is null) return;

        var existing = FindExistingDescriptor();
        if (existing is not null)
        {
            _statusText.Text = $"Current: \"{existing.Value}\"";
            if (_textBox is not null && string.IsNullOrEmpty(_textBox.Text))
                _textBox.Text = existing.Value?.ToString() ?? "";
        }
        else
        {
            _statusText.Text = "No active filter";
        }
    }

    private FilteringDescriptor? FindExistingDescriptor()
    {
        foreach (var d in _model.Descriptors)
        {
            if (Equals(d.ColumnId, _columnKey))
                return d;
        }

        return null;
    }

    private void OnApplyClick(object? sender, EventArgs e)
    {
        var text = _textBox?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            OnClearClick(sender, e);
            return;
        }

        var desc = new FilteringDescriptor(
            _columnKey,
            FilteringOperator.Contains,
            _propertyPath,
            text,
            stringComparison: StringComparison.OrdinalIgnoreCase);

        _model.SetOrUpdate(desc);
        Hide();
    }

    private void OnClearClick(object? sender, EventArgs e)
    {
        _model.Remove(_columnKey);
        if (_textBox is not null)
            _textBox.Text = "";
        Hide();
    }
}