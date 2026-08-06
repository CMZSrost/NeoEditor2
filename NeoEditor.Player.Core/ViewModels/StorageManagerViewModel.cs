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

    public ObservableCollection<SaveEntry> Entries { get; } = [];

    /// <summary>On-disk write-before-overwrite backups, newest first (v2.37).</summary>
    public ObservableCollection<SaveBackup> Backups { get; } = [];

    [ObservableProperty] private string _statusText = "";

    public StorageManagerViewModel(Func<string, Task<string?>> executeJs, Func<string, string> localize,
        SaveBackupService? backups = null)
    {
        _executeJs = executeJs;
        _localize = localize;
        _backups = backups ?? new SaveBackupService();
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
        await _executeJs($"localStorage.removeItem({JsonSerializer.Serialize(entry.Key)}); 'ok'");
        await Refresh();
    }

    [RelayCommand]
    public async Task ClearAll()
    {
        await _executeJs("localStorage.clear(); 'ok'");
        await Refresh();
    }

    // ── on-disk backups (v2.37) ──

    [RelayCommand]
    public void RefreshBackups()
    {
        Backups.Clear();
        foreach (var backup in _backups.List())
            Backups.Add(backup);
    }

    /// <summary>Restore a backup: write its value back into localStorage (the game deleted
    /// the save on death — this is the recovery path).</summary>
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

        await _executeJs($"localStorage.setItem({JsonSerializer.Serialize(backup.Key)}, {JsonSerializer.Serialize(value)}); 'ok'");
        await Refresh();
        StatusText = string.Format(_localize("Storage.Restored"), backup.Key);
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
