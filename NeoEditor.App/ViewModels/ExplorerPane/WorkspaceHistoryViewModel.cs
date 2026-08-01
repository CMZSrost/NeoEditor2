using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.ViewModels.ExplorerPane;

/// <summary>
/// Workspace history panel (sidebar "Workspace" button). Lists profile workspaces
/// newest-first together with their dirty (unsaved-edit) state; clicking an entry
/// opens its merge editor. Replaces the Mods / Profiles sidebar panes
/// (Doc 36 §5.0 side bar: Home / Explorer / Workspace / Settings).
/// Registered transient so every pane open re-reads the latest history + dirty state.
/// </summary>
public partial class WorkspaceHistoryViewModel : ViewModelBase
{
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IProfileManager _profileManager;
    private readonly IWorkspacePersistenceService _persistenceSvc;

    public ObservableCollection<ProfileEntry> Workspaces { get; } = [];

    [ObservableProperty] public partial ProfileEntry? SelectedWorkspace { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }

    public bool HasWorkspaces => Workspaces.Count > 0;
    public int DirtyWorkspaceCount => Workspaces.Count(w => w.HasUnsavedEdits);
    public bool HasDirtyWorkspaces => DirtyWorkspaceCount > 0;
    public string DirtyWorkspaceCountText => HasDirtyWorkspaces
        ? $"⚠ {DirtyWorkspaceCount} workspace(s) with unsaved edits"
        : string.Empty;

    public WorkspaceHistoryViewModel(
        IDbContextFactory<EditorDbContext> editorDbFactory,
        IProfileManager profileManager,
        IWorkspacePersistenceService persistenceSvc,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ILogger<WorkspaceHistoryViewModel> logger)
        : base(localizationService, notificationService, logger)
    {
        _editorDbFactory = editorDbFactory;
        _profileManager = profileManager;
        _persistenceSvc = persistenceSvc;
        AsyncHelper.FireAndForget(RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await using var db = await _editorDbFactory.CreateDbContextAsync();
            var profiles = await db.ProfileInfos.OrderByDescending(p => p.UpdateTime).ToListAsync();

            Workspaces.Clear();
            foreach (var profile in profiles)
            {
                try
                {
                    var loadInfos = _profileManager.LoadMods(profile.Content);
                    var dirtyCount = 0;
                    foreach (var mli in loadInfos)
                    {
                        if (mli.Info is { ModId: > 0 or -1 } &&
                            await _persistenceSvc.HasUnsavedCommandsAsync("mod", mli.Info.ModId))
                            dirtyCount++;
                    }

                    Workspaces.Add(new ProfileEntry(profile.ProfileId, profile.Name,
                        Loc["HomePageModsFormat", loadInfos.Count])
                    {
                        HasUnsavedEdits = dirtyCount > 0,
                        DirtyModCount = dirtyCount
                    });
                }
                catch { /* skip unparseable profiles */ }
            }
        }
        catch { /* DB may not be initialized yet */ }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasWorkspaces));
            OnPropertyChanged(nameof(DirtyWorkspaceCount));
            OnPropertyChanged(nameof(HasDirtyWorkspaces));
            OnPropertyChanged(nameof(DirtyWorkspaceCountText));
        }
    }

    [RelayCommand]
    private void OpenMergeEditor(ProfileEntry? entry)
    {
        if (entry is null) return;
        Task.Run(async () =>
        {
            await using var db = await _editorDbFactory.CreateDbContextAsync();
            var profile = await db.ProfileInfos.FindAsync(entry.ProfileId);
            if (profile is not null)
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Messenger.Send(new OpenMergeEditorMessage(profile)));
        });
    }
}
