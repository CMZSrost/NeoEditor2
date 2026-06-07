using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;

namespace NeoEditor.Views.UserControls;

public partial class ValidationView : UserControl
{
    public ValidationView() => InitializeComponent();

    private void OnRunValidationClick(object? sender, RoutedEventArgs e)
        => App.ServiceProvider!.GetRequiredService<IMessenger>().Send(new RequestValidationMessage());
}
