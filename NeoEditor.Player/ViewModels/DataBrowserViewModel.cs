using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveMarkdown.Avalonia;
using NeoEditor.Player.Core.Data;
using NeoEditor.Player.Services;

namespace NeoEditor.Player.ViewModels;

/// <summary>One gallery image for the carousel — decoded on the UI thread (v2.28).</summary>
public sealed record ImageItem(Bitmap? Image, string FileName)
{
    public bool Exists => Image is not null;

    public string DisplayText => Exists
        ? FileName
        : string.Format(LocalizationManager.Instance["Gallery.Missing"], FileName);
}

/// <summary>
/// One reference tab (v2.32): a source table with its markdown line list; carries the
/// shared link command so db:// links inside the tab navigate the browser.
/// </summary>
public sealed class ReferenceTab
{
    public ReferenceTab(string tableName, string markdown, ICommand linkCommand)
    {
        TableName = tableName;
        LinkCommand = linkCommand;
        Markdown = new ObservableStringBuilder();
        Markdown.Append(markdown);
    }

    public string TableName { get; }
    public ICommand LinkCommand { get; }
    public ObservableStringBuilder Markdown { get; }
}

/// <summary>
/// Data browser view model (Docs/42 v2.15 + v2.22): the merged catalog grouped by entity
/// table (the 24 data classes) — base data/*.xml overlaid by Mods/*/neogame.xml, same merge
/// semantics the game applies at load. Three master-master-detail panes: table list →
/// row list → wiki-style detail page (LiveMarkdown), with db://table/key links
/// navigating the browser itself.
/// </summary>
public sealed partial class DataBrowserViewModel : ObservableObject
{
    private readonly DataBrowserService _service;
    private GameDataCatalog? _catalog;
    private WikiDetailBuilder? _wiki;

    public ObservableCollection<string> Tables { get; } = [];

    [ObservableProperty] private string? _selectedTable;
    [ObservableProperty] private ObservableCollection<GameDataRow> _rows = [];
    [ObservableProperty] private GameDataRow? _selectedRow;
    [ObservableProperty] private string _statusText = "";

    /// <summary>Detail body markdown (recipe card / loot tree / header — LiveMarkdown).</summary>
    public ObservableStringBuilder DetailMarkdown { get; } = new();

    /// <summary>Field table rows for the UI grid (v2.34 — multi-line values safe).</summary>
    public ObservableCollection<FieldItem> Fields { get; } = [];

    /// <summary>Gallery images for the carousel (v2.32).</summary>
    public ObservableCollection<ImageItem> Images { get; } = [];

    /// <summary>Incoming references grouped by source table — one tab each (v2.32).</summary>
    public ObservableCollection<ReferenceTab> ReferenceTabs { get; } = [];

    /// <summary>Whether the side pane sections have content (title visibility).</summary>
    [ObservableProperty] private bool _hasImages;
    [ObservableProperty] private bool _hasReferences;
    [ObservableProperty] private bool _hasFields;

    /// <summary>Carousel state (v2.32): the image currently shown and its position.</summary>
    [ObservableProperty] private ImageItem? _currentImage;
    [ObservableProperty] private int _currentImageIndex = -1;
    [ObservableProperty] private string _imageCounter = "";

    /// <summary>db://table/key links inside the detail page navigate the browser.</summary>
    public RelayCommand<LinkClickedEventArgs> LinkCommand { get; }

    partial void OnCurrentImageIndexChanged(int value)
    {
        CurrentImage = value >= 0 && value < Images.Count ? Images[value] : null;
        ImageCounter = Images.Count == 0 ? "" : $"{value + 1}/{Images.Count}";
    }

    public DataBrowserViewModel(DataBrowserService service)
    {
        _service = service;
        LinkCommand = new RelayCommand<LinkClickedEventArgs>(HandleLinkClick);
        StatusText = LocalizationManager.Instance["Db.Status.NotLoaded"];
    }

