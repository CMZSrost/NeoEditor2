using System;
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

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var r in rows) Entities.Add(r);
                Console.WriteLine($"[DB] LoadEntities done: Entities.Count={Entities.Count}");
            });
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

    public BrowserEntityRow(Data.Model.Game.IEntity entity)
    {
        Entity = entity;
        EntityId = entity.EntityId;
        TypeName = entity.GetType().Name;
        DisplayName = ResolveDisplayName(entity);
    }

    private static string ResolveDisplayName(Data.Model.Game.IEntity entity)
    {
        var type = entity.GetType();
        foreach (var name in new[] { "strName", "Name", "strLabel", "strTitle", "PropertyName", "strPropertyName" })
        {
            var prop = type.GetProperty(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.GetValue(entity) is string s && s.Length > 0)
                return s;
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
