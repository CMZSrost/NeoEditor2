using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Core.Events;
using NeoEditor.Data.Messages;
using NeoEditor.ViewModels;
using NeoEditor.ViewModels.ExplorerPane;
using Serilog;

namespace NeoEditor.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;

    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.S:
                    if (_vm?.CurrentPage == PageType.Workspace)
                    {
                        var messenger = WeakReferenceMessenger.Default;
                        // R11: Ctrl+S saves only the current document/tab, not all.
                        messenger.Send(new SaveRequestedMessage(SaveScope.CurrentTab));
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
}