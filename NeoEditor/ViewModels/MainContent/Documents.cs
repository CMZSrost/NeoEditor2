using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using LiveMarkdown.Avalonia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model;
using NeoEditor.Helper;

namespace NeoEditor.ViewModels.MainContent;

public interface IDocumentBase
{
    public string Title { get; set; }
    public bool CanClose { get; set; }
    public bool NeedNotifyWhenClose { get; set; }
    public void SetStaticTitle(string title);
    public void SetLocalizedTitle(string key, params object[] args);
    public void RefreshLocalizedText();
}

file static class DocumentTitleLocalization
{
    public static string Format(string key, params object[] args)
    {
        return App.Localizor[key, args];
    }
}

public abstract partial class DocumentBase : ObservableObject, IDocumentBase
{
    private string _title = string.Empty;
    private string? _localizedTitleKey;
    private object[] _localizedTitleArguments = [];

    protected DocumentBase()
    {
        SetLocalizedTitle("Untitled");
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    [ObservableProperty] public partial bool CanClose { get; set; } = true;
    [ObservableProperty] public partial bool NeedNotifyWhenClose { get; set; }

    public void SetStaticTitle(string title)
    {
        _localizedTitleKey = null;
        _localizedTitleArguments = Array.Empty<object>();
        Title = title;
    }

    public void SetLocalizedTitle(string key, params object[] args)
    {
        _localizedTitleKey = key;
        _localizedTitleArguments = CloneArguments(args);
        Title = DocumentTitleLocalization.Format(key, _localizedTitleArguments);
    }

    public virtual void RefreshLocalizedText()
    {
        if (!string.IsNullOrWhiteSpace(_localizedTitleKey))
        {
            Title = DocumentTitleLocalization.Format(_localizedTitleKey, _localizedTitleArguments);
        }
    }

    private static object[] CloneArguments(object[] args)
    {
        return args.Length == 0 ? Array.Empty<object>() : (object[])args.Clone();
    }
}

public abstract partial class DocumentViewBase : ViewModelBase, IDocumentBase
{
    private string _title = string.Empty;
    private string? _localizedTitleKey;
    private object[] _localizedTitleArguments = [];

    protected DocumentViewBase()
    {
        SetLocalizedTitle("Untitled");
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    [ObservableProperty] public partial bool CanClose { get; set; } = true;
    [ObservableProperty] public partial bool NeedNotifyWhenClose { get; set; }

    public void SetStaticTitle(string title)
    {
        _localizedTitleKey = null;
        _localizedTitleArguments = Array.Empty<object>();
        Title = title;
    }

    public void SetLocalizedTitle(string key, params object[] args)
    {
        _localizedTitleKey = key;
        _localizedTitleArguments = CloneArguments(args);
        Title = DocumentTitleLocalization.Format(key, _localizedTitleArguments);
    }

    public virtual void RefreshLocalizedText()
    {
        if (!string.IsNullOrWhiteSpace(_localizedTitleKey))
        {
            Title = DocumentTitleLocalization.Format(_localizedTitleKey, _localizedTitleArguments);
        }
    }

    private static object[] CloneArguments(object[] args)
    {
        return args.Length == 0 ? Array.Empty<object>() : (object[])args.Clone();
    }
}

public partial class XmlDocument : DocumentBase
{
    [ObservableProperty] public partial string XmlPath { get; set; }
    [ObservableProperty] public partial TextDocument Xml { get; set; }

    public XmlDocument(string xmlPath)
    {
        XmlPath = Path.GetFullPath(xmlPath);
        Xml = new TextDocument
        {
            Text = File.ReadAllText(XmlPath)
        };
    }
}

public partial class XmlDiffDocument : DocumentBase
{
    [ObservableProperty] public partial TextDocument OldXml { get; set; }
    [ObservableProperty] public partial TextDocument NewXml { get; set; }

