using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

public enum PendingImageKind
{
    Imported,
    AiGenerated,
}

/// <summary>One entry in the create-image document's pending list (multi-select → open
/// each selected item in an editor document).</summary>
public partial class PendingImageItem : ObservableObject
{
    public required string Path { get; init; }
    public required string DisplayName { get; init; }
    public required PendingImageKind Kind { get; init; }

    [ObservableProperty] public partial bool IsSelected { get; set; }

    /// <summary>AI candidates live in a temp staging directory (cleanup is lazy — staged
    /// files are removed when deselected or on the next app start).</summary>
    public bool IsAiGenerated => Kind == PendingImageKind.AiGenerated;
}
