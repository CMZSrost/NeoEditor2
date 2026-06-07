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
    IRecipient<OpenModImagesDocumentMessage>,
    IRecipient<OpenHelpDocumentMessage>,
    IRecipient<OpenMergeEditorMessage>,
    IRecipient<OpenImageDocumentMessage>
{
    public static DocumentWorkspaceViewModel? Instance { get; private set; }

    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<DocumentWorkspaceViewModel> _logger;
    private IMessenger _messenger = null!;

    public ObservableCollection<IDocumentBase> Documents { get; }

    [ObservableProperty] public partial bool IsHomePageVisible { get; set; } = true;
    [ObservableProperty] public partial string ActiveDocumentTitle { get; set; } = "";

    private readonly OverlayChainToolContent _overlayChainContent;

    public OverlayChainToolContent OverlayChainContent => _overlayChainContent;
    public ReferenceInspectorContent ReferenceInspector { get; } = new();
    public ImagePreviewContent ImagePreview { get; } = new();
    public BottomToolsViewModel BottomTools { get; } = new();

    // ToolDock tool instances
    public OverlayChainTool LeftTool { get; private set; } = null!;
    public ValueEditorTool ValueEditor { get; private set; } = null!;
    public ImagePreviewTool ImagePreviewTool { get; private set; } = null!;
    public ReferenceInspectorTool RefInspectorTool { get; private set; } = null!;
    public SearchResultsTool SearchResultsTool { get; private set; } = null!;
    public ConflictsTool ConflictsTool { get; private set; } = null!;
    public ValidationTool ValidationTool { get; private set; } = null!;

    public HomePageViewModel HomePage { get; }

    public System.Collections.ObjectModel.ObservableCollection<object> LeftToolItems { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<object> RightToolItems { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<object> BottomToolItems { get; } = [];

    [ObservableProperty] public partial bool IsLeftToolVisible { get; set; } = true;
    [ObservableProperty] public partial bool IsRightToolVisible { get; set; } = true;
    [ObservableProperty] public partial bool IsBottomToolVisible { get; set; } = true;

    public DocumentWorkspaceViewModel() : this(App.ServiceProvider)
    {
    }

    public DocumentWorkspaceViewModel(IServiceProvider serviceProvider)
    {
        Instance = this;
        _config = serviceProvider.GetRequiredService<IConfigService>();
        _logger = serviceProvider.GetRequiredService<ILogger<DocumentWorkspaceViewModel>>();
        _messenger = serviceProvider.GetRequiredService<IMessenger>();

        // Restore panel visibility from config
        var cfg = _config.Config;
        if (cfg is not null)
        {
            IsLeftToolVisible = cfg.LeftPanelVisible;
            IsRightToolVisible = cfg.RightPanelVisible;
            IsBottomToolVisible = cfg.BottomPanelVisible;
        }

        HomePage = new HomePageViewModel();
        Documents = [];
        Documents.CollectionChanged += async (_, _) =>
        {
            IsHomePageVisible = Documents.Count == 0;
            if (IsHomePageVisible)
                await HomePage.RefreshAsync();
        };

        _overlayChainContent = new OverlayChainToolContent();
        LeftTool = new OverlayChainTool(_overlayChainContent);
        ValueEditor = new ValueEditorTool();
        ImagePreviewTool = new ImagePreviewTool(ImagePreview);
        RefInspectorTool = new ReferenceInspectorTool(ReferenceInspector);
        SearchResultsTool = new SearchResultsTool(BottomTools);
        ConflictsTool = new ConflictsTool(BottomTools);
        ValidationTool = new ValidationTool(BottomTools);
        // Tool items collections kept for potential future use

        // Peek: Ctrl+Click reference → show in Reference Inspector. Return true to block navigation.
        Helper.GenericDataGridHelper.PeekRequested = (type, rawId, entity) =>
        {
            ReferenceInspector.ShowEntity(type, rawId, entity);
            return entity is not null; // block navigation when entity found (peek-only)
        };

        // Row selection → update image preview
        _messenger.Register<VisualEditorRequestedMessage>(this, (_, m) => ImagePreview.ShowEntity(m.Entity));

        // Subscribe to overlay chain updates from ModGameDataTabsView
        _messenger.Register<OverlayChainRequestedMessage>(this, (_, m) =>
        {
            _overlayChainContent.LoadChain(m.EntityId, m.Subject, m.EntityType);
            IsLeftToolVisible = true;
        });

        // Conflicts auto-update

        // Validation request
        _messenger.Register<RequestValidationMessage>(this, (_, _) =>
        {
            BottomTools.SetValidationResults("Click Save (Ctrl+S) to run validation.", "");
        });

        _messenger.Register<ValidationCompletedMessage>(this, (_, m) =>
        {
            if (m.Warnings == 0 && m.Errors == 0)
                BottomTools.SetValidationResults("Validation passed — no issues.", "");
            else
                BottomTools.SetValidationResults(
                    $"{m.Errors} error(s), {m.Warnings} warning(s) in changed entities.", "");
            BottomTools.SelectedTabIndex = 2; // switch to Validation tab
        });
        DockFactory = serviceProvider.GetRequiredService<Factory>();
        DockFactory.DockableClosing += ClosingDockable;
        Loc.PropertyChanged += OnLocalizationPropertyChanged;
        _messenger.Register<MergeViewDirtyChangedMessage>(this, (_, m) => OnMergeViewDirtyChanged(m.IsDirty));
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
        var document = new ImageEditorDocument();
        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
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

        var isMergeOpen = Documents.OfType<MergeEditorDocument>().Any();
        var document = new ModGameDataDocument
        {
            ModInfo = message.ModInfo,
            ReadOnly = message.ReadOnly || isMergeOpen,
        };
        document.SetLocalizedTitle("ModGameDataTitleFormat", message.ModInfo.Name);

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    public void Receive(OpenModImagesDocumentMessage message)
    {
        _logger.LogInformation("Opening mod images document: {ModName}", message.ModInfo.Name);

        if (FindOpenModImagesDocument(message.ModInfo) is { } existingDocument)
        {
            existingDocument.SetLocalizedTitle("ModImagesTitleFormat", message.ModInfo.Name);
            ActivateDocument(existingDocument);
            return;
        }

        var document = new ModImagesDocument(message.ModInfo);
        document.SetLocalizedTitle("ModImagesTitleFormat", message.ModInfo.Name);

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

    public async void Receive(OpenMergeEditorMessage message)
    {
        _logger.LogInformation("Opening merge editor for profile: {ProfileName} (id={ProfileId})",
            message.ProfileInfo.Name, message.ProfileInfo.ProfileId);

        if (FindOpenMergeEditorDocument(message.ProfileInfo) is { } existing)
        {
            ActivateDocument(existing);
            return;
        }

        // Synchronously load all mods into DB BEFORE creating the merge view.
        // This is critical: ModLoadInfos is [NotMapped] and must be populated from Content.
        // The merge view queries DB directly, so entities MUST be in DB first.
        try
        {
            var profileManager = App.ServiceProvider!.GetRequiredService<IProfileManager>();
            var modManager = App.ServiceProvider!.GetRequiredService<IModManager>();
            var gameRoot = App.ServiceProvider!.GetRequiredService<IConfigService>().Config.GameRootDir;

            _logger.LogInformation("[PreLoad] Content length={Len}", message.ProfileInfo.Content?.Length ?? -1);

            var modLoadInfos = profileManager.LoadMods(message.ProfileInfo.Content);
            message.ProfileInfo.ModLoadInfos.Clear();
            foreach (var mli in modLoadInfos)
                message.ProfileInfo.ModLoadInfos.Add(mli);

            _logger.LogInformation("[PreLoad] parsed {Count} mod(s) from getmods.php", modLoadInfos.Count);

            foreach (var mli in message.ProfileInfo.ModLoadInfos)
            {
                _logger.LogInformation("[PreLoad] mod: namespace='{Ns}' hasInfo={HasInfo} modId={ModId} path='{Path}'",
                    mli.Namespace, mli.Info is not null, mli.Info?.ModId ?? -999, mli.Info?.Path ?? "(null)");

                if (mli.Info is null) continue;

                if (mli.Info.ModId <= 0)
                {
                    var modPath = System.IO.Path.Combine(gameRoot, mli.Info.Path ?? "");
                    _logger.LogInformation("[PreLoad] attempting import: '{Path}' exists={Exists}",
                        modPath, System.IO.Directory.Exists(modPath));
                    if (!string.IsNullOrWhiteSpace(mli.Info.Path) && System.IO.Directory.Exists(modPath))
                    {
                        var imported = await modManager.ImportModAsync(modPath);
                        if (imported is not null)
                        {
                            mli.Info = imported;
                            _logger.LogInformation("[PreLoad] import OK: '{Name}' ModId={ModId}",
                                imported.Name, imported.ModId);
                        }
                        else
                        {
                            _logger.LogWarning("[PreLoad] import FAILED for '{Path}'", mli.Info.Path);
                        }
                    }
                }
                else
                {
                    try
                    {
                        await modManager.LoadModAsync(mli.Info);
                        _logger.LogInformation("[PreLoad] LoadModAsync OK: '{Name}' ModId={ModId}",
                            mli.Info.Name, mli.Info.ModId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PreLoad] LoadModAsync FAILED: '{Name}'", mli.Info.Name);
                    }
                }
            }
            _logger.LogInformation("[PreLoad] complete: {Count} mod(s) processed",
                message.ProfileInfo.ModLoadInfos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PreLoad] FATAL error during mod loading");
        }

        // Only one merge view at a time — close any existing merge editors
        foreach (var existingMerge in Documents.OfType<MergeEditorDocument>().ToList())
        {
            Documents.Remove(existingMerge);
        }

        // Set all single mod views to read-only
        foreach (var modDoc in Documents.OfType<ModGameDataDocument>())
            modDoc.ReadOnly = true;

        var document = new MergeEditorDocument
        {
            ProfileInfo = message.ProfileInfo
        };
        document.SetLocalizedTitle("MergeEditorTitleFormat", message.ProfileInfo.Name);

        Documents.Add(document);
        ActivateDocument(document);
        UpdateDockingEnabled();
    }

    private MergeEditorDocument? FindOpenMergeEditorDocument(ProfileInfo profileInfo)
    {
        return Documents.OfType<MergeEditorDocument>()
            .FirstOrDefault(d => d.ProfileInfo?.ProfileId == profileInfo.ProfileId);
    }

    public void Receive(OpenImageDocumentMessage message)
    {
        var normalizedPath = NormalizeDocumentPath(message.ImagePath);
        if (!File.Exists(normalizedPath)) return;

        if (FindOpenImageDocument(normalizedPath) is { } existing)
        {
            ActivateDocument(existing);
            return;
        }

        var doc = new ImageDocument { ImagePath = normalizedPath };
        doc.SetStaticTitle(message.Title);
        Documents.Add(doc);
        ActivateDocument(doc);
    }

    private ImageDocument? FindOpenImageDocument(string path)
    {
        return Documents.OfType<ImageDocument>()
            .FirstOrDefault(d => string.Equals(d.ImagePath, path, StringComparison.OrdinalIgnoreCase));
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
        Helper.AsyncHelper.FireAndForget(ConfirmCloseDockableAsync(docContext));
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

        if (docContext is MergeEditorDocument { NeedNotifyWhenClose: true } or ModGameDataDocument { NeedNotifyWhenClose: true })
        {
            var result = await ShowConfirmDialogAsync(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                ContentTitle = "Unsaved Changes",
                ContentMessage = "You have unsaved changes in the merge view. Save before closing?",
                Icon = Icon.Question
            });
            switch (result)
            {
                case ButtonResult.Yes:
                    _messenger.Send(new SaveRequestedMessage());
                    // Wait briefly for save to complete
                    await System.Threading.Tasks.Task.Delay(300);
                    break;
                case ButtonResult.Cancel:
                    return;
            }
        }

        if (docContext is ModImagesDocument { ModInfo: { } modInfo, NeedNotifyWhenClose: true } imageDocument)
        {
            _logger.LogInformation("Closing image document for mod: {ModName}", modInfo.Name);

            var result = await ShowConfirmDialogAsync(new MessageBoxStandardParams
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                ContentTitle = Loc["CloseModImages"],
                ContentMessage = Loc["CloseModImagesConfirmation"],
                Icon = Icon.Question
            });

            switch (result)
            {
                case ButtonResult.Yes:
                    await imageDocument.SaveCommand.ExecuteAsync(null);
                    if (imageDocument.NeedNotifyWhenClose)
                    {
                        return;
                    }
                    break;
                case ButtonResult.Cancel:
                    return;
            }
        }

        Documents.Remove(docContext);
        UpdateDockingEnabled();

        // When merge view is closed, restore single mod views to editable
        if (docContext is MergeEditorDocument && !Documents.OfType<MergeEditorDocument>().Any())
        {
            foreach (var modDoc in Documents.OfType<ModGameDataDocument>())
                modDoc.ReadOnly = false;
        }
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

    private ModImagesDocument? FindOpenModImagesDocument(ModInfo modInfo)
    {
        var documentKey = GetModImagesDocumentKey(modInfo);
        return Documents
            .OfType<ModImagesDocument>()
            .FirstOrDefault(doc => string.Equals(GetModImagesDocumentKey(doc.ModInfo), documentKey,
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

    private void OnMergeViewDirtyChanged(bool isDirty)
    {
        var mergeDoc = Documents.OfType<MergeEditorDocument>().FirstOrDefault();
        if (mergeDoc is not null)
            mergeDoc.NeedNotifyWhenClose = isDirty;
        var modDoc = Documents.OfType<ModGameDataDocument>().FirstOrDefault();
        if (modDoc is not null)
            modDoc.NeedNotifyWhenClose = isDirty;
    }

    public void ActivateDocument(IDocumentBase document)
    {
        ActiveDocumentTitle = document.Title ?? "";
        // Collapse sidebar when opening a document
        try { App.ServiceProvider?.GetService<MainWindowSideBarViewModel>()?.TogglePaneCommand.Execute(null); } catch { }
        try
        {
            var sidebar = App.ServiceProvider?.GetService<MainWindowSideBarViewModel>();
            if (sidebar is not null) sidebar.SideBarExpanded = false;
        }
        catch { }

        var currentIndex = Documents.IndexOf(document);
        if (currentIndex < 0 || currentIndex == Documents.Count - 1)
        {
            return;
        }

        Documents.RemoveAt(currentIndex);
        Documents.Add(document);
    }

    [RelayCommand]
    private void ToggleLeftPanel()
    {
        IsLeftToolVisible = !IsLeftToolVisible;
        SaveLayout();
    }

    [RelayCommand]
    private void ToggleRightPanel()
    {
        IsRightToolVisible = !IsRightToolVisible;
        SaveLayout();
    }

    [RelayCommand]
    private void ToggleBottomPanel()
    {
        IsBottomToolVisible = !IsBottomToolVisible;
        SaveLayout();
    }

    /// <summary>Called by DocumentWorkspaceView to persist panel sizes.</summary>
    public void SaveLayoutSizes(double leftWidth, double rightWidth, double bottomHeight)
    {
        var cfg = _config.Config;
        if (cfg is null) return;
        cfg.LeftPanelWidth = leftWidth;
        cfg.RightPanelWidth = rightWidth;
        cfg.BottomPanelHeight = bottomHeight;
        SaveLayout();
    }

    private void SaveLayout()
    {
        var cfg = _config.Config;
        if (cfg is null) return;
        cfg.LeftPanelVisible = IsLeftToolVisible;
        cfg.RightPanelVisible = IsRightToolVisible;
        cfg.BottomPanelVisible = IsBottomToolVisible;
        Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
    }

    [RelayCommand]
    private void CloseAllDocuments()
    {
        // Remove all non-merge documents
        var toRemove = Documents.Where(d => d is not MergeEditorDocument).ToList();
        foreach (var doc in toRemove)
            Documents.Remove(doc);
        // Close merge editors last
        var merges = Documents.OfType<MergeEditorDocument>().ToList();
        foreach (var m in merges)
            Documents.Remove(m);

        // Restore single-mod editability
        foreach (var modDoc in Documents.OfType<ModGameDataDocument>())
            modDoc.ReadOnly = false;
    }

    private void UpdateDockingEnabled()
    {
        IsDockingEnabled = true; // always on — enables drag-to-split for comparison
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

    private string GetModImagesDocumentKey(ModInfo? modInfo)
    {
        return GetModGameDataDocumentKey(modInfo);
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