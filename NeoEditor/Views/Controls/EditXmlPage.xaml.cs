using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.ViewModels.Controls;

namespace NeoEditor.Views.Controls;

public partial class EditXmlPage : UserControl
{
    private readonly IEventAggregator _eventAggregator;
    private readonly EditXmlViewModel _viewModel;

    public EditXmlPage(IContainer container, IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        _viewModel = container.GetService<EditXmlViewModel>();
        DataContext = _viewModel;
        InitializeComponent();
        Subscribe();
    }

    public string? XmlPath { get; set; }

    private void Subscribe()
    {
        _eventAggregator.GetEvent<OpenXmlEvent>().Subscribe(LoadXml, ThreadOption.UIThread);
    }

    private async void LoadXml(OpenXmlMessage message)
    {
        XmlPath = message.FilePath;
        if (!string.IsNullOrEmpty(XmlPath))
            await _viewModel.LoadXmlAsync(XmlPath);
    }
}