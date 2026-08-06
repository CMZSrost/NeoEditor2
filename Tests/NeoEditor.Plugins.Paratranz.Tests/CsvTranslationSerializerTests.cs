using System.Linq;
using NeoEditor.Plugins.Paratranz.Conversion;
using NeoEditor.Plugins.Paratranz.Models;
using Xunit;

namespace NeoEditor.Plugins.Paratranz.Tests;

public class CsvTranslationSerializerTests
{
    private readonly ICsvTranslationSerializer _serializer = new CsvTranslationSerializer();

    private static readonly TranslationUnit[] SampleUnits =
    [
        new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]", "Punch", "拳击"),
        new TranslationUnit("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=2]/../column[@name=\"strName\"]", "Kick", null),
    ];

    [Fact]
    public void Serialize_输出三列无头CSV_行终止为LF()
    {
        var csv = _serializer.Serialize(SampleUnits);

        var lines = csv.Split('\n');
        Assert.Equal(3, lines.Length); // 2 行 + 尾部空串
        // key 含引号 → RFC4180 引号包裹 + 内部引号加倍
        Assert.StartsWith("\"//table[@name=\"\"attackmodes\"\"]", lines[0]);
        Assert.EndsWith("Punch,拳击", lines[0]);
        Assert.EndsWith("Kick,", lines[1]); // 无译文写空串
        Assert.DoesNotContain("\r", csv);
    }

    [Fact]
    public void Serialize_含逗号引号换行的文本_按RFC4180转义()
    {
        var unit = new TranslationUnit("k", "He said \"hi\", then left.\nBye", "他说「嗨」，然后离开。\n再见");

        var csv = _serializer.Serialize([unit]);
        var parsed = _serializer.Deserialize(csv);

        var round = Assert.Single(parsed);
        Assert.Equal(unit.Key, round.Key);
        Assert.Equal(unit.Original, round.Original);
        Assert.Equal(unit.Translation, round.Translation);
    }

    [Fact]
    public void Deserialize_兼容两列格式_译文在第2列()
    {
        // 模拟旧工具（Python csv）生成的合法 RFC4180：key 含引号 → 引号包裹 + 内部加倍
        var csv = "\"\"\"//table[@name=\"\"headlines\"\"]/column[@name=\"\"id\"\"][text()=1]/../column[@name=\"\"strHeadline\"\"]\"\"\",战争爆发";

        var units = _serializer.Deserialize(csv);

        var unit = Assert.Single(units);
        Assert.Equal("战争爆发", unit.Translation);
        Assert.Equal("", unit.Original);
        Assert.Equal("//table[@name=\"headlines\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strHeadline\"]", unit.Key);
    }

    [Fact]
    public void Deserialize_清洗key_首尾引号与多余前缀()
    {
        // 字段内容 = "//table[@name="factions"]/column[@name="id"][text()=2]/../column[@name="strName"]"
        // （首尾引号 + 内部引号按 RFC4180 加倍）
        var csv = "\"\"\"//table[@name=\"\"factions\"\"]/column[@name=\"\"id\"\"][text()=2]/../column[@name=\"\"strName\"\"]\"\"\",,阵营名";

        var units = _serializer.Deserialize(csv);

        var unit = Assert.Single(units);
        Assert.Equal("//table[@name=\"factions\"]/column[@name=\"id\"][text()=2]/../column[@name=\"strName\"]", unit.Key);
        Assert.Equal("阵营名", unit.Translation);
    }

    [Fact]
    public void Deserialize_字面反斜杠n_还原为换行()
    {
        var csv = "k,原文,第一行\\n第二行";

        var units = _serializer.Deserialize(csv);

        var unit = Assert.Single(units);
        Assert.Equal("第一行\n第二行", unit.Translation);
    }

    [Fact]
    public void Deserialize_坏行与空行跳过()
    {
        var csv = "\n单列行\n\"\"\"//table[@name=\"\"a\"\"]/column[@name=\"\"id\"\"][text()=1]/../column[@name=\"\"b\"\"]\"\"\",原文,译文\n";

        var units = _serializer.Deserialize(csv);

        var unit = Assert.Single(units);
        Assert.Equal("译文", unit.Translation);
    }

    [Fact]
    public void Deserialize_字段内未转义引号_宽容解析()
    {
        // 宽松格式（非 RFC4180）：key 未加引号包裹且内含引号 —— BadDataFound=null
        // 下按字面宽容解析（兼容第三方宽松导出）
        var csv = "//table[@name=\"a\"]/column[@name=\"id\"][text()=1]/../column[@name=\"b\"],原文,译文";

        var units = _serializer.Deserialize(csv);

        var unit = Assert.Single(units);
        Assert.Equal("//table[@name=\"a\"]/column[@name=\"id\"][text()=1]/../column[@name=\"b\"]", unit.Key);
        Assert.Equal("原文", unit.Original);
        Assert.Equal("译文", unit.Translation);
    }

    [Fact]
    public void Deserialize_空输入_返回空列表()
    {
        Assert.Empty(_serializer.Deserialize(""));
        Assert.Empty(_serializer.Deserialize(null!));
    }

    [Fact]
    public void SerializeDeserialize_往返一致()
    {
        var csv = _serializer.Serialize(SampleUnits);

        var units = _serializer.Deserialize(csv);

        Assert.Equal(2, units.Count);
        Assert.Equal(SampleUnits[0].Key, units[0].Key);
        Assert.Equal(SampleUnits[0].Original, units[0].Original);
        Assert.Equal(SampleUnits[0].Translation, units[0].Translation);
        Assert.Equal(SampleUnits[1].Translation ?? "", units[1].Translation ?? "");
    }
}
