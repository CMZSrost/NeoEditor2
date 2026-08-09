using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Xml.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// D09 §3.1: assembles EntitySnapshotDto from an entity (by id, via IHostService
/// cache — R24) or from raw XML (IXmlParser.ImportEntities, the "传 XML 看效果"
/// channel). Type dispatch: Encounter gets full semantics; other types fall back
/// to identity + rawXml until their extractor exists.
/// </summary>
public sealed class VizSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  // keep CJK readable in /viz/data
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IHostService _host;
    private readonly IXmlParser _xmlParser;
    private readonly IEntityLookupService _dataTable;
    private readonly EncounterSemanticsExtractor _encounterExtractor;

    public VizSnapshotService(
        IHostService host,
        IXmlParser xmlParser,
        IEntityLookupService dataTable,
        EncounterSemanticsExtractor encounterExtractor)
    {
        _host = host;
        _xmlParser = xmlParser;
        _dataTable = dataTable;
        _encounterExtractor = encounterExtractor;
    }

    public string Serialize(EntitySnapshotDto snapshot) => JsonSerializer.Serialize(snapshot, JsonOptions);

    public EntitySnapshotDto? BuildById(string entityType, string entityId, ISet<string>? preConds = null)
    {
        var entity = _host.GetCachedEntity(entityId)
                     ?? FindInLookups(entityType, entityId);
        if (entity is null) return null;
        return Build(entity, preConds);
    }

    /// <summary>XML 输入通道：<c>?type=Encounter&amp;xml=&lt;text&gt;</c> → parse → same pipeline.</summary>
    public EntitySnapshotDto? BuildFromXml(string entityType, string xml)
    {
        var type = ResolveEntityType(entityType);
        if (type is null) return null;

        try
        {
            var method = typeof(IXmlParser).GetMethod(nameof(IXmlParser.ImportEntities))!
                .MakeGenericMethod(type);
            var imported = (System.Collections.IList)method.Invoke(_xmlParser,
                new object[] { XDocument.Parse(xml), 0, "viz-snapshot.xml" })!;
            var entity = imported.OfType<IEntity>().FirstOrDefault();
            return entity is null ? null : Build(entity);
        }
        catch (Exception)
        {
            return null; // XML parse error → 400-ish (null → 404 by caller)
        }
    }

    private EntitySnapshotDto Build(IEntity entity, ISet<string>? preConds = null)
    {
        var snapshot = new EntitySnapshotDto
        {
            Type = entity.GetType().Name,
            Id = entity.EntityId,
            ModId = entity.ModId < 0 ? null : entity.ModId.ToString(),
            DisplayName = entity.Subject ?? entity.EntityId,
            RawXml = GenerateXmlFragment(entity),
        };

        if (entity is Encounter enc)
            snapshot = snapshot with { Semantics = _encounterExtractor.Extract(enc, preConds) };
        // Other entity types: identity + rawXml only until their extractor ships (P1).
        return snapshot;
    }

    private IEntity? FindInLookups(string entityType, string entityId)
    {
        var type = ResolveEntityType(entityType);
        if (type is null || !_dataTable.ReferenceLookups.TryGetValue(type, out var list)) return null;
        // EntityId（缓存键）优先；未命中按数字主键（Id/nID）回退——页面分支卡导航传的是
        // 解析后的 EntityId（BranchDto.EntityId），未解析的灰色卡传数字 id 到这里兜底。
        return list.OfType<IEntity>().FirstOrDefault(e => e.EntityId == entityId)
               ?? list.OfType<IEntity>().FirstOrDefault(e => MatchIdColumn(e, entityId));
    }

    private static bool MatchIdColumn(IEntity entity, string id)
    {
        var prop = entity.GetType().GetProperty("Id")
                   ?? entity.GetType().GetProperty("nID");
        return prop?.GetValue(entity)?.ToString() == id;
    }

    private static Type? ResolveEntityType(string name)
        => typeof(Encounter).Assembly.GetTypes()
            .FirstOrDefault(t => t.Name == name && typeof(IEntity).IsAssignableFrom(t));

    /// <summary>Single-entity XML fragment in the game's pma_xml_export format
    /// (same shape as the editor's XML tab — round-trip compatible).</summary>
    public static string GenerateXmlFragment(IEntity entity)
    {
        var type = entity.GetType();
        var tableName = type.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>()
                            ?.Name ?? type.Name.ToLower();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version='1.0' encoding='utf8'?>");
        sb.AppendLine($"<table name=\"{tableName}\">");

        var props = type.GetProperties()
            .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>() != null
                        && p.DeclaringType != typeof(IEntity));
        foreach (var prop in props)
        {
            var colName = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()!.Name;
            var rawValue = ReferenceText.GetRawString(prop.GetValue(entity),
                prop.GetCustomAttribute<ReferenceFieldAttribute>());
            sb.AppendLine($"  <column name=\"{colName}\">{System.Security.SecurityElement.Escape(rawValue)}</column>");
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }
}
