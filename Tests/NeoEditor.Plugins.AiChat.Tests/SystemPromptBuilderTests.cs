using NeoEditor.Plugins.AiChat.Services;
using Xunit;

namespace NeoEditor.Plugins.AiChat.Tests;

public class SystemPromptBuilderTests
{
    private readonly SystemPromptBuilder _builder = new();

    [Fact]
    public void BuildDefaultPrompt_ContainsIdentity()
    {
        var prompt = _builder.BuildDefaultPrompt();

        Assert.Contains("NeoEditor Assistant", prompt);
        Assert.Contains("NeoScavenger", prompt);
    }

    [Fact]
    public void BuildDefaultPrompt_ContainsGuidelines()
    {
        var prompt = _builder.BuildDefaultPrompt();

        Assert.Contains("Guidelines", prompt);
        Assert.Contains("ListEntities BEFORE GetEntity", prompt);
        Assert.Contains("destructive actions", prompt);
    }

    [Fact]
    public void BuildDefaultPrompt_ContainsToolsSection()
    {
        var prompt = _builder.BuildDefaultPrompt();

        Assert.Contains("Available Tools", prompt);
        Assert.Contains("GetEntity", prompt);
        Assert.Contains("ListEntities", prompt);
        Assert.Contains("EditEntity", prompt);
        Assert.Contains("Save", prompt);
    }

    [Fact]
    public void BuildDefaultPrompt_ContainsEntityTypes()
    {
        var prompt = _builder.BuildDefaultPrompt();

        Assert.Contains("Game Entity Types", prompt);
        // Should contain at least some known entity types
        Assert.Contains("ItemType", prompt);
        Assert.Contains("Creature", prompt);
    }

    [Fact]
    public void BuildDefaultPrompt_IsNotEmpty()
    {
        var prompt = _builder.BuildDefaultPrompt();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.True(prompt.Length > 500); // Should be a substantial prompt
    }

    [Fact]
    public void BuildEntitySchemaSection_ContainsEntityTypes()
    {
        var schema = _builder.BuildEntitySchemaSection();

        Assert.Contains("Game Entity Types", schema);
        Assert.Contains("ItemType", schema);
        Assert.Contains("Creature", schema);
        Assert.Contains("Recipe", schema);
    }

    [Fact]
    public void BuildEntitySchemaSection_ContainsReferenceFieldHints()
    {
        var schema = _builder.BuildEntitySchemaSection();

        Assert.Contains("Reference fields contain", schema);
    }

    [Fact]
    public void BuildDefaultPrompt_And_BuildEntitySchemaSection_AreNotIdentical()
    {
        var full = _builder.BuildDefaultPrompt();
        var schema = _builder.BuildEntitySchemaSection();

        Assert.True(full.Length > schema.Length);
    }
}
