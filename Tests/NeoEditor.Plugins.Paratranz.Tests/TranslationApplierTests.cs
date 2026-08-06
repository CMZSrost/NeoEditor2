using System.Linq;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.Paratranz.Conversion;
using NeoEditor.Plugins.Paratranz.Models;
using Xunit;

namespace NeoEditor.Plugins.Paratranz.Tests;

public class TranslationApplierTests
{
    private readonly ITranslationApplier _applier =
        new TranslationApplier(new TranslationKeyParser());

    private static AttackMode[] SampleEntities() =>
    [
        new AttackMode { Id = 1, Name = "Punch", Notes = "A punch attack" },
        new AttackMode { Id = 2, Name = "Kick" },
    ];

    [Fact]
    public void BuildCommands_匹配实体与列_执行后值变化_可Undo()
    {
        var entities = SampleEntities();
        var units = new[]
        {
            new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]", "Punch", "拳击"),
            new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=2]/../column[@name=\"strName\"]", "Kick", "踢击"),
        };

        var result = _applier.BuildCommands(units, entities);

        Assert.Equal(2, result.Stats.Total);
        Assert.Equal(2, result.Stats.Applied);
        var command = Assert.Single(result.Commands);
        command.Execute();
        Assert.Equal("拳击", entities[0].Name);
        Assert.Equal("踢击", entities[1].Name);

        command.Undo();
        Assert.Equal("Punch", entities[0].Name);
        Assert.Equal("Kick", entities[1].Name);
    }

    [Fact]
    public void BuildCommands_多列编辑_单命令批量执行()
    {
        var entity = new AttackMode { Id = 1, Name = "Punch", Notes = "Old note" };
        var units = new[]
        {
            new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]", "Punch", "拳击"),
            new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strNotes\"]", "Old note", "旧注释"),
        };

        var result = _applier.BuildCommands(units, [entity]);

        var command = Assert.Single(result.Commands);
        command.Execute();
        Assert.Equal("拳击", entity.Name);
        Assert.Equal("旧注释", entity.Notes);
    }

    [Fact]
    public void BuildCommands_无译文或无法定位_计入Skipped_不生成命令()
    {
        var entities = SampleEntities();
        var units = new[]
        {
            new TranslationUnit("k1", "原文", null),                                     // 无译文
            new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=99]/../column[@name=\"strName\"]", "x", "y"), // id 不存在
            new TranslationUnit("//table[@name=\"nonexistent\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]", "x", "y"), // 表不存在
            new TranslationUnit("not-a-key", "x", "y"),                                  // 坏 key
        };

        var result = _applier.BuildCommands(units, entities);

        Assert.Equal(4, result.Stats.Skipped);
        Assert.Empty(result.Commands);
    }

    [Fact]
    public void BuildCommands_译文与现值相同_计入Unchanged()
    {
        var entities = SampleEntities();
        var units = new[]
        {
            new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]", "Punch", "Punch"),
        };

        var result = _applier.BuildCommands(units, entities);

        Assert.Equal(1, result.Stats.Unchanged);
        Assert.Empty(result.Commands);
    }

    [Fact]
    public void BuildCommands_nID主键实体_匹配生效()
    {
        var chargeProfile = new ChargeProfile { Id = 5, Name = "Battery" };
        var units = new[]
        {
            new TranslationUnit("//table[@name=\"chargeprofiles\"]/column[@name=\"nID\"][text()=5]/../column[@name=\"strName\"]", "Battery", "电池"),
        };

        var result = _applier.BuildCommands(units, [chargeProfile]);

        Assert.Equal(1, result.Stats.Applied);
        result.Commands.Single().Execute();
        Assert.Equal("电池", chargeProfile.Name);
    }

    [Fact]
    public void BuildCommands_非string列_计入Skipped()
    {
        var entity = new AttackMode { Id = 1, Name = "Punch" };
        var units = new[]
        {
            new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"nRange\"]", "1", "2"),
        };

        var result = _applier.BuildCommands(units, [entity]);

        Assert.Equal(1, result.Stats.Skipped);
        Assert.Empty(result.Commands);
    }
}
