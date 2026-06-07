using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Views.UserControls;

public partial class SearchableDataGrid : UserControl
{
    public LocalizationService Loc { get; }

    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<SearchableDataGrid, bool>("ReadOnly");

    public IEnumerable? ItemsSource // ObservableCollection<object>不行，必须IEnumerable，否则无法绑定到DataGrid
    {
        get;
        set => SetAndRaise(ItemsSourceProperty, ref field, value);
    }

    public static readonly DirectProperty<SearchableDataGrid, IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.RegisterDirect<SearchableDataGrid, IEnumerable?>(nameof(ItemsSource),
            o => o.ItemsSource, (o, v) => o.ItemsSource = v);

    public bool ReadOnly
    {
        get { return GetValue(ReadOnlyProperty); }
        set { SetValue(ReadOnlyProperty, value); }
    }

    public static readonly StyledProperty<bool> ShowRowDetailsProperty =
        AvaloniaProperty.Register<SearchableDataGrid, bool>(nameof(ShowRowDetails), true);

    public bool ShowRowDetails
    {
        get => GetValue(ShowRowDetailsProperty);
        set => SetValue(ShowRowDetailsProperty, value);
    }

    /// <summary>EntityIds that have been edited (for yellow row background). Set by parent view.</summary>
    public static readonly DirectProperty<SearchableDataGrid, HashSet<string>?> EditedEntityIdsProperty =
        AvaloniaProperty.RegisterDirect<SearchableDataGrid, HashSet<string>?>(
            nameof(EditedEntityIds), o => o.EditedEntityIds, (o, v) => o.EditedEntityIds = v);

    private HashSet<string>? _editedEntityIds;
    public HashSet<string>? EditedEntityIds
    {
        get => _editedEntityIds;
        set => SetAndRaise(EditedEntityIdsProperty, ref _editedEntityIds, value);
    }

    /// <summary>EntityIds overridden by higher-priority mod (for gray row background).</summary>
    public static readonly DirectProperty<SearchableDataGrid, HashSet<string>?> OverriddenEntityIdsProperty =
        AvaloniaProperty.RegisterDirect<SearchableDataGrid, HashSet<string>?>(
            nameof(OverriddenEntityIds), o => o.OverriddenEntityIds, (o, v) => o.OverriddenEntityIds = v);

    private HashSet<string>? _overriddenEntityIds;
    public HashSet<string>? OverriddenEntityIds
    {
        get => _overriddenEntityIds;
        set => SetAndRaise(OverriddenEntityIdsProperty, ref _overriddenEntityIds, value);
    }

    /// <summary>EntityIds of newly created entities (for green row background).</summary>
    public static readonly DirectProperty<SearchableDataGrid, HashSet<string>?> NewEntityIdsProperty =
        AvaloniaProperty.RegisterDirect<SearchableDataGrid, HashSet<string>?>(
            nameof(NewEntityIds), o => o.NewEntityIds, (o, v) => o.NewEntityIds = v);

    private HashSet<string>? _newEntityIds;
    public HashSet<string>? NewEntityIds
    {
        get => _newEntityIds;
        set => SetAndRaise(NewEntityIdsProperty, ref _newEntityIds, value);
    }

    /// <summary>Merge store for this DataGrid instance. Set by parent view before attach.</summary>
    public EntityMergeStore? MergeStore { get; set; }

    /// <summary>Edit tracking store for this DataGrid instance. Set by parent view before attach.</summary>
    public EditTrackingStore? EditStore { get; set; }

    private readonly ILogger<SearchableDataGrid> _slog;

