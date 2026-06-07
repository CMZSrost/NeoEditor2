using System.Collections.Generic;

namespace NeoEditor.Services;

/// <summary>
/// Per-tab edit tracking — replaces static EditedCells / NewEntityIds on GenericDataGridHelper.
/// One instance per ModGameDataTabsView.
/// </summary>
public class EditTrackingStore
{
    /// <summary>Cells that have been edited in the current session: (EntityId, ColumnName).</summary>
    public HashSet<(string EntityId, string ColumnName)> EditedCells { get; } = new();

    /// <summary>Entities created during the current editing session.</summary>
    public HashSet<string> NewEntityIds { get; } = new();

    public void Clear()
    {
        EditedCells.Clear();
        NewEntityIds.Clear();
    }
}
