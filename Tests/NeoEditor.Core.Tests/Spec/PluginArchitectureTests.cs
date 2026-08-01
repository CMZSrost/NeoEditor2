using System.Reflection;
using NeoEditor.Core.Abstractions;
using Xunit;

namespace NeoEditor.Core.Tests.Spec;

/// <summary>
/// Architecture compliance tests for spec rules R23 (Plugin Classification)
/// and R25 (Cross-Plugin Extension Points).
///
/// These tests verify:
/// - Every IPlugin class has exactly one [PluginKind] attribute (R23)
/// - Workbench plugins implement IToolPlugin and/or IDocumentPlugin (R23)
/// - Service plugins implement IServicePlugin (R23)
/// - IHostService declares extension point registration methods (R25)
/// - PluginKindAttribute is strictly single-use (R23)
/// </summary>
public class PluginArchitectureTests
{
    private static Assembly[] PluginAssemblies =>
    [
        typeof(NeoEditor.Plugins.DataViewer.DataTablePlugin).Assembly,
        typeof(NeoEditor.Plugins.EntityEditor.EntityEditorPlugin).Assembly,
        typeof(NeoEditor.Plugins.ImageTools.ImageAssetManagerPlugin).Assembly,
        typeof(NeoEditor.Plugins.Mcp.McpPlugin).Assembly,
        typeof(NeoEditor.Plugins.Cli.CliPlugin).Assembly,
        typeof(NeoEditor.Plugins.AiChat.AiChatPlugin).Assembly,
    ];

    private static IEnumerable<Type> GetAllPluginTypes()
    {
        return PluginAssemblies
            .SelectMany(asm => asm.GetExportedTypes())
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
    }

    /// <summary>
    /// R23: Every IPlugin implementation must have exactly one [PluginKind] attribute.
    /// Missing or multiple attributes is a spec violation.
    /// </summary>
    [Fact]
    public void R23_EveryPlugin_HasExactlyOnePluginKind()
    {
        var pluginTypes = GetAllPluginTypes().ToList();
        Assert.NotEmpty(pluginTypes); // ensure we're actually testing something

        foreach (var type in pluginTypes)
        {
            var attrs = type.GetCustomAttributes<PluginKindAttribute>().ToList();
            Assert.True(attrs.Count == 1,
                $"Plugin {type.FullName} must have exactly one [PluginKind] attribute. Found: {attrs.Count}");
        }
    }

    /// <summary>
    /// R23: Workbench plugins must implement IToolPlugin and/or IDocumentPlugin.
    /// A Workbench plugin that implements neither is incorrectly classified.
    /// </summary>
    [Fact]
    public void R23_WorkbenchPlugins_MustImplement_ToolOrDocumentInterface()
    {
        var workbenchPlugins = GetAllPluginTypes()
            .Where(t => t.GetCustomAttribute<PluginKindAttribute>()?.Kind == PluginKind.Workbench);

        foreach (var type in workbenchPlugins)
        {
            var implementsTool = typeof(IToolPlugin).IsAssignableFrom(type);
            var implementsDoc = typeof(IDocumentPlugin).IsAssignableFrom(type);
            Assert.True(implementsTool || implementsDoc,
                $"Workbench plugin {type.FullName} must implement IToolPlugin and/or IDocumentPlugin.");
        }
    }

    /// <summary>
    /// R23: Service plugins must implement IServicePlugin.
    /// (No Service plugins exist in this phase, but the test enforces the contract
    /// so future additions are caught.)
    /// </summary>
    [Fact]
    public void R23_ServicePlugins_MustImplement_IServicePlugin()
    {
        foreach (var asm in PluginAssemblies)
        {
            var servicePlugins = asm.GetExportedTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t)
                            && t is { IsAbstract: false, IsInterface: false })
                .Where(t => t.GetCustomAttribute<PluginKindAttribute>()?.Kind == PluginKind.Service);

            foreach (var type in servicePlugins)
            {
                Assert.True(typeof(IServicePlugin).IsAssignableFrom(type),
                    $"Service plugin {type.FullName} must implement IServicePlugin.");
            }
        }
    }

    /// <summary>
    /// R25: IHostService must declare the three extension point registration methods:
    /// RegisterPreSaveHook, RegisterPostLoadHook, RegisterPreExecuteHook.
    /// </summary>
    [Fact]
    public void R25_IHostService_Declares_ExtensionPoint_RegistrationMethods()
    {
        var iface = typeof(IHostService);

        var preSave = iface.GetMethod(nameof(IHostService.RegisterPreSaveHook));
        Assert.NotNull(preSave);
        Assert.Single(preSave!.GetParameters());

        var postLoad = iface.GetMethod(nameof(IHostService.RegisterPostLoadHook));
        Assert.NotNull(postLoad);
        Assert.Single(postLoad!.GetParameters());

        var preExecute = iface.GetMethod(nameof(IHostService.RegisterPreExecuteHook));
        Assert.NotNull(preExecute);
        Assert.Single(preExecute!.GetParameters());
    }

    /// <summary>
    /// R23: PluginKindAttribute must be single-use (AllowMultiple = false)
    /// and non-inherited (Inherited = false) so each plugin class declares
    /// its own classification explicitly.
    /// </summary>
    [Fact]
    public void R23_PluginKindAttribute_IsSingleUse_And_NotInherited()
    {
        var attrType = typeof(PluginKindAttribute);
        var usage = attrType.GetCustomAttribute<System.AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.False(usage!.AllowMultiple,
            "[PluginKind] must disallow multiple applications on the same class.");
        Assert.False(usage.Inherited,
            "[PluginKind] must not be inherited — each plugin class declares its own kind.");
    }

    /// <summary>
    /// Verify that PluginKindAttribute has a Kind property of type PluginKind.
    /// </summary>
    [Fact]
    public void R23_PluginKindAttribute_Has_KindProperty()
    {
        var kindProp = typeof(PluginKindAttribute).GetProperty("Kind");
        Assert.NotNull(kindProp);
        Assert.Equal(typeof(PluginKind), kindProp!.PropertyType);
    }
}
