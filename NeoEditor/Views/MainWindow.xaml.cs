using System.Windows;
using AvalonDock.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Services;
using NeoEditor.ViewModels;
using NeoEditor.Views.Controls;

namespace NeoEditor.Views;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MessageHelper _messageHelper = new();

    public MainWindow(
        MainWindowViewModel vm,
        LoggerService loggerService
    )
    {
        DataContext = vm;
        LoggerService = loggerService;
        _messageHelper.LoadProjectMessageHandler += ReceiveLoadProject;

        InitializeComponent();
    }

    private LoggerService LoggerService { get; }

    private void ReceiveLoadProject(LoadProjectMessage message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var children = DocumentPane.Children;
            children?.Clear();
            children?.Add(new LayoutDocument
            {
                Title = "edit",
                Content = App.Services.GetService<EditTablePage>()
            });
        });
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        _messageHelper.Onloaded();
    }

    public class MessageHelper : ObservableRecipient, IRecipient<LoadProjectMessage>
    {
        public MessageHelper()
        {
            IsActive = true;
        }

        public void Receive(LoadProjectMessage message)
        {
            LoadProjectMessageHandler?.Invoke(message);
        }

        public void Onloaded()
        {
            Messenger.Send(new MainWindowLoadedMessage());
        }

        public event Action<LoadProjectMessage>? LoadProjectMessageHandler;
    }
}