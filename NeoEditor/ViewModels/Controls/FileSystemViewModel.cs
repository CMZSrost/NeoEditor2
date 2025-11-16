using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Options;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.ViewModels.Controls;

public partial class FileSystemViewModel : ObservableObject
{
    private readonly IEventAggregator _eventAggregator;
    private readonly SemaphoreSlim _fileOpenSemaphore = new(1, 1);

    public FileSystemViewModel(IOptions<ProjectOption> projectOption,
        IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        ModConfigFilePath = projectOption.Value.ModConfigPath;
        if (ProjectRootDirectory != null)
            Roots.Add(new FileSystemNodeViewModel(ProjectRootDirectory, this));
        Subscribe();
    }

    [ObservableProperty] public partial string? ModConfigFilePath { get; set; }
    private string? ProjectRootDirectory => Path.GetDirectoryName(ModConfigFilePath);
    private string ProjectModDirectory => Path.Join(ProjectRootDirectory, "Mods");
    private string ProjectDataDirectory => Path.Join(ProjectRootDirectory, "Data");
    public ObservableCollection<FileSystemNodeViewModel> Roots { get; } = [];

    private void Subscribe()
    {
    }


    public async void OpenFile(string filePath)
    {
        await _fileOpenSemaphore.WaitAsync();
        try
        {
            // 异步打开文件，避免阻塞UI
            await Task.Run(() =>
            {
                if (Path.GetExtension(filePath) == ".xml")
                    _eventAggregator.GetEvent<OpenXmlEvent>().Publish(
                        new OpenXmlMessage
                        {
                            FilePath = filePath,
                            Mode = FileMode.Open
                        }
                    );
            });
        }
        finally
        {
            _fileOpenSemaphore.Release();
        }
    }
    // private async void Receive(LoadFromXmlMessage message)
    // {
    //     Console.WriteLine("Recv LoadData!");
    //     try
    //     {
    //         CancellationTokenSource cancellationTokenSource = new();
    //         var token = cancellationTokenSource.Token;
    //         foreach (var modData in Mods.Order())
    //         {
    //             var urls = Directory.EnumerateFiles(
    //                 Path.Join(ProjectRootDirectory, modData.ModDirectoryPath),
    //                 "*.xml",
    //                 SearchOption.AllDirectories
    //             );
    //             foreach (var url in urls)
    //                 _worker.Add(new ModXmlData
    //                 {
    //                     ModIndex = modData.ModIndex,
    //                     ModName = modData.ModName,
    //                     XmlPath = url
    //                 });
    //         }
    //
    //         await _worker.RunAsync(message.FilePath, token);
    //         _eventAggregator.GetEvent<LoadFromXlsxEvent>()
    //             .Publish(new LoadFromXlsxMessage { FilePath = message.FilePath });
    //     }
    //     catch (Exception ex)
    //     {
    //         _eventAggregator.GetEvent<LoggingEvent>()
    //             .Publish(new LogMessage
    //             {
    //                 Level = LogLevel.Error, Message = $"load from xml error: {ex.Message}\n{ex.StackTrace}",
    //                 MsgBox = true
    //             });
    //     }
    // }

    private void Receive(MainWindowLoadedMessage message)
    {
        if (File.Exists(ModConfigFilePath))
            _eventAggregator.GetEvent<SetProjectEvent>()
                .Publish(new SetProjectMessage { ModConfigFilePath = ModConfigFilePath });
    }

    [RelayCommand]
    private void OpenEditXml()
    {
        if (ModConfigFilePath is null)
            _eventAggregator.GetEvent<LoggingEvent>().Publish(new LogMessage
            {
                Level = LogLevel.Warning,
                Message = "ModConfigFilePath is null! Open edit table failed."
            });

        // _eventAggregator.GetEvent<OpenXmlEvent>()
        //     .Publish(new OpenXmlMessage
        //     {
        //         // FilePath = 
        //     });
    }

    [RelayCommand]
    private void LoadData()
    {
    }

    private bool ValidateProject()
    {
        return true;
    }
}