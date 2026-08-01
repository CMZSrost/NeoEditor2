using Xunit;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

public class EntityToPromptConverterTests
{
    private readonly EntityToPromptConverter _converter = new();

    [Fact]
    public void BuildPrompt_IncludesPixelArtKeywords()
    {
        var entity = new ItemType { EntityId = "item_weapon_sword", Name = "Iron Sword" };
        var options = new ImageGenerationOptions(64, 64, "pixel-art");

        var prompt = _converter.BuildPrompt(entity, options);

        Assert.Contains("pixel art", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("64x64", prompt);
        Assert.Contains("transparent background", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("16-32 colors", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPrompt_IncludesSubject_WhenAvailable()
    {
        var entity = new ItemType { EntityId = "item_weapon_sword", Name = "Iron Sword" };
        var options = new ImageGenerationOptions();

        var prompt = _converter.BuildPrompt(entity, options);

        Assert.Contains("Iron Sword", prompt);
    }

    [Fact]
    public void BuildPrompt_Fallback_WhenNoDescriptiveProperties()
    {
        var entity = new ItemType { EntityId = "item_unknown", Name = "" };
        var options = new ImageGenerationOptions();

        var prompt = _converter.BuildPrompt(entity, options);

        // Should still produce a valid prompt
        Assert.Contains("pixel art", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ItemType", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesTargetDimensions()
    {
        var entity = new Creature { EntityId = "creature_wolf", Name = "Wolf" };
        var options = new ImageGenerationOptions(128, 128, "pixel-art");

        var prompt = _converter.BuildPrompt(entity, options);

        Assert.Contains("128x128", prompt);
    }

    [Fact]
    public void BuildPrompt_DifferentEntityTypes_ProduceDifferentPrompts()
    {
        var item = new ItemType { EntityId = "item_key", Name = "Rusty Key" };
        var creature = new Creature { EntityId = "creature_rat", Name = "Giant Rat" };
        var options = new ImageGenerationOptions();

        var itemPrompt = _converter.BuildPrompt(item, options);
        var creaturePrompt = _converter.BuildPrompt(creature, options);

        // Content should differ
        Assert.NotEqual(itemPrompt, creaturePrompt);
        Assert.Contains("Rusty Key", itemPrompt);
        Assert.Contains("Giant Rat", creaturePrompt);
    }
}
