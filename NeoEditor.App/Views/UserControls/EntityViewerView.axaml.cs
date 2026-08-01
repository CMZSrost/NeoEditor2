using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;
// Removed Editors.EditorHelper reference — all types now covered by EntityVisualizerRegistry (M3)
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Helper;

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
        var browserIndexService = ViewServices.BrowserIndex;
        await browserIndexService.EnsureBuiltAsync();

        var visualizers = ViewServices.VisualizerRegistry;
        var visualizer = visualizers.Get(doc.Entity.GetType());
        var content = visualizer?.BuildDetail(doc.Entity)
                      ?? new TextBlock { Text = $"No visualizer for {doc.Entity.GetType().Name}" };
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
