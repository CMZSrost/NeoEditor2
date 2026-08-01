using CommunityToolkit.Mvvm.Input;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// A button contributed by a tool plugin to its own toolbar (assembled inside
/// the Tool panel). Optional — a plugin that does not contribute toolbar items
/// returns <c>null</c> from <see cref="IToolPlugin.CreateToolbarItems"/>.
/// Spec: D02-dynamic-dock-layout §四.
/// </summary>
public record ToolbarItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? IconSymbol { get; init; }
    public required IRelayCommand Command { get; init; }

    /// <summary>Group the button belongs to (Navigation / Edit / View / Persistence).</summary>
    public string? Group { get; init; }
    public int Order { get; init; }
}
