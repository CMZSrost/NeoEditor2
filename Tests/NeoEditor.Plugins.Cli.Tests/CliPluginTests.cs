using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using Xunit;

namespace NeoEditor.Plugins.Cli.Tests;

public class CliPluginTests
{
    [Fact]
    public void Plugin_HasCorrectMetadata()
    {
        var plugin = new CliPlugin();

        Assert.Equal("Cli", plugin.Name);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);
    }

    [Fact]
    public void Plugin_IsDecoratedWith_Service_PluginKind()
    {
        var attr = typeof(CliPlugin).GetCustomAttributes(typeof(PluginKindAttribute), false);

        Assert.Single(attr);
        var kind = (PluginKindAttribute)attr[0];
        Assert.Equal(PluginKind.Service, kind.Kind);
    }

    [Fact]
    public void Plugin_Implements_IServicePlugin()
    {
        var plugin = new CliPlugin();
        Assert.IsAssignableFrom<IServicePlugin>(plugin);
        Assert.IsAssignableFrom<IPlugin>(plugin);
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        var plugin = new CliPlugin();
        await plugin.InitializeAsync(null!);
    }
}
