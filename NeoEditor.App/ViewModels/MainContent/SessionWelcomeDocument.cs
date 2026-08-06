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

    /// <summary>Docs/41 需求5: localized usage + shortcuts (set by the workspace VM at
    /// creation; falls back to English defaults).</summary>
    [ObservableProperty] public partial string UsageTips { get; set; } =
        "Click a row in the bottom Data Table to view an entity.\n" +
        "Left panel: edit field values.\n" +
        "Edits auto-save; yellow/green highlights = changes not yet exported.\n" +
        "Right panel: Overview of the selected entity.\n" +
        "Bottom tabs: Reference indexes, Search, Conflicts.\n\n" +
        "Keyboard Shortcuts:\n" +
        "  Ctrl+Shift+S — Save & Export (write to game XML)\n" +
        "  Ctrl+S — Save current tab to DB (usually auto)\n" +
        "  Ctrl+Z / Ctrl+Shift+Z — Undo / Redo\n" +
        "  Ctrl+Y — Redo\n" +
        "  Ctrl+F — Find in DataGrid\n" +
        "  Ctrl+C/V — Copy/Paste cells\n" +
        "  Ctrl+E — Toggle value editor";

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
