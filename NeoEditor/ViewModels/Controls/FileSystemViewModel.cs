using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
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

    private bool ValidateProject()
    {
        return true;
    }
}