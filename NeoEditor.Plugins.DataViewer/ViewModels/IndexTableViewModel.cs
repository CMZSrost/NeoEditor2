using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.ViewModels;

public partial class IndexTableViewModel : ObservableRecipient
{
    private readonly IWorkspaceSession _session;
    private readonly IBrowserIndexService _bis;
    private bool _loading;

    [ObservableProperty] public partial string Label { get; set; } = "";
    [ObservableProperty] public partial bool IsExpired { get; set; }
    public System.Collections.ObjectModel.ObservableCollection<IndexRow> Rows { get; } = [];
    public IndexDirection Direction { get; init; }
    public bool IsForward => Direction == IndexDirection.Forward;

    /// <summary>Tab title for the bottom Dock tool, includes expired indicator.</summary>
    public string TabTitle => Direction == IndexDirection.Forward
        ? (IsExpired ? "Ref Index ⚠" : "Ref Index")
        : (IsExpired ? "Reverse Index ⚠" : "Reverse Index");

    /// <summary>Set by DocumentWorkspaceViewModel when an entity is selected.
    /// The index is NOT auto-refreshed — user must click Refresh.</summary>
    public IEntity? CurrentEntity { get; set; }

    public IndexTableViewModel(IWorkspaceSession session, IBrowserIndexService bis)
    {
        _session = session;
        _bis = bis;
        IsActive = true;

        // R10: Forward index is pre-built by BrowserIndexService at startup.
        if (IsForward) TryLoad();

        // When BrowserStore becomes available later (async), load then too.
        _session.StateChanged += (_, _) => TryLoad();
    }

    public void Clear()
    {
        Rows.Clear();
        CurrentEntity = null;
        IsExpired = false;
        Label = Direction == IndexDirection.Forward ? "Refs → (click Refresh)" : "Refs ← (click Refresh)";
    }

    /// <summary>Mark index as stale — entity data has changed, index may be out of date. R10: no auto-rebuild.</summary>
    public void MarkExpired()
    {
        IsExpired = true;
        var prefix = Direction == IndexDirection.Forward ? "Refs →" : "Refs ←";
        Label = $"{prefix} (expired — click Refresh)";
        OnPropertyChanged(nameof(TabTitle));
    }

    public void SelectRow(IndexRow? row)
    {
        if (row == null) return;
        Messenger.Send(new NavigateToEntityRequestedMessage("Unknown", row.SourceEntityId));
    }

    private void TryLoad()
    {
        if (!IsForward || _loading) return;
        var idx = _session.BrowserStore?.IndexService;
        if (idx == null) return;

        var all = idx.GetAllIndexEntries();
        if (all.Count == 0) return;

        Rows.Clear();
        foreach (var (et, ns, pk, eid, gid, sid) in all)
            Rows.Add(new IndexRow
            {
                EntityType = et, Namespace = ns, Pk = pk,
                EntityId = eid, GroupId = gid, SubgroupId = sid,
            });
        IsExpired = false;
        Label = $"reference_index ({Rows.Count})";
    }

    public void OnCurrentEntityChanged(IEntity? entity)
    {
        CurrentEntity = entity;
        if (IsForward) return;

        Rows.Clear();
        if (entity == null)
        {
            IsExpired = false;
            Label = "Refs ← (click Refresh)";
            return;
        }

        var idx = _session.BrowserStore?.IndexService;
        if (idx == null)
        {
            Label = "No index loaded";
            return;
        }

        var revs = idx.ReverseLookup(entity.EntityId);
        foreach (var (srcEid, propName, rawId) in revs)
            Rows.Add(new IndexRow
            {
                TargetEntityId = entity.EntityId,
                SourceEntityId = srcEid,
                PropertyName = propName,
                RawId = rawId,
            });
        IsExpired = false;
        Label = $"reference_reverse ({Rows.Count})";
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Refresh()
    {
        _loading = true;
        try
        {
            Rows.Clear();
            Label = "Loading…";
            _bis.Invalidate();
            await _bis.EnsureBuiltAsync();
            var indexStore = _session.BrowserStore;
            if (indexStore?.IndexService == null)
            {
                Label = "No data loaded";
                return;
            }

            var idx = indexStore.IndexService!;
            if (IsForward)
            {
                var all = idx.GetAllIndexEntries();
                foreach (var (et, ns, pk, eid, gid, sid) in all)
                    Rows.Add(new IndexRow
                    {
                        EntityType = et, Namespace = ns, Pk = pk,
                        EntityId = eid, GroupId = gid, SubgroupId = sid,
                    });
                IsExpired = false;
                Label = $"reference_index ({Rows.Count})";
            }
            else
            {
                var targetEid = CurrentEntity?.EntityId;
                if (targetEid == null)
                {
                    Label = "Select an entity first";
                    return;
                }
                var revs = idx.ReverseLookup(targetEid);
                foreach (var (srcEid, propName, rawId) in revs)
                    Rows.Add(new IndexRow
                    {
                        TargetEntityId = targetEid,
                        SourceEntityId = srcEid,
                        PropertyName = propName,
                        RawId = rawId,
                    });
                IsExpired = false;
                Label = $"reference_reverse ({Rows.Count})";
            }
        }
        finally
        {
            _loading = false;
        }
    }
}

public class IndexRow
{
    // reference_index columns (forward)
    public string EntityType { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Pk { get; set; } = "";
    public string EntityId { get; set; } = "";
    public long? GroupId { get; set; }
    public long? SubgroupId { get; set; }
    // reference_reverse columns (reverse)
    public string TargetEntityId { get; set; } = "";
    public string SourceEntityId { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public string RawId { get; set; } = "";
}

public enum IndexDirection { Forward, Reverse }

public partial class IndexTableViewModel
{
    partial void OnIsExpiredChanged(bool value) => OnPropertyChanged(nameof(TabTitle));
}
