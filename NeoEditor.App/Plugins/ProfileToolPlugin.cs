using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Views.UserControls;

namespace NeoEditor;

/// <summary>
/// Profile Tool (left dock, D02 §5.0) — Mod management (New / Import) plus
/// profile orchestration entry points (Edit Profile / Reload Merge View).
/// App-level plugin because it owns the profile dialogs. Spec: D02 §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class ProfileToolPlugin : IToolPlugin
{
    private readonly ProfileToolViewModel _viewModel;

    public ProfileToolPlugin(ProfileToolViewModel viewModel) => _viewModel = viewModel;

    public string Name => "ProfileTool";
    public Version Version => new(1, 0, 0);
    public string Title => "Profile Tool";
    public ToolDock DefaultDock => ToolDock.Left;
    public int Order => 25;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new ProfileToolView { DataContext = _viewModel };
}
