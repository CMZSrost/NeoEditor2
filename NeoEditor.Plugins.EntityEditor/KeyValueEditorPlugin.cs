using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.EntityEditor.ViewModels;

namespace NeoEditor.Plugins.EntityEditor;

/// <summary>
/// Key-Value field editor tool (left dock). Hosts the shared
/// <see cref="KeyValueEditorViewModel"/> (DI singleton) inside a
/// <see cref="Views.KeyValueEditorView"/>. Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class KeyValueEditorPlugin : IToolPlugin
{
    private readonly KeyValueEditorViewModel _viewModel;

    public KeyValueEditorPlugin(KeyValueEditorViewModel viewModel) => _viewModel = viewModel;

    public string Name => "EntityEditor.KeyValueEditor";
    public Version Version => new(1, 0, 0);
    public string Title => "Editor";
    public ToolDock DefaultDock => ToolDock.Left;
    public int Order => 10;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.KeyValueEditorView { DataContext = _viewModel };
}
