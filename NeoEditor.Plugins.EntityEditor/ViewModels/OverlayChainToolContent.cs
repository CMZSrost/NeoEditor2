using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Infra.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.EntityEditor.ViewModels;

/// <summary>
/// Overlay chain display for the left ToolDock.
/// Shows which mods override which data entries for the active entity.
/// Migrated from NeoEditor.App during M10 Phase 5 with DI-injected ILocalizationService.
/// </summary>
public partial class OverlayChainToolContent : ObservableObject
{
    private readonly IWorkspaceSession _session;
    public ILocalizationService Loc { get; }

    [ObservableProperty] public partial string EntitySubject { get; set; } = "";
    [ObservableProperty] public partial string EntityType { get; set; } = "";
    [ObservableProperty] public partial string EntityId { get; set; } = "";
    public ObservableCollection<ChainEntryViewModel> Winners { get; } = [];
    public ObservableCollection<ChainEntryViewModel> Losers { get; } = [];
    public bool HasEntity => !string.IsNullOrEmpty(EntityId);
    public bool HasNoEntity => string.IsNullOrEmpty(EntityId);
    public bool HasWinners => Winners.Count > 0;
    public bool HasLosers => Losers.Count > 0;

    public OverlayChainToolContent(IWorkspaceSession session, ILocalizationService loc)
    {
        _session = session;
        Loc = loc;
    }

    public void LoadChain(string entityId, string subject, string entityType)
    {
        EntityId = entityId;
        EntitySubject = subject;
        EntityType = entityType;
        Winners.Clear();
        Losers.Clear();

        var chainDisplay = _session.ActiveMergeStore?.OverlayChainDisplay;
        if (chainDisplay is null || !chainDisplay.TryGetValue(entityId, out var entries) || entries.Count == 0)
        {
            chainDisplay = _session.BrowserStore?.OverlayChainDisplay;
            if (chainDisplay is null || !chainDisplay.TryGetValue(entityId, out entries) || entries.Count == 0)
            {
                RefreshComputed();
                return;
            }
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var isWinner = i == entries.Count - 1;
            var vm = new ChainEntryViewModel(e.ModName, e.Id, e.EntityId, e.Subject ?? "?", isWinner);
            if (isWinner) Winners.Add(vm);
            else Losers.Insert(0, vm);
        }
        RefreshComputed();
    }

    public void Clear()
    {
        EntitySubject = "";
        EntityType = "";
        EntityId = "";
        Winners.Clear();
        Losers.Clear();
        RefreshComputed();
    }

    private void RefreshComputed()
    {
        OnPropertyChanged(nameof(HasEntity));
        OnPropertyChanged(nameof(HasNoEntity));
        OnPropertyChanged(nameof(HasWinners));
        OnPropertyChanged(nameof(HasLosers));
    }
}

public partial class ChainEntryViewModel : ObservableObject
{
    public string ModName { get; }
    public int Id { get; }
    public string EntryEntityId { get; }
    public string Subject { get; }
    public bool IsWinner { get; }

    public ChainEntryViewModel(string modName, int id, string entityId, string subject, bool isWinner)
    {
        ModName = modName;
        Id = id;
        EntryEntityId = entityId;
        Subject = subject;
        IsWinner = isWinner;
    }
}
