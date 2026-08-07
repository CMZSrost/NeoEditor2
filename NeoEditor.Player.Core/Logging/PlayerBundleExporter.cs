using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace NeoEditor.Player.Core.Logging;

/// <summary>
/// R43: 存档+日志 zip 打包——试用反馈/存档迁移包。布局：
/// <c>info.txt</c> + <c>saves/localstorage.json</c>（__exportSaves 全量 JSON）+
/// <c>logs/*.log</c>（当前日志文件）+ <c>backups/*.json</c>（save_backup 备份）。
/// 纯文件 IO，无 UI（错误上抛给调用方显示）。
/// </summary>
public static class PlayerBundleExporter
{
    /// <summary>
    /// Build the bundle zip at <paramref name="zipPath"/> (overwrites if present).
    /// </summary>
    /// <param name="infoText">info.txt 内容（版本/时间/路径/条目清单）。</param>
    /// <param name="localStorageJson">__exportSaves() 返回的全量 localStorage JSON；null 跳过。</param>
    /// <param name="logFiles">日志文件绝对路径（按文件名拷入 logs/）。</param>
    /// <param name="backupFiles">存档备份绝对路径（按文件名拷入 backups/）。</param>
    public static void Export(string zipPath, string infoText, string? localStorageJson,
        IEnumerable<string> logFiles, IEnumerable<string> backupFiles)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        WriteEntry(zip, "info.txt", infoText);
        if (!string.IsNullOrWhiteSpace(localStorageJson))
            WriteEntry(zip, "saves/localstorage.json", localStorageJson);
        foreach (var file in logFiles)
            zip.CreateEntryFromFile(file, "logs/" + Path.GetFileName(file));
        foreach (var file in backupFiles)
            zip.CreateEntryFromFile(file, "backups/" + Path.GetFileName(file));
    }

    private static void WriteEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
