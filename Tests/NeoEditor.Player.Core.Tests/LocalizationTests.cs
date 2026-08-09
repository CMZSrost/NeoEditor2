using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoEditor.Player.Core.Data;
using NeoEditor.Player.Core.Services;
using NeoEditor.Player.Core.ViewModels;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>
/// Data-browser localization tests (Docs/42 v2.72): the (table, column) → [Display] resx
/// key mapping, the WikiDetailBuilder's localized markdown via the injected text delegate,
/// and the row-summary column-label path. The built-in Chinese defaults (no delegate) keep
/// the pre-v2.72 output — covered by WikiDetailBuilderTests.
/// </summary>
public class LocalizationTests
{
    private static GameDataField F(string column, string value) => new(column, value);

    private static GameDataRow Row(string table, params GameDataField[] fields) => new(table, fields);

    private static GameDataCatalog Catalog(params (string Table, IReadOnlyList<GameDataRow> Rows)[] tables)
        => new(tables.ToDictionary(t => t.Table, t => t.Rows, StringComparer.OrdinalIgnoreCase));

    // ── GameTableMap column → display key ──

    [Fact]
    public void FieldDisplayKeyUsesDisplayAttribute()
    {
        Assert.Equal("Name", GameTableMap.GetFieldDisplayKey("creatures", "strName"));
        Assert.Equal("ItemId", GameTableMap.GetFieldDisplayKey("chargeprofiles", "strItemID"));
        Assert.Equal("Tools", GameTableMap.GetFieldDisplayKey("recipes", "strTools"));
        Assert.Equal("SpriteList", GameTableMap.GetFieldDisplayKey("itemtypes", "vSpriteList"));
    }

    [Fact]
    public void FieldDisplayKeyFallsBackToPropertyName()
    {
        // itemtypes has no [Display] on its core columns — the property name is the key.
        Assert.Equal("Id", GameTableMap.GetFieldDisplayKey("itemtypes", "id"));
        Assert.Equal("Name", GameTableMap.GetFieldDisplayKey("itemtypes", "strName"));
        Assert.Equal("Durability", GameTableMap.GetFieldDisplayKey("itemtypes", "fDurability"));
    }

    [Fact]
    public void FieldDisplayKeyUnknownColumnIsNull()
    {
        Assert.Null(GameTableMap.GetFieldDisplayKey("itemtypes", "noSuchColumn"));
        Assert.Null(GameTableMap.GetFieldDisplayKey("unknown_table", "id"));
    }

    // ── WikiDetailBuilder localized output ──

    /// <summary>Fake text provider: known keys → values, unknown keys → the key itself
    /// (mimics LocalizationManager's fallback so T() falls back to the built-in Chinese).</summary>
    private static Func<string, string> ZhText() => key => key switch
    {
        "Table.itemtypes" => "物品类型",
        "Table.treasuretable" => "战利品表",
        "Wiki.ID" => "ID",
        "Wiki.Nested" => "嵌套",
        "Wiki.Suppress" => "抑制",
        "Wiki.Identify" => "识别",
        "FieldName.Name" => "名称",
        "FieldName.Durability" => "耐久度",
        "FieldName.Id" => "代码标号",
        "FieldDesc.Name" => "物品名称",
        _ => key,
    };

    [Fact]
    public void BuildGenericUsesLocalizedTableNameAndIdLabel()
    {
        var builder = new WikiDetailBuilder(Catalog(("itemtypes", new[] { Row("itemtypes", F("nID", "5"), F("strName", "Knife")) })),
            text: ZhText());

        var md = builder.BuildDetail(Row("itemtypes", F("nID", "5"), F("strName", "Knife")));

        Assert.Contains("> 物品类型 · ID `5`", md);
        Assert.DoesNotContain("itemtypes", md);
    }

    [Fact]
    public void BuildGenericFallsBackToRawTableNameForUnknownTables()
    {
        // 未知表没有 Table.* 键 → 显示原始表名（T 回退内置值 = 原始表名）。
        var builder = new WikiDetailBuilder(Catalog(("mystery", new[] { Row("mystery", F("id", "1")) })),
            text: ZhText());

        var md = builder.BuildDetail(Row("mystery", F("id", "1")));

        Assert.Contains("> mystery · ID `1`", md);
    }

    [Fact]
    public void GetFieldsCarriesLocalizedDisplayNameAndDescription()
    {
        var builder = new WikiDetailBuilder(Catalog(("itemtypes", new[] { Row("itemtypes", F("id", "5"), F("strName", "Knife")) })),
            text: ZhText());

        var fields = builder.GetFields(Row("itemtypes", F("id", "5"), F("strName", "Knife")));

        var name = Assert.Single(fields, f => f.Column == "strName");
        Assert.Equal("名称", name.DisplayName);
        Assert.Equal("物品名称", name.Description);
        var id = Assert.Single(fields, f => f.Column == "id");
        Assert.Equal("代码标号", id.DisplayName);
    }

    [Fact]
    public void GetFieldsKeepsRawColumnWhenUntranslated()
    {
        var builder = new WikiDetailBuilder(Catalog(("itemtypes", new[] { Row("itemtypes", F("nID", "5"), F("vImageUsage", "x")) })),
            text: ZhText());

        var fields = builder.GetFields(Row("itemtypes", F("nID", "5"), F("vImageUsage", "x")));

        // vImageUsage → property ImageUsage → FieldName.ImageUsage 不在测试文本表 → 原始列名
        var usage = Assert.Single(fields, f => f.Column == "vImageUsage");
        Assert.Equal("vImageUsage", usage.DisplayName);
        Assert.Equal("", usage.Description);
    }

    [Fact]
    public void BuildTreasureTableLocalizesFlagsAndDropHeading()
    {
        var tt = Row("treasuretable", F("id", "7"), F("strName", "Junk"), F("bNested", "1"), F("aTreasures", "3x1x1"));
        var items = new[] { Row("itemtypes", F("id", "3"), F("strName", "Junk Item")) };
        var builder = new WikiDetailBuilder(Catalog(("treasuretable", new[] { tt }), ("itemtypes", items)),
            text: ZhText());

        var md = builder.BuildDetail(tt);

        Assert.Contains("嵌套", md);
        Assert.Contains("## 掉落物", md);
        Assert.DoesNotContain("Nested", md);
    }

    // ── row summary column labels ──

    [Fact]
    public void RowSummaryUsesLocalizedColumnLabelsWhenProvided()
    {
        var row = new GameDataRow("itemtypes", new[] { F("strName", "Knife"), F("id", "5") });
        row.ColumnLabel = column => column switch
        {
            "strName" => "名称",
            "id" => "代码标号",
            _ => column,
        };

        Assert.Contains("名称:Knife", row.Summary);
        Assert.Contains("代码标号:5", row.Summary);
        Assert.DoesNotContain("strName:", row.Summary);
    }

    [Fact]
    public void RowSummaryKeepsRawColumnsWithoutLabel()
    {
        var row = new GameDataRow("itemtypes", new[] { F("vImageUsage", "x") });

        Assert.Contains("vImageUsage:x", row.Summary);
    }
}
