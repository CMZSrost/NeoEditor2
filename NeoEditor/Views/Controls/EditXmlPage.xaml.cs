using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.Views.Controls;

public partial class EditXmlPage : UserControl
{
    private readonly EditXmlViewModel _viewModel;
    private bool _hasLoaded;

    public EditXmlPage(IContainer container, IEventAggregator eventAggregator)
    {
        _viewModel = container.GetService<EditXmlViewModel>();
        DataContext = _viewModel;
        InitializeComponent();
        
        // 调试输出：验证每个页面都有独立的 ViewModel
        System.Diagnostics.Debug.WriteLine($"[EditXmlPage] Created new instance with ViewModel HashCode: {_viewModel.GetHashCode()}");
        
        // 只在首次 Loaded 时加载文件
        Loaded += OnPageLoaded;
    }

    public string? XmlPath { get; set; }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // 只加载一次，防止切换标签时重复加载
        if (!_hasLoaded && !string.IsNullOrEmpty(XmlPath))
        {
            _hasLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[EditXmlPage] Loading file for the first time: {XmlPath}");
            await _viewModel.LoadXmlAsync(XmlPath);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[EditXmlPage] Skipping reload - already loaded: {XmlPath}");
        }
    }
}

