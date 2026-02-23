using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.ViewModels;
using Serilog;

namespace NeoEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.ServiceProvider!.GetRequiredService<MainWindowViewModel>();
    }

    private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // 假设 _localizationService 是注入的本地化服务实例
        var vm = (MainWindowViewModel)DataContext!;
        GenericDataGridHelper.ConfigureColumn(e, key => vm.Loc[key], typeof(GameVar));
    }

    private void FolderEntity_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        Console.WriteLine($"double-tapped on {sender.GetType()}");
        if (sender is not TreeView
            {
                SelectedItem: FolderEntity { Info: FileInfo fileInfo } folderEntity
            } treeView) return;
        Console.WriteLine($"Double-tapped on file: {fileInfo.FullName}");
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