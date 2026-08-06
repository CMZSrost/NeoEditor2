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
using SearchableDataGrid = NeoEditor.Plugins.DataViewer.Views.SearchableDataGrid;
using NeoEditor.Plugins.DataViewer.Services;
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
using System.Xml.Linq;
using NeoEditor.Data.Command;
using NeoEditor.Data.Repository;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;


namespace NeoEditor.Views.UserControls;

public partial class ModGameDataTabsView
{
    private async Task ShowMergeSavePreviewAsync()
    {
        if (_isSavePreviewOpen || IsPreparingSavePreview || ProfileInfo is null)
            return;

        _isSavePreviewOpen = true;
        SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewScanning"]);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(static () => { },
            Avalonia.Threading.DispatcherPriority.Render);

        try
        {
            var allEntities = CaptureCurrentTabEntities();
            // Saveable = any mod (ModId >= 0); only the game base (ModId=-1) is read-only.
            // ModId=0 is a valid mod id — the old ModId>0 filter silently dropped its edits.
            var entitiesToSave = allEntities
                .Where(e => e.ModId >= 0)
                .ToList();
            var modIds = allEntities.Select(e => e.ModId).Distinct().OrderBy(x => x).ToList();
            _logger.LogInformation(
                "[MergeSave] total={Total} modId>=0={Saveable} modIds=[{ModIds}] tabCount={TabCount}",
                allEntities.Count, entitiesToSave.Count, string.Join(",", modIds), Tabs.Count);

            if (entitiesToSave.Count == 0)
            {
                ViewServices.Notification.ShowInfo(
                    $"No mod entities to save. Found {allEntities.Count} entities across {modIds.Count} modIds (saveable: requires ModId >= 0).",
                    "Merge View");
                return;
            }

            // B5: build the in-memory XML diff + preview dialog FIRST (R26: cancel = no DB write).
            SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewExporting"]);
            var affectedModIds = entitiesToSave.Select(e => e.ModId).Distinct().ToHashSet();
            var affectedMods = ProfileInfo.ModLoadInfos
                .Where(m => m.Info is not null && affectedModIds.Contains(m.Info.ModId))
                .Select(m => m.Info!)
                .ToList();

            if (affectedMods.Count == 0)
            {
                ViewServices.Notification.ShowInfo("No affected mods found for export.", "Merge View");
                return;
            }

            var confirmedItems = await BuildExportPreviewAsync(allEntities, affectedMods);
            if (confirmedItems is null) return;

            // Commit: Save (memory → DB via HostService), then export XML through HostService
            // (R24 — the view no longer writes mod XML files directly).
            await _hostService.SaveAllAsync();
            await UpdateLastModifiedAsync(affectedModIds);
            await _hostService.CommitExportAsync(confirmedItems.Select(i =>
                new RowDiff(i.FilePath, DiffKind.Modified, i.OldXml, i.NewXml)));

            // Docs/41 追修(C): the exported state becomes the shared baseline — advance the
            // entity tables and drop this profile's overlay (its edits are now in the XML).
            await _hostService.AdvanceBaselineAsync(entitiesToSave.Select(e => e.EntityId).ToList());
            await _workspacePersistence.ClearProfileEditsAsync(ProfileInfo.ProfileId,
                entitiesToSave.Select(e => e.EntityId));

            await UpdateExportTimestampsAsync(affectedMods.Select(m => m.ModId));
            // Docs/41: XML is now on disk — clear the persisted "not yet exported" markers.
            await _workspacePersistence.ClearPendingExportsAsync(affectedModIds);

            // All UI state changes MUST run on the UI thread.
            // (HostService already cleared the per-profile dirty set.)
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetDirty(false);
                ClearDirtyTabs();
                EditStore.EditedCells.Clear();
                EditStore.NewEntityIds.Clear();
                RefreshActiveDataGrid();
            });
            _logger.LogInformation("[MergeSave] saved {Count} entities to DB + exported XML", entitiesToSave.Count);

            ViewServices.Notification.ShowSuccess(Loc["ModGameDataSaveSuccessMessage"], Loc["Save"]);
            // Docs/41 P3: one-shot positive reinforcement after the first real export.
            if (_hintService.TryShow("first-export"))
                ViewServices.Notification.ShowInfo(Loc["OnboardingFirstExport"], Loc["Save"]);
            AsyncHelper.FireAndForget(ClearWorkspaceAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save merge view");
            ViewServices.Notification.ShowError($"Merge save failed: {ex.Message}", "Error");
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                SetSavePreviewPreparationState(false);
                _isSavePreviewOpen = false;
                UpdateSavePreviewUiState();
            });
        }
    }

    /// <summary>
    /// Builds XML diff from in-memory entities and shows the export preview dialog.
    /// Returns the confirmed export items (null if user cancelled or nothing differs).
    /// Does NOT write files or touch the DB — the caller commits after confirmation (R26: cancel = rollback).
    /// </summary>
    private async Task<List<MergeXmlExportDialog.ExportItem>?> BuildExportPreviewAsync(
        IReadOnlyList<IEntity> allEntities, List<ModInfo> mods)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null || mods.Count == 0) return null;

        var exportPlans = new List<(string ModName, string FilePath, string OldXml, string NewXml)>();
        _logger.LogInformation("[ExportXml] building diff from {Count} in-memory entities for {ModCount} mod(s)",
            allEntities.Count, mods.Count);

        foreach (var modInfo in mods)
        {
            var modDir = Path.GetFullPath(Path.Combine(_configService.Config.GameRootDir, modInfo.Path));
            var modEntities = allEntities.Where(e => e.ModId == modInfo.ModId).ToList();
            _logger.LogInformation("[ExportXml] mod='{ModName}' (id={ModId}) inMemEntities={Count}",
                modInfo.Name, modInfo.ModId, modEntities.Count);
            if (modEntities.Count == 0) continue;

            var fileGroups = modEntities.GroupBy(e => e.FilePath).ToList();
            foreach (var fileGroup in fileGroups)
            {
                var filePath = fileGroup.Key;
                var fullPath = string.IsNullOrWhiteSpace(filePath)
                    ? Path.Combine(modDir, "neogame.xml")
                    : (Path.IsPathRooted(filePath)
                        ? filePath
                        : Path.GetFullPath(Path.Combine(_configService.Config.GameRootDir, filePath)));

                var oldSnapshot = File.Exists(fullPath)
                    ? NormalizeXmlForDiff(LoadXmlSafe(fullPath).ToString(SaveOptions.None))
                    : "<!-- new file -->";

                var exportedDoc = _xmlParser.Export(fileGroup, "neogame");
                exportedDoc.Declaration = null;
                var newSnapshot = NormalizeXmlForDiff(exportedDoc.ToString(SaveOptions.None));

                if (oldSnapshot != newSnapshot)
                    exportPlans.Add((modInfo.Name, fullPath, oldSnapshot, newSnapshot));
            }
        }

        _logger.LogInformation("[ExportXml] total export plans={Count}", exportPlans.Count);
        if (exportPlans.Count == 0)
        {
            ViewServices.Notification.ShowInfo("No differences between editor and disk XML files.", "XML Export");
            return null;
        }

        var exportItems = exportPlans.Select(p => new MergeXmlExportDialog.ExportItem(
            ModName: p.ModName,
            FileName: System.IO.Path.GetFileName(p.FilePath),
            FilePath: p.FilePath,
            OldXml: p.OldXml,
            NewXml: p.NewXml
        )).ToList();

        var confirmedItems = await MergeXmlExportDialog.ShowAsync(owner, exportItems);
        if (confirmedItems is null)
        {
            _logger.LogInformation("[ExportXml] user cancelled — no files written");
            return null;
        }

        return confirmedItems;
    }

    /// <summary>
    /// Exports DB changes to XML files for the given mods (R26 Export action).
    /// The DB→XML diff comes from HostService/XmlRepository (a repository capability), not the View.
    /// </summary>
    private async Task ExportXmlAsync(List<ModInfo> mods)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null || mods.Count == 0) return;

        // B5: build per-mod DB→XML diffs through HostService (R26: diff is a repository capability).
        var exportItems = new List<MergeXmlExportDialog.ExportItem>();
        var exportedModIds = new List<int>();
        foreach (var modInfo in mods)
        {
            var results = await _hostService.ExportModAsync(modInfo.ModId);
            foreach (var r in results)
            {
                if (r.Files.Count == 0) continue;
                exportedModIds.Add(r.ModId);
                foreach (var f in r.Files)
                    exportItems.Add(new MergeXmlExportDialog.ExportItem(
                        ModName: modInfo.Name,
                        FileName: System.IO.Path.GetFileName(f.TargetId),
                        FilePath: f.TargetId,
                        OldXml: f.OldContent,
                        NewXml: f.NewContent));
            }
        }

        if (exportItems.Count == 0)
        {
            ViewServices.Notification.ShowInfo("No differences between DB and disk XML files.", "XML Export");
            return;
        }

        var confirmedItems = await MergeXmlExportDialog.ShowAsync(owner, exportItems);
        if (confirmedItems is null)
        {
            _logger.LogInformation("[ExportXml] user cancelled — XML files not written");
            ViewServices.Notification.ShowInfo("XML export cancelled. DB changes are preserved.", "XML Export");
            return;
        }

        // R24: write the confirmed XML files through the HostService export commit path.
        await _hostService.CommitExportAsync(confirmedItems.Select(i =>
            new RowDiff(i.FilePath, DiffKind.Modified, i.OldXml, i.NewXml)));

        await UpdateExportTimestampsAsync(exportedModIds);

        ViewServices.Notification.ShowSuccess(
            $"Exported {confirmedItems.Count} file(s) across {exportedModIds.Distinct().Count()} mod(s).",
            "XML Export");
    }

    private IReadOnlyList<IEntity> CaptureCurrentTabEntities()
    {
        return Tabs
            .SelectMany(tab => tab.SourceCollection)
            .OfType<IEntity>()
            .ToList();
    }

    /// <summary>Capture only entities from the currently active tab (R11: single-tab save).</summary>
    private IReadOnlyList<IEntity> CaptureSingleTabEntities()
    {
        var activeTab = GetActiveTab();
        if (activeTab is null) return [];
        return activeTab.SourceCollection.OfType<IEntity>().ToList();
    }

    private void SetSavePreviewPreparationState(bool isPreparing, string? statusText = null)
    {
        IsPreparingSavePreview = isPreparing;
        SavePreviewStatusText = isPreparing ? statusText : null;
        UpdateSavePreviewUiState();
    }

    private void UpdateSavePreviewUiState()
    {
        CanStartSavePreview = !_isSavePreviewOpen && !IsPreparingSavePreview;
        SavePreviewButtonText = IsPreparingSavePreview
            ? Loc["ModGameDataPreparingSavePreviewButton"]
            : Loc["Save"];
    }

    /// <summary>Bookkeeping: bump ModInfo.LastModified for the mods just saved to DB (non-entity write).</summary>
    private async Task UpdateLastModifiedAsync(IEnumerable<int> modIds)
    {
        var ids = modIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return;

        await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
        var now = DateTime.Now;
        foreach (var modId in ids)
        {
            var mod = await editorDb.ModInfos.FirstOrDefaultAsync(m => m.ModId == modId);
            if (mod is not null) mod.LastModified = now;
        }

        await editorDb.SaveChangesAsync();
        _logger.LogInformation("[DB Save] updated LastModified for {Count} mods", ids.Count);
    }

    /// <summary>Bookkeeping: bump ModInfo.LastImport for the mods just exported to XML (non-entity write).</summary>
    private async Task UpdateExportTimestampsAsync(IEnumerable<int> modIds)
    {
        var ids = modIds.Distinct().ToList();
        if (ids.Count == 0) return;

        await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
        var now = DateTime.Now;
        foreach (var modId in ids)
        {
            var mod = await editorDb.ModInfos.FirstOrDefaultAsync(m => m.ModId == modId);
            if (mod is not null) mod.LastImport = now;
        }

        await editorDb.SaveChangesAsync();
    }

    private async Task ReloadMergeTabsAsync(ProfileInfo profileInfo)
    {
        IsLoading = true;
        var loadVersion = ++_loadVersion;
        _logger.LogInformation("[ReloadMergeTabs] START profile='{ProfileName}' (id={ProfileId})",
            profileInfo.Name, profileInfo.ProfileId);
        Tabs.Clear();
        _commandHistory.Clear();
        CanUndo = false;
        CanRedo = false;
        // Clear MergeStore + EditStore directly (GDH may not have active store at this point)
        MergeStore.Clear();
        // B4: only include game data (ModId=-1) when the profile opts in via IncludeGame.
        if (profileInfo.IncludeGame)
            MergeStore.MergeSpaceModIds.Add(-1);
        EditStore.Clear();
        MergeStore.SubjectCache.Clear();
        ClearDirtyTabs();
        // R01: Clear global DirtyEntities before reload (Test Round 9 Bug 1).
        WorkspaceSession.ClearDirtyEntities();
        _selectedModId = null;
        FilterText = null;
        _overlayChains.Clear();
        _overriddenEntityIds = new HashSet<string>();

        // Ensure mod load infos are populated
        if (profileInfo.ModLoadInfos.Count == 0)
        {
            profileInfo.ModLoadInfos.Clear();
            foreach (var modLoad in _profileManager
                         .LoadMods(profileInfo.Content))
                profileInfo.ModLoadInfos.Add(modLoad);
        }

        // Auto-load mods into DB if not already loaded (ensures merge view always has data)
        var modManager = _modManager;
        foreach (var modLoad in profileInfo.ModLoadInfos)
        {
            _logger.LogInformation("[AutoLoad] mod namespace={Ns} info={HasInfo} modId={ModId}",
                modLoad.Namespace, modLoad.Info is not null, modLoad.Info?.ModId ?? -999);
            // "Needs import" = not yet persisted in the editor DB (Id is the autoincrement PK).
            // Never key this on ModId — ModId=0 is a valid business id (mods imported first hold it),
            // and keying on ModId<=0 re-imported them on every merge-view open, violating the UNIQUE
            // constraint on mod_info.Path and skipping their LoadModAsync (ModId 0 is not > 0).
            if (modLoad.Info is not null && modLoad.Info.Id <= 0)
            {
                // Skip game base data — already imported at startup with ModId=-1
                if (modLoad.Info.ModId == -1) continue;

                var modPath = System.IO.Path.Combine(_configService.Config.GameRootDir, modLoad.Info.Path);
                _logger.LogInformation("[AutoLoad] attempting import: path='{Path}' exists={Exists}",
                    modPath, System.IO.Directory.Exists(modPath));
                if (!string.IsNullOrEmpty(modLoad.Info.Path) && System.IO.Directory.Exists(modPath))
                {
                    // Namespace "0" = game base data → use ModId=-1
                    int? explicitModId = modLoad.Namespace == "0" ? -1 : null;
                    var imported = await modManager.ImportModAsync(modPath, modId: explicitModId);
                    if (imported is not null) modLoad.Info = imported;
                    _logger.LogInformation("[AutoLoad] import result: {Result}",
                        imported is not null ? $"success, new ModId={imported.ModId}" : "failed (null)");
                }
            }

            // Load data for any persisted mod (Id>0), including ModId=0 mods. The old ModId>0 gate
            // skipped them, so a ModId=0 mod (e.g. NSEaid) never had its data ensured via LoadModAsync.
            if (modLoad.Info is not null && modLoad.Info.Id > 0)
            {
                try
                {
                    await modManager.LoadModAsync(modLoad.Info);
                    _logger.LogInformation("[AutoLoad] LoadModAsync OK for ModId={ModId}", modLoad.Info.ModId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[AutoLoad] LoadModAsync failed for ModId={ModId}", modLoad.Info.ModId);
                }
            }
        }

        _logger.LogInformation("[AutoLoad] done. modEntries with Info: {Count}",
            profileInfo.ModLoadInfos.Count(m => m.Info is not null));

        // Collect all valid mod entries sorted by load order
        var modEntries = profileInfo.ModLoadInfos
            .Where(m => m.Info is not null)
            .Select((m, idx) => (Entry: m, LoadIndex: idx))
            .ToList();

        if (modEntries.Count == 0)
        {
            _logger.LogWarning("[ReloadMergeTabs] No mod entries found. Falling back to loading ALL mod data from DB.");
            // Fallback: load ALL non-game entities from DB regardless of profile
            await using var fallbackDb = await _gameDbContextFactory.CreateDbContextAsync();
            var allModIdsFromDb = await fallbackDb.AttackModes
                .Where(e => e.ModId >= 0)
                .Select(e => e.ModId)
                .Distinct()
                .ToListAsync();
            if (allModIdsFromDb.Count == 0)
            {
                _logger.LogWarning("[ReloadMergeTabs] No mod data in DB at all. Nothing to load.");
                IsLoading = false;
                return;
            }

            // Build synthetic mod entries from DB data
            foreach (var modId in allModIdsFromDb)
            {
                profileInfo.ModLoadInfos.Add(new ModLoadInfo
                {
                    Type = ModType.Merge,
                    Namespace = modId.ToString(),
                    Info = new ModInfo { ModId = modId, Name = $"Mod#{modId}", Path = "", IsBase = false }
                });
                MergeStore.MergeSpaceModIds.Add(modId);
            }

            // Rebuild modEntries with the synthetic entries
            modEntries = profileInfo.ModLoadInfos
                .Where(m => m.Info is not null)
                .Select((m, idx) => (Entry: m, LoadIndex: idx))
                .ToList();
        }

        // Precompute: modId → (LoadIndex, ModName, IsMerge)
        var modMeta = new Dictionary<int, (int LoadIndex, string Name, bool IsMerge)>();
        foreach (var (entry, idx) in modEntries)
        {
            modMeta[entry.Info.ModId] = (idx, entry.Info.Name, entry.Type == ModType.Merge);
            if (entry.Type == ModType.Merge)
                MergeStore.MergeSpaceModIds.Add(entry.Info.ModId);
            if (entry.Namespace is { Length: > 0 } ns && ns != "0")
                MergeStore.NamespaceToModName[ns] = entry.Info.Name;
        }

        // B4: only register game data (LoadIndex=-1, "Game") when IncludeGame is true.
        if (profileInfo.IncludeGame)
            modMeta[-1] = (-1, "Game", false);

        // Build ModId → strModName mapping from profile entries.
        // strModName (e.g. "0") determines entity namespace, NOT the mod directory name.
        var modIdToNs = new Dictionary<int, string> { [-1] = "0" };
        foreach (var (entry, _) in modEntries)
            if (entry.Namespace is { Length: > 0 } ns)
                modIdToNs[entry.Info.ModId] = ns;

        var allModIds = modMeta.Keys.ToList();
        _logger.LogInformation("[ReloadMergeTabs] querying {ModCount} modIds: [{Ids}]",
            allModIds.Count, string.Join(",", allModIds));

        await using var db = await _gameDbContextFactory.CreateDbContextAsync();

        // Delegate merge computation to MergeService
        var mergeService = _mergeService;
        var mergeResult = await mergeService.ComputeMergeAsync(
            db, modMeta, allModIds,
            MergeStore.NamespaceToModName,
            modIdToNs,
            MergeStore.MergeSpaceModIds,
            ShowAllEntities);

        // Activate stores before copying results (so GDH writes delegate to MergeStore)
        PushEditStateToGrid(MergeStore, EditStore);

        // Copy merge results into BOTH MergeStore (for cache) AND GDH (for converters)
        foreach (var kv in mergeResult.EntityModNames)
            MergeStore.EntityModNames[kv.Key] = kv.Value;
        foreach (var kv in mergeResult.EntityNamespaces)
            MergeStore.EntityNamespaces[kv.Key] = kv.Value;
        foreach (var kv in mergeResult.OverlayChains)
            MergeStore.OverlayChainDisplay[kv.Key] = kv.Value;
        foreach (var kv in mergeResult.FieldSources)
            MergeStore.FieldSources[kv.Key] = kv.Value;
        foreach (var fk in mergeResult.FieldConflicts)
            MergeStore.FieldConflicts.Add(fk);
        foreach (var kv in mergeResult.EntityMergedIds)
            MergeStore.EntityMergedIds[kv.Key] = kv.Value;
        foreach (var eid in mergeResult.OverriddenEntityIds)
        {
            _overriddenEntityIds.Add(eid);
            MergeStore.OverriddenEntityIds.Add(eid);
        }

        foreach (var kv in mergeResult.NamespaceToModName)
            MergeStore.NamespaceToModName[kv.Key] = kv.Value;
        foreach (var mid in mergeResult.MergeSpaceModIds)
            MergeStore.MergeSpaceModIds.Add(mid);
        foreach (var kv in mergeResult.ReferenceLookups)
            MergeStore.ReferenceLookups[kv.Key] = kv.Value;

        foreach (var typeData in mergeResult.Types)
        {
            if (loadVersion != _loadVersion) return;

            var allSource = new ObservableCollection<object>(typeData.AllEntities.Select(e => (object)e));
            var visibleItems = new ObservableCollection<object>(typeData.VisibleEntities.Select(e => (object)e));

            Tabs.Add(new GameDataTypeTabItem
            {
                EntityType = typeData.EntityType,
                Header = _dataLoader.BuildHeader(typeData.EntityType, allSource.Count),
                SourceCollection = allSource,
                ItemsSource = visibleItems
            });

            _logger.LogInformation(
                "[ReloadMergeTabs] {EntityType}: source={SourceCount} visible={VisibleCount} overridden={OverriddenCount} showAll={ShowAll}",
                typeData.EntityType.Name, allSource.Count, visibleItems.Count, typeData.OverriddenCount,
                ShowAllEntities);
        }

        _logger.LogInformation(
            "[ReloadMergeTabs] completed: {TabCount} tabs, {TotalOverridden} overridden entities across all types",
            Tabs.Count, _overriddenEntityIds.Count);

        // Docs/41 追修(C): merge THIS profile's edit overlay (per-column overrides + new /
        // deleted markers) into the loaded baseline view — multi-profile isolation: another
        // profile's edits live in its own overlay and never touch this view (or game.db).
        await ApplyProfileOverlayAsync();

        // R30 (追修 6): seed the HostService working-set cache with every loaded entity.
        // SaveAllAsync persists ONLY entities present in that cache (dirty id → cache miss
        // → silently dropped → "No mod entities to save" → WAL never cleared → replay on
        // every restart = dirty-on-open). Edit commands now carry cache deltas too, but a
        // never-edited entity (e.g. one restored from WAL replay) would still miss.
        foreach (var tab in Tabs)
        {
            foreach (var item in tab.SourceCollection)
            {
                if (item is IEntity e)
                    _hostService.AddEntityToCache(e);
            }
        }

        // P3 fix (Test Round 10): build in-memory ReferenceIndex so reference columns
        // in the DataGrid can resolve raw IDs to display names.
        // R30 (H1): MUST run BEFORE BuildMergeViewIndexAsync — the SQLite reverse index
        // resolves each segment through the in-memory index; building it first left
        // reference_reverse empty on first load.
        await MergeStore.Index.BuildAsync();

        // Build SQLite reference index for O(1) namespace-prefixed lookups
        await BuildMergeViewIndexAsync();

        var cacheKey = $"profile_{profileInfo.ProfileId}";
        TabSnapshotCache[cacheKey] = (Tabs, MergeStore, EditStore);
        PopulateModFilterCombo(profileInfo);
        _persistSequence = 0;
        _commandsSinceSnapshot = 0;
        await RestoreCommandsFromLogAsync();
        // Docs/41: restore the persisted "edited, not yet exported" set so row/cell
        // highlights (and green "new" rows) survive an app restart. NOT marked dirty —
        // those entities are already in game.db (auto-saved); dirty now means "not in DB".
        await RestorePendingExportsAsync();
        PushEditStateToGrid(MergeStore, EditStore);
        IsLoading = false;
        var totalEntities = Tabs.Sum(t => t.SourceCollection.Count);
        _messenger.Send(new DataLoadCompletedMessage(Tabs.Count, totalEntities));
        SelectFirstNonEmptyTab();
        Dispatcher.UIThread.Post(() => { RefreshIsEmptyMod(); }, DispatcherPriority.Loaded);
    }


    /// <summary>
    /// Docs/41: restore the persisted "edited, not yet exported" set into the EditStore so
    /// row/cell highlights survive a restart. Deliberately does NOT mark session-dirty —
    /// those entities are already persisted to game.db (auto-save); dirty now means
    /// "changed but not in the DB yet" (WAL window).
    /// </summary>
    private async Task RestorePendingExportsAsync()
    {
        var modIds = ProfileInfo?.ModLoadInfos
            .Where(m => m.Info is not null)
            .Select(m => m.Info!.ModId)
            .Distinct()
            .ToList() ?? [];
        var count = 0;
        var pendingRows = new List<(string EntityId, int ModId, string? ColumnName, bool IsNew)>();
        foreach (var modId in modIds)
        {
            var pending = await _workspacePersistence.GetPendingExportsAsync(modId);
            foreach (var (entityId, columnName, isNew) in pending)
            {
                // Docs/41 追修: per-column markers restore FIELD-level highlights; the "*"
                // wildcard fallback covers legacy rows persisted before per-column tracking.
                // New rows restore via NewEntityIds (green) and need no cell markers.
                if (isNew)
                    EditStore.NewEntityIds.Add(entityId);
                else if (columnName is not null)
                    EditStore.EditedCells.Add((entityId, columnName));
                else
                    EditStore.EditedCells.Add((entityId, "*"));
                pendingRows.Add((entityId, modId, columnName, isNew));
                count++;
            }
        }

        if (count > 0)
            _logger.LogInformation("[RestorePending] {Count} not-yet-exported entities restored", count);

        // Docs/41 追修: validate every marker against the game XML on disk — an entity that
        // was edited then reverted (or changed back externally) no longer differs from the
        // XML, so its marker is stale. Real diffs are rewritten per column (this also
        // upgrades legacy NULL-column rows); stale markers are removed (memory + DB);
        // anything unresolvable (new entities, missing files) is kept conservatively.
        if (pendingRows.Count > 0)
            await ValidatePendingMarkersAsync(pendingRows);
    }

    /// <summary>
    /// Docs/41 追修: DB-vs-XML validation of pending-export markers. The entity was never
    /// exported, so the game XML still holds the ORIGINAL values — diff it against the
    /// current (DB) entity:
    /// - diff &gt; 0 → rewrite the marker with the exact changed columns (in-memory + DB);
    /// - diff == 0 → the edit was undone / reverted — remove the stale marker;
    /// - unresolvable (new entities, missing files, parse errors) → keep as-is.
    /// </summary>
    private async Task ValidatePendingMarkersAsync(
        IReadOnlyList<(string EntityId, int ModId, string? ColumnName, bool IsNew)> pendingRows)
    {
        // Loaded entities give the concrete type + FilePath + current values.
        var entitiesById = new Dictionary<string, IEntity>();
        foreach (var tab in Tabs)
            foreach (var item in tab.SourceCollection)
                if (item is IEntity e && !entitiesById.ContainsKey(e.EntityId))
                    entitiesById[e.EntityId] = e;

        var importMethod = typeof(IXmlParser).GetMethod(nameof(IXmlParser.ImportEntities))!;
        // One file may hold many pending entities — parse it once, reuse for all of them.
        var originalsByFile = new Dictionary<string, Dictionary<string, IEntity>>();

        foreach (var (entityId, modId, _, isNew) in pendingRows)
        {
            if (isNew) continue; // created this session — not in the XML, keep the green marker
            try
            {
                if (!entitiesById.TryGetValue(entityId, out var current)) continue;
                var fullPath = ResolveLegacyXmlPath(current.FilePath);
                if (fullPath is null || !File.Exists(fullPath)) continue;

                if (!originalsByFile.TryGetValue(fullPath, out var originals))
                {
                    originals = LoadOriginalsFromFile(importMethod, current.GetType(), modId, fullPath);
                    originalsByFile[fullPath] = originals;
                }
                if (!originals.TryGetValue(entityId, out var original)) continue; // not in XML — keep

                var columns = DiffEngine.ComputeChangedColumns(original, current);
                if (columns.Count == 0)
                {
                    // Edit was undone / value reverted — the marker is stale: drop it.
                    EditStore.EditedCells.RemoveWhere(c => c.EntityId == entityId);
                    await _workspacePersistence.RemovePendingExportEntityAsync(modId, entityId);
                    _logger.LogInformation(
                        "[PendingValidate] {Eid} matches the XML — stale marker removed", entityId);
                    continue;
                }

                // Real diff → rewrite with the exact columns (replaces "*" or old columns,
                // and upgrades legacy NULL-column rows permanently).
                EditStore.EditedCells.RemoveWhere(c => c.EntityId == entityId);
                foreach (var col in columns)
                    EditStore.EditedCells.Add((entityId, col));
                await _workspacePersistence.RemovePendingExportEntityAsync(modId, entityId);
                await _workspacePersistence.UpsertPendingExportsAsync(modId,
                    columns.Select(col => (entityId, (string?)col, false)));
                _logger.LogInformation(
                    "[PendingValidate] {Eid} rewritten as {ColumnCount} column(s): [{Columns}]",
                    entityId, columns.Count, string.Join(", ", columns));
            }
            catch (Exception ex)
            {
                // Never drop dirty state on a validation hiccup.
                _logger.LogWarning(ex, "[PendingValidate] failed for {Eid} — keeping marker", entityId);
            }
        }
    }

    /// <summary>Parse a source XML file once and index its entities by EntityId.</summary>
    private Dictionary<string, IEntity> LoadOriginalsFromFile(MethodInfo importMethod,
        Type entityType, int modId, string fullPath)
    {
        var doc = LoadXmlSafe(fullPath);
        var generic = importMethod.MakeGenericMethod(entityType);
        var list = (IList)generic.Invoke(_xmlParser, new object[] { doc, modId, fullPath })!;
        var map = new Dictionary<string, IEntity>();
        foreach (var item in list)
            if (item is IEntity e)
                map[e.EntityId] = e;
        return map;
    }

    /// <summary>Resolve an entity's source XML file: absolute paths are used as-is, relative
    /// paths are joined onto the game root (mirrors XmlRepository.ResolveFilePath).</summary>
    private string? ResolveLegacyXmlPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        if (Path.IsPathRooted(filePath)) return filePath;
        var root = _configService.Config.GameRootDir;
        return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(Path.Combine(root, filePath));
    }


    /// <summary>
    /// Docs/41 追修(C): merge this profile's edit overlay into the loaded baseline view.
    /// Column overrides are applied to the matching entities, deleted entities are removed,
    /// and entities created in this profile are rebuilt from their overlay rows. Runs after
    /// the merge tabs are built and BEFORE the HostService cache seeding / WAL restore.
    /// </summary>
    private async Task ApplyProfileOverlayAsync()
    {
        var profileId = ProfileInfo?.ProfileId ?? -1;
        if (profileId < 0) return;
        var edits = await _workspacePersistence.GetProfileEditsAsync(profileId);
        if (edits.Count == 0) return;

        var entitiesById = new Dictionary<string, IEntity>();
        foreach (var tab in Tabs)
            foreach (var item in tab.SourceCollection)
                if (item is IEntity e && !entitiesById.ContainsKey(e.EntityId))
                    entitiesById[e.EntityId] = e;

        var deleted = edits.Where(e => e.IsDeleted).Select(e => e.EntityId).ToHashSet();
        var created = edits.Where(e => e.IsNew && e.ColumnName is null).ToList();
        var overrides = edits.Where(e => e.ColumnName is not null).ToList();

        // 1) Deleted entities → remove from the source collections (ItemsSource is a
        // filtered view rebuilt below).
        if (deleted.Count > 0)
        {
            foreach (var tab in Tabs)
            {
                for (var i = tab.SourceCollection.Count - 1; i >= 0; i--)
                    if (tab.SourceCollection[i] is IEntity e && deleted.Contains(e.EntityId))
                        tab.SourceCollection.RemoveAt(i);
            }
        }

        // 2) Entities created in this profile → rebuild from their overlay rows.
        foreach (var marker in created)
        {
            if (string.IsNullOrWhiteSpace(marker.EntityType)) continue;
            if (!Constants.GameTypes.TryGetValue(marker.EntityType, out var type)) continue;

            var entity = (IEntity)Activator.CreateInstance(type)!;
            entity.EntityId = marker.EntityId;
            entity.ModId = marker.ModId;
            ApplyColumnOverrides(entity, overrides.Where(o => o.EntityId == marker.EntityId).ToList());

            var tab = Tabs.FirstOrDefault(t => t.EntityType == type);
            if (tab is null) continue;
            tab.SourceCollection.Add(entity);
            _hostService.AddEntityToCache(entity);
            EditStore.NewEntityIds.Add(marker.EntityId);
        }

        // 3) Column overrides on existing entities.
        foreach (var group in overrides.GroupBy(o => o.EntityId))
        {
            if (!entitiesById.TryGetValue(group.Key, out var entity)) continue;
            ApplyColumnOverrides(entity, group.ToList());
        }

        if (deleted.Count > 0 || created.Count > 0)
            RebuildFilteredItemsSources();

        _logger.LogInformation(
            "[ProfileOverlay] applied {EditCount} overlay rows for profile {ProfileId} " +
            "(deleted={Deleted}, created={Created}, overrides={Overrides})",
            edits.Count, profileId, deleted.Count, created.Count, overrides.Count);
    }

    /// <summary>Apply raw-text overlay rows to an entity's [Column] properties.</summary>
    private void ApplyColumnOverrides(IEntity entity, IReadOnlyList<ProfileEdit> rows)
    {
        var type = entity.GetType();
        foreach (var row in rows)
        {
            if (row.ColumnName is null) continue;
            var prop = type.GetProperties().FirstOrDefault(p =>
                p.GetCustomAttribute<ColumnAttribute>()?.Name == row.ColumnName);
            if (prop is null || !prop.CanWrite) continue;
            try
            {
                var raw = row.RawValue ?? "";
                object? value = raw;
                if (prop.PropertyType == typeof(int)) value = int.Parse(raw);
                else if (prop.PropertyType == typeof(long)) value = long.Parse(raw);
                else if (prop.PropertyType == typeof(float)) value = float.Parse(raw);
                else if (prop.PropertyType == typeof(double)) value = double.Parse(raw);
                else if (prop.PropertyType == typeof(bool)) value = bool.Parse(raw);
                else if (prop.PropertyType.IsEnum) value = Enum.ToObject(prop.PropertyType, int.Parse(raw));
                else if (prop.PropertyType == typeof(ReferenceList<IReferenceEntry>))
                    value = ViewServices.Get<IReferenceListSerializer>()
                        .Deserialize(raw, prop.GetCustomAttribute<ReferenceFieldAttribute>());
                prop.SetValue(entity, value);
            }
            catch
            {
                // Skip unparseable overlay values — never crash the load.
            }
        }
    }

    /// <summary>
    /// Build the SQLite reference_index (in-memory) for the merge view.
    /// Populates entity_type, namespace, pk, entity_id, group_id, subgroup_id.
    /// INSERT OR REPLACE semantics: Game (ns="0") first, then mods in load order.
    /// </summary>
    private async Task BuildMergeViewIndexAsync()
    {
        MergeStore.EnsureIndexService();
        var index = MergeStore.IndexService!;
        index.Clear();

        var entries = new List<ReferenceIndexService.IndexEntry>();

        foreach (var tab in Tabs)
        {
            var entityType = tab.EntityType;
            var typeName = entityType.Name;
            var keyProp = DataLoaderService.ResolveEntityKeyProperty(entityType);
            if (keyProp is null) continue;

            var groupIdProp = entityType.GetProperty("GroupId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            var subgroupIdProp = entityType.GetProperty("SubgroupId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            foreach (var obj in tab.SourceCollection)
            {
                if (obj is not IEntity entity) continue;
                var pk = keyProp.GetValue(entity)?.ToString();
                if (pk is null) continue;

                MergeStore.EntityNamespaces.TryGetValue(entity.EntityId, out var ns);
                ns ??= "0";

                int? gid = null, sid = null;
                if (groupIdProp is not null && subgroupIdProp is not null)
                {
                    if (groupIdProp.GetValue(entity) is int g) gid = g;
                    if (subgroupIdProp.GetValue(entity) is int s) sid = s;
                }

                entries.Add(new ReferenceIndexService.IndexEntry(
                    typeName, ns, pk, entity.EntityId, gid, sid));
            }
        }

        // Sort entries: Game (ns="0") first within each type, then mods by load order.
        // This ensures INSERT OR REPLACE correctly implements merge override.
        entries.Sort((a, b) =>
        {
            var typeCmp = string.CompareOrdinal(a.EntityType, b.EntityType);
            if (typeCmp != 0) return typeCmp;
            var nsA = a.Namespace == "0" ? 0 : 1;
            var nsB = b.Namespace == "0" ? 0 : 1;
            return nsA.CompareTo(nsB);
        });

        await index.BuildAsync(entries);

        // Build reverse reference index
        await ReferenceResolver.BuildReverseIndexAsync(index, MergeStore);

        _logger.LogInformation(
            "[BuildMergeIndex] {Count} entries in reference_index for {TabCount} types",
            index.Count, Tabs.Count);
    }

    /// <summary>Recalculate mergeIds. Merge space (Game + Merge mods) = business key;
    /// Insert space (Insert mods) = sequential starting from max merge key + 1.</summary>
    private void RecalculateMergeIds(Type entityType, IList<IEntity> allEntities)
    {
        var keyProp = DataLoaderService.ResolveEntityKeyProperty(entityType);
        bool InMergeSpace(IEntity e) => MergeStore.MergeSpaceModIds.Contains(e.ModId);

        var maxMergeKey = allEntities
            .Where(InMergeSpace)
            .Select(e => keyProp?.GetValue(e))
            .OfType<int>()
            .DefaultIfEmpty(0)
            .Max();
        var nextInsertId = maxMergeKey + 1;

        foreach (var entity in allEntities
                     .OrderBy(e => _entityLoadIndex.TryGetValue(e, out var idx) ? idx : 999)
                     .ThenBy(e => keyProp?.GetValue(e) is int k ? k : 0))
        {
            if (InMergeSpace(entity))
                MergeStore.EntityMergedIds[entity.EntityId] =
                    keyProp?.GetValue(entity) is int ik ? ik : 0;
            else
                MergeStore.EntityMergedIds[entity.EntityId] = nextInsertId++;
        }
    }

    private static string GetEntityKey(IEntity entity, PropertyInfo? keyProp)
    {
        if (keyProp is null) return entity.EntityId;
        return keyProp.GetValue(entity)?.ToString() ?? entity.EntityId;
    }

    /// <summary>
    /// Rebuilds each merge-view tab's ItemsSource from SourceCollection,
    /// applying the current ShowAll / overridden filter.
    /// ItemsSource is replaced as a new ObservableCollection to trigger DataGrid rebind.
    /// </summary>
    private void RebuildFilteredItemsSources()
    {
        _logger.LogInformation(
            "[RebuildFilter] showAll={ShowAll}, tabs={TabCount}, overriddenIds={OverriddenCount}, modFilter={ModFilter}, textFilter='{FilterText}'",
            ShowAllEntities, Tabs.Count, _overriddenEntityIds.Count, _selectedModId, FilterText);

        var activeGrid = SharedDataGrid;
        var savedSortProp = activeGrid?.CurrentSortProperty;
        var savedSortDir = activeGrid?.CurrentSortDirection ?? System.ComponentModel.ListSortDirection.Ascending;

        foreach (var tab in Tabs)
        {
            // For single mod view with no filter text, restore original SourceCollection
            if (!IsMergeView && string.IsNullOrEmpty(FilterText))
            {
                tab.ItemsSource = tab.SourceCollection;
                continue;
            }

            var filtered = ApplyAllFilters(tab.SourceCollection, tab.EntityType);

            tab.ItemsSource = filtered;

            _logger.LogInformation(
                "[RebuildFilter] {EntityType}: source={SourceCount} → visible={VisibleCount}",
                tab.EntityType.Name, tab.SourceCollection.Count, filtered.Count);
        }

        // Update the active DataGrid to show the current tab's filtered data
        if (TabListBox.SelectedItem is GameDataTypeTabItem activeTab)
            SharedDataGrid.ItemsSource = activeTab.ItemsSource;

        if (savedSortProp is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var grid = SharedDataGrid;
                if (grid is not null)
                {
                    var gridType = typeof(SearchableDataGrid);
                    var propField = gridType.GetField("_lastSortProperty",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var dirField = gridType.GetField("_lastDirection",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    propField?.SetValue(grid, savedSortProp);
                    dirField?.SetValue(grid, savedSortDir);
                    grid.ReapplySort();
                }
            }, DispatcherPriority.Background);
        }

        RefreshIsEmptyMod();
    }

    private ObservableCollection<object> ApplyAllFilters(ObservableCollection<object> source, Type entityType)
    {
        return _filterService.ApplyFilters(source, entityType,
            IsMergeView, ShowAllEntities, _overriddenEntityIds, _selectedModId, FilterText);
    }

    private sealed record ModFilterItem(int? ModId, string Label)
    {
        public override string ToString() => Label;
    }

    private void PopulateModFilterCombo(ProfileInfo profileInfo)
    {
        var items = new ObservableCollection<ModFilterItem>
        {
            new(null, Loc["ModFilterAll"]),
            new(-1, Loc["Games"])
        };
        foreach (var m in profileInfo.ModLoadInfos.Where(m => m.Info is not null))
            items.Add(new ModFilterItem(m.Info.ModId, m.Info.Name));

        _selectedModId = null;
        ModFilterCombo.ItemsSource = items;
        ModFilterCombo.SelectedIndex = 0;
    }

    private async Task RunDependencyAnalysis(ProfileInfo profileInfo)
    {
        try
        {
            var allEntities = Tabs.SelectMany(t => t.SourceCollection.OfType<IEntity>()).ToList();
            var loadedModNames = new HashSet<string>(
                profileInfo.ModLoadInfos.Where(m => m.Info is not null).Select(m => m.Info!.Name));
            loadedModNames.Add("Game");

            var analyzer = new Services.DependencyAnalysisService(WorkspaceSession, ReferenceResolver);
            var issues = analyzer.Analyze(allEntities, loadedModNames);

            if (issues.Count == 0) return;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var missing = issues.Count(i => !i.Issue.Contains("negation"));
                var negations = issues.Count - missing;
                var msg = missing > 0
                    ? $"{missing} unresolved reference(s), {negations} condition negation(s)."
                    : $"{negations} condition negation(s) (harmless).";
                ViewServices.Notification.ShowWarning(msg, "Dependency Check");
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dependency analysis failed");
        }
    }

    private void SelectFirstNonEmptyTab()
    {
        for (var i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].SourceCollection.Count > 0)
            {
                TabListBox.SelectedIndex = i;
                break;
            }
        }

        if (Tabs.Count > 0 && TabListBox.SelectedIndex < 0) TabListBox.SelectedIndex = 0;

        // Set the shared DataGrid's DataContext + ItemsSource to the selected tab
        if (TabListBox.SelectedItem is GameDataTypeTabItem tab)
        {
            SharedDataGrid.DataContext = tab;
            SharedDataGrid.ItemsSource = tab.ItemsSource;
        }

        RefreshIsEmptyMod();

        Avalonia.Threading.Dispatcher.UIThread.Post(() => WireActiveGridSelection(),
            Avalonia.Threading.DispatcherPriority.Background);
    }

    private void WireActiveGridSelection()
    {
        // Wire CanEditEntity hook on the single shared DataGrid
        SharedDataGrid.CanEditEntity = entity => !IsMergeView || entity.ModId != -1;
        SharedDataGrid.OnEditBlocked = _ =>
            ViewServices.Notification.ShowInfo(
                Loc["GameDataReadOnlyMessage"],
                Loc["GameDataReadOnly"]);

        var grid = FindActiveDataGrid();
        if (grid is null) return;
        grid.SelectionChanged -= OnDataGridSelectionChanged;
        grid.SelectionChanged += OnDataGridSelectionChanged;
        // R15: double-click on row → open Center EntityEditorDocument
        grid.DoubleTapped -= OnDataGridDoubleTapped;
        grid.DoubleTapped += OnDataGridDoubleTapped;
        if (grid.SelectedItem is IEntity entity)
        {
            var entityType = entity.GetType().Name;
            _messenger.Send(new OverlayChainRequestedMessage(entity.EntityId, entity.Subject, entityType));
            _messenger.Send(new VisualEditorRequestedMessage(entity.GetType(), entity));
            _messenger.Send(new EntitySelectedMessage(entity, SelectSource.BottomDataGrid));
        }
    }

    private static string NormalizeXmlForDiff(string xml)
    {
        try
        {
            // Strip <?xml ...?> declaration line to prevent spurious diffs on every save
            if (xml.StartsWith("<?"))
            {
                var endIndex = xml.IndexOf("?>", StringComparison.Ordinal);
                if (endIndex >= 0)
                    xml = xml.Substring(endIndex + 2).TrimStart('\r', '\n');
            }

            var doc = XDocument.Parse(xml);
            return doc.ToString(SaveOptions.None);
        }
        catch
        {
            return xml;
        }
    }

    private string BuildDiffText(string oldSnapshot, string newSnapshot)
    {
        var tempDirectory =
            Path.Combine(Path.GetTempPath(), "NeoEditor", "ModGameDataDiff", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var oldSnapshotPath = Path.Combine(tempDirectory, "old.xml");
        var newSnapshotPath = Path.Combine(tempDirectory, "new.xml");

        try
        {
            File.WriteAllText(oldSnapshotPath, oldSnapshot, new UTF8Encoding(false));
            File.WriteAllText(newSnapshotPath, newSnapshot, new UTF8Encoding(false));
            try
            {
                return XmlCompareHelper.Compare(oldSnapshotPath, newSnapshotPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "XmlCompareHelper failed while generating save preview diff text. Falling back to current snapshot text.");
                return newSnapshot;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch
            {
                // Ignore temp cleanup errors.
            }
        }
    }

    private static XDocument LoadXmlSafe(string path)
    {
        var text = File.ReadAllText(path);
        if (text.Contains("encoding=\"utf8\"", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("encoding=\"utf8\"", "encoding=\"utf-8\"", StringComparison.OrdinalIgnoreCase);
        return XDocument.Parse(text);
    }
}