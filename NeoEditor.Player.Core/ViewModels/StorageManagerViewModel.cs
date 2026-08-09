using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Player.Core.ViewModels;

/// <summary>One local-storage save entry (Ruffle SharedObject → localStorage, v2.36).</summary>
public sealed record SaveEntry(string Key, int Length)
{
    public string SizeText => $"{Length / 1024.0:F1} KB";
}

/// <summary>
/// Save management over the page's localStorage (Docs/42 v2.36 + v2.37): Ruffle persists
/// SharedObject saves there, keyed with the swf path prefix. The host injects an async
/// JS executor (NativeWebView.InvokeScript), a localizer and the on-disk backup service
/// (write-before-overwrite backups, newest 5 kept); the VM stays platform-free so it can
/// be unit-tested.
/// </summary>
public sealed partial class StorageManagerViewModel : ObservableObject
{
    private readonly Func<string, Task<string?>> _executeJs;
    private readonly Func<string, string> _localize;
    private readonly SaveBackupService _backups;
    private readonly Action? _restartGame;

    public ObservableCollection<SaveEntry> Entries { get; } = [];

    /// <summary>On-disk write-before-overwrite backups, newest first (v2.37).</summary>
    public ObservableCollection<SaveBackup> Backups { get; } = [];

    [ObservableProperty] private string _statusText = "";

    /// <summary>
    /// v2.77 引导式重启：某项操作需要重启游戏才能生效（已触碰存档后的恢复）→ 置位；
    /// 操作本身不打断（不自动重启），窗口标题提示「退出时将重启游戏生效」，窗口关闭时
    /// 统一触发一次重启。不需要重启的操作（备份/删除/清空、未触碰时的恢复）保持静默直接生效。
    /// </summary>
    [ObservableProperty] private bool _needsRestart;

    /// <summary>窗口标题：常规标题 + 待重启提示（v2.77）。</summary>
    [ObservableProperty] private string _windowTitle = "";

    public StorageManagerViewModel(Func<string, Task<string?>> executeJs, Func<string, string> localize,
        SaveBackupService? backups = null, Action? restartGame = null)
    {
        _executeJs = executeJs;
        _localize = localize;
        _backups = backups ?? new SaveBackupService();
        _restartGame = restartGame;
        WindowTitle = BuildTitle();
    }

    private string BuildTitle()
        => _localize("Storage.Title") + (NeedsRestart ? " — " + _localize("Storage.RestartPendingTitle") : "");

    partial void OnNeedsRestartChanged(bool value) => WindowTitle = BuildTitle();

    /// <summary>
    /// v2.72（免重启方案）：删除/清空不再自动重启游戏；**恢复仍然自动重启**——恢复的
    /// 意义就是加载这份存档，而 Ruffle 把运行中游戏的 SharedObject 缓存在 AVM 内存里
    /// （avm2_shared_objects），从不重读 localStorage，不重启游戏读到的永远是内存副本
    /// （删除后的空档 → 「显示没有存档」）。v2.74 细化：**游戏尚未读取过存档时**（主菜单/
    /// 新开档早期——反编译确认 LoadGame/SaveGame 才创建 FlxSave）三个操作都**即时生效、
    /// 免重启**（游戏首次读档时才从 localStorage 创建实例，读到的就是最新值）；已触碰后
    /// 恢复才必须重启（硬限制：Ruffle 内存缓存）。host.html 的拦截表：
    /// - 墓碑（__tombstoneKey）：删除/清空后该 key 的写回被拦截 → 删除立即永久生效，
    ///   游戏可继续玩但该档保存挂起，直到重启游戏（或游戏内新开档 clear 解除墓碑）。
    /// - 保护（__protectKey）：恢复后该 key 的写回被拦截 → 重启瞬间内存旧档无法覆盖
    ///   恢复的存档（卸载 flush 同样被拦）。
    /// </summary>
    /// <summary>
    /// v2.77: 手动「重启游戏」或窗口关闭（NeedsRestart 置位时）→ 触发重启。
    /// 先清标志：手动路径由回调关闭窗口，Closed 处理器不会再触发第二次重启。
    /// </summary>
    [RelayCommand]
    private void RestartGame()
    {
        NeedsRestart = false;
        _restartGame?.Invoke();
    }

