using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NeoEditor.Player.Core.Logging;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class PlayerBundleExporterTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wv-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void ExportBundlesSavesLogsBackupsAndInfo()
    {
        var dir = NewTempDir();
        try
        {
            var logFile = Path.Combine(dir, "player-run-1.log");
            var backupFile = Path.Combine(dir, "backup-1.json");
            File.WriteAllText(logFile, "12:00:00 [info] test log");
            File.WriteAllText(backupFile, "{\"save\":1}");
            var zipPath = Path.Combine(dir, "bundle.zip");

            PlayerBundleExporter.Export(zipPath,
                infoText: "NeoScavenger Player v0.9.0",
                localStorageJson: "[{\"key\":\"k\",\"value\":\"v\"}]",
                logFiles: [logFile], backupFiles: [backupFile]);

            using var zip = ZipFile.OpenRead(zipPath);
            var names = zip.Entries.Select(e => e.FullName).OrderBy(n => n).ToArray();
            Assert.Contains("info.txt", names);
            Assert.Contains("saves/localstorage.json", names);
            Assert.Contains("logs/player-run-1.log", names);
            Assert.Contains("backups/backup-1.json", names);

            var info = new StreamReader(zip.GetEntry("info.txt")!.Open()).ReadToEnd();
            Assert.Contains("NeoScavenger Player v0.9.0", info);
            var saves = new StreamReader(zip.GetEntry("saves/localstorage.json")!.Open()).ReadToEnd();
            Assert.Contains("v", saves);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ExportSkipsNullSavesJson()
    {
        var dir = NewTempDir();
        try
        {
            var zipPath = Path.Combine(dir, "bundle.zip");
            PlayerBundleExporter.Export(zipPath, "info", null, [], []);

            using var zip = ZipFile.OpenRead(zipPath);
            var names = zip.Entries.Select(e => e.FullName).ToArray();
            Assert.Contains("info.txt", names);
            Assert.DoesNotContain(names, n => n.StartsWith("saves/", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
