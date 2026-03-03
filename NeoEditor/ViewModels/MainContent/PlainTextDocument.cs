using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

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

public partial class PlainTextDocument : DocumentBase
{
    [ObservableProperty] public partial string Content { get; set; } = "";
}

public partial class ImageDocument : DocumentBase
{
    [ObservableProperty] public partial string ImagePath { get; set; } = "";
}