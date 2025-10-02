using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Messages;

namespace NeoEditor.ViewModels;

public class LoggerViewModel : ObservableObject
{
    private readonly IEventAggregator _eventAggregator;

    public LoggerViewModel(ILogger<App> logger, IEventAggregator eventAggregator)
    {
        Console.WriteLine("loggerService!");
        _eventAggregator = eventAggregator;
        Logger = logger;
        Subscribe();
    }

    public ObservableCollection<LogMessage> Logs { get; } = new();

    private ILogger<App> Logger { get; }

    private void Subscribe()
    {
        _eventAggregator.GetEvent<LoggingEvent>().Subscribe(Receive);
    }

    public async void Receive(LogMessage message)
    {
        Logger.Log(message.Level, message.Message);
        Logs.Add(message);
        if (message.MsgBox) await Show(message);
    }

    public async Task Show(LogMessage message)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            MessageBox.Show(message.Message, message.Level.ToString());
        });
    }
}