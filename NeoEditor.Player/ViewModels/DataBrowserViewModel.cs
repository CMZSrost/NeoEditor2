using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveMarkdown.Avalonia;
using NeoEditor.Player.Core.Data;
using NeoEditor.Player.Core.Services;
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
/// One entity table entry in the browser's table list (v2.72): the raw table key
/// ("itemtypes", drives data lookups) plus its localized display name (resx "Table.{key}").
/// </summary>
public sealed record TableItem(string Key, string Display)
{
    public override string ToString() => Display;
}

/// <summary>
/// One reference tab (v2.32): a source table with its markdown line list; carries the
/// shared link command so db:// links inside the tab navigate the browser.
/// v2.72: <see cref="DisplayName"/> = localized table name, <see cref="TableName"/> keeps
/// the raw key (tooltip).
/// </summary>
public sealed class ReferenceTab
{
    public ReferenceTab(string tableName, string markdown, ICommand linkCommand)
    {
        TableName = tableName;
        DisplayName = DataBrowserViewModel.TableDisplay(tableName);
        LinkCommand = linkCommand;
        Markdown = new ObservableStringBuilder();
        Markdown.Append(markdown);
    }

    /// <summary>Raw table key (e.g. "itemtypes") — tooltip for cross-referencing.</summary>
    public string TableName { get; }
    public string DisplayName { get; }
    public ICommand LinkCommand { get; }
    public ObservableStringBuilder Markdown { get; }
}

/// <summary>
/// Data browser view model (Docs/42 v2.15 + v2.22): the merged catalog grouped by entity
/// table (the 24 data classes) — base data/*.xml overlaid by Mods/*/neogame.xml, same merge
/// semantics the game applies at load. Three master-master-detail panes: table list →
/// row list → wiki-style detail page (LiveMarkdown), with db://table/key links
/// navigating the browser itself. v2.72: all labels / table names / field names go through
/// LocalizationManager (the editor's [Display] resx dictionary), and language switches
/// re-render the browser in place.
/// </summary>
public sealed partial class DataBrowserViewModel : ObservableObject
{
    private readonly DataBrowserService _service;
    private GameDataCatalog? _catalog;
    private WikiDetailBuilder? _wiki;

    /// <summary>R56: 图片诊断输出（宿主接 RunLogStore → 日志文件，便于定位缺失）。</summary>
    public Action<string>? LogAction { get; set; }

    public ObservableCollection<TableItem> Tables { get; } = [];

