using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NeoEditor.Player.Core.Data;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>
/// Wiki-style detail page generator tests (Docs/42 v2.22): generic field tables,
/// recipes crafting cards, treasuretable loot probability trees (nested + cycle),
/// and cross-table db:// reference links.
/// </summary>
public class WikiDetailBuilderTests
{
    private static GameDataField F(string column, string value) => new(column, value);

    private static GameDataRow Row(string table, params GameDataField[] fields) => new(table, fields);

    private static WikiDetailBuilder Builder(params (string Table, IReadOnlyList<GameDataRow> Rows)[] tables)
        => new(new GameDataCatalog(
            tables.ToDictionary(t => t.Table, t => t.Rows, StringComparer.OrdinalIgnoreCase)));

    private static WikiDetailBuilder BuilderWithImageRoot(string imageRoot,
        params (string Table, IReadOnlyList<GameDataRow> Rows)[] tables)
        => new(new GameDataCatalog(
            tables.ToDictionary(t => t.Table, t => t.Rows, StringComparer.OrdinalIgnoreCase)), imageRoot);

    // ── generic table ──

    [Fact]
    public void GenericTableRendersFieldTableWithTruncation()
    {
        var builder = Builder(("itemtypes", new[] { Row("itemtypes", F("nID", "5"), F("strName", "Knife")) }));
        var longValue = new string('x', 500);

        var md = builder.Build(Row("itemtypes", F("nID", "5"), F("strName", "Knife"), F("strDescription", longValue)));

        Assert.Contains("# Knife", md);
        Assert.Contains("| `nID` | 5 |", md);
        Assert.Contains("…", md);                       // long value truncated
        Assert.DoesNotContain("db://", md);             // no reference columns here
    }

