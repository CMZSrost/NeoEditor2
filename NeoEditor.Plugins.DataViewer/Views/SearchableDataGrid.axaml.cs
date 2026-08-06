using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.DataViewer.Converters;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.Views;

public partial class SearchableDataGrid : UserControl
{
    // ── Service properties — set by parent view after construction ──────

    public ILocalizationService? Loc { get; set; }
    public ILoggerFactory? LoggerFactory { get; set; }
    public IConfigService? ConfigService { get; set; }
    public IMessenger? Messenger { get; set; }
    public DataTableService? DataTable { get; set; }
    public ColumnTemplateFactory? ColumnTemplateFactory { get; set; }
    public InteractionHandler? InteractionHandler { get; set; }
    public DataGridInteractionState? DataGridState { get; set; }
    public IDataGridCellInteractionService? CellInteraction { get; set; }
    public IDataGridNavigationService? DataGridNavigation { get; set; }
    public ISelectionService? SelectionService { get; set; }

    private ILogger<SearchableDataGrid> Slog =>
        _slog ??= LoggerFactory?.CreateLogger<SearchableDataGrid>();

    private ILogger<SearchableDataGrid>? _slog;

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

    public SearchableDataGrid()
    {
        InitializeComponent();
        ShowRowDetailsProperty.Changed.AddClassHandler<SearchableDataGrid>((s, _) =>
            s.MainGrid.RowDetailsVisibilityMode = s.ShowRowDetails
                ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected
                : DataGridRowDetailsVisibilityMode.Collapsed);

        // Set initial state — class handler only fires on change, not for default value
        MainGrid.RowDetailsVisibilityMode = ShowRowDetails
            ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected
            : DataGridRowDetailsVisibilityMode.Collapsed;

        // Fixed compact row height — dynamic height disabled
        MainGrid.RowHeight = 22;
    }

    /// <summary>Filtering model for column-level filtering. Created in InitializeServices.</summary>
    private FilteringModel? _filterModel;

    /// <summary>Whether column filtering is currently active (any filter descriptor applied).</summary>
    public bool HasActiveFilter => _filterModel?.Descriptors.Count > 0;

    /// <summary>Clears all column filters and refreshes the view.</summary>
    public void ClearFilter()
    {
        _filterModel?.Clear();
    }

