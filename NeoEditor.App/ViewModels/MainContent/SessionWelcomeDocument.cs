using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.MainContent;

/// <summary>Welcome document shown in center when session starts.
/// Displays loading progress, entity statistics, and usage tips.</summary>
public partial class SessionWelcomeDocument : DocumentViewBase
{
    [ObservableProperty] public partial string StatusText { get; set; } = "Ready";
    [ObservableProperty] public partial string StatsText { get; set; } = "Select an entity below to begin editing.";
    [ObservableProperty] public partial bool IsLoading { get; set; } = false;

    public string UsageTips { get; } =
        "Click a row in the bottom Data Table to view an entity.\n" +
        "Left panel: edit field values → click Apply.\n" +
        "Right panel: Overview of the selected entity.\n" +
        "Bottom tabs: Reference indexes, Search, Conflicts.\n\n" +
        "Keyboard Shortcuts:\n" +
        "  Ctrl+S — Save session\n" +
        "  Ctrl+F — Find in DataGrid\n" +
        "  Ctrl+C/V — Copy/Paste cells\n" +
        "  Ctrl+Z/Y — Undo/Redo";

    public SessionWelcomeDocument()
    {
        SetStaticTitle("Welcome");
    }

    public void SetLoaded(int typeCount, int entityCount)
    {
        IsLoading = false;
        StatusText = "Ready";
        StatsText = $"Loaded {typeCount} entity types, {entityCount} total entities.";
    }
}