    public XmlDiffDocument(string oldPath, string newPath)
    {
        OldXml = new TextDocument
        {
            Text = File.ReadAllText(oldPath)
        };
        NewXml = new TextDocument
        {
            Text = XmlCompareHelper.Compare(oldPath, newPath)
        };
    }
}

public partial class ModGameDataDocument : DocumentBase
{
    [ObservableProperty] public partial ModInfo? ModInfo { get; set; }
    [ObservableProperty] public partial bool ReadOnly { get; set; } = false;
    [ObservableProperty] public partial bool IsDirty { get; set; }
}

public partial class MergeEditorDocument : DocumentBase
{
    [ObservableProperty] public partial ProfileInfo? ProfileInfo { get; set; }
    [ObservableProperty] public partial bool IsDirty { get; set; }
}

public class PlainTextDocument : DocumentBase
{
    private string _content = string.Empty;
    private string? _localizedContentKey;
    private object[] _localizedContentArguments = [];

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public void SetStaticContent(string content)
    {
        _localizedContentKey = null;
        _localizedContentArguments = Array.Empty<object>();
        Content = content;
    }

    public void SetLocalizedContent(string key, params object[] args)
    {
        _localizedContentKey = key;
        _localizedContentArguments = args.Length == 0 ? Array.Empty<object>() : (object[])args.Clone();
        Content = DocumentTitleLocalization.Format(key, _localizedContentArguments);
    }

    public override void RefreshLocalizedText()
    {
        base.RefreshLocalizedText();
        if (!string.IsNullOrWhiteSpace(_localizedContentKey))
        {
            Content = DocumentTitleLocalization.Format(_localizedContentKey, _localizedContentArguments);
        }
    }
}

public partial class MarkdownDocument : DocumentViewBase
{
    private static readonly Regex MarkdownImageRegex = new(
        @"!\[(?<alt>[^\]]*)\]\((?<target>[^\r\n\)]*)\)",
        RegexOptions.Compiled);

    private static readonly Regex HtmlImageSrcRegex = new(
        """
        (<img\b[^>]*\bsrc\s*=\s*['\"])(?<src>[^'\"]+)(['\"][^>]*>)"+
        """,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [ObservableProperty] public partial string FilePath { get; set; }
    [ObservableProperty] public partial string Content { get; set; }
    public string BaseDirectory => Path.GetDirectoryName(FilePath) ?? "";
    public LiveMarkdown.Avalonia.ObservableStringBuilder MarkdownBuilder { get; }
    public System.Windows.Input.ICommand LinkCommand { get; }

    public MarkdownDocument(string filePath, string title)
    {
        FilePath = Path.GetFullPath(filePath);
        SetStaticTitle(title);
        var raw = File.ReadAllText(FilePath);
        Serilog.Log.Logger.Debug("[MarkdownDocument] Read {Length} chars from {Path}", raw.Length, FilePath);
        var prepared = PrepareMarkdownContent(raw, BaseDirectory);
        Content = prepared;
        MarkdownBuilder = new LiveMarkdown.Avalonia.ObservableStringBuilder();
        MarkdownBuilder.Append(prepared);

        LinkCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<LinkClickedEventArgs>(HandleLinkClick);
    }

    private void HandleLinkClick(LinkClickedEventArgs? e)
    {
        if (e.HRef is null) return;
        var url = e.HRef.ToString();

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }

        string localPath;
        if (url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            localPath = new Uri(url).LocalPath;
        else if (Path.IsPathRooted(url))
            localPath = Path.GetFullPath(url);
        else
            localPath = Path.GetFullPath(Path.Combine(BaseDirectory, url));

        if (localPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && File.Exists(localPath))
        {
            var docTitle = Path.GetFileNameWithoutExtension(localPath);
            Messenger.Send(new Data.Messages.OpenHelpDocumentMessage(localPath, docTitle));
        }
    }

    private static string PrepareMarkdownContent(string content, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return content;
        }

        var withImages = MarkdownImageRegex.Replace(content, match => RewriteMarkdownImage(match, baseDirectory));
        return HtmlImageSrcRegex.Replace(withImages, match => RewriteHtmlImage(match, baseDirectory));
    }

    private static string RewriteMarkdownImage(Match match, string baseDirectory)
    {
        var rawTarget = match.Groups["target"].Value.Trim();
        if (!TrySplitMarkdownTarget(rawTarget, out var imagePath, out var suffix))
        {
            return match.Value;
        }

        var resolvedUri = ResolveLocalFileUri(imagePath, baseDirectory);
        return resolvedUri is null
            ? match.Value
            : $"![{match.Groups["alt"].Value}]({resolvedUri}{suffix})";
    }

