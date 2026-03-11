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

    private void ReloadHelpMenu()
    {
        HelpMenuItems.Clear();

        var helpCultureDirectory = ResolveHelpCultureDirectory(CurrentCultureInfo);
        if (string.IsNullOrWhiteSpace(helpCultureDirectory) || !Directory.Exists(helpCultureDirectory))
        {
            return;
        }

        foreach (var node in BuildHelpMenuTree(helpCultureDirectory))
        {
            HelpMenuItems.Add(node);
        }
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
        var helpRoots = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Help"),
            Path.Combine(AppContext.BaseDirectory, "Help")
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        var cultureNames = new[]
            {
                culture.Name,
                culture.TwoLetterISOLanguageName,
                _defaultCultureCode
            }.Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var helpRoot in helpRoots)
        {
            foreach (var cultureName in cultureNames)
            {
                var candidate = Path.Combine(helpRoot, cultureName);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool IsSupportedHelpFile(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() is ".md" or ".markdown";
    }
}