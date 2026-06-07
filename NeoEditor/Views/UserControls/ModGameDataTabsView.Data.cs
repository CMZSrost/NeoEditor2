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
    private async Task ReloadTabsAsync(ModInfo? modInfo)
    {
        IsLoading = true;
        var loadVersion = ++_loadVersion;
        Tabs.Clear();
        _commandHistory.Clear();
        CanUndo = false;
        CanRedo = false;
        // Clear stores directly for single-mod too
        MergeStore.Clear();
        EditStore.Clear();
        GenericDataGridHelper.ClearSubjectCache();
        ClearDirtyTabs();
        _selectedModId = null;
        FilterText = null;

        if (modInfo is null)
        {
            return;
        }

        // Activate stores for GDH bridge before the load loop
        PushEditStateToGrid(MergeStore, EditStore);

        await using var db = await _gameDbContextFactory.CreateDbContextAsync();
        foreach (var (_, entityType) in Constants.GameTypes.OrderBy(x => x.Key))
        {
            var items = await LoadEntitiesByTypeAsync(db, entityType, modInfo.ModId);
            if (loadVersion != _loadVersion)
            {
                return;
            }

            // Populate reference lookup for this entity type
            if (items.Count > 0)
                MergeStore.ReferenceLookups[entityType] = items.ToList();

            // Populate overlay chain data for single-mod mode
            foreach (var entity in items.OfType<IEntity>())
            {
                var idVal = ResolveEntityKeyProperty(entityType)?.GetValue(entity) is int i ? i : 0;
                MergeStore.OverlayChainDisplay[entity.EntityId] = new List<OverlayChainEntry>
                {
                    new(modInfo.Name, idVal, entityType, entity.EntityId, entity.Subject)
                };
                MergeStore.EntityModNames[entity.EntityId] = modInfo.Name;
            }

            Tabs.Add(new GameDataTypeTabItem
            {
                EntityType = entityType,
                Header = BuildHeader(entityType, items.Count),
                SourceCollection = items,
                ItemsSource = items
            });
        }

        _logger.LogInformation("[ReloadTabs] loaded {TabCount} tabs for mod '{ModName}'",
            Tabs.Count, modInfo.Name);
        _persistSequence = 0;
        _commandsSinceSnapshot = 0;
        await RestoreCommandsFromLogAsync();
        PushEditStateToGrid(MergeStore, EditStore);
        IsLoading = false;
        SelectFirstNonEmptyTab();
    }

    private async Task ShowSavePreviewAsync()
    {
        if (_isSavePreviewOpen || IsPreparingSavePreview) return;

        var modInfo = ModInfo;
        if (modInfo is null)
        {
            App.Notification.ShowWarning(Loc["ModGameDataSaveMissingModMessage"], Loc["Save"]);
            return;
        }

        _isSavePreviewOpen = true;
        SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewScanning"]);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(static () => { },
            Avalonia.Threading.DispatcherPriority.Render);

        try
        {
            var allEntities = CaptureCurrentTabEntities();
            var entitiesToSave = allEntities.Where(e => e.ModId > 0).ToList();

            SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewValidating"]);
            if (!await RunPreSaveValidationAsync(entitiesToSave))
                return;

            // Build diff from in-memory entities (no DB save yet)
            SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewExporting"]);
            var confirmed = await ExportEntitiesToXmlAsync(allEntities, [modInfo]);
            if (!confirmed) return;

            // User confirmed — now save to DB
            await SaveToDatabaseAsync(entitiesToSave, modInfo.ModId);

            SetDirty(false);
            ClearDirtyTabs();
            GenericDataGridHelper.EditedCells.Clear();
            GenericDataGridHelper.NewEntityIds.Clear();
            RefreshActiveDataGrid();
            _logger.LogInformation("[Save] saved {Count} entities to DB for mod '{ModName}'",
                entitiesToSave.Count, modInfo.Name);
            App.Notification.ShowSuccess(Loc["ModGameDataSaveSuccessMessage"], Loc["Save"]);
            AsyncHelper.FireAndForget(ClearWorkspaceAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save mod {ModId} ({ModName}).", modInfo.ModId, modInfo.Name);
            App.Notification.ShowError(Loc["ModGameDataSavePreviewFailedMessage", ex.Message], Loc["Error"]);
        }
        finally
        {
            SetSavePreviewPreparationState(false);
            _isSavePreviewOpen = false;
            UpdateSavePreviewUiState();
        }
    }

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
            var entitiesToSave = allEntities
                .Where(e => e.ModId > 0)
                .ToList();
            var modIds = allEntities.Select(e => e.ModId).Distinct().OrderBy(x => x).ToList();
            _logger.LogInformation(
                "[MergeSave] total={Total} modId>0={Saveable} modIds=[{ModIds}] tabCount={TabCount}",
                allEntities.Count, entitiesToSave.Count, string.Join(",", modIds), Tabs.Count);

            if (entitiesToSave.Count == 0)
            {
                App.Notification.ShowInfo($"No mod entities to save. Found {allEntities.Count} entities across {modIds.Count} modIds (saveable: requires ModId > 0).", "Merge View");
                return;
            }

            SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewValidating"]);
            if (!await RunPreSaveValidationAsync(entitiesToSave))
                return;

            // Build diff from in-memory entities (no DB save yet)
            SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewExporting"]);
            var affectedModIds = entitiesToSave.Select(e => e.ModId).Distinct().ToHashSet();
            var affectedMods = ProfileInfo.ModLoadInfos
                .Where(m => m.Info is not null && affectedModIds.Contains(m.Info.ModId))
                .Select(m => m.Info!)
                .ToList();

            if (affectedMods.Count == 0)
            {
                App.Notification.ShowInfo("No affected mods found for export.", "Merge View");
                return;
            }

            var confirmed = await ExportEntitiesToXmlAsync(allEntities, affectedMods);
            if (!confirmed) return;

            // User confirmed — now save to DB
            await SaveToDatabaseAsync(entitiesToSave);

            SetDirty(false);
            ClearDirtyTabs();
            GenericDataGridHelper.EditedCells.Clear();
            GenericDataGridHelper.NewEntityIds.Clear();
            RefreshActiveDataGrid();
            _logger.LogInformation("[MergeSave] saved {Count} entities to DB + exported XML", entitiesToSave.Count);

            App.Notification.ShowSuccess(Loc["ModGameDataSaveSuccessMessage"], Loc["Save"]);
            AsyncHelper.FireAndForget(ClearWorkspaceAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save merge view");
            App.Notification.ShowError($"Merge save failed: {ex.Message}", "Error");
        }
        finally
        {
            SetSavePreviewPreparationState(false);
            _isSavePreviewOpen = false;
            UpdateSavePreviewUiState();
        }
    }

    /// <summary>Validate only changed entities before save. Warnings show as non-blocking notification. Errors (rare) block save.</summary>
    private async Task<bool> RunPreSaveValidationAsync(List<IEntity> entities)
    {
        // Only validate entities that were actually modified
        var editedIds = new HashSet<string>(GenericDataGridHelper.EditedCells.Select(c => c.EntityId));
        editedIds.UnionWith(GenericDataGridHelper.NewEntityIds);
        var changedEntities = entities.Where(e => editedIds.Contains(e.EntityId)).ToList();

        if (changedEntities.Count == 0) return true;

        var svc = new Data.Validation.ValidationService();
        var report = svc.Validate(changedEntities);
        if (report.Entries.Count == 0) return true;

        // Errors: show blocking dialog (currently no rules produce Error, but keep the safety net)
        if (report.HasErrors)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return true;
            return await Views.Dialog.ValidationReportDialog.ShowAsync(owner, report);
        }

        // Warnings: non-blocking notification
        var msg = report.WarningCount == 1
            ? $"1 warning in changed entities: {report.Entries[0].Message}"
            : $"{report.WarningCount} warnings in {changedEntities.Count} changed entities.";
        App.Notification.ShowWarning(msg, "Validation");
        _messenger.Send(new ValidationCompletedMessage(report.WarningCount, 0));
        return true;
    }

    /// <summary>
    /// Builds XML diff from in-memory entities, shows preview, writes on confirmation.
    /// Returns true if user confirmed and files were written, false if cancelled.
    /// Does NOT save to DB — caller must handle DB persistence after confirmation.
    /// </summary>
    private async Task<bool> ExportEntitiesToXmlAsync(IReadOnlyList<IEntity> allEntities, List<ModInfo> mods)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null || mods.Count == 0) return false;

        var exportPlans = new List<(ModInfo Mod, string FilePath, string OldXml, string NewXml)>();
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
            _logger.LogInformation("[ExportXml] mod='{ModName}' files={FileCount} paths=[{Paths}]",
                modInfo.Name, fileGroups.Count,
                string.Join(", ", fileGroups.Select(g => g.Key ?? "(null)")));

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
                    exportPlans.Add((modInfo, fullPath, oldSnapshot, newSnapshot));
            }
        }

        _logger.LogInformation("[ExportXml] total export plans={Count}", exportPlans.Count);
        if (exportPlans.Count == 0)
        {
            App.Notification.ShowInfo("No differences between editor and disk XML files.", "XML Export");
            return false;
        }

        var exportItems = exportPlans.Select(p => new MergeXmlExportDialog.ExportItem(
            ModName: p.Mod.Name,
            FileName: System.IO.Path.GetFileName(p.FilePath),
            FilePath: p.FilePath,
            OldXml: p.OldXml,
            NewXml: p.NewXml
        )).ToList();

        var confirmedItems = await MergeXmlExportDialog.ShowAsync(owner, exportItems);

        if (confirmedItems is not null)
        {
            foreach (var item in confirmedItems)
            {
                await File.WriteAllTextAsync(item.FilePath, item.NewXml, Encoding.UTF8);
                _logger.LogInformation("[ExportXml] wrote {Path}", item.FilePath);
            }

            var exportedModIds = exportPlans.Select(p => p.Mod.ModId).Distinct().ToList();
            await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
            var now = DateTime.Now;
            foreach (var modId in exportedModIds)
            {
                var mod = await editorDb.ModInfos.FindAsync(modId);
                if (mod is not null) mod.LastImport = now;
            }
            await editorDb.SaveChangesAsync();

            App.Notification.ShowSuccess(
                $"Exported {confirmedItems.Count} file(s) across {exportedModIds.Count} mod(s).",
                "XML Export");
            return true;
        }
        else
        {
            _logger.LogInformation("[ExportXml] user cancelled — no files written");
            return false;
        }
    }

    /// <summary>
    /// Exports DB changes to XML files for the given mods.
    /// Loads entities from DB, compares against disk XML, shows diff, writes on confirmation.
    /// </summary>
    private async Task ExportXmlAsync(List<ModInfo> mods)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null || mods.Count == 0) return;

        await using var db = await _gameDbContextFactory.CreateDbContextAsync();
        var exportPlans = new List<(ModInfo Mod, string FilePath, string OldXml, string NewXml)>();
        _logger.LogInformation("[ExportXml] exporting {ModCount} mod(s)", mods.Count);

        foreach (var modInfo in mods)
        {
            var modDir = Path.GetFullPath(Path.Combine(_configService.Config.GameRootDir, modInfo.Path));

            // Load ALL entities for this mod from DB
            var dbEntities = new List<IEntity>();
            foreach (var (_, entityType) in Constants.GameTypes)
            {
                var entities = await LoadEntitiesByModAsync(db, entityType, modInfo.ModId);
                dbEntities.AddRange(entities);
            }

            _logger.LogInformation("[ExportXml] mod='{ModName}' (id={ModId}) dbEntities={Count}",
                modInfo.Name, modInfo.ModId, dbEntities.Count);
            if (dbEntities.Count == 0) continue;

            // Group by FilePath
            var fileGroups = dbEntities.GroupBy(e => e.FilePath).ToList();
            _logger.LogInformation("[ExportXml] mod='{ModName}' files={FileCount} paths=[{Paths}]",
                modInfo.Name, fileGroups.Count,
                string.Join(", ", fileGroups.Select(g => g.Key ?? "(null)")));
            foreach (var fileGroup in fileGroups)
            {
                var filePath = fileGroup.Key;
                var fullPath = string.IsNullOrWhiteSpace(filePath)
                    ? Path.Combine(modDir, "neogame.xml")
                    : (Path.IsPathRooted(filePath)
                        ? filePath
                        : Path.GetFullPath(Path.Combine(_configService.Config.GameRootDir, filePath)));

                // Disk snapshot
                var oldSnapshot = File.Exists(fullPath)
                    ? NormalizeXmlForDiff(LoadXmlSafe(fullPath).ToString(SaveOptions.None))
                    : "<!-- new file -->";

                // DB snapshot
                var exportedDoc = _xmlParser.Export(fileGroup, "neogame");
                exportedDoc.Declaration = null;
                var newSnapshot = NormalizeXmlForDiff(exportedDoc.ToString(SaveOptions.None));

                if (oldSnapshot != newSnapshot)
                    exportPlans.Add((modInfo, fullPath, oldSnapshot, newSnapshot));
            }
        }

        _logger.LogInformation("[ExportXml] total export plans={Count}", exportPlans.Count);
        if (exportPlans.Count == 0)
        {
            App.Notification.ShowInfo("No differences between DB and disk XML files.", "XML Export");
            return;
        }

        // Show multi-file diff dialog
        var exportItems = exportPlans.Select(p => new MergeXmlExportDialog.ExportItem(
            ModName: p.Mod.Name,
            FileName: System.IO.Path.GetFileName(p.FilePath),
            FilePath: p.FilePath,
            OldXml: p.OldXml,
            NewXml: p.NewXml
        )).ToList();

        var confirmedItems = await MergeXmlExportDialog.ShowAsync(owner, exportItems);

        if (confirmedItems is not null)
        {
            foreach (var item in confirmedItems)
            {
                await File.WriteAllTextAsync(item.FilePath, item.NewXml, Encoding.UTF8);
                _logger.LogInformation("[ExportXml] wrote {Path}", item.FilePath);
            }

            // Update LastImport timestamps
            var exportedModIds = exportPlans.Select(p => p.Mod.ModId).Distinct().ToList();
            await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
            var now = DateTime.Now;
            foreach (var modId in exportedModIds)
            {
                var mod = await editorDb.ModInfos.FindAsync(modId);
                if (mod is not null) mod.LastImport = now;
            }
            await editorDb.SaveChangesAsync();

            App.Notification.ShowSuccess(
                $"Exported {confirmedItems.Count} file(s) across {exportedModIds.Count} mod(s).",
                "XML Export");
        }
        else
        {
            _logger.LogInformation("[ExportXml] user cancelled — XML files not written");
            App.Notification.ShowInfo("XML export cancelled. DB changes are preserved.", "XML Export");
        }
    }

    private IReadOnlyList<IEntity> CaptureCurrentTabEntities()
    {
        return Tabs
            .SelectMany(tab => tab.SourceCollection)
            .OfType<IEntity>()
            .ToList();
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

    private async Task SaveToDatabaseAsync(IReadOnlyList<IEntity> entities, int? modIdFilter = null)
    {
        if (entities.Count == 0) return;

        await using var db = await _gameDbContextFactory.CreateDbContextAsync();

        // Remove deleted entities for a specific mod (if filter provided)
        if (modIdFilter.HasValue)
        {
            foreach (var (_, entityType) in Constants.GameTypes)
            {
                var currentIds = entities
                    .Where(e => e.GetType() == entityType)
                    .Select(e => e.EntityId)
                    .ToHashSet();

                var dbSet = db.GetDbSet(entityType);
                var dbEntities = ((IQueryable<IEntity>)dbSet)
                    .Where(e => e.ModId == modIdFilter.Value)
                    .AsEnumerable()
                    .Where(e => e.GetType() == entityType)
                    .ToList();

                var toDelete = dbEntities.Where(e => !currentIds.Contains(e.EntityId)).ToList();
                if (toDelete.Count > 0)
                {
                    db.RemoveRange(toDelete);
                }
            }
        }

        // Upsert entities
        foreach (var group in entities.GroupBy(e => e.GetType()))
        {
            var entityType = group.Key;
            var typedList = typeof(List<>).MakeGenericType(entityType);
            var addMethod = typedList.GetMethod("Add")!;
            var list = Activator.CreateInstance(typedList);
            foreach (var entity in group)
                addMethod.Invoke(list, [entity]);

            await db.DbBulkInsertOrUpdate(entityType, list!);
        }

        await db.SaveChangesAsync();

        // Update LastModified for affected mods
        var affectedModIds = entities.Select(e => e.ModId).Where(id => id > 0).Distinct().ToList();
        if (affectedModIds.Count > 0)
        {
            await using var editorDb = await _editorDbFactory.CreateDbContextAsync();
            var now = DateTime.Now;
            foreach (var modId in affectedModIds)
            {
                var mod = await editorDb.ModInfos.FindAsync(modId);
                if (mod is not null)
                {
                    mod.LastModified = now;
                }
            }
            await editorDb.SaveChangesAsync();
            _logger.LogInformation("[DB Save] updated LastModified for {Count} mods", affectedModIds.Count);
        }
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
        MergeStore.MergeSpaceModIds.Add(-1);
        EditStore.Clear();
        GenericDataGridHelper.ClearSubjectCache();
        ClearDirtyTabs();
        _selectedModId = null;
        FilterText = null;
        _overlayChains.Clear();
        _overriddenEntityIds = new HashSet<string>();

        // Ensure mod load infos are populated
        if (profileInfo.ModLoadInfos.Count == 0)
        {
            profileInfo.ModLoadInfos.Clear();
            foreach (var modLoad in App.ServiceProvider.GetRequiredService<IProfileManager>()
                         .LoadMods(profileInfo.Content))
                profileInfo.ModLoadInfos.Add(modLoad);
        }

        // Auto-load mods into DB if not already loaded (ensures merge view always has data)
        var modManager = App.ServiceProvider.GetRequiredService<IModManager>();
        foreach (var modLoad in profileInfo.ModLoadInfos)
        {
            _logger.LogInformation("[AutoLoad] mod namespace={Ns} info={HasInfo} modId={ModId}",
                modLoad.Namespace, modLoad.Info is not null, modLoad.Info?.ModId ?? -999);
            if (modLoad.Info is not null && modLoad.Info.ModId <= 0)
            {
                var modPath = System.IO.Path.Combine(_configService.Config.GameRootDir, modLoad.Info.Path);
                _logger.LogInformation("[AutoLoad] attempting import: path='{Path}' exists={Exists}",
                    modPath, System.IO.Directory.Exists(modPath));
                if (!string.IsNullOrEmpty(modLoad.Info.Path) && System.IO.Directory.Exists(modPath))
                {
                    var imported = await modManager.ImportModAsync(modPath);
                    if (imported is not null) modLoad.Info = imported;
                    _logger.LogInformation("[AutoLoad] import result: {Result}",
                        imported is not null ? $"success, new ModId={imported.ModId}" : "failed (null)");
                }
            }
            if (modLoad.Info is not null && modLoad.Info.ModId > 0)
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
                .Where(e => e.ModId > 0)
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
                GenericDataGridHelper.NamespaceToModName[ns] = entry.Info.Name;
        }
        modMeta[-1] = (-1, "Game", false);

        var allModIds = modMeta.Keys.ToList();
        _logger.LogInformation("[ReloadMergeTabs] querying {ModCount} modIds: [{Ids}]",
            allModIds.Count, string.Join(",", allModIds));

        await using var db = await _gameDbContextFactory.CreateDbContextAsync();

        // Delegate merge computation to MergeService
        var mergeService = App.ServiceProvider.GetRequiredService<IMergeService>();
        var mergeResult = await mergeService.ComputeMergeAsync(
            db, modMeta, allModIds,
            GenericDataGridHelper.NamespaceToModName,
            MergeStore.MergeSpaceModIds,
            ShowAllEntities);

        // Activate stores before copying results (so GDH writes delegate to MergeStore)
        PushEditStateToGrid(MergeStore, EditStore);

        // Copy merge results into BOTH MergeStore (for cache) AND GDH (for converters)
        foreach (var kv in mergeResult.EntityModNames)
        {
            MergeStore.EntityModNames[kv.Key] = kv.Value;
            GenericDataGridHelper.EntityModNames[kv.Key] = kv.Value;
        }
        foreach (var kv in mergeResult.OverlayChains)
        {
            MergeStore.OverlayChainDisplay[kv.Key] = kv.Value;
            GenericDataGridHelper.OverlayChainDisplay[kv.Key] = kv.Value;
        }
        foreach (var kv in mergeResult.FieldSources)
        {
            MergeStore.FieldSources[kv.Key] = kv.Value;
            GenericDataGridHelper.FieldSources[kv.Key] = kv.Value;
        }
        foreach (var fk in mergeResult.FieldConflicts)
        {
            MergeStore.FieldConflicts.Add(fk);
            GenericDataGridHelper.FieldConflicts.Add(fk);
        }
        foreach (var kv in mergeResult.EntityMergedIds)
        {
            MergeStore.EntityMergedIds[kv.Key] = kv.Value;
            GenericDataGridHelper.EntityMergedIds[kv.Key] = kv.Value;
        }
        foreach (var eid in mergeResult.OverriddenEntityIds)
        {
            _overriddenEntityIds.Add(eid);
            MergeStore.OverriddenEntityIds.Add(eid);
            GenericDataGridHelper.OverriddenEntityIds.Add(eid);
        }
        foreach (var kv in mergeResult.NamespaceToModName)
        {
            MergeStore.NamespaceToModName[kv.Key] = kv.Value;
            GenericDataGridHelper.NamespaceToModName[kv.Key] = kv.Value;
        }
        foreach (var mid in mergeResult.MergeSpaceModIds)
            MergeStore.MergeSpaceModIds.Add(mid);
        foreach (var kv in mergeResult.ReferenceLookups)
        {
            MergeStore.ReferenceLookups[kv.Key] = kv.Value;
            GenericDataGridHelper.ReferenceLookups[kv.Key] = kv.Value;
        }

        foreach (var typeData in mergeResult.Types)
        {
            if (loadVersion != _loadVersion) return;

            var allSource = new ObservableCollection<object>(typeData.AllEntities.Select(e => (object)e));
            var visibleItems = new ObservableCollection<object>(typeData.VisibleEntities.Select(e => (object)e));

            Tabs.Add(new GameDataTypeTabItem
            {
                EntityType = typeData.EntityType,
                Header = BuildHeader(typeData.EntityType, allSource.Count),
                SourceCollection = allSource,
                ItemsSource = visibleItems
            });

            _logger.LogInformation(
                "[ReloadMergeTabs] {EntityType}: source={SourceCount} visible={VisibleCount} overridden={OverriddenCount} showAll={ShowAll}",
                typeData.EntityType.Name, allSource.Count, visibleItems.Count, typeData.OverriddenCount, ShowAllEntities);
        }

        _logger.LogInformation(
            "[ReloadMergeTabs] completed: {TabCount} tabs, {TotalOverridden} overridden entities across all types",
            Tabs.Count, _overriddenEntityIds.Count);
        var cacheKey = $"profile_{profileInfo.ProfileId}";
        TabSnapshotCache[cacheKey] = (Tabs, MergeStore, EditStore);
        PopulateModFilterCombo(profileInfo);
        _persistSequence = 0;
        _commandsSinceSnapshot = 0;
        await RestoreCommandsFromLogAsync();
        PushEditStateToGrid(MergeStore, EditStore);
        IsLoading = false;
        SelectFirstNonEmptyTab();
        Dispatcher.UIThread.Post(() =>
        {
            RefreshIsEmptyMod();
        }, DispatcherPriority.Loaded);
    }

    private async Task<List<IEntity>> LoadEntitiesByModAsync(GameDbContext db, Type entityType, int modId)
    {
        var method = typeof(ModGameDataTabsView)
                         .GetMethod(nameof(LoadEntitiesByModTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");

        var task = method.Invoke(null, [db, modId]) as Task<List<IEntity>>;
        return task is not null ? await task : [];
    }

    private static async Task<List<IEntity>> LoadEntitiesByModTypedAsync<TEntity>(GameDbContext db, int modId)
        where TEntity : IEntity
    {
        return await db.Set<TEntity>()
            .Where(x => x.ModId == modId)
            .Cast<IEntity>()
            .ToListAsync();
    }

    private static async Task<List<IEntity>> LoadEntitiesByModIdsAsync(GameDbContext db, Type entityType, List<int> modIds)
    {
        var method = typeof(ModGameDataTabsView)
                         .GetMethod(nameof(LoadEntitiesByModIdsTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");
        var task = method.Invoke(null, [db, modIds]) as Task<List<IEntity>>;
        return task is not null ? await task : [];
    }

    private static async Task<List<IEntity>> LoadEntitiesByModIdsTypedAsync<TEntity>(GameDbContext db, List<int> modIds)
        where TEntity : IEntity
    {
        return await db.Set<TEntity>()
            .Where(x => modIds.Contains(x.ModId))
            .Cast<IEntity>()
            .ToListAsync();
    }

    /// <summary>Recalculate mergeIds. Merge space (Game + Merge mods) = business key;
    /// Insert space (Insert mods) = sequential starting from max merge key + 1.</summary>
    private void RecalculateMergeIds(Type entityType, IList<IEntity> allEntities)
    {
        var keyProp = ResolveEntityKeyProperty(entityType);
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
                GenericDataGridHelper.EntityMergedIds[entity.EntityId] =
                    keyProp?.GetValue(entity) is int ik ? ik : 0;
            else
                GenericDataGridHelper.EntityMergedIds[entity.EntityId] = nextInsertId++;
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
        if (DataTabs.SelectedItem is GameDataTypeTabItem activeTab)
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

            var analyzer = new Services.DependencyAnalysisService();
            var issues = analyzer.Analyze(allEntities, loadedModNames);

            if (issues.Count == 0) return;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var missing = issues.Count(i => !i.Issue.Contains("negation"));
                var negations = issues.Count - missing;
                var msg = missing > 0
                    ? $"{missing} unresolved reference(s), {negations} condition negation(s)."
                    : $"{negations} condition negation(s) (harmless).";
                App.Notification.ShowWarning(msg, "Dependency Check");
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
                DataTabs.SelectedIndex = i;
                break;
            }
        }
        if (Tabs.Count > 0 && DataTabs.SelectedIndex < 0) DataTabs.SelectedIndex = 0;

        // Set the shared DataGrid's DataContext + ItemsSource to the selected tab
        if (DataTabs.SelectedItem is GameDataTypeTabItem tab)
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
            App.Notification.ShowInfo(
                Loc["GameDataReadOnlyMessage"],
                Loc["GameDataReadOnly"]);

        var grid = FindActiveDataGrid();
        if (grid is null) return;
        grid.SelectionChanged -= OnDataGridSelectionChanged;
        grid.SelectionChanged += OnDataGridSelectionChanged;
        if (grid.SelectedItem is IEntity entity)
        {
            var entityType = entity.GetType().Name;
            _messenger.Send(new OverlayChainRequestedMessage(entity.EntityId, entity.Subject, entityType));
            _messenger.Send(new VisualEditorRequestedMessage(entity.GetType(), entity));
        }
    }

    private string BuildHeader(Type entityType, int count)
    {
        var title = Loc[entityType.Name];
        return $"{title} ({count})";
    }

    private async Task<ObservableCollection<object>> LoadEntitiesByTypeAsync(GameDbContext db, Type entityType, int modId)
    {
        var method = typeof(ModGameDataTabsView)
                         .GetMethod(nameof(LoadEntitiesByTypeTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");

        var task = method.Invoke(null, [db, modId]) as Task<ObservableCollection<object>>;
        if (task == null)
        {
            throw new InvalidOperationException($"Loading entity type {entityType.Name} did not return a task.");
        }

        return await task;
    }

    private static async Task<ObservableCollection<object>> LoadEntitiesByTypeTypedAsync<TEntity>(GameDbContext db, int modId)
        where TEntity : IEntity
    {
        var list = await db.Set<TEntity>()
            .Where(x => x.ModId == modId)
            .Cast<object>()
            .ToListAsync();
        return new ObservableCollection<object>(list);
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

