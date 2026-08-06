using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Player.Core.Data;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>
/// Incoming-reference analysis tests (Docs/42 v2.24): who references a row — across
/// tables, via treasuretable's dual targets and itemtypes composite keys, with
/// dedup and image-column exclusion.
/// </summary>
public class ReferenceAnalyzerTests
{
    private static GameDataField F(string column, string value) => new(column, value);

    private static GameDataRow Row(string table, params GameDataField[] fields) => new(table, fields);

    private static ReferenceAnalyzer Analyzer(params (string Table, IReadOnlyList<GameDataRow> Rows)[] tables)
        => new(new GameDataCatalog(
            tables.ToDictionary(t => t.Table, t => t.Rows, StringComparer.OrdinalIgnoreCase)));

    [Fact]
    public void FindsIncomingReferencesAcrossTables()
    {
        var analyzer = Analyzer(
            ("creatures", new[] { Row("creatures", F("nID", "3"), F("strName", "Dogman"), F("nTreasureID", "7")) }),
            ("hextypes", new[] { Row("hextypes", F("nID", "9"), F("strName", "Forest"), F("nScavengeInitialID", "7")) }),
            ("treasuretable", new[] { Row("treasuretable", F("id", "7"), F("strName", "Junk Pile")) }));

        var target = Row("treasuretable", F("id", "7"), F("strName", "Junk Pile"));
        var hits = analyzer.FindIncoming(target);

        Assert.Equal(2, hits.Count);
        Assert.Contains(hits, h => h.SourceRow.TableName == "creatures" && h.Column == "nTreasureID");
        Assert.Contains(hits, h => h.SourceRow.TableName == "hextypes" && h.Column == "nScavengeInitialID");
    }

    [Fact]
    public void SameRowKeyInAnotherTableDoesNotCrossMatch()
    {
        var analyzer = Analyzer(
            ("recipes", new[]
            {
                Row("recipes", F("nID", "3"), F("strName", "Repair"), F("vAlsoTry", "9")),
                Row("recipes", F("nID", "9"), F("strName", "Alternate")),
            }),
            ("creatures", new[] { Row("creatures", F("nID", "9"), F("strName", "Dogman")) }));

        // creatures nID=9 has the same RowKey as recipes nID=9 — the vAlsoTry reference
        // targets recipes only and must NOT report the creature row.
        var target = Row("creatures", F("nID", "9"), F("strName", "Dogman"));
        var hits = analyzer.FindIncoming(target);

        Assert.Empty(hits);
    }

    [Fact]
    public void TreasureLootResolvesSecondaryTarget()
    {
        // aTreasures plain ids resolve to nested TreasureTable (secondary target) —
        // that row must appear as a source when the loot id matches the target.
        var analyzer = Analyzer(
            ("treasuretable", new[]
            {
                Row("treasuretable", F("id", "5"), F("strName", "Outer"), F("aTreasures", "7x1")),
                Row("treasuretable", F("id", "7"), F("strName", "Inner Loot")),
            }));

        var target = Row("treasuretable", F("id", "7"), F("strName", "Inner Loot"));
        var hits = analyzer.FindIncoming(target);

        var hit = Assert.Single(hits);
        Assert.Equal("5", hit.SourceRow.RowKey);
        Assert.Equal("aTreasures", hit.Column);
    }

    [Fact]
    public void CompositeItemTypeKeyResolvesIncomingReference()
    {
        var analyzer = Analyzer(
            ("treasuretable", new[] { Row("treasuretable", F("id", "5"), F("strName", "Outer"), F("aTreasures", "G.Sx10")) }),
            ("itemtypes", new[]
            {
                Row("itemtypes", F("nID", "1"), F("strGroupID", "G"), F("strSubgroupID", "S"), F("strName", "Scrap Metal")),
            }));

        var target = Row("itemtypes", F("nID", "1"), F("strGroupID", "G"), F("strSubgroupID", "S"), F("strName", "Scrap Metal"));
        var hits = analyzer.FindIncoming(target);

        var hit = Assert.Single(hits);
        Assert.Equal("treasuretable", hit.SourceRow.TableName);
        Assert.Equal("aTreasures", hit.Column);
    }

    [Fact]
    public void DeduplicatesRepeatedIdsFromSameColumn()
    {
        var analyzer = Analyzer(
            ("recipes", new[]
            {
                Row("recipes", F("nID", "1"), F("strName", "Repair"), F("vAlsoTry", "9,9,9")),
                Row("recipes", F("nID", "9"), F("strName", "Alternate")),
            }));

        var target = Row("recipes", F("nID", "9"), F("strName", "Alternate"));
        var hits = analyzer.FindIncoming(target);

        Assert.Single(hits);
    }

    [Fact]
    public void ImageColumnsDoNotProduceHits()
    {
        var analyzer = Analyzer(
            ("creatures", new[] { Row("creatures", F("nID", "1"), F("strName", "Dogman"), F("strImg", "CreDogman.png")) }),
            ("itemtypes", new[] { Row("itemtypes", F("nID", "1"), F("strName", "Knife"), F("vImageList", "ItmKnife.png")) }));

        // Whatever the target, image references have no real target table → no hits.
        var target = Row("creatures", F("nID", "1"), F("strName", "Dogman"));
        Assert.Empty(analyzer.FindIncoming(target));
        var knife = Row("itemtypes", F("nID", "1"), F("strName", "Knife"));
        Assert.Empty(analyzer.FindIncoming(knife));
    }
}
