using Avalonia.Controls;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

/// <summary>
/// Provides custom visualizations for a specific entity type.
/// Detail view: shown in the Data Browser entity viewer tabs (full detail).
/// Overview view: shown in the merge view's visual overview panel (compact).
/// </summary>
public interface IEntityVisualizer
{
    System.Type EntityType { get; }

    /// <summary>Full detail visualization for the Data Browser viewer tab.</summary>
    Control BuildDetail(IEntity entity);

    /// <summary>Compact overview for the merge view visual overview panel.</summary>
    Control BuildOverview(IEntity entity);
}
