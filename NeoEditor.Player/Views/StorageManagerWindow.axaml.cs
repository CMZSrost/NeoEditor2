using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NeoEditor.Player.Core.Services;
using NeoEditor.Player.Core.ViewModels;
using NeoEditor.Player.Services;

namespace NeoEditor.Player.Views;

/// <summary>
/// Save manager dialog (Docs/42 v2.36 + v2.37 + v2.41): lists the game's localStorage
/// saves (Ruffle SharedObject) and on-disk backups. v2.41: manual backup button with
/// naming, backup rename, and confirm-before-delete prompts.
/// </summary>
public partial class StorageManagerWindow : Window
{
    /// <summary>R46: 「修改」按钮 → 宿主打开存档修改工具并预载该存档。</summary>
    public event Action<SaveEntry>? EditSaveRequested;

    public StorageManagerWindow()
    {
        InitializeComponent();
        // v2.43: auto backups land on disk while the window is open (the game keeps
        // running) — refresh the backups list whenever the window re-gains focus so
        // new backups appear without a manual Refresh click.
        Activated += (_, _) => RefreshBackupsIfVisible();
    }

    /// <summary>Refresh the backups list when the Backups tab is selected (or on focus).</summary>
    private void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        // BackupTab can be null while InitializeComponent is still populating the XAML —
        // TabControl raises SelectionChanged when its selection model is created (v2.43
        // regression: NRE on open). Guard it.
        if (BackupTab is { IsSelected: true })
            RefreshBackupsIfVisible();
    }

    private void RefreshBackupsIfVisible()
    {
        if (DataContext is StorageManagerViewModel vm)
            vm.RefreshBackupsCommand.Execute(null);
    }

    private static string L(string key) => LocalizationManager.Instance[key];

    private async void OnBackupClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SaveEntry entry } || DataContext is not StorageManagerViewModel vm)
            return;
        var defaultName = entry.Key.Split('/').LastOrDefault() ?? "save";
        var name = await PromptDialogWindow.PromptAsync(this,
            L("Storage.BackupPromptTitle"), null, defaultName,
            L("Storage.Backup"), L("Common.Cancel"));
        if (string.IsNullOrWhiteSpace(name)) return;
        await vm.ManualBackupAsync(entry, name.Trim());
    }

    private async void OnRenameBackupClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SaveBackup backup } || DataContext is not StorageManagerViewModel vm)
            return;
        var name = await PromptDialogWindow.PromptAsync(this,
            L("Storage.RenamePromptTitle"), null, backup.DisplayName,
            L("Storage.Rename"), L("Common.Cancel"));
        if (string.IsNullOrWhiteSpace(name)) return;
        vm.RenameBackup(backup, name.Trim());
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SaveEntry entry } || DataContext is not StorageManagerViewModel vm)
            return;
        var confirmed = await PromptDialogWindow.PromptAsync(this,
            L("Storage.ConfirmDeleteTitle"),
            string.Format(L("Storage.ConfirmDelete"), entry.Key),
            okText: L("Common.Ok"), cancelText: L("Common.Cancel"));
        if (confirmed is null) return;
        // v2.50: DeleteCommand 已自动重启游戏（并关闭本窗口）——不再弹手动重启框
        await vm.DeleteCommand.ExecuteAsync(entry);
    }

    private async void OnDeleteBackupClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SaveBackup backup } || DataContext is not StorageManagerViewModel vm)
            return;
        var confirmed = await PromptDialogWindow.PromptAsync(this,
            L("Storage.ConfirmDeleteTitle"),
            string.Format(L("Storage.ConfirmDeleteBackup"), backup.DisplayName),
            okText: L("Common.Ok"), cancelText: L("Common.Cancel"));
        if (confirmed is null) return;
        vm.DeleteBackupCommand.Execute(backup);
    }

    private async void OnClearAllClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not StorageManagerViewModel vm) return;
        var confirmed = await PromptDialogWindow.PromptAsync(this,
            L("Storage.ConfirmClearAllTitle"), L("Storage.ConfirmClearAll"),
            okText: L("Common.Ok"), cancelText: L("Common.Cancel"));
        if (confirmed is null) return;
        // v2.50: ClearAllCommand 已自动重启游戏
        await vm.ClearAllCommand.ExecuteAsync(null);
    }

    private async void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SaveBackup backup } && DataContext is StorageManagerViewModel vm)
        {
            // v2.50: RestoreCommand 已自动重启游戏
            await vm.RestoreCommand.ExecuteAsync(backup);
        }
    }

    /// <summary>R46: 存档修改工具入口（宿主打开编辑器并预载该存档）。</summary>
    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SaveEntry entry })
            EditSaveRequested?.Invoke(entry);
    }
}
