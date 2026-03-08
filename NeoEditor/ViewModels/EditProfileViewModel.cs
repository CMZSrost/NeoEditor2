using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Context;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.Helper.DragDropHandler;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.ViewModels;

public partial class EditProfileViewModel : ViewModelBase, IDocumentBase
{
    private INotificationService _notificationService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IDbContextFactory<EditorDbContext> _editorDbContextFactory;
    private bool _isLoadingEntries;

    [ObservableProperty] public partial string Title { get; set; } = "Edit Profile";
    [ObservableProperty] public partial bool CanClose { get; set; } = true;
    [ObservableProperty] public partial bool NeedNotifyWhenClose { get; set; } = false;

    [ObservableProperty] public partial ProfileInfo? ProfileInfo { get; set; }
    private readonly PhpParser _phpParser;
    public ObservableCollection<ModEntry> Entries { get; } = new();
    public ObservableCollection<ModInfo> AvailableMods { get; } = new();
    [ObservableProperty] public partial ModEntry? SelectedEntry { get; set; }

    public ModInfo? SelectedModInfo
    {
        get;
        set
        {
            SetProperty(ref field, value);
            AddCommand.NotifyCanExecuteChanged();
        }
    }

    public ModEntryDropHandler DataGridDropHandler { get; }

    private bool CanAddSelectedMod()
    {
        return SelectedModInfo is { Path: { } path } && !HasEntryWithPath(path);
    }

    public EditProfileViewModel() : this(App.ServiceProvider!)
    {
    }

    public EditProfileViewModel(IServiceProvider serviceProvider)
    {
        _phpParser = serviceProvider.GetRequiredService<PhpParser>();
        DataGridDropHandler = serviceProvider.GetRequiredService<ModEntryDropHandler>();

        _notificationService = serviceProvider.GetRequiredService<INotificationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
        _editorDbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<EditorDbContext>>();

        Entries.CollectionChanged += OnEntriesCollectionChanged;
        LoadAvailableMods();
    }

    [RelayCommand]
    public void LoadEntries()
    {
        if (ProfileInfo is null) return;

        _isLoadingEntries = true;
        try
        {
            var originalContent = ProfileInfo.Content;
            foreach (var entry in Entries)
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
            }

            Entries.Clear();
            var entries = _phpParser.ParseModsContent(originalContent);
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }
        }
        finally
        {
            _isLoadingEntries = false;
            AddCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddSelectedMod))]
    public void Add()
    {
        if (SelectedModInfo is null || HasEntryWithPath(SelectedModInfo.Path))
        {
            return;
        }

        Entries.Add(new ModEntry
        {
            Name = SelectedModInfo.Name,
            Path = SelectedModInfo.Path
        });
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
            SynchronizeProfileContent(markAsDirty: false);
            Messenger.Send(new SaveProfileMessage(ProfileInfo));
            NeedNotifyWhenClose = false;
        }
    }

    private void LoadAvailableMods()
    {
        AvailableMods.Clear();
        using var db = _editorDbContextFactory.CreateDbContext();
        foreach (var modInfo in db.ModInfos
                     .Where(info => !info.IsBase)
                     .OrderBy(m => m.Name)
                     .ThenBy(m => m.Path)
                     .ToList())
        {
            AvailableMods.Add(modInfo);
        }

        SelectedModInfo = AvailableMods.FirstOrDefault();
    }

    private void OnEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<ModEntry>())
            {
                item.PropertyChanged -= OnEntryPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<ModEntry>())
            {
                item.PropertyChanged -= OnEntryPropertyChanged;
                item.PropertyChanged += OnEntryPropertyChanged;
            }
        }

        if (_isLoadingEntries)
        {
            AddCommand.NotifyCanExecuteChanged();
            return;
        }

        SynchronizeProfileContent(markAsDirty: true);
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingEntries)
        {
            return;
        }

        if (e.PropertyName == nameof(ModEntry.Name) ||
            e.PropertyName == nameof(ModEntry.Path) ||
            e.PropertyName == nameof(ModEntry.Type) ||
            string.IsNullOrWhiteSpace(e.PropertyName))
        {
            SynchronizeProfileContent(markAsDirty: true);
        }
    }

    private bool HasEntryWithPath(string path)
    {
        var normalizedPath = NormalizePath(path);
        return Entries.Any(entry => string.Equals(NormalizePath(entry.Path), normalizedPath,
            StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string? path)
    {
        return path?.Trim().Replace('\\', '/') ?? string.Empty;
    }

    private void SynchronizeProfileContent(bool markAsDirty)
    {
        try
        {
            if (ProfileInfo is null)
            {
                return;
            }

            ProfileInfo.Content = _phpParser.GenerateModsPhp(Entries.ToList());
            if (markAsDirty)
            {
                NeedNotifyWhenClose = true;
            }
        }
        finally
        {
            AddCommand.NotifyCanExecuteChanged();
        }
    }
}