    [Fact]
    public void GenericTableResolvesReferenceColumnsToLinks()
    {
        // creatures.nTreasureID → treasuretable (reference metadata from the entity model)
        var builder = Builder(("treasuretable", new[]
        {
            Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")),
        }));

        var md = builder.Build(Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "7")));

        Assert.Contains("[Junk Pile](db://treasuretable/7)", md);
    }

    // ── recipes ──

    [Fact]
    public void RecipeTemplateLinksIngredientsAndProduct()
    {
        var builder = Builder(
            ("ingredients", new[]
            {
                Row("ingredients", F("nID", "3"), F("strName", "Water")),
                Row("ingredients", F("nID", "5"), F("strName", "Tape")),
            }),
            ("treasuretable", new[]
            {
                Row("treasuretable", F("id", "7"), F("strName", "Junk Pile"), F("aTreasures", "G.Sx10")),
            }));

        var md = builder.Build(Row("recipes", F("nID", "1"), F("strName", "Repair"),
            F("strTools", "2x3+1x5"), F("strConsumed", "1x3"), F("nTreasureID", "7"), F("fHours", "2.5")));

        Assert.Contains("# Repair", md);
        Assert.Contains("[Water](db://ingredients/3)", md);
        Assert.Contains("×2", md);                          // tool qty from {mult}x{id}
        Assert.DoesNotContain("×1", md);                    // qty 1 is implicit
        Assert.Contains("[Tape](db://ingredients/5)", md);
        Assert.Contains("[Junk Pile](db://treasuretable/7)", md);
        Assert.Contains("耗时 2.5h", md);
    }

    [Fact]
    public void RecipeAlsoTryAndHiddenRenderLinks()
    {
        var builder = Builder(("recipes", new[]
        {
            Row("recipes", F("nID", "9"), F("strName", "Alternate")),
            Row("recipes", F("nID", "22"), F("strName", "Hidden One")),
        }));

        var md = builder.Build(Row("recipes", F("nID", "1"), F("strName", "Repair"),
            F("vAlsoTry", "9"), F("nHiddenID", "22")));

        Assert.Contains("[Alternate](db://recipes/9)", md);
        Assert.Contains("[Hidden One](db://recipes/22)", md);
    }

    // ── treasuretable ──

    [Fact]
    public void TreasureTableComputesProbabilitiesAcrossAllItems()
    {
        var builder = Builder(("itemtypes", new[]
        {
            // composite key "G.S" (GroupId.SubgroupId) — dotted ids resolve to itemtypes
            Row("itemtypes", F("nID", "1"), F("strGroupID", "G"), F("strSubgroupID", "S"), F("strName", "Scrap Metal")),
        }));

        // "G.Sx10x2|3x30" — OR branch flattened; total weight 40 → 25% / 75%
        var md = builder.Build(Row("treasuretable", F("id", "10"), F("strName", "Loot Table"),
            F("aTreasures", "G.Sx10x2|3x30")));

        Assert.Contains("# Loot Table", md);
        Assert.Contains("`25.0%`", md);
        Assert.Contains("`75.0%`", md);
        Assert.Contains("×2", md);
        Assert.Contains("[Scrap Metal](db://itemtypes/G.S)", md);
        Assert.Contains("`3`", md);                          // unresolved plain id shown raw
    }

    [Fact]
    public void TreasureTableNestedRecursionAndCycleDetection()
    {
        var builder = Builder(("treasuretable", new[]
        {
            Row("treasuretable", F("id", "1"), F("strName", "Outer"), F("aTreasures", "5x1")),
            // Inner refers back to Outer (cycle) and to a missing id (unresolved)
            Row("treasuretable", F("id", "5"), F("strName", "Inner"), F("aTreasures", "1x1,99x1")),
        }));

        var md = builder.Build(Row("treasuretable", F("id", "1"), F("strName", "Outer"), F("aTreasures", "5x1")));

        Assert.Contains("[Inner](db://treasuretable/5)", md);
        Assert.Contains("循环引用", md);                       // Inner → Outer back edge
        Assert.Contains("99", md);                            // unresolved id shown raw
        Assert.Contains("未解析", md);
    }

    [Fact]
    public void EmptyTreasureTableShowsPlaceholder()
    {
        var builder = Builder(("treasuretable", Array.Empty<GameDataRow>()));

        var md = builder.Build(Row("treasuretable", F("id", "2"), F("strName", "Empty"), F("aTreasures", "")));

        Assert.Contains("（无掉落条目）", md);
        Assert.DoesNotContain("掉落物", md);
    }

    // ── field grid API (v2.34) ──

    [Fact]
    public void GetFieldsPreservesMultiLineValues()
    {
        var builder = Builder(("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Knife")) }));
        var multiLine = "line one\nline two\r\nline three";

        var fields = builder.GetFields(Row("itemtypes", F("nID", "1"), F("strName", "Knife"), F("strDescription", multiLine)));

        var desc = Assert.Single(fields, f => f.Column == "strDescription");
        Assert.Equal(multiLine, desc.Value);          // raw value, line breaks intact
        Assert.True(desc.ShowRawValue);
    }

    [Fact]
    public void GetFieldsExcludesImageColumns()
    {
        var builder = Builder(("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Bag")) }));

        var fields = builder.GetFields(Row("itemtypes", F("nID", "1"), F("strName", "Bag"),
            F("vImageList", "ItmBag.png")));

        Assert.DoesNotContain(fields, f => f.Column == "vImageList");
    }

    [Fact]
    public void GetFieldsExcludesRecipeShownColumns()
    {
        var builder = Builder(("ingredients", new[] { Row("ingredients", F("nID", "3"), F("strName", "Water")) }));

        var fields = builder.GetFields(Row("recipes", F("nID", "1"), F("strName", "Repair"),
            F("strTools", "2x3"), F("bScrap", "1"), F("strSecretName", "hidden")));

        Assert.DoesNotContain(fields, f => f.Column == "strTools");
        Assert.DoesNotContain(fields, f => f.Column == "bScrap");
        Assert.Contains(fields, f => f.Column == "strSecretName");
    }

    [Fact]
    public void GetFieldsResolvesReferenceColumnsToLinks()
    {
        var builder = Builder(("treasuretable", new[]
        {
            Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")),
        }));

        var fields = builder.GetFields(Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "7")));

        var treasure = Assert.Single(fields, f => f.Column == "nTreasureID");
        Assert.False(treasure.ShowRawValue);
        var link = Assert.Single(treasure.Links!);
        Assert.Equal("Junk Pile", link.Display);
        Assert.Equal("db://treasuretable/7", link.Target);
    }

    [Fact]
    public void GetFieldsKeepsUnresolvableIdsAsPlainText()
    {
        var builder = Builder(("treasuretable", Array.Empty<GameDataRow>()));

        var fields = builder.GetFields(Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "999")));

        var treasure = Assert.Single(fields, f => f.Column == "nTreasureID");
        var link = Assert.Single(treasure.Links!);
        Assert.Equal("999", link.Display);
        Assert.Null(link.Target);                    // unresolvable → plain text, not a link
    }

    // ── image gallery (v2.24) ──

    private static string NewImageRoot()
        => Path.Combine(Path.GetTempPath(), "wv-wiki-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ImageGalleryRendersExistingFilesInGrid()
    {
        var root = NewImageRoot();
        Directory.CreateDirectory(Path.Combine(root, "img"));
        try
        {
            File.WriteAllText(Path.Combine(root, "img", "ItmBag.png"), "x");
            File.WriteAllText(Path.Combine(root, "img", "ItmBagStored.png"), "x");
            var builder = BuilderWithImageRoot(root,
                ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Bag")) }));

            var md = builder.Build(Row("itemtypes", F("nID", "1"), F("strName", "Bag"),
                F("vImageList", "ItmBag.png,ItmBagStored.png")));

            Assert.Contains("## 图片", md);
            Assert.Contains("![ItmBag.png](img/ItmBag.png)", md);
            Assert.Contains("![ItmBagStored.png](img/ItmBagStored.png)", md);
            Assert.Contains("| ", md);                       // grid row
            Assert.DoesNotContain("缺失", md);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ModImagesResolveFromModsDirWhenNotInGameImg()
    {
        // R54-R56: mod 图片目录 = 主 img/ + mod 目录（根 + img 子目录）。mod 路径
        // 默认两层结构 Mods/<分组>/<mod>（ModListScanner 同款兜底）；getmods.php
        // 存在时以它声明的 strModURL 为准（见下个用例）。
        var root = NewImageRoot();
        Directory.CreateDirectory(Path.Combine(root, "img"));
        Directory.CreateDirectory(Path.Combine(root, "Mods", "cat", "m1", "img"));
        try
        {
            File.WriteAllText(Path.Combine(root, "Mods", "cat", "m1", "img", "ModWeapon.png"), "x");
            File.WriteAllText(Path.Combine(root, "Mods", "cat", "m1", "ModRootPic.png"), "x");
            File.WriteAllText(Path.Combine(root, "img", "VanillaItem.png"), "x");
            var builder = BuilderWithImageRoot(root,
                ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Item")) }));

            var md = builder.Build(Row("itemtypes", F("nID", "1"), F("strName", "Item"),
                F("vImageList", "VanillaItem.png,ModWeapon.png,ModRootPic.png")));

            Assert.Contains("![VanillaItem.png](img/VanillaItem.png)", md);
            Assert.Contains("![ModWeapon.png](img/ModWeapon.png)", md);   // Mods/cat/m1/img
            Assert.Contains("![ModRootPic.png](img/ModRootPic.png)", md);  // Mods/cat/m1 根目录
            Assert.DoesNotContain("缺失", md);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ModImagesResolveFromGetmodsPhpDeclaredPaths()
    {
        // R56: 游戏自带 getmods.php 声明 mod 路径（strModURL{i}，可在任意位置）——
        // 数据浏览器以它为准收集图片目录，不再假设 Mods/ 固定布局。
        var root = NewImageRoot();
        Directory.CreateDirectory(Path.Combine(root, "img"));
        Directory.CreateDirectory(Path.Combine(root, "CustomMods", "m9", "img"));
        try
        {
            File.WriteAllText(Path.Combine(root, "getmods.php"),
                "nRows=1&strModName0=m9&strModURL0=CustomMods/m9");
            File.WriteAllText(Path.Combine(root, "CustomMods", "m9", "img", "ExtPic.png"), "x");
            var builder = BuilderWithImageRoot(root,
                ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Item")) }));

            var md = builder.Build(Row("itemtypes", F("nID", "1"), F("strName", "Item"),
                F("vImageList", "ExtPic.png")));

            Assert.Contains("![ExtPic.png](img/ExtPic.png)", md);   // CustomMods/m9/img
            Assert.DoesNotContain("缺失", md);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ModImagesFallBackToGetmods2WhenGetmodsIsEmpty()
    {
        // R57: 用户目录的 getmods.php 是空壳（nRows=0），真正生效的是 getmods2.php——
        // 两个都读，任一解析出路径即用（实测 D:/Downloads/Neo Scavenger/）。
        var root = NewImageRoot();
        Directory.CreateDirectory(Path.Combine(root, "img"));
        Directory.CreateDirectory(Path.Combine(root, "Mods", "NeoScavExtended", "NSExtended", "img"));
        try
        {
            File.WriteAllText(Path.Combine(root, "getmods.php"), "nRows=0");
            // 真实格式：多行 + 每行末尾换行（值末尾带 \n 必须 Trim，否则路径带换行找不到目录）
            File.WriteAllText(Path.Combine(root, "getmods2.php"),
                "nRows=1&strModName0=NSE&strModURL0=Mods/NeoScavExtended/NSExtended\n");
            File.WriteAllText(Path.Combine(root, "Mods", "NeoScavExtended", "NSExtended", "img", "NsePic.png"), "x");
            var builder = BuilderWithImageRoot(root,
                ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Item")) }));

            var md = builder.Build(Row("itemtypes", F("nID", "1"), F("strName", "Item"),
                F("vImageList", "NsePic.png")));

            Assert.Contains("![NsePic.png](img/NsePic.png)", md);   // getmods2 声明的路径
            Assert.DoesNotContain("缺失", md);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingImagesShownAsText()
    {
        var root = NewImageRoot();
        Directory.CreateDirectory(root);
        try
        {
            var builder = BuilderWithImageRoot(root,
                ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Bag")) }));

            var md = builder.Build(Row("itemtypes", F("nID", "1"), F("strName", "Bag"),
                F("vImageList", "ItmMissing.png")));

            Assert.Contains("ItmMissing.png", md);
            Assert.Contains("缺失", md);
            Assert.DoesNotContain("![", md);                  // no broken image links
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SpriteListValueEqualsIdRendersImage()
    {
        var root = NewImageRoot();
        Directory.CreateDirectory(Path.Combine(root, "img"));
        try
        {
            File.WriteAllText(Path.Combine(root, "img", "CreItmBagL.png"), "x");
            var builder = BuilderWithImageRoot(root,
                ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Bag")) }));

            // vSpriteList pattern {value}={id} → id is the file name
            var md = builder.Build(Row("itemtypes", F("nID", "1"), F("strName", "Bag"),
                F("vSpriteList", "20=CreItmBagL.png")));

            Assert.Contains("![CreItmBagL.png](img/CreItmBagL.png)", md);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NamespacePrefixIsStripped()
    {
        var root = NewImageRoot();
        Directory.CreateDirectory(Path.Combine(root, "img"));
        try
        {
            File.WriteAllText(Path.Combine(root, "img", "AModeSpear.png"), "x");
            var builder = BuilderWithImageRoot(root,
                ("attackmodes", new[] { Row("attackmodes", F("nID", "1")) }));

            // "ns:FileName" style (defensive — real data is bare file names)
            var md = builder.Build(Row("attackmodes", F("nID", "1"), F("strIMG", "0:AModeSpear.png")));

            Assert.Contains("![AModeSpear.png](img/AModeSpear.png)", md);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ── side-pane split API (v2.26) ──

    [Fact]
    public void GetImageItemsListsExistingAndMissing()
    {
        var root = NewImageRoot();
        Directory.CreateDirectory(Path.Combine(root, "img"));
        try
        {
            File.WriteAllText(Path.Combine(root, "img", "ItmBag.png"), "x");
            var builder = BuilderWithImageRoot(root,
                ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Bag")) }));

            var images = builder.GetImageItems(Row("itemtypes", F("nID", "1"), F("strName", "Bag"),
                F("vImageList", "ItmBag.png,ItmMissing.png")));

            Assert.Equal(2, images.Count);
            Assert.True(images[0].Exists);
            Assert.Equal(Path.Combine(root, "img", "ItmBag.png"), images[0].FullPath);
            Assert.False(images[1].Exists);
            Assert.Contains("缺失", images[1].DisplayText);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildDetailExcludesGalleryAndReferences()
    {
        var builder = Builder(
            ("creatures", new[] { Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "7")) }),
            ("treasuretable", new[] { Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")) }));

        var detail = builder.BuildDetail(Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")));

        Assert.Contains("# Junk Pile", detail);
        Assert.DoesNotContain("被引用", detail);
        Assert.DoesNotContain("## 图片", detail);
    }

    [Fact]
    public void BuildReferencesSplitsOutIncomingSection()
    {
        var builder = Builder(
            ("creatures", new[] { Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "7")) }),
            ("treasuretable", new[] { Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")) }));

        var target = Row("treasuretable", F("id", "7"), F("strName", "Junk Pile"));
        var references = builder.BuildReferences(target);

        Assert.Contains("## 被引用（1）", references);
        Assert.DoesNotContain("# Junk Pile", references);
        Assert.DoesNotContain("## 字段", references);
    }

    [Fact]
    public void BuildReferenceGroupsGroupsBySourceTable()
    {
        var builder = Builder(
            ("creatures", new[] { Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "7")) }),
            ("hextypes", new[]
            {
                Row("hextypes", F("nID", "9"), F("strName", "Forest"), F("nScavengeInitialID", "7")),
                Row("hextypes", F("nID", "11"), F("strName", "Lake"), F("nTreasureID", "7")),
            }),
            ("treasuretable", new[] { Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")) }));

        var groups = builder.BuildReferenceGroups(Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")));

        Assert.Equal(2, groups.Count);
        var creatures = Assert.Single(groups, g => g.TableName == "creatures");
        Assert.Equal(1, creatures.Count);
        Assert.Contains("[Dogman](db://creatures/3)", creatures.Markdown);
        var hextypes = Assert.Single(groups, g => g.TableName == "hextypes");
        Assert.Equal(2, hextypes.Count);
        Assert.Contains("`nScavengeInitialID`", hextypes.Markdown);
        Assert.Contains("`nTreasureID`", hextypes.Markdown);
    }

    // ── incoming references (v2.24) ──

    [Fact]
    public void IncomingReferencesSectionListsSources()
    {
        var builder = Builder(
            ("creatures", new[] { Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "7")) }),
            ("treasuretable", new[] { Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")) }));

        var md = builder.Build(Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")));

        Assert.Contains("## 被引用（1）", md);
        Assert.Contains("### creatures", md);
        Assert.Contains("[Dogman](db://creatures/3) — `nTreasureID`", md);
    }

    [Fact]
    public void NoIncomingReferencesShowsNoSection()
    {
        var builder = Builder(("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Knife")) }));

        var md = builder.Build(Row("itemtypes", F("nID", "1"), F("strName", "Knife")));

        Assert.DoesNotContain("被引用", md);
    }
}
