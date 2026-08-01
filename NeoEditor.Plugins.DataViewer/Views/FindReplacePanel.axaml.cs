using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NeoEditor.Data.Command;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.DataViewer.Views;

public partial class FindReplacePanel : UserControl
{
    public bool ReplaceMode { get; private set; }
    public bool IsOpen => base.IsVisible;

    /// <summary>
    /// Injectable localization service. Falls back to Application.Current DI if not set.
    /// </summary>
    public ILocalizationService? InjectedLoc { get; set; }

    /// <summary>
    /// Injectable notification service. Falls back to Application.Current DI if not set.
    /// </summary>
    public INotificationService? InjectedNotification { get; set; }

    public ILocalizationService Loc => InjectedLoc
        ?? (Application.Current?.Resources["Services"] as IServiceProvider)
            ?.GetService(typeof(ILocalizationService)) as ILocalizationService
        ?? throw new InvalidOperationException("ILocalizationService not available");

    private INotificationService Notification => InjectedNotification
        ?? (Application.Current?.Resources["Services"] as IServiceProvider)
            ?.GetService(typeof(INotificationService)) as INotificationService
        ?? throw new InvalidOperationException("INotificationService not available");

    private DataGrid? _targetGrid;
    private List<MatchInfo> _matches = [];
    private int _currentMatch = -1;
    private int _lastSearchVersion;
    public CommandHistory? CommandHistory { get; set; }
    public Action? OnDirtyChanged { get; set; }

