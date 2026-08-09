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
    {
        var extractor = new EncounterSemanticsExtractor(lookup, resolver, new StubLocalizationService(), _ => null);
        return new VizSnapshotService(host, xmlParser, lookup, extractor);
    }

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

    // ── 非 Encounter 类型：通用快照兜底 ─────────────────────────────────

    [Fact]
    public void BuildById_OtherEntityType_NoSemantics()
    {
        var item = new ItemType { EntityId = "52", Name = "撬棍" };
        var host = new StubHostService { Cache = { ["52"] = item } };
        var lookup = new StubEntityLookupService { ReferenceLookups = { [typeof(ItemType)] = new List<object> { item } } };
        var service = CreateService(host, new StubXmlParser(), lookup, new StubReferenceResolver());

        var snapshot = service.BuildById("ItemType", "52");

        Assert.NotNull(snapshot);
        Assert.Null(snapshot!.Semantics); // P1 渲染器
        Assert.Equal("撬棍", snapshot.RawXml is null ? snapshot.DisplayName : snapshot.DisplayName);
    }
}
