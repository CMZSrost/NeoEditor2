using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Data.Command;

public class CommandHistory : ICommandHistory
{
    private readonly Stack<IEditorCommand> _undoStack = new();
    private readonly Stack<IEditorCommand> _redoStack = new();
    private const int MaxHistory = 100;

    /// <summary>Called after a command is executed, for persisting to DB.</summary>
    public Func<IEditorCommand, Task>? OnCommandPersist { get; set; }

    /// <summary>Track the last persist Task so callers can await it before saving/exiting.</summary>
    private volatile Task _lastPersistTask = Task.CompletedTask;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event Action? StateChanged;

    public void Execute(IEditorCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        TrimHistory();
        StateChanged?.Invoke();
        // Persist synchronously — SQLite write to a local file is ~1-5ms.
        // Fire-and-forget can lose WAL commands on app exit, leaving unsaved
        // XML edits unrecoverable when the editor reopens.
        if (OnCommandPersist != null)
        {
            System.Diagnostics.Debug.WriteLine($"[CmdHistory] persist START: cmd={command.GetType().Name} desc={command.Description}");
            _lastPersistTask = OnCommandPersist(command);
            try
            {
                _lastPersistTask.GetAwaiter().GetResult();
                System.Diagnostics.Debug.WriteLine($"[CmdHistory] persist OK: cmd={command.GetType().Name}");
            }
            catch (Exception ex)
            {
                // Error already logged in the callback itself; prevent re-throw.
                System.Diagnostics.Debug.WriteLine($"[CommandHistory] persist task faulted: {ex}");
            }
        }
    }

    /// <summary>Await the last persist operation to ensure WAL durability before save or exit.</summary>
    public Task FlushAsync() => _lastPersistTask;

    public IEditorCommand? Undo()
    {
        if (!CanUndo) return null;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
        StateChanged?.Invoke();
        return cmd;
    }

    public IEditorCommand? Redo()
    {
        if (!CanRedo) return null;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        StateChanged?.Invoke();
        return cmd;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    public void RestoreFromLog(IEditorCommand command)
    {
        _undoStack.Push(command);
        _redoStack.Clear();
        TrimHistory();
        StateChanged?.Invoke();
    }

    private void TrimHistory()
    {
        if (_undoStack.Count <= MaxHistory) return;
        var temp = new Stack<IEditorCommand>(MaxHistory);
        for (var i = 0; i < MaxHistory; i++)
            temp.Push(_undoStack.Pop());
        _undoStack.Clear();
        while (temp.Count > 0)
            _undoStack.Push(temp.Pop());
    }
}
