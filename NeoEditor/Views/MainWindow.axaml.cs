using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Dock.Model.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.ViewModels;
using NeoEditor.ViewModels.ExplorerPane;
using Serilog;

namespace NeoEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = App.ServiceProvider!.GetRequiredService<MainWindowViewModel>();
        DataContext = vm;
    }
}