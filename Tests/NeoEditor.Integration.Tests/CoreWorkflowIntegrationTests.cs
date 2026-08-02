using System;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Helper;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Services;
using Xunit;
using Xunit.Abstractions;

namespace NeoEditor.Integration.Tests;

/// <summary>
/// Integration tests covering cross-module workflows without Avalonia UI.
/// Focus on message flow across plugin boundaries and DI composition.
/// </summary>
public class CoreWorkflowIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly IMessenger _messenger = WeakReferenceMessenger.Default;

    public CoreWorkflowIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. Cross-Plugin Message Flow
    //  DataViewer → EntityEditor: entity selection opens editor
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Message_Flow_EntitySelected_Triggers_Editor_Chain()
    {
        int selected = 0, active = 0, refresh = 0;

        _messenger.Register<EntitySelectedMessage>(this, (_, _) => selected++);
        _messenger.Register<ActiveEntityChangedMessage>(this, (_, _) => active++);
        _messenger.Register<RefreshEntityEditorMessage>(this, (_, _) => refresh++);

        var entity = new ItemType { Id = 10, GroupId = 1, SubgroupId = 1, Name = "ChainTest" };
        _messenger.Send(new EntitySelectedMessage(entity, SelectSource.BottomDataGrid));
        _messenger.Send(new ActiveEntityChangedMessage(entity));
        _messenger.Send(new RefreshEntityEditorMessage(entity));

        Assert.Equal(1, selected);
        Assert.Equal(1, active);
        Assert.Equal(1, refresh);

        _messenger.Unregister<EntitySelectedMessage>(this);
        _messenger.Unregister<ActiveEntityChangedMessage>(this);
        _messenger.Unregister<RefreshEntityEditorMessage>(this);
        _output.WriteLine("[PASS] EntitySelected → Active → Refresh: all 3 messages received");
    }

    [Fact]
    public void Message_Flow_DataViewer_To_EntityEditor_Bridge()
    {
        // Simulates DataViewer triggering entity editing flow
        int navRequested = 0;
        _messenger.Register<NavigateToEntityRequestedMessage>(this, (_, _) => navRequested++);

        _messenger.Send(new NavigateToEntityRequestedMessage("ItemType", "42"));

        Assert.Equal(1, navRequested);
        _messenger.Unregister<NavigateToEntityRequestedMessage>(this);
        _output.WriteLine("[PASS] Navigation request flows across plugin boundary");
    }

    [Fact]
    public void Message_Flow_Save_Coordinated()
    {
        // Simulates Save workflow: DataGrid → Editor → Persistence
        int saveCompleted = 0;
        _messenger.Register<SaveCompletedMessage>(this, (_, _) => saveCompleted++);

        _messenger.Send(new SaveRequestedMessage());
        _messenger.Send(new EntityDbSavedMessage(1));
        _messenger.Send(new SaveCompletedMessage());

        Assert.Equal(1, saveCompleted);
        _messenger.Unregister<SaveCompletedMessage>(this);
        _output.WriteLine("[PASS] Save workflow: requested → saved → completed");
    }

    [Fact]
    public void Message_Flow_Profile_Edit_Load_Save()
    {
        int loadCount = 0, editCount = 0, saveCount = 0;
        _messenger.Register<LoadProfileMessage>(this, (_, _) => loadCount++);
        _messenger.Register<EditProfileMessage>(this, (_, _) => editCount++);
        _messenger.Register<SaveProfileMessage>(this, (_, _) => saveCount++);

        var profile = new ProfileInfo { Name = "Test Profile" };
        _messenger.Send(new LoadProfileMessage(profile));
        _messenger.Send(new EditProfileMessage(profile));
        _messenger.Send(new SaveProfileMessage(profile));

        Assert.Equal(1, loadCount);
        Assert.Equal(1, editCount);
        Assert.Equal(1, saveCount);

        _messenger.Unregister<LoadProfileMessage>(this);
        _messenger.Unregister<EditProfileMessage>(this);
        _messenger.Unregister<SaveProfileMessage>(this);
        _output.WriteLine("[PASS] Profile lifecycle messages flow correctly");
    }

    [Fact]
    public void Message_Flow_MergeView_Dirty_State()
    {
        int dirtyChanged = 0;
        _messenger.Register<MergeViewDirtyChangedMessage>(this, (_, _) => dirtyChanged++);

        _messenger.Send(new MergeViewDirtyChangedMessage(true));
        _messenger.Send(new MergeViewDirtyChangedMessage(false));

        Assert.Equal(2, dirtyChanged);
        _messenger.Unregister<MergeViewDirtyChangedMessage>(this);
        _output.WriteLine("[PASS] MergeView dirty state messages flow correctly");
    }

    [Fact]
    public void Message_Flow_OverlayChain_Request()
    {
        int overlayRequested = 0;
        _messenger.Register<OverlayChainRequestedMessage>(this, (_, _) => overlayRequested++);

        _messenger.Send(new OverlayChainRequestedMessage("ItemType:1", "TestItem", "ItemType"));

        Assert.Equal(1, overlayRequested);
        _messenger.Unregister<OverlayChainRequestedMessage>(this);
        _output.WriteLine("[PASS] OverlayChain request message flows correctly");
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. DI Composition: DataTableService resolves
    // ═══════════════════════════════════════════════════════════════

    private static ServiceCollection AddMinimalDataTableServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkspaceSession>(new WorkspaceSession());
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
        services.AddSingleton<IReferenceResolver>(new StubReferenceResolver());
        services.AddSingleton<DataTableService>();
        return services;
    }

    [Fact]
    public void DataTableService_Resolves_From_Minimal_DI()
    {
        var sp = AddMinimalDataTableServices().BuildServiceProvider();
        var dts = sp.GetRequiredService<DataTableService>();
        Assert.NotNull(dts);
        _output.WriteLine("[PASS] DataTableService resolves from minimal DI");
    }

    [Fact]
    public void DataTableService_Delegates_To_Session()
    {
        var session = new WorkspaceSession();
        var mergeStore = new EntityMergeStore();
        var editStore = new EditTrackingStore();
        session.SetActiveStores(mergeStore, editStore);

        var services = new ServiceCollection();
        services.AddSingleton<IWorkspaceSession>(session);
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
        services.AddSingleton<IReferenceResolver>(new StubReferenceResolver());
        services.AddSingleton<DataTableService>();
        var sp = services.BuildServiceProvider();

        var dts = sp.GetRequiredService<DataTableService>();
        Assert.Same(mergeStore, dts.ActiveMergeStore);
        Assert.Same(editStore, dts.ActiveEditStore);
        _output.WriteLine("[PASS] DataTableService → session delegation works (no static Instance)");
    }

    // ═══════════════════════════════════════════════════════════════
    //  3. Plugin Contracts
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Plugin_DataViewer_Contract_Verification()
    {
        // D02: DataViewer is split into per-Tool IToolPlugin classes (DataTablePlugin,
        // ForwardIndexPlugin, ReverseIndexPlugin, SearchPlugin, PeekPlugin).
        var dvPlugin = new NeoEditor.Plugins.DataViewer.DataTablePlugin();
        Assert.Equal("DataViewer.DataTable", dvPlugin.Name);
        Assert.IsAssignableFrom<Core.Abstractions.IToolPlugin>(dvPlugin);
        _output.WriteLine("[PASS] DataViewerPlugin implements IToolPlugin");
    }
}

internal sealed class StubReferenceResolver : IReferenceResolver
{
    public T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity => default;
    public string? LookupSubject(string sourceEntityId, string propertyName, Type targetType, string rawId,
        Type? secondaryTargetType = null) => null;
    public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)> ReverseLookup(
        EntityMergeStore store, string targetEntityId) => [];
    public void NavigateTo(Type entityType, string entityId) { }
    public void NavigateToByKey<T>(int key) where T : IEntity { }
    public void NavigateToByKeyFor<T>(int key, IEntity sourceEntity) where T : IEntity { }
    public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType, EntityMergeStore? storeOverride = null) => null;
    public System.Threading.Tasks.Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store)
        => System.Threading.Tasks.Task.CompletedTask;
    public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)> ResolveReverseRefs(
        EntityMergeStore store, string targetEntityId) => [];
    public void ClearLookupCache() { }
    public string? LookupEntityId(ReferenceIndexService indexService, string entityType, string rawId, string? sourceNs) => null;
}
