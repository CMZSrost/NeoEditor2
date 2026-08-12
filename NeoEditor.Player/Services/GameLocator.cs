using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace NeoEditor.Player.Services;

/// <summary>
/// 为不懂代码的玩家自动定位游戏文件（v2.79）：首次启动没有保存过游戏目录时，
/// 依次尝试 Steam 安装路径（注册表）+ 常见用户目录（下载/桌面/文档）扫描
/// `NEOScavenger.swf`（找不到时退回根目录唯一 *.swf）。找到即「打开即玩」，
/// 找不到则由占位页给图文指引。
/// </summary>
public static class GameLocator
{
    /// <summary>候选查找顺序：Steam → 下载/桌面/文档 → null。</summary>
    public static string? TryLocate()
    {
        return SteamNeoScavengerSwf()
               ?? ScanCommonFolders()
               ?? null;
    }

    /// <summary>Steam 库：注册表 SteamPath → steamapps/common/NeoScavenger（及常见别名）。</summary>
    private static string? SteamNeoScavengerSwf()
    {
        try
        {
            foreach (var (hive, view) in new (RegistryHive, RegistryView)[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64),
                (RegistryHive.LocalMachine, RegistryView.Registry32),
                (RegistryHive.CurrentUser, RegistryView.Registry64),
                (RegistryHive.CurrentUser, RegistryView.Registry32),
            })
            {
                using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (key?.GetValue("SteamPath") is not string steamPath || steamPath.Length == 0) continue;
                var common = Path.Combine(steamPath, "steamapps", "common");
                foreach (var gameDir in new[]
                {
                    "NeoScavenger", "Neo Scavenger", "NEOScavenger",
                })
                {
                    var swf = FindSwf(Path.Combine(common, gameDir));
                    if (swf is not null) return swf;
                }
            }
        }
        catch (Exception)
        {
            // 注册表不可读（非 Windows/权限）→ 走目录扫描
        }
        return null;
    }

    /// <summary>下载/桌面/文档目录（浅层递归）找 NEOScavenger.swf。</summary>
    private static string? ScanCommonFolders()
    {
        foreach (var dir in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        })
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
            var found = ScanFolder(dir, depth: 3);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>递归扫描（限深）找游戏 SWF：固定 NEOScavenger.swf，缺失时退回目录内唯一 *.swf。</summary>
    public static string? ScanFolder(string root, int depth)
    {
        if (depth < 0 || !Directory.Exists(root)) return null;
        try
        {
            var direct = FindSwf(root);
            if (direct is not null) return direct;
            foreach (var sub in Directory.EnumerateDirectories(root))
            {
                var found = ScanFolder(sub, depth - 1);
                if (found is not null) return found;
            }
        }
        catch (Exception)
        {
            // 无权限目录跳过
        }
        return null;
    }

    /// <summary>目录内找 NEOScavenger.swf（固定名优先），缺失时退回唯一 *.swf（同 RuffleOptionsBuilder 语义）。</summary>
    public static string? FindSwf(string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
        try
        {
            var fixedName = Path.Combine(dir, "NEOScavenger.swf");
            if (File.Exists(fixedName)) return fixedName;
            var swfs = Directory.EnumerateFiles(dir, "*.swf").Take(2).ToList();
            return swfs.Count == 1 ? swfs[0] : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
