using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.MainContent;

/// <summary>Level of a node in the Profile Tool's Mod → XML → data-class tree.</summary>
public enum ProfileTreeItemKind
{
    Mod,
    Xml,
    DataType
}

/// <summary>
/// Unified node for the Profile Tool tree (round22). A single node type keeps the
/// hierarchical grid's DataTemplate simple — the <see cref="Kind"/> drives the icon,
/// double-click behavior and right-click context menu. Mod and XML children are built
/// eagerly; XML → data-class children load lazily the first time the node expands.
/// </summary>
public sealed class ProfileTreeItem : ObservableObject
{
    public required ProfileTreeItemKind Kind { get; init; }

    public required string Name { get; init; }

    /// <summary>Mod → content-root directory; Xml/DataType → absolute XML file path.</summary>
    public string? Path { get; init; }

    public int ModId { get; init; }

    /// <summary>Row count for data-class leaves.</summary>
    public int Count { get; init; }

    public bool IsGame { get; init; }

    public string DisplayName => Kind == ProfileTreeItemKind.DataType ? $"{Name} ({Count})" : Name;

    /// <summary>Small leading glyph for the tree row (display only).</summary>
    public string Icon => Kind switch
    {
        ProfileTreeItemKind.Mod => IsGame ? "🌍" : "📦",
        ProfileTreeItemKind.Xml => "📄",
        _ => "🏷️"
    };

    public bool TypesLoaded { get; set; }

    public ObservableCollection<ProfileTreeItem> Children { get; } = [];
}