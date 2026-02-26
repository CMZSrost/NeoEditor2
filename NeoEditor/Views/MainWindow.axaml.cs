using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels;
using NeoEditor.ViewModels.ExplorerPane;
using Serilog;

namespace NeoEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider!.GetRequiredService<MainWindowViewModel>();
    }

    private void FolderEntity_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TreeView
            {
                SelectedItem: FolderEntity { Info: FileInfo fileInfo } folderEntity
            } treeView) return;
        // 打开文件
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileInfo.FullName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, $"Failed to open file: {fileInfo.FullName}");
        }
    }
}