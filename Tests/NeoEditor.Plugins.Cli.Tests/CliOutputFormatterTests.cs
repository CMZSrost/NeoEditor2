using NeoEditor.Plugins.Cli.Cli;
using Xunit;

namespace NeoEditor.Plugins.Cli.Tests;

public class CliOutputFormatterTests
{
    private readonly CliOutputFormatter _formatter = new();

    [Fact]
    public void Format_Null_Text_ReturnsNoResult()
    {
        var result = _formatter.Format(null, "text");
        Assert.Equal("(no result)", result);
    }

    [Fact]
    public void Format_Null_Json_ReturnsEmptyObject()
    {
        var result = _formatter.Format(null, "json");
        Assert.Equal("{}", result);
    }

    [Fact]
    public void Format_String_Text_ReturnsSameString()
    {
        var result = _formatter.Format("hello world", "text");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Format_Object_Text_ReturnsJsonFormatted()
    {
        var obj = new { name = "test", value = 42 };
        var result = _formatter.Format(obj, "text");

        Assert.Contains("name", result);
        Assert.Contains("test", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void Format_Object_Json_ReturnsIndentedJson()
    {
        var obj = new { name = "test", value = 42 };
        var result = _formatter.Format(obj, "json");

        Assert.Contains("\"name\"", result);
        Assert.Contains("\"test\"", result);
        Assert.Contains("\"value\"", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void Format_UnknownFormat_FallsBackToText()
    {
        var result = _formatter.Format("hello", "xml");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Format_Table_ReturnsSameAsText()
    {
        var result = _formatter.Format("data", "table");
        Assert.Equal("data", result);
    }

    [Fact]
    public void FormatHelp_ContainsAllCommands()
    {
        var help = _formatter.FormatHelp();

        Assert.Contains("NeoEditor CLI", help);
        Assert.Contains("get-entity", help);
        Assert.Contains("edit-entity", help);
        Assert.Contains("add-entity", help);
        Assert.Contains("delete-entity", help);
        Assert.Contains("list-entities", help);
        Assert.Contains("save", help);
        Assert.Contains("diff", help);
        Assert.Contains("query-references", help);
        Assert.Contains("--format", help);
        Assert.Contains("--entity-type", help);
        Assert.Contains("--entity-id", help);
        Assert.Contains("--property", help);
        Assert.Contains("--value", help);
    }
}
