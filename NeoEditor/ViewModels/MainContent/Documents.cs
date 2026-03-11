using System;
using System.IO;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
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
    [ObservableProperty] public partial bool ReadOnly { get; set; } = true;
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

    public MarkdownDocument(string filePath, string title)
    {
        FilePath = Path.GetFullPath(filePath);
        SetStaticTitle(title);
        Content = PrepareMarkdownContent(File.ReadAllText(FilePath), Path.GetDirectoryName(FilePath));
    }

    private static string PrepareMarkdownContent(string content, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(baseDirectory))
        {
            return content;
        }

        var rewrittenMarkdown = MarkdownImageRegex.Replace(content, match => RewriteMarkdownImage(match, baseDirectory));
        return HtmlImageSrcRegex.Replace(rewrittenMarkdown, match => RewriteHtmlImage(match, baseDirectory));
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
}