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

using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data;
using IXmlParser = NeoEditor.Core.Abstractions.IXmlParser;
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
using NeoEditor.Plugins.DataViewer.Services;

namespace NeoEditor.Views.UserControls;

public partial class ModGameDataTabsView : UserControl, Helper.INavigationTarget
{
    // Cache tabs + store snapshot per view key (profile_N or mod_N)
    private static readonly Dictionary<string, (ObservableCollection<GameDataTypeTabItem> Tabs,
            EntityMergeStore MergeStore, EditTrackingStore EditStore)>
        TabSnapshotCache = new();

    private readonly IConfigService _configService;
    private readonly IDbContextFactory<GameDbContext> _gameDbContextFactory;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly ILogger<ModGameDataTabsView> _logger;
    private readonly IXmlParser _xmlParser;
    private int _loadVersion;
    private bool _isSavePreviewOpen;
    private readonly Stack<(Type EntityType, int Id)> _navHistory = new();
    private bool _isNavigatingBack;
    private readonly Dictionary<IEntity, List<(string ModName, int Id)>> _overlayChains = new();
    private HashSet<string> _overriddenEntityIds = new();
    private Dictionary<IEntity, int> _entityLoadIndex = new();

    // ── ViewModel (N03 fix: CommandHistory + WAL + dirty state owned by VM) ──
    private readonly DataTableViewModel _vm;
    private readonly NeoEditor.Core.Abstractions.IHostService _hostService;
    private readonly string _scopeId;
    private CommandHistory _commandHistory => _vm.CommandHistory;

    private int _persistSequence
    {
        get => _vm.PersistSequence;
        set => _vm.PersistSequence = value;
    }

    private int _commandsSinceSnapshot
    {
        get => _vm.CommandsSinceSnapshot;
        set => _vm.CommandsSinceSnapshot = value;
    }

    private bool _isDirty
    {
        get => _vm.IsViewDirty;
        set => _vm.SetDirty(value);
    }

    // M9: State ownership moved to VM. View delegates via properties.
    internal Services.EntityMergeStore MergeStore => _vm.MergeStore;
    internal Services.EditTrackingStore EditStore => _vm.EditStore;
    internal ObservableCollection<GameDataTypeTabItem> Tabs => _vm.Tabs;
    private readonly Helper.INavigationRouter _navigationRouter;
    private readonly NeoEditor.Plugins.DataViewer.Services.DataLoaderService _dataLoader;
    private readonly IFilterService _filterService = new FilterService();
    private readonly IWorkspacePersistenceService _workspacePersistence;
    private readonly IProfileManager _profileManager;
    private readonly IModManager _modManager;
    private readonly IMergeService _mergeService;
    private readonly DataGridInteractionState _dataGridState;
    private readonly IDataGridCellInteractionService _dataGridCellInteraction;
    private readonly IDataGridNavigationService _dataGridNavigationService;

