using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.DataViewer.ViewModels;

namespace NeoEditor.Plugins.DataViewer;

/// <summary>
/// DataTable tool — the bottom merge/single-mod data grid.
/// The tool's initial Context is a <see cref="DataTablePlaceholder"/>; the App shell
/// replaces it with the shared <see cref="ModDataToolViewModel"/> once a profile loads
/// (see DocumentWorkspaceViewModel). Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class DataTablePlugin : IToolPlugin
{
    public string Name => "DataViewer.DataTable";
    public Version Version => new(1, 0, 0);
    public string Title => "Data Table";
    public ToolDock DefaultDock => ToolDock.Bottom;
    public int Order => 10;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new DataTablePlaceholder();
}
