using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class DataBrowserViewModel : ViewModelBase
{
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;

    public ObservableCollection<DomainGroup> DomainGroups { get; } = [];
    [ObservableProperty] public partial string StatusText { get; set; } = "Loading...";
    [ObservableProperty] public partial bool IsLoading { get; set; }

    public DataBrowserViewModel() : this(
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<GameDbContext>>())
    {
    }

    public DataBrowserViewModel(IDbContextFactory<GameDbContext> gameDbFactory)
    {
        _gameDbFactory = gameDbFactory;
        Helper.AsyncHelper.FireAndForget(LoadDomainsAsync());

        // Invalidate reference index when mods or profiles change
        Messenger.Register<SaveProfileMessage>(this, (_, _) => InvalidateIndex());
        Messenger.Register<RefreshModMessage>(this, (_, _) => InvalidateIndex());
        Messenger.Register<InitModMessage>(this, (_, _) => InvalidateIndex());
        Messenger.Register<CellEditedMessage>(this, (_, _) => InvalidateIndex());
    }

    private static void InvalidateIndex()
    {
        EntityBrowserDocument.InvalidateIndex();
    }

    private async Task LoadDomainsAsync()
    {
        IsLoading = true;
        await Dispatcher.UIThread.InvokeAsync(() => DomainGroups.Clear());
        try
        {
            await using var db = await _gameDbFactory.CreateDbContextAsync();
            foreach (var (domainKey, types) in GameDomain.Domains)
            {
                var domainName = Loc[domainKey];
                var entityTypes = new System.Collections.Generic.List<EntityTypeGroup>();
                foreach (var type in types)
                {
                    var count = CountEntitiesFast(db, type);
                    entityTypes.Add(new EntityTypeGroup(type.Name, type, count));
                }
                DomainGroups.Add(new DomainGroup(domainName, entityTypes));
            }
            StatusText = $"{DomainGroups.Count} domains loaded.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        IsLoading = false;
    }

    private static int CountEntitiesFast(GameDbContext db, Type type)
    {
        var m = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(type);
        int c = 0;
        foreach (var _ in (System.Collections.IEnumerable)m.Invoke(db, null)!) c++;
        return c;
    }

    /// <summary>Click entity type → open EntityBrowser tab in main Dock.</summary>
    [RelayCommand]
    private void OpenEntityType(EntityTypeGroup? typeGroup)
    {
        if (typeGroup is null) return;
        var docVm = DocumentWorkspaceViewModel.Instance;
        if (docVm is null) return;

        var document = new EntityBrowserDocument(typeGroup);
        document.SetStaticTitle(typeGroup.TypeName);
        docVm.Documents.Add(document);
        docVm.ActivateDocument(document);
    }

    [RelayCommand]
    private async Task RefreshAsync() { DomainGroups.Clear(); await LoadDomainsAsync(); }

    [RelayCommand]
    private async Task RebuildIndexAsync()
    {
        StatusText = "Rebuilding index...";
        MainContent.EntityBrowserDocument.InvalidateIndex();
        await MainContent.EntityBrowserDocument.EnsureIndexBuiltAsync();
        StatusText = "Index rebuilt.";
    }
}
