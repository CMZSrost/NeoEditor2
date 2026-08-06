using System;
using System.IO;
using System.Linq;

namespace NeoEditor.Core.Services;

/// <summary>
/// Game SWF discovery for the in-app WebView preview (Docs/42). The old ruffle.exe
/// launch path (Docs/40, RuffleLaunchOptions/Build) was removed 2026-08-05 — this class
/// now only locates the game's SWF file inside the game root.
/// </summary>
public static class RuffleOptionsBuilder
{
    /// <summary>Fixed SWF name inside the game root (NeoScavenger).</summary>
    public const string GameSwfFileName = "NEOScavenger.swf";

    /// <summary>
    /// Locate the game SWF: the fixed <c>NEOScavenger.swf</c> name first; otherwise a
    /// lone *.swf in the game root (to stay safe, ambiguous folders return null).
    /// </summary>
    public static string? FindSwfPath(string gameRootDir)
    {
        if (string.IsNullOrWhiteSpace(gameRootDir) || !Directory.Exists(gameRootDir)) return null;

        var fixedPath = Path.Combine(gameRootDir, GameSwfFileName);
        if (File.Exists(fixedPath)) return fixedPath;

        var swfs = Directory.EnumerateFiles(gameRootDir, "*.swf", SearchOption.TopDirectoryOnly).ToArray();
        return swfs.Length == 1 ? swfs[0] : null;
    }
}