    public SearchableDataGrid()
    {
        InitializeComponent();
        Loc = App.ServiceProvider.GetRequiredService<LocalizationService>();
        _slog = App.ServiceProvider.GetRequiredService<ILogger<SearchableDataGrid>>();
        ShowRowDetailsProperty.Changed.AddClassHandler<SearchableDataGrid>((s, _) =>
            s.MainGrid.RowDetailsVisibilityMode = s.ShowRowDetails
                ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected
                : DataGridRowDetailsVisibilityMode.Collapsed);

        // Set initial state — class handler only fires on change, not for default value
        MainGrid.RowDetailsVisibilityMode = ShowRowDetails
            ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected
            : DataGridRowDetailsVisibilityMode.Collapsed;

        CloneMenuItem.Header = Loc["CloneRow"];
        FindRefsMenuItem.Header = Loc["FindReferences"];

        var config = App.ServiceProvider.GetRequiredService<IConfigService>().Config;
        if (config.GridRowHeight > 0)
            MainGrid.RowHeight = config.GridRowHeight;

        WeakReferenceMessenger.Default.Register<SearchableDataGrid, Data.Messages.GridRowHeightChangedMessage>(
            this, (grid, msg) =>
            {
                grid.MainGrid.RowHeight = msg.RowHeight > 0 ? msg.RowHeight : double.NaN;
            });

        MainGrid.AddHandler(Control.ContextRequestedEvent, OnContextMenuOpening,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loadingRowCount = 0;
        _slog.LogDebug("[SdgAttach] editedIds={EIds} ovIds={OIds} newIds={NIds}",
            EditedEntityIds?.Count ?? -1, OverriddenEntityIds?.Count ?? -1, NewEntityIds?.Count ?? -1);

        // Push this DataGrid's stores as the active stores for converters/support code
        if (MergeStore is not null || EditStore is not null)
            GenericDataGridHelper.SetActiveStores(MergeStore, EditStore);

        Dispatcher.UIThread.Post(() =>
        {
            _slog.LogDebug("[SdgAttach:Loaded] itemsSource={HasSrc} editedIds={EIds}",
                MainGrid.ItemsSource != null, EditedEntityIds?.Count ?? -1);
            if (MainGrid.ItemsSource is not null)
            {
                var src = MainGrid.ItemsSource;
                MainGrid.ItemsSource = null;
                MainGrid.ItemsSource = src;
            }
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _slog.LogDebug("[SdgDetach]");
        if (MergeStore is not null || EditStore is not null)
            GenericDataGridHelper.SetActiveStores(null, null);
    }

    private ListSortDirection _lastDirection = ListSortDirection.Ascending;
    private string? _lastSortProperty;

    // Pending edit state captured before cell editing begins
    private IEntity? _pendingEntity;
    private string? _pendingPropertyName;
    private object? _pendingOldValue;

    public string? CurrentSortProperty => _lastSortProperty;
    public ListSortDirection CurrentSortDirection => _lastDirection;

    /// <summary>
    /// Re-applies the last known sort to the current ItemsSource.
    /// Used when ItemsSource is replaced externally (e.g. ShowAll toggle).
    /// </summary>
    public void ReapplySort()
    {
        if (string.IsNullOrEmpty(_lastSortProperty)) return;

        var source = MainGrid.ItemsSource;
        if (source is not IList list) return;

        var items = list.Cast<object>().ToList();
        SortItems(items, _lastSortProperty);
        MainGrid.ItemsSource = new ObservableCollection<object>(items);
    }

    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        var prop = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(prop)) return;

        if (prop == _lastSortProperty)
            _lastDirection = _lastDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        else
            _lastDirection = ListSortDirection.Ascending;
        _lastSortProperty = prop;

        var source = MainGrid.ItemsSource;
        if (source is IList list)
        {
            var items = list.Cast<object>().ToList();
            SortItems(items, prop);

            Dispatcher.UIThread.Post(() => { MainGrid.ItemsSource = new ObservableCollection<object>(items); },
                DispatcherPriority.Background);
        }
    }

    private void SortItems(List<object> items, string prop)
    {
        var first = items.FirstOrDefault();
        if (first == null) return;
        var propInfo = first.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (propInfo == null) return;

        items.Sort((a, b) =>
        {
            var va = propInfo.GetValue(a);
            var vb = propInfo.GetValue(b);
            var cmp = Comparer<object>.Default.Compare(va, vb);
            return _lastDirection == ListSortDirection.Descending ? -cmp : cmp;
        });
    }

    /// <summary>Optional hook: called before a cell enters edit mode. Return false to block editing.</summary>
    public Func<IEntity, bool>? CanEditEntity { get; set; }

    /// <summary>Optional hook: called when an edit is blocked by CanEditEntity.</summary>
    public Action<IEntity>? OnEditBlocked { get; set; }

    private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Row.DataContext is not IEntity entity) return;

