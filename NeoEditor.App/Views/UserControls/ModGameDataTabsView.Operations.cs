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
using NeoEditor.Plugins.DataViewer.Services;
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
    private async void OnExportXmlClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfileInfo is not null)
        {
            var mods = ProfileInfo.ModLoadInfos
                .Where(m => m.Info is not null)
                .Select(m => m.Info)
                .ToList();
            await ExportXmlAsync(mods);
        }
    }

    private async Task QuickSaveAsync(SaveScope saveScope = SaveScope.All)
    {
        if (_isSavePreviewOpen || IsPreparingSavePreview) return;

        // Flush any in-flight WAL persists so the captured entity state reflects
        // all pending edits (e.g. XML editor changes that were just applied).
        await _commandHistory.FlushAsync();

        _isSavePreviewOpen = true;
        SetSavePreviewPreparationState(true, Loc["Saving"]);

        try
        {
            // B5: DB persistence via HostService (R26 Save action). HostService saves the
            // per-profile dirty entities and clears the dirty set; the View only does UI cleanup.
            var save = await _hostService.SaveAllAsync();
            var savedEntityIds = save.SavedEntityIds.ToHashSet();

            if (savedEntityIds.Count == 0)
            {
                // Docs/41: auto-save already persisted everything — "nothing to save" is the
                // NORMAL state now (Ctrl+S after auto-save), not an error. Surface a warning
                // only when dirty entities exist but could not be persisted (cache miss,
                // see HostService R30 fix: dirty id → cache miss → silently dropped).
                if (WorkspaceSession.DirtyEntities.Count > 0)
                {
                    ViewServices.Notification.ShowInfo(
                        "Entities are dirty but could not be saved to the database — see the log for cache-miss warnings.",
                        Loc["Save"]);
                }
                else
                {
                    _logger.LogDebug("[QuickSave] nothing to save — all edits already auto-saved");
                }

                return;
            }

            // All UI state changes MUST run on the UI thread.
            // After SaveAllAsync, the continuation may be on a thread-pool thread,
            // and Avalonia bindings (title *, KV yellow bar, DataGrid cell highlights)
            // won't pick up PropertyChanged notifications fired from background threads.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (saveScope == SaveScope.CurrentTab)
                {
                    // R11: only clear the saved tab's dirty state, preserving other tabs' edits.
                    var activeTab = GetActiveTab();
                    if (activeTab is not null)
                    {
                        _dirtyTabs.Remove(activeTab);
                        activeTab.ClearDirty();
                    }
                    SetDirty(_dirtyTabs.Count > 0);
                }
                else
                {
                    SetDirty(false);
                    ClearDirtyTabs();
                }
                // Docs/41 P1: auto/quick save is a DB-only cache write — highlights are
                // NOT cleared here. Yellow/green express "not yet exported to the game";
                // only Save & Export (ShowMergeSavePreviewAsync) clears them.
                _commandsSinceSnapshot = 0;
            });

            // WAL is now redundant: SaveAllAsync persisted the ENTIRE per-profile dirty set,
            // and every WAL command's affected entity is in that set (HostService.ExecuteAsync
            // marks them dirty). Clear it so stale commands cannot replay on restart and
            // re-mark entities dirty (dirty-on-open regression). The old per-mod snapshot-marker
            // hack never covered ("game", 0) or ModId=0 targets, so game-data edits replayed
            // on EVERY restart no matter how many times the user saved.
            var savedModIds = savedEntityIds
                .Select(id => _hostService.GetCachedEntity(id)?.ModId)
                .Where(id => id is >= 0)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            await ClearWorkspaceAsync();
            await UpdateLastModifiedAsync(savedModIds);
            // Docs/41: persist "edited, not yet exported" markers (survive restart).
            await PersistPendingExportsAsync(savedEntityIds);
            _logger.LogInformation("[QuickSave] cleared WAL, saved {Count} entities to DB", savedEntityIds.Count);
            UpdatePersistenceDebugInfo();

            // Notify EntityEditorDocument instances to MarkClean() their title dirty indicators.
            // Must be dispatched to UI thread so handlers can modify Dock.Title / KV state safely.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _messenger.Send(new SaveCompletedMessage());
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick save failed");
            ViewServices.Notification.ShowError($"Quick save failed: {ex.Message}", Loc["Error"]);
        }
        finally
        {
            // Ensure UI cleanup runs on the UI thread (catch may leave us on a thread-pool thread).
            Dispatcher.UIThread.Post(() =>
            {
                SetSavePreviewPreparationState(false);
                _isSavePreviewOpen = false;
                UpdateSavePreviewUiState();
            });
        }
    }

    /// <summary>Full save: DB + XML export with diff preview.</summary>
    private async void OnSavePreviewButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowMergeSavePreviewAsync();
    }

    private async void OnSaveAndLaunchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OnSaveAndLaunchClickAsync(sender, e);
    }

    private async System.Threading.Tasks.Task OnSaveAndLaunchClickAsync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Full save + export first
        await ShowMergeSavePreviewAsync();

        // Launch game
        var gameRoot = _configService.Config.GameRootDir;
        var exePath = System.IO.Path.Combine(gameRoot, "NEOScavenger.exe");
        if (!System.IO.File.Exists(exePath))
        {
            ViewServices.Notification.ShowWarning(
                $"NEOScavenger.exe not found at:\n{exePath}\n\nPlease verify Game Root Dir in Settings.",
                Loc["Launch"]);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gameRoot,
                UseShellExecute = true
            });
            _logger.LogInformation("[Launch] launched {ExePath}", exePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Launch] failed to start {ExePath}", exePath);
            ViewServices.Notification.ShowError($"Failed to launch game: {ex.Message}", Loc["Launch"]);
        }
    }

    /// <summary>
    /// In-app SWF preview (Docs/42 §3.7): resolves the game SWF and asks the shell to open
    /// the WebView panel, which runs it through the live reverse proxy (no export needed).
    /// Scenario split: preview = dev state, Launch = released disk state.
    /// </summary>
    private void OnSwfPreviewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var gameRoot = _configService.Config.GameRootDir;
        var swfPath = NeoEditor.Core.Services.RuffleOptionsBuilder.FindSwfPath(gameRoot);
        if (swfPath is null)
        {
            ViewServices.Notification.ShowWarning(Loc["SwfPreviewSwfNotFound"], Loc["SwfPreviewButton"]);
            return;
        }

        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(
            new SwfPreviewRequestedMessage(swfPath));
    }

    /// <summary>Save & Export: full DB save + XML diff preview + write to disk.</summary>
    private async void OnSaveAndExportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowMergeSavePreviewAsync();
    }

    private async void OnAddRowButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await AddOrCloneEntityAsync(copyFrom: null);
    }

    /// <summary>Copy the selected row as a new entity (D02 §5.0 entity ops: [Add] [Copy] [Delete]).</summary>
    private async void OnCopyRowButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = GetSelectedItemFromActiveGrid();
        if (selected is IEntity sourceEntity)
            await AddOrCloneEntityAsync(copyFrom: sourceEntity, skipDialog: true);
    }

    private async void OnCloneRowRequested(IEntity sourceEntity)
    {
        await AddOrCloneEntityAsync(copyFrom: sourceEntity, skipDialog: true);
    }

    private void OnFindReferencesRequested(IEntity target)
    {
        var results = new List<(string SourceLabel, string SourceCol, Type SourceType, string SourceEntityId)>();
        var keyProp = DataLoaderService.ResolveEntityKeyProperty(target.GetType());
        var targetKeyVal = keyProp?.GetValue(target)?.ToString() ?? target.EntityId;

        // R30: use the in-memory reverse index — the same backend the DataGrid display and
        // the "Referenced By" panels use. The old hand-written scan fed on the damaged
        // ToString() format ("[a, b]") and split multi-char separators char-by-char.
        var store = WorkspaceSession.ActiveMergeStore ?? WorkspaceSession.BrowserStore;
        if (store?.Index is { } index)
        {
            foreach (var (srcEid, propName, _) in index.ReverseLookup(target.EntityId))
            {
                var (srcType, srcEntity) = FindEntityById(store, srcEid);
                if (srcType is null || srcEntity is null) continue;
                var prop = srcType.GetProperty(propName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.IgnoreCase);
                var colName = prop?.GetCustomAttribute<ColumnAttribute>()?.Name ?? propName;
                results.Add((srcEntity.Subject ?? srcEid, colName, srcType, srcEid));
            }
        }
        else
        {
            // Fallback: no in-memory index (Browser mode) — scan raw values correctly.
            foreach (var tab in Tabs)
            {
                foreach (var entity in tab.SourceCollection.OfType<IEntity>())
                {
                    var type = entity.GetType();
                    foreach (var prop in type.GetProperties(
                                 System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                             .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() != null))
                    {
                        var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>()!;
                        if (refAttr.TargetEntityType != target.GetType()) continue;
                        if (refAttr.SecondaryTargetEntityType != null
                            && refAttr.SecondaryTargetEntityType != target.GetType()) continue;

                        var raw = ReferenceText.GetRawString(prop.GetValue(entity), refAttr);
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                        var segments = refAttr.Separator is not null
                            ? raw.Split(refAttr.Separator, StringSplitOptions.RemoveEmptyEntries)
                            : new[] { raw };
                        foreach (var seg in segments)
                        {
                            var id = ReferenceParser.ExtractRawId(seg.Trim(), refAttr.Pattern);
                            if (id == targetKeyVal)
                            {
                                results.Add((entity.Subject, colName, type, entity.EntityId));
                                break;
                            }
                        }
                    }
                }
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"References to: {target.Subject}");
        sb.AppendLine($"Key: {targetKeyVal}  Type: {target.GetType().Name}");
        sb.AppendLine();
        if (results.Count == 0)
        {
            sb.AppendLine("No references found in loaded data.");
        }
        else
        {
            sb.AppendLine($"Found {results.Count} reference(s):");
            foreach (var r in results)
                sb.AppendLine($"  [{r.SourceType.Name}] {r.SourceLabel}  → {r.SourceCol}");
        }

        ViewServices.Notification.ShowInfo(sb.ToString(), "Find References");
    }

    /// <summary>R30: locate a source entity by id inside a merge store (for reverse-index results).</summary>
    private static (Type? SourceType, IEntity? Entity) FindEntityById(EntityMergeStore store, string entityId)
    {
        foreach (var (t, list) in store.ReferenceLookups)
        {
            foreach (var obj in list)
            {
                if (obj is IEntity e && e.EntityId == entityId)
                    return (t, e);
            }
        }

        return (null, null);
    }

    private static object? ConvertValue(string str, Type targetType)
    {
        if (targetType == typeof(string)) return str;
        if (targetType == typeof(int)) return int.TryParse(str, out var i) ? i : null;
        if (targetType == typeof(float) || targetType == typeof(double))
            return double.TryParse(str, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        if (targetType == typeof(bool)) return str == "1" || str.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        if (targetType.IsEnum) return System.Enum.TryParse(targetType, str, out var ev) ? ev : null;
        try { return System.Convert.ChangeType(str, targetType); }
        catch { return null; }
    }

    private async Task AddOrCloneEntityAsync(IEntity? copyFrom, bool skipDialog = false)
    {
        var activeTab = GetActiveTab();
        if (activeTab is not { } tab || tab.EntityType is not { } entityType)
            return;

        try
        {
            var newEntity = Activator.CreateInstance(entityType) as IEntity;
            if (newEntity is null) return;

            var keyProp = DataLoaderService.ResolveEntityKeyProperty(entityType);

            if (skipDialog && copyFrom is not null)
            {
                // Direct clone: use source's ModId, FilePath, and copy all fields
                newEntity.ModId = copyFrom.ModId;
                newEntity.FilePath = copyFrom.FilePath;
                foreach (var prop in entityType.GetProperties(
                             BindingFlags.Instance | BindingFlags.Public)
                         .Where(p => p.DeclaringType != typeof(IEntity) &&
                                     p.GetCustomAttribute<ColumnAttribute>() != null &&
                                     p.CanWrite))
                {
                    prop.SetValue(newEntity, prop.GetValue(copyFrom));
                }
            }
            else
            {
                // Build mod and file path data for the dialog
                var mods = BuildModListForAddDialog();
                if (mods is null) return;
                var filePathsByMod = BuildFilePathsByMod(tab);

                var owner = TopLevel.GetTopLevel(this) as Window;
                if (owner is null) return;

                AddRowDialog.Result? result;
                if (copyFrom is not null)
                {
                    // Clone via dialog: full dialog with Copy From pre-selected
                    var sourceRows = tab.SourceCollection.ToList();
                    result = await AddRowDialog.ShowAsync(owner, _configService, mods, filePathsByMod, sourceRows, copyFrom);
                }
                else
                {
                    // Simple add: just mod + xmlPath
                    result = await AddRowDialog.ShowSimpleAsync(owner, _configService, mods, filePathsByMod);
                }

                if (result is null) return;
                newEntity.ModId = result.ModId;
                newEntity.FilePath = Path.IsPathRooted(result.FilePath)
                    ? result.FilePath
                    : Path.GetFullPath(Path.Combine(_configService.Config.GameRootDir, result.FilePath));

                // Copy data from source row if selected in dialog
                if (result.CopyFrom is { } sourceEntity)
                {
                    foreach (var prop in entityType.GetProperties(
                                 BindingFlags.Instance | BindingFlags.Public)
                             .Where(p => p.DeclaringType != typeof(IEntity) &&
                                         p.GetCustomAttribute<ColumnAttribute>() != null &&
                                         p.CanWrite))
                    {
                        prop.SetValue(newEntity, prop.GetValue(sourceEntity));
                    }
                }
            }
            newEntity.EntityId = $"new_{Guid.NewGuid():N}";

            // Auto-increment ID: max ID from the TARGET mod + 1
            if (keyProp != null)
            {
                var maxId = tab.SourceCollection
                    .OfType<IEntity>()
                    .Where(e => e.ModId == newEntity.ModId)
                    .Select(item => keyProp.GetValue(item))
                    .OfType<int>()
                    .DefaultIfEmpty(0)
                    .Max();
                keyProp.SetValue(newEntity, maxId + 1);
            }

            // Execute add through HostService (undoable, dirty tracking, events)
            var addCmd = new AddEntityCommand(entityType.Name, newEntity,
                e => { _hostService.AddEntityToCache(e); tab.SourceCollection.Add(e); },
                e => { _hostService.RemoveEntityFromCache(e.EntityId); tab.SourceCollection.Remove(e); });
            AsyncHelper.FireAndForget(_hostService.ExecuteAsync(addCmd, _scopeId));
            EditStore.NewEntityIds.Add(newEntity.EntityId);

            // Fix overlay chain for new entity
            var newModName = ProfileInfo?.ModLoadInfos.FirstOrDefault(m => m.Info?.ModId == newEntity.ModId)?.Info?.Name;
            if (!string.IsNullOrEmpty(newModName))
            {
                var newIdVal = keyProp?.GetValue(newEntity) is int ni ? ni : 0;
                MergeStore.EntityModNames[newEntity.EntityId] = newModName;
                MergeStore.OverlayChainDisplay[newEntity.EntityId] = new List<OverlayChainEntry>
                {
                    new(newModName, newIdVal, entityType, newEntity.EntityId, newEntity.Subject)
                };
            }

            // Recalculate all mergeIds
            if (IsMergeView)
            {
                var allOfType = tab.SourceCollection.OfType<IEntity>().ToList();
                RecalculateMergeIds(entityType, allOfType);
                RebuildFilteredItemsSources();
            }

            SetDirty(true);
            _logger.LogInformation("[AddRow] added {EntityType} id={Id} EntityId={EntityId}",
                entityType.Name, keyProp?.GetValue(newEntity), newEntity.EntityId);

            // Scroll to the new row
            var capturedEntityId = newEntity.EntityId;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var dGrid = FindActiveDataGrid();
                if (dGrid is null) return;
                var target = (dGrid.ItemsSource as IEnumerable)?.Cast<object>()
                    .FirstOrDefault(o => o is IEntity e && e.EntityId == capturedEntityId);
                if (target is not null)
                {
                    dGrid.SelectedItem = target;
                    dGrid.ScrollIntoView(target, null);
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add/clone row for {EntityType}", entityType.Name);
            ViewServices.Notification.ShowError($"Add row failed: {ex.Message}");
        }
    }

    private List<ModLoadInfo>? BuildModListForAddDialog()
    {
        if (ProfileInfo is null) return null;

        // B4: single-mod profile → the single mod itself is the add target (Merge, keeps business keys).
        if (ProfileInfo.SingleModId is not null)
            return ProfileInfo.ModLoadInfos.Where(m => m.Info is not null).ToList();

        var mods = ProfileInfo.ModLoadInfos
            .Where(m => m.Type != ModType.Merge)
            .ToList();
        if (mods.Count == 0)
        {
            ViewServices.Notification.ShowWarning("No Insert mods available. New rows require an Insert mod (strModName≠0).", "Add Row");
            return null;
        }
        return mods;
    }

    private Dictionary<int, List<string>> BuildFilePathsByMod(GameDataTypeTabItem tab)
    {
        var gameRoot = _configService.Config.GameRootDir;
        var filePathsByMod = new Dictionary<int, List<string>>();
        foreach (var group in tab.SourceCollection.OfType<IEntity>().GroupBy(e => e.ModId))
        {
            var paths = group.Select(e => e.FilePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .Select(p => Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(gameRoot, p)))
                .ToList();
            if (paths.Count > 0)
                filePathsByMod[group.Key] = paths;
        }
        return filePathsByMod;
    }

    private void OnDeleteRowButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var activeTab = GetActiveTab();
            if (activeTab is not { } tab) return;

            var selectedItem = GetSelectedItemFromActiveGrid();
            if (selectedItem is null)
            {
                ViewServices.Notification.ShowInfo(Loc["NoRowSelectedMessage"]);
                return;
            }

            if (selectedItem is not IEntity ent)
            {
                tab.SourceCollection.Remove(selectedItem);
                SetDirty(true);
                return;
            }

            // Execute delete through HostService (undoable, dirty tracking, events)
            var entityTypeName = tab.EntityType.Name;
            var delCmd = new DeleteEntityCommand(entityTypeName, ent,
                e => { _hostService.RemoveEntityFromCache(e.EntityId); tab.SourceCollection.Remove(e); },
                e => { _hostService.AddEntityToCache(e); tab.SourceCollection.Add(e); });
            AsyncHelper.FireAndForget(_hostService.ExecuteAsync(delCmd, _scopeId));
            if (IsMergeView) RebuildFilteredItemsSources();
            SetDirty(true);
            _logger.LogInformation("[DeleteRow] removed from {EntityType} EntityId={EntityId}",
                tab.EntityType.Name, ent.EntityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete row");
            ViewServices.Notification.ShowError($"Delete row failed: {ex.Message}");
        }
    }


}

