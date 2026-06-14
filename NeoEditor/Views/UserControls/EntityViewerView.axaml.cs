using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class EntityViewerView : UserControl
{
    public EntityViewerView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not EntityViewerDocument doc) return;

        // Wait for reference index to be ready, then build the visualizer
        _ = BuildContentAsync(doc);
    }

    private async System.Threading.Tasks.Task BuildContentAsync(EntityViewerDocument doc)
    {
        await Services.BrowserIndexService.EnsureBuiltAsync();

        var visualizers = App.ServiceProvider!.GetRequiredService<EntityVisualizerRegistry>();
        var visualizer = visualizers.Get(doc.Entity.GetType());
        var content = visualizer?.BuildDetail(doc.Entity)
                      ?? Editors.EditorHelper.BuildOverviewTab(doc.Entity);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Reuse existing ScrollViewer if BuildDetail already returned one (avoid double-nesting)
            if (content is ScrollViewer sv)
            {
                sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                Content = sv;
            }
            else
            {
                Content = new ScrollViewer
                {
                    Content = content,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
            }
        });
    }
}
