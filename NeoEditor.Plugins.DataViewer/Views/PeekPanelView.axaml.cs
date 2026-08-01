using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Plugins.DataViewer.ViewModels;
using NeoEditor.UI.Common.Services;

namespace NeoEditor.Plugins.DataViewer.Views;

public partial class PeekPanelView : UserControl
{
    private PeekPanelViewModel? _lastVm;

    /// <summary>
    /// Injectable VisualizerRegistry. Set by the parent view before the panel is shown.
    /// Falls back to resolving from Application.Current DI container if not explicitly set.
    /// </summary>
    public EntityVisualizerRegistry? VisualizerRegistry { get; set; }

    private EntityVisualizerRegistry ResolveRegistry() =>
        VisualizerRegistry
        ?? (Application.Current?.Resources["Services"] as IServiceProvider)
            ?.GetService(typeof(EntityVisualizerRegistry)) as EntityVisualizerRegistry
        ?? throw new InvalidOperationException("EntityVisualizerRegistry not available");

    public PeekPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            if (_lastVm != null && _lastVm.CurrentEntity != null)
                RebuildContent(_lastVm);
        };
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_lastVm != null)
            _lastVm.PropertyChanged -= OnVmPropertyChanged;

        _lastVm = DataContext as PeekPanelViewModel;
        if (_lastVm != null)
        {
            _lastVm.PropertyChanged += OnVmPropertyChanged;
            RebuildContent(_lastVm);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PeekPanelViewModel.CurrentEntity) ||
            e.PropertyName == nameof(PeekPanelViewModel.IsEmpty))
        {
            if (DataContext is PeekPanelViewModel vm)
                RebuildContent(vm);
        }
    }

    public void RebuildContent(PeekPanelViewModel vm)
    {
        if (vm.CurrentEntity == null)
        {
            PeekContentHost.Content = null;
            return;
        }

        // BuildDetail can be heavy (image loads, large control trees).
        // Post at Background priority so the UI thread stays responsive.
        var entity = vm.CurrentEntity;
        var registry = ResolveRegistry();
        Dispatcher.UIThread.Post(() =>
        {
            // Guard: entity may have changed by the time this fires
            if (!ReferenceEquals(vm.CurrentEntity, entity)) return;
            try
            {
                var vis = registry.Get(entity.GetType());
                var control = vis?.BuildDetail(entity);
                PeekContentHost.Content = control;
            }
            catch
            {
                PeekContentHost.Content = null;
            }
        }, DispatcherPriority.Background);
    }
}
