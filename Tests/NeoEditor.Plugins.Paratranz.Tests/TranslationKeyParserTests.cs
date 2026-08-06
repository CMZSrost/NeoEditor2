using NeoEditor.Plugins.Paratranz.Conversion;
using Xunit;

namespace NeoEditor.Plugins.Paratranz.Tests;

public class TranslationKeyParserTests
{
    private readonly ITranslationKeyParser _parser = new TranslationKeyParser();

    [Fact]
    public void BuildKey_与旧脚本格式一致()
    {
        var key = _parser.BuildKey("attackmodes", "id", "1", "strName");

        Assert.Equal("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]", key);
    }

    [Fact]
    public void BuildKey_nID字段_字符串id值()
    {
        var key = _parser.BuildKey("chargeprofiles", "nID", "5", "strName");

        Assert.Equal("//table[@name=\"chargeprofiles\"]/column[@name=\"nID\"][text()=5]/../column[@name=\"strName\"]", key);
    }

    [Theory]
    [InlineData("//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]", "attackmodes", "id", "1", "strName")]
    [InlineData("//table[@name=\"recipes\"]/column[@name=\"nID\"][text()=42]/../column[@name=\"strSecretName\"]", "recipes", "nID", "42", "strSecretName")]
    [InlineData("  //table[@name=\"maps\"]/column[@name=\"id\"][text()=3]/../column[@name=\"strDesc\"]  ", "maps", "id", "3", "strDesc")]
    public void TryParseKey_解析成功(string key, string table, string idField, string id, string column)
    {
        Assert.True(_parser.TryParseKey(key, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(table, parsed!.Table);
        Assert.Equal(idField, parsed.IdField);
        Assert.Equal(id, parsed.Id);
        Assert.Equal(column, parsed.Column);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-xpath")]
    [InlineData("//table[@name=\"attackmodes\"]/column[@name=\"id\"]")]
    [InlineData("//column[@name=\"id\"][text()=1]/../column[@name=\"strName\"]")]
    public void TryParseKey_畸形键返回false(string key)
    {
        Assert.False(_parser.TryParseKey(key, out var parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void BuildAndParse_往返一致()
    {
        var key = _parser.BuildKey("hextypes", "id", "7", "strDesc");

        Assert.True(_parser.TryParseKey(key, out var parsed));
        Assert.Equal("hextypes", parsed!.Table);
        Assert.Equal("7", parsed.Id);
    }
}
