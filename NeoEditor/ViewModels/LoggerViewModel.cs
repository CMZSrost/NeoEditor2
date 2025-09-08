using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Messages;

namespace NeoEditor.ViewModels;

public class LoggerViewModel : ObservableRecipient, IRecipient<LogMessage>
{
    public ObservableCollection<LogMessage> Logs { get; } = new();
    public LoggerViewModel(ILogger<App> logger)
    {
        Console.WriteLine("loggerService!");
        Logger = logger;
        IsActive = true;
    }

    private ILogger<App> Logger { get; }

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