using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NeoEditor.Player.Core.Logging;

/// <summary>
/// File sink for run logs (Docs/42 v2.25): every page run gets its own
/// <c>player-run-*.log</c> file under the log directory (BaseDirectory/logs, falling back
/// to LocalApplicationData when that is not writable), only the NEWEST 2 files are kept
/// (oldest deleted on rotation), and every line is flushed immediately so a crash loses at
/// most the tail of one line. Pure file IO — no UI.
/// </summary>
public sealed class FileRunLogWriter : IDisposable
{
    private const int KeepFileCount = 2;
    private const string FilePrefix = "player-run-";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly object _gate = new();
    private readonly string _directory;
    private string? _currentRunId;
    private StreamWriter? _writer;

    /// <param name="logDirectory">Override the log directory (tests / custom installs).</param>
    public FileRunLogWriter(RunLogStore store, string? logDirectory = null)
    {
        _directory = ResolveDirectory(logDirectory);
        LogDirectory = _directory;
        store.LineAppended += OnLineAppended;
        TrimOldFiles();
    }

    /// <summary>Where the log files live (surfaced in the UI so users can find them).</summary>
    public string LogDirectory { get; }

    /// <summary>
    /// BaseDirectory/logs when writable, else LocalApplicationData/NeoScavengerPlayer/logs.
    /// </summary>
    public static string ResolveDirectory(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred;

        var portable = Path.Combine(AppContext.BaseDirectory, "logs");
        try
        {
            Directory.CreateDirectory(portable);
            return portable;
        }
        catch (Exception)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeoScavengerPlayer", "logs");
        }
    }

    private void OnLineAppended(object? sender, PlayerLogLineAppendedEventArgs e)
    {
        lock (_gate)
        {
            try
            {
                EnsureRun(e.RunId);
                _writer!.WriteLine(
                    $"{e.Line.Timestamp:HH:mm:ss.fff} [{e.Line.Level}] {e.Line.Message}");
                _writer.Flush();   // crash-safe: every line hits disk immediately
            }
            catch (Exception)
            {
                // The log sink must never take the player down with it.
            }
        }
    }

    /// <summary>Append a fatal line (crash handlers) to the current run file, or a fresh one.</summary>
    public void WriteCrash(string message)
    {
        lock (_gate)
        {
            try
            {
                EnsureRun(_currentRunId ?? "crash");
                _writer!.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [FATAL] {message}");
                _writer.Flush();
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Open a new file when the run changes; keep only the newest 2 files.</summary>
    private void EnsureRun(string runId)
    {
        if (_currentRunId == runId && _writer is not null) return;

        _writer?.Dispose();
        _currentRunId = runId;
        var path = Path.Combine(_directory, NewFileName(runId));
        // FileShare.ReadWrite: the log viewer (or tests) may open the file while it's hot.
        _writer = new StreamWriter(
            new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
            Utf8NoBom) { AutoFlush = true };
        TrimOldFiles();
    }

    private static string NewFileName(string runId)
    {
        // Timestamp prefix sorts chronologically; runId disambiguates same-second runs.
        var safeRunId = new string(runId.Select(ch =>
            Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
        return $"{FilePrefix}{DateTime.Now:yyyyMMdd-HHmmss}-{safeRunId}.log";
    }

    private void TrimOldFiles()
    {
        try
        {
            var stale = Directory.EnumerateFiles(_directory, FilePrefix + "*.log")
                .OrderByDescending(Path.GetFileName)
                .Skip(KeepFileCount);
            foreach (var file in stale)
            {
                try
                {
                    File.Delete(file);
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

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
            _currentRunId = null;
        }
    }
}
