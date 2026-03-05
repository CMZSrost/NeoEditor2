using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.DesignerSupport.Remote.HtmlTransport;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Services;
using NeoEditor.ViewModels.Dialog;
using NeoEditor.Views;
using NeoEditor.Views.Dialog;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class ModDatabaseViewModel : ViewModelBase, IRecipient<GameRootDirChangedMessage>,
    IRecipient<RefreshModMessage>
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;

    private ProjectDbContextFactory _gameContextFactory;
    private IDbContextFactory<EditorDbContext> _editorContextFactory;
    private readonly EditorDbContext _editorDbContext;
    private readonly IModManager _modManager;

    private readonly ILogger<ModDatabaseViewModel> _logger;
    public ObservableCollection<ModInfo> Mods { get; set; } = [];

    [ObservableProperty] public partial string Filter { get; set; } = "";
    [ObservableProperty] public partial ModInfo? SelectedItem { get; set; }

    public ModDatabaseViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ProjectDbContextFactory>(),
        App.ServiceProvider!.GetRequiredService<ILogger<ModDatabaseViewModel>>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<EditorDbContext>(),
        App.ServiceProvider!.GetRequiredService<IModManager>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>()
    )
    {
    }

    public ModDatabaseViewModel(ProjectDbContextFactory gameContextFactory, ILogger<ModDatabaseViewModel> logger,
        IDbContextFactory<EditorDbContext> editorContextFactory, EditorDbContext editorDbContext,
        IModManager modManager,
        IConfigService configService)
    {
        _config = configService;
        _gameContextFactory = gameContextFactory;
        _logger = logger;
        _editorContextFactory = editorContextFactory;
        _editorDbContext = editorDbContext;
        _modManager = modManager;
        editorDbContext.ModInfos.Load();
        foreach (var mod in _editorDbContext.ModInfos.ToList())
            Mods.Add(mod);
    }

    [RelayCommand]
    public async Task CreateMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var dialog = App.ServiceProvider.GetRequiredService<CreateModDialog>();

        await dialog.ShowDialog<ButtonResult>(mainWindow);
    }

    [RelayCommand]
    public async Task ImportMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        if (TopLevel.GetTopLevel(desktop.MainWindow) is not { StorageProvider: { } storageProvider }) return;
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            Title = App.Localizor!["SelectModFolder"], // 选择Mod文件夹
            AllowMultiple = true, // 允许多选
        });


        foreach (var folder in folders)
        {
            if (folder.TryGetLocalPath() is not { } folderPath) continue;
            if (Mods!.Any(m => m.Path == folderPath))
            {
                App.Notification!.ShowInfo($"Folder already imported: {folderPath}, skipping.");
                return;
            }

            await _modManager.ImportModAsync(folderPath);
        }

        Mods.Clear();
        await using var dbContext = await _editorContextFactory.CreateDbContextAsync();
        Mods.AddRange(dbContext.ModInfos.ToList());
    }

    [RelayCommand]
    public async Task RefreshMods(ModInfo? selectedItem = null)
    {
        Mods.Clear();
        Mods.AddRange(await _editorDbContext.ModInfos.ToListAsync());
        App.Notification.ShowSuccess(Loc["ReloadModsSuccess"], Loc["ReloadModsSuccessMessage"]);
    }

    [RelayCommand]
    public async Task ClearMods(ModInfo? selectedItem = null)
    {
        if (selectedItem is null)
        {
            _editorDbContext.ModInfos.Local.Clear();
            await _editorDbContext.SaveChangesAsync();
            Mods.Clear();
        }
        else
        {
            _editorDbContext.ModInfos.Remove(selectedItem);
            await _editorDbContext.SaveChangesAsync();
            Mods.Remove(Mods.First(m => m.ModId == selectedItem.ModId));
        }
    }

    [RelayCommand]
    public async Task DeleteMods(ModInfo? selectedItem = null)
    {
        if (Application.Current is not
            { ApplicationLifetime: IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow } }) return;

        if (selectedItem is null)
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(Loc["DeleteAllModsConfirmMessage"],
                Loc["DeleteAllModsConfirmMessageDetail"], ButtonEnum.YesNo, Icon.Warning);
            var result = await msgBox.ShowWindowDialogAsync(mainWindow);
            if (result != ButtonResult.Yes) return;
            foreach (var modInfo in Mods)
            {
                await _modManager.DeleteMod(modInfo);
            }
        }
        else
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(Loc["DeleteModsConfirmMessage"],
                Loc["DeleteModsConfirmMessageDetail"], ButtonEnum.YesNo, Icon.Warning);
            var result = await msgBox.ShowWindowDialogAsync(mainWindow);
            if (result != ButtonResult.Yes) return;
            await _modManager.DeleteMod(selectedItem);
        }

        await RefreshMods();
    }

    public void Receive(GameRootDirChangedMessage message)
    {
        ClearMods().Wait();
    }

    public void Receive(RefreshModMessage message)
    {
        Dispatcher.UIThread.InvokeAsync(() => RefreshMods());
    }
}