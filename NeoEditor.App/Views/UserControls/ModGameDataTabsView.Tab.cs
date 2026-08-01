using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;

using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using SearchableDataGrid = NeoEditor.Plugins.DataViewer.Views.SearchableDataGrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Helper.Converter;
using NeoEditor.Services;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Views.Dialog;
using System.Xml.Linq;
using NeoEditor.Data.Command;


namespace NeoEditor.Views.UserControls;

public partial class ModGameDataTabsView
{
    private GameDataTypeTabItem? GetActiveTab()
    {
        return TabListBox?.SelectedItem as GameDataTypeTabItem;
    }

    /// <summary>Push edit/override/new state to SharedDataGrid from explicit stores.
    /// Takes parameters to avoid the global GenericDataGridHelper.SetActiveStores race condition
    /// when multiple views attach simultaneously.</summary>
    private void PushEditStateToGrid(EntityMergeStore mergeStore, EditTrackingStore editStore)
    {
        if (SharedDataGrid is null) { _logger.LogWarning("[PushEdit] SharedDataGrid is null"); return; }
        var editedCells = editStore.EditedCells;
        var overridden = mergeStore.OverriddenEntityIds;
        var newIds = editStore.NewEntityIds;
        _logger.LogDebug("[PushEdit] ESHash={ESHash:x} editedCells={EC} overridden={OR} newIds={NI} isMerge={IM} loadPending={LP}",
            editStore.GetHashCode(), editedCells.Count, overridden.Count, newIds.Count, IsMergeView, _loadPending);
        foreach (var ec in editedCells.Take(3))
            _logger.LogDebug("[PushEdit]   sample: entityId={EID} col={Col}", ec.EntityId, ec.ColumnName);
        // Push stores to DataGrid properties + activate them in GDH immediately
        // (DataGrid may already be attached, so OnAttachedToVisualTree won't fire)
        SharedDataGrid.MergeStore = mergeStore;
        SharedDataGrid.EditStore = editStore;
        WorkspaceSession.SetActiveStores(mergeStore, editStore);
        SharedDataGrid.EditedEntityIds = new HashSet<string>(WorkspaceSession.DirtyEntities);
        SharedDataGrid.OverriddenEntityIds = new HashSet<string>(overridden);
        SharedDataGrid.NewEntityIds = new HashSet<string>(newIds);
        // Immediately refresh visible row backgrounds so dirty/overridden/new indicators
        // are applied without waiting for scroll/LoadingRow. Fixes Tab dirty vs DataGrid
        // row highlighting inconsistency (Test Round 9 Bug 2).
        SharedDataGrid.RefreshRowBackgrounds();
    }

    private object? GetSelectedItemFromActiveGrid()
    {
        // SelectedContent is the data item (GameDataTypeTabItem), not a Control.
        // Walk the visual tree to find the active DataGrid inside SharedDataGrid.
        var dataGrid = FindActiveDataGrid();
        return dataGrid?.SelectedItem;
    }

