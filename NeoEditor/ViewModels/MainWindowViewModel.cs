using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Options;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly string _defaultCultureCode;

    public MainWindowViewModel() : this(App.ServiceProvider)
    {
    }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        SideBar = serviceProvider.GetRequiredService<MainWindowSideBarViewModel>();
        DocumentWorkspace = serviceProvider.GetRequiredService<DocumentWorkspaceViewModel>();

        var settings = serviceProvider.GetRequiredService<IOptions<CultureSettings>>().Value;
        _defaultCultureCode = settings.DefaultCulture.Code;
        CurrentCultureInfo = Loc.CurrentCulture;
        SupportedCultures =
            new ObservableCollection<CultureInfo>(settings.Cultures.Select(info => new CultureInfo(info.Code)));

        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
        _config = serviceProvider.GetRequiredService<IConfigService>();

        Loc.PropertyChanged += OnLocalizationPropertyChanged;
        ReloadHelpMenu();
    }

    public MainWindowSideBarViewModel SideBar { get; }
    public DocumentWorkspaceViewModel DocumentWorkspace { get; }
    public ObservableCollection<IDocumentBase> OpenedDocuments => DocumentWorkspace.Documents;

    public ObservableCollection<CultureInfo> SupportedCultures { get; }
    [ObservableProperty] public partial CultureInfo CurrentCultureInfo { get; set; }
    public ObservableCollection<HelpMenuNode> HelpMenuItems { get; } = new();

    [RelayCommand]
    public async Task SetFolder()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc["SelectGameRootDir"],
            AllowMultiple = false
        });

        foreach (var folder in folders)
        {
            var folderPath = folder.TryGetLocalPath();
            if (folderPath == null)
            {
                continue;
            }

            _logger.LogInformation("Selected folder: {FolderPath}", folderPath);
            Config.GameRootDir = folderPath;
            await _config.SaveAsync();
            return;
        }
    }

    public void NavigateToSettings()
    {
        SideBar.CurrentPaneContent = App.ServiceProvider!.GetRequiredService<ExplorerPane.SettingsPaneViewModel>();
        SideBar.SideBarExpanded = true;
    }

    [RelayCommand]
    private void ChangeCulture(CultureInfo? culture)
    {
        _logger.LogInformation("Changing culture to: {CultureName}", culture?.Name);
        if (culture is null)
        {
            return;
        }

        CurrentCultureInfo = culture;
        Loc.SetCulture(culture);
        OnPropertyChanged(nameof(Loc));
    }

    [RelayCommand]
    private async Task ImportTutorial()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider == null) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc["ImportTutorial"],
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Markdown & Images")
                {
                    Patterns = new[] { "*.md", "*.markdown", "*.png", "*.jpg", "*.jpeg", "*.gif", "*.svg" }
                }
            }
        });

        if (files.Count == 0) return;

        var helpDir = ResolveHelpCultureDirectory(CurrentCultureInfo);
        if (string.IsNullOrWhiteSpace(helpDir) || !Directory.Exists(helpDir))
        {
            helpDir = Path.Combine(AppContext.BaseDirectory, "Help",
                CurrentCultureInfo.TwoLetterISOLanguageName);
            Directory.CreateDirectory(helpDir);
        }

        foreach (var file in files)
        {
            var filePath = file.TryGetLocalPath();
            if (filePath == null) continue;
            var destPath = Path.Combine(helpDir, Path.GetFileName(filePath));
            if (!File.Exists(destPath) || await ConfirmOverwriteAsync(destPath))
            {
                File.Copy(filePath, destPath, overwrite: true);
            }
        }

        ReloadHelpMenu();
    }

    private async Task<bool> ConfirmOverwriteAsync(string path)
    {
        return true; // For now, always overwrite
    }

    [RelayCommand]
    private void OpenHelpDocument(HelpMenuNode? node)
    {
        if (node is not { IsLeaf: true, AbsolutePath: { } absolutePath, DocumentTitle: { } title } ||
            !File.Exists(absolutePath))
        {
            return;
        }

        DocumentWorkspace.Receive(new OpenHelpDocumentMessage(absolutePath, title));
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocalizationService.CurrentCulture))
        {
            CurrentCultureInfo = Loc.CurrentCulture;
            ReloadHelpMenu();
        }
    }

    public void ReloadHelpMenu()
    {
        HelpMenuItems.Clear();

        var helpCultureDirectory = ResolveHelpCultureDirectory(CurrentCultureInfo);
        if (string.IsNullOrWhiteSpace(helpCultureDirectory) || !Directory.Exists(helpCultureDirectory))
        {
            _logger.LogWarning("Help: no Help/{Culture} dir found for culture {Culture}. " +
                               "Searched from assembly dir: {Base}. " +
                               "Place .md files in Help/zh/ or Help/en/ next to the executable.",
                CurrentCultureInfo?.Name ?? "?", AppContext.BaseDirectory);
            return;
        }

        foreach (var node in BuildHelpMenuTree(helpCultureDirectory))
            HelpMenuItems.Add(node);

        _logger.LogInformation("Help menu loaded {Count} items from {Dir}",
            HelpMenuItems.Count, helpCultureDirectory);
    }

    private IReadOnlyList<HelpMenuNode> BuildHelpMenuTree(string helpCultureDirectory)
    {
        var root = new HelpMenuNode { Header = string.Empty };
        foreach (var filePath in Directory.EnumerateFiles(helpCultureDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(IsSupportedHelpFile)
                     .OrderBy(path => Path.GetRelativePath(helpCultureDirectory, path),
                         StringComparer.OrdinalIgnoreCase))
        {
            AddHelpNode(root, helpCultureDirectory, filePath);
        }

        SortHelpNodes(root.Children);
        return root.Children.ToList();
    }

    private void AddHelpNode(HelpMenuNode root, string helpCultureDirectory, string filePath)
    {
        var relativePath = Path.GetRelativePath(helpCultureDirectory, filePath);
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
        if (segments.Length == 0)
        {
            return;
        }

        var current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            var segment = segments[index];
            var next = current.Children.FirstOrDefault(node =>
                string.Equals(node.Header, segment, StringComparison.OrdinalIgnoreCase));
            if (next is null)
            {
                next = new HelpMenuNode
                {
                    Header = segment,
                    RelativePath = string.Join('/', segments.Take(index + 1))
                };
                current.Children.Add(next);
            }

            current = next;
        }

        var fileName = Path.GetFileNameWithoutExtension(segments[^1]);
        var normalizedAbsolutePath = Path.GetFullPath(filePath);
        var documentTitle = Path.ChangeExtension(string.Join('/', segments), null);

        current.Children.Add(new HelpMenuNode
        {
            Header = fileName,
            AbsolutePath = normalizedAbsolutePath,
            RelativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/'),
            DocumentTitle = documentTitle,
            Command = OpenHelpDocumentCommand,
            CommandParameter = null
        });
    }

    private static void SortHelpNodes(IEnumerable<HelpMenuNode> nodes)
    {
        foreach (var node in nodes)
        {
            SortHelpNodes(node.Children);
        }

        if (nodes is not ObservableCollection<HelpMenuNode> collection)
        {
            return;
        }

        var orderedNodes = collection
            .OrderBy(node => node.IsLeaf)
            .ThenBy(node => node.Header, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        collection.Clear();
        foreach (var node in orderedNodes)
        {
            collection.Add(node);
        }
    }

    private string? ResolveHelpCultureDirectory(CultureInfo culture)
    {
        // Collect all potential Help roots
        var candidates = new List<string>();

        // 1. Output directory (bin/Debug/netX.Y/Help) — CopyToOutputDirectory
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "Help"));

        // 2. Current working directory
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "Help"));

        // 3. Walk up from assembly to find project root (handles Debug/Release, net10.0 subfolder)
        var assemblyDir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var probe = Path.Combine(assemblyDir, "Help");
            if (Directory.Exists(probe)) { candidates.Add(probe); break; }
            assemblyDir = Path.GetDirectoryName(assemblyDir);
            if (assemblyDir is null) break;
        }

        var helpRoots = candidates
            .Select(p => { try { return Path.GetFullPath(p); } catch { return null; } })
            .Where(p => p is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var cultureNames = new[]
        {
            culture.Name,
            culture.TwoLetterISOLanguageName,
            _defaultCultureCode
        }.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var helpRoot in helpRoots)
        {
            if (helpRoot is null || !Directory.Exists(helpRoot)) continue;
            foreach (var cultureName in cultureNames)
            {
                var candidate = Path.Combine(helpRoot, cultureName);
                if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*.md").Any())
                {
                    _logger.LogInformation("Help menu root: {Path}", candidate);
                    return candidate;
                }
            }
        }

        _logger.LogWarning("No Help directory found. Checked roots: {Roots}", string.Join(", ", helpRoots));
        return null;
    }

    private static bool IsSupportedHelpFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() is ".md" or ".markdown";
    }
}