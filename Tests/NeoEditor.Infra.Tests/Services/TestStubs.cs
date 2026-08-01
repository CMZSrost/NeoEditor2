using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Services;

namespace NeoEditor.Infra.Tests.Services;

/// <summary>Stub IWorkspaceSession (Infra version) for testing HostService.</summary>
public class StubWorkspaceSession : NeoEditor.Services.IWorkspaceSession
{
    public int CurrentProfileId { get; set; } = -1;
    public ISet<string> DirtyEntities { get; } = new HashSet<string>();
    public event EventHandler? DirtyStateChanged;
    public event EventHandler? StateChanged;

    // Infra IWorkspaceSession members
    public EntityMergeStore? Store => null;
    public EntityMergeStore? ActiveMergeStore => null;
    public EntityMergeStore? BrowserStore => null;
    public EditTrackingStore? ActiveEditStore => null;
    public ReferenceIndexService? ForwardIndex { get; set; }
    public ReferenceIndexService? ReverseIndex { get; set; }

    public void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore) { }
    public void SetBrowserStore(EntityMergeStore? store) { }

    public ISet<string> GetDirtyEntities(int profileId) => DirtyEntities;
    public void UnloadProfile(int profileId) { }

    public void MarkEntityDirty(string entityId)
    {
        if (DirtyEntities.Add(entityId))
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkEntitiesDirty(IEnumerable<string> entityIds)
    {
        foreach (var id in entityIds)
            DirtyEntities.Add(id);
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearDirtyEntities()
    {
        DirtyEntities.Clear();
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveDirtyEntities(IEnumerable<string> entityIds)
    {
        DirtyEntities.ExceptWith(entityIds);
        DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Stub ICommandHistory that tracks executed/undone commands.</summary>
public class StubCommandHistory : ICommandHistory
{
    public List<IEditorCommand> ExecutedCommands { get; } = new();
    public List<IEditorCommand> UndoneCommands { get; } = new();
    public List<IEditorCommand> RedoneCommands { get; } = new();

    public bool CanUndo => UndoneCommands.Count < ExecutedCommands.Count;
    public bool CanRedo => RedoneCommands.Count > 0;
    public event Action? StateChanged;

    public void Execute(IEditorCommand command)
    {
        command.Execute();
        ExecutedCommands.Add(command);
        StateChanged?.Invoke();
    }

    public IEditorCommand? Undo()
    {
        if (ExecutedCommands.Count > UndoneCommands.Count)
        {
            var cmd = ExecutedCommands[UndoneCommands.Count];
            cmd.Undo();
            UndoneCommands.Add(cmd);
            RedoneCommands.Add(cmd);
            StateChanged?.Invoke();
            return cmd;
        }
        return null;
    }

    public IEditorCommand? Redo()
    {
        if (RedoneCommands.Count > 0)
        {
            var cmd = RedoneCommands[^1];
            cmd.Execute();
            RedoneCommands.RemoveAt(RedoneCommands.Count - 1);
            StateChanged?.Invoke();
            return cmd;
        }
        return null;
    }

    public void Clear()
    {
        ExecutedCommands.Clear();
        UndoneCommands.Clear();
        RedoneCommands.Clear();
        StateChanged?.Invoke();
    }

    public void RestoreFromLog(IEditorCommand command)
    {
        ExecutedCommands.Add(command);
        StateChanged?.Invoke();
    }

    public Task FlushAsync() => Task.CompletedTask;
}

/// <summary>Stub IEditorCommand that tracks invocation.</summary>
public class StubCommand : IEditorCommand
{
    public bool WasExecuted { get; private set; }
    public bool WasUndone { get; private set; }
    public string Description { get; set; } = "test command";
    private readonly HashSet<string> _affectedIds = new();

    public StubCommand(params string[] affectedEntityIds)
    {
        _affectedIds = new HashSet<string>(affectedEntityIds);
    }

    public void Execute()
    {
        WasExecuted = true;
    }

    public void Undo()
    {
        WasUndone = true;
    }

    public IReadOnlySet<string> GetAffectedEntityIds() => _affectedIds;
}

/// <summary>No-op INotificationService stub (B5: HostService/ModManager ctor dep).</summary>
public class StubNotificationService : NeoEditor.Infra.Services.INotificationService
{
    public void ShowSuccess(string message, string title = "Success") { }
    public void ShowError(string message, string title = "Error") { }
    public void ShowInfo(string message, string title = "Info") { }
    public void ShowWarning(string message, string title = "Warning") { }
}

/// <summary>Minimal IBrowserIndexService stub (B5: HostService/ModManager ctor dep).</summary>
public class StubBrowserIndexService : NeoEditor.Infra.Services.IBrowserIndexService
{
    public ReferenceIndexService? Index => null;
    public bool IsBuilding => false;
    public Dictionary<string, string> GlobalModNames { get; } = new();
    public void Invalidate() { }
    public Task EnsureBuiltAsync() => Task.CompletedTask;
}

/// <summary>Simple IObserver{T} implementation using delegates.</summary>
public class AnonymousObserver<T> : IObserver<T>
{
    private readonly Action<T> _onNext;
    private readonly Action<Exception>? _onError;
    private readonly Action? _onCompleted;

    public AnonymousObserver(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)
    {
        _onNext = onNext;
        _onError = onError;
        _onCompleted = onCompleted;
    }

    public void OnNext(T value) => _onNext(value);
    public void OnError(Exception error) => _onError?.Invoke(error);
    public void OnCompleted() => _onCompleted?.Invoke();
}
