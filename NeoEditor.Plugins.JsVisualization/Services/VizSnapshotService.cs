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
    private readonly ItemTypeSemanticsExtractor _itemTypeExtractor;
    private readonly CreatureSemanticsExtractor _creatureExtractor;
    private readonly RecipeSemanticsExtractor _recipeExtractor;
    private readonly ThinSemanticsExtractor _thinExtractor;
    private readonly TemplateSemanticsExtractor _templateExtractor;
    private readonly SemanticsShared _shared;

    public VizSnapshotService(
        IHostService host,
        IXmlParser xmlParser,
        IEntityLookupService dataTable,
        EncounterSemanticsExtractor encounterExtractor,
        ItemTypeSemanticsExtractor itemTypeExtractor,
        CreatureSemanticsExtractor creatureExtractor,
        RecipeSemanticsExtractor recipeExtractor,
        ThinSemanticsExtractor thinExtractor,
        TemplateSemanticsExtractor templateExtractor,
        SemanticsShared shared)
    {
        _host = host;
        _xmlParser = xmlParser;
        _dataTable = dataTable;
        _encounterExtractor = encounterExtractor;
        _itemTypeExtractor = itemTypeExtractor;
        _creatureExtractor = creatureExtractor;
        _recipeExtractor = recipeExtractor;
        _thinExtractor = thinExtractor;
        _templateExtractor = templateExtractor;
        _shared = shared;
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
            Image = FindImage(entity),
        };

        snapshot = snapshot with { Semantics = ExtractSemantics(entity, preConds) };
        // TopBar 审计统计（D10 §3.3：N 字段 · M 有值 · K 未解析）
        snapshot = snapshot with { Audit = _shared.BuildAudit(entity) };
        return snapshot;
    }

    /// <summary>类型分发：A 级 3 个走深度语义；C 级 3 个走薄提取器；剩余 17 个（B 级 7 +
    /// D 级 10）走模板提取器（D10 §四 全类型铺开）——24 类型全部可渲染。</summary>
    private object? ExtractSemantics(IEntity entity, ISet<string>? preConds) => entity switch
    {
        Encounter enc => _encounterExtractor.Extract(enc, preConds),
        ItemType it => _itemTypeExtractor.Extract(it),
        Creature c => _creatureExtractor.Extract(c),
        Recipe r => _recipeExtractor.Extract(r),
        ContainerType ct => _thinExtractor.ExtractContainerType(ct),
        BarterHex bh => _thinExtractor.ExtractBarterHex(bh),
        Map m => _thinExtractor.ExtractMap(m),
        // B 级 7 个（语义迁移）
        AttackMode am => _templateExtractor.ExtractAttackMode(am),
        Condition cond => _templateExtractor.ExtractCondition(cond),
        TreasureTable tt => _templateExtractor.ExtractTreasureTable(tt),
        HexType ht => _templateExtractor.ExtractHexType(ht),
        Faction f => _templateExtractor.ExtractFaction(f),
        BattleMove bm => _templateExtractor.ExtractBattleMove(bm),
        CampType camp => _templateExtractor.ExtractCampType(camp),
        // D 级 10 个（通用模板 + 特化）
        _ => _templateExtractor.ExtractThin(entity),
    };

    /// <summary>快照级 Hero 图：Encounter/Creature 图列、ItemType 首图、Map 名即图、
    /// AttackMode/CampType/DmcPlace/DataFile 图列；其余 null。</summary>
    private string? FindImage(IEntity entity) => entity switch
    {
        Encounter enc => _shared.ImageUrl(SemanticsShared.Raw(enc.Image, ",")),
        Creature c => _shared.ImageUrl(SemanticsShared.Raw(c.Image, ",")),
        ItemType it => _shared.ImageUrl(SemanticsShared.Raw(it.ImageList, ",").Split(',').FirstOrDefault()?.Trim()),
        Map m => _shared.ImageUrl(m.Name),
        AttackMode am => _shared.ImageUrl(SemanticsShared.Raw(am.Image, ",")),
        CampType camp => _shared.ImageUrl(SemanticsShared.Raw(camp.ImageList, ",").Split(',').FirstOrDefault()?.Trim()),
        DmcPlace d => _shared.ImageUrl(SemanticsShared.Raw(d.Image, ",")),
        DataFile df => _shared.ImageUrl(SemanticsShared.Raw(df.Image, ",")),
        _ => null,
    };

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
