using System.IO;
using System.Windows;
using AvalonDock.Layout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Options;
using NeoEditor.ViewModels;
using NeoEditor.ViewModels.Controls;
using NeoEditor.Views.Controls;

namespace NeoEditor.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindowView : Window
{
    private readonly string? _editExcelName;
    private readonly Func<EditTableReoPage> _editTablePageFactory;
    private readonly Func<EditXmlPage> _editXmlPageFactory;
    private readonly IEventAggregator _eventAggregator;

    public MainWindowView(
        MainWindowViewModel vm,
        IOptions<ProjectOption> options,
        IEventAggregator eventAggregator,
        Func<EditTableReoPage> editTablePageFactory,
        Func<EditXmlPage> editXmlPageFactory
    )
    {
        DataContext = vm;
        _editExcelName = options.Value.EditExcelName;
        _eventAggregator = eventAggregator;
        _editTablePageFactory = editTablePageFactory;
        _editXmlPageFactory = editXmlPageFactory;

        InitializeComponent();
        Subscribe();
    }

    private void Subscribe()
    {
        _eventAggregator.GetEvent<OpenEditTableEvent>().Subscribe(ReceiveLoadProject, ThreadOption.UIThread);
        _eventAggregator.GetEvent<OpenXmlEvent>().Subscribe(ReceiveOpenXml, ThreadOption.UIThread);
    }


    private void ReceiveLoadProject(OpenEditTableMessage message)
    {
        var children = DocumentPane.Children;
        var editTable = _editTablePageFactory.Invoke();
        editTable.Loaded += (sender, args) =>
        {
            Console.WriteLine("emit!!");
            if (_editExcelName == null || !_editExcelName.EndsWith(".xlsx"))
            {
                Console.WriteLine();
                _eventAggregator.GetEvent<LoggingEvent>().Publish(new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message = $"excel名称 \"{_editExcelName}\" 为空或后缀名不正确"
                });
                return;
            }

            var excelPath = Path.Join(message.ProjectRootDirectory, _editExcelName);

            if (File.Exists(excelPath))
                _eventAggregator.GetEvent<LoadFromXlsxEvent>().Publish(new LoadFromXlsxMessage
                    { FilePath = excelPath, TargetTable = "edit" });
            else
                _eventAggregator.GetEvent<LoadFromXmlEvent>().Publish(new LoadFromXmlMessage { FilePath = excelPath });
        };

        children?.Clear();
        children?.Add(new LayoutDocument
        {
            Title = "edit",
            Content = editTable
        });
    }

    private void ReceiveOpenXml(OpenXmlMessage message)
    {
        var editXml = _editXmlPageFactory.Invoke();
        editXml.Name = Path.GetFileNameWithoutExtension(message.FilePath);

        editXml.Loaded += async (sender, args) =>
        {
            await ((EditXmlViewModel)editXml.DataContext).LoadXmlAsync(message.FilePath);
        };

        DocumentPane.Children?.Add(new LayoutDocument
        {
            Title = Path.GetFileName(message.FilePath),
            Content = editXml
        });
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        _eventAggregator.GetEvent<MainWindowLoadedEvent>().Publish(new MainWindowLoadedMessage());
    }
}