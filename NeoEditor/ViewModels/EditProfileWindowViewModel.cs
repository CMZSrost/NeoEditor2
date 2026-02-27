using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Options;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels.ExplorerPane;

namespace NeoEditor.ViewModels;

public partial class EditProfileWindowViewModel : ViewModelBase
{
    private INotificationService _notificationService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    public LocalizationService Loc { get; set; }


    [ObservableProperty] public partial ProfileInfo? ProfileInfo { get; set; }
    private readonly PhpParser _phpParser;
    public ObservableCollection<ModEntry> Entries { get; } = new();
    [ObservableProperty] public partial ModEntry? SelectedEntry { get; set; }

    public EditProfileWindowViewModel() : this(App.ServiceProvider!)
    {
    }

    public EditProfileWindowViewModel(IServiceProvider serviceProvider)
    {
        _phpParser = serviceProvider.GetRequiredService<PhpParser>();

        _serviceProvider = serviceProvider;
        Loc = serviceProvider.GetRequiredService<LocalizationService>();
        _notificationService = serviceProvider.GetRequiredService<INotificationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
    }

    [RelayCommand]
    public void LoadEntries()
    {
        if (ProfileInfo is null) return;
        var entries = _phpParser.ParseContent(ProfileInfo.Content);
        foreach (var entry in entries)
            Entries.Add(entry);
    }

    [RelayCommand]
    public void Add()
    {
        // 简单添加一个默认条目，可让用户随后编辑
        Entries.Add(new ModEntry { Name = "New Mod", Path = "path/to/mod" });
    }

    [RelayCommand]
    public void Delete()
    {
        if (SelectedEntry != null)
            Entries.Remove(SelectedEntry);
    }

    [RelayCommand]
    public void MoveUp()
    {
        if (SelectedEntry == null) return;

        var index = Entries.IndexOf(SelectedEntry);
        if (index > 0)
        {
            var item = SelectedEntry;
            Entries.RemoveAt(index);
            Entries.Insert(index - 1, item);
            SelectedEntry = item; // Keep selection
        }
    }

    [RelayCommand]
    public void MoveDown()
    {
        if (SelectedEntry == null) return;

        var index = Entries.IndexOf(SelectedEntry);
        if (index < Entries.Count - 1)
        {
            var item = SelectedEntry;
            Entries.RemoveAt(index);
            Entries.Insert(index + 1, item);
            SelectedEntry = item; // Keep selection
        }
    }

    [RelayCommand]
    public void Save()
    {
        if (ProfileInfo is not null)
        {
            ProfileInfo.Content = _phpParser.Generate(Entries.ToList()).Replace("\r\n", "");
            Messenger.Send(new SaveProfileMessage(ProfileInfo));
        }

        Cancel();
    }

    public event EventHandler? CloseRequested;

    [RelayCommand]
    public void Cancel()
    {
        // 通知窗口关闭（通过Window的Close方法）
        // 可以使用一个事件或通过交互服务
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}