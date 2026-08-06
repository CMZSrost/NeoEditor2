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

        public Task<string?> Execute(string script)
        {
            Scripts.Add(script);
            return Task.FromResult(Result);
        }
    }

    private static StorageManagerViewModel Create(FakeJs js)
        => new(js.Execute, key => key);

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
