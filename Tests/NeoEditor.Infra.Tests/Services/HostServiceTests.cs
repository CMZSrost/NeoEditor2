using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Tests.Data.Repository;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Infra.Tests.Services;

public class HostServiceTests
{
    private readonly StubWorkspaceSession _session = new();
    private readonly HostService _hostService;

    public HostServiceTests()
    {
        // HostService requires IDbContextFactory<GameDbContext> which needs a real SQLite setup.
        // For unit tests, we use constructor that only requires the session.
        // We test HostService through an anonymous subclass that skips DB factory setup.
        _hostService = new HostService(_session, null!,
            new RepositoryTestHelpers.StubXmlParser(),
            new RepositoryTestHelpers.StubConfigService(Path.GetTempPath()),
            null!,
            null!,
            null!);
    }

    // ──────────────────────────────────────────
    //  Test 1: ExecuteAsync calls command.Execute
    // ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Executes_Command()
    {
        var cmd = new StubCommand("entity1");

        var result = await _hostService.ExecuteAsync(cmd);

        Assert.True(cmd.WasExecuted);
        Assert.True(result.Success);
    }

    // ──────────────────────────────────────────
    //  Test 2: ExecuteAsync marks entities dirty
    // ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Marks_Entity_Dirty()
    {
        var cmd = new StubCommand("entity1", "entity2");

        await _hostService.ExecuteAsync(cmd);

        Assert.Contains("entity1", _session.DirtyEntities);
        Assert.Contains("entity2", _session.DirtyEntities);
        Assert.Equal(2, _session.DirtyEntities.Count);
    }

    // ──────────────────────────────────────────
    //  Test 3: ExecuteAsync fires Changed event
    // ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Fires_ChangedEvent()
    {
        var cmd = new StubCommand("entity1");
        var eventCount = 0;
        EntityChangedEvent? lastEvent = null;
        var observer = new AnonymousObserver<EntityChangedEvent>(ev =>
        {
            eventCount++;
            lastEvent = ev;
        });
        using var sub = _hostService.Changes.Subscribe(observer);

        await _hostService.ExecuteAsync(cmd);

        Assert.Equal(1, eventCount);
        Assert.NotNull(lastEvent);
        Assert.Equal("entity1", lastEvent!.Value.EntityId);
    }

    // ──────────────────────────────────────────
    //  PreExecuteHook fires before the command executes (R25 空挂修复)
    // ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Fires_PreExecuteHook()
    {
        var calls = new List<IEditorCommand>();
        _hostService.RegisterPreExecuteHook(new PreExecuteHookStub(calls));
        var cmd = new StubCommand("entity1");

        await _hostService.ExecuteAsync(cmd);

        Assert.Single(calls);
        Assert.Same(cmd, calls[0]);
    }

    private sealed class PreExecuteHookStub(List<IEditorCommand> calls) : IExtensionPoint<PreExecuteContext>
    {
        public string Name => "test";
        public int Order => 0;

        public Task ExecuteAsync(PreExecuteContext ctx)
        {
            calls.Add(ctx.Command);
            return Task.CompletedTask;
        }
    }

    // ──────────────────────────────────────────
    //  Test 4: Execute via scope
    // ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Uses_Scope_CommandHistory()
    {
        var scopeHistory = new StubCommandHistory();
        _hostService.RegisterCommandScope("testScope", scopeHistory);
        _hostService.SetActiveScope("testScope");
        var cmd = new StubCommand("entity1");

        var result = await _hostService.ExecuteAsync(cmd);

        Assert.True(cmd.WasExecuted);
        Assert.Contains(scopeHistory.ExecutedCommands, c => c == cmd);
        Assert.True(result.Success);
    }

    // ──────────────────────────────────────────
    //  Test 5: Scope registry
    // ──────────────────────────────────────────