    private static string RewriteHtmlImage(Match match, string baseDirectory)
    {
        var source = match.Groups["src"].Value.Trim();
        var resolvedUri = ResolveLocalFileUri(source, baseDirectory);
        return resolvedUri is null
            ? match.Value
            : $"{match.Groups[1].Value}{resolvedUri}{match.Groups[3].Value}";
    }

    private static bool TrySplitMarkdownTarget(string rawTarget, out string imagePath, out string suffix)
    {
        imagePath = string.Empty;
        suffix = string.Empty;
        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            return false;
        }

        if (rawTarget[0] == '<')
        {
            var closingBracketIndex = rawTarget.IndexOf('>');
            if (closingBracketIndex <= 0)
            {
                return false;
            }

            imagePath = rawTarget[1..closingBracketIndex].Trim();
            suffix = rawTarget[(closingBracketIndex + 1)..];
            return !string.IsNullOrWhiteSpace(imagePath);
        }

        var separatorIndex = rawTarget.IndexOfAny([' ', '\t']);
        if (separatorIndex < 0)
        {
            imagePath = rawTarget;
            return true;
        }

        imagePath = rawTarget[..separatorIndex].Trim();
        suffix = rawTarget[separatorIndex..];
        return !string.IsNullOrWhiteSpace(imagePath);
    }

    private static string? ResolveLocalFileUri(string imagePath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return null;
        }

        var normalizedPath = imagePath.Trim();
        if (normalizedPath.StartsWith('#') ||
            normalizedPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("resm:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.IsFile ? absoluteUri.AbsoluteUri : null;
        }

        var combinedPath = normalizedPath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.IsPathRooted(combinedPath)
            ? Path.GetFullPath(combinedPath)
            : Path.GetFullPath(Path.Combine(baseDirectory, combinedPath));

        return File.Exists(fullPath) ? new Uri(fullPath).AbsoluteUri : null;
    }
}


public partial class ImageDocument : DocumentBase
{
    [ObservableProperty] public partial string ImagePath { get; set; } = "";
    [ObservableProperty] public partial Avalonia.Media.IImage? ImageSource { get; set; }

    partial void OnImagePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && System.IO.File.Exists(value))
            ImageSource = new Avalonia.Media.Imaging.Bitmap(value);
    }
}

/// <summary>
/// Read-only entity browser tab. Left: entity list. Right: tabbed visual overviews.
/// </summary>
public partial class EntityBrowserDocument : DocumentViewBase
{
    /// <summary>Independent Factory for the nested DockControl (must NOT share the DI singleton).</summary>
    [ObservableProperty] public partial Factory DockFactory { get; set; }

    public Helper.EntityTypeGroup EntityType { get; }

    /// <summary>All entities of this type (loaded on open).</summary>
    public System.Collections.ObjectModel.ObservableCollection<BrowserEntityRow> Entities { get; } = [];

    /// <summary>Entity viewer tabs in the right panel.</summary>
    public System.Collections.ObjectModel.ObservableCollection<EntityViewerDocument> ViewerTabs { get; } = [];
    [ObservableProperty] public partial EntityViewerDocument? SelectedViewerTab { get; set; }