    private bool _loadPending;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ProfileInfoProperty && ProfileInfo is not null)
        {
            // Skip reload if we already have tabs for this same profile
            if (change.OldValue is ProfileInfo oldProfile && oldProfile.ProfileId == ProfileInfo.ProfileId && Tabs.Count > 0)
                return;
            _loadPending = true;
            if (this.IsAttachedToVisualTree())
                AsyncHelper.FireAndForget(ReloadMergeTabsAsync(ProfileInfo));
        }
        else if (change.Property == ReadOnlyProperty)
        {
            IsBrowseMode = ReadOnly && !IsMergeView;
        }
        else if (change.Property == IsValueEditorVisibleProperty)
        {
            // Editor is now per-tab — no global column manipulation needed
        }
        else if (change.Property == FilterTextProperty && !IsLoading)
        {
            DebounceFilter();
        }
    }

    private void DebounceFilter()
    {
        _filterCts?.Cancel();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;
        Task.Delay(200, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                Dispatcher.UIThread.Post(() =>
                {
                    if (!IsLoading) RebuildFilteredItemsSources();
                });
        }, TaskScheduler.Default);
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        var innerGrid = SharedDataGrid.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
        innerGrid?.CommitEdit(DataGridEditingUnit.Row, true);

        if (TabListBox.SelectedItem is GameDataTypeTabItem tab)
        {
            SharedDataGrid.DataContext = tab;
            // P2 fix (Test Round 10): re-push edit state BEFORE SwitchTabItemsSource so
            // EditedEntityIds is up-to-date when LoadingRow fires for the new tab's rows.
            PushEditStateToGrid(MergeStore, EditStore);
            SwitchTabItemsSource(SharedDataGrid, tab.ItemsSource);
            // Wire column chooser to the newly active DataGrid and persist column
            // visibility changes via PropertyChanged hook.
            WireColumnChooser();
            // ProDataGrid fixes column lifecycle — event handlers survive ItemsSource swap,
            // no need to re-wire SelectionChanged/DoubleTapped.
        }
    }

    /// <summary>
    /// Switch the shared DataGrid to a new ItemsSource.
    /// ProDataGrid fixes column attach/detach lifecycle issues, eliminating the need
    /// for the old AutoGenerateColumns=false→true workaround.
    /// </summary>
    internal static void SwitchTabItemsSource(SearchableDataGrid sdg, IEnumerable newItems)
    {
        var newCount = newItems?.Cast<object>().Count() ?? 0;
        // Clear first to ensure ProDataGrid detaches old columns cleanly,
        // preventing NRE in DataGridColumnHeader.ProcessSort when clicking
        // a column header during data transition.
        sdg.ItemsSource = null;
        sdg.ItemsSource = newItems;
        Serilog.Log.Logger.Information("[SwitchTab] OK: {Count} items", newCount);
    }

    private void OnDataGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Skip if user is Ctrl+clicking a reference — the PointerPressed inline handler
        // in GenericDataGridHelper sets these flags before the DataGrid processes selection.
        // _ctrlKeyDown: quick check based on current keyboard state (works when Ctrl is still held)
        // SuppressNextSelectionChanged: survives even if user released Ctrl before SelectionChanged fires
        if (_ctrlKeyDown || _dataGridState.SuppressNextSelectionChanged)
        {
            _dataGridState.SuppressNextSelectionChanged = false;
            return;
        }
        if (sender is DataGrid grid && grid.SelectedItem is IEntity entity)
        {
            var entityType = entity.GetType().Name;
            _messenger.Send(new OverlayChainRequestedMessage(entity.EntityId, entity.Subject, entityType));
            _messenger.Send(new EntitySelectedMessage(entity, SelectSource.BottomDataGrid));
            // R15: single-click = highlight only.
            // Center document opening is NOT triggered here — see OnDataGridDoubleTapped.
        }
    }

    /// <summary>R15: double-click on DataTable row → open Center EntityEditorDocument.</summary>
    private void OnDataGridDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is IEntity entity)
        {
            var selectionService = ViewServices.SelectionService;
            selectionService.RequestOpenEntity(entity);
        }
    }

    private void OnToggleValueEditorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Toggle editor visibility on all tabs
        var newVisible = !Tabs.Any(t => t.IsEditorVisible);
        foreach (var tab in Tabs)
            tab.IsEditorVisible = newVisible;
    }

    private DataGrid? FindActiveDataGrid()
    {
        return SharedDataGrid.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
    }

    /// <summary>Bind the DataGridColumnChooser to the active DataGrid and hook column
    /// visibility change persistence.</summary>
    private void WireColumnChooser()
    {
        var dataGrid = FindActiveDataGrid();
        if (dataGrid is null || ColumnChooser is null) return;

        ColumnChooser.DataGrid = dataGrid;
        HookColumnVisibilityPersistence(dataGrid);
    }

    /// <summary>Listen for column visibility changes from the DataGridColumnChooser
    /// and persist them to Config + broadcast via ColumnVisibilityChangedMessage.</summary>
    private void HookColumnVisibilityPersistence(DataGrid dataGrid)
    {
        var currentTab = GetActiveTab();
        var tableName = currentTab?.EntityType.GetCustomAttribute<TableAttribute>()?.Name;
        var entityType = currentTab?.EntityType;

        foreach (var col in dataGrid.Columns)
        {
            var capturedTable = tableName;
            var capturedType = entityType;
            var propName = col.SortMemberPath;
            col.PropertyChanged += (_, args) =>
            {
                if (args is not AvaloniaPropertyChangedEventArgs apc) return;
                if (apc.Property.Name != nameof(DataGridColumn.IsVisible)) return;
                if (string.IsNullOrEmpty(capturedTable) || string.IsNullOrEmpty(propName)) return;
                ToggleColumnVisibility(capturedTable, capturedType, propName, col.IsVisible);
                WeakReferenceMessenger.Default.Send(
                    new ColumnVisibilityChangedMessage { TableName = capturedTable });
            };
        }
    }

    /// <summary>Returns true when a text-editing control has keyboard focus, so global undo/redo/paste is suppressed.</summary>
    private static bool IsEditingTextBoxFocused(object? topLevel)
    {
        var focused = (topLevel as TopLevel)?.FocusManager?.GetFocusedElement();
        if (focused is null) return false;
        if (focused is TextBox) return true;
        // AvaloniaEdit.TextEditor may delegate focus to internal elements (TextArea, TextView).
        // Walk the visual ancestor chain to detect if focus is inside any TextEditor.
        for (var v = focused as Avalonia.StyledElement; v != null; v = v.Parent)
            if (v is AvaloniaEdit.TextEditor) return true;
        return false;
    }

    /// <summary>Incrementally toggle one column in the shared ColumnVisibility config.
    /// On first touch, seeds the set with all known keys via ColumnVisibilityKeys.</summary>
    private void ToggleColumnVisibility(string tableName, Type? entityType, string key, bool visible)
    {
        var cv = _configService.Config.ColumnVisibility;
        if (entityType is not null)
            ColumnVisibilityKeys.SeedAllVisible(cv, entityType);
        if (!cv.TryGetValue(tableName, out var set)) return; // shouldn't happen after seed
        if (visible) set.Add(key);
        else set.Remove(key);
        AsyncHelper.FireAndForget(_configService.SaveAsync());
    }

    private void OnSearchHelpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var activeTab = GetActiveTab();
        if (activeTab?.EntityType is not { } entityType) return;

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = Loc["SearchHelpAvailableColumns"], IsEnabled = false });
        menu.Items.Add(new Separator());

        foreach (var prop in Services.FilterService.GetStringProperties(entityType))
        {
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var colName = colAttr?.Name ?? prop.Name;
            var menuItem = new MenuItem { Header = colName };
            menuItem.Click += (_, _) =>
            {
                var current = FilterText ?? "";
                var suffix = string.IsNullOrEmpty(current) ? "" : " ";
                FilterText = current + suffix + colName + ":";
                var tb = this.GetVisualDescendants().OfType<TextBox>()
                    .FirstOrDefault(t => t.Watermark is string);
                tb?.Focus();
            };
            menu.Items.Add(menuItem);
        }

        if (sender is Control c)
            menu.Open(c);
    }

    private void OnModFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ModFilterCombo.SelectedItem is ModFilterItem item)
        {
            _selectedModId = item.ModId;
            _logger.LogDebug("[ModFilter] selected modId={ModId}", _selectedModId);
            RebuildFilteredItemsSources();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _logger.LogInformation("[Attach] ENTER viewHash={VH:x} ESHash={ES:x} MSHash={MS:x} loadPending={LP} merge={IM}",
            GetHashCode(), EditStore.GetHashCode(), MergeStore.GetHashCode(), _loadPending, IsMergeView);

        _navigationRouter.RegisterTarget(this);

        // Global key handlers — works regardless of focus
        var topLevel = TopLevel.GetTopLevel(this);
        topLevel?.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, OnGlobalKeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel?.AddHandler(Avalonia.Input.InputElement.KeyUpEvent, (_, e) =>
        {
            _ctrlKeyDown = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        if (!_loadPending)
        {
            // Re-attach without reload: push current state to grid + force background refresh.
            PushEditStateToGrid(MergeStore, EditStore);
            Dispatcher.UIThread.Post(() => SharedDataGrid.RefreshRowBackgrounds(), DispatcherPriority.Loaded);
            Dispatcher.UIThread.Post(UpdatePersistenceDebugInfo, DispatcherPriority.Loaded);
            return;
        }
        _loadPending = false;

        var cacheKey = $"profile_{ProfileInfo?.ProfileId}";

        // Restore from cache when available — avoids full DB reload on tab switch.
        // Works for both single-mod and merge views. Cache stores Tabs + MergeStore + EditStore.
        if (TabSnapshotCache.TryGetValue(cacheKey, out var cached))
        {
            foreach (var tab in cached.Tabs)
                Tabs.Add(tab);
            _logger.LogInformation("[Attach] cache hit, replacing stores  oldES={OldES:x}→newES={NewES:x} oldMS={OldMS:x}→newMS={NewMS:x}",
                EditStore.GetHashCode(), cached.EditStore.GetHashCode(),
                MergeStore.GetHashCode(), cached.MergeStore.GetHashCode());
            // Replace VM stores with cached ones — so ALL code paths use the same stores.
            _vm.ReplaceStores(cached.MergeStore, cached.EditStore);
            _overriddenEntityIds = new HashSet<string>(MergeStore.OverriddenEntityIds);
            PushEditStateToGrid(MergeStore, EditStore);
            _isDirty = EditStore.EditedCells.Count > 0
                       || EditStore.NewEntityIds.Count > 0;
            IsLoading = false;
            RebuildFilteredItemsSources();
            if (Tabs.Count > 0 && TabListBox.SelectedIndex < 0)
                SelectFirstNonEmptyTab();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                WireActiveGridSelection();
                RefreshIsEmptyMod();
                if (IsMergeView && ProfileInfo is not null)
                    PopulateModFilterCombo(ProfileInfo);
            }, Avalonia.Threading.DispatcherPriority.Loaded);
            AsyncHelper.FireAndForget(RestoreUndoStackFromLogAsync());
            _logger.LogInformation("[Attached] restored {Count} tabs from cache for '{Key}' merge={IsMerge}",
                Tabs.Count, cacheKey, IsMergeView);
            return;
        }

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (ProfileInfo is not null)
                    await ReloadMergeTabsAsync(ProfileInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Attached] reload failed");
                IsLoading = false;
            }
        }, DispatcherPriority.Background);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _navigationRouter.UnregisterTarget(this);
        // NOTE: Do NOT clear active stores here.
        // Multiple data views can coexist in the Dock workspace, and clearing on detach
        // would break reference navigation for the remaining visible view.
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  INavigationTarget implementation
    // ═══════════════════════════════════════════════════════════════════════

    bool Helper.INavigationTarget.CanNavigate(Type entityType, string entityId)
    {
        if (string.IsNullOrEmpty(entityId)) return false;
        return Tabs.Any(t => t.EntityType == entityType);
    }

    void Helper.INavigationTarget.NavigateTo(Type entityType, string entityId)
    {
        NavigateToEntityByEntityId(entityType, entityId);
    }

    int Helper.INavigationTarget.Priority => 50;


}

