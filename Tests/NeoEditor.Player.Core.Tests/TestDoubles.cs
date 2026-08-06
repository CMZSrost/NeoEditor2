using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Player.Core.Tests;

/// <summary>Minimal IConfigService fake with a settable AppConfig.</summary>
internal sealed class FakeConfigService : IConfigService
{
    public AppConfig Config { get; } = new();

    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;
}

/// <summary>Fake live-data source returning canned pma_xml_export text per table.</summary>
internal sealed class FakeGameDataExportService : IGameDataExportService
{
    private readonly Dictionary<string, string> _tables = new(StringComparer.OrdinalIgnoreCase);

    public FakeGameDataExportService Add(string tableName, string xml)
    {
        _tables[tableName] = xml;
        return this;
    }

    public Task<string?> ExportTableXmlAsync(string tableName)
        => Task.FromResult(_tables.GetValueOrDefault(tableName));
}

/// <summary>Shared temp-folder helpers for scanner/server tests.</summary>
internal static class TestFs
{
    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
