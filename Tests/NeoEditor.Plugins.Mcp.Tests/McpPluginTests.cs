using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using Xunit;

namespace NeoEditor.Plugins.Mcp.Tests;

public class McpPluginTests
{
    [Fact]
    public void Plugin_HasCorrectMetadata()
    {
        var plugin = new McpPlugin();

        Assert.Equal("Mcp", plugin.Name);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);
    }

    [Fact]
    public void Plugin_IsDecoratedWith_Service_PluginKind()
    {
        var attr = typeof(McpPlugin).GetCustomAttributes(typeof(PluginKindAttribute), false);

        Assert.Single(attr);
        var kind = (PluginKindAttribute)attr[0];
        Assert.Equal(PluginKind.Service, kind.Kind);
    }

    [Fact]
    public void Plugin_Implements_IServicePlugin()
    {
        var plugin = new McpPlugin();
        Assert.IsAssignableFrom<IServicePlugin>(plugin);
        Assert.IsAssignableFrom<IPlugin>(plugin);
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        var plugin = new McpPlugin();
        await plugin.InitializeAsync(null!);
        // Should not throw — McpPlugin does not auto-start server
    }
}
