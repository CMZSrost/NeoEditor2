using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.ViewModels;

/// <summary>
/// ViewModel for DataTableView. Extracts business logic that was previously
/// mixed into the View code-behind (N03 violation — View should only assemble controls).
///
/// Responsibilities:
/// - CommandHistory ownership and lifecycle (undo/redo/WAL persistence)
/// - Dirty state tracking (IsViewDirty, dirty tabs)
/// - Auto-save timer
/// - Tab + store ownership (Tabs, MergeStore, EditStore)
/// - Data loading orchestration (via DataLoaderService)
///
/// R07: Dependencies received via constructor injection.
/// This is a Transient-per-View instance, created by the View constructor.
/// M9: Renamed from ModGameDataTabsViewModel, moved to DataViewer plugin.
/// </summary>
public partial class DataTableViewModel : ObservableObject
{
    private readonly IWorkspacePersistenceService _workspacePersistence;
    private readonly IConfigService _configService;
    private readonly ILogger _logger;
    private readonly IMessenger _messenger;

    // ── M9: State ownership (moved from View) ────────────────────────────
    /// <summary>Per-instance tabs. M9: moved from View to VM.</summary>
    public ObservableCollection<GameDataTypeTabItem> Tabs { get; } = [];
    /// <summary>Per-instance merge store. M9: moved from View to VM.</summary>
    public EntityMergeStore MergeStore { get; private set; } = new();
    /// <summary>Per-instance edit tracking store. M9: moved from View to VM.</summary>
    public EditTrackingStore EditStore { get; private set; } = new();
    /// <summary>Current ModInfo (set by View when StyledProperty changes). M9: moved from View to VM.</summary>
    public ModInfo? ModInfo { get; set; }
    /// <summary>Current ProfileInfo (set by View when StyledProperty changes). M9: moved from View to VM.</summary>
    public ProfileInfo? ProfileInfo { get; set; }

    /// <summary>
    /// Replace the current stores with cached ones (used by TabSnapshotCache restore).
    /// Clears existing data before swapping.
    /// </summary>
    public void ReplaceStores(EntityMergeStore mergeStore, EditTrackingStore editStore)
    {
        MergeStore = mergeStore;
        EditStore = editStore;
    }

    // ── Data loading service ─────────────────────────────────────────────
    private DataLoaderService? _dataLoader;
    /// <summary>Injectable DataLoaderService. Set by View after construction.</summary>
    public DataLoaderService? DataLoader
    {
        get => _dataLoader;
        set => _dataLoader = value;
    }

    // ── Command history (WAL) — single owner per N03 ────────────────────
    public CommandHistory CommandHistory { get; } = new();
    private int _persistSequence;
    private int _commandsSinceSnapshot;

    // ── Dirty state ──────────────────────────────────────────────────────
    private bool _isDirty;
    public bool IsViewDirty => _isDirty;
    private readonly HashSet<GameDataTypeTabItem> _dirtyTabs = [];

    // ── Observable properties (bound by View) ────────────────────────────
    [ObservableProperty] public partial bool CanUndo { get; set; }
    [ObservableProperty] public partial bool CanRedo { get; set; }
    [ObservableProperty] public partial bool CanAddRow { get; set; } = true;
    [ObservableProperty] public partial bool CanDeleteRow { get; set; } = true;

    // ── View callbacks (set by View after construction) ──────────────────
    // M9: Gradually replaced by direct VM state access.
    // Remaining callbacks that the View still needs to provide.
    public Func<(string targetType, int targetId)>? GetPersistenceTarget { private get; set; }
    public Func<IReadOnlyList<IEntity>>? CaptureAllEntities { private get; set; }
    public Action<Type>? OnMarkTabDirty { private get; set; }
    public Action? OnRefreshDataGrid { private get; set; }
    public Action? OnPushEditState { private get; set; }
    public Action? OnRebuildFilteredSources { private get; set; }
    public Action? OnClearDirtyTabsUi { private get; set; }
    public Action<HashSet<string>>? OnMarkSessionEntitiesDirty { private get; set; }
    /// <summary>M9: replaced by VM.ModInfo property. Kept for backward compat.</summary>
    [Obsolete("Use VM.ModInfo property directly.")]
    public Func<ModInfo?>? GetModInfo { private get; set; }
    /// <summary>M9: replaced by VM.ProfileInfo property. Kept for backward compat.</summary>
    [Obsolete("Use VM.ProfileInfo property directly.")]
    public Func<ProfileInfo?>? GetProfileInfo { private get; set; }
    public Func<bool>? IsReadOnly { private get; set; }
    /// <summary>M9: replaced by VM.Tabs property. Kept for backward compat.</summary>
    [Obsolete("Use VM.Tabs property directly.")]
    public Func<IReadOnlyList<GameDataTypeTabItem>>? GetTabs { private get; set; }

