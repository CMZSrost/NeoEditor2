using System;

namespace NeoEditor.Data.Command;

public interface ICommandHistory
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    event Action? StateChanged;
    void Execute(IEditorCommand command);
    void Undo();
    void Redo();
    void Clear();
    /// <summary>Push a command directly onto the undo stack without executing it (for log replay).</summary>
    void RestoreFromLog(IEditorCommand command);
}