    [Fact]
    public async Task ScopeRegistry_Works()
    {
        var scopeA = new StubCommandHistory();
        var scopeB = new StubCommandHistory();

        _hostService.RegisterCommandScope("a", scopeA);
        _hostService.RegisterCommandScope("b", scopeB);

        // Execute on scope A
        await _hostService.ExecuteAsync(new StubCommand("e1"), "a");
        Assert.Single(scopeA.ExecutedCommands);
        Assert.Empty(scopeB.ExecutedCommands);

        // Execute on scope B
        await _hostService.ExecuteAsync(new StubCommand("e2"), "b");
        Assert.Single(scopeA.ExecutedCommands);
        Assert.Single(scopeB.ExecutedCommands);

        // Unregister A — A commands stay in A's history
        _hostService.UnregisterCommandScope("a");
        await _hostService.ExecuteAsync(new StubCommand("e3"), "b");
        Assert.Single(scopeA.ExecutedCommands);
        Assert.Equal(2, scopeB.ExecutedCommands.Count);
    }

    // ──────────────────────────────────────────
    //  Test 6: Discard clears dirty entities
    // ──────────────────────────────────────────

    [Fact]
    public async Task Discard_Clears_DirtyEntities()
    {
        _session.MarkEntityDirty("d1");
        _session.MarkEntityDirty("d2");
        Assert.Equal(2, _session.DirtyEntities.Count);

        await _hostService.DiscardAsync();

        Assert.Empty(_session.DirtyEntities);
    }

    // ──────────────────────────────────────────
    //  Test 7: Undo via scope
    // ──────────────────────────────────────────

    [Fact]
    public async Task Undo_Calls_Scope_Undo()
    {
        var scope = new StubCommandHistory();
        _hostService.RegisterCommandScope("u", scope);
        _hostService.SetActiveScope("u");
        var cmd = new StubCommand("e1");

        await _hostService.ExecuteAsync(cmd);
        Assert.Single(scope.ExecutedCommands);
        Assert.Empty(scope.UndoneCommands);

        await _hostService.UndoAsync("u");
        Assert.Single(scope.UndoneCommands);
        Assert.True(cmd.WasUndone);
    }

    // ──────────────────────────────────────────
    //  Test 8: SetActiveProfile scopes dirty per profile (R26 §3)
    // ──────────────────────────────────────────

    [Fact]
    public void SetActiveProfile_Scopes_Dirty_Entities_Per_Profile()
    {
        var session = new WorkspaceSession();
        var host = new HostService(session, null!,
            new RepositoryTestHelpers.StubXmlParser(),
            new RepositoryTestHelpers.StubConfigService(Path.GetTempPath()),
            null!,
            null!,
            null!);

        host.SetActiveProfile(1);
        host.MarkEntityDirty("e1");
        Assert.Contains("e1", host.DirtyEntities);

        // Switching profile exposes an empty dirty set (per-profile scope).
        host.SetActiveProfile(2);
        Assert.Empty(host.DirtyEntities);

        // Switching back restores the original profile's dirty set.
        host.SetActiveProfile(1);
        Assert.Contains("e1", host.DirtyEntities);
    }

    // ──────────────────────────────────────────
    //  Test 9: Undo re-marks affected entities dirty
    // ──────────────────────────────────────────

    [Fact]
    public async Task Undo_Marks_Affected_Entities_Dirty()
    {
        var scope = new StubCommandHistory();
        _hostService.RegisterCommandScope("u", scope);
        _hostService.SetActiveScope("u");
        var cmd = new StubCommand("e1");

        await _hostService.ExecuteAsync(cmd);
        _hostService.ClearDirtyEntities();
        Assert.Empty(_session.DirtyEntities);

        await _hostService.UndoAsync("u");

        Assert.Contains("e1", _session.DirtyEntities);
    }

    // ──────────────────────────────────────────
    //  Test 10: Redo re-marks affected entities dirty
    // ──────────────────────────────────────────

    [Fact]
    public async Task Redo_Marks_Affected_Entities_Dirty()
    {
        var scope = new StubCommandHistory();
        _hostService.RegisterCommandScope("u", scope);
        _hostService.SetActiveScope("u");
        var cmd = new StubCommand("e1");

        await _hostService.ExecuteAsync(cmd);
        await _hostService.UndoAsync("u");
        _hostService.ClearDirtyEntities();

        await _hostService.RedoAsync("u");

        Assert.Contains("e1", _session.DirtyEntities);
    }
}