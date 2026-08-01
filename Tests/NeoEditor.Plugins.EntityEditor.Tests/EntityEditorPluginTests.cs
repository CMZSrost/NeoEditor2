using System;
using System.Collections.Generic;
using NeoEditor.Core.Abstractions;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// D02 plugin-split contract tests. EntityEditor is now a Document plugin only;
/// the KV / OverlayChain Tools are separate IToolPlugin classes.
/// </summary>
public class EntityEditorPluginTests
{
    [Fact]
    public void Plugin_HasCorrectMetadata()
    {
        var plugin = new EntityEditorPlugin();

        Assert.Equal("EntityEditor", plugin.Name);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);
    }

    [Fact]
    public void Plugin_IsDocumentPlugin_NotToolPlugin()
    {
        var plugin = new EntityEditorPlugin();

        Assert.IsAssignableFrom<IDocumentPlugin>(plugin);
        Assert.IsNotAssignableFrom<IToolPlugin>(plugin);
    }

    [Fact]
    public void Plugin_SupportsExpectedEntityTypes()
    {
        var plugin = new EntityEditorPlugin();

        Assert.Contains("ItemType", plugin.SupportedEntityTypes);
        Assert.Contains("Recipe", plugin.SupportedEntityTypes);
        Assert.Contains("Encounter", plugin.SupportedEntityTypes);
        Assert.Contains("Creature", plugin.SupportedEntityTypes);
        Assert.Contains("Condition", plugin.SupportedEntityTypes);
    }

    [Fact]
    public void KeyValueEditorPlugin_HasCorrectMetadata()
    {
        var plugin = new KeyValueEditorPlugin(null!);

        Assert.Equal("EntityEditor.KeyValueEditor", plugin.Name);
        Assert.Equal("Editor", plugin.Title);
        Assert.Equal(ToolDock.Left, plugin.DefaultDock);
        Assert.Equal(10, plugin.Order);
        Assert.IsAssignableFrom<IToolPlugin>(plugin);
    }

    [Fact]
    public void OverlayChainPlugin_HasCorrectMetadata()
    {
        var plugin = new OverlayChainPlugin(null!);

        Assert.Equal("EntityEditor.OverlayChain", plugin.Name);
        Assert.Equal("Overlay Chain", plugin.Title);
        Assert.Equal(ToolDock.Left, plugin.DefaultDock);
        Assert.Equal(20, plugin.Order);
        Assert.IsAssignableFrom<IToolPlugin>(plugin);
    }
}
