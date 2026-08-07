using System.Reflection;

namespace NeoEditor.Player.Services;

/// <summary>
/// App identity (R43): the version surfaces in the window title, About dialog, startup log
/// and export bundles — trial users must be able to report the exact build. Single source
/// is the csproj &lt;Version&gt; (publish.ps1 / zip naming reads the same value).
/// </summary>
public static class AppInfo
{
    /// <summary>csproj &lt;Version&gt; (0.9.0 → "0.9.0").</summary>
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Bundled Ruffle nightly build (Docs/42 §八 — 升级走独立变更).</summary>
    public const string RuffleVersion = "nightly-2026-08-04";

    /// <summary>Display name used in titles/About.</summary>
    public const string ProductName = "NeoScavenger Player";
}