    /// <summary>
    /// v2.74: 本会话游戏是否已读取过存档（Ruffle 是否已创建 nsSGv1 SharedObject 实例）。
    /// 未触碰 → 操作直接生效无需重启；已触碰 → 恢复需重启（删除/清空靠墓碑仍免重启）。
    /// </summary>
    private async Task<bool> IsSaveTouchedAsync()
    {
        try
        {
            var result = await _executeJs("window.__saveTouched === true ? true : false");
            return result is "true";
        }
        catch (Exception)
        {
            return true;   // 查询失败保守按已触碰处理（恢复走重启路径，删除走墓碑路径）
        }
    }

    [RelayCommand]
    public async Task Refresh()
    {
        Entries.Clear();
        // ExecuteScriptAsync returns the JSON encoding of the expression's value — an
        // array literal serializes directly; a string would come back double-encoded.
        // Filters (v2.41): only real storage entries (typeof string, non-empty) of the
        // CURRENT swf (location.pathname prefix) that are actual saves — SharedObjects
        // named *test (e.g. nsTest) are dev noise the user doesn't want to manage.
        var json = await _executeJs(
            "Object.keys(localStorage).filter(function (k) {" +
            "  var v = localStorage[k];" +
            "  if (typeof v !== 'string' || v.length === 0) return false;" +
            "  var path = location.pathname;" +
            "  if (path && path !== '/' && k.indexOf(path) < 0) return false;" +
            "  var name = (k.split('/').pop() || '').toLowerCase();" +
            "  return name.indexOf('test') < 0;" +
            "}).map(function (k) { return { k: k, s: localStorage[k].length }; })");
        if (string.IsNullOrWhiteSpace(json))
        {
            StatusText = _localize("Storage.ReadFailed");
            return;
        }

        try
        {
            var items = DeserializeSaveItems(json);
            if (items is null) return;
            foreach (var item in items)
                Entries.Add(new SaveEntry(item.K, item.S));

            var total = Entries.Sum(e => e.Length);
            StatusText = Entries.Count == 0
                ? _localize("Storage.Empty")
                : string.Format(_localize("Storage.Summary"), Entries.Count, $"{total / 1024.0:F1}");
        }
        catch (JsonException)
        {
            StatusText = _localize("Storage.ParseFailed");
        }
    }