    // ── Auto-save ────────────────────────────────────────────────────────
    private Avalonia.Threading.DispatcherTimer? _autoSaveTimer;
    public event Func<SaveScope, Task>? SaveRequested;

    public DataTableViewModel(
        IWorkspacePersistenceService workspacePersistence,
        IConfigService configService,
        ILogger logger,
        IMessenger messenger)
    {
        _workspacePersistence = workspacePersistence;
        _configService = configService;
        _logger = logger;
        _messenger = messenger;

        CommandHistory.StateChanged += () =>
        {
            CanUndo = CommandHistory.CanUndo;
            CanRedo = CommandHistory.CanRedo;
        };

        CommandHistory.OnCommandPersist = OnCommandPersistAsync;
    }

    /// <summary>Called by View after setting all callbacks.</summary>
    public void Initialize()
    {
        _autoSaveTimer = new Avalonia.Threading.DispatcherTimer(
            TimeSpan.FromSeconds(30),
            Avalonia.Threading.DispatcherPriority.Background,
            OnAutoSaveTick);
        if (_configService.Config.AutoSaveInterval > 0)
            _autoSaveTimer.Start();

        _messenger.Register<SaveRequestedMessage>(this, (_, m) =>
        {
            if (IsReadOnly?.Invoke() == true || !_isDirty) return;
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                () => SaveRequested?.Invoke(m.Scope));
        });
    }

    // ── Dirty state management ──────────────────────────────────────────

    public void SetDirty(bool dirty)
    {
        _isDirty = dirty;
        _messenger.Send(new MergeViewDirtyChangedMessage(dirty));
    }

    public void MarkTabDirty(Type entityType)
    {
        var tab = Tabs.FirstOrDefault(t => t.EntityType == entityType);
        if (tab is not null)
        {
            _dirtyTabs.Add(tab);
            OnMarkTabDirty?.Invoke(entityType);
            SetDirty(true);
        }
        OnPushEditState?.Invoke();
    }

    public void MarkTabsDirtyFromEditedCells(HashSet<(string EntityId, string ColumnName)> editedCells)
    {
        var editedEntityIds = new HashSet<string>(editedCells.Select(c => c.EntityId));
        if (editedEntityIds.Count == 0) return;

        foreach (var tab in Tabs)
        {
            foreach (var item in tab.SourceCollection)
            {
                if (item is IEntity e && editedEntityIds.Contains(e.EntityId))
                {
                    _dirtyTabs.Add(tab);
                    OnMarkTabDirty?.Invoke(tab.EntityType);
                    break;
                }
            }
        }

        OnMarkSessionEntitiesDirty?.Invoke(editedEntityIds);
        _logger.LogInformation("[VM] MarkTabsDirtyFromEditedCells: {Count} entities across {TabCount} tabs",
            editedEntityIds.Count, _dirtyTabs.Count);
    }

    public void ClearDirtyTabs()
    {
        OnClearDirtyTabsUi?.Invoke();
        _dirtyTabs.Clear();
    }

    // ── Command execution ────────────────────────────────────────────────

    public void ExecuteCommand(IEditorCommand cmd) => CommandHistory.Execute(cmd);

    public void Undo()
    {
        CommandHistory.Undo();
        OnRefreshDataGrid?.Invoke();
    }

    public void Redo()
    {
        CommandHistory.Redo();
        OnRefreshDataGrid?.Invoke();
    }

    /// <summary>Restore a single command from persisted log without executing it.</summary>
    public void RestoreFromLog(IEditorCommand cmd) => CommandHistory.RestoreFromLog(cmd);

    // ── WAL persistence callback ─────────────────────────────────────────

    private async Task OnCommandPersistAsync(IEditorCommand cmd)
    {
        var (targetType, targetId) = GetPersistenceTarget?.Invoke() ?? ("", -1);
        if (targetId < 0)
        {
            var cmdModId = ExtractModIdFromCommand(cmd);
            if (cmdModId > 0)
            {
                targetType = "mod";
                targetId = cmdModId;
                _logger.LogInformation("[Persist] merge-editor fallback → mod:{ModId}", cmdModId);
            }
            else if (cmdModId == -1)
            {
                targetType = "game";
                targetId = 0;
                _logger.LogInformation("[Persist] merge-editor fallback → game:0 (ModId=-1)");
            }
            else
            {
                _logger.LogWarning("[Persist] SKIP — no persistence target");
                return;
            }
        }
        var seq = Interlocked.Increment(ref _persistSequence);
        _logger.LogInformation("[Persist] {TargetType}:{TargetId} seq={Seq} cmd={CmdType}",
            targetType, targetId, seq, cmd.GetType().Name);
        try
        {
            await _workspacePersistence.PersistCommandAsync(targetType, targetId, seq, cmd);
            _commandsSinceSnapshot++;
            CheckPeriodicSnapshot(targetType, targetId);
            _logger.LogInformation("[Persist] OK seq={Seq}", seq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Persist] FAIL seq={Seq}", seq);
        }
    }

    private static int ExtractModIdFromCommand(IEditorCommand cmd) => cmd switch
    {
        BatchEditCommand bec => bec.SourceModId,
        EditCellCommand ecc => ecc.SourceModId,
        _ => -1
    };

    private void CheckPeriodicSnapshot(string targetType, int targetId)
    {
        var interval = _configService.Config.SnapshotInterval;
        if (interval <= 0 || _commandsSinceSnapshot < interval) return;
        _commandsSinceSnapshot = 0;
        _logger.LogInformation("[Snapshot] taking snapshot for {TargetType}:{TargetId}", targetType, targetId);
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var entities = CaptureAllEntities?.Invoke()?.Where(e => e.ModId > 0).ToList();
            if (entities is null || entities.Count == 0) return;
            await _workspacePersistence.TakeSnapshotAsync(targetType, targetId, entities, _persistSequence);
            _logger.LogInformation("[Snapshot] done: {TargetType}:{TargetId} {Count} entities seq={Seq}",
                targetType, targetId, entities.Count, _persistSequence);
        });
    }

    // ── Restore from WAL ─────────────────────────────────────────────────

    public void InitPersistSequence(int maxSeq) => _persistSequence = Math.Max(_persistSequence, maxSeq);

    public void TrackRestoredCommand(int seq, IEditorCommand cmd)
    {
        CommandHistory.RestoreFromLog(cmd);
        _persistSequence = Math.Max(_persistSequence, seq);
    }

    // ── Save helpers ─────────────────────────────────────────────────────

    public async Task FlushCommandsAsync() => await CommandHistory.FlushAsync();

    public int PersistSequence { get => _persistSequence; set => _persistSequence = value; }
    public int CommandsSinceSnapshot { get => _commandsSinceSnapshot; set => _commandsSinceSnapshot = value; }

    public async Task UpdateSnapshotMarkerAsync(string targetType, int targetId)
        => await _workspacePersistence.UpdateSnapshotMarkerAsync(targetType, targetId, _persistSequence);

    public void ResetPersistenceState()
    {
        CommandHistory.Clear();
        _persistSequence = 0;
        _commandsSinceSnapshot = 0;
    }

    // ── Auto-save ────────────────────────────────────────────────────────

    private async void OnAutoSaveTick(object? sender, EventArgs e)
    {
        var interval = _configService.Config.AutoSaveInterval;
        if (interval <= 0)
        {
            _autoSaveTimer?.Stop();
            return;
        }
        if (_autoSaveTimer is not null)
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(interval);
        if (!_isDirty) return;
        _logger.LogInformation("[AutoSave] triggering");
        if (SaveRequested is not null)
            await SaveRequested.Invoke(SaveScope.All);
    }
}