    [ObservableProperty] private TableItem? _selectedTableItem;
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
        StatusText = L("Db.Status.NotLoaded");
        // v2.72: 语言切换时在浏览器内就地重渲染（表名/行摘要/详情/字段/引用全部跟随）。
        LocalizationManager.Instance.PropertyChanged += (_, _) => Relocalize();
    }

    private static string L(string key) => LocalizationManager.Instance[key];

    /// <summary>Localized table display name (resx "Table.{key}", raw key fallback).</summary>
    public static string TableDisplay(string tableName)
    {
        var key = $"Table.{tableName}";
        var value = LocalizationManager.Instance[key];
        return value == key ? tableName : value;
    }

    /// <summary>Localized field label for (table, column) — raw column when untranslated.</summary>
    private static string? FieldLabel(string tableName, string column)
    {
        var displayKey = GameTableMap.GetFieldDisplayKey(tableName, column);
        if (displayKey is null) return null;
        var key = $"FieldName.{displayKey}";
        var value = LocalizationManager.Instance[key];
        return value == key ? null : value;
    }

    /// <summary>Rebuild the merged catalog (call before showing the dialog).</summary>
    public void Refresh()
    {
        // v2.72: 行摘要列名前缀本地化（ColumnLabel 取值时动态解析 → 语言切换即时生效）。
        _catalog = _service.BuildCatalog((table, column) => FieldLabel(table, column));
        _wiki = new WikiDetailBuilder(_catalog, _service.GameRootDir, key => LocalizationManager.Instance[key]);
        Tables.Clear();
        foreach (var table in _catalog.TableNames)
            Tables.Add(new TableItem(table, TableDisplay(table)));

        SelectedTableItem = Tables.FirstOrDefault();
        StatusText = _catalog.TableNames.Count == 0
            ? L("Db.Status.NoData")
            : string.Format(L("Db.Status.Summary"),
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
        SelectedTableItem = null;
        SelectedRow = null;
        DetailMarkdown.Clear();
        Fields.Clear();
        Images.Clear();
        ReferenceTabs.Clear();
        CurrentImageIndex = -1;
        HasImages = false;
        HasReferences = false;
        HasFields = false;
        StatusText = L("Db.Status.QuitReset");
    }

    /// <summary>
    /// v2.72: 语言切换 → 就地重渲染：表名/行摘要/详情 markdown/字段/引用全部按新语言重建
    /// （catalog 不重扫磁盘；ColumnLabel 与 builder 文本委托都在取值时解析当前语言）。
    /// </summary>
    private void Relocalize()
    {
        if (_catalog is null)
        {
            if (Tables.Count == 0 && Rows.Count == 0) return;
            StatusText = L("Db.Status.QuitReset");
            return;
        }
        var catalog = _catalog;

        var tableKey = SelectedTableItem?.Key;
        var rowKey = SelectedRow?.RowKey;

        // 1. 表列表显示名刷新（Replace 触发 UI 更新）
        for (var i = 0; i < Tables.Count; i++)
        {
            var t = Tables[i];
            Tables[i] = new TableItem(t.Key, TableDisplay(t.Key));
        }

        // 2. 重建行（摘要列名前缀按新语言）+ 状态文本
        SelectedTableItem = null;
        if (tableKey is not null)
            SelectedTableItem = Tables.FirstOrDefault(t => t.Key == tableKey);

        // 3. 重渲染详情（字段/图片/引用 Tab 全部重建）
        SelectedRow = null;
        if (rowKey is not null && tableKey is not null)
            SelectedRow = catalog.GetRows(tableKey).FirstOrDefault(r => r.RowKey == rowKey)
                ?? Rows.FirstOrDefault(r => r.RowKey == rowKey);

        StatusText = catalog.TableNames.Count == 0
            ? L("Db.Status.NoData")
            : string.Format(L("Db.Status.Summary"),
                catalog.TableNames.Count, catalog.TotalRows);
    }

    partial void OnSelectedTableItemChanged(TableItem? value)
    {
        SelectedRow = null;
        Rows.Clear();
        var table = value?.Key;
        if (table is null || _catalog is null) return;

        foreach (var row in _catalog.GetRows(table))
            Rows.Add(row);

        StatusText = string.Format(L("Db.Status.Rows"), value?.Display ?? table, Rows.Count);
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

        try
        {
            // R55: 整段构建包 try/catch——任何一条数据/一张图异常都不让播放器崩，
            // 状态行提示（浏览数据时闪退的防御；具体错误待实机定位）。
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

            // R56 诊断：图片缺失时把文件名 + 游戏根目录 + getmods.php 内容 + 各目录存在性
            // 写到状态行与日志文件（LogAction → RunLogStore），一次拿到定位所需全部信息。
            var missing = Images.Where(i => !i.Exists).Select(i => i.FileName).Take(3).ToList();
            if (missing.Count > 0)
            {
                var root = _service.GameRootDir ?? "";
                var modsPhp = Path.Combine(root, "getmods.php");
                var modsPhpHead = File.Exists(modsPhp)
                    ? (File.ReadAllText(modsPhp).Length > 300 ? File.ReadAllText(modsPhp)[..300] : File.ReadAllText(modsPhp))
                    : "(不存在)";
                var detail = $"图片缺失: {string.Join(", ", missing)} | gameRoot={root}" +
                    $" | img/={Directory.Exists(Path.Combine(root, "img"))}" +
                    $" | Mods/={Directory.Exists(Path.Combine(root, "Mods"))}" +
                    $" | getmods.php={modsPhpHead}";
                StatusText = detail;
                LogAction?.Invoke(detail);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"数据渲染失败: {ex.Message}";
        }
    }

    /// <summary>Decode the image on the UI thread — Image.Source binds the Bitmap directly
    /// (string path bindings to Image.Source were unreliable under compiled bindings).
    /// R55: DecodeToWidth 限制解码尺寸——大图（mod 高清图可达数千像素）全尺寸解码
    /// 内存爆炸会让进程直接退出（无托管异常可捕获）；512px 对预览/缩略图足够。</summary>
    private static Bitmap? Decode(string? path)
    {
        if (path is null) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, 512);
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

        SelectedTableItem = Tables.FirstOrDefault(t => t.Key == table);   // fills Rows synchronously (no-op when already selected)
        var row = _catalog.FindRow(table, key);
        SelectedRow = row ?? Rows.FirstOrDefault(r =>
            string.Equals(r.RowKey, key, StringComparison.OrdinalIgnoreCase));
        if (SelectedRow is null)
            StatusText = string.Format(L("Db.Status.NotFound"), TableDisplay(table), key);
    }
}