    /// <summary>
    /// Wire up message handlers and event handlers after service properties have been set.
    /// Must be called by parent view after all service properties are assigned.
    /// </summary>
    public void InitializeServices()
    {
        // ── Set up column filtering infrastructure ──
        // FilteringModel + DataGridAccessorFilteringAdapterFactory drive the
        // DataGridCollectionView.Filter predicate. Per-column, ShowFilterButton
        // renders the filter icon; ColumnFilterFlyout (assigned in
        // OnAutoGeneratingColumn) provides the popup UI.
        _filterModel = new FilteringModel { OwnsViewFilter = true };
        MainGrid.FilteringModel = _filterModel;
        MainGrid.FilteringAdapterFactory = new DataGridAccessorFilteringAdapterFactory();

        var messenger = Messenger ?? WeakReferenceMessenger.Default;
        messenger.Register<SearchableDataGrid, ColumnVisibilityChangedMessage>(
            this, (grid, msg) => grid.OnColumnVisibilityChanged(msg.TableName));

        // Ctrl+RMB on reference cells is handled by PointerPressed handlers in
        // ColumnTemplateFactory.ConfigureColumn. All right-click context menus removed.

        // R15: Ctrl+LMB on data row (non-ref cell) → Navigate; Ctrl+RMB → Peek.
        var state = DataGridState;

        this.AddHandler(InputElement.PointerPressedEvent, (_, args) =>
        {
            if (args is not PointerPressedEventArgs pp
                || (pp.KeyModifiers & KeyModifiers.Control) == 0)
            {
                if (state is not null)
                    state.SuppressNextSelectionChanged = false;
                return;
            }

            if (state is not null)
            {
                state.SuppressNextSelectionChanged = false;
                if (pp.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    state.SuppressNextSelectionChanged = true;
            }
        }, RoutingStrategies.Tunnel, false);

        MainGrid.AddHandler(InputElement.PointerPressedEvent, (_, args) =>
        {
            if (args is not PointerPressedEventArgs pp) return;
            if ((pp.KeyModifiers & KeyModifiers.Control) == 0) return;
            if (state is not null && state.SuppressNextSelectionChanged)
            {
                // Ref-cell ColumnTemplateFactory Tunnel already handled this click.
                return;
            }

            var row = (pp.Source as Avalonia.Visual)?.FindAncestorOfType<DataGridRow>();
            if (row?.DataContext is not IEntity entity) return;

            pp.Handled = true;
            if (state is not null)
                state.SuppressNextSelectionChanged = pp.GetCurrentPoint(MainGrid).Properties.IsLeftButtonPressed;
            if (pp.GetCurrentPoint(MainGrid).Properties.IsRightButtonPressed)
                messenger.Send(new PeekEntityMessage(entity.GetType(), entity.EntityId, entity));
            else
                SelectionService?.RequestOpenEntity(entity);
        }, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _loadingRowCount = 0;
        Slog?.LogDebug("[SdgAttach] editedIds={EIds} ovIds={OIds} newIds={NIds}",
            EditedEntityIds?.Count ?? -1, OverriddenEntityIds?.Count ?? -1, NewEntityIds?.Count ?? -1);

        // Push this DataGrid's stores as the active stores for converters/support code
        if (MergeStore is not null || EditStore is not null)
            DataTable?.SetActiveStores(MergeStore, EditStore);

        Dispatcher.UIThread.Post(() =>
        {
            Slog?.LogDebug("[SdgAttach:Loaded] itemsSource={HasSrc} editedIds={EIds}",
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
        Slog?.LogDebug("[SdgDetach]");
        DataGridState?.ColumnMetaCache.Remove(MainGrid);
        if (MergeStore is not null || EditStore is not null)
            DataTable?.SetActiveStores(null, null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Per-row height freeze — prevents column-virtualization jitter
    // ═══════════════════════════════════════════════════════════════════════

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
        var keySelector = GetSortKeySelector(_lastSortProperty, items);
        if (keySelector == null) return;
        SortItems(items, keySelector);
        // Safe to replace synchronously — called outside the Sorting event,
        // no pending ProcessSort to conflict with.
        MainGrid.ItemsSource = null;
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
            var keySelector = GetSortKeySelector(prop, items);
            if (keySelector == null) return;
            SortItems(items, keySelector);
            // MUST defer ItemsSource replacement: ProDataGrid's
            // DataGridColumnHeader.ProcessSort is dispatched asynchronously
            // after this event returns. Replacing ItemsSource synchronously
            // invalidates column state that the pending ProcessSort depends on
            // → NullReferenceException crash.
            // Deferring to Background priority lets ProcessSort complete first,
            // then our pre-sorted data replaces the ItemsSource.
            Dispatcher.UIThread.Post(() =>
            {
                MainGrid.ItemsSource = null;
                MainGrid.ItemsSource = new ObservableCollection<object>(items);
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Build a key selector for the given sort property.
    /// Handles virtual columns (MergedId → DataTable.EntityMergedIds,
    /// Mod → DataTable.EntityModNames) alongside ordinary reflected properties.
    /// </summary>
    private Func<object, object?>? GetSortKeySelector(string prop, List<object> items)
    {
        // Virtual column "→Id" — sort by merged entity ID
        if (prop == "MergedId" && DataTable?.EntityMergedIds is { Count: > 0 } mergedIds)
        {
            return item => item is IEntity e
                ? mergedIds.TryGetValue(e.EntityId, out var mid) ? mid : int.MaxValue
                : null;
        }

        // Virtual column "Mod" — sort by source mod name
        if (prop == "Mod" && DataTable?.EntityModNames is { Count: > 0 } modNames)
        {
            return item => item is IEntity e
                ? modNames.TryGetValue(e.EntityId, out var name) ? name : "￿"
                : null;
        }

        // Ordinary reflected property
        var first = items.FirstOrDefault();
        if (first == null) return null;
        var propInfo = first.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (propInfo == null) return null;
        return item => propInfo.GetValue(item);
    }

    private void SortItems(List<object> items, Func<object, object?> keySelector)
    {
        items.Sort((a, b) =>
        {
            var va = keySelector(a);
            var vb = keySelector(b);
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

            // Fire commit event for undo/redo — only when value actually changed.
            // EditedCells / local tracking / background highlighter are also gated here
            // to avoid polluting dirty state on click-without-edit interactions.
            if (_pendingEntity == entity && _pendingPropertyName is not null
                                         && !Equals(_pendingOldValue, newValue))
            {
                InteractionHandler?.RaiseCellEditCommitted(
                    entity, _pendingPropertyName, _pendingOldValue, newValue);
                // Add to this DataGrid's own EditStore (not the global session,
                // which may point to a different view's store in multi-view layouts).
                // Use the [Column] name — the header shows the C# property name, which can
                // differ from the XML column key the highlight converter compares against.
                var prop = entity.GetType().GetProperty(propName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                var colName = prop?.GetCustomAttribute<ColumnAttribute>()?.Name ?? propName;
                if (EditStore is not null)
                    EditStore.EditedCells.Add((entity.EntityId, colName));
                else if (DataTable is not null)
                    DataTable.EditedCells.Add((entity.EntityId, colName));
                // Also update local property so LoadingRow sees the edit after tab switch
                if (_editedEntityIds is not null)
                    _editedEntityIds.Add(entity.EntityId);
                var totalEdits = DataTable?.EditedCells.Count ?? EditStore?.EditedCells.Count ?? -1;
                Slog?.LogDebug("[CellEditEnd] eid={EID} col={Col} totalEdits={Total}",
                    entity.EntityId[..Math.Min(8, entity.EntityId.Length)], e.Column.Header, totalEdits);
                (Messenger ?? WeakReferenceMessenger.Default)
                    .Send(new CellEditedMessage(entity.GetType()));
                // No whole-row paint here: field-level highlights are applied by the cell
                // converter (re-evaluated on commit re-render) and RefreshRowBackgrounds
                // (called by PushEditStateToGrid) — a row wash would mark every field dirty.
            }
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
                Slog?.LogDebug("[LoadingRow #{N}] eid={EID} ov={OV} new={NW} ed={ED} editedIdsCount={EC}",
                    _loadingRowCount, entity.EntityId[..Math.Min(8, entity.EntityId.Length)], isOv, isNew, hasEd,
                    editedIds?.Count ?? -1);
            _loadingRowCount++;

            ApplyCellHighlights(e.Row, entity, isOv, isNew, hasEd);
        }
    }

    /// <summary>Force-refresh all visible row/cell highlights from the current local properties.</summary>
    public void RefreshRowBackgrounds()
    {
        var overridden = OverriddenEntityIds;
        var newIds = NewEntityIds;
        var editedIds = EditedEntityIds;
        var rows = MainGrid.GetVisualDescendants().OfType<DataGridRow>().ToList();
        Slog?.LogDebug("[RefreshBG] rows={Rows} editedIds={EIds} ovIds={OIds} newIds={NIds}",
            rows.Count, editedIds?.Count ?? -1, overridden?.Count ?? -1, newIds?.Count ?? -1);
        foreach (var row in rows)
        {
            if (row.DataContext is not IEntity entity) continue;
            var isOv = overridden is not null && overridden.Contains(entity.EntityId);
            var isNew = newIds is not null && newIds.Contains(entity.EntityId);
            var hasEd = editedIds is not null && editedIds.Contains(entity.EntityId);
            ApplyCellHighlights(row, entity, isOv, isNew, hasEd);
        }
    }

    /// <summary>
    /// Docs/41 需求: field-level diff on the DataGrid — edited CELLS are highlighted yellow
    /// (not the whole row), and the PRIMARY-KEY cell of any edited row is always highlighted
    /// as an anchor (keys are immutable, so they would otherwise never light up and the row
    /// becomes hard to find). Cell backgrounds come from CellEditedHighlightConverter on the
    /// column templates (re-evaluated on every row load / grid refresh); here we only decide
    /// the ROW-level wash: overridden rows keep their grey wash, new rows their green wash,
    /// edited rows get NO row background (field-level only).
    /// </summary>
    private void ApplyCellHighlights(DataGridRow row, IEntity entity,
        bool isOverridden, bool isNew, bool hasEdits)
    {
        if (isOverridden)
        {
            row.Background = new SolidColorBrush(Color.FromRgb(200, 200, 200));
            return;
        }

        if (isNew)
        {
            row.Background = new SolidColorBrush(Color.FromRgb(220, 255, 220));
            return;
        }

        // Edited rows: no row background — cell-level highlights (converter) only.
        row.Background = null;
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (ItemsSource == null) return;

        var dataGrid = (DataGrid)sender;
        const string mergedIdHeader = "→Id";

        // Add/remove →Id column based on whether we're in merge view
        var dt = DataTable;
        if (dt is not null && dt.EntityMergedIds.Count > 0)
        {
            if (!dataGrid.Columns.Any(c => c.Header?.ToString() == mergedIdHeader))
            {
                var mergedIdColumn = new DataGridTextColumn
                {
                    Header = mergedIdHeader,
                    IsReadOnly = true,
                    SortMemberPath = "MergedId",
                    FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
                    ShowFilterButton = true,
                    ColumnKey = "MergedId",
                    CanUserHide = false,
                    FilterFlyout = new ColumnFilterFlyout(_filterModel!, "MergedId", "MergedId"),
                    Binding = new global::Avalonia.Data.Binding(".")
                    {
                        Converter = new EntityMergedIdConverter()
                    }
                };
                // Virtual column — supply a value accessor so filtering reads the
                // merged ID instead of the IValueConverter-transformed display text.
                var dtCapture = dt;
                DataGridColumnFilter.SetValueAccessor(mergedIdColumn,
                    new DataGridColumnValueAccessor<object, object?>(item => item is IEntity entity
                                                                             && dtCapture.EntityMergedIds.TryGetValue(
                                                                                 entity.EntityId, out var mid)
                        ? mid
                        : null));
                dataGrid.Columns.Insert(0, mergedIdColumn);
            }
        }
        else
        {
            var existing = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == mergedIdHeader);
            if (existing != null) dataGrid.Columns.Remove(existing);
        }

        // Add ModName column (read-only, shows source mod name) — only when multiple mods
        if (dt is not null && dt.EntityModNames.Values.Distinct().Count() > 1)
        {
            const string modColHeader = "Mod";
            if (!dataGrid.Columns.Any(c => c.Header?.ToString() == modColHeader))
            {
                var insertPos = Math.Min(1, dataGrid.Columns.Count);
                var modColumn = new DataGridTextColumn
                {
                    Header = modColHeader,
                    IsReadOnly = true,
                    Width = new DataGridLength(120),
                    SortMemberPath = "Mod",
                    ShowFilterButton = true,
                    ColumnKey = "Mod",
                    FilterFlyout = new ColumnFilterFlyout(_filterModel!, "Mod", "Mod"),
                    Binding = new global::Avalonia.Data.Binding("EntityId")
                    {
                        Converter = new ModNameColumnConverter()
                    }
                };
                // Virtual column — supply a value accessor so filtering reads the
                // mod name instead of the IValueConverter-transformed display text.
                var dtCapture2 = dt;
                DataGridColumnFilter.SetValueAccessor(modColumn,
                    new DataGridColumnValueAccessor<object, object?>(item => item is IEntity entity
                                                                             && dtCapture2.EntityModNames.TryGetValue(
                                                                                 entity.EntityId, out var mn)
                        ? mn
                        : null));
                dataGrid.Columns.Insert(insertPos, modColumn);
            }
        }
        else
        {
            var modCol = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Mod");
            if (modCol != null) dataGrid.Columns.Remove(modCol);
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
            ColumnTemplateFactory?.ConfigureColumn(e, modelType);

        // Populate column metadata cache for navigation (Bug 2 fix)
        if (modelType != null && !e.Cancel)
        {
            var propInfo = modelType.GetProperty(e.PropertyName);
            var refAttr = propInfo?.GetCustomAttribute<Helper.ReferenceFieldAttribute>();
            if (refAttr is not null)
            {
                var state = DataGridState;
                if (state is not null)
                {
                    if (!state.ColumnMetaCache.TryGetValue(dataGrid, out var meta))
                        state.ColumnMetaCache[dataGrid] = meta = new();
                    meta[e.PropertyName] = refAttr;
                }
            }

            // Enable column header filter button + stable column key + filter flyout.
            // ShowFilterButton renders the filter icon. FilterFlyoutFactory creates a
            // type-aware Flyout with ProDataGrid's built-in editor templates (text/number/
            // enum/bool), falling back to ColumnFilterFlyout if the theme lacks templates.
            e.Column.ShowFilterButton = true;
            var columnKey = e.PropertyName ?? e.Column.SortMemberPath;
            e.Column.ColumnKey = columnKey;
            e.Column.FilterFlyout = FilterFlyoutFactory.Create(
                propInfo?.PropertyType ?? modelType.GetProperty(e.PropertyName)?.PropertyType ?? typeof(string),
                columnKey!, e.PropertyName, _filterModel!, Slog);
        }

        // Apply persisted column visibility from the shared ColumnVisibilityKeys source.
        var configService = ConfigService;
        if (modelType != null && configService is not null)
        {
            var tableName = ColumnVisibilityKeys.GetTableName(modelType);
            if (tableName is not null)
            {
                var cv = configService.Config?.ColumnVisibility;
                if (cv is not null)
                {
                    var propName = e.PropertyName ?? e.Column.SortMemberPath;
                    if (!string.IsNullOrEmpty(propName))
                        e.Column.IsVisible = ColumnVisibilityKeys.IsVisible(cv, tableName, propName);

                    foreach (var col in dataGrid.Columns)
                    {
                        var key = col.SortMemberPath;
                        if (string.IsNullOrEmpty(key)) continue;
                        col.IsVisible = ColumnVisibilityKeys.IsVisible(cv, tableName, key);
                    }
                }
            }
        }
    }

    /// <summary>Live-update column visibility when changed in settings or DataGrid column manager.</summary>
    private void OnColumnVisibilityChanged(string tableName)
    {
        var configService = ConfigService;
        if (configService is null) return;
        var cv = configService.Config?.ColumnVisibility;
        if (cv is null) return;

        // Only apply if this DataGrid is currently showing the matching table
        if (ItemsSource is not System.Collections.IEnumerable items) return;
        var first = items.Cast<object>().FirstOrDefault();
        if (first is null) return;
        var currentTable = ColumnVisibilityKeys.GetTableName(first.GetType());
        if (currentTable != tableName) return;

        foreach (var col in MainGrid.Columns)
        {
            var key = col.SortMemberPath;
            if (string.IsNullOrEmpty(key)) continue;
            col.IsVisible = ColumnVisibilityKeys.IsVisible(cv, tableName, key);
        }
    }
}