    /// <summary>
    /// Tolerate both shapes: an array literal (ExecuteScript JSON-serializes it) and a
    /// double-encoded JSON string (page wrapped it in JSON.stringify itself).
    /// </summary>
    private static SaveItem[]? DeserializeSaveItems(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveItem[]>(json);
        }
        catch (JsonException)
        {
            var inner = JsonSerializer.Deserialize<string>(json);
            return inner is null ? null : JsonSerializer.Deserialize<SaveItem[]>(inner);
        }
    }

    [RelayCommand]
    public async Task Delete(SaveEntry? entry)
    {
        if (entry is null) return;
        // v2.72：墓碑化（拦截该 key 的一切写回 → 删除不会被 Ruffle 内存副本复活）再删除。
        // __managerOp 让包装器区分「存档管理自己的删除」与「游戏内 clear」（后者解除墓碑）。
        // v2.74：游戏尚未读取过存档 → 无内存副本可复活，直接删除即时生效、免重启。
        if (!await IsSaveTouchedAsync())
        {
            await _executeJs($"localStorage.removeItem({JsonSerializer.Serialize(entry.Key)}); 'ok'");
            await Refresh();
            StatusText = string.Format(_localize("Storage.DeleteInstant"), entry.Key);
            return;
        }
        await _executeJs($"window.__managerOp = true; window.__tombstoneKey({JsonSerializer.Serialize(entry.Key)});" +
            $" localStorage.removeItem({JsonSerializer.Serialize(entry.Key)}); window.__managerOp = false; 'ok'");
        await Refresh();
        StatusText = string.Format(_localize("Storage.DeleteDone"), entry.Key);
    }

    [RelayCommand]
    public async Task ClearAll()
    {
        // v2.74：未触碰 → 无内存副本，直接清空即时生效；已触碰 → 墓碑化全部 key 后清空。
        if (!await IsSaveTouchedAsync())
        {
            await _executeJs("localStorage.clear(); 'ok'");
            await Refresh();
            StatusText = _localize("Storage.ClearAllInstant");
            return;
        }
        // v2.72：把当前全部 key 墓碑化后 clear——运行中游戏的内存副本无法再写回任何档。
        await _executeJs(
            "window.__managerOp = true;" +
            "(function () { var ks = Object.keys(localStorage);" +
            "  for (var i = 0; i < ks.length; i++) window.__tombstoneKey(ks[i]);" +
            "  localStorage.clear(); })();" +
            "window.__managerOp = false; 'ok'");
        await Refresh();
        StatusText = _localize("Storage.ClearAllDone");
    }

    // ── on-disk backups (v2.37) ──

    [RelayCommand]
    public void RefreshBackups()
    {
        Backups.Clear();
        foreach (var backup in _backups.List())
            Backups.Add(backup);
    }

    /// <summary>
    /// v2.72/v2.74/v2.77（引导式）：恢复分两档——
    /// - 游戏尚未读取过存档（__saveTouched=false）→ 直接写回，**无需重启**——游戏首次
    ///   读档时才从 localStorage 创建 SharedObject，读到的就是恢复的存档；
    /// - 已触碰 → 解除墓碑 → 写入 → 保护（内存旧档无法覆盖恢复值），置
    ///   <see cref="NeedsRestart"/>——**不打断操作、不立即重启**，标题提示「退出时将
    ///   重启游戏生效」，窗口关闭时统一重启一次。
    /// </summary>
    [RelayCommand]
    public async Task Restore(SaveBackup? backup)
    {
        if (backup is null) return;
        var value = _backups.ReadValue(backup.FilePath);
        if (value is null)
        {
            StatusText = _localize("Storage.RestoreFailed");
            return;
        }

        if (!await IsSaveTouchedAsync())
        {
            await _executeJs($"localStorage.setItem({JsonSerializer.Serialize(backup.Key)}, {JsonSerializer.Serialize(value)}); 'ok'");
            await Refresh();
            StatusText = string.Format(_localize("Storage.RestoreInstant"), backup.Key);
            return;
        }

        await _executeJs(
            $"window.__managerOp = true; window.__unmarkKey({JsonSerializer.Serialize(backup.Key)});" +
            $" localStorage.setItem({JsonSerializer.Serialize(backup.Key)}, {JsonSerializer.Serialize(value)});" +
            $" window.__protectKey({JsonSerializer.Serialize(backup.Key)}); window.__managerOp = false; 'ok'");
        await Refresh();
        StatusText = string.Format(_localize("Storage.Restored"), backup.Key);
        NeedsRestart = true;   // v2.77：不立即重启，标题提示 + 退出窗口时重启
    }

    [RelayCommand]
    public void DeleteBackup(SaveBackup? backup)
    {
        if (backup is null) return;
        _backups.Delete(backup.FilePath);
        RefreshBackups();
    }

    /// <summary>Manual backup (v2.41): persist the save's CURRENT value with a user-chosen
    /// name. `manual-*` files are exempt from the auto newest-5 trim.</summary>
    public async Task ManualBackupAsync(SaveEntry entry, string name)
    {
        if (entry is null) return;
        var json = await _executeJs(
            "(function () { return { v: localStorage[" + JsonSerializer.Serialize(entry.Key) + "] || '' }; })()");
        var value = DeserializeManualValue(json);
        if (value is null)
        {
            StatusText = _localize("Storage.ReadFailed");
            return;
        }
        _backups.SaveManual(entry.Key, value, name);
        RefreshBackups();
        StatusText = string.Format(_localize("Storage.BackupDone"), name);
    }

    /// <summary>Rename a manual backup (v2.41).</summary>
    public void RenameBackup(SaveBackup backup, string newName)
    {
        if (backup is null || string.IsNullOrWhiteSpace(newName)) return;
        _backups.Rename(backup.FilePath, newName.Trim());
        RefreshBackups();
    }

    private static string? DeserializeManualValue(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<ManualValue>(json)?.V;
        }
        catch (JsonException)
        {
            // tolerate a double-encoded string (page wrapped the object in JSON.stringify)
            try
            {
                var inner = JsonSerializer.Deserialize<string>(json);
                return inner is null ? null : JsonSerializer.Deserialize<ManualValue>(inner)?.V;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private sealed class SaveItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("k")]
        public string K { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("s")]
        public int S { get; set; }
    }

    private sealed class ManualValue
    {
        [System.Text.Json.Serialization.JsonPropertyName("v")]
        public string? V { get; set; }
    }
}
