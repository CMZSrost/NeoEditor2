using System;
using System.IO;
using System.Linq;
using NeoEditor.Player.Core.Services;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>
/// Save backup tests (Docs/42 v2.37): write-before-overwrite backups land in
/// {gameRoot}/save_backup, newest 5 kept; restore reads the value back.
/// </summary>
public class SaveBackupServiceTests : IDisposable
{
    private readonly string _gameRoot;
    private readonly FakeConfigService _config = new();
    private readonly SaveBackupService _service;

    public SaveBackupServiceTests()
    {
        _gameRoot = TestFs.NewTempDir();
        _config.Config.GameRootDir = _gameRoot;
        _service = new SaveBackupService(_config);
    }

    public void Dispose()
    {
        try { Directory.Delete(_gameRoot, recursive: true); } catch (IOException) { }
    }

    private string BackupDir => Path.Combine(_gameRoot, "save_backup");

    [Fact]
    public void BackupWritesJsonUnderGameRootSaveBackup()
    {
        _service.Backup("http://127.0.0.1:17583/NEOScavenger.swf/save", "old save data");

        var file = Assert.Single(Directory.GetFiles(BackupDir, "backup-*.json"));
        var content = File.ReadAllText(file);
        Assert.Contains("old save data", content);
        Assert.Contains("NEOScavenger.swf/save", content);
    }

    [Fact]
    public void KeepsOnlyNewestFive()
    {
        for (var i = 1; i <= 7; i++)
            _service.Backup("key", $"value {i}");

        var files = Directory.GetFiles(BackupDir, "backup-*.json");
        Assert.Equal(5, files.Length);

        var backups = _service.List();
        Assert.Equal(5, backups.Count);
        Assert.Equal("value 7", _service.ReadValue(backups[0].FilePath));   // newest first
    }

    [Fact]
    public void ReadValueReturnsBackedUpValue()
    {
        _service.Backup("key", "pre-death value");

        var backup = Assert.Single(_service.List());
        Assert.Equal("pre-death value", _service.ReadValue(backup.FilePath));
        Assert.Equal("key", backup.Key);
    }

    [Fact]
    public void DeleteRemovesBackup()
    {
        _service.Backup("key", "value");
        var backup = Assert.Single(_service.List());

        _service.Delete(backup.FilePath);

        Assert.Empty(_service.List());
    }

    [Fact]
    public void HandleBackupRequestParsesPagePayload()
    {
        _service.HandleBackupRequest("""{"k":"http://127.0.0.1:17583/NEOScavenger.swf/save","v":"old from page"}""");

        var backup = Assert.Single(_service.List());
        Assert.Equal("old from page", _service.ReadValue(backup.FilePath));
    }

    [Fact]
    public void SaveManualCreatesNamedManualBackup()
    {
        _service.SaveManual("http://127.0.0.1:17583/NEOScavenger.swf/nsSGv1", "save data", "战役前夜");

        var backup = Assert.Single(_service.List());
        Assert.True(backup.IsManual);
        Assert.Equal("战役前夜", backup.Name);
        Assert.Equal("战役前夜", backup.DisplayName);
        Assert.Equal("save data", _service.ReadValue(backup.FilePath));
    }

    [Fact]
    public void TrimKeepsManualBackupsButCapsAutoBackups()
    {
        for (var i = 1; i <= 7; i++)
            _service.Backup("key", $"auto {i}");
        _service.SaveManual("key", "manual 1", "珍贵存档1");
        _service.SaveManual("key", "manual 2", "珍贵存档2");
        _service.Backup("key", "auto 8");   // triggers trim again

        var backups = _service.List();
        Assert.Equal(7, backups.Count);                     // 5 auto + 2 manual
        Assert.Equal(2, backups.Count(b => b.IsManual));
        Assert.Equal(5, backups.Count(b => !b.IsManual));
        Assert.Equal("auto 8",
            _service.ReadValue(backups.First(b => !b.IsManual).FilePath));  // newest auto first
    }

    [Fact]
    public void RenameUpdatesManualBackupNameAndFile()
    {
        _service.SaveManual("key", "value", "old-name");

        var before = Assert.Single(_service.List());
        _service.Rename(before.FilePath, "new-name");

        var after = Assert.Single(_service.List());
        Assert.Equal("new-name", after.Name);
        Assert.Equal("value", _service.ReadValue(after.FilePath));
        Assert.False(File.Exists(before.FilePath));   // old file replaced
    }

    [Fact]
    public void RenameIgnoresAutoBackups()
    {
        _service.Backup("key", "value");
        var auto = Assert.Single(_service.List());

        _service.Rename(auto.FilePath, "should-not-work");

        var after = Assert.Single(_service.List());
        Assert.False(after.IsManual);
        Assert.Equal("key", after.DisplayName);
    }
}
