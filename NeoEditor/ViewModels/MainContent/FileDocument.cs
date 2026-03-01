using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.MainContent;

public partial class DocumentBase : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; } = "Untitled";
    [ObservableProperty] public partial string Content { get; set; } = "";
    [ObservableProperty] public partial bool CanClose { get; set; } = true;
}

public partial class FileDocument : DocumentBase
{
}

public partial class ImageDocument : DocumentBase
{
    [ObservableProperty] public partial string ImagePath { get; set; } = "";
}