        if (CanEditEntity is not null && !CanEditEntity(entity))
        {
            e.Cancel = true;
            OnEditBlocked?.Invoke(entity);
            return;
        }

        var propName = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(propName)) return;

        var prop = entity.GetType().GetProperty(propName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (prop is null) return;

        _pendingEntity = entity;
        _pendingPropertyName = propName;
        _pendingOldValue = prop.GetValue(entity);
    }

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel)
        {
            _pendingEntity = null;
            _pendingPropertyName = null;
            _pendingOldValue = null;
            return;
        }

        if (e.Row.DataContext is IEntity entity)
        {
            var propName = e.Column.SortMemberPath ?? _pendingPropertyName;
            var newValue = string.IsNullOrEmpty(propName)
                ? null
                : entity.GetType().GetProperty(propName,
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                    ?.GetValue(entity);

            // Fire commit event for undo/redo
            if (_pendingEntity == entity && _pendingPropertyName is not null
                                         && !Equals(_pendingOldValue, newValue))
            {
                GenericDataGridHelper.RaiseCellEditCommitted(
                    entity, _pendingPropertyName, _pendingOldValue, newValue);
            }

            GenericDataGridHelper.EditedCells.Add((entity.EntityId, e.Column.Header?.ToString() ?? ""));
            // Also update local property so LoadingRow sees the edit after tab switch
            if (_editedEntityIds is not null)
                _editedEntityIds.Add(entity.EntityId);
            _slog.LogDebug("[CellEditEnd] eid={EID} col={Col} totalEdits={Total}",
                entity.EntityId[..Math.Min(8, entity.EntityId.Length)], e.Column.Header, GenericDataGridHelper.EditedCells.Count);
            App.ServiceProvider!.GetRequiredService<CommunityToolkit.Mvvm.Messaging.IMessenger>()
                .Send(new NeoEditor.Data.Messages.CellEditedMessage(entity.GetType()));
            e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 255, 220));
        }

        _pendingEntity = null;
        _pendingPropertyName = null;
        _pendingOldValue = null;
    }

    private int _loadingRowCount;
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is IEntity entity)
        {
            var overridden = OverriddenEntityIds;
            var newIds = NewEntityIds;
            var editedIds = EditedEntityIds;

            var isOv = overridden is not null && overridden.Contains(entity.EntityId);
            var isNew = newIds is not null && newIds.Contains(entity.EntityId);
            var hasEd = editedIds is not null && editedIds.Contains(entity.EntityId);

            if (_loadingRowCount < 5 || hasEd || isNew || isOv)
                _slog.LogDebug("[LoadingRow #{N}] eid={EID} ov={OV} new={NW} ed={ED} editedIdsCount={EC}",
                    _loadingRowCount, entity.EntityId[..Math.Min(8, entity.EntityId.Length)], isOv, isNew, hasEd, editedIds?.Count ?? -1);
            _loadingRowCount++;

            if (isOv)
                e.Row.Background = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            else if (isNew)
                e.Row.Background = new SolidColorBrush(Color.FromRgb(220, 255, 220));
            else if (hasEd)
                e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 255, 220));
            else
                e.Row.Background = null;
        }
    }

    private void OnCloneRowClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is IEntity entity)
            GenericDataGridHelper.RaiseCloneRowRequested(entity);
    }

    private void OnFindRefsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MainGrid.SelectedItem is IEntity entity)
            GenericDataGridHelper.RaiseFindReferencesRequested(entity);
    }

    /// <summary>Force-refresh all visible row backgrounds from the current local properties.</summary>
    public void RefreshRowBackgrounds()
    {
        var overridden = OverriddenEntityIds;
        var newIds = NewEntityIds;
        var editedIds = EditedEntityIds;
        var rows = MainGrid.GetVisualDescendants().OfType<DataGridRow>().ToList();
        _slog.LogDebug("[RefreshBG] rows={Rows} editedIds={EIds} ovIds={OIds} newIds={NIds}",
            rows.Count, editedIds?.Count ?? -1, overridden?.Count ?? -1, newIds?.Count ?? -1);
        foreach (var row in rows)
        {
            if (row.DataContext is not IEntity entity) continue;
            if (overridden is not null && overridden.Contains(entity.EntityId))
                row.Background = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            else if (newIds is not null && newIds.Contains(entity.EntityId))
                row.Background = new SolidColorBrush(Color.FromRgb(220, 255, 220));
            else if (editedIds is not null && editedIds.Contains(entity.EntityId))
                row.Background = new SolidColorBrush(Color.FromRgb(255, 255, 220));
            else
                row.Background = null;
        }
    }

    private void OnContextMenuOpening(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = MainGrid.SelectedItems.Count > 0;
        CloneMenuItem.IsVisible = selected;
        FindRefsMenuItem.IsVisible = selected;
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (ItemsSource == null) return;

        var dataGrid = (DataGrid)sender;
        const string mergedIdHeader = "→Id";

        // Add/remove →Id column based on whether we're in merge view
        if (GenericDataGridHelper.EntityMergedIds.Count > 0)
        {
            if (!dataGrid.Columns.Any(c => c.Header?.ToString() == mergedIdHeader))
                dataGrid.Columns.Insert(0, new DataGridTextColumn
                {
                    Header = mergedIdHeader,
                    IsReadOnly = true,
                    SortMemberPath = "MergedId",
                    FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                    Binding = new global::Avalonia.Data.Binding(".")
                    {
                        Converter = new Helper.Converter.EntityMergedIdConverter()
                    }
                });
        }
        else
        {
            var existing = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == mergedIdHeader);
            if (existing != null) dataGrid.Columns.Remove(existing);
        }

        // Add ModName column (read-only, shows source mod name) — only when multiple mods
        if (GenericDataGridHelper.EntityModNames.Values.Distinct().Count() > 1)
        {
            const string modColHeader = "Mod";
            if (!dataGrid.Columns.Any(c => c.Header?.ToString() == modColHeader))
            {
                var insertPos = Math.Min(1, dataGrid.Columns.Count);
                dataGrid.Columns.Insert(insertPos, new DataGridTextColumn
                {
                    Header = modColHeader,
                    IsReadOnly = true,
                    Width = new DataGridLength(120),
                    Binding = new global::Avalonia.Data.Binding("EntityId")
                    {
                        Converter = new Helper.Converter.ModNameColumnConverter()
                    }
                });
            }
        }
        else
        {
            var modCol = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Mod");
            if (modCol != null) dataGrid.Columns.Remove(modCol);
        }

        // Hide internal tracking columns by default (can be re-shown via column manager)
        var hiddenProps = new HashSet<string> { "ModId", "FilePath", "EntityId" };
        if (hiddenProps.Contains(e.PropertyName))
        {
            e.Column.IsVisible = false;
        }

        // Try to get the runtime item type (handles ObservableCollection<object> where T is object)
        Type? runtimeType = null;
        var enumerator = ItemsSource.GetEnumerator();
        using (var disposable = enumerator as IDisposable)
        {
            if (enumerator.MoveNext() && enumerator.Current is not null)
                runtimeType = enumerator.Current.GetType();
        }

        // Prefer the generic argument type, but fall back to runtime if it's too generic (object)
        var enumerableType = ItemsSource.GetType()
            .GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        var genericArgType = enumerableType?.GetGenericArguments()[0];

        var modelType = (genericArgType is null || genericArgType == typeof(object))
            ? runtimeType
            : genericArgType;

        if (modelType != null)
            GenericDataGridHelper.ConfigureColumn(e, key => App.Localizor[key] ?? key, modelType);

        // Apply persisted column visibility config (per-table)
        if (modelType != null)
            ApplyColumnVisibilityConfig(e, modelType);
    }

    private static void ApplyColumnVisibilityConfig(DataGridAutoGeneratingColumnEventArgs e, Type modelType)
    {
        var tableName = modelType.GetCustomAttribute<TableAttribute>()?.Name;
        if (tableName is null) return;

        var configSvc = App.ServiceProvider.GetService<Services.IConfigService>();
        if (configSvc?.Config?.ColumnVisibility is not { } cv) return;

        if (!cv.TryGetValue(tableName, out var visibleCols)) return;

        var propName = e.PropertyName ?? e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(propName)) return;

        e.Column.IsVisible = visibleCols.Contains(propName);
    }
}