    public static readonly StyledProperty<bool> CanUndoProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(CanUndo));

    public static readonly StyledProperty<bool> CanRedoProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(CanRedo));

    public bool CanUndo
    {
        get => GetValue(CanUndoProperty);
        private set => SetValue(CanUndoProperty, value);
    }

    public bool CanRedo
    {
        get => GetValue(CanRedoProperty);
        private set => SetValue(CanRedoProperty, value);
    }

    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>("ReadOnly");

    public static readonly StyledProperty<ProfileInfo?> ProfileInfoProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, ProfileInfo?>(nameof(ProfileInfo));

    public static readonly StyledProperty<bool> IsValueEditorVisibleProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(IsValueEditorVisible), true);

    public static readonly StyledProperty<bool> IsPreparingSavePreviewProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(IsPreparingSavePreview));

    public static readonly StyledProperty<bool> CanStartSavePreviewProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(CanStartSavePreview), true);

    public static readonly StyledProperty<string> SavePreviewButtonTextProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, string>(nameof(SavePreviewButtonText), string.Empty);

    public static readonly StyledProperty<string?> SavePreviewStatusTextProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, string?>(nameof(SavePreviewStatusText));

    public static readonly StyledProperty<bool> CanAddRowProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(CanAddRow), true);

    public static readonly StyledProperty<bool> CanDeleteRowProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(CanDeleteRow), true);

    public static readonly StyledProperty<string?> FilterTextProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, string?>(nameof(FilterText));

    private int? _selectedModId; // null = All
    private CancellationTokenSource? _filterCts;
    private static readonly Dictionary<Type, PropertyInfo[]> _colPropsCache = new();

    private static PropertyInfo[] GetColumnPropertiesCached(Type entityType)
    {
        if (!_colPropsCache.TryGetValue(entityType, out var props))
        {
            props = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.DeclaringType != typeof(IEntity)
                            && p.GetCustomAttribute<ColumnAttribute>() != null)
                .OrderBy(p => p.MetadataToken)
                .ToArray();
            _colPropsCache[entityType] = props;
        }

        return props;
    }

    public string? FilterText
    {
        get => GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    public ProfileInfo? ProfileInfo
    {
        get => GetValue(ProfileInfoProperty);
        set => SetValue(ProfileInfoProperty, value);
    }

    // M9: Tabs moved to VM (see internal Tabs => _vm.Tabs property above)
    public ILocalizationService Loc { get; set; }

    public bool IsValueEditorVisible
    {
        get => GetValue(IsValueEditorVisibleProperty);
        set => SetValue(IsValueEditorVisibleProperty, value);
    }

    public bool IsPreparingSavePreview
    {
        get => GetValue(IsPreparingSavePreviewProperty);
        private set => SetValue(IsPreparingSavePreviewProperty, value);
    }

    public bool CanStartSavePreview
    {
        get => GetValue(CanStartSavePreviewProperty);
        private set => SetValue(CanStartSavePreviewProperty, value);
    }

    public string SavePreviewButtonText
    {
        get => GetValue(SavePreviewButtonTextProperty);
        private set => SetValue(SavePreviewButtonTextProperty, value);
    }

    public string? SavePreviewStatusText
    {
        get => GetValue(SavePreviewStatusTextProperty);
        private set => SetValue(SavePreviewStatusTextProperty, value);
    }

    public bool ReadOnly
    {
        get { return GetValue(ReadOnlyProperty); }
        set { SetValue(ReadOnlyProperty, value); }
    }

    public bool CanAddRow
    {
        get => GetValue(CanAddRowProperty);
        private set => SetValue(CanAddRowProperty, value);
    }

    public bool CanDeleteRow
    {
        get => GetValue(CanDeleteRowProperty);
        private set => SetValue(CanDeleteRowProperty, value);
    }

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(IsLoading));

    public bool IsViewDirty => _isDirty;
    private readonly IMessenger _messenger;
    private IWorkspaceSession? _workspaceSession;
    private IWorkspaceSession WorkspaceSession => _workspaceSession!;

    private IReferenceResolver? _referenceResolver;
    private IReferenceResolver ReferenceResolver => _referenceResolver!;

    private void SetDirty(bool dirty)
    {
        _vm.SetDirty(dirty); // N03: dirty state owned by ViewModel

        if (Tabs.Count > 0)
        {
            var cacheKey = $"profile_{ProfileInfo?.ProfileId}";
            if (dirty)
                TabSnapshotCache[cacheKey] = (Tabs, MergeStore, EditStore);
            else
                TabSnapshotCache.Remove(cacheKey); // clean after save: allow fresh reload next open
        }
    }

    public static readonly StyledProperty<bool> CanNavigateBackProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(CanNavigateBack));

    public static readonly StyledProperty<bool> ShowAllEntitiesProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(ShowAllEntities));

    public static readonly DirectProperty<ModGameDataTabsView, bool> IsMergeViewProperty =
        AvaloniaProperty.RegisterDirect<ModGameDataTabsView, bool>(nameof(IsMergeView),
            o => o.IsMergeView);

    private bool _isMergeView = true; // B4: single-mod view removed — the view is always profile-shaped

    public bool IsMergeView
    {
        get => _isMergeView;
        private set => SetAndRaise(IsMergeViewProperty, ref _isMergeView, value);
    }

    public static readonly StyledProperty<bool> IsBrowseModeProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(IsBrowseMode));

    public bool IsBrowseMode
    {
        get => GetValue(IsBrowseModeProperty);
        private set => SetValue(IsBrowseModeProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        private set => SetValue(IsLoadingProperty, value);
    }

    public bool CanNavigateBack
    {
        get => GetValue(CanNavigateBackProperty);
        private set => SetValue(CanNavigateBackProperty, value);
    }

    public bool ShowAllEntities
    {
        get => GetValue(ShowAllEntitiesProperty);
        set
        {
            SetValue(ShowAllEntitiesProperty, value);
            _logger.LogInformation("[ShowAllEntities] toggled to {Value}", value);
            RebuildFilteredItemsSources();
        }
    }

    public ModGameDataTabsView()
    {
        _configService = ViewServices.ConfigService;
        _gameDbContextFactory = ViewServices.GameDbFactory;
        _editorDbFactory = ViewServices.EditorDbFactory;
        Loc = ViewServices.Loc;
        _logger = ViewServices.LoggerFactory.CreateLogger<ModGameDataTabsView>();
        _xmlParser = ViewServices.XmlParser;
        _workspacePersistence = ViewServices.WorkspacePersistence;
        _messenger = WeakReferenceMessenger.Default;
        _navigationRouter = ViewServices.NavigationRouter;
        _profileManager = ViewServices.ProfileManager;
        _modManager = ViewServices.ModManager;
        _mergeService = ViewServices.MergeService;
        _workspaceSession = ViewServices.WorkspaceSession;
        _referenceResolver = ViewServices.ReferenceResolver;
        _dataGridState = ViewServices.Get<DataGridInteractionState>();
        _dataGridCellInteraction = ViewServices.Get<IDataGridCellInteractionService>();
        _dataGridNavigationService = ViewServices.Get<IDataGridNavigationService>();
        _hostService = ViewServices.HostService;
        _scopeId = $"mgdt_{Guid.NewGuid():N}";

        // ── Dirty state: subscribe to the single source of truth ──
        _workspaceSession.DirtyStateChanged += (_, _) => SyncDirtyViewState();

        // ── Create ViewModel (N03 fix: owns CommandHistory + WAL + dirty state) ──
        _vm = new DataTableViewModel(_workspacePersistence, _configService, _logger, _messenger);
        _hostService.RegisterCommandScope(_scopeId, _vm.CommandHistory);
        _vm.DataLoader = _dataLoader;
        _vm.GetProfileInfo = () => ProfileInfo;
        _vm.GetPersistenceTarget = GetPersistenceTarget;
        _vm.CaptureAllEntities = CaptureCurrentTabEntities;
        _vm.OnMarkTabDirty = entityType =>
        {
            var tab = Tabs.FirstOrDefault(t => t.EntityType == entityType);
            if (tab is not null)
            {
                _dirtyTabs.Add(tab);
                tab.MarkDirty();
            }
        };
        _vm.OnRefreshDataGrid = RefreshActiveDataGrid;
        _vm.OnPushEditState = () => PushEditStateToGrid(MergeStore, EditStore);
        _vm.OnRebuildFilteredSources = RebuildFilteredItemsSources;
        _vm.OnClearDirtyTabsUi = () =>
        {
            foreach (var tab in _dirtyTabs) tab.ClearDirty();
            _dirtyTabs.Clear();
        };
        _vm.OnMarkSessionEntitiesDirty = ids => WorkspaceSession.MarkEntitiesDirty(ids);
        _vm.SaveRequested += async scope => await QuickSaveAsync(scope);
        // Bridge VM CanUndo/CanRedo → View StyledProperties
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DataTableViewModel.CanUndo))
                CanUndo = _vm.CanUndo;
            else if (e.PropertyName == nameof(DataTableViewModel.CanRedo))
                CanRedo = _vm.CanRedo;
        };

        InitializeComponent();
        PersistenceDebugText = "Init...";

        // ── Wire DataViewer Plugin services to SharedDataGrid (Phase 1: GDH/ViewServices decoupling) ──
        _dataLoader = ViewServices.Get<NeoEditor.Plugins.DataViewer.Services.DataLoaderService>();
        var dataTableService = ViewServices.Get<DataTableService>();
        var columnTemplateFactory = ViewServices.Get<ColumnTemplateFactory>();
        columnTemplateFactory.Localizer = key => Loc[key] ?? key;
        columnTemplateFactory.Messenger = _messenger;
        SharedDataGrid.Loc = Loc;
        SharedDataGrid.LoggerFactory = ViewServices.LoggerFactory;
        SharedDataGrid.ConfigService = _configService;
        SharedDataGrid.Messenger = _messenger;
        SharedDataGrid.DataTable = dataTableService;
        SharedDataGrid.ColumnTemplateFactory = columnTemplateFactory;
        SharedDataGrid.InteractionHandler = ViewServices.Get<InteractionHandler>();
        SharedDataGrid.DataGridState = _dataGridState;
        SharedDataGrid.CellInteraction = _dataGridCellInteraction;
        SharedDataGrid.DataGridNavigation = _dataGridNavigationService;
        SharedDataGrid.SelectionService = ViewServices.SelectionService;
        SharedDataGrid.InitializeServices();

        SharedDataGrid.CanEditEntity = entity => !IsMergeView || entity.ModId != -1;
        SharedDataGrid.OnEditBlocked = _ =>
            ViewServices.Notification.ShowInfo(
                Loc["GameDataReadOnlyMessage"],
                Loc["GameDataReadOnly"]);
        IsLoading = true;
        UpdateSavePreviewUiState();
        _messenger.Register<CellEditedMessage>(this, (_, m) =>
        {
            if (ReadOnly) return;
            MarkTabDirty(m.EntityType);
        });

        _messenger.Register<CellEditCommittedMessage>(this,
            (_, m) => OnCellEditCommitted(m.Entity, m.PropertyName, m.OldValue, m.NewValue));
        _messenger.Register<EntityFieldEditsMessage>(this, (_, m) => OnEntityFieldEditsFromXml(m));
        _messenger.Register<CloneRowRequestedMessage>(this, (_, m) => OnCloneRowRequested(m.Entity));
        _messenger.Register<FindReferencesRequestedMessage>(this, (_, m) => OnFindReferencesRequested(m.Entity));

        // Workspace toolbar CRUD — only respond when NOT read-only (i.e., in Center, not Bottom)
        _messenger.Register<CreateEntityRequestedMessage>(this, (_, _) =>
        {
            if (ReadOnly) return;
            AsyncHelper.FireAndForget(AddOrCloneEntityAsync(copyFrom: null));
        });
        _messenger.Register<CopyEntityRequestedMessage>(this, (_, _) =>
        {
            if (ReadOnly) return;
            var selected = GetSelectedItemFromActiveGrid();
            if (selected is IEntity sourceEntity)
                AsyncHelper.FireAndForget(AddOrCloneEntityAsync(copyFrom: sourceEntity, skipDialog: true));
        });
        _messenger.Register<DeleteEntityRequestedMessage>(this, (_, _) =>
        {
            if (ReadOnly) return;
            var selected = GetSelectedItemFromActiveGrid();
            if (selected is IEntity) OnDeleteRowButtonClick(null, null!);
        });

        // When KV editor modifies a field, refresh the DataGrid so cells show the new value.
        // EF entities don't implement INotifyPropertyChanged, so a manual refresh is needed.
        // Also set dirty state so SaveRequestedMessage actually triggers persistence (R09/R11).
        // NOTE: Must be synchronous — Background dispatch causes race condition where
        // _isDirty is still false when SaveRequestedMessage checks it, skipping the save.
        _messenger.Register<RefreshEntityEditorMessage>(this, (_, m) =>
        {
            if (ReadOnly) return;
            MarkTabDirty(m.Entity.GetType());
            RefreshActiveDataGrid();
        });

        TabListBox.SelectionChanged += OnTabChanged;

        // Per-document save boundary fix: when EntityEditorDocument.SaveDocument() persists
        // a single entity to game.db, update the WAL snapshot marker for that mod so the
        // entity's commands won't be replayed (and re-mark it dirty) on restart.
        _messenger.Register<EntityDbSavedMessage>(this, (_, m) =>
        {
            if (m.ModId <= 0) return;
            _logger.LogInformation("[EntityDbSaved] updating snapshot for mod:{ModId} seq={Seq}",
                m.ModId, _persistSequence);
            AsyncHelper.FireAndForget(
                _workspacePersistence.UpdateSnapshotMarkerAsync("mod", m.ModId, _persistSequence));
        });

        // ── Activate ViewModel (auto-save timer, message handlers) ──
        _vm.IsReadOnly = () => ReadOnly;
        _vm.Initialize();
    }

    private void OnCellEditCommitted(IEntity entity, string propertyName,
        object? oldValue, object? newValue)
    {
        // Block edits on game data (ModId=-1) in merge view
        if (IsMergeView && entity.ModId == -1)
        {
            ViewServices.Notification.ShowInfo(
                Loc["GameDataReadOnlyMessage"],
                Loc["GameDataReadOnly"]);
            return;
        }

        var prop = entity.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (prop is null) return;

        var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
        var colName = colAttr?.Name ?? propertyName;

        var cmd = new EditCellCommand(entity, prop, colName, oldValue, newValue,
            () => SetDirty(true));
        AsyncHelper.FireAndForget(_hostService.ExecuteAsync(cmd, _scopeId));

        // Populate per-view EditStore so PushEditStateToGrid sees this edit.
        EditStore.EditedCells.Add((entity.EntityId, colName));
    }

    /// <summary>Handle XML-tab edits routed from EntityEditorDocument.ApplyXmlToEntity.
    /// Routes through _commandHistory so changes are persisted to command_log (WAL)
    /// and survive editor restart. Does NOT check ReadOnly — that flag controls
    /// UI-triggered CRUD only; Center XML edits must always persist.</summary>
    private void OnEntityFieldEditsFromXml(EntityFieldEditsMessage m)
    {
        _logger.LogInformation("[XmlEdit→WAL] received {Count} edits for entity {Eid} (IsMergeView={Mv} ModId={Mid})",
            m.Edits.Count, m.Entity.EntityId, IsMergeView, m.Entity.ModId);

        // Allow XML/KV edits on game data (ModId=-1) in merge view.
        // Previously skipped here, causing silent data loss on restart (Test Round 9 data loss).
        // UI-level blocking (DataGrid CanEditEntity) still prevents cell edits on game data.
        if (m.Edits.Count == 0) return;

        var cmd = new BatchEditCommand(m.Edits.ToList(), () => SetDirty(true));
        _logger.LogInformation("[XmlEdit→WAL] executing BatchEditCommand ({Count} edits) via HostService",
            m.Edits.Count);
        AsyncHelper.FireAndForget(_hostService.ExecuteAsync(cmd, _scopeId));

        // Populate per-view EditStore so PushEditStateToGrid (called by MarkTabDirty)
        // can see the affected entities and create correct EditedEntityIds for row highlighting.
        foreach (var eid in cmd.GetAffectedEntityIds())
            EditStore.EditedCells.Add((eid, "*"));

        MarkTabDirty(m.Entity.GetType());
        // R06: refresh DataGrid cells so edited values are visible immediately.
        RefreshActiveDataGrid();
    }

    private async void OnUndoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _hostService.UndoAsync(_scopeId);
        RefreshActiveDataGrid();
    }

    private async void OnRedoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _hostService.RedoAsync(_scopeId);
        RefreshActiveDataGrid();
    }

    /// <summary>Force the active DataGrid to re-read all cell values after undo/redo.</summary>
    private void RefreshActiveDataGrid()
    {
        var dataGrid = FindActiveDataGrid();
        if (dataGrid?.ItemsSource is null) return;
        var src = dataGrid.ItemsSource;
        dataGrid.ItemsSource = null;
        dataGrid.ItemsSource = src;
    }

    private readonly HashSet<GameDataTypeTabItem> _dirtyTabs = [];

    private void MarkTabDirty(Type entityType)
    {
        var tab = Tabs.FirstOrDefault(t => t.EntityType == entityType);
        if (tab is not null)
        {
            _dirtyTabs.Add(tab);
            tab.MarkDirty();
            SetDirty(true);
        }

        PushEditStateToGrid(MergeStore, EditStore);
    }

    private void ClearDirtyTabs()
    {
        foreach (var tab in _dirtyTabs)
            tab.ClearDirty();
        _dirtyTabs.Clear();
    }

    /// <summary>After WAL restore, mark all tabs that contain edited entities as dirty
    /// and populate IWorkspaceSession.DirtyEntities so EntityEditorDocument can check.</summary>
    private void MarkTabsDirtyFromEditedCells()
    {
        var editedEntityIds = new HashSet<string>(EditStore.EditedCells.Select(c => c.EntityId));
        if (editedEntityIds.Count == 0) return;

        foreach (var tab in Tabs)
        {
            foreach (var item in tab.SourceCollection)
            {
                if (item is IEntity e && editedEntityIds.Contains(e.EntityId))
                {
                    _dirtyTabs.Add(tab);
                    tab.MarkDirty();
                    break;
                }
            }
        }

        // Populate session DirtyEntities so EntityEditorDocument knows entities are unsaved
        WorkspaceSession.MarkEntitiesDirty(editedEntityIds);

        _logger.LogInformation("[MarkTabsDirtyFromEditedCells] {EntityCount} edited entities across {TabCount} tabs",
            editedEntityIds.Count, _dirtyTabs.Count);
    }

    /// <summary>
    /// Re-derive ALL dirty UI state from IWorkspaceSession.DirtyEntities (the single source of truth).
    /// Called when DirtyStateChanged fires, ensuring tab titles, DataGrid rows, and VM dirty flag
    /// all stay consistent regardless of which code path modifies DirtyEntities.
    /// </summary>
    private void SyncDirtyViewState()
    {
        var dirtyIds = WorkspaceSession.DirtyEntities;
        var hasDirty = dirtyIds.Count > 0;

        // ── Sync tab dirty indicators ──
        foreach (var tab in Tabs)
        {
            var tabHasDirty = tab.SourceCollection?.OfType<IEntity>().Any(e => dirtyIds.Contains(e.EntityId)) == true;
            if (tabHasDirty)
            {
                if (!_dirtyTabs.Contains(tab))
                {
                    _dirtyTabs.Add(tab);
                    tab.MarkDirty();
                }
            }
            else
            {
                if (_dirtyTabs.Contains(tab))
                {
                    _dirtyTabs.Remove(tab);
                    tab.ClearDirty();
                }
            }
        }

        // ── Sync VM dirty flag ──
        SetDirty(hasDirty);

        // ── Sync DataGrid row highlighting ──
        PushEditStateToGrid(MergeStore, EditStore);
    }


    private void ShowOverlayChain(IEntity entity)
    {
        // Try display strings first, then raw chain data
        if (MergeStore.OverlayChainDisplay.TryGetValue(entity.EntityId, out var displayChain) && displayChain.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Overlay Chain (load order → current):");
            for (var i = 0; i < displayChain.Count; i++)
            {
                var prefix = i == displayChain.Count - 1 ? "→" : "  ";
                sb.AppendLine($"{prefix}{displayChain[i]}");
            }

            ViewServices.Notification.ShowInfo(sb.ToString(), "Overlay Chain");
            return;
        }

        if (_overlayChains.TryGetValue(entity, out var chain) && chain.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Overlay Chain (load order → current):");
            for (var i = 0; i < chain.Count; i++)
            {
                var prefix = i == chain.Count - 1 ? "→" : "  ";
                sb.AppendLine($"{prefix} [{chain[i].ModName}] id={chain[i].Id}");
            }

            ViewServices.Notification.ShowInfo(sb.ToString(), "Overlay Chain");
            return;
        }

        ViewServices.Notification.ShowInfo("No overlay data for this row.");
    }

    public void NavigateToEntity(Type entityType, int id)
    {
        NavigateToEntityImpl(entityType, entityId: null, businessId: id);
    }

    public void NavigateToEntityByEntityId(Type entityType, string entityId)
    {
        NavigateToEntityImpl(entityType, entityId, businessId: null);
    }

    private void NavigateToEntityImpl(Type entityType, string? entityId, int? businessId)
    {
        var searchMode = entityId is not null ? "entityId" : "id";
        var searchValue = entityId ?? businessId?.ToString() ?? "?";
        _logger.LogInformation("[Navigate] type={EntityType} {SearchMode}={SearchValue}",
            entityType.Name, searchMode, searchValue);

        // Check for overridden target in non-ShowAll mode before searching the DataGrid
        // (the CV filter hides overridden entities, so the search would fail silently)
        if (!ShowAllEntities && entityId is not null
                             && MergeStore.OverriddenEntityIds.Contains(entityId))
        {
            _logger.LogInformation("[Navigate] blocked — target is overridden and ShowAll is off");
            ViewServices.Notification.ShowInfo(Loc["NavigateToOverriddenRequiresShowAll"], "Navigate");
            return;
        }

        // Save current position for back-navigation (unless we're going back)
        if (!_isNavigatingBack)
        {
            var currentTab = GetActiveTab();
            if (currentTab is not null)
            {
                var currentId = GetSelectedEntityId();
                if (currentId.HasValue)
                {
                    _navHistory.Push((currentTab.EntityType, currentId.Value));
                    CanNavigateBack = true;
                }
            }
        }

        var targetTab = Tabs.FirstOrDefault(t => t.EntityType == entityType);
        if (targetTab is null)
        {
            _logger.LogDebug("[NavigateToEntity] targetTab not found for {entityType.Name}");
            ViewServices.Notification.ShowInfo(Loc["RefTargetNotLoaded", entityType.Name]);
            return;
        }

        _logger.LogInformation(
            "[NavigateToEntity] switching to tab={TabHeader}, entityType={EntityType} {SearchMode}={SearchValue}",
            targetTab.Header, entityType.Name, searchMode, searchValue);
        TabListBox.SelectedItem = targetTab;

        // Wait for the tab content DataGrid to be created and loaded before searching.
        // Use Post with Loaded priority + a retry on Render priority as fallback.
        DoScrollToEntity(entityType, entityId, businessId, searchMode, searchValue, 0);
    }

    private void DoScrollToEntity(Type entityType, string? entityId, int? businessId,
        string searchMode, string searchValue, int attempt)
    {
        if (attempt > 3) return; // give up after 3 attempts

        var priority = attempt == 0 ? DispatcherPriority.Loaded : DispatcherPriority.Background;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // SharedDataGrid is the single DataGrid — always present.
                var dataGrid = SharedDataGrid?.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();

                if (dataGrid is null)
                {
                    if (attempt == 0)
                        DoScrollToEntity(entityType, entityId, businessId, searchMode, searchValue, attempt + 1);
                    else
                        _logger.LogWarning("[NavigateToEntity] no DataGrid found after {Attempt} attempts", attempt);
                    return;
                }

                var targetIndex = -1;
                var idx = 0;
                object? targetItem = null;

                var source = dataGrid.ItemsSource as IEnumerable ?? Enumerable.Empty<object>();
                var itemCount = source.Cast<object>().Count();

                _logger.LogInformation("[DoScroll] attempt={Attempt} itemCount={ItemCount} cols={Cols}",
                    attempt, itemCount, dataGrid.Columns.Count);

                // If the tab switch failed (0 items), retry the switch
                if (itemCount == 0 && attempt < 2)
                {
                    var tab = Tabs.FirstOrDefault(t => t.EntityType == entityType);
                    if (tab is not null)
                    {
                        _logger.LogInformation("[DoScroll] retrying switch for {EntityType}, tab items={TabCount}",
                            entityType.Name, tab.ItemsSource?.Cast<object>().Count() ?? -1);
                        try
                        {
                            SwitchTabItemsSource(SharedDataGrid, tab.ItemsSource);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[DoScroll] retry switch threw");
                            try
                            {
                                SharedDataGrid.ItemsSource = tab.ItemsSource;
                            }
                            catch (Exception exFallback)
                            {
                                _logger.LogDebug(exFallback, "[DoScroll] retry ItemsSource fallback failed");
                            }
                        }

                        source = dataGrid.ItemsSource as IEnumerable ?? Enumerable.Empty<object>();
                        itemCount = source.Cast<object>().Count();
                        _logger.LogInformation("[DoScroll] after retry: itemCount={ItemCount}", itemCount);
                    }
                }

                if (itemCount == 0)
                {
                    _logger.LogWarning("[NavigateToEntity] still 0 items after retry for {EntityType}, cols={Cols}",
                        entityType.Name, dataGrid.Columns.Count);
                    return;
                }

                foreach (var item in source)
                {
                    bool match;
                    if (entityId is not null)
                        match = item is IEntity e && e.EntityId == entityId;
                    else if (businessId.HasValue)
                    {
                        var keyProp = DataLoaderService.ResolveEntityKeyProperty(entityType);
                        var val = keyProp?.GetValue(item);
                        match = (val is int intVal && intVal == businessId.Value)
                                || val?.ToString() == businessId.Value.ToString();
                    }
                    else
                    {
                        match = false;
                    }

                    if (match)
                    {
                        targetItem = item;
                        targetIndex = idx;
                        break;
                    }

                    idx++;
                }

                if (targetItem is null)
                {
                    _logger.LogWarning(
                        "[NavigateToEntity] {SearchMode}={SearchValue} not found among {ItemCount} items (firstEid={FirstEid})",
                        searchMode, searchValue, idx,
                        source.Cast<object>().FirstOrDefault() is IEntity fe
                            ? fe.EntityId[..Math.Min(16, fe.EntityId.Length)]
                            : "none");
                    return;
                }

                if (ReferenceEquals(targetItem, dataGrid.SelectedItem))
                {
                    ViewServices.Notification.ShowInfo(Loc["NavigateSameEntity"], "Navigate");
                    return;
                }

                if (entityId is null && !ShowAllEntities && targetItem is IEntity te
                    && MergeStore.OverriddenEntityIds.Contains(te.EntityId))
                {
                    ViewServices.Notification.ShowInfo(Loc["NavigateToOverriddenRequiresShowAll"], "Navigate");
                    return;
                }

                dataGrid.SelectedItem = targetItem;
                dataGrid.SelectedIndex = targetIndex;
                dataGrid.ScrollIntoView(targetItem, null);

                Dispatcher.UIThread.Post(() =>
                {
                    var sv = dataGrid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                    if (sv is not null && sv.Viewport.Height > 0)
                    {
                        var rowH = dataGrid.RowHeight > 0 ? dataGrid.RowHeight : 30;
                        var ideal = targetIndex * rowH - sv.Viewport.Height / 2 + rowH / 2;
                        sv.Offset = new Avalonia.Vector(sv.Offset.X, Math.Max(0, ideal));
                    }
                }, DispatcherPriority.Background);

                dataGrid.Focus();
                _logger.LogInformation(
                    "[NavigateToEntity] ✓ Selected {EntityType} {SearchMode}={SearchValue} at index={TargetIndex}",
                    entityType.Name, searchMode, searchValue, targetIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NavigateToEntity] callback failed for {EntityType} {SearchMode}={SearchValue}",
                    entityType.Name, searchMode, searchValue);
            }
        }, priority);
    }


    private int? GetSelectedEntityId()
    {
        var tab = GetActiveTab();
        if (tab is null) return null;
        var item = GetSelectedItemFromActiveGrid();
        if (item is null) return null;
        var keyProp = DataLoaderService.ResolveEntityKeyProperty(tab.EntityType);
        var val = keyProp?.GetValue(item);
        return val as int?;
    }

    private bool _ctrlKeyDown;

    private void OnGlobalKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        _ctrlKeyDown = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);

        if (e.Key == Avalonia.Input.Key.Escape && FindPanel.IsOpen)
        {
            FindPanel.Hide();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Avalonia.Input.Key.F:
                    e.Handled = true;
                    ShowFindPanel(replaceMode: false);
                    break;
                case Avalonia.Input.Key.H:
                    e.Handled = true;
                    ShowFindPanel(replaceMode: true);
                    break;
                case Avalonia.Input.Key.Z:
                    if (!IsEditingTextBoxFocused(sender))
                    {
                        AsyncHelper.FireAndForget(_hostService.UndoAsync(_scopeId));
                        e.Handled = true;
                    }

                    break;
                case Avalonia.Input.Key.Y:
                    if (!IsEditingTextBoxFocused(sender))
                    {
                        AsyncHelper.FireAndForget(_hostService.RedoAsync(_scopeId));
                        e.Handled = true;
                    }

                    break;
                case Avalonia.Input.Key.S:
                    if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
                    {
                        e.Handled = true;
                        AsyncHelper.FireAndForget(OnSaveAndLaunchClickAsync(null, null!));
                    }
                    else
                    {
                        e.Handled = true;
                        AsyncHelper.FireAndForget(QuickSaveAsync());
                    }

                    break;
                case Avalonia.Input.Key.E:
                    e.Handled = true;
                    OnToggleValueEditorClick(null, null!);
                    break;
                case Avalonia.Input.Key.C:
                    if (IsEditingTextBoxFocused(sender)) break; // let TextBox handle native copy
                    e.Handled = true;
                    CopySelectedCells();
                    break;
                case Avalonia.Input.Key.V:
                    if (IsEditingTextBoxFocused(sender)) break; // let TextBox handle native paste
                    if (!ReadOnly)
                    {
                        e.Handled = true;
                        PasteCells();
                    }

                    break;
            }
        }
    }

    // Internal cell copy buffer — avoids system clipboard issues
    private string? _copyBuffer;

    private void CopySelectedCells()
    {
        var dataGrid = FindActiveDataGrid();
        if (dataGrid is null) return;

        var selectedItems = dataGrid.SelectedItems;
        if (selectedItems is null || selectedItems.Count == 0) return;

        var columns = dataGrid.Columns
            .Where(c => c.IsVisible && !string.IsNullOrEmpty(c.SortMemberPath))
            .ToList();
        if (columns.Count == 0) return;

        var tab = GetActiveTab();
        var colProps = tab is not null ? GetColumnPropertiesCached(tab.EntityType) : Array.Empty<PropertyInfo>();

        var sb = new StringBuilder();
        foreach (var row in selectedItems)
        {
            if (row is not IEntity entity) continue;
            var rowParts = new List<string>();
            foreach (var col in columns)
            {
                var prop = colProps.FirstOrDefault(p => p.Name == col.SortMemberPath);
                if (prop is not null)
                    rowParts.Add(prop.GetValue(entity)?.ToString() ?? "");
            }

            sb.AppendLine(string.Join("\t", rowParts));
        }

        _copyBuffer = sb.ToString().TrimEnd('\r', '\n');
    }

    private void PasteCells()
    {
        if (string.IsNullOrEmpty(_copyBuffer)) return;

        var dataGrid = FindActiveDataGrid();
        if (dataGrid is null) return;

        var selectedItems = dataGrid.SelectedItems;
        if (selectedItems is null || selectedItems.Count == 0) return;

        var targetEntity = selectedItems[0] as IEntity;
        if (targetEntity is null) return;

        var tab = GetActiveTab();
        if (tab is null) return;

        var colProps = GetColumnPropertiesCached(tab.EntityType);
        var columns = dataGrid.Columns
            .Where(c => c.IsVisible && !string.IsNullOrEmpty(c.SortMemberPath))
            .ToList();
        if (columns.Count == 0 || colProps.Length == 0) return;

        // Cell-to-cell: paste first line into first selected row
        var firstLine = _copyBuffer.Split('\n', 2, StringSplitOptions.TrimEntries)[0];
        var parts = firstLine.Split('\t');

        var edits = new List<EditRecord>();

        for (var colIdx = 0; colIdx < parts.Length && colIdx < columns.Count; colIdx++)
        {
            var prop = colProps.FirstOrDefault(p => p.Name == columns[colIdx].SortMemberPath);
            if (prop is null || !prop.CanWrite) continue;

            var rawValue = parts[colIdx].Trim();
            if (string.IsNullOrEmpty(rawValue)) continue;
            var oldValue = prop.GetValue(targetEntity);
            if (oldValue?.ToString() == rawValue) continue;

            object? newValue;
            try
            {
                newValue = ValueConverter.Convert(rawValue, prop.PropertyType);
            }
            catch
            {
                continue;
            }

            var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
            edits.Add(new EditRecord(targetEntity, prop, colName, oldValue, newValue));
        }

        if (edits.Count == 0) return;

        if (edits.Count == 1)
        {
            var edit = edits[0];
            AsyncHelper.FireAndForget(_hostService.ExecuteAsync(
                new EditCellCommand(edit.Entity, edit.Property, edit.ColumnName, edit.OldValue, edit.NewValue,
                    () => SetDirty(true)), _scopeId));
        }
        else
        {
            AsyncHelper.FireAndForget(_hostService.ExecuteAsync(
                new BatchEditCommand(edits, () => SetDirty(true)), _scopeId));
        }

        RefreshActiveDataGrid();
    }

    private void ShowFindPanel(bool replaceMode)
    {
        if (FindPanel.IsOpen && FindPanel.ReplaceMode == replaceMode)
        {
            FindPanel.Hide();
            return;
        }

        var dataGrid = FindActiveDataGrid();
        if (dataGrid is null) return;
        FindPanel.InjectedLoc = Loc;
        FindPanel.InjectedNotification = ViewServices.Notification;
        FindPanel.CommandHistory = _commandHistory;
        FindPanel.OnDirtyChanged = () => SetDirty(true);
        FindPanel.Show(dataGrid, replaceMode);
    }

    private void OnToggleShowAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowAllEntities = !ShowAllEntities;
        ShowAllToggle.IsChecked = ShowAllEntities;
    }

    public static readonly StyledProperty<bool> IsEmptyModProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(IsEmptyMod));

    public bool IsEmptyMod
    {
        get => GetValue(IsEmptyModProperty);
        private set => SetValue(IsEmptyModProperty, value);
    }

    private void RefreshIsEmptyMod()
    {
        IsEmptyMod = !IsLoading && Tabs.Count > 0
                                && Tabs.All(t => t.SourceCollection.Count == 0);
    }

    private async void OnCreateModFromBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = Views.Dialog.CreateModDialog.Create(ViewServices.Get<IServiceProvider>());
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            var result = await dialog.ShowDialog<Data.Model.ModInfo?>(owner);
            if (result is not null)
                WeakReferenceMessenger.Default
                    .Send(new Data.Messages.OpenModGameDataDocumentMessage(result));
        }
    }

    private void OnClearColumnFilterClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SharedDataGrid.ClearFilter();
    }

    private void OnLocateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var grid = FindActiveDataGrid();
        if (grid?.SelectedItem is not null)
        {
            grid.ScrollIntoView(grid.SelectedItem, null);
            grid.Focus();
        }
    }

    private void OnBackNavigationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_navHistory.Count == 0) return;
        var (entityType, id) = _navHistory.Pop();
        CanNavigateBack = _navHistory.Count > 0;
        _isNavigatingBack = true;
        NavigateToEntity(entityType, id);
        _isNavigatingBack = false;
    }

    private async void OnImportCsvClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var activeTab = GetActiveTab();
        if (activeTab is not { } tab || tab.EntityType is not { } entityType) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        // Avalonia 12: OpenFileDialog replaced by IStorageProvider
        var csvFileType = new FilePickerFileType("CSV Files")
        {
            Patterns = new[] { "*.csv" }
        };
        var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import CSV",
            AllowMultiple = false,
            FileTypeFilter = new[] { csvFileType }
        });
        if (result.Count == 0) return;
        var csvPath = result[0].TryGetLocalPath();
        if (csvPath is null) return;

        var lines = File.ReadAllLines(csvPath);
        if (lines.Length < 2) return;

        var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
        var colProps = entityType
            .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity)
                        && p.GetCustomAttribute<ColumnAttribute>() != null
                        && p.CanWrite)
            .ToList();

        // Map CSV columns to entity properties by Column attribute name
        var mappings = new List<(int CsvIdx, PropertyInfo Prop)>();
        foreach (var prop in colProps)
        {
            var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
            var csvIdx = Array.IndexOf(headers, colName);
            if (csvIdx >= 0) mappings.Add((csvIdx, prop));
        }

        if (mappings.Count == 0)
        {
            ViewServices.Notification.ShowWarning("No matching columns found in CSV header.", "Import CSV");
            return;
        }

        var keyProp = DataLoaderService.ResolveEntityKeyProperty(entityType);
        var imported = 0;
        for (var li = 1; li < lines.Length; li++)
        {
            var fields = ParseCsvLine(lines[li]);
            var newEntity = Activator.CreateInstance(entityType) as IEntity;
            if (newEntity is null) continue;

            // Use the current profile's single mod (or its first mod) for ModId and a default FilePath
            var firstModInfo = ProfileInfo?.ModLoadInfos.FirstOrDefault(m => m.Info is not null)?.Info;
            newEntity.ModId = ProfileInfo?.SingleModId ?? firstModInfo?.ModId ?? 1;
            newEntity.FilePath = Path.Combine(
                _configService.Config.GameRootDir, "Mods", firstModInfo?.Name ?? "import", "neogame.xml");
            newEntity.EntityId = $"import_{Guid.NewGuid():N}";

            foreach (var (csvIdx, prop) in mappings)
            {
                var raw = csvIdx < fields.Length ? fields[csvIdx] : "";
                var converted = ConvertValue(raw, prop.PropertyType);
                if (converted is not null)
                    prop.SetValue(newEntity, converted);
            }

            // Auto-increment ID
            if (keyProp != null)
            {
                var maxId = tab.SourceCollection.OfType<IEntity>()
                    .Select(item => keyProp.GetValue(item))
                    .OfType<int>()
                    .DefaultIfEmpty(0)
                    .Max();
                keyProp.SetValue(newEntity, maxId + 1);
            }

            tab.SourceCollection.Add(newEntity);
            EditStore.NewEntityIds.Add(newEntity.EntityId);
            imported++;
        }

        SetDirty(true);
        RebuildFilteredItemsSources();
        ViewServices.Notification.ShowSuccess($"Imported {imported} rows into {entityType.Name}.", "Import CSV");
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    private void OnExportCsvClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var activeTab = GetActiveTab();
        if (activeTab is null) return;

        var entityType = activeTab.EntityType;
        var entities = activeTab.SourceCollection.OfType<IEntity>().ToList();
        if (entities.Count == 0) return;

        var colProps = entityType
            .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity)
                        && p.GetCustomAttribute<ColumnAttribute>() != null)
            .OrderBy(p => p.MetadataToken)
            .ToList();

        var sb = new System.Text.StringBuilder();
        // Header
        sb.AppendLine(string.Join(",", colProps.Select(p =>
        {
            var name = p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name;
            return name.Contains(',') ? $"\"{name}\"" : name;
        })));

        // Rows
        foreach (var entity in entities)
        {
            sb.AppendLine(string.Join(",", colProps.Select(p =>
            {
                var val = p.GetValue(entity)?.ToString() ?? "";
                return val.Contains(',') || val.Contains('"') || val.Contains('\n')
                    ? $"\"{val.Replace("\"", "\"\"")}\""
                    : val;
            })));
        }

        var fileName = $"{entityType.Name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var savePath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), fileName);
        System.IO.File.WriteAllText(savePath, sb.ToString(), System.Text.Encoding.UTF8);
        ViewServices.Notification.ShowSuccess($"Exported {entities.Count} rows to {savePath}", "CSV Export");
    }

    #region Workspace Persistence

    private (string targetType, int targetId) GetPersistenceTarget()
    {
        // B4: single-mod profiles persist WAL per-mod (("mod", modId)) so edits survive restart.
        if (ProfileInfo is not null && ProfileInfo.SingleModId is int singleModId)
            return ("mod", singleModId);
        if (ProfileInfo is not null)
            return ("profile", ProfileInfo.ProfileId);
        return ("", -1);
    }

    private async Task RestoreCommandsFromLogAsync()
    {
        var (targetType, targetId) = GetPersistenceTarget();

        // Merge editor case: ProfileInfo.ProfileId == -1 is the sentinel.
        // Commands are persisted per-mod under ("mod", entity.ModId),
        // so we must restore from each mod in the profile.
        if (targetId < 0 && ProfileInfo is { ProfileId: -1 })
        {
            await RestoreMergeCommandsFromLogAsync();
            return;
        }

        if (targetId < 0) return;

        try
        {
            var snapshotSeq = await _workspacePersistence.GetSnapshotSequenceAsync(targetType, targetId);
            _logger.LogInformation("[Restore] snapshot seq={SnapshotSeq} for {TargetType}:{TargetId}",
                snapshotSeq, targetType, targetId);

            // Init _persistSequence from DB state to prevent snapshot regression.
            // Without this, _persistSequence=0 on restart → new commands start at seq=1 →
            // next save updates snapshot to seq=X < previous → old commands replayed on restart.
            _persistSequence = Math.Max(_persistSequence,
                await _workspacePersistence.GetMaxSequenceAsync(targetType, targetId));

            var commands = await _workspacePersistence.LoadCommandsAsync(
                targetType, targetId,
                ResolveEntityForReplay,
                () => SetDirty(true));

            if (commands.Count == 0)
            {
                _logger.LogWarning(
                    "[Restore] NO commands to replay for {TargetType}:{TargetId} — WAL is empty or already saved",
                    targetType, targetId);
                UpdatePersistenceDebugInfo();
                return;
            }

            _logger.LogInformation("[Restore] replaying {Count} commands for {TargetType}:{TargetId}",
                commands.Count, targetType, targetId);

            foreach (var (seq, cmd) in commands)
            {
                try
                {
                    cmd.Execute();
                    _commandHistory.RestoreFromLog(cmd);
                    _persistSequence = Math.Max(_persistSequence, seq);
                }
                catch (Exception ex)
                {
                    // Single command replay failure (e.g. entity no longer exists)
                    // should not prevent the rest of the WAL from restoring.
                    _logger.LogWarning(ex,
                        "[Restore] skip command seq={Seq} type={CmdType}: {Msg}",
                        seq, cmd.GetType().Name, ex.Message);
                }
            }

            // Populate EditStore.EditedCells from restored commands so that
            // MarkTabsDirtyFromEditedCells and PushEditStateToGrid can see them.
            // (EditStore was cleared before reload; command Execute() only sets
            // property values, not edit-tracking state. WAL restore must bridge this gap.)
            var walEntityCount = 0;
            foreach (var (_, cmd) in commands)
            {
                foreach (var eid in cmd.GetAffectedEntityIds())
                {
                    EditStore.EditedCells.Add((eid, "*"));
                    walEntityCount++;
                }
            }

            _logger.LogInformation("[Restore] populated EditStore with {Count} entity entries from WAL commands",
                walEntityCount);

            MarkTabsDirtyFromEditedCells();
            SetDirty(true);
            RebuildFilteredItemsSources();
            PushEditStateToGrid(MergeStore, EditStore);
            // Refresh triggers LoadingRow — must be after PushEditStateToGrid
            RefreshActiveDataGrid();
            UpdatePersistenceDebugInfo();
            _logger.LogInformation("[Restore] completed, sequence={Seq}, dirty", _persistSequence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Restore] failed for {TargetType}:{TargetId}", targetType, targetId);
        }
    }

    private async Task RestoreUndoStackFromLogAsync()
    {
        var (targetType, targetId) = GetPersistenceTarget();

        // Merge editor: restore undo stack from each mod.
        if (targetId < 0 && ProfileInfo is { ProfileId: -1 })
        {
            await RestoreMergeUndoStackFromLogAsync();
            return;
        }

        if (targetId < 0) return;
        try
        {
            var commands = await _workspacePersistence.LoadCommandsAsync(
                targetType, targetId,
                ResolveEntityForReplay,
                () => { }); // no dirty callback — data is already correct in cache

            if (commands.Count == 0) return;
            foreach (var (seq, cmd) in commands)
            {
                _commandHistory.RestoreFromLog(cmd); // push to undo stack without executing
                _persistSequence = Math.Max(_persistSequence, seq);
            }

            SetDirty(true);
            _logger.LogInformation("[RestoreUndoStack] restored {Count} commands to undo stack from cache",
                commands.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RestoreUndoStack] failed");
        }
    }

    /// <summary>Restore WAL commands for all mods in the merge editor profile.
    /// Since commands are persisted per-mod under ("mod", entity.ModId),
    /// we iterate each mod in the profile and replay its command log.</summary>
    private async Task RestoreMergeCommandsFromLogAsync()
    {
        var modIds = ProfileInfo?.ModLoadInfos
            .Where(m => m.Info is not null)
            .Select(m => m.Info!.ModId)
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        if (modIds.Count == 0)
        {
            _logger.LogWarning("[Restore] merge editor: no mods in profile to restore from");
            UpdatePersistenceDebugInfo();
            return;
        }

        _logger.LogInformation("[Restore] merge editor: restoring WAL for {Count} mods: [{ModIds}]",
            modIds.Count, string.Join(",", modIds));

        // Init _persistSequence from DB to prevent snapshot regression (see RestoreCommandsFromLogAsync).
        foreach (var modId in modIds)
        {
            var max = await _workspacePersistence.GetMaxSequenceAsync("mod", modId);
            _persistSequence = Math.Max(_persistSequence, max);
        }

        var totalRestored = 0;
        foreach (var modId in modIds)
        {
            try
            {
                var commands = await _workspacePersistence.LoadCommandsAsync(
                    "mod", modId,
                    ResolveEntityForReplay,
                    () => SetDirty(true));

                if (commands.Count == 0)
                {
                    _logger.LogInformation("[Restore] mod:{ModId} — no commands to replay", modId);
                    continue;
                }

                _logger.LogInformation("[Restore] mod:{ModId} replaying {Count} commands",
                    modId, commands.Count);

                foreach (var (seq, cmd) in commands)
                {
                    try
                    {
                        cmd.Execute();
                        _commandHistory.RestoreFromLog(cmd);
                        _persistSequence = Math.Max(_persistSequence, seq);
                        // Populate EditStore during replay so MarkTabsDirtyFromEditedCells
                        // can find the restored entities (EditStore was cleared before reload).
                        foreach (var eid in cmd.GetAffectedEntityIds())
                            EditStore.EditedCells.Add((eid, "*"));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[Restore] mod:{ModId} skip command seq={Seq} type={CmdType}: {Msg}",
                            modId, seq, cmd.GetType().Name, ex.Message);
                    }
                }

                totalRestored += commands.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Restore] failed for mod:{ModId}", modId);
            }
        }

        // ── Also restore base game data edits (ModId=-1, persisted to "game":0) ──
        try
        {
            var gameMaxSeq = await _workspacePersistence.GetMaxSequenceAsync("game", 0);
            _persistSequence = Math.Max(_persistSequence, gameMaxSeq);

            var gameCommands = await _workspacePersistence.LoadCommandsAsync(
                "game", 0,
                ResolveEntityForReplay,
                () => SetDirty(true));

            if (gameCommands.Count > 0)
            {
                _logger.LogInformation("[Restore] game:0 replaying {Count} commands", gameCommands.Count);
                foreach (var (seq, cmd) in gameCommands)
                {
                    try
                    {
                        cmd.Execute();
                        _commandHistory.RestoreFromLog(cmd);
                        _persistSequence = Math.Max(_persistSequence, seq);
                        foreach (var eid in cmd.GetAffectedEntityIds())
                            EditStore.EditedCells.Add((eid, "*"));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "[Restore] game:0 skip command seq={Seq} type={CmdType}: {Msg}",
                            seq, cmd.GetType().Name, ex.Message);
                    }
                }

                totalRestored += gameCommands.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Restore] failed for game:0");
        }

        if (totalRestored > 0)
        {
            MarkTabsDirtyFromEditedCells();
            SetDirty(true);
            RebuildFilteredItemsSources();
            PushEditStateToGrid(MergeStore, EditStore);
            RefreshActiveDataGrid();
        }

        UpdatePersistenceDebugInfo();
        _logger.LogInformation("[Restore] merge editor: completed, {Count} commands restored, sequence={Seq}",
            totalRestored, _persistSequence);
    }

    /// <summary>Restore undo stack for all mods in the merge editor profile.</summary>
    private async Task RestoreMergeUndoStackFromLogAsync()
    {
        var modIds = ProfileInfo?.ModLoadInfos
            .Where(m => m.Info is not null)
            .Select(m => m.Info!.ModId)
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        if (modIds.Count == 0) return;

        foreach (var modId in modIds)
        {
            try
            {
                var commands = await _workspacePersistence.LoadCommandsAsync(
                    "mod", modId,
                    ResolveEntityForReplay,
                    () => { });

                foreach (var (seq, cmd) in commands)
                {
                    _commandHistory.RestoreFromLog(cmd);
                    _persistSequence = Math.Max(_persistSequence, seq);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[RestoreUndoStack] merge: failed for mod:{ModId}", modId);
            }
        }
    }

    private IEntity? ResolveEntityForReplay(string entityId, Type entityType)
    {
        foreach (var tab in Tabs)
        {
            foreach (var item in tab.SourceCollection)
            {
                if (item is IEntity e && e.EntityId == entityId && e.GetType() == entityType)
                    return e;
            }
        }

        return null;
    }

    private ObservableCollection<object>? ResolveCollectionForReplay(string tabType)
    {
        var tab = Tabs.FirstOrDefault(t => t.EntityType.Name == tabType);
        return tab?.SourceCollection;
    }

    private async Task ClearWorkspaceAsync()
    {
        var (targetType, targetId) = GetPersistenceTarget();
        if (targetId < 0) return;
        try
        {
            await _workspacePersistence.ClearWorkspaceAsync(targetType, targetId);
            _persistSequence = 0;
            _commandsSinceSnapshot = 0;
            _logger.LogInformation("[ClearWorkspace] cleared for {TargetType}:{TargetId}", targetType, targetId);
            UpdatePersistenceDebugInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClearWorkspace] failed for {TargetType}:{TargetId}", targetType, targetId);
        }
    }

    #endregion

    #region Debug Status Bar

    public static readonly StyledProperty<string> PersistenceDebugTextProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, string>(nameof(PersistenceDebugText), "");

    public string PersistenceDebugText
    {
        get => GetValue(PersistenceDebugTextProperty);
        private set => SetValue(PersistenceDebugTextProperty, value);
    }

    private async void UpdatePersistenceDebugInfo()
    {
        var (targetType, targetId) = GetPersistenceTarget();
        if (targetId < 0)
        {
            PersistenceDebugText = "No target";
            return;
        }

        try
        {
            var snapSeq = await _workspacePersistence.GetSnapshotSequenceAsync(targetType, targetId);
            var hasSnap = snapSeq >= 0;
            await using var db = await _editorDbFactory.CreateDbContextAsync();
            var allCount = await System.Threading.Tasks.Task.Run(() =>
                db.CommandLogs.Count(c => c.TargetType == targetType && c.TargetId == targetId));
            var unsavedCount = await System.Threading.Tasks.Task.Run(() =>
                db.CommandLogs.Count(c => c.TargetType == targetType && c.TargetId == targetId && c.IsUnsaved));

            PersistenceDebugText = hasSnap
                ? $"Snap:seq={snapSeq} | CmdLog:{allCount} | Unsv:{unsavedCount} | Seq:{_persistSequence} | SinceSnap:{_commandsSinceSnapshot}"
                : $"Snap:none | CmdLog:{allCount} | Unsv:{unsavedCount} | Seq:{_persistSequence} | SinceSnap:{_commandsSinceSnapshot}";
        }
        catch (Exception ex)
        {
            PersistenceDebugText = $"Debug: err - {ex.Message}";
        }
    }

    public async void OnDebugInfoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var (targetType, targetId) = GetPersistenceTarget();
        if (targetId < 0) return;

        try
        {
            var snapSeq = await _workspacePersistence.GetSnapshotSequenceAsync(targetType, targetId);
            var hasSnap = snapSeq >= 0;

            await using var db = await _editorDbFactory.CreateDbContextAsync();
            var allCommands = db.CommandLogs
                .Where(c => c.TargetType == targetType && c.TargetId == targetId)
                .OrderBy(c => c.Sequence)
                .Select(c => new { c.Sequence, c.CommandType, c.IsUnsaved })
                .ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Persistence Debug: {targetType}:{targetId} ===");
            sb.AppendLine($"Snapshot: {(hasSnap ? $"seq={snapSeq}" : "none")}");
            sb.AppendLine($"In-memory sequence: {_persistSequence}");
            sb.AppendLine($"Commands since snapshot: {_commandsSinceSnapshot}");
            sb.AppendLine();
            sb.AppendLine("--- command_log ---");
            foreach (var c in allCommands)
            {
                var marker = c.Sequence <= snapSeq ? "[snap]" : c.IsUnsaved ? "[unsaved]" : "[saved]";
                sb.AppendLine($"  seq={c.Sequence} {c.CommandType,-12} {marker}");
            }

            sb.AppendLine($"--- {allCommands.Count} total ---");

            var dialog = new Window
            {
                Title = "Persistence Debug",
                Width = 500,
                Height = 400,
                Content = new Avalonia.Controls.TextBox
                {
                    Text = sb.ToString(),
                    IsReadOnly = true,
                    FontFamily = new Avalonia.Media.FontFamily("Consolas, monospace"),
                    AcceptsReturn = true,
                    TextWrapping = Avalonia.Media.TextWrapping.NoWrap
                }
            };
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is not null)
                await dialog.ShowDialog(owner);
            else
                dialog.Show();
        }
        catch (Exception ex)
        {
            ViewServices.Notification.ShowError($"Debug info failed: {ex.Message}");
        }
    }

    #endregion
}