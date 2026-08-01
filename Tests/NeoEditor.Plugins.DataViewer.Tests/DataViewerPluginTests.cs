using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.DataViewer;
using NeoEditor.Plugins.DataViewer.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.DataViewer.Tests;

/// <summary>
/// D02 plugin-split contract tests: each DataViewer Tool is its own IToolPlugin
/// (1:1), declares its dock position / order, and produces its tool view.
/// </summary>
public class DataViewerPluginTests
{
    [Fact]
    public void DataTablePlugin_HasCorrectMetadata()
    {
        var plugin = new DataTablePlugin();

        Assert.Equal("DataViewer.DataTable", plugin.Name);
        Assert.Equal("Data Table", plugin.Title);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);
        Assert.Equal(ToolDock.Bottom, plugin.DefaultDock);
        Assert.Equal(10, plugin.Order);
        Assert.IsAssignableFrom<IToolPlugin>(plugin);
    }

    [Fact]
    public void DataTablePlugin_InitialContext_IsPlaceholder()
    {
        var plugin = new DataTablePlugin();

        Assert.IsType<DataTablePlaceholder>(plugin.CreateToolView());
    }

    [Fact]
    public void ForwardIndexPlugin_HasCorrectMetadata()
    {
        var plugin = new ForwardIndexPlugin(null!);

        Assert.Equal("DataViewer.ForwardIndex", plugin.Name);
        Assert.Equal("Ref Index", plugin.Title);
        Assert.Equal(ToolDock.Bottom, plugin.DefaultDock);
        Assert.Equal(11, plugin.Order);
    }

    [Fact]
    public void ReverseIndexPlugin_HasCorrectMetadata()
    {
        var plugin = new ReverseIndexPlugin(null!);

        Assert.Equal("DataViewer.ReverseIndex", plugin.Name);
        Assert.Equal("Reverse Index", plugin.Title);
        Assert.Equal(ToolDock.Bottom, plugin.DefaultDock);
        Assert.Equal(12, plugin.Order);
    }

    [Fact]
    public void SearchPlugin_HasCorrectMetadata()
    {
        var plugin = new SearchPlugin(null!);

        Assert.Equal("DataViewer.Search", plugin.Name);
        Assert.Equal("Search", plugin.Title);
        Assert.Equal(ToolDock.Bottom, plugin.DefaultDock);
        Assert.Equal(13, plugin.Order);
    }

    [Fact]
    public void PeekPlugin_HasCorrectMetadata()
    {
        var plugin = new PeekPlugin(null!);

        Assert.Equal("DataViewer.Peek", plugin.Name);
        Assert.Equal("Peek", plugin.Title);
        Assert.Equal(ToolDock.Right, plugin.DefaultDock);
        Assert.Equal(10, plugin.Order);
    }

    [Fact]
    public async Task DataTablePlugin_Initialize_Completes()
    {
        await new DataTablePlugin().InitializeAsync(null!);
    }
}