    private readonly System.Collections.Generic.List<BrowserEntityRow> _allEntities = [];

    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _allEntities
            : _allEntities.Where(e =>
                (e.DisplayName ?? "").Contains(FilterText, System.StringComparison.OrdinalIgnoreCase)
                || (e.EntityId ?? "").Contains(FilterText, System.StringComparison.OrdinalIgnoreCase)).ToList();
        Entities.Clear();
        foreach (var e in filtered) Entities.Add(e);
    }

    // ═══════════════ Reference Index (via EntityMergeStore) ═══════════════

    private static bool _indexBuilt;
    private static System.Threading.Tasks.Task? _indexBuildTask;
    private static readonly object _indexBuildLock = new();

    /// <summary>Mark index as stale so next browser open or manual refresh rebuilds it.</summary>
    public static void InvalidateIndex()
    {
        lock (_indexBuildLock)
        {
            _indexBuilt = false;
            _indexBuildTask = null;
        }
        GenericDataGridHelper.BrowserStore = null;
        GlobalBrowserCache.Clear();
        GlobalModNames.Clear();
        try { if (System.IO.File.Exists(CachePath)) System.IO.File.Delete(CachePath); }
        catch { /* ignore */ }
        try { if (System.IO.File.Exists(IndexCachePath)) System.IO.File.Delete(IndexCachePath); }
        catch { /* ignore */ }
    }

    /// <summary>Ensure the reference index is built, waiting if a build is in progress.
    /// Thread-safe: only one build can be in flight at a time; subsequent callers
    /// either get the in-flight task or return immediately if already built.</summary>
    public static System.Threading.Tasks.Task EnsureIndexBuiltAsync()
    {
        // Fast path: already built — no lock needed (boolean read is atomic in .NET)
        if (_indexBuilt) return System.Threading.Tasks.Task.CompletedTask;

        lock (_indexBuildLock)
        {
            // Re-check under lock
            if (_indexBuilt) return System.Threading.Tasks.Task.CompletedTask;

            // If a build is already in flight, return the same task
            if (_indexBuildTask is { IsCompleted: false })
                return _indexBuildTask;

            // Start a new build
            _indexBuildTask = RebuildBrowserIndexAsync();
            return _indexBuildTask;
        }
    }

    private static string CachePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeoEditor", "browser_index_cache.json");
    private static string IndexCachePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeoEditor", "browser_reference_index.json");

    /// <summary>Global browser lookup cache. Maps entity type → (key → cache entry).
    /// Populated from DB on first build, persisted to disk, loaded on next startup.</summary>
    public static Dictionary<Type, Dictionary<int, BrowserIndexCacheEntry>> GlobalBrowserCache { get; } = new();
    public static Dictionary<string, string> GlobalModNames { get; } = new();

    /// <summary>Populate the reference index from the database for all entity types.
    /// ReferenceIndex is persisted to disk — skips expensive BuildAsync on next startup.
    /// Only ONE instance should run at a time (enforced by EnsureIndexBuiltAsync lock).</summary>
    public static async System.Threading.Tasks.Task RebuildBrowserIndexAsync()
    {
        try
        {
            Serilog.Log.Logger.Information("[BrowserIndex] Rebuild started");

            // Invalidate old disk caches (MergedId scheme changed)
            try { System.IO.File.Delete(CachePath); } catch { /* ignore */ }
            try { System.IO.File.Delete(IndexCachePath); } catch { /* ignore */ }

            // 1. Load entities from DB (always needed for ReferenceLookups)
            await using var db = await App.ServiceProvider!
                .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Data.Context.GameDbContext>>()
                .CreateDbContextAsync();
            await using var editorDb = await App.ServiceProvider!
                .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Data.Context.EditorDbContext>>()
                .CreateDbContextAsync();

            var modNames = new Dictionary<int, string> { [-1] = "Game" };
            var modNsNames = new Dictionary<int, string> { [-1] = "0" }; // base game namespace
            foreach (var mi in editorDb.ModInfos)
            {
                modNames[mi.ModId] = mi.Name;
                // Use mod directory name as namespace fallback (strModName not stored in ModInfo yet)
                modNsNames[mi.ModId] = mi.Name;
            }

            var store = new Services.EntityMergeStore();
            GlobalBrowserCache.Clear();
            GlobalModNames.Clear();

            var entityTypes = typeof(Data.Context.GameDbContext).GetProperties()
                .Where(p => p.PropertyType.IsGenericType
                    && p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>))
                .Select(p => p.PropertyType.GetGenericArguments()[0])
                .Where(t => typeof(Data.Model.Game.IEntity).IsAssignableFrom(t))
                .ToList();

            foreach (var eType in entityTypes)
            {
                var m = typeof(Data.Context.GameDbContext).GetMethod(nameof(Data.Context.GameDbContext.Set),
                    Type.EmptyTypes)!.MakeGenericMethod(eType);
                var dbSet = (System.Collections.IEnumerable)m.Invoke(db, null)!;
                var items = new List<object>();
                var keyProp = eType.GetProperty("Id") ?? eType.GetProperty("nID");
                foreach (var obj in dbSet)
                {
                    if (obj is not Data.Model.Game.IEntity entity) continue;
                    items.Add(entity);
                    modNames.TryGetValue(entity.ModId, out var modName);
                    var mn = modName ?? $"mod_{entity.ModId}";
                    store.EntityModNames[entity.EntityId] = mn;
                    GlobalModNames[entity.EntityId] = mn;

                    modNsNames.TryGetValue(entity.ModId, out var nsName);
                    store.EntityNamespaces[entity.EntityId] = nsName ?? mn;
                }
                store.ReferenceLookups[eType] = items;

                // Compute MergedIds CONSISTENTLY with MergeService:
                //   merge space (ns="0") → MergedId = primary key
                //   insert space (ns≠"0") → MergedId = sequential from max merge key + 1
                bool InMergeSpace(Data.Model.Game.IEntity e) =>
                    store.EntityNamespaces.TryGetValue(e.EntityId, out var ns) && ns == "0";
                var maxMergeKey = items.OfType<Data.Model.Game.IEntity>()
                    .Where(InMergeSpace)
                    .Select(e => keyProp?.GetValue(e)).OfType<int>()
                    .DefaultIfEmpty(0).Max();
                var nextInsertId = maxMergeKey + 1;

                var typeCache = new Dictionary<int, BrowserIndexCacheEntry>();
                foreach (var obj in items)
                {
                    if (obj is not Data.Model.Game.IEntity entity) continue;
                    if (keyProp?.GetValue(entity) is int k)
                    {
                        int mergedId;
                        if (InMergeSpace(entity))
                            mergedId = k;
                        else
                            mergedId = nextInsertId++;
                        store.EntityMergedIds[entity.EntityId] = mergedId;
                        typeCache[mergedId] = new BrowserIndexCacheEntry(entity.EntityId, entity.ModId, k, entity.Subject);
                    }
                }
                GlobalBrowserCache[eType] = typeCache;
            }

            store.NamespaceToModName["0"] = "Game";

            // 2. Try loading ReferenceIndex from disk — skip expensive BuildAsync if cached
            if (store.Index.TryLoadFromDisk(IndexCachePath))
            {
                // If merged fallback is empty but the rest has data, force rebuild
                if (store.Index.MergedFallbackCount == 0)
                {
                    Serilog.Log.Logger.Warning(
                        "[BrowserIndex] Disk cache has empty MergedFallback — forcing rebuild");
                    try { System.IO.File.Delete(IndexCachePath); } catch { /* ignore */ }
                }
                else
                {
                    Console.WriteLine($"[BrowserIndex] ReferenceIndex loaded from disk: {store.ReferenceLookups.Count} types");
                    GenericDataGridHelper.BrowserStore = store;
                    _indexBuilt = true;
                    // 3. Save lightweight cache (backward compat)
                    SaveToDiskCache();
                    Serilog.Log.Logger.Information("[BrowserIndex] Rebuild complete (from disk)");
                    return;
                }
            }

            await store.Index.BuildAsync();
            Serilog.Log.Logger.Information(
                "[BrowserIndex] BuildAsync done — mergedIdIdx={MergedIdIdx} totalEntities={TotalEnt}",
                store.Index.MergedFallbackCount,
                store.ReferenceLookups.Sum(kv => kv.Value.Count));
            store.Index.SaveToDisk(IndexCachePath);

            GenericDataGridHelper.BrowserStore = store;
            _indexBuilt = true;

            // 3. Save lightweight cache (backward compat)
            SaveToDiskCache();
            Serilog.Log.Logger.Information("[BrowserIndex] Rebuild complete (full build)");
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Logger.Error(ex, "[BrowserIndex] Rebuild FAILED");
            // Do NOT set _indexBuilt = true on failure — next caller will retry
        }
    }

    private static bool TryLoadFromDiskCache()
    {
        try
        {
            if (!System.IO.File.Exists(CachePath)) return false;
            var json = System.IO.File.ReadAllText(CachePath);
            var data = System.Text.Json.JsonSerializer.Deserialize<BrowserIndexCacheData>(json);
            if (data?.Types is null) return false;

            GlobalBrowserCache.Clear();
            GlobalModNames.Clear();
            if (data.EntityModNames is not null)
                foreach (var (k, v) in data.EntityModNames) GlobalModNames[k] = v;

            foreach (var (typeName, entries) in data.Types)
            {
                var type = Type.GetType(typeName);
                if (type is null) continue;
                var dict = new Dictionary<int, BrowserIndexCacheEntry>();
                foreach (var e in entries)
                    dict[e.K] = e;
                GlobalBrowserCache[type] = dict;
            }

            Console.WriteLine($"[BrowserIndex] Loaded from disk: {GlobalBrowserCache.Count} types, {GlobalBrowserCache.Sum(kv => kv.Value.Count)} entities");
            return GlobalBrowserCache.Count > 0;
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[BrowserIndex] Disk load failed: {ex.Message}");
            return false;
        }
    }

    private static void SaveToDiskCache()
    {
        try
        {
            var data = new BrowserIndexCacheData
            {
                Types = GlobalBrowserCache.ToDictionary(
                    kv => kv.Key.FullName!,
                    kv => kv.Value.Values.ToList()),
                EntityModNames = new Dictionary<string, string>(GlobalModNames)
            };
            var dir = System.IO.Path.GetDirectoryName(CachePath)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(CachePath, System.Text.Json.JsonSerializer.Serialize(data));
            Console.WriteLine($"[BrowserIndex] Cache saved to {CachePath}");
        }
        catch (System.Exception ex) { Console.WriteLine($"[BrowserIndex] Cache save failed: {ex.Message}"); }
    }

    // ── Cache data types ──

    public class BrowserIndexCacheEntry
    {
        public string Eid { get; set; } = "";
        public int Mid { get; set; }
        public int K { get; set; }
        public string Sub { get; set; } = "";
        public BrowserIndexCacheEntry() { }
        public BrowserIndexCacheEntry(string entityId, int modId, int key, string subject)
        { Eid = entityId; Mid = modId; K = key; Sub = subject; }
    }

    private class BrowserIndexCacheData
    {
        public Dictionary<string, List<BrowserIndexCacheEntry>> Types { get; set; } = new();
        public Dictionary<string, string>? EntityModNames { get; set; }
    }

    public EntityBrowserDocument(Helper.EntityTypeGroup entityType)
    {
        EntityType = entityType;
        DockFactory = new Factory();
        Helper.AsyncHelper.FireAndForget(LoadEntitiesAsync());
    }

    private async System.Threading.Tasks.Task LoadEntitiesAsync()
    {
        try
        {
            await using var db = await App.ServiceProvider!
                .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Data.Context.GameDbContext>>()
                .CreateDbContextAsync();

            var m = typeof(Data.Context.GameDbContext).GetMethod(nameof(Data.Context.GameDbContext.Set),
                System.Type.EmptyTypes)!.MakeGenericMethod(EntityType.EntityType);
            var dbSet = (System.Collections.IEnumerable)m.Invoke(db, null)!;

            var rows = new System.Collections.Generic.List<BrowserEntityRow>();
            int rawCount = 0;
            foreach (var obj in dbSet)
            {
                rawCount++;
                if (obj is Data.Model.Game.IEntity e)
                    rows.Add(new BrowserEntityRow(e));
            }

            Console.WriteLine($"[DB] LoadEntities: type={EntityType.EntityType.Name}, rawCount={rawCount}, matched={rows.Count}");

            _allEntities.AddRange(rows);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var r in rows) Entities.Add(r);
                Console.WriteLine($"[DB] LoadEntities done: Entities.Count={Entities.Count}");
            });

            await EnsureIndexBuiltAsync();
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"[DB] LoadEntities FAILED: {ex.Message}");
        }
    }
}

