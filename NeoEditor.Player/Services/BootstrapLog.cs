using System;
using System.IO;
using System.Linq;
using System.Text;
using NeoEditor.Player.Core.Logging;

namespace NeoEditor.Player.Services;

/// <summary>
/// Earliest-possible startup log (v2.69). Some machines crash right after launch — cursor
/// spins, then nothing — before Serilog / FileRunLogWriter / UI exist, so the reason was
/// invisible. BootstrapLog is created as the FIRST thing in Program.Main: every line
/// flushes to {exe}/logs/player-boot-*.log (LocalAppData fallback, newest 5 kept), and
/// startup milestones + crash handlers record exactly how far the launch got. Game/run
/// logs stay in their own player-run-*.log files (FileRunLogWriter) — this file is for
/// startup/crash diagnosis only.
/// </summary>
public static class BootstrapLog
{
    private const int KeepFileCount = 5;
    private const string FilePrefix = "player-boot-";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly object Gate = new();
    private static string? _filePath;

    /// <summary>Full path of this launch's boot log (null when the directory is unwritable).</summary>
    public static string? FilePath => _filePath;

    /// <summary>Create this launch's log file (idempotent) — call BEFORE anything that can crash.</summary>
    public static void Initialize()
    {
        lock (Gate)
        {
            if (_filePath is not null) return;
            var dir = FileRunLogWriter.ResolveDirectory(null);   // {exe}/logs → LocalAppData fallback
            _filePath = Path.Combine(dir, $"{FilePrefix}{DateTime.Now:yyyyMMdd-HHmmss}.log");
            try
            {
                File.WriteAllText(_filePath,
                    $"=== NeoScavengerPlayer boot {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}",
                    Utf8NoBom);
                TrimOldFiles(dir);
            }
            catch (Exception)
            {
                _filePath = null;   // logging must never take the player down
            }
        }
    }

    /// <summary>Milestone line with timestamp.</summary>
    public static void Write(string message) => Append($"{DateTime.Now:HH:mm:ss.fff} {message}");

    /// <summary>
    /// Crash line — exception type + message + stack, flushed before the process dies.
    /// </summary>
    public static void WriteException(string context, Exception ex) => Append(
        $"{DateTime.Now:HH:mm:ss.fff} [FATAL] {context}: {ex.GetType().Name}: {ex.Message}" +
        $"{Environment.NewLine}{ex.StackTrace}");

    private static void Append(string line)
    {
        lock (Gate)
        {
            if (_filePath is null) return;
            try
            {
                File.AppendAllText(_filePath, line + Environment.NewLine, Utf8NoBom);
            }
            catch (Exception)
            {
                // never take the player down for logging
            }
        }
    }

    private static void TrimOldFiles(string dir)
    {
        try
        {
            foreach (var stale in Directory.EnumerateFiles(dir, FilePrefix + "*.log")
                         .OrderByDescending(Path.GetFileName).Skip(KeepFileCount))
            {
                try
                {
                    File.Delete(stale);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }
}
