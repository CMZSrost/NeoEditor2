using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Avalonia.Xaml.Interactivity;
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
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.ViewModels;

public partial class EditProfileViewModel : ViewModelBase, IDocumentBase
{
    private INotificationService _notificationService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;


    [ObservableProperty] public partial string Title { get; set; } = "Edit Profile";
    [ObservableProperty] public partial bool CanClose { get; set; } = true;
    [ObservableProperty] public partial bool NeedNotifyWhenClose { get; set; } = false;

    [ObservableProperty] public partial ProfileInfo? ProfileInfo { get; set; }
    private readonly PhpParser _phpParser;
    public ObservableCollection<ModEntry> Entries { get; } = new();
    [ObservableProperty] public partial ModEntry? SelectedEntry { get; set; }

    public EditProfileViewModel() : this(App.ServiceProvider!)
    {
    }

    public EditProfileViewModel(IServiceProvider serviceProvider)
    {
        _phpParser = serviceProvider.GetRequiredService<PhpParser>();

        _serviceProvider = serviceProvider;
        _notificationService = serviceProvider.GetRequiredService<INotificationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
    }

    [RelayCommand]
    public void LoadEntries()
    {
        if (ProfileInfo is null) return;
        var entries = _phpParser.ParseModsContent(ProfileInfo.Content);
        foreach (var entry in entries)
            Entries.Add(entry);
    }

    [RelayCommand]
    public void Add()
    {
        // 简单添加一个默认条目，可让用户随后编辑
        Entries.Add(new ModEntry { Name = "New Mod", Path = "path/to/mod" });
        NeedNotifyWhenClose = true;
    }

    [RelayCommand]
    public void Delete()
    {
        if (SelectedEntry != null)
        {
            Entries.Remove(SelectedEntry);
            NeedNotifyWhenClose = true;
        }
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
            NeedNotifyWhenClose = true;
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
            NeedNotifyWhenClose = true;
        }
    }

    [RelayCommand]
    public void Save()
    {
        if (ProfileInfo is not null)
        {
            ProfileInfo.Content = _phpParser.GenerateModsPhp(Entries.ToList());
            Messenger.Send(new SaveProfileMessage(ProfileInfo));
            NeedNotifyWhenClose = false;
        }
    }


    [RelayCommand]
    private void OnEntriesLoadingRow(DataGridRowEventArgs e)
    {
        var behaviors = Interaction.GetBehaviors(e.Row);
        if (!behaviors.Any(b => b is ContextDragBehavior))
        {
            behaviors.Add(new ContextDragBehavior());
        }
    }
}