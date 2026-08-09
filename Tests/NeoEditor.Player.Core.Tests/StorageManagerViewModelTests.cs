using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NeoEditor.Player.Core.Services;
using NeoEditor.Player.Core.ViewModels;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>
/// Save manager VM tests (Docs/42 v2.36): the JS executor is injected, so listing /
/// delete / clear-all are testable without a webview.
/// </summary>
public class StorageManagerViewModelTests
{
    private sealed class FakeJs
    {
        public List<string> Scripts { get; } = [];
        public string? Result { get; set; }

        /// <summary>Per-script result (v2.74): the touched query and the list query get
        /// different answers — keyed on script content.</summary>
        public Func<string, string?>? ResultFor { get; set; }

        public Task<string?> Execute(string script)
        {
            Scripts.Add(script);
            return Task.FromResult(ResultFor?.Invoke(script) ?? Result);
        }
    }

    private static StorageManagerViewModel Create(FakeJs js)
        => new(js.Execute, key => key);

    /// <summary>Fake where the game HAS read the save (__saveTouched=true) — delete/clear
    /// take the tombstone path, restore the protect+restart path.</summary>
    private static FakeJs Touched(string? listResult = "[]") => new()
    {
        ResultFor = script => script.Contains("__saveTouched") ? "true" : listResult,
    };

    /// <summary>Fake where the game has NOT read the save yet — all ops take the instant path.</summary>
    private static FakeJs Untouched(string? listResult = "[]") => new()
    {
        ResultFor = script => script.Contains("__saveTouched") ? "false" : listResult,
    };

