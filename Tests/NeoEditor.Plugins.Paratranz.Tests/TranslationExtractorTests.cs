using System.Collections.Generic;
using System.Linq;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.Paratranz.Conversion;
using Xunit;

namespace NeoEditor.Plugins.Paratranz.Tests;

public class TranslationExtractorTests
{
    private readonly ITranslationExtractor _extractor =
        new TranslationExtractor(new TranslationKeyParser());

    [Fact]
    public void Extract_提取可翻译列_生成xpath键()
    {
        var attackMode = new AttackMode
        {
            Id = 1,
            Name = "Punch",
            Notes = "A punch attack",
            WieldPhrase = "", // 空值跳过
            AttackPhrases = "You punch.",
        };

        var units = _extractor.Extract(new IEntity[] { attackMode });

        Assert.Equal(3, units.Count);
        var name = units.Single(u => u.Key.EndsWith("column[@name=\"strName\"]"));
        Assert.Equal("Punch", name.Original);
        Assert.Equal("attackmodes.strName", name.Context);
        var notes = units.Single(u => u.Key.EndsWith("column[@name=\"strNotes\"]"));
        Assert.Equal("A punch attack", notes.Original);
        Assert.Contains("[text()=1]", notes.Key);
        var phrases = units.Single(u => u.Key.EndsWith("column[@name=\"vAttackPhrases\"]"));
        Assert.Equal("You punch.", phrases.Original);
    }

    [Fact]
    public void Extract_nID主键实体_键含nID列()
    {
        var chargeProfile = new ChargeProfile { Id = 5, Name = "Battery" };

        var units = _extractor.Extract(new IEntity[] { chargeProfile });

        var unit = Assert.Single(units);
        Assert.Contains("column[@name=\"nID\"][text()=5]", unit.Key);
        Assert.Contains("column[@name=\"strName\"]", unit.Key);
    }

    [Fact]
    public void Extract_maps_无可翻译列_返回空()
    {
        // strName 是图片名（跳过），strDef 不在白名单（对齐旧脚本 translation_name）
        var map = new Map { Id = 3, Name = "forest.png", Definition = "A forest" };

        var units = _extractor.Extract(new IEntity[] { map });

        Assert.Empty(units);
    }

    [Fact]
    public void Extract_gamevars_整体跳过()
    {
        var gameVar = new GameVar { Name = "Weather", Type = "string", Value = "Sunny" };

        var units = _extractor.Extract(new IEntity[] { gameVar });

        Assert.Empty(units);
    }

    [Fact]
    public void Extract_非翻译列与非string值_不提取()
    {
        var itemType = new ItemType { Id = 10, Name = "Water Bottle", Weight = 1.5 };

        var units = _extractor.Extract(new IEntity[] { itemType });

        var unit = Assert.Single(units); // 仅 strName
        Assert.Contains("column[@name=\"strName\"]", unit.Key);
    }

    [Fact]
    public void Extract_多种实体混合_顺序稳定()
    {
        var units = _extractor.Extract(new IEntity[]
        {
            new Headline { Id = 1, HeadlineText = "War!" },
            new Faction { Id = 2, Name = "Enclave" },
        });

        Assert.Equal(2, units.Count);
        Assert.Equal("headlines.strHeadline", units[0].Context);
        Assert.Equal("factions.strName", units[1].Context);
    }
}
