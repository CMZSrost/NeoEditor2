using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.XmlDiffPatch;
using NeoEditor.Helper;

namespace NeoEditor.ViewModels.MainContent;

public interface IDocumentBase
{
    public string Title { get; set; }
    public bool CanClose { get; set; }
    public bool NeedNotifyWhenClose { get; set; }
}

public abstract partial class DocumentBase : ObservableObject, IDocumentBase
{
    [ObservableProperty] public partial string Title { get; set; } = "Untitled";
    [ObservableProperty] public partial bool CanClose { get; set; } = true;
    [ObservableProperty] public partial bool NeedNotifyWhenClose { get; set; }
}

public abstract partial class DocumentViewBase : ViewModelBase, IDocumentBase
{
    [ObservableProperty] public partial string Title { get; set; } = "Untitled";
    [ObservableProperty] public partial bool CanClose { get; set; } = true;
    [ObservableProperty] public partial bool NeedNotifyWhenClose { get; set; }
}
public partial class XmlDocument : DocumentBase
{
    [ObservableProperty] public partial string XmlPath { get; set; }
    [ObservableProperty] public partial TextDocument Xml { get; set; }

    public XmlDocument(string xmlPath) : base()
    {
        XmlPath = Path.GetFullPath(xmlPath);
        Xml = new TextDocument
        {
            Text = System.IO.File.ReadAllText(XmlPath)
        };
    }
}

public partial class XmlDiffDocument : DocumentBase
{
    [ObservableProperty] public partial TextDocument OldXml { get; set; }
    [ObservableProperty] public partial TextDocument NewXml { get; set; }

    public XmlDiffDocument(string oldPath, string newPath) : base()
    {
        OldXml = new TextDocument
        {
            Text = System.IO.File.ReadAllText(oldPath)
        };
        NewXml = new TextDocument
        {
            Text = XmlCompareHelper.Compare(oldPath, newPath)
        };
    }
}

public partial class PlainTextDocument : DocumentBase
{
    [ObservableProperty] public partial string Content { get; set; } = "";
}

public partial class ImageDocument : DocumentBase
{
    [ObservableProperty] public partial string ImagePath { get; set; } = "";
}