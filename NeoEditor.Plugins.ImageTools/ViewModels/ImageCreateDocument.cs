using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

/// <summary>
/// Create-image document: material sourcing only. The left side imports images,
/// hosts the AI candidate gallery and the pending list; the right side previews the
/// selected material. Double-clicking a pending item (or selecting + Open) opens it
/// in <see cref="ImageEditorDocument"/> via <see cref="OpenImageDocumentMessage"/> —
/// items stay in the list, nothing is copied or saved here.
/// </summary>
public partial class ImageCreateDocument : ImageToolDocumentBase
{
    private readonly IImageFileService _fileService;
    private readonly IMessenger _messenger;
    private Bitmap? _previewImage;

    public AiGenerationPanelViewModel AiPanel { get; }
    public ObservableCollection<PendingImageItem> PendingItems { get; } = [];

    /// <summary>Currently selected pending item (single-click) — drives the right-side preview.</summary>
    [ObservableProperty] public partial PendingImageItem? SelectedItem { get; set; }

    public bool HasPendingItems => PendingItems.Count > 0;
    public bool HasNoPendingItems => !HasPendingItems;
    public bool CanOpenPending => SelectedItem is not null || PendingItems.Any(item => item.IsSelected);

    public string OpenPendingButtonText
    {
        get
        {
            var checkedCount = PendingItems.Count(item => item.IsSelected);
            return checkedCount > 0 ? $"{Loc["OpenPending"]} ({checkedCount})" : Loc["OpenPending"];
        }
    }

    // ── Right-side preview ──
    public Bitmap? PreviewImage => _previewImage;
    public bool HasPreview => _previewImage is not null;
    public bool HasNoPreview => !HasPreview;
    public string PreviewName { get; private set; } = string.Empty;
    public string PreviewDimensions { get; private set; } = string.Empty;

    public ImageCreateDocument(ILocalizationService loc, IImageFileService fileService,
        AiGenerationPanelViewModel aiPanel, IMessenger messenger)
        : base(loc)
    {
        _fileService = fileService;
        _messenger = messenger;
        AiPanel = aiPanel;
        aiPanel.CandidateGenerated += OnCandidateGenerated;
        PendingItems.CollectionChanged += OnPendingItemsChanged;

        // Lazy cleanup: staged AI files left from a previous session are removed when
        // the create-image document is (re)built on app start.
        _fileService.CleanupStagedCandidates();

        SetLocalizedTitle("CreateImage");
    }

    /// <summary>Queue already-picked image files into the pending list (dedup by path);
    /// used by both the in-document import picker and the right-click Add Image.</summary>
    public void AddPendingFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (PendingItems.Any(item =>
                    string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            PendingItems.Add(new PendingImageItem
            {
                Path = path,
                DisplayName = Path.GetFileName(path),
                Kind = PendingImageKind.Imported
            });
        }
    }

    [RelayCommand]
    private async Task ImportImagesAsync()
    {
        var paths = await _fileService.PickImagesAsync(allowMultiple: true);
        AddPendingFiles(paths);
    }

    [RelayCommand(CanExecute = nameof(CanOpenPending))]
    private void OpenPending()
    {
        // Checked items win (batch); with nothing checked, open the selected item.
        var targets = PendingItems.Where(item => item.IsSelected).ToList();
        if (targets.Count == 0 && SelectedItem is not null)
        {
            targets.Add(SelectedItem);
        }

        foreach (var item in targets)
        {
            OpenItem(item);
        }
    }

    /// <summary>Open a pending item in an editor document. The item stays in the list
    /// (re-openable); staged AI files are cleaned lazily on the next app start.</summary>
    public void OpenItem(PendingImageItem item)
    {
        if (!PendingItems.Contains(item))
        {
            return;
        }

        _messenger.Send(new OpenImageDocumentMessage(Path.GetFileName(item.Path), item.Path));
    }

    partial void OnSelectedItemChanged(PendingImageItem? value)
    {
        // Selecting (single-click) a pending item shows it in the right-side preview pane.
        if (value is not null)
        {
            ShowPendingPreview(value);
        }

        // The button's IsEnabled binds to CanOpenPending (not just the command), so the
        // property needs a notification — otherwise Open stays disabled after selecting.
        OnPropertyChanged(nameof(CanOpenPending));
        OpenPendingCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(OpenPendingButtonText));
    }

    /// <summary>Every generated candidate is staged and queued into the pending list
    /// automatically — the in-panel gallery is gone, the right-side preview is the
    /// single place to inspect materials (double-click a pending item).</summary>
    private void OnCandidateGenerated(byte[] pngBytes, string name)
    {
        var stagedPath = _fileService.StageAiCandidate(pngBytes, name);
        PendingItems.Add(new PendingImageItem
        {
            Path = stagedPath,
            DisplayName = name,
            Kind = PendingImageKind.AiGenerated
        });
    }

    private void ShowPendingPreview(PendingImageItem item)
    {
        try
        {
            if (!File.Exists(item.Path))
            {
                return;
            }

            var bitmap = _fileService.FromFile(item.Path);
            SetPreview(bitmap, item.DisplayName, bitmap);
        }
        catch
        {
            // Preview is best-effort; the list item remains usable.
        }
    }

    private void SetPreview(Bitmap bitmap, string name, Bitmap dimensionsSource)
    {
        _previewImage?.Dispose();
        _previewImage = bitmap;
        PreviewName = name;
        PreviewDimensions = $"{dimensionsSource.PixelSize.Width} × {dimensionsSource.PixelSize.Height}px";
        OnPropertyChanged(nameof(PreviewImage));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasNoPreview));
        OnPropertyChanged(nameof(PreviewName));
        OnPropertyChanged(nameof(PreviewDimensions));
    }

    private void OnPendingItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (PendingImageItem item in e.NewItems)
            {
                item.PropertyChanged += OnPendingItemPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (PendingImageItem item in e.OldItems)
            {
                item.PropertyChanged -= OnPendingItemPropertyChanged;
                if (ReferenceEquals(item, SelectedItem))
                {
                    SelectedItem = null;
                }
            }
        }

        OnPropertyChanged(nameof(HasPendingItems));
        OnPropertyChanged(nameof(HasNoPendingItems));
        OnPropertyChanged(nameof(CanOpenPending));
        OpenPendingCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(OpenPendingButtonText));
    }

    private void OnPendingItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PendingImageItem.IsSelected))
        {
            return;
        }

        OnPropertyChanged(nameof(CanOpenPending));
        OpenPendingCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(OpenPendingButtonText));
    }
}
