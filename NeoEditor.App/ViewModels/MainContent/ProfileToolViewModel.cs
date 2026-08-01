using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.MainContent;

/// <summary>Profile entry shown in the Profile Tool's selector.</summary>
public record ProfileOption(int ProfileId, string Name, ProfileInfo Info);

/// <summary>
/// Profile Tool (left dock, D02 §5.0). Mod management (New / Import) plus
/// profile orchestration entry points (Edit Profile → EditProfileView document,
/// Reload Merge View → reloads the merge data view). Spec: D02 §五.
/// </summary>
public partial class ProfileToolViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IModManager _modManager;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IMessenger _messenger;

    public ObservableCollection<ProfileOption> Profiles { get; } = [];

    [ObservableProperty] public partial ProfileOption? SelectedProfile { get; set; }

    public ProfileToolViewModel(IServiceProvider serviceProvider,
        IModManager modManager,
        IDbContextFactory<EditorDbContext> editorDbFactory,
        IMessenger messenger)
    {
        _serviceProvider = serviceProvider;
        _modManager = modManager;
        _editorDbFactory = editorDbFactory;
        _messenger = messenger;

        Helper.AsyncHelper.FireAndForget(LoadProfilesAsync());
        // Refresh the selector after a profile is saved so the list stays current.
        _messenger.Register<EditProfileMessage>(this, (_, _) => Helper.AsyncHelper.FireAndForget(LoadProfilesAsync()));
    }

    public async Task RefreshAsync() => await LoadProfilesAsync();

    private async Task LoadProfilesAsync()
    {
        try
        {
            await using var db = await _editorDbFactory.CreateDbContextAsync();
            var infos = await db.ProfileInfos.OrderByDescending(p => p.UpdateTime).ToListAsync();
            Profiles.Clear();
            foreach (var p in infos)
                Profiles.Add(new ProfileOption(p.ProfileId, p.Name, p));
        }
        catch
        {
            /* DB may not be initialized yet */
        }
    }

    [RelayCommand]
    private async Task NewMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;

        var dialog = Views.Dialog.CreateModDialog.Create(_serviceProvider);
        var result = await dialog.ShowDialog<ModInfo?>(mainWindow);
        if (result is not null)
            _messenger.Send(new OpenModGameDataDocumentMessage(result));
    }

    [RelayCommand]
    private async Task ImportMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Mod Folder",
            AllowMultiple = false
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folderPath)
        {
            var modInfo = await _modManager.ImportModAsync(folderPath);
            if (modInfo is not null)
                _messenger.Send(new OpenModGameDataDocumentMessage(modInfo));
        }
    }

    [RelayCommand]
    private void EditProfile()
    {
        if (SelectedProfile is { } p)
            _messenger.Send(new EditProfileMessage(p.Info));
    }

    [RelayCommand]
    private void ReloadMergeView()
    {
        if (SelectedProfile is { } p)
            _messenger.Send(new OpenMergeEditorMessage(p.Info));
    }
}