    [Fact]
    public async Task RefreshParsesEntriesAndSummarizes()
    {
        var js = new FakeJs
        {
            Result = """[{"k":"http://127.0.0.1:17583/NEOScavenger.swf/save1","s":2048},{"k":"/NEOScavenger.swf/save2","s":1024}]""",
        };
        var vm = Create(js);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Entries.Count);
        Assert.Contains(vm.Entries, e => e.Key.Contains("save1") && e.SizeText.Contains("2.0"));
        Assert.Contains(vm.Entries, e => e.Key.Contains("save2") && e.SizeText.Contains("1.0"));
        Assert.Equal("Storage.Summary", vm.StatusText);
    }

    [Fact]
    public async Task RefreshToleratesDoubleEncodedJsonString()
    {
        // ExecuteScriptAsync JSON-encodes the expression's value: a page that returns a
        // string (e.g. via JSON.stringify) comes back double-encoded — must still parse.
        var inner = """[{"k":"http://127.0.0.1:17583/NEOScavenger.swf/save1","s":2048}]""";
        var js = new FakeJs { Result = JsonSerializer.Serialize(inner) };
        var vm = Create(js);

        await vm.RefreshCommand.ExecuteAsync(null);

        var entry = Assert.Single(vm.Entries);
        Assert.Contains("save1", entry.Key);
        Assert.Equal("Storage.Summary", vm.StatusText);
    }

    [Fact]
    public async Task RefreshEmptyShowsNoSaves()
    {
        var js = new FakeJs { Result = "[]" };
        var vm = Create(js);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.Entries);
        Assert.Equal("Storage.Empty", vm.StatusText);
    }

    [Fact]
    public async Task RefreshFailsWhenJsUnavailable()
    {
        var vm = Create(new FakeJs { Result = null });

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.Entries);
        Assert.Equal("Storage.ReadFailed", vm.StatusText);
    }

    [Fact]
    public async Task DeleteIssuesRemoveItemForThatKey()
    {
        var js = new FakeJs { Result = "[]" };
        var vm = Create(js);
        var entry = new SaveEntry("http://127.0.0.1:17583/NEOScavenger.swf/save1", 2048);

        await vm.DeleteCommand.ExecuteAsync(entry);

        Assert.Contains(js.Scripts, s => s.Contains("removeItem") && s.Contains("save1"));
    }

    [Fact]
    public async Task DeleteTombstonesTheKeyAndDoesNotRestart()
    {
        // v2.72/v2.74: 游戏已读取过存档（touched）→ 墓碑化拦截 Ruffle 内存副本写回；不再自动重启。
        var js = Touched();
        var restarts = 0;
        var vm = new StorageManagerViewModel(js.Execute, key => key, null, () => restarts++);
        var entry = new SaveEntry("http://127.0.0.1:17583/NEOScavenger.swf/nsSGv1", 2048);

        await vm.DeleteCommand.ExecuteAsync(entry);

        // 操作脚本 + Refresh 列表脚本各一条；断言操作脚本内容。
        var script = Assert.Single(js.Scripts, s => s.Contains("removeItem"));
        Assert.Contains("__tombstoneKey", script);
        Assert.Contains("__managerOp = true", script);
        Assert.Equal(0, restarts);                          // 不再自动重启
        Assert.False(vm.NeedsRestart);                      // 删除不需要重启
        Assert.Equal("Storage.DeleteDone", vm.StatusText);
    }

    [Fact]
    public async Task DeleteUntouchedIsInstantWithoutTombstone()
    {
        // v2.74: 游戏尚未读取存档 → 无内存副本可复活，直接删除、无需重启、不设墓碑。
        var js = Untouched();
        var restarts = 0;
        var vm = new StorageManagerViewModel(js.Execute, key => key, null, () => restarts++);
        var entry = new SaveEntry("http://127.0.0.1:17583/NEOScavenger.swf/nsSGv1", 2048);

        await vm.DeleteCommand.ExecuteAsync(entry);

        var script = Assert.Single(js.Scripts, s => s.Contains("removeItem"));
        Assert.DoesNotContain("__tombstoneKey", script);
        Assert.Equal(0, restarts);
        Assert.Equal("Storage.DeleteInstant", vm.StatusText);
    }

    [Fact]
    public async Task ClearAllTombstonesEveryKeyAndDoesNotRestart()
    {
        var js = Touched();
        var restarts = 0;
        var vm = new StorageManagerViewModel(js.Execute, key => key, null, () => restarts++);

        await vm.ClearAllCommand.ExecuteAsync(null);

        var script = Assert.Single(js.Scripts, s => s.Contains("localStorage.clear()"));
        Assert.Contains("Object.keys(localStorage)", script);
        Assert.Contains("__tombstoneKey", script);
        Assert.Equal(0, restarts);
        Assert.Equal("Storage.ClearAllDone", vm.StatusText);
    }

    [Fact]
    public async Task ClearAllUntouchedIsInstant()
    {
        var js = Untouched();
        var restarts = 0;
        var vm = new StorageManagerViewModel(js.Execute, key => key, null, () => restarts++);

        await vm.ClearAllCommand.ExecuteAsync(null);

        var script = Assert.Single(js.Scripts, s => s.Contains("localStorage.clear()"));
        Assert.DoesNotContain("__tombstoneKey", script);
        Assert.Equal(0, restarts);
        Assert.Equal("Storage.ClearAllInstant", vm.StatusText);
    }

    [Fact]
    public async Task RestoreTouchedProtectsKeyAndSetsPendingRestart()
    {
        var root = TestFs.NewTempDir();
        try
        {
            var config = new FakeConfigService();
            config.Config.GameRootDir = root;
            var backups = new SaveBackupService(config);
            backups.SaveManual("http://127.0.0.1:17583/NEOScavenger.swf/nsSGv1", "restored-save-data", "决战前");
            var js = Touched();
            var restarts = 0;
            var vm = new StorageManagerViewModel(js.Execute, key => key, backups, () => restarts++);

            await vm.RestoreCommand.ExecuteAsync(backups.List()[0]);

            var script = Assert.Single(js.Scripts, s => s.Contains("localStorage.setItem"));
            Assert.Contains("__unmarkKey", script);
            Assert.Contains("restored-save-data", script);
            Assert.Contains("__protectKey", script);
            // v2.77 引导式：不立即重启，置待重启标志 + 标题提示；窗口关闭时统一重启。
            await Task.Delay(400);
            Assert.Equal(0, restarts);
            Assert.True(vm.NeedsRestart);
            Assert.Contains("Storage.RestartPendingTitle", vm.WindowTitle);

            // 手动/关闭触发重启 → 标志清除、重启一次。
            vm.RestartGameCommand.Execute(null);
            Assert.Equal(1, restarts);
            Assert.False(vm.NeedsRestart);
            Assert.DoesNotContain("Storage.RestartPendingTitle", vm.WindowTitle);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task RestoreUntouchedIsInstantWithoutRestart()
    {
        var root = TestFs.NewTempDir();
        try
        {
            var config = new FakeConfigService();
            config.Config.GameRootDir = root;
            var backups = new SaveBackupService(config);
            backups.SaveManual("http://127.0.0.1:17583/NEOScavenger.swf/nsSGv1", "restored-save-data", "决战前");
            // v2.74: 游戏尚未读取存档 → 直接写回即时生效、不保护、不重启、无待重启标志。
            var js = Untouched();
            var restarts = 0;
            var vm = new StorageManagerViewModel(js.Execute, key => key, backups, () => restarts++);

            await vm.RestoreCommand.ExecuteAsync(backups.List()[0]);

            var script = Assert.Single(js.Scripts, s => s.Contains("localStorage.setItem"));
            Assert.DoesNotContain("__protectKey", script);
            Assert.DoesNotContain("__unmarkKey", script);
            await Task.Delay(400);
            Assert.Equal(0, restarts);
            Assert.False(vm.NeedsRestart);
            Assert.Equal("Storage.RestoreInstant", vm.StatusText);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task RestoreUnreadableBackupShowsFailure()
    {
        var js = new FakeJs { Result = "[]" };
        var vm = Create(js);
        var missing = new SaveBackup("D:/no/such/backup.json", "key", DateTime.Now, 10, false, null);

        await vm.RestoreCommand.ExecuteAsync(missing);

        Assert.Empty(js.Scripts);
        Assert.Equal("Storage.RestoreFailed", vm.StatusText);
    }

    [Fact]
    public async Task ClearAllIssuesLocalStorageClear()
    {
        var js = new FakeJs { Result = "[]" };
        var vm = Create(js);

        await vm.ClearAllCommand.ExecuteAsync(null);

        Assert.Contains(js.Scripts, s => s.Contains("localStorage.clear()"));
    }

    [Fact]
    public async Task ManualBackupReadsValueAndPersistsNamedBackup()
    {
        var root = TestFs.NewTempDir();
        try
        {
            var config = new FakeConfigService();
            config.Config.GameRootDir = root;
            var js = new FakeJs { Result = """{"v":"current save data"}""" };
            var backups = new SaveBackupService(config);
            var vm = new StorageManagerViewModel(js.Execute, key => key, backups);
            var entry = new SaveEntry("http://127.0.0.1:17583/NEOScavenger.swf/nsSGv1", 100);

            await vm.ManualBackupAsync(entry, "决战前");

            var backup = Assert.Single(backups.List());
            Assert.True(backup.IsManual);
            Assert.Equal("决战前", backup.DisplayName);
            Assert.Equal("current save data", backups.ReadValue(backup.FilePath));
            Assert.Equal("Storage.BackupDone", vm.StatusText);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task RenameBackupUpdatesName()
    {
        var root = TestFs.NewTempDir();
        try
        {
            var config = new FakeConfigService();
            config.Config.GameRootDir = root;
            var backups = new SaveBackupService(config);
            backups.SaveManual("key", "value", "old");
            var vm = new StorageManagerViewModel(_ => Task.FromResult<string?>(null), key => key, backups);

            vm.RenameBackup(backups.List()[0], "new name");

            var renamed = Assert.Single(backups.List());
            Assert.Equal("new name", renamed.Name);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}
