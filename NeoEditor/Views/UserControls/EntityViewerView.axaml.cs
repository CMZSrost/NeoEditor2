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
        Console.WriteLine($"[EV] OnDataContextChanged: type={DataContext?.GetType().Name ?? "null"}");
        if (DataContext is not EntityViewerDocument doc) return;

        var visualizers = App.ServiceProvider!.GetRequiredService<EntityVisualizerRegistry>();
        var visualizer = visualizers.Get(doc.Entity.GetType());
        var content = visualizer?.BuildDetail(doc.Entity)
                      ?? Editors.EditorHelper.BuildOverviewTab(doc.Entity);
        Console.WriteLine($"[EV] Built content: {content?.GetType().Name ?? "null"}");
        Content = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Console.WriteLine($"[EV] Content set, Bounds={Bounds}");
    }
}
