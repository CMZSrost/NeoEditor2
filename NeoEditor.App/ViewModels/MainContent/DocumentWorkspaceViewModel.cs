using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Avalonia;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.DataViewer;
using NeoEditor.Plugins.DataViewer.ViewModels;
using NeoEditor.Plugins.EntityEditor.ViewModels;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Plugins.ImageTools.ViewModels;
using NeoEditor.Services;
using NeoEditor.Core.Abstractions;
using NeoEditor.Views.UserControls;

namespace NeoEditor.ViewModels.MainContent;

public partial class DocumentWorkspaceViewModel : ViewModelBase,
    IRecipient<EditProfileMessage>,
    IRecipient<OpenXmlDocumentMessage>,
    IRecipient<OpenModGameDataDocumentMessage>,
    IRecipient<OpenModImagesDocumentMessage>,
    IRecipient<OpenHelpDocumentMessage>,
    IRecipient<OpenMergeEditorMessage>,
    IRecipient<OpenImageDocumentMessage>
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<DocumentWorkspaceViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly NeoEditor.Services.IWorkspaceSession _session;
    private readonly Services.ISelectionService _selection;
    private readonly IEntityEditorDocumentFactory _entityEditorFactory;
    private readonly IModImagesDocumentFactory _modImagesDocumentFactory;

    public ObservableCollection<IDocumentBase> Documents { get; }

    [ObservableProperty] public partial bool IsHomePageVisible { get; set; } = true;
    [ObservableProperty] public partial string ActiveDocumentTitle { get; set; } = "";
    [ObservableProperty] public partial string SessionStatusText { get; set; } = "";
    [ObservableProperty] public partial string LastSavedText { get; set; } = "";

    private readonly OverlayChainToolContent _overlayChainContent;

    public OverlayChainToolContent OverlayChainContent => _overlayChainContent;
    public ReferenceInspectorContent ReferenceInspector { get; } = new();
    public ImagePreviewContent ImagePreview { get; }

    // New workspace view models (Phase 2-5)
    public KeyValueEditorViewModel KeyValueEditorVm { get; }
    public PeekPanelViewModel PeekPanel { get; }
    public IndexTableViewModel ForwardIndex { get; }
    public IndexTableViewModel ReverseIndex { get; }
    public ModDataToolViewModel ModDataToolVm { get; }

    // D02: the dock panes are built dynamically from IToolPlugin (see BuildToolDock).
    // The DataTable tool is the only one whose Context is swapped at runtime
    // (placeholder ↔ ModDataToolViewModel).
    public PluginTool? DataTableTool { get; private set; }


    public HomePageViewModel HomePage { get; }

    public System.Collections.ObjectModel.ObservableCollection<object> LeftToolItems { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<object> RightToolItems { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<object> BottomToolItems { get; } = [];

    [ObservableProperty] public partial bool IsLeftToolVisible { get; set; } = true;
    [ObservableProperty] public partial bool IsRightToolVisible { get; set; } = false; // Peek panel — show on demand
    [ObservableProperty] public partial bool IsBottomToolVisible { get; set; } = true;

    public DocumentWorkspaceViewModel(IServiceProvider serviceProvider)
    {
        _config = serviceProvider.GetRequiredService<IConfigService>();
        _logger = serviceProvider.GetRequiredService<ILogger<DocumentWorkspaceViewModel>>();
        _serviceProvider = serviceProvider;
        _session = serviceProvider.GetRequiredService<NeoEditor.Services.IWorkspaceSession>();
        _selection = serviceProvider.GetRequiredService<Services.ISelectionService>();
        _entityEditorFactory = serviceProvider.GetRequiredService<IEntityEditorDocumentFactory>();
        _modImagesDocumentFactory = serviceProvider.GetRequiredService<IModImagesDocumentFactory>();
        Notification = serviceProvider.GetRequiredService<INotificationService>();
        Loc = serviceProvider.GetRequiredService<ILocalizationService>();

        // Restore panel visibility from config
        var cfg = _config.Config;
        if (cfg is not null)
        {
            IsLeftToolVisible = cfg.LeftPanelVisible;
            IsRightToolVisible = cfg.RightPanelVisible;
            IsBottomToolVisible = cfg.BottomPanelVisible;
        }

        HomePage = serviceProvider.GetRequiredService<HomePageViewModel>();
        Documents = [];
        Documents.CollectionChanged += async (_, _) =>
        {
            IsHomePageVisible = Documents.Count == 0;
            if (IsHomePageVisible)
                await HomePage.RefreshAsync();
        };

        // Shared tool ViewModels are DI singletons (D02 §五) — the same instances the
        // plugin views bind to. Resolved here for the message-handler logic below.
        _overlayChainContent = _serviceProvider.GetRequiredService<OverlayChainToolContent>();
        KeyValueEditorVm = _serviceProvider.GetRequiredService<KeyValueEditorViewModel>();
        ImagePreview = serviceProvider.GetRequiredService<ImagePreviewContent>();
        PeekPanel = _serviceProvider.GetRequiredService<PeekPanelViewModel>();
        var indexFactory = _serviceProvider.GetRequiredService<IIndexTableFactory>();
        ForwardIndex = indexFactory.Forward;
        ReverseIndex = indexFactory.Reverse;
        // R10: Forward index eager-loads in constructor. Reverse loads on CurrentEntity selection.
        ModDataToolVm = _serviceProvider.GetRequiredService<ModDataToolViewModel>();

        // D02: enumerate IToolPlugin and build the Left/Right/Bottom dock panes dynamically.
        BuildToolDock();

        // ── ISelectionService: unified current-entity tracking (R12) ──
        // CurrentEntityChanged is the SINGLE handler for updating KV + OverlayChain + status.
        // All sources (Center doc focus, XML apply, double-click open) converge here.
        _selection.CurrentEntityChanged += (_, entity) =>
        {
            if (entity != null)
            {
                _logger.LogInformation("[VM] CurrentEntity→KV: {Eid}", entity.EntityId);
                KeyValueEditorVm.LoadEntityCommand.Execute(entity);
                _overlayChainContent.LoadChain(entity.EntityId, entity.Subject ?? "?", entity.GetType().Name);
                SessionStatusText = $"Editing: {entity.GetType().Name} — {entity.Subject ?? entity.EntityId}";
            }

            // R10: Forward index is global (all entities), unaffected by current entity change.
            // Only reverse index is entity-scoped and should be marked expired.
            ReverseIndex.MarkExpired();
        };

        _selection.OpenEntityRequested += (_, entity) =>
        {
            OpenEntityEditor(entity);
            ForwardIndex.CurrentEntity = entity;
            ReverseIndex.OnCurrentEntityChanged(entity);
        };

        _selection.NavigateRequested += (_, args) => { OpenEntityEditor(args.EntityType.Name, args.EntityId); };

        // PeekEntityMessage: decoupled peek command sent by Router (R05)
        Messenger.Register<PeekEntityMessage>(this, (_, m) =>
        {
            IsRightToolVisible = true;
            PeekPanel.Peek(m.Entity, null, null);
        });

        Messenger.Register<ActiveEntityChangedMessage>(this, (_, m) =>
        {
            // R12: route through ISelectionService — CurrentEntityChanged handles KV/OverlayChain
            if (m.Entity != null)
            {
                _selection.SetCurrentEntity(m.Entity);
                // R06: XML edits modify entity properties in-place. SetCurrentEntity's
                // ReferenceEquals check short-circuits for same-entity, so CurrentEntityChanged
                // never fires. Force KV reload so field values reflect the latest changes.
                KeyValueEditorVm.LoadEntityCommand.Execute(m.Entity);
            }
        });

        // ── Refresh EntityEditorDocument (visual + XML) after edits ──
        Messenger.Register<RefreshEntityEditorMessage>(this, (_, m) =>
        {
            var doc = Documents.OfType<EntityEditorDocument>()
                .FirstOrDefault(d => d.Entity?.EntityId == m.Entity.EntityId);
            if (doc != null)
            {
                doc.RefreshVisualizationCommand.Execute(null);
                doc.RefreshXml(); // respects IsXmlFocused to preserve undo stack
                doc.MarkDirty(); // R09: track unsaved edit from KV
            }

            // R10: entity edited → mark indexes as expired (no auto-rebuild)
            ForwardIndex.MarkExpired();
            ReverseIndex.MarkExpired();
        });

        // ── Cell edit in DataTable → mark indexes expired (R10) ──
        Messenger.Register<CellEditedMessage>(this, (_, _) =>
        {
            ForwardIndex.MarkExpired();
            ReverseIndex.MarkExpired();
        });

        // ── Cell edit committed → mark matching EntityEditorDocument dirty (Bug 1 fix) ──
        // When a DataGrid cell is edited, the EntityEditorDocument's IsDirty must be set
        // so the title shows "*" indicator and the Center Save button actually works.
        Messenger.Register<CellEditCommittedMessage>(this, (_, m) =>
        {
            foreach (var doc in Documents.OfType<EntityEditorDocument>())
            {
                if (doc.Entity?.EntityId == m.Entity.EntityId)
                {
                    doc.MarkDirty();
                    break;
                }
            }
        });

        // ── Data load completed: update welcome document stats ──
        Messenger.Register<DataLoadCompletedMessage>(this, (_, m) =>
        {
            var welcome = Documents.OfType<SessionWelcomeDocument>().FirstOrDefault();
            if (welcome != null)
                welcome.SetLoaded(m.TypeCount, m.EntityCount);
        });

        // ── New: Entity selection coordination ──
        Messenger.Register<EntitySelectedMessage>(this, (_, m) => OnEntitySelected(m));

        // ── New: Peek reference resolution (from KeyValueEditor) ──
        Messenger.Register<PeekReferenceRequestMessage>(this, (_, m) =>
        {
            try
            {
                var store = _session.ActiveMergeStore;
                if (store?.ReferenceLookups.TryGetValue(m.TargetType, out var entities) == true)
                {
                    // Try to find the entity by raw ID match (like "42" matches key=42 or entityId ending with #42)
                    foreach (var obj in entities)
                    {
                        if (obj is IEntity e)
                        {
                            var match = e.EntityId == m.RawId
                                        || e.EntityId.EndsWith("#" + m.RawId)
                                        || GetEntityKey(e) == m.RawId;
                            if (match)
                            {
                                PeekPanel.Peek(e, m.SourceEntity, m.PropertyName);
                                return;
                            }
                        }
                    }
                }
            }
            catch
            {
                /* resolution failure */
            }
        });

        // ── New: NavigateToEntity (Open Full from Peek / double-click) ──
        Messenger.Register<NavigateToEntityRequestedMessage>(this,
            (_, m) => { OpenEntityEditor(m.EntityType, m.EntityId); });

        // ── New: Open in split view ──
        Messenger.Register<OpenInSplitViewMessage>(this, (_, m) =>
        {
            var doc = CreateEntityEditorDocument(m.Entity);
            Documents.Add(doc);
            ActivateDocument(doc);
        });

        // Subscribe to overlay chain updates from ModGameDataTabsView
        Messenger.Register<OverlayChainRequestedMessage>(this, (_, m) =>
        {
            _overlayChainContent.LoadChain(m.EntityId, m.Subject, m.EntityType);
            IsLeftToolVisible = true;
        });

        // Conflicts auto-update

        DockFactory = serviceProvider.GetRequiredService<Factory>();
        DockFactory.DockableClosing += ClosingDockable;

        // R06/R12: When a document tab gains focus (user clicks tab header or
        // keyboard switches tabs), immediately notify KV editor and selection service.
        // This is more reliable than View-level PointerPressed/AttachedToVisualTree
        // events which don't fire on tab header clicks in Dock.Avalonia.
        DockFactory.FocusedDockableChanged += (_, args) =>
        {
            if (args.Dockable is IDockable { Context: EntityEditorDocument entityDoc }
                && entityDoc.Entity != null)
            {
                _logger.LogInformation("[DockFocus] tab activated: {Type} {Eid}",
                    entityDoc.Entity.GetType().Name, entityDoc.Entity.EntityId);
                Messenger.Send(new ActiveEntityChangedMessage(entityDoc.Entity));
                _selection.SetCurrentEntity(entityDoc.Entity);
            }
        };

        Loc.PropertyChanged += OnLocalizationPropertyChanged;
        Messenger.Register<MergeViewDirtyChangedMessage>(this, (_, m) => OnMergeViewDirtyChanged(m.IsDirty));

        // ── QuickSave completed → MarkClean all EntityEditorDocument instances ──
        Messenger.Register<SaveCompletedMessage>(this, (_, _) =>
        {
            foreach (var doc in Documents.OfType<EntityEditorDocument>())
            {
                if (doc.IsDirty)
                    doc.MarkClean();
                // Sync the Dock framework's Document.Title (DockableBase.Title)
                // which the tab header binds to. MarkClean updates DocumentBase.Title
                // but the Dockable wrapper has its own separate Title property.
                // Run outside the IsDirty check so the toolbar SaveDocument path
                // (which calls MarkClean before sending SaveCompletedMessage) also syncs.
                var dockDoc = DockFactory.GetContainerFromItem(doc);
                if (dockDoc is not null)
                    dockDoc.Title = doc.Title;
            }

            // Reload current KV entity to re-evaluate IsCurrentEntityDirty.
            // After save, DirtyEntities are cleared, but KV still shows old dirty state
            // until the entity is reloaded (which typically only happens on tab switch).
            if (KeyValueEditorVm.CurrentEntity is not null)
                KeyValueEditorVm.LoadEntityCommand.Execute(KeyValueEditorVm.CurrentEntity);
        });

        UpdateDockingEnabled();
    }

    /// <summary>
    /// Add the dynamically built tools into the layout's ToolDocks. Called by
    /// <c>DocumentWorkspaceView</c> once the DockControl has loaded (its
    /// <see cref="Dock.Avalonia.Controls.DockControl.Layout"/> is populated then). Retries briefly
    /// because the layout can still be null on the very first <c>Loaded</c> callback.
    /// </summary>
    /// <remarks>
    /// Dock.Avalonia 12.1.0 does NOT sync <c>ToolDock.ItemsSource</c> into the layout's
    /// <c>VisibleDockables</c> — tools stay in the bound collection but never appear in the dock
    /// (document docks sync fine; tool docks do not). The working pattern (same as
    /// <c>DomainBrowserView</c>) is to add each tool to its ToolDock via
    /// <see cref="Factory.AddDockable"/> once the DockControl layout has been initialized.
    /// </remarks>
    public void SyncToolDockIntoLayout(Dock.Avalonia.Controls.DockControl dockControl)
    {
        Helper.AsyncHelper.FireAndForget(SyncToolDockIntoLayoutCoreAsync(dockControl));
    }

    private async Task SyncToolDockIntoLayoutCoreAsync(Dock.Avalonia.Controls.DockControl dockControl)
    {
        var map = new Dictionary<string, IEnumerable<object>>
        {
            ["LeftToolPane"] = LeftToolItems,
            ["RightToolPane"] = RightToolItems,
            ["BottomToolPane"] = BottomToolItems,
        };

        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (dockControl.Layout is Dock.Model.Core.IDockable rootDock)
            {
                AddToolsToDock(rootDock, map);
                return;
            }
            await Task.Delay(100);
        }
        _logger.LogWarning("[Dock] Tool dock layout sync timed out — tools may be missing.");
    }

    private void AddToolsToDock(Dock.Model.Core.IDockable? dockable,
        IReadOnlyDictionary<string, IEnumerable<object>> byId)
    {
        if (dockable is null) return;
        if (dockable is Dock.Model.Avalonia.Controls.ToolDock td
            && byId.TryGetValue(td.Id, out var tools))
        {
            foreach (var t in tools)
            {
                if (t is Dock.Model.Core.IDockable d
                    && td.VisibleDockables is { } visible
                    && !visible.Contains(d))
                    DockFactory.AddDockable(td, d);
            }

            // The DataTable tool is the primary bottom tool — make it the active tab so its
            // ModGameDataTabsView content attaches and the merge view is visible by default.
            if (td.Id == "BottomToolPane" && DataTableTool is not null)
            {
                DockFactory.SetActiveDockable(DataTableTool);
                DockFactory.SetFocusedDockable(td, DataTableTool);
            }
        }
        if (dockable is Dock.Model.Core.IDock dock && dock.VisibleDockables is { } list)
            foreach (var d in list)
                AddToolsToDock(d, byId);
    }

    /// <summary>
    /// D02: enumerate all <see cref="IToolPlugin"/> and dynamically build the
    /// Left/Right/Bottom dock panes. Each plugin contributes exactly one Tool
    /// (a <see cref="PluginTool"/> wrapper; Id = plugin type name, stable for
    /// Dock.Avalonia layout persistence). Spec: D02 §六.
    /// </summary>
    private void BuildToolDock()
    {
        var plugins = _serviceProvider.GetRequiredService<IEnumerable<IToolPlugin>>()
            .OrderBy(p => p.Order)
            .ToList();

        foreach (var plugin in plugins)
        {
            var tool = new PluginTool(plugin);

            if (plugin is DataTablePlugin)
            {
                DataTableTool = tool;
                // The DataTable tool's Content is the merge grid itself, bound to the shared
                // ModDataToolViewModel. (A bare VM is NOT valid Tool.Content — Dock.Avalonia's
                // Tool.Build expects a buildable view/template content.) The grid is driven by
                // ModDataToolVm.SetProfile/Clear through the ProfileInfo binding, so no runtime
                // Content swap is needed.
                var grid = new ModGameDataTabsView
                {
                    ReadOnly = true,
                    DataContext = ModDataToolVm,
                };
                grid.Bind(ModGameDataTabsView.ProfileInfoProperty,
                    new Avalonia.Data.Binding("ProfileInfo"));
                DataTableTool.Content = grid;
                DataTableTool.Context = grid;
            }

            switch (plugin.DefaultDock)
            {
                case ToolDock.Left: LeftToolItems.Add(tool); break;
                case ToolDock.Right: RightToolItems.Add(tool); break;
                case ToolDock.Bottom: BottomToolItems.Add(tool); break;
            }
        }

        // Note: Dock.Avalonia 12.1.0 does NOT sync ToolDock.ItemsSource into the layout, so the
        // tools are added to the dock panes manually once the DockControl has loaded — the view's
        // code-behind calls SyncToolDockIntoLayout(MainDockControl) (see DocumentWorkspaceView).
    }

    // ── Entity selection coordination ──

    private void OnEntitySelected(EntitySelectedMessage msg)
    {
        // R15: single-click on DataTable row highlights it but does NOT open a Center tab
        // or change the current entity. The current entity is set when a Center document
        // gains focus, or when double-click/Ctrl+LMB explicitly requests it.
        _activeEntity = msg.Entity;
        ForwardIndex.CurrentEntity = msg.Entity;
        ReverseIndex.OnCurrentEntityChanged(msg.Entity);
        // P1 fix (Test Round 10): keep KV editor in sync with DataGrid selection so
        // IsCurrentEntityDirty reflects the correct entity's dirty state. Without this,
        // single-clicking a non-dirty entity after previously loading a dirty entity
        // leaves the yellow "unsaved changes" banner visible (stale state).
        KeyValueEditorVm.LoadEntityCommand.Execute(msg.Entity);
        // Note: OpenEntityEditor is NOT called here — per R15 it requires double-click
        // or Ctrl+LMB, which go through ISelectionService.RequestOpenEntity / NavigateRequested.
    }

    private void OpenEntityEditor(IEntity entity)
    {
        // ActivateDocument now sends ActiveEntityChangedMessage and calls _selection.SetCurrentEntity,
        // so KV editor updates synchronously when the document tab becomes active.

        var existing = Documents.OfType<EntityEditorDocument>()
            .FirstOrDefault(d => d.Entity?.EntityId == entity.EntityId);
        if (existing != null)
        {
            // Only update Entity if reference changed (avoids clearing XML undo stack)
            if (!ReferenceEquals(existing.Entity, entity))
                existing.Entity = entity;
            existing.IsVisualDirty = true;
            // Defer visualizer rebuild so the KV editor already rendered.
            // Background priority fires after layout/rendering of the current batch.
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    existing.RefreshVisualizationCommand.Execute(null),
                Avalonia.Threading.DispatcherPriority.Background);
            ActivateDocument(existing);
            return;
        }

        var doc = CreateEntityEditorDocument(entity);
        Documents.Add(doc);
        ActivateDocument(doc);
    }

    private void OpenEntityEditor(string entityTypeName, string entityId)
    {
        // Fallback for NavigateToEntityRequestedMessage: try to find entity in active store
        try
        {
            var existing = Documents.OfType<EntityEditorDocument>()
                .FirstOrDefault(d => d.Entity?.EntityId == entityId);
            if (existing != null)
            {
                ActivateDocument(existing);
                return;
            }

            // Search ReferenceLookups in the active workspace store (R03: no static Instance)
            var store = GetActiveMergeStore();
            if (store != null)
            {
                foreach (var lookup in store.ReferenceLookups)
                {
                    foreach (var entityObj in lookup.Value)
                    {
                        if (entityObj is IEntity e && e.EntityId == entityId)
                        {
                            var doc = CreateEntityEditorDocument(e);
                            Documents.Add(doc);
                            ActivateDocument(doc);
                            return;
                        }
                    }
                }
            }
        }
        catch
        {
            /* Silently fail if entity cannot be resolved */
        }
    }


    private static string? GetEntityKey(IEntity entity)
    {
        var type = entity.GetType();
        foreach (var prop in type.GetProperties())
        {
            var indexAttr = prop.GetCustomAttribute<Microsoft.EntityFrameworkCore.IndexAttribute>();
            if (indexAttr?.PropertyNames != null && indexAttr.PropertyNames.Contains(prop.Name)
                                                 && prop.Name != nameof(IEntity.EntityId))
            {
                return prop.GetValue(entity)?.ToString();
            }
        }

        return null;
    }

    private EntityMergeStore? GetActiveMergeStore()
    {
        try
        {
            return _session.ActiveMergeStore;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Create an EntityEditorDocument via the factory (avoids direct Plugin VM reference).</summary>
    private EntityEditorDocument CreateEntityEditorDocument(IEntity entity)
        => (EntityEditorDocument)_entityEditorFactory.CreateDocument(entity);

    [ObservableProperty] public partial Factory DockFactory { get; set; }

    private bool _isDockingEnabled;

    public bool IsDockingEnabled
    {
        get => _isDockingEnabled;
        set => SetProperty(ref _isDockingEnabled, value);
    }

    [RelayCommand]
    private void AddImage()
    {
        var document = new ImageEditorDocument(
            _serviceProvider.GetRequiredService<IImageEditorProcessingService>(),
            _serviceProvider.GetRequiredService<PixelArtConversionService>(),
            _serviceProvider.GetRequiredService<ILocalizationService>());
        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    [RelayCommand]
    private void AddDocument()
    {
        var index = Documents.Count + 1;
        var document = new PlainTextDocument();
        document.SetLocalizedTitle("NewDocumentTitleFormat", index);
        document.SetLocalizedContent("NewDocumentContentFormat", index);
        Documents.Add(document);
        UpdateDockingEnabled();
    }

    public void Receive(EditProfileMessage message)
    {
        _logger.LogInformation("Loading profile: {ProfileName}", message.ProfileInfo.Name);

        if (FindOpenEditProfileDocument(message.ProfileInfo) is { } existingDocument)
        {
            ActivateDocument(existingDocument);
            return;
        }

        var viewModel = new EditProfileViewModel(_serviceProvider)
        {
            ProfileInfo = message.ProfileInfo,
        };
        viewModel.SetStaticTitle(message.ProfileInfo.Name);
        viewModel.LoadEntries();
        Documents.Add(viewModel);
        ActivateDocument(viewModel);
        UpdateDockingEnabled();
    }

    public void Receive(OpenModGameDataDocumentMessage message)
    {
        _logger.LogInformation("[WorkspaceVM] OpenModGameData: {ModName}", message.ModInfo.Name);

        // B4: single mod opens through a persisted single-mod profile (only that mod, IncludeGame=false).
        // WAL per-mod isolation ensures no data loss on switch — no guard needed.
        Helper.AsyncHelper.FireAndForget(OpenSingleModAsync(message.ModInfo));
    }

    /// <summary>B4: open a single mod as a single-mod profile (only that mod, IncludeGame=false).</summary>
    private async Task OpenSingleModAsync(Data.Model.ModInfo modInfo)
    {
        // Close peer documents (Data Browser) — only one workspace mode active
        foreach (var d in Documents.OfType<EntityBrowserDocument>().ToList()) Documents.Remove(d);

        await LoadModDataAsync(modInfo);
        var profile = await EnsureSingleModProfileAsync(modInfo);
        // The DataTable tool's Content is already bound to ModDataToolViewModel (BuildToolDock);
        // SetProfile drives the reload through the ProfileInfo binding.
        ModDataToolVm.SetProfile(profile);
        Messenger.Send(new SessionStateChangedMessage(true));
        ShowWelcomeDocument();
    }

    /// <summary>
    /// B4: find or create the persisted single-mod profile for the given mod.
    /// The profile contains exactly that mod, IncludeGame=false, and is tagged with
    /// <see cref="ProfileInfo.SingleModId"/> so WAL persistence stays per-mod.
    /// </summary>
    private async Task<ProfileInfo> EnsureSingleModProfileAsync(Data.Model.ModInfo modInfo)
    {
        var editorDbFactory = _serviceProvider
            .GetRequiredService<IDbContextFactory<EditorDbContext>>();
        await using var db = await editorDbFactory.CreateDbContextAsync();

        var existing = await db.ProfileInfos.FirstOrDefaultAsync(p => p.SingleModId == modInfo.ModId);
        if (existing is not null)
        {
            existing.IncludeGame = false;
            existing.ModLoadInfos.Clear();
            existing.ModLoadInfos.Add(new ModLoadInfo { Info = modInfo, Type = ModType.Merge, Namespace = "0" });
            return existing;
        }

        // strModName "0" → ModEntry.Type == Merge → the mod keeps business keys (same as the old single-mod view).
        var content = _serviceProvider.GetRequiredService<PhpParser>()
            .GenerateModsPhp([new ModEntry { Name = "0", Path = modInfo.Path }]);

        var profile = new ProfileInfo
        {
            Name = modInfo.Name,
            Description = "Single Mod",
            Path = "",
            Content = content,
            IncludeGame = false,
            SingleModId = modInfo.ModId,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now
        };
        profile.ModLoadInfos.Add(new ModLoadInfo { Info = modInfo, Type = ModType.Merge, Namespace = "0" });

        db.ProfileInfos.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    /// <summary>Show a welcome document in center with stats and usage tips.</summary>
    private void ShowWelcomeDocument()
    {
        foreach (var d in Documents.OfType<SessionWelcomeDocument>().ToList()) Documents.Remove(d);
        var welcome = new SessionWelcomeDocument();
        // Data loads async in background — show ready state immediately
        welcome.StatusText = "Ready — select an entity below to begin editing.";
        welcome.IsLoading = false;
        welcome.StatsText = "Data is loading in the bottom table…";
        Documents.Add(welcome);
        ActivateDocument(welcome);
    }

    private async System.Threading.Tasks.Task LoadModDataAsync(Data.Model.ModInfo modInfo)
    {
        try
        {
            var modManager = _serviceProvider.GetRequiredService<IModManager>();
            await modManager.LoadModAsync(modInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadMod failed");
        }
    }

    public void Receive(OpenModImagesDocumentMessage message)
    {
        _logger.LogInformation("Opening mod images document: {ModName}", message.ModInfo.Name);

        if (FindOpenModImagesDocument(message.ModInfo) is { } existingDocument)
        {
            existingDocument.SetLocalizedTitle("ModImagesTitleFormat", message.ModInfo.Name);
            ActivateDocument(existingDocument);
            return;
        }

        var document = (ModImagesDocument)_modImagesDocumentFactory.CreateDocument(message.ModInfo);
        document.SetLocalizedTitle("ModImagesTitleFormat", message.ModInfo.Name);

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    public void Receive(OpenXmlDocumentMessage message)
    {
        var normalizedPath = NormalizeDocumentPath(message.XmlPath);
        _logger.LogInformation("Opening xml document: {XmlPath}", normalizedPath);

        if (FindOpenXmlDocument(normalizedPath) is { } existingDocument)
        {
            ActivateDocument(existingDocument);
            return;
        }

        var title = string.IsNullOrWhiteSpace(message.Title)
            ? Path.GetFileName(normalizedPath)
            : message.Title;
        var document = new XmlDocument(normalizedPath);
        document.SetStaticTitle(title);

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    public async void Receive(OpenMergeEditorMessage message)
    {
        _logger.LogInformation("Opening merge editor for profile: {ProfileName} (id={ProfileId})",
            message.ProfileInfo.Name, message.ProfileInfo.ProfileId);

        // R26 §3: dirty tracking is scoped to the profile being opened.
        _serviceProvider.GetRequiredService<IHostService>().SetActiveProfile(message.ProfileInfo.ProfileId);

        if (FindOpenMergeEditorDocument(message.ProfileInfo) is { } existing)
        {
            ActivateDocument(existing);
            return;
        }

        // Synchronously load all mods into DB BEFORE creating the merge view.
        // This is critical: ModLoadInfos is [NotMapped] and must be populated from Content.
        // The merge view queries DB directly, so entities MUST be in DB first.
        try
        {
            var profileManager = _serviceProvider.GetRequiredService<IProfileManager>();
            var modManager = _serviceProvider.GetRequiredService<IModManager>();
            var gameRoot = _serviceProvider.GetRequiredService<IConfigService>().Config.GameRootDir;

            _logger.LogInformation("[PreLoad] Content length={Len}", message.ProfileInfo.Content?.Length ?? -1);

            var modLoadInfos = profileManager.LoadMods(message.ProfileInfo.Content);
            message.ProfileInfo.ModLoadInfos.Clear();
            foreach (var mli in modLoadInfos)
                message.ProfileInfo.ModLoadInfos.Add(mli);

            _logger.LogInformation("[PreLoad] parsed {Count} mod(s) from getmods.php", modLoadInfos.Count);

            foreach (var mli in message.ProfileInfo.ModLoadInfos)
            {
                _logger.LogInformation("[PreLoad] mod: namespace='{Ns}' hasInfo={HasInfo} modId={ModId} path='{Path}'",
                    mli.Namespace, mli.Info is not null, mli.Info?.ModId ?? -999, mli.Info?.Path ?? "(null)");

                if (mli.Info is null) continue;

                // "Needs import" = not yet persisted in the editor DB (Id is the autoincrement PK,
                // so a synthetic ModInfo from ProfileManager.LoadMods has Id=0). Do NOT key this on
                // ModId: ModId=0 is a valid business id (convention: -1=Game, >=0=Mod), and mods that
                // were imported first (e.g. NSEaid) legitimately hold ModId=0. Keying on ModId<=0 forced
                // a re-import of those mods on every merge-view open, which hit the UNIQUE constraint
                // on mod_info.Path and aborted the load.
                if (mli.Info.Id <= 0)
                {
                    var modPath = System.IO.Path.Combine(gameRoot, mli.Info.Path ?? "");
                    _logger.LogInformation("[PreLoad] attempting import: '{Path}' exists={Exists}",
                        modPath, System.IO.Directory.Exists(modPath));
                    if (!string.IsNullOrWhiteSpace(mli.Info.Path) && System.IO.Directory.Exists(modPath))
                    {
                        var imported = await modManager.ImportModAsync(modPath);
                        if (imported is not null)
                        {
                            mli.Info = imported;
                            _logger.LogInformation("[PreLoad] import OK: '{Name}' ModId={ModId}",
                                imported.Name, imported.ModId);
                        }
                        else
                        {
                            _logger.LogWarning("[PreLoad] import FAILED for '{Path}'", mli.Info.Path);
                        }
                    }
                }
                else
                {
                    try
                    {
                        await modManager.LoadModAsync(mli.Info);
                        _logger.LogInformation("[PreLoad] LoadModAsync OK: '{Name}' ModId={ModId}",
                            mli.Info.Name, mli.Info.ModId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PreLoad] LoadModAsync FAILED: '{Name}'", mli.Info.Name);
                    }
                }
            }

            _logger.LogInformation("[PreLoad] complete: {Count} mod(s) processed",
                message.ProfileInfo.ModLoadInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PreLoad] FATAL error during mod loading");
        }

        // Only one merge view at a time — close any existing merge editors
        foreach (var existingMerge in Documents.OfType<MergeEditorDocument>().ToList())
            Documents.Remove(existingMerge);

        // Close peer documents
        foreach (var d in Documents.OfType<EntityBrowserDocument>().ToList()) Documents.Remove(d);

        // Put the merge DataGrid into the bottom DataTable Tool. The tool's Content is bound to the
        // shared ModDataToolViewModel at creation (BuildToolDock); SetProfile drives the reload
        // through the ProfileInfo binding.
        ModDataToolVm.SetProfile(message.ProfileInfo);
        // Focus the DataTable tool so the merge grid (its Content) is the active bottom tab.
        if (DataTableTool is not null)
            DockFactory.SetActiveDockable(DataTableTool);
        Messenger.Send(new SessionStateChangedMessage(true));
        ShowWelcomeDocument();
    }


    private MergeEditorDocument? FindOpenMergeEditorDocument(ProfileInfo profileInfo)
    {
        return Documents.OfType<MergeEditorDocument>()
            .FirstOrDefault(d => d.ProfileInfo?.ProfileId == profileInfo.ProfileId);
    }

    public void Receive(OpenImageDocumentMessage message)
    {
        var normalizedPath = NormalizeDocumentPath(message.ImagePath);
        if (!File.Exists(normalizedPath)) return;

        if (FindOpenImageDocument(normalizedPath) is { } existing)
        {
            ActivateDocument(existing);
            return;
        }

        var doc = new ImageDocument { ImagePath = normalizedPath };
        doc.SetStaticTitle(message.Title);
        Documents.Add(doc);
        ActivateDocument(doc);
    }

    private ImageDocument? FindOpenImageDocument(string path)
    {
        return Documents.OfType<ImageDocument>()
            .FirstOrDefault(d => string.Equals(d.ImagePath, path, StringComparison.OrdinalIgnoreCase));
    }

    public void Receive(OpenHelpDocumentMessage message)
    {
        var normalizedPath = NormalizeDocumentPath(message.DocumentPath);
        _logger.LogInformation("Opening help document: {HelpPath}", normalizedPath);

        if (!File.Exists(normalizedPath))
        {
            return;
        }

        if (FindOpenHelpDocument(normalizedPath) is { } existingDocument)
        {
            ActivateDocument(existingDocument);
            return;
        }

        var title = string.IsNullOrWhiteSpace(message.Title)
            ? Path.GetFileNameWithoutExtension(normalizedPath)
            : message.Title;
        MarkdownDocument? document = Path.GetExtension(normalizedPath).ToLowerInvariant() switch
        {
            ".md" or ".markdown" => new MarkdownDocument(normalizedPath, title),
            _ => null
        };

        if (document is null)
        {
            return;
        }

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    public void ClosingDockable(object? sender, DockableClosingEventArgs e)
    {
        if (e.Dockable is not { Context: IDocumentBase docContext })
        {
            return;
        }

        e.Cancel = true;
        Helper.AsyncHelper.FireAndForget(ConfirmCloseDockableAsync(docContext));
    }

    private async Task ConfirmCloseDockableAsync(IDocumentBase docContext)
    {
        if (docContext is EditProfileViewModel { ProfileInfo: { } profileInfo, NeedNotifyWhenClose: true } model)
        {
            _logger.LogInformation("Closing document for profile: {ProfileName}", profileInfo.Name);

            var result = await ShowConfirmDialogAsync(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                ContentTitle = Loc["CloseProfile"],
                ContentMessage = Loc["CloseProfileConfirmation"],
                Icon = Icon.Question
            });

            switch (result)
            {
                case ButtonResult.Yes:
                    model.Save();
                    model.NeedNotifyWhenClose = false;
                    break;
                case ButtonResult.Cancel:
                    return;
            }
        }

        if (docContext is MergeEditorDocument { NeedNotifyWhenClose: true })
        {
            var result = await ShowConfirmDialogAsync(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                ContentTitle = "Unsaved Changes",
                ContentMessage = "You have unsaved changes in the merge view. Save before closing?",
                Icon = Icon.Question
            });
            switch (result)
            {
                case ButtonResult.Yes:
                    Messenger.Send(new SaveRequestedMessage());
                    // Wait briefly for save to complete
                    await System.Threading.Tasks.Task.Delay(300);
                    break;
                case ButtonResult.Cancel:
                    return;
            }
        }

        if (docContext is ModImagesDocument { ModInfo: { } modInfo, NeedNotifyWhenClose: true } imageDocument)
        {
            _logger.LogInformation("Closing image document for mod: {ModName}", modInfo.Name);

            var result = await ShowConfirmDialogAsync(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                ContentTitle = Loc["CloseModImages"],
                ContentMessage = Loc["CloseModImagesConfirmation"],
                Icon = Icon.Question
            });

            switch (result)
            {
                case ButtonResult.Yes:
                    await imageDocument.SaveCommand.ExecuteAsync(null);
                    if (imageDocument.NeedNotifyWhenClose)
                    {
                        return;
                    }

                    break;
                case ButtonResult.Cancel:
                    return;
            }
        }

        Documents.Remove(docContext);
        UpdateDockingEnabled();
    }

    private async Task<ButtonResult> ShowConfirmDialogAsync(MessageBoxStandardParams parameters)
    {
        var msgBox = MessageBoxManager.GetMessageBoxStandard(parameters);
        if (Application.Current is
            {
                ApplicationLifetime: IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: { } mainWindow
                }
            })
        {
            return await msgBox.ShowWindowDialogAsync(mainWindow);
        }

        return await msgBox.ShowAsync();
    }

    private EditProfileViewModel? FindOpenEditProfileDocument(ProfileInfo profileInfo)
    {
        var documentKey = GetEditProfileDocumentKey(profileInfo);
        return Documents
            .OfType<EditProfileViewModel>()
            .FirstOrDefault(doc => string.Equals(GetEditProfileDocumentKey(doc.ProfileInfo), documentKey,
                StringComparison.OrdinalIgnoreCase));
    }

    private ModImagesDocument? FindOpenModImagesDocument(ModInfo modInfo)
    {
        var documentKey = GetModImagesDocumentKey(modInfo);
        return Documents
            .OfType<ModImagesDocument>()
            .FirstOrDefault(doc => string.Equals(GetModImagesDocumentKey(doc.ModInfo), documentKey,
                StringComparison.OrdinalIgnoreCase));
    }

    private XmlDocument? FindOpenXmlDocument(string normalizedPath)
    {
        return Documents
            .OfType<XmlDocument>()
            .FirstOrDefault(doc => string.Equals(NormalizeDocumentPath(doc.XmlPath), normalizedPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private MarkdownDocument? FindOpenHelpDocument(string normalizedPath)
    {
        return Documents
            .OfType<MarkdownDocument>()
            .FirstOrDefault(doc => string.Equals(NormalizeDocumentPath(doc.FilePath), normalizedPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private void OnMergeViewDirtyChanged(bool isDirty)
    {
        var mergeDoc = Documents.OfType<MergeEditorDocument>().FirstOrDefault();
        if (mergeDoc is not null)
            mergeDoc.NeedNotifyWhenClose = isDirty;
    }

    public void ActivateDocument(IDocumentBase document)
    {
        ActiveDocumentTitle = document.Title ?? "";

        // R06/R12: When activating an EntityEditorDocument tab, immediately notify
        // KV editor and selection service so the left panel reflects the active entity.
        // Previously this only happened on unreliable View events (AttachedToVisualTree,
        // PointerPressed), causing KV editor to lag or not update at all on tab switch.
        if (document is EntityEditorDocument entityDoc && entityDoc.Entity != null)
        {
            Messenger.Send(new ActiveEntityChangedMessage(entityDoc.Entity));
            _selection.SetCurrentEntity(entityDoc.Entity);
        }

        // Collapse sidebar when opening a document
        try
        {
            _serviceProvider.GetService<MainWindowSideBarViewModel>()?.TogglePaneCommand.Execute(null);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[ActivateDocument] TogglePane failed");
        }

        try
        {
            var sidebar = _serviceProvider.GetService<MainWindowSideBarViewModel>();
            if (sidebar is not null) sidebar.SideBarExpanded = false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[ActivateDocument] Set SideBarExpanded failed");
        }

        var currentIndex = Documents.IndexOf(document);
        if (currentIndex < 0 || currentIndex == Documents.Count - 1)
        {
            return;
        }

        Documents.RemoveAt(currentIndex);
        Documents.Add(document);
    }

    [RelayCommand]
    private void ToggleLeftPanel()
    {
        IsLeftToolVisible = !IsLeftToolVisible;
        SaveLayout();
    }

    [RelayCommand]
    private void ToggleRightPanel()
    {
        IsRightToolVisible = !IsRightToolVisible;
        SaveLayout();
    }

    [RelayCommand]
    private void ToggleBottomPanel()
    {
        IsBottomToolVisible = !IsBottomToolVisible;
        SaveLayout();
    }

    /// <summary>Called by DocumentWorkspaceView to persist panel sizes.</summary>
    public void SaveLayoutSizes(double leftWidth, double rightWidth, double bottomHeight)
    {
        var cfg = _config.Config;
        if (cfg is null) return;
        cfg.LeftPanelWidth = leftWidth;
        cfg.RightPanelWidth = rightWidth;
        cfg.BottomPanelHeight = bottomHeight;
        SaveLayout();
    }

    private void SaveLayout()
    {
        var cfg = _config.Config;
        if (cfg is null) return;
        cfg.LeftPanelVisible = IsLeftToolVisible;
        cfg.RightPanelVisible = IsRightToolVisible;
        cfg.BottomPanelVisible = IsBottomToolVisible;
        Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
    }

    [RelayCommand]
    private void CloseAllDocuments()
    {
        _logger.LogInformation("[VM] CloseAllDocuments: clearing session");
        _activeEntity = null;
        // The DataTable tool's Content stays bound to ModDataToolViewModel; Clear() sets
        // ProfileInfo=null and ModGameDataTabsView drops its tabs (no placeholder swap needed).
        ModDataToolVm.Clear();
        ForwardIndex.Clear();
        ReverseIndex.Clear();
        Messenger.Send(new SessionStateChangedMessage(false));
        KeyValueEditorVm.LoadEntityCommand.Execute(null);
        _session.SetActiveStores(null, null);
        foreach (var doc in Documents.ToList()) Documents.Remove(doc);
    }

    private IEntity? _activeEntity;

    [RelayCommand]
    private void NewEntity()
    {
        if (ModDataToolVm.ProfileInfo == null)
        {
            Notification.ShowInfo("Open a mod or profile first to create entities.", "Info");
            return;
        }

        Messenger.Send(new CreateEntityRequestedMessage());
    }

    [RelayCommand]
    private void CopyEntity()
    {
        var entity = _activeEntity ?? KeyValueEditorVm.CurrentEntity;
        if (entity == null)
        {
            Notification.ShowInfo("Select an entity first to copy.", "Info");
            return;
        }

        Messenger.Send(new CopyEntityRequestedMessage());
    }

    [RelayCommand]
    private void DeleteEntity()
    {
        var entity = _activeEntity ?? KeyValueEditorVm.CurrentEntity;
        if (entity == null)
        {
            Notification.ShowInfo("Select an entity first to delete.", "Info");
            return;
        }

        Messenger.Send(new DeleteEntityRequestedMessage());
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveSession()
    {
        try
        {
            // 1. Commit pending KV editor changes to entity objects
            KeyValueEditorVm.ApplyChangesCommand.Execute(null);

            // 2. Send save request to all open views (DB write + XML export)
            Messenger.Send(new SaveRequestedMessage());

            // 3. Persist config
            await _config.SaveAsync();

            // 4. Update status
            var now = DateTime.Now.ToString("HH:mm");
            LastSavedText = $"Saved at {now}";
            SessionStatusText = $"Saved at {now}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save failed");
            Notification.ShowInfo("Save failed — check logs for details.", "Error");
        }
    }

    private void UpdateDockingEnabled()
    {
        IsDockingEnabled = true; // always on — enables drag-to-split for comparison
    }

    private PlainTextDocument CreateWelcomeDocument()
    {
        var document = new PlainTextDocument();
        document.SetLocalizedTitle("Welcome");
        document.SetLocalizedContent("WelcomeDocumentContent");
        return document;
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(LocalizationService.CurrentCulture))
        {
            return;
        }

        foreach (var document in Documents)
        {
            document.RefreshLocalizedText();
        }
    }

    private string GetEditProfileDocumentKey(ProfileInfo? profileInfo)
    {
        if (profileInfo is null)
        {
            return string.Empty;
        }

        if (profileInfo.ProfileId != 0)
        {
            return $"profileid:{profileInfo.ProfileId}";
        }

        var normalizedPath = NormalizeWorkspacePath(profileInfo.Path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            return $"path:{normalizedPath}";
        }

        return $"name:{profileInfo.Name}";
    }

    private string GetModImagesDocumentKey(ModInfo? modInfo)
    {
        if (modInfo is null)
        {
            return string.Empty;
        }

        if (modInfo.ModId != 0)
        {
            return $"modid:{modInfo.ModId}";
        }

        var normalizedPath = NormalizeWorkspacePath(modInfo.Path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            return $"path:{normalizedPath}";
        }

        return $"name:{modInfo.Name}";
    }

    private string NormalizeWorkspacePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalizedPath = path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedPath))
        {
            return Path.GetFullPath(normalizedPath);
        }

        return string.IsNullOrWhiteSpace(Config.GameRootDir)
            ? normalizedPath.TrimStart(Path.DirectorySeparatorChar)
            : Path.GetFullPath(Path.Combine(Config.GameRootDir, normalizedPath));
    }

    private static string NormalizeDocumentPath(string path)
    {
        return Path.GetFullPath(path);
    }
}