    /// <summary>Rebuild the merged catalog (call before showing the dialog).</summary>
    public void Refresh()
    {
        _catalog = _service.BuildCatalog();
        _wiki = new WikiDetailBuilder(_catalog, _service.GameRootDir);
        Tables.Clear();
        foreach (var table in _catalog.TableNames)
            Tables.Add(table);

        SelectedTable = Tables.FirstOrDefault();
        StatusText = _catalog.TableNames.Count == 0
            ? LocalizationManager.Instance["Db.Status.NoData"]
            : string.Format(LocalizationManager.Instance["Db.Status.Summary"],
                _catalog.TableNames.Count, _catalog.TotalRows);
    }

    /// <summary>
    /// Clear everything (game quit): stale data must never survive into the next SWF —
    /// the next open re-runs <see cref="Refresh"/> against the new game root.
    /// </summary>
    public void Reset()
    {
        _catalog = null;
        _wiki = null;
        Tables.Clear();
        Rows.Clear();
        SelectedTable = null;
        SelectedRow = null;
        DetailMarkdown.Clear();
        Fields.Clear();
        Images.Clear();
        ReferenceTabs.Clear();
        CurrentImageIndex = -1;
        HasImages = false;
        HasReferences = false;
        HasFields = false;
        StatusText = LocalizationManager.Instance["Db.Status.QuitReset"];
    }

    partial void OnSelectedTableChanged(string? value)
    {
        SelectedRow = null;
        Rows.Clear();
        if (value is null || _catalog is null) return;

        foreach (var row in _catalog.GetRows(value))
            Rows.Add(row);

        StatusText = string.Format(LocalizationManager.Instance["Db.Status.Rows"], value, Rows.Count);
    }

    partial void OnSelectedRowChanged(GameDataRow? value)
    {
        DetailMarkdown.Clear();
        Fields.Clear();
        Images.Clear();
        ReferenceTabs.Clear();
        CurrentImageIndex = -1;
        HasImages = false;
        HasReferences = false;
        HasFields = false;
        if (value is null || _wiki is null) return;

        DetailMarkdown.Append(_wiki.BuildDetail(value));

        foreach (var field in _wiki.GetFields(value))
            Fields.Add(field);
        HasFields = Fields.Count > 0;

        foreach (var group in _wiki.BuildReferenceGroups(value))
            ReferenceTabs.Add(new ReferenceTab(group.TableName, group.Markdown, LinkCommand));
        HasReferences = ReferenceTabs.Count > 0;

        foreach (var image in _wiki.GetImageItems(value))
            Images.Add(new ImageItem(Decode(image.FullPath), image.FileName));
        HasImages = Images.Count > 0;
        if (HasImages) CurrentImageIndex = 0;
    }

    /// <summary>Decode the image on the UI thread — Image.Source binds the Bitmap directly
    /// (string path bindings to Image.Source were unreliable under compiled bindings).</summary>
    private static Bitmap? Decode(string? path)
    {
        if (path is null) return null;
        try
        {
            return new Bitmap(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void HandleLinkClick(LinkClickedEventArgs? e) => NavigateTo(e?.HRef?.ToString());

    /// <summary>Navigate the browser to a db://table/key link (markdown links and the
    /// field grid's reference links both land here — v2.34).</summary>
    public void NavigateTo(string? url)
    {
        if (url is null || !url.StartsWith("db://", StringComparison.OrdinalIgnoreCase)) return;
        var rest = url["db://".Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0) return;
        var table = rest[..slash];
        var key = Uri.UnescapeDataString(rest[(slash + 1)..]);
        if (_catalog is null) return;

        SelectedTable = table;   // fills Rows synchronously (no-op when already selected)
        var row = _catalog.FindRow(table, key);
        SelectedRow = row ?? Rows.FirstOrDefault(r =>
            string.Equals(r.RowKey, key, StringComparison.OrdinalIgnoreCase));
        if (SelectedRow is null)
            StatusText = string.Format(LocalizationManager.Instance["Db.Status.NotFound"], table, key);
    }
}
