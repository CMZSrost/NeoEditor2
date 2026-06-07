using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeoEditor.Data.Command;

public class CommandHistory : ICommandHistory
{
    private readonly Stack<IEditorCommand> _undoStack = new();
    private readonly Stack<IEditorCommand> _redoStack = new();
    private const int MaxHistory = 100;

    /// <summary>Called after a command is executed, for persisting to DB.</summary>
    public Func<IEditorCommand, Task>? OnCommandPersist { get; set; }

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
        OnCommandPersist?.Invoke(command);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
        StateChanged?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        StateChanged?.Invoke();
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
