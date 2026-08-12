using System.Collections.Generic;
using System.Text.Json;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using Xunit;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>D09 §3.1: snapshot assembly (by id / by XML) and the XML fragment contract.</summary>
public class VizSnapshotServiceTests
{
    private static VizSnapshotService CreateService(StubHostService host, StubXmlParser xmlParser,
        StubEntityLookupService lookup, StubReferenceResolver resolver)
        => TestSemantics.CreateService(host, xmlParser, lookup, resolver);

    // ── XML fragment（pma_xml_export 形状，round-trip）───────────────────

    [Fact]
    public void GenerateXmlFragment_ContainsTableAndColumns()
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "N", Type = EncounterType.Normal, Price = 5 };
        var xml = VizSnapshotService.GenerateXmlFragment(enc);

        Assert.Contains("<table name=\"encounters\">", xml);
        Assert.Contains("<column name=\"fPrice\">5</column>", xml);     // D09: 游戏列名原样导出
        Assert.Contains("<column name=\"strName\">N</column>", xml);
    }

    [Fact]
    public void GenerateXmlFragment_EscapesXmlSpecials()
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "A&B<C>", Type = EncounterType.Normal };
        var xml = VizSnapshotService.GenerateXmlFragment(enc);
        Assert.Contains("A&amp;B&lt;C&gt;", xml);
    }

    // ── 序列化契约（JS 页面的 JSON 形状）────────────────────────────────

    [Fact]
    public void Serialize_ProducesWebFriendlyJson()
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "剧情", Type = EncounterType.Normal };
        var host = new StubHostService { Cache = { ["90"] = enc } };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var service = CreateService(host, new StubXmlParser(), lookup, new StubReferenceResolver());

        var json = service.Serialize(service.BuildById("Encounter", "90")!);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("Encounter", root.GetProperty("type").GetString());
        Assert.Equal("90", root.GetProperty("id").GetString());
        Assert.True(root.TryGetProperty("rawXml", out _));
        // 中文不转义（UnsafeRelaxedJsonEscaping）
        Assert.Contains("剧情", json);
    }

    // ── BuildById：走缓存（R24 只读通道）────────────────────────────────

    [Fact]
    public void BuildById_ResolvesFromHostCache()
    {
        var enc = new Encounter
        {
            Id = 90, EntityId = "90", Name = "缓存剧情", Type = EncounterType.Normal,
            Responses = "=90x1x0x0x0",
        };
        var host = new StubHostService { Cache = { ["90"] = enc } };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var service = CreateService(host, new StubXmlParser(), lookup, new StubReferenceResolver());

        var snapshot = service.BuildById("Encounter", "90");

        Assert.NotNull(snapshot);
        Assert.Equal("缓存剧情", snapshot!.DisplayName);
        Assert.NotNull(snapshot.Semantics as EncounterSemantics);
        Assert.Equal("stay", ((EncounterSemantics)snapshot.Semantics).Flow.Branches[0].EndKind);
    }

    // ── BuildFromXml：「传 XML 看效果」通道 ───────────────────────────────

    [Fact]
    public void BuildFromXml_ImportsAndExtracts()
    {
        var enc = new Encounter { Id = 90, EntityId = "90", Name = "XML 剧情", Type = EncounterType.Normal };
        var xmlParser = new StubXmlParser { Imported = new List<object> { enc } };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(Encounter)] = new List<object>() } };
        var service = CreateService(new StubHostService(), xmlParser, lookup, new StubReferenceResolver());

        var snapshot = service.BuildFromXml("Encounter", "<table name=\"encounters\"/>");

        Assert.NotNull(snapshot);
        Assert.Equal("XML 剧情", snapshot!.DisplayName);
    }

    [Fact]
    public void BuildFromXml_UnknownType_ReturnsNull()
    {
        var service = CreateService(new StubHostService(), new StubXmlParser(),
            new StubEntityLookupService(), new StubReferenceResolver());
        Assert.Null(service.BuildFromXml("NoSuchType", "<table/>"));
    }

    // ── 类型分发（P1）：ItemType 全语义；无提取器的类型 → 通用快照兜底 ──

    [Fact]
    public void BuildById_ItemType_HasFullSemantics()
    {
        var item = new ItemType { EntityId = "52", Name = "猎刀", GroupId = 0, SubgroupId = 0 };
        var host = new StubHostService { Cache = { ["52"] = item } };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(ItemType)] = new List<object> { item } } };
        var service = CreateService(host, new StubXmlParser(), lookup, new StubReferenceResolver());

        var snapshot = service.BuildById("ItemType", "52");

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot!.Semantics as ItemTypeSemantics);
        Assert.Equal("0.0", ((ItemTypeSemantics)snapshot.Semantics).Gs);
    }

    [Fact]
    public void BuildById_ThinType_HasTemplateSemantics()
    {
        // P4 全类型铺开：D 级薄类型（如 ItemProp）→ 模板语义（字段表 + refs）
        var prop = new ItemProp { EntityId = "p1", PropertyName = "锋利" };
        var host = new StubHostService { Cache = { ["p1"] = prop } };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(ItemProp)] = new List<object> { prop } } };
        var service = CreateService(host, new StubXmlParser(), lookup, new StubReferenceResolver());

        var snapshot = service.BuildById("ItemProp", "p1");

        Assert.NotNull(snapshot);
        var sem = Assert.IsType<TemplateSemantics>(snapshot!.Semantics);
        Assert.NotNull(snapshot.Audit);   // TopBar 审计统计对所有类型生效
        Assert.True(snapshot.Audit.Fields > 0);
    }

    [Fact]
    public void BuildById_AttackMode_HasTemplateSemantics()
    {
        var am = new AttackMode { EntityId = "14", Name = "挥击", DamageCut = 4, Morale = 0.5 };
        var host = new StubHostService { Cache = { ["14"] = am } };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(AttackMode)] = new List<object> { am } } };
        var service = CreateService(host, new StubXmlParser(), lookup, new StubReferenceResolver());

        var snapshot = service.BuildById("AttackMode", "14");

        Assert.NotNull(snapshot);
        var sem = Assert.IsType<TemplateSemantics>(snapshot!.Semantics);
        // 战斗区块带 Mode（单攻击模式详情）
        Assert.Contains(sem.Blocks, b => b.Mode is not null);
    }
}
