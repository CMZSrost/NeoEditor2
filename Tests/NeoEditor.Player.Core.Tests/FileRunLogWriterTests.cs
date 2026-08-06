using System;
using System.IO;
using System.Linq;
using NeoEditor.Player.Core.Logging;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>
/// File run-log sink tests (Docs/42 v2.25): one player-run-*.log per run, only the newest
/// 2 files kept, per-line flush (crash-safe), crash lines, and run-id sanitization.
/// </summary>
public class FileRunLogWriterTests : IDisposable
{
    private readonly string _dir;
    private readonly RunLogStore _store = new();

    public FileRunLogWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wv-runlog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private FileRunLogWriter NewWriter() => new(_store, _dir);

    private static string[] Files(string dir) =>
        Directory.EnumerateFiles(dir, "player-run-*.log")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Read a hot log file — the writer holds it with FileShare.ReadWrite.</summary>
    private static string ReadAll(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void WritesLinesToRunFile()
    {
        using var writer = NewWriter();
        _store.Append("100", "console", "hello ruffle");

        var files = Files(_dir);
        var file = Assert.Single(files);
        var content = ReadAll(file);
        Assert.Contains("[console] hello ruffle", content);
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}\.\d{3} ", content);
    }

    [Fact]
    public void CreatesNewFilePerRun()
    {
        using var writer = NewWriter();
        _store.Append("100", "console", "run one");
        _store.Append("101", "error", "run two");

        Assert.Equal(2, Files(_dir).Length);
    }

    [Fact]
    public void KeepsOnlyNewestTwoFiles()
    {
        using var writer = NewWriter();
        _store.Append("1", "console", "first");
        _store.Append("2", "console", "second");
        _store.Append("3", "console", "third");

        var files = Files(_dir);
        Assert.Equal(2, files.Length);
        Assert.Contains(files, f => f.EndsWith("-2.log", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("-3.log", StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => f.EndsWith("-1.log", StringComparison.Ordinal));
    }

    [Fact]
    public void TrimsPreExistingOldFiles()
    {
        // simulate leftovers from previous sessions: 3 old files, keep newest 2
        foreach (var id in new[] { "111", "222", "333" })
            File.WriteAllText(Path.Combine(_dir, $"player-run-20260101-000000-{id}.log"), "old");

        using var writer = NewWriter();

        var files = Files(_dir);
        Assert.Equal(2, files.Length);
        Assert.Contains(files, f => f.EndsWith("-222.log", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("-333.log", StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => f.EndsWith("-111.log", StringComparison.Ordinal));
    }

    [Fact]
    public void SanitizesInvalidRunIdCharacters()
    {
        using var writer = NewWriter();
        _store.Append("?", "console", "odd run id");

        var files = Files(_dir);
        var file = Assert.Single(files);
        Assert.DoesNotContain('?', file);
        Assert.EndsWith(".log", file);
    }

    [Fact]
    public void WriteCrashAppendsFatalLine()
    {
        using var writer = NewWriter();
        _store.Append("100", "console", "some lines");
        writer.WriteCrash("NullReferenceException: boom");

        var file = Files(_dir).Single();
        var content = ReadAll(file);
        Assert.Contains("[FATAL] NullReferenceException: boom", content);
    }
}
