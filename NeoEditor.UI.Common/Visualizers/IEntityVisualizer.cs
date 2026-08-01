using Avalonia.Controls;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.UI.Common.Visualizers;

/// <summary>
/// Provides custom visualizations for a specific entity type.
/// Detail view: shown in the Data Browser entity viewer tabs (full detail).
/// M10: moved from DataViewer Plugin to UI.Common — shared by both DataViewer and EntityEditor Plugins.
/// </summary>
public interface IEntityVisualizer
{
    System.Type EntityType { get; }

    /// <summary>Full detail visualization for the Data Browser viewer tab.</summary>
    Control BuildDetail(IEntity entity);
}