/// <summary>Lightweight row for entity list display.</summary>
public partial class BrowserEntityRow : ObservableObject
{
    public Data.Model.Game.IEntity Entity { get; }
    public string DisplayName { get; }
    public string EntityId { get; }
    public string TypeName { get; }
    public string ModBadge { get; }

    public BrowserEntityRow(Data.Model.Game.IEntity entity)
    {
        Entity = entity;
        EntityId = entity.EntityId;
        TypeName = entity.GetType().Name;
        DisplayName = ResolveDisplayName(entity);

        // Format: "mid:modName" e.g. "-1:Game" or "5:NSE"
        var modName = EntityBrowserDocument.GlobalModNames.TryGetValue(entity.EntityId, out var mn)
            ? mn : $"mod_{entity.ModId}";
        ModBadge = $"{entity.ModId}:{modName}";
    }

    private static string ResolveDisplayName(Data.Model.Game.IEntity entity)
    {
        // Headline: "News #N: first 10 chars..."
        if (entity is Data.Model.Game.Headline hl && !string.IsNullOrWhiteSpace(hl.HeadlineText))
        {
            var p = hl.HeadlineText.Length > 10 ? hl.HeadlineText[..10] : hl.HeadlineText;
            return $"News #{hl.Id}: {p}";
        }
        // DataFile: "Name: first 10 chars..."
        if (entity is Data.Model.Game.DataFile df && !string.IsNullOrWhiteSpace(df.Description))
        {
            var p = df.Description.Length > 10 ? df.Description[..10] : df.Description;
            return $"{df.Name}: {p}";
        }

        var type = entity.GetType();
        foreach (var name in new[] { "strName", "Name", "strLabel", "strTitle", "PropertyName", "strPropertyName", "Description" })
        {
            var prop = type.GetProperty(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.GetValue(entity) is string s && s.Length > 0)
                return s.Length > 50 ? s[..47] + "..." : s;
        }
        var indexAttr = type.GetCustomAttribute<Microsoft.EntityFrameworkCore.IndexAttribute>();
        var keyName = indexAttr?.PropertyNames?.FirstOrDefault(n => n != nameof(EntityId));
        if (keyName is not null)
        {
            var keyProp = type.GetProperty(keyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (keyProp?.GetValue(entity) is { } kv)
                return $"[{type.Name}] #{kv}";
        }
        return $"[{type.Name}]";
    }
}

/// <summary>A single-entity Dock document opened from the Data Browser.</summary>
public partial class EntityViewerDocument : DocumentViewBase
{
    public Data.Model.Game.IEntity Entity { get; }
    public EntityViewerDocument(Data.Model.Game.IEntity entity)
    {
        Entity = entity;
        Title = entity.Subject ?? entity.GetType().Name;
    }
}

/// <summary>A single tab in the DomainBrowser's right-side entity viewer.</summary>
public partial class EntityViewerTab : ObservableObject
{
    public string Header { get; init; } = "";
    public Data.Model.Game.IEntity Entity { get; init; } = null!;
}

// ── Tool classes for ToolDock panels ────────────────────────────────

/// <summary>Left ToolDock: overlay chain display.</summary>
public class OverlayChainTool : Tool
{
    public OverlayChainTool(OverlayChainToolContent content)
    {
        Id = "OverlayChain";
        Title = "Overlay Chain";
        Context = content;
        Proportion = 1.0;
    }
}

/// <summary>Right: value editor panel.</summary>
public class ValueEditorTool : Tool
{
    public ValueEditorTool()
    {
        Id = "ValueEditor";
        Title = "Value Editor";
        Proportion = 1.0;
    }
}

/// <summary>Right: image preview.</summary>
public class ImagePreviewTool : Tool
{
    public ImagePreviewTool(ImagePreviewContent content)
    {
        Id = "ImagePreview";
        Title = "Image Preview";
        Context = content;
        Proportion = 1.0;
    }
}

/// <summary>Right: reference inspector.</summary>
public class ReferenceInspectorTool : Tool
{
    public ReferenceInspectorTool(ReferenceInspectorContent content)
    {
        Id = "RefInspector";
        Title = "Reference Inspector";
        Context = content;
        Proportion = 1.0;
    }
}

/// <summary>Bottom: search results.</summary>
public class SearchResultsTool : Tool
{
    public SearchResultsTool(BottomToolsViewModel content)
    {
        Id = "SearchResults";
        Title = "Search Results";
        Context = content;
        Proportion = 1.0;
    }
}

/// <summary>Bottom: conflicts.</summary>
public class ConflictsTool : Tool
{
    public ConflictsTool(BottomToolsViewModel content)
    {
        Id = "Conflicts";
        Title = "Conflicts";
        Context = content;
        Proportion = 1.0;
    }
}

/// <summary>Bottom: validation.</summary>
public class ValidationTool : Tool
{
    public ValidationTool(BottomToolsViewModel content)
    {
        Id = "Validation";
        Title = "Validation";
        Context = content;
        Proportion = 1.0;
    }
}
