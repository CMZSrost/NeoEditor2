using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class ModDatabaseViewModel : ViewModelBase
{
    private ProjectDbContextFactory _gameContextFactory;
    private IDbContextFactory<EditorDbContext> _editorContextFactory;
    private readonly EditorDbContext _editorDbContext;
    private readonly ILogger<ModDatabaseViewModel> _logger;
    public ObservableCollection<ModInfo> Mods { get; }

    [ObservableProperty] public partial string Filter { get; set; } = "";

    public ModDatabaseViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ProjectDbContextFactory>(),
        App.ServiceProvider!.GetRequiredService<ILogger<ModDatabaseViewModel>>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<EditorDbContext>())
    {
    }

    public ModDatabaseViewModel(ProjectDbContextFactory gameContextFactory, ILogger<ModDatabaseViewModel> logger,
        IDbContextFactory<EditorDbContext> editorContextFactory, EditorDbContext editorDbContext)
    {
        _gameContextFactory = gameContextFactory;
        _logger = logger;
        _editorContextFactory = editorContextFactory;
        _editorDbContext = editorDbContext;
        editorDbContext.ModInfos.Load();
        Mods = _editorDbContext.ModInfos.Local.ToObservableCollection();
    }

    [RelayCommand]
    public async Task ImportMod()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow); // 获取顶层窗口
            var storageProvider = topLevel?.StorageProvider;
            if (storageProvider == null) return;
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = App.Localizor!["SelectModFolder"], // 选择Mod文件夹
                AllowMultiple = true, // 允许多选
            });

            await using var dbContext = await _editorContextFactory.CreateDbContextAsync();

            foreach (var folder in folders)
            {
                if (folder.TryGetLocalPath() is { } folderPath)
                {
                    await AddNewMod(folderPath, dbContext);
                }
            }

            dbContext.ModInfos.Local.Clear();
            await dbContext.ModInfos.LoadAsync();
        }
    }

    private async Task AddNewMod(string modFolder, EditorDbContext dbContext)
    {
        if (Mods!.Any(m => m.Path == modFolder))
        {
            App.Notification!.ShowInfo($"Folder already imported: {modFolder}, skipping.");
            return;
        }

        try
        {
            await dbContext.ModInfos.AddAsync(new ModInfo
            {
                Name = Path.GetFileName(modFolder),
                Path = modFolder,
                IsBase = false,
                LastImport = DateTime.Now
            });
            await dbContext.SaveChangesAsync();
            App.Notification!.ShowInfo($"mod {modFolder} imported");
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"mod {modFolder} not imported: {e.Message}", "Import Warning");
        }
    }
}