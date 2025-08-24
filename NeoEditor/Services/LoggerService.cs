using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Messages;

namespace NeoEditor.Services;

public class LoggerService : ObservableRecipient, IRecipient<LogMessage>
{
    public LoggerService(ILogger<App> logger)
    {
        Console.WriteLine("loggerService!");
        Logger = logger;
        IsActive = true;
    }

    private ILogger<App> Logger { get; }

    public async void Receive(LogMessage message)
    {
        Console.WriteLine(message.Message);
        Logger.Log(message.Level, message.Message);
        if (message.MsgBox) await Show(message);
    }

    public async Task Show(LogMessage message)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            // var msgBox = new MessageBox
            // {
            //     Title = message.Level.ToString(),
            //     Content = message.Message
            // };
            // await msgBox.ShowDialogAsync();
            MessageBox.Show(message.Message, message.Level.ToString());
        });
    }
}