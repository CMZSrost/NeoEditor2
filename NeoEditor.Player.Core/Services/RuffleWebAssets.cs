using System;
using System.Collections.Generic;
using System.IO;

namespace NeoEditor.Player.Core.Services;

/// <summary>
/// Locates the bundled Ruffle self-hosted web build (Docs/42 §2.2 / P0.3). Assets ship as
/// Content in Web/ruffle/ (MIT/Apache-2.0, version-locked) and are served by GameContentServer.
/// </summary>
public static class RuffleWebAssets
{
    /// <summary>Locked version — nightly-2026-08-04 (0.6.0-nightly.2026.8.4), verified by P0.1.</summary>
    public const string Version = "0.6.0-nightly.2026.8.4";

    /// <summary>Absolute path of the ruffle assets directory, or null when not deployed.</summary>
    public static string? LocateDirectory()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Web", "ruffle");
        return Directory.Exists(dir) ? dir : null;
    }

    /// <summary>True when the loader (ruffle.js) and at least one wasm core are deployed.</summary>
    public static bool IsAvailable()
    {
        var dir = LocateDirectory();
        return dir is not null
               && File.Exists(Path.Combine(dir, "ruffle.js"))
               && Directory.EnumerateFiles(dir, "*.wasm").AnySafe();
    }

    private static bool AnySafe(this IEnumerable<string> files)
    {
        foreach (var _ in files) return true;
        return false;
    }
}
