using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;

namespace NeoEditor.Data.Repository;

/// <summary>
/// XML-backed repository bound to a single mod (R26 v2 symmetric contract).
/// Constructed per (entity type, modId); <see cref="LoadAsync"/> parses the mod's XML files
/// (same discovery as ModManager import), <see cref="SaveAsync"/> writes generated XML back to
/// the mod's files, <see cref="GetDiffAsync"/> produces file-level old/new snapshots.
/// Implements all five capabilities — no backend special-casing.
/// </summary>
public class XmlRepository<T> : RepositoryBase<T> where T : IEntity, new()
{
    private const string NewFilePlaceholder = "<!-- new file -->";

    private readonly int _modId;
    private readonly IXmlParser _xmlParser;
    private readonly IConfigService _configService;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;

    public XmlRepository(
        IHostService host,
        int modId,
        IXmlParser xmlParser,
        IConfigService configService,
        IDbContextFactory<EditorDbContext> editorDbFactory)
        : base(host)
    {
        _modId = modId;
        _xmlParser = xmlParser;
        _configService = configService;
        _editorDbFactory = editorDbFactory;
    }

    /// <inheritdoc />
    public override async Task<T?> GetByIdAsync(string entityId)
    {
        var all = await LoadAsync();
        return all.FirstOrDefault(e => e.EntityId == entityId);
    }

    /// <inheritdoc />
    public override Task<IReadOnlyList<T>> GetAllAsync() => LoadAsync();

    /// <inheritdoc />
    public override async Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates)
    {
        if (candidates.Count == 0) return [];

        var modDir = await ResolveModDirAsync();
        if (modDir is null) return [];

        var result = new List<RowDiff>();
        foreach (var fileGroup in candidates.GroupBy(e => e.FilePath))
        {
            var fullPath = ResolveFilePath(modDir, fileGroup.Key);

            var existed = File.Exists(fullPath);
            var oldXml = existed
                ? NormalizeXml(LoadXmlSafe(fullPath).ToString(SaveOptions.None))
                : NewFilePlaceholder;

            var exported = _xmlParser.Export(fileGroup.Cast<IEntity>());
            exported.Declaration = null;
            var newXml = NormalizeXml(exported.ToString(SaveOptions.None));

            // Only surface files whose generated XML differs from disk (matches View preview behavior).
            if (oldXml != newXml)
                result.Add(new RowDiff(
                    fullPath,
                    existed ? DiffKind.Modified : DiffKind.Added,
                    oldXml,
                    newXml));
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task SaveAsync(IEnumerable<T> entities)
    {
        var list = entities.ToList();
        if (list.Count == 0) return;

        var diffs = await GetDiffAsync(list);
        foreach (var diff in diffs)
        {
            var dir = Path.GetDirectoryName(diff.TargetId);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(diff.TargetId, diff.NewContent, new UTF8Encoding(false));
        }
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<T>> LoadAsync()
    {
        var modDir = await ResolveModDirAsync();
        if (modDir is null || !Directory.Exists(modDir)) return [];

        var result = new List<T>();
        foreach (var xmlPath in Directory.GetFiles(modDir, "*.xml", SearchOption.AllDirectories))
        {
            var doc = LoadXmlSafe(xmlPath);
            result.AddRange(_xmlParser.ImportEntities<T>(doc, _modId, xmlPath));
        }

        return result;
    }

    private async Task<string?> ResolveModDirAsync()
    {
        var root = _configService.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(root)) return null;

        await using var edb = await _editorDbFactory.CreateDbContextAsync();
        var mod = await edb.ModInfos.FirstOrDefaultAsync(m => m.ModId == _modId);
        var relPath = mod?.Path;

        return string.IsNullOrWhiteSpace(relPath)
            ? null
            : Path.GetFullPath(Path.Combine(root, relPath));
    }

    private string ResolveFilePath(string modDir, string? filePath)
    {
        var root = _configService.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(filePath))
            return Path.Combine(modDir, "neogame.xml");
        return Path.IsPathRooted(filePath)
            ? filePath
            : Path.GetFullPath(Path.Combine(root, filePath));
    }

    private static XDocument LoadXmlSafe(string path)
    {
        var text = File.ReadAllText(path);
        if (text.Contains("encoding=\"utf8\"", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("encoding=\"utf8\"", "encoding=\"utf-8\"", StringComparison.OrdinalIgnoreCase);
        return XDocument.Parse(text);
    }

    private static string NormalizeXml(string xml)
    {
        try
        {
            // Strip <?xml ...?> declaration line to prevent spurious diffs on every save
            if (xml.StartsWith("<?"))
            {
                var endIndex = xml.IndexOf("?>", StringComparison.Ordinal);
                if (endIndex >= 0)
                    xml = xml.Substring(endIndex + 2).TrimStart('\r', '\n');
            }

            var doc = XDocument.Parse(xml);
            return doc.ToString(SaveOptions.None);
        }
        catch
        {
            return xml;
        }
    }
}