using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.MainContent;

/// <summary>
/// Mod node in the Profile Tool tree — one entry from the active profile's ModLoadInfos.
/// Its XML children are scanned eagerly from the mod's content root (task 5).
/// </summary>
public sealed class ProfileModNode : ObservableObject
{
    public required string Name { get; init; }
    public required int ModId { get; init; }
    public required string Path { get; init; }
    public required string ContentRoot { get; init; }
    public bool IsGame { get; init; }

    public ObservableCollection<ProfileXmlNode> XmlNodes { get; } = [];
}

/// <summary>
/// XML file node under a mod. Its non-empty data-class children are loaded lazily
/// (per mod, cached) the first time the node is expanded.
/// </summary>
public sealed class ProfileXmlNode : ObservableObject
{
    public required string Name { get; init; }
    public required string AbsolutePath { get; init; }

    public bool TypesLoaded { get; set; }

    public ObservableCollection<ProfileDataTypeNode> TypeNodes { get; } = [];
}

/// <summary>Non-empty data class node under an XML file.</summary>
public sealed class ProfileDataTypeNode : ObservableObject
{
    public required string TypeName { get; init; }
    public required int Count { get; init; }

    public string DisplayName => $"{TypeName} ({Count})";
}