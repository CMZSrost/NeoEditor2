using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Xaml.Interactions.Custom;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Options;
using NeoEditor.ViewModels.ExplorerPane;
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Views;
using Newtonsoft.Json;
using Ursa.Controls;
using ModIndexViewModel = NeoEditor.ViewModels.ExplorerPane.ModIndexViewModel;

namespace NeoEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IRecipient<EditProfileMessage>, IRecipient<OpenXmlDocumentMessage>, IRecipient<OpenModGameDataDocumentMessage>
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private INotificationService _notificationService;
    private readonly ILogger<MainWindowViewModel> _logger;

    public ObservableCollection<IDocumentBase> Documents { get; } =
    [
        new PlainTextDocument()
        {
            Title = "Welcome",
            Content = "This is the NeoEditor, a modding tool for Neople games.\n\n" +
                      "Use the sidebar to explore resources, manage mods, and edit profiles.\n\n" +
                      "Click 'Set Game Folder' in the Project menu to get started."
        }
    ];

    public ObservableCollection<Tool> Tools { get; } =
    [
        new Tool()
        {
            Title = "Tool 1",
            Context = App.ServiceProvider!.GetRequiredService<EditProfileViewModel>(),
        }
    ];


    public MainWindowViewModel() : this(App.ServiceProvider!)
    {
    }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Loc = serviceProvider.GetRequiredService<LocalizationService>();
        var cultureSettings = serviceProvider.GetRequiredService<IOptions<CultureSettings>>();
        CurrentCultureInfo = new CultureInfo(cultureSettings.Value?.DefaultCulture.Code ?? "en-us");
        SupportedCultures = new ObservableCollection<CultureInfo>(
            cultureSettings.Value?.Cultures.Select((info => new CultureInfo(info.Code))) ??
            [new CultureInfo("en-us"), new CultureInfo("zh")]
        );
        _notificationService = serviceProvider.GetRequiredService<INotificationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
        _config = serviceProvider.GetRequiredService<IConfigService>();
        DockFactory = serviceProvider.GetRequiredService<Factory>();

        DockFactory.DockableClosing += ClosingDockable;
    }

    #region SideBar

    #region Base

    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty] public partial bool SideBarExpanded { get; set; } = false;
    [ObservableProperty] public partial object? CurrentPaneContent { get; set; }
    [ObservableProperty] public partial object? CurrentMainContent { get; set; }

    private string _currentPaneId = ""; // 当前打开的面板标识

    [RelayCommand]
    private void TogglePane(string paneId)
    {
        try
        {
            // 如果点击的是同一个面板，则切换展开/折叠状态
            if (_currentPaneId == paneId && SideBarExpanded)
            {
                SideBarExpanded = false;
                // 不清除 CurrentPaneContent，保留内容以便下次快速展开
                CurrentPaneContent = null;
            }
            else
            {
                // 切换为新面板，确保展开
                _currentPaneId = paneId;
                CurrentPaneContent = CreatePaneContent(paneId);
                SideBarExpanded = true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            throw;
        }
    }

    private object CreatePaneContent(string paneId)
    {
        return paneId switch
        {
            "Explorer" => _serviceProvider.GetRequiredService<ResourceManagerViewModel>(),
            "Search" => _serviceProvider.GetRequiredService<SearchPaneViewModel>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsPaneViewModel>(),
            "ModDatabase" => _serviceProvider.GetRequiredService<ModDatabaseViewModel>(),
            "Profiles" => _serviceProvider.GetRequiredService<ModIndexViewModel>(),
            _ => throw new NotSupportedException()
        };
    }

    #endregion

    #endregion

    #region Menu

    #region Project

    [RelayCommand]
    public async Task SetFolder()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow); // 获取顶层窗口
            var storageProvider = topLevel?.StorageProvider;
            if (storageProvider == null) return;
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = Loc!["SelectGameRootDir"],
                AllowMultiple = false
            });

            foreach (var folder in folders)
            {
                var folderPath = folder.TryGetLocalPath();
                if (folderPath != null)
                {
                    _logger.LogInformation($"Selected folder: {folderPath}");
                    Config.GameRootDir = folderPath;
                    await _config.SaveAsync();
                    return;
                }
            }
        }
    }

    #endregion

    #region Language

    [ObservableProperty] public partial LocalizationService Loc { get; set; }

    public ObservableCollection<CultureInfo> SupportedCultures { get; }
    [ObservableProperty] public partial CultureInfo CurrentCultureInfo { get; set; }

    [RelayCommand]
    private void ChangeCulture(CultureInfo? culture)
    {
        _logger.LogInformation($"Changing culture to: {culture?.Name}");
        if (culture is null)
        {
            return;
        }

        Loc.SetCulture(culture);
        OnPropertyChanged(nameof(Loc));
        CurrentCultureInfo = culture;
    }

    #endregion

    #endregion

    #region Profile

    public void Receive(EditProfileMessage message)
    {
        _logger.LogInformation($"Loading profile: {message.ProfileInfo.Name}");

        if (FindOpenEditProfileDocument(message.ProfileInfo) is { } existingDocument)
        {
            ActivateDocument(existingDocument);
            return;
        }

        var factory = _serviceProvider.GetRequiredService<Func<ProfileInfo, EditProfileViewModel>>();
        var vm = factory(message.ProfileInfo);
        Documents.Add(vm);
        ActivateDocument(vm);

        if (Documents.Count >= 2)
        {
            IsDockingEnabled = true;
        }
    }

    public void Receive(OpenModGameDataDocumentMessage message)
    {
        _logger.LogInformation("Opening mod game data document: {ModName}", message.ModInfo.Name);

        if (FindOpenModGameDataDocument(message.ModInfo) is { } existingDocument)
        {
            existingDocument.ModInfo = message.ModInfo;
            existingDocument.Title = $"Data: {message.ModInfo.Name}";
            ActivateDocument(existingDocument);
            return;
        }

        var document = new ModGameDataDocument
        {
            Title = $"Data: {message.ModInfo.Name}",
            ModInfo = message.ModInfo,
            ReadOnly = true,
        };

        Documents.Add(document);
        ActivateDocument(document);

        if (Documents.Count >= 2)
        {
            IsDockingEnabled = true;
        }
    }

    public void Receive(OpenXmlDocumentMessage message)
    {
        var normalizedPath = NormalizeDocumentPath(message.XmlPath);
        _logger.LogInformation("Opening xml document: {XmlPath}", normalizedPath);

        if (FindOpenXmlDocument(normalizedPath) is { } existingDocument)
        {
            ActivateDocument(existingDocument);
            return;
        }

        var title = string.IsNullOrWhiteSpace(message.Title)
            ? Path.GetFileName(normalizedPath)
            : message.Title;
        var document = new XmlDocument(normalizedPath)
        {
            Title = title,
        };

        Documents.Add(document);
        ActivateDocument(document);

        if (Documents.Count >= 2)
        {
            IsDockingEnabled = true;
        }
    }

    private EditProfileViewModel? FindOpenEditProfileDocument(ProfileInfo profileInfo)
    {
        var documentKey = GetEditProfileDocumentKey(profileInfo);
        return Documents
            .OfType<EditProfileViewModel>()
            .FirstOrDefault(doc => string.Equals(GetEditProfileDocumentKey(doc.ProfileInfo), documentKey,
                StringComparison.OrdinalIgnoreCase));
    }

    private string GetEditProfileDocumentKey(ProfileInfo? profileInfo)
    {
        if (profileInfo is null)
        {
            return string.Empty;
        }

        if (profileInfo.ProfileId != 0)
        {
            return $"profileid:{profileInfo.ProfileId}";
        }

        var normalizedPath = NormalizeProfileDocumentPath(profileInfo.Path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            return $"path:{normalizedPath}";
        }

        return $"name:{profileInfo.Name}";
    }

    private string NormalizeProfileDocumentPath(string? profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return string.Empty;
        }

        var path = profilePath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return string.IsNullOrWhiteSpace(Config.GameRootDir)
            ? path.TrimStart(Path.DirectorySeparatorChar)
            : Path.GetFullPath(Path.Combine(Config.GameRootDir, path));
    }

    private ModGameDataDocument? FindOpenModGameDataDocument(ModInfo modInfo)
    {
        var documentKey = GetModGameDataDocumentKey(modInfo);
        return Documents
            .OfType<ModGameDataDocument>()
            .FirstOrDefault(doc => string.Equals(GetModGameDataDocumentKey(doc.ModInfo), documentKey,
                StringComparison.OrdinalIgnoreCase));
    }

    private string GetModGameDataDocumentKey(ModInfo? modInfo)
    {
        if (modInfo is null)
        {
            return string.Empty;
        }

        if (modInfo.ModId != 0)
        {
            return $"modid:{modInfo.ModId}";
        }

        var normalizedPath = NormalizeModDocumentPath(modInfo.Path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            return $"path:{normalizedPath}";
        }

        return $"name:{modInfo.Name}";
    }

    private string NormalizeModDocumentPath(string? modPath)
    {
        if (string.IsNullOrWhiteSpace(modPath))
        {
            return string.Empty;
        }

        var path = modPath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return string.IsNullOrWhiteSpace(Config.GameRootDir)
            ? path.TrimStart(Path.DirectorySeparatorChar)
            : Path.GetFullPath(Path.Combine(Config.GameRootDir, path));
    }

    private XmlDocument? FindOpenXmlDocument(string normalizedPath)
    {
        return Documents
            .OfType<XmlDocument>()
            .FirstOrDefault(doc => string.Equals(NormalizeDocumentPath(doc.XmlPath), normalizedPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private void ActivateDocument(IDocumentBase document)
    {
        var currentIndex = Documents.IndexOf(document);
        if (currentIndex < 0)
        {
            return;
        }

        if (currentIndex == Documents.Count - 1)
        {
            return;
        }

        Documents.RemoveAt(currentIndex);
        Documents.Add(document);
    }

    private static string NormalizeDocumentPath(string path)
    {
        return Path.GetFullPath(path);
    }

    #endregion

    #region Document

    [ObservableProperty] public partial Factory DockFactory { get; set; }

    private bool _isDockingEnabled;

    public bool IsDockingEnabled
    {
        get => _isDockingEnabled;
        set => SetProperty(ref _isDockingEnabled, value);
    }

    [RelayCommand]
    private void AddDocument()
    {
        var index = Documents.Count + 1;
        Documents.Add(new PlainTextDocument()
        {
            Title = $"Document {index}",
            Content = $"Content of document {index}"
        });
        Console.WriteLine($"Document {index} created");
        if (Documents.Count >= 2)
            IsDockingEnabled = true;
    }

    public void ClosingDockable(object? sender, DockableClosingEventArgs e)
    {
        if (e.Dockable is not { Context: IDocumentBase docContext })
        {
            return;
        }

        // 接管cancel事件，显示确认对话框
        e.Cancel = true;
        _ = ConfirmCloseDockableAsync(docContext);
        if (Documents.Count < 2)
            IsDockingEnabled = false;
    }

    private async Task ConfirmCloseDockableAsync(IDocumentBase docContext)
    {
        if (docContext is EditProfileViewModel { ProfileInfo: { } profileInfo, NeedNotifyWhenClose: true } model)
        {
            _logger.LogInformation($"Closing document for profile: {profileInfo.Name}");

            var res = await ShowConfirmDialogAsync(new MessageBoxStandardParams()
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                ContentTitle = Loc["CloseProfile"],
                ContentMessage = Loc["CloseProfileConfirmation"],
                Icon = Icon.Question
            });

            switch (res)
            {
                case ButtonResult.Yes:
                    model.Save();
                    model.NeedNotifyWhenClose = false;
                    break;
                case ButtonResult.Cancel:
                    return;
            }
        }

        Documents.Remove(docContext);
    }

    private async Task<ButtonResult> ShowConfirmDialogAsync(MessageBoxStandardParams parameters)
    {
        var msgBox = MessageBoxManager.GetMessageBoxStandard(parameters);
        if (Application.Current is
            {
                ApplicationLifetime: IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: { } mainWindow
                }
            })
        {
            return await msgBox.ShowWindowDialogAsync(mainWindow);
        }

        return await msgBox.ShowAsync();
    }

    #endregion
}