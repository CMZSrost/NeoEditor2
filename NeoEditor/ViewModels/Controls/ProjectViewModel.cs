using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Options;
using NeoEditor.Helpers;
using NeoEditor.Services.Worker;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.ViewModels.Controls;

public partial class ProjectViewModel : ObservableObject
{
    private readonly IEventAggregator _eventAggregator;
    private readonly ProjectLoadingWorker _worker;

    public ProjectViewModel(IOptions<ProjectOption> projectOption, ProjectLoadingWorker worker,
        IEventAggregator eventAggregator)
    {
        _worker = worker;
        _eventAggregator = eventAggregator;
        ModConfigFilePath = projectOption.Value.ModConfigPath;
        Subscribe();
    }

    [ObservableProperty] public partial string? ModConfigFilePath { get; set; }
    private string? ProjectRootDirectory => Path.GetDirectoryName(ModConfigFilePath);
    private string ProjectModDirectory => Path.Join(ProjectRootDirectory, "Mods");
    private string ProjectDataDirectory => Path.Join(ProjectRootDirectory, "Data");

    public ObservableCollection<ModData> Mods { get; } = new();

    private void Subscribe()
    {
        _eventAggregator.GetEvent<MainWindowLoadedEvent>().Subscribe(Receive);
        _eventAggregator.GetEvent<SetProjectEvent>().Subscribe(Receive);
        _eventAggregator.GetEvent<LoadFromXmlEvent>().Subscribe(Receive);
    }

    private async void Receive(LoadFromXmlMessage message)
    {
        Console.WriteLine("Recv LoadData!");
        try
        {
            CancellationTokenSource cancellationTokenSource = new();
            var token = cancellationTokenSource.Token;
            foreach (var modData in Mods.Order())
            {
                var urls = Directory.EnumerateFiles(
                    Path.Join(ProjectRootDirectory, modData.ModDirectoryPath),
                    "*.xml",
                    SearchOption.AllDirectories
                );
                foreach (var url in urls)
                    _worker.Add(new ModXmlData
                    {
                        ModIndex = modData.ModIndex,
                        ModName = modData.ModName,
                        XmlPath = url
                    });
            }

            await _worker.RunAsync(message.FilePath, token);
            _eventAggregator.GetEvent<LoadFromXlsxEvent>()
                .Publish(new LoadFromXlsxMessage { FilePath = message.FilePath });
        }
        catch (Exception ex)
        {
            _eventAggregator.GetEvent<LoggingEvent>()
                .Publish(new LogMessage
                {
                    Level = LogLevel.Error, Message = $"load from xml error: {ex.Message}\n{ex.StackTrace}",
                    MsgBox = true
                });
        }
    }

    private void Receive(MainWindowLoadedMessage message)
    {
        if (File.Exists(ModConfigFilePath))
            _eventAggregator.GetEvent<SetProjectEvent>()
                .Publish(new SetProjectMessage { ModConfigFilePath = ModConfigFilePath });
    }

    private async void Receive(SetProjectMessage message)
    {
        Console.WriteLine(message.ModConfigFilePath);
        _eventAggregator.GetEvent<LoggingEvent>()
            .Publish(new LogMessage { Message = $"recv OpenProjectMessage: {message}" });
        ModConfigFilePath = message.ModConfigFilePath;

        var eModConfig = File.Exists(ModConfigFilePath);
        var eRootDirectory = Directory.Exists(ProjectRootDirectory);
        var eModDirectory = Directory.Exists(ProjectModDirectory);
        var eDataDirectory = Directory.Exists(ProjectDataDirectory);

        if (!(eModConfig && eRootDirectory && eModDirectory && eDataDirectory))
        {
            _eventAggregator.GetEvent<LoggingEvent>()
                .Publish(new LogMessage
                {
                    Level = LogLevel.Warning,
                    Message =
                        $"Invalid project path modConfig:{eModConfig} rootDir:{eRootDirectory} modDir:{eModDirectory} dataDir:{eDataDirectory}"
                });
            return;
        }

        Mods.Clear();
        Mods.Add(new ModData
        {
            ModName = "data",
            ModDirectoryPath = "data",
            ModDirectoryName = "data",
            ModIndex = -1
        });
        await foreach (var modData in PhPHelper.FileToList(ModConfigFilePath)) Mods.Add(modData);

        // _eventAggregator.GetEvent<OpenEditTableEvent>().Publish(new OpenEditTableMessage
        // {
        //     ProjectRootDirectory = ProjectRootDirectory!,
        //     ProjectDataFolder = ProjectDataDirectory,
        //     ProjectModFolder = ProjectModDirectory,
        //     ModConfigFilePath = ModConfigFilePath
        // });
    }

    [RelayCommand]
    private void OpenEditTable()
    {
        if (ModConfigFilePath is null)
        {
            _eventAggregator.GetEvent<LoggingEvent>().Publish(new LogMessage
            {
                Level = LogLevel.Warning,
                Message = "ModConfigFilePath is null! Open edit table failed."
            });
            return;
        }

        _eventAggregator.GetEvent<OpenEditTableEvent>()
            .Publish(new OpenEditTableMessage
            {
                ProjectRootDirectory = ProjectRootDirectory!,
                ProjectDataFolder = ProjectDataDirectory,
                ProjectModFolder = ProjectModDirectory,
                ModConfigFilePath = ModConfigFilePath
            });
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