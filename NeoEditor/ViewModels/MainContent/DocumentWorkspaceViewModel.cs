using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Avalonia;
using Dock.Model.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.MainContent;

public partial class DocumentWorkspaceViewModel : ViewModelBase,
    IRecipient<EditProfileMessage>,
    IRecipient<OpenXmlDocumentMessage>,
    IRecipient<OpenModGameDataDocumentMessage>,
    IRecipient<OpenHelpDocumentMessage>
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<DocumentWorkspaceViewModel> _logger;

    public ObservableCollection<IDocumentBase> Documents { get; }

    public DocumentWorkspaceViewModel() : this(App.ServiceProvider)
    {
    }

    public DocumentWorkspaceViewModel(IServiceProvider serviceProvider)
    {
        _config = serviceProvider.GetRequiredService<IConfigService>();
        _logger = serviceProvider.GetRequiredService<ILogger<DocumentWorkspaceViewModel>>();
        Documents = [CreateWelcomeDocument()];
        DockFactory = serviceProvider.GetRequiredService<Factory>();
        DockFactory.DockableClosing += ClosingDockable;
        Loc.PropertyChanged += OnLocalizationPropertyChanged;
        UpdateDockingEnabled();
    }

    [ObservableProperty] public partial Factory DockFactory { get; set; }

    private bool _isDockingEnabled;

    public bool IsDockingEnabled
    {
        get => _isDockingEnabled;
        set => SetProperty(ref _isDockingEnabled, value);
    }

    [RelayCommand]
    private void AddImage()
    {
    }

    [RelayCommand]
    private void AddDocument()
    {
        var index = Documents.Count + 1;
        var document = new PlainTextDocument();
        document.SetLocalizedTitle("NewDocumentTitleFormat", index);
        document.SetLocalizedContent("NewDocumentContentFormat", index);
        Documents.Add(document);
        UpdateDockingEnabled();
    }

    public void Receive(EditProfileMessage message)
    {
        _logger.LogInformation("Loading profile: {ProfileName}", message.ProfileInfo.Name);

        if (FindOpenEditProfileDocument(message.ProfileInfo) is { } existingDocument)
        {
            ActivateDocument(existingDocument);
            return;
        }

        var viewModel = new EditProfileViewModel
        {
            ProfileInfo = message.ProfileInfo,
        };
        viewModel.SetStaticTitle(message.ProfileInfo.Name);
        viewModel.LoadEntries();
        Documents.Add(viewModel);
        ActivateDocument(viewModel);
        UpdateDockingEnabled();
    }

    public void Receive(OpenModGameDataDocumentMessage message)
    {
        _logger.LogInformation("Opening mod game data document: {ModName}", message.ModInfo.Name);

        if (FindOpenModGameDataDocument(message.ModInfo) is { } existingDocument)
        {
            existingDocument.ModInfo = message.ModInfo;
            existingDocument.SetLocalizedTitle("ModGameDataTitleFormat", message.ModInfo.Name);
            ActivateDocument(existingDocument);
            return;
        }

        var document = new ModGameDataDocument
        {
            ModInfo = message.ModInfo,
            ReadOnly = true,
        };
        document.SetLocalizedTitle("ModGameDataTitleFormat", message.ModInfo.Name);

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
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
        var document = new XmlDocument(normalizedPath);
        document.SetStaticTitle(title);

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    public void Receive(OpenHelpDocumentMessage message)
    {
        var normalizedPath = NormalizeDocumentPath(message.DocumentPath);
        _logger.LogInformation("Opening help document: {HelpPath}", normalizedPath);

        if (!File.Exists(normalizedPath))
        {
            return;
        }

        if (FindOpenHelpDocument(normalizedPath) is { } existingDocument)
        {
            ActivateDocument(existingDocument);
            return;
        }

        var title = string.IsNullOrWhiteSpace(message.Title)
            ? Path.GetFileNameWithoutExtension(normalizedPath)
            : message.Title;
        MarkdownDocument? document = Path.GetExtension(normalizedPath).ToLowerInvariant() switch
        {
            ".md" or ".markdown" => new MarkdownDocument(normalizedPath, title),
            _ => null
        };

        if (document is null)
        {
            return;
        }

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    public void ClosingDockable(object? sender, DockableClosingEventArgs e)
    {
        if (e.Dockable is not { Context: IDocumentBase docContext })
        {
            return;
        }

        e.Cancel = true;
        _ = ConfirmCloseDockableAsync(docContext);
    }

    private async Task ConfirmCloseDockableAsync(IDocumentBase docContext)
    {
        if (docContext is EditProfileViewModel { ProfileInfo: { } profileInfo, NeedNotifyWhenClose: true } model)
        {
            _logger.LogInformation("Closing document for profile: {ProfileName}", profileInfo.Name);

            var result = await ShowConfirmDialogAsync(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                ContentTitle = Loc["CloseProfile"],
                ContentMessage = Loc["CloseProfileConfirmation"],
                Icon = Icon.Question
            });

            switch (result)
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
        UpdateDockingEnabled();
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

    private EditProfileViewModel? FindOpenEditProfileDocument(ProfileInfo profileInfo)
    {
        var documentKey = GetEditProfileDocumentKey(profileInfo);
        return Documents
            .OfType<EditProfileViewModel>()
            .FirstOrDefault(doc => string.Equals(GetEditProfileDocumentKey(doc.ProfileInfo), documentKey,
                StringComparison.OrdinalIgnoreCase));
    }

    private ModGameDataDocument? FindOpenModGameDataDocument(ModInfo modInfo)
    {
        var documentKey = GetModGameDataDocumentKey(modInfo);
        return Documents
            .OfType<ModGameDataDocument>()
            .FirstOrDefault(doc => string.Equals(GetModGameDataDocumentKey(doc.ModInfo), documentKey,
                StringComparison.OrdinalIgnoreCase));
    }

    private XmlDocument? FindOpenXmlDocument(string normalizedPath)
    {
        return Documents
            .OfType<XmlDocument>()
            .FirstOrDefault(doc => string.Equals(NormalizeDocumentPath(doc.XmlPath), normalizedPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private MarkdownDocument? FindOpenHelpDocument(string normalizedPath)
    {
        return Documents
            .OfType<MarkdownDocument>()
            .FirstOrDefault(doc => string.Equals(NormalizeDocumentPath(doc.FilePath), normalizedPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private void ActivateDocument(IDocumentBase document)
    {
        var currentIndex = Documents.IndexOf(document);
        if (currentIndex < 0 || currentIndex == Documents.Count - 1)
        {
            return;
        }

        Documents.RemoveAt(currentIndex);
        Documents.Add(document);
    }

    private void UpdateDockingEnabled()
    {
        IsDockingEnabled = Documents.Count >= 2;
    }

    private PlainTextDocument CreateWelcomeDocument()
    {
        var document = new PlainTextDocument();
        document.SetLocalizedTitle("Welcome");
        document.SetLocalizedContent("WelcomeDocumentContent");
        return document;
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(LocalizationService.CurrentCulture))
        {
            return;
        }

        foreach (var document in Documents)
        {
            document.RefreshLocalizedText();
        }
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

        var normalizedPath = NormalizeWorkspacePath(profileInfo.Path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            return $"path:{normalizedPath}";
        }

        return $"name:{profileInfo.Name}";
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

        var normalizedPath = NormalizeWorkspacePath(modInfo.Path);
        if (!string.IsNullOrWhiteSpace(normalizedPath))
        {
            return $"path:{normalizedPath}";
        }

        return $"name:{modInfo.Name}";
    }

    private string NormalizeWorkspacePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalizedPath = path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedPath))
        {
            return Path.GetFullPath(normalizedPath);
        }

        return string.IsNullOrWhiteSpace(Config.GameRootDir)
            ? normalizedPath.TrimStart(Path.DirectorySeparatorChar)
            : Path.GetFullPath(Path.Combine(Config.GameRootDir, normalizedPath));
    }

    private static string NormalizeDocumentPath(string path)
    {
        return Path.GetFullPath(path);
    }
}