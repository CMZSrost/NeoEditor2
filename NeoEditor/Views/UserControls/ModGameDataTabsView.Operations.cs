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
    private async void OnExportXmlClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (IsMergeView && ProfileInfo is not null)
        {
            var mods = ProfileInfo.ModLoadInfos
                .Where(m => m.Info is not null)
                .Select(m => m.Info)
                .ToList();
            await ExportXmlAsync(mods);
        }
        else if (ModInfo is not null)
        {
            await ExportXmlAsync([ModInfo]);
        }
    }

    /// <summary>Quick save: persist to DB only. No XML export, no diff preview.</summary>
    private async void OnQuickSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await QuickSaveAsync();
    }

    private async Task QuickSaveAsync()
    {
        if (_isSavePreviewOpen || IsPreparingSavePreview) return;

        _isSavePreviewOpen = true;
        SetSavePreviewPreparationState(true, Loc["Saving"]);

        try
        {
            var entitiesToSave = CaptureCurrentTabEntities().Where(e => e.ModId > 0).ToList();
            if (entitiesToSave.Count == 0)
            {
                if (ProfileInfo is not null)
                    App.Notification.ShowInfo("No mod entities to save. Ensure mods are loaded in the profile.", "Quick Save");
                else
                    App.Notification.ShowInfo("No entities to save.", "Quick Save");
                return;
            }

            if (ProfileInfo is not null)
                await SaveToDatabaseAsync(entitiesToSave);
            else if (ModInfo is not null)
                await SaveToDatabaseAsync(entitiesToSave, ModInfo.ModId);

            SetDirty(false);
            ClearDirtyTabs();
            GenericDataGridHelper.EditedCells.Clear();
            GenericDataGridHelper.NewEntityIds.Clear();
            RefreshActiveDataGrid();
            _commandsSinceSnapshot = 0;
            // QuickSave IS a snapshot point — mark current commands as covered (game.db already written by SaveToDatabaseAsync)
            var (tt, tid) = GetPersistenceTarget();
            if (tid >= 0)
                await _workspacePersistence.UpdateSnapshotMarkerAsync(tt, tid, _persistSequence);
            UpdatePersistenceDebugInfo();
            _logger.LogInformation("[QuickSave] saved {Count} entities to DB", entitiesToSave.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick save failed");
            App.Notification.ShowError($"Quick save failed: {ex.Message}", Loc["Error"]);
        }
        finally
        {
            SetSavePreviewPreparationState(false);
            _isSavePreviewOpen = false;
            UpdateSavePreviewUiState();
        }
    }

    /// <summary>Full save: DB + XML export with diff preview.</summary>
    private async void OnSavePreviewButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfileInfo is not null)
            await ShowMergeSavePreviewAsync();
        else
            await ShowSavePreviewAsync();
    }

    private async void OnSaveAndLaunchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OnSaveAndLaunchClickAsync(sender, e);
    }

    private async System.Threading.Tasks.Task OnSaveAndLaunchClickAsync(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Full save + export first
        if (ProfileInfo is not null)
            await ShowMergeSavePreviewAsync();
        else
            await ShowSavePreviewAsync();

        // Launch game
        var gameRoot = _configService.Config.GameRootDir;
        var exePath = System.IO.Path.Combine(gameRoot, "NEOScavenger.exe");
        if (!System.IO.File.Exists(exePath))
        {
            App.Notification.ShowWarning(
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
            App.Notification.ShowError($"Failed to launch game: {ex.Message}", Loc["Launch"]);
        }
    }

    /// <summary>Save & Export: full DB save + XML diff preview + write to disk.</summary>
    private async void OnSaveAndExportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProfileInfo is not null)
            await ShowMergeSavePreviewAsync();
        else
            await ShowSavePreviewAsync();
    }

    private async void OnAddRowButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await AddOrCloneEntityAsync(copyFrom: null);
    }

    private async void OnCloneRowRequested(IEntity sourceEntity)
    {
        await AddOrCloneEntityAsync(copyFrom: sourceEntity, skipDialog: true);
    }

    private void OnFindReferencesRequested(IEntity target)
    {
        var results = new List<(string SourceLabel, string SourceCol, Type SourceType, string SourceEntityId)>();
        var keyProp = ResolveEntityKeyProperty(target.GetType());
        var targetKeyVal = keyProp?.GetValue(target)?.ToString() ?? target.EntityId;

        foreach (var tab in Tabs)
        {
            foreach (var entity in tab.SourceCollection.OfType<IEntity>())
            {
                var type = entity.GetType();
                foreach (var prop in type.GetProperties(
                             System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                         .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() != null))
                {
                    var raw = prop.GetValue(entity)?.ToString();
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>()!;
                    if (refAttr.TargetEntityType != target.GetType()) continue;

                    var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

                    // Check if raw value matches the target's key
                    var separator = refAttr.Separator;
                    if (separator is not null)
                    {
                        foreach (var seg in raw.Split(separator[0]))
                        {
                            var id = ReferenceHelper.ExtractRawId(seg.Trim(), refAttr.Pattern);
                            if (id == targetKeyVal)
                            {
                                results.Add((entity.Subject, colName, type, entity.EntityId));
                                break;
                            }
                        }
                    }
                    else
                    {
                        var id = ReferenceHelper.ExtractRawId(raw, refAttr.Pattern);
                        if (id == targetKeyVal)
                            results.Add((entity.Subject, colName, type, entity.EntityId));
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

        App.Notification.ShowInfo(sb.ToString(), "Find References");
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

            var keyProp = ResolveEntityKeyProperty(entityType);

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
                    result = await AddRowDialog.ShowAsync(owner, mods, filePathsByMod, sourceRows, copyFrom);
                }
                else
                {
                    // Simple add: just mod + xmlPath
                    result = await AddRowDialog.ShowSimpleAsync(owner, mods, filePathsByMod);
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

            // Execute add through command history (undoable)
            var addCmd = new AddEntityCommand(tab.SourceCollection, newEntity, () => SetDirty(true));
            _commandHistory.Execute(addCmd);
            GenericDataGridHelper.NewEntityIds.Add(newEntity.EntityId);

            // Fix overlay chain for new entity
            var newModName = IsMergeView
                ? ProfileInfo?.ModLoadInfos.FirstOrDefault(m => m.Info?.ModId == newEntity.ModId)?.Info?.Name
                : ModInfo?.Name;
            if (!string.IsNullOrEmpty(newModName))
            {
                var newIdVal = keyProp?.GetValue(newEntity) is int ni ? ni : 0;
                GenericDataGridHelper.EntityModNames[newEntity.EntityId] = newModName;
                GenericDataGridHelper.OverlayChainDisplay[newEntity.EntityId] = new List<OverlayChainEntry>
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
            App.Notification.ShowError($"Add row failed: {ex.Message}");
        }
    }

    private List<ModLoadInfo>? BuildModListForAddDialog()
    {
        if (IsMergeView && ProfileInfo is not null)
        {
            var mods = ProfileInfo.ModLoadInfos
                .Where(m => m.Type != ModType.Merge)
                .ToList();
            if (mods.Count == 0)
            {
                App.Notification.ShowWarning("No Insert mods available. New rows require an Insert mod (strModName≠0).", "Add Row");
                return null;
            }
            return mods;
        }
        else if (ModInfo is not null)
        {
            return [new ModLoadInfo { Info = ModInfo, Type = ModType.Merge }];
        }
        return null;
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

    private static PropertyInfo? ResolveEntityKeyProperty(Type entityType)
    {
        var indexAttr = entityType.GetCustomAttributes<IndexAttribute>().FirstOrDefault();
        var keyPropName = indexAttr?.PropertyNames
            .FirstOrDefault(n => n != nameof(IEntity.EntityId));
        if (!string.IsNullOrWhiteSpace(keyPropName))
            return entityType.GetProperty(keyPropName, BindingFlags.Instance | BindingFlags.Public);

        // Fallback: first property with [Column] attribute that is int type
        return entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity))
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
            .Where(p => p.PropertyType == typeof(int))
            .OrderBy(p => p.MetadataToken)
            .FirstOrDefault();
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
                App.Notification.ShowInfo(Loc["NoRowSelectedMessage"]);
                return;
            }

            if (selectedItem is not IEntity ent)
            {
                tab.SourceCollection.Remove(selectedItem);
                SetDirty(true);
                return;
            }

            // Execute delete through command history (undoable)
            var delCmd = new DeleteEntityCommand(tab.SourceCollection, ent, () => SetDirty(true));
            _commandHistory.Execute(delCmd);
            if (IsMergeView) RebuildFilteredItemsSources();
            SetDirty(true);
            _logger.LogInformation("[DeleteRow] removed from {EntityType} EntityId={EntityId}",
                tab.EntityType.Name, ent.EntityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete row");
            App.Notification.ShowError($"Delete row failed: {ex.Message}");
        }
    }


}