    public FindReplacePanel()
    {
        DataContext = this;
        InitializeComponent();
        SearchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                PerformSearch();
        };
    }

    private bool _resizing;
    private double _resizeStartX;
    private double _resizeStartWidth;

    public void Show(DataGrid targetGrid, bool replaceMode)
    {
        _targetGrid = targetGrid;
        ReplaceMode = replaceMode;
        base.IsVisible = true;
        ClearHighlights();
        ReplaceRow.IsVisible = replaceMode;
        BottomRow.IsVisible = replaceMode;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void Hide()
    {
        base.IsVisible = false;
        ClearCellHighlight();
        ClearHighlights();
        _targetGrid = null;
        _matches.Clear();
        _currentMatch = -1;
        MatchCount.Text = "";
    }

    private void PerformSearch()
    {
        ClearHighlights();
        _matches.Clear();
        _currentMatch = -1;
        _lastSearchVersion++;

        var searchText = SearchBox.Text ?? "";
        if (string.IsNullOrEmpty(searchText) || _targetGrid is null)
        {
            MatchCount.Text = "";
            return;
        }

        var caseSensitive = CaseBtn.IsChecked == true;
        var wholeWord = WordBtn.IsChecked == true;
        var useRegex = RegexBtn.IsChecked == true;

        Regex? regex;
        try
        {
            var pattern = useRegex ? searchText : Regex.Escape(searchText);
            if (wholeWord) pattern = $@"\b{pattern}\b";
            regex = new Regex(pattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        }
        catch
        {
            MatchCount.Text = Loc["FindInvalid"];
            return;
        }

        var items = _targetGrid.ItemsSource as IEnumerable;
        if (items is null) return;

        var idx = 0;
        var capturedVersion = _lastSearchVersion;
        foreach (var item in items)
        {
            if (item is not IEntity entity) { idx++; continue; }

            var type = entity.GetType();
            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.DeclaringType != typeof(IEntity)
                    && p.GetCustomAttribute<ColumnAttribute>() != null
                    && p.PropertyType == typeof(string));

            foreach (var prop in props)
            {
                var text = prop.GetValue(entity)?.ToString() ?? "";
                if (string.IsNullOrEmpty(text)) continue;

                var matches = regex.Matches(text);
                if (matches.Count > 0)
                {
                    _matches.Add(new MatchInfo(idx, entity, prop, prop.Name, text, matches));
                    // NO break — find ALL matching columns per row
                }
            }
            idx++;
            if (capturedVersion != _lastSearchVersion) return;
        }

        MatchCount.Text = _matches.Count > 0 ? string.Format(Loc["FindMatchCount"], _matches.Count) : Loc["FindNoMatches"];
        if (_matches.Count > 0)
            NavigateTo(0);
    }

    private DataGridCell? _highlightedCell;

    private void NavigateTo(int index)
    {
        if (_matches.Count == 0 || _targetGrid is null) return;
        _currentMatch = (index + _matches.Count) % _matches.Count;

        var match = _matches[_currentMatch];
        var col = _targetGrid.Columns.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.SortMemberPath) && c.SortMemberPath == match.ColumnName);

        ClearCellHighlight();
        _targetGrid.ScrollIntoView(match.Entity, col);
        _targetGrid.SelectedItem = match.Entity;
        _targetGrid.SelectedIndex = match.RowIndex;

        // Highlight the matched cell after layout
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_targetGrid is null) return;
            try
            {
                var row = _targetGrid.GetVisualDescendants()
                    .OfType<DataGridRow>().FirstOrDefault(r => r.DataContext == match.Entity);
                if (row is null) return;
                var colIdx = col is not null ? _targetGrid.Columns.IndexOf(col) : -1;
                var cells = row.GetVisualDescendants().OfType<DataGridCell>().ToList();
                var cell = colIdx >= 0 && colIdx < cells.Count ? cells[colIdx] : null;
                if (cell is not null)
                {
                    cell.BorderBrush = Application.Current?.TryFindResource("SystemControlHighlightAccentBrush", out var brush) == true
                    ? (IBrush)brush
                    : Brushes.OrangeRed;
                    cell.BorderThickness = new Avalonia.Thickness(2);
                    _highlightedCell = cell;
                }
            }
            catch { /* visual tree may not be ready */ }
        }, Avalonia.Threading.DispatcherPriority.Background);

        MatchCount.Text = $"{_currentMatch + 1}/{_matches.Count}";
    }

    private void ClearCellHighlight()
    {
        if (_highlightedCell is not null)
        {
            try
            {
                _highlightedCell.BorderBrush = null;
                _highlightedCell.BorderThickness = default;
            }
            catch (Exception ex) { Serilog.Log.Logger.Verbose(ex, "[FindReplace] Failed to clear cell highlight"); }
            _highlightedCell = null;
        }
    }

    private void OnResizeStart(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _resizing = true;
        _resizeStartX = e.GetPosition(null).X; // screen-relative
        _resizeStartWidth = ContentStack.Width;
        e.Pointer.Capture(ResizeGrip);
        e.Handled = true;
    }

    private void OnResizeMove(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!_resizing) return;
        var screenX = e.GetPosition(null).X;
        var delta = _resizeStartX - screenX;
        var newWidth = Math.Max(200, _resizeStartWidth + delta);
        ContentStack.Width = newWidth;
    }

    private void OnResizeEnd(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        _resizing = false;
        e.Pointer.Capture(null);
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Hide();

    private void OnPrevClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NavigateTo(_currentMatch - 1);
    }

    private void OnNextClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NavigateTo(_currentMatch + 1);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
                    NavigateTo(_currentMatch - 1);
                else
                    NavigateTo(_currentMatch + 1);
                e.Handled = true;
                break;
            case Key.Escape:
                Hide();
                e.Handled = true;
                break;
        }
    }

    private void OnReplaceClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentMatch < 0 || _currentMatch >= _matches.Count) return;
        if (CommandHistory is null || OnDirtyChanged is null) return;

        var match = _matches[_currentMatch];
        var oldValue = match.Text;
        var newValue = ComputeReplaceText(match);
        if (newValue is null || newValue == oldValue) return;

        var colAttr = match.Property.GetCustomAttribute<ColumnAttribute>();
        var displayColName = colAttr?.Name ?? match.ColumnName;
        var cmd = new EditCellCommand(match.Entity, match.Property, displayColName, oldValue, newValue, OnDirtyChanged);
        CommandHistory.Execute(cmd);
        RefreshGrid();
        PerformSearch();
        if (_matches.Count > 0 && _currentMatch >= _matches.Count)
            NavigateTo(_matches.Count - 1);
    }

    private void OnReplaceAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CommandHistory is null || OnDirtyChanged is null) return;

        var edits = new List<EditRecord>();
        foreach (var m in _matches)
        {
            var oldValue = m.Text;
            var newValue = ComputeReplaceText(m);
            if (newValue is not null && newValue != oldValue)
            {
                var colAttr = m.Property.GetCustomAttribute<ColumnAttribute>();
                var colName = colAttr?.Name ?? m.Property.Name;
                edits.Add(new EditRecord(m.Entity, m.Property, colName, oldValue, newValue));
            }
        }
        if (edits.Count == 0) return;

        var batch = new BatchEditCommand(edits, OnDirtyChanged);
        CommandHistory.Execute(batch);
        RefreshGrid();
        Hide();
        Notification.ShowSuccess(string.Format(Loc["FindReplaceSuccess"], edits.Count), Loc["FindReplaceTitle"]);
    }

    private string? ComputeReplaceText(MatchInfo match)
    {
        try
        {
            var searchText = SearchBox.Text ?? "";
            var replaceText = ReplaceBox.Text ?? "";

            var caseSensitive = CaseBtn.IsChecked == true;
            var wholeWord = WordBtn.IsChecked == true;
            var useRegex = RegexBtn.IsChecked == true;

            var pattern = useRegex ? searchText : Regex.Escape(searchText);
            if (wholeWord) pattern = $@"\b{pattern}\b";
            var regex = new Regex(pattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

            return regex.Replace(match.Text, replaceText);
        }
        catch { return null; }
    }

    private void RefreshGrid()
    {
        if (_targetGrid is null) return;
        var src = _targetGrid.ItemsSource;
        if (src is null) return;

        // DataGridCollectionView has a clean Refresh() (merge view)
        if (src is DataGridCollectionView cv)
        {
            cv.Refresh();
            return;
        }

        // ObservableCollection: force re-read via deferred swap
        try
        {
            _targetGrid.ItemsSource = null;
            _targetGrid.ItemsSource = src;
        }
        catch
        {
            _targetGrid.ItemsSource = Array.Empty<object>();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_targetGrid is not null)
                    _targetGrid.ItemsSource = src;
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void ClearHighlights()
    {
        ClearCellHighlight();
    }

    private record MatchInfo(int RowIndex, IEntity Entity, PropertyInfo Property,
        string ColumnName, string Text, MatchCollection RegexMatches);
}
