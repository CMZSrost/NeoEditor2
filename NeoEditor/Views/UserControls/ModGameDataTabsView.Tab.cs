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
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
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
        return DataTabs?.SelectedItem as GameDataTypeTabItem;
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
        GenericDataGridHelper.SetActiveStores(mergeStore, editStore);
        SharedDataGrid.EditedEntityIds = new HashSet<string>(editedCells.Select(c => c.EntityId));
        SharedDataGrid.OverriddenEntityIds = new HashSet<string>(overridden);
        SharedDataGrid.NewEntityIds = new HashSet<string>(newIds);
    }

    private object? GetSelectedItemFromActiveGrid()
    {
        // SelectedContent is the data item (GameDataTypeTabItem), not a Control.
        // Walk the visual tree from DataTabs to find the active DataGrid.
        var dataGrid = FindActiveDataGrid();
        return dataGrid?.SelectedItem;
    }

    private bool _loadPending;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ModInfoProperty && ModInfo is not null)
        {
            // Skip reload if we already have tabs for this same mod
            if (change.OldValue is ModInfo oldMod && oldMod.ModId == ModInfo.ModId && Tabs.Count > 0)
                return;
            IsMergeView = false;
            _loadPending = true;
            if (this.IsAttachedToVisualTree())
                AsyncHelper.FireAndForget(ReloadTabsAsync(ModInfo));
        }
        else if (change.Property == ProfileInfoProperty && ProfileInfo is not null)
        {
            // Skip reload if we already have tabs for this same profile
            if (change.OldValue is ProfileInfo oldProfile && oldProfile.ProfileId == ProfileInfo.ProfileId && Tabs.Count > 0)
                return;
            IsMergeView = true;
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

    private void OnColumnManagerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dataGrid = FindActiveDataGrid();
        if (dataGrid is null) return;

        var currentTab = GetActiveTab();
        var tableName = currentTab?.EntityType.GetCustomAttribute<TableAttribute>()?.Name;

        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = Loc["ColumnManagerHeader"], IsEnabled = false });
        menu.Items.Add(new Separator());

        foreach (var col in dataGrid.Columns)
        {
            var headerText = GetColumnHeaderText(col);
            if (string.IsNullOrEmpty(headerText)) continue;

            var checkBox = new CheckBox
            {
                Content = headerText,
                IsChecked = col.IsVisible
            };
            var capturedCol = col;
            var capturedTable = tableName;
            var capturedType = currentTab?.EntityType;
            var propName = capturedCol.SortMemberPath;
            checkBox.IsCheckedChanged += (_, _) =>
            {
                var visible = checkBox.IsChecked == true;
                capturedCol.IsVisible = visible;
                if (!string.IsNullOrEmpty(capturedTable) && !string.IsNullOrEmpty(propName))
                    ToggleColumnVisibility(capturedTable, capturedType, propName, visible);
            };
            menu.Items.Add(new MenuItem { Header = checkBox });
        }

        if (sender is Control c)
            menu.Open(c);
    }

    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        var innerGrid = SharedDataGrid.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
        innerGrid?.CommitEdit(DataGridEditingUnit.Row, true);

        if (DataTabs.SelectedItem is GameDataTypeTabItem tab)
        {
            SharedDataGrid.DataContext = tab;
            SwitchTabItemsSource(SharedDataGrid, tab.ItemsSource);
            if (innerGrid is not null && IsMergeView)
            {
                innerGrid.SelectionChanged -= OnDataGridSelectionChanged;
                innerGrid.SelectionChanged += OnDataGridSelectionChanged;
            }
        }
    }

    /// <summary>
    /// Switch the shared DataGrid to a new ItemsSource without crashing.
    /// Disables AutoGenerateColumns before the switch to prevent Avalonia's
    /// internal RemoveAutoGeneratedColumns from NRE-ing on large datasets.
    /// </summary>
    internal static void SwitchTabItemsSource(SearchableDataGrid sdg, IEnumerable newItems)
    {
        var innerGrid = sdg.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
        var newCount = newItems?.Cast<object>().Count() ?? 0;
        if (innerGrid is null)
        {
            Serilog.Log.Logger.Warning("[SwitchTab] no innerGrid, direct set (count={Count})", newCount);
            sdg.ItemsSource = newItems;
            return;
        }

        try
        {
            // Prevent Avalonia's RemoveAutoGeneratedColumns from NRE-ing:
            // disable auto-gen → set ItemsSource → defer re-enable to Loaded priority,
            // so the DataGrid internal state stabilizes before column regeneration.
            innerGrid.AutoGenerateColumns = false;
            sdg.ItemsSource = newItems;
            var actualCount = (sdg.ItemsSource as IEnumerable)?.Cast<object>().Count() ?? 0;
            Serilog.Log.Logger.Information("[SwitchTab] OK: setter succeeded, expected={Exp} actual={Act}, cols={Cols}",
                newCount, actualCount, innerGrid.Columns.Count);
            // Defer AutoGenerateColumns=true to avoid set_AutoGenerateColumns → OnAutoGeneratedColumnsChanged NRE
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try { innerGrid.AutoGenerateColumns = true; } catch { }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            Serilog.Log.Logger.Warning(ex, "[SwitchTab] FAIL: setter threw for {Count} items, cols={Cols}",
                newCount, innerGrid.Columns.Count);
            // Recovery: fully reset the DataGrid, then re-apply ItemsSource
            try { innerGrid.AutoGenerateColumns = false; } catch { }
            try { innerGrid.ItemsSource = null; } catch { }
            try { innerGrid.Columns.Clear(); } catch { }
            try
            {
                sdg.ItemsSource = newItems;
                var recoveredCount = (sdg.ItemsSource as IEnumerable)?.Cast<object>().Count() ?? 0;
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try { innerGrid.AutoGenerateColumns = true; } catch { }
                }, Avalonia.Threading.DispatcherPriority.Loaded);
                Serilog.Log.Logger.Information("[SwitchTab] Recovery OK: items={Count} cols={Cols}",
                    recoveredCount, innerGrid.Columns.Count);
            }
            catch (Exception ex2)
            {
                Serilog.Log.Logger.Error(ex2, "[SwitchTab] Recovery also failed for {Count} items", newCount);
            }
        }
    }

    private void OnDataGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is IEntity entity)
        {
            var entityType = entity.GetType().Name;
            _messenger.Send(new OverlayChainRequestedMessage(entity.EntityId, entity.Subject, entityType));
            _messenger.Send(new VisualEditorRequestedMessage(entity.GetType(), entity));
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

    /// <summary>Returns true when a cell-editing TextBox has keyboard focus, so global copy/paste is suppressed.</summary>
    private static bool IsEditingTextBoxFocused(object? topLevel)
    {
        var focused = (topLevel as TopLevel)?.FocusManager?.GetFocusedElement();
        return focused is TextBox;
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
        WeakReferenceMessenger.Default
            .Send(new Data.Messages.ColumnVisibilityChangedMessage { TableName = tableName });
    }

    private static string GetColumnHeaderText(DataGridColumn col)
    {
        if (col.Header is StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
                    return tb.Text;
            }
        }
        if (col.Header is TextBlock t)
            return t.Text ?? "";
        return col.Header?.ToString() ?? col.SortMemberPath ?? "?";
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

        // Global key handler for Ctrl+F/H — works regardless of focus
        var topLevel = TopLevel.GetTopLevel(this);
        topLevel?.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, OnGlobalKeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        if (!_loadPending)
        {
            // Re-attach without reload: push current state to grid + force background refresh.
            PushEditStateToGrid(MergeStore, EditStore);
            Dispatcher.UIThread.Post(() => SharedDataGrid.RefreshRowBackgrounds(), DispatcherPriority.Loaded);
            Dispatcher.UIThread.Post(UpdatePersistenceDebugInfo, DispatcherPriority.Loaded);
            return;
        }
        _loadPending = false;

        var cacheKey = IsMergeView ? $"profile_{ProfileInfo?.ProfileId}" : $"mod_{ModInfo?.ModId}";

        // Restore from cache when available — avoids full DB reload on tab switch.
        // Works for both single-mod and merge views. Cache stores Tabs + MergeStore + EditStore.
        if (TabSnapshotCache.TryGetValue(cacheKey, out var cached))
        {
            foreach (var tab in cached.Tabs)
                Tabs.Add(tab);
            _logger.LogInformation("[Attach] cache hit, replacing stores  oldES={OldES:x}→newES={NewES:x} oldMS={OldMS:x}→newMS={NewMS:x}",
                EditStore.GetHashCode(), cached.EditStore.GetHashCode(),
                MergeStore.GetHashCode(), cached.MergeStore.GetHashCode());
            // Replace field stores with cached ones — so ALL code paths use the same stores.
            EditStore = cached.EditStore;
            MergeStore = cached.MergeStore;
            _overriddenEntityIds = new HashSet<string>(MergeStore.OverriddenEntityIds);
            _isDirty = GenericDataGridHelper.EditedCells.Count > 0
                       || GenericDataGridHelper.NewEntityIds.Count > 0;
            PushEditStateToGrid(MergeStore, EditStore);
            IsLoading = false;
            RebuildFilteredItemsSources();
            if (Tabs.Count > 0 && DataTabs.SelectedIndex < 0)
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
                if (IsMergeView && ProfileInfo is not null)
                    await ReloadMergeTabsAsync(ProfileInfo);
                else if (ModInfo is not null)
                    await ReloadTabsAsync(ModInfo);
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

