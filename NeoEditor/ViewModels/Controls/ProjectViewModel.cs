using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Options;
using NeoEditor.Helpers;
using NeoEditor.ViewModels.Data;

namespace NeoEditor.ViewModels.Controls;

public partial class ProjectViewModel : ObservableRecipient, IRecipient<SetProjectMessage>, IRecipient<LoadDataMessage>,
    IRecipient<MainWindowLoadedMessage>
{
    private readonly XmlLoader _loader;
    private readonly SerialIdHelper _serialIdHelper;

    public ProjectViewModel(IOptions<ProjectOption> projectOption, XmlLoader loader, SerialIdHelper serialIdHelper)
    {
        IsActive = true;
        _loader = loader;
        _serialIdHelper = serialIdHelper;
        ModConfigFilePath = projectOption.Value.ModConfigPath;
    }

    [ObservableProperty] public partial string? ModConfigFilePath { get; set; }
    private string? ProjectRootDirectory => Path.GetDirectoryName(ModConfigFilePath);
    private string ProjectModDirectory => Path.Join(ProjectRootDirectory, "Mods");
    private string ProjectDataDirectory => Path.Join(ProjectRootDirectory, "Data");

    public ObservableCollection<ModData> Mods { get; } = new();

    public async void Receive(LoadDataMessage message)
    {
        try
        {
            await _loader.Clean();
            foreach (var modData in Mods)
            {
                var offset = _loader.Idx;
                var urls = Directory.EnumerateFiles(
                    Path.Join(ProjectRootDirectory, modData.ModDirectoryPath),
                    "*.xml",
                    SearchOption.AllDirectories
                );
                foreach (var url in urls)
                    try
                    {
                        await _loader.LoadXml(url, modData);
                    }
                    catch (Exception ex)
                    {
                        Messenger.Send(new LogMessage
                            { Level = LogLevel.Warning, Message = $"load {url} error: {ex}" });
                    }

                Messenger.Send(new LogMessage { Message = $"{modData.ModName} loaded {_loader.Idx - offset} items" });
            }

            await _serialIdHelper.ReorderAll();

            // Console.WriteLine($"loaded {_loader.Idx} items");
            Messenger.Send(new LogMessage { Message = $"loaded {_loader.Idx} items" });
        }
        catch (Exception ex)
        {
            Messenger.Send(new LogMessage { Level = LogLevel.Error, Message = $"{ex}", MsgBox = true });
        }
    }

    public void Receive(MainWindowLoadedMessage message)
    {
        if (File.Exists(ModConfigFilePath))
            Messenger.Send(new SetProjectMessage { ModConfigFilePath = ModConfigFilePath });
    }

    public async void Receive(SetProjectMessage message)
    {
        Messenger.Send(new LogMessage { Message = $"recv OpenProjectMessage: {message}" });
        ModConfigFilePath = message.ModConfigFilePath;

        var eModConfig = File.Exists(ModConfigFilePath);
        var eRootDirectory = Directory.Exists(ProjectRootDirectory);
        var eModDirectory = Directory.Exists(ProjectModDirectory);
        var eDataDirectory = Directory.Exists(ProjectDataDirectory);

        if (!(eModConfig && eRootDirectory && eModDirectory && eDataDirectory))
        {
            Messenger.Send(new LogMessage
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
            ModDirectory = "data",
            ModIndex = -1
        });
        await foreach (var modData in PhPHelper.FileToList(ModConfigFilePath)) Mods.Add(modData);

        Messenger.Send(new OpenEditTableMessage
        {
            ProjectRootDirectory = ProjectRootDirectory!,
            ProjectDataFolder = ProjectDataDirectory,
            ProjectModFolder = ProjectModDirectory,
            ModConfigFilePath = ModConfigFilePath
        });
    }

    [RelayCommand]
    public void OpenEditTable()
    {
        if (ModConfigFilePath is null)
        {
            Messenger.Send(new LogMessage
            {
                Level = LogLevel.Warning,
                Message = "ModConfigFilePath is null! Open edit table failed."
            });
            return;
        }

        Messenger.Send(new OpenEditTableMessage
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