using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NeoEditor.Data.Messages;

namespace NeoEditor.ViewModels.Controls;

public partial class MenuViewModel : ObservableObject
{
    private readonly IEventAggregator _eventAggregator;

    public MenuViewModel(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    public string AppVersion => "1.0.0";

    // [ObservableProperty] public partial string CurrentTheme { get; set; } = string.Empty;
    [ObservableProperty] public partial string CurrentTheme { get; set; } = "Light";
    public List<string> Themes => ["Dark", "Light", "HighContrast", "Unknown"];

    private void Subscribe()
    {
        // _eventAggregator.GetEvent<>()
    }


    [RelayCommand]
    private void SetProject()
    {
        var dialog = new OpenFileDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Filter = "Mod PHP|*.php*"
        };
        if (dialog.ShowDialog() != true) return;
        Console.WriteLine(dialog.FileName);
        if (!File.Exists(dialog.FileName)) return;

        Console.WriteLine($"send OpenProjectMessage: {dialog.FileName}");
        _eventAggregator.GetEvent<SetProjectEvent>()
            .Publish(new SetProjectMessage { ModConfigFilePath = dialog.FileName });
    }

    [RelayCommand]
    private void CloseProject()
    {
        _eventAggregator.GetEvent<CloseProjectEvent>().Publish(new CloseProjectMessage());
    }
}