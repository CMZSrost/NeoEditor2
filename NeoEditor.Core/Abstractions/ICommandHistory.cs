using System;
using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Undo/redo stack for a single scope.
/// HostService manages a registry of named scopes (one per tab/view).
/// Concrete implementations also handle WAL persistence via OnCommandPersist.
/// DI lifetime: not registered globally; created per-scope by the owner.
/// </summary>
public interface ICommandHistory
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    event Action? StateChanged;
    void Execute(IEditorCommand command);

    /// <summary>Undo the last executed command. Returns the undone command, or null if none.</summary>
    IEditorCommand? Undo();

    /// <summary>Redo the last undone command. Returns the redone command, or null if none.</summary>
    IEditorCommand? Redo();

    void Clear();
    /// <summary>Push a command directly onto the undo stack without executing it (for log replay).</summary>
    void RestoreFromLog(IEditorCommand command);
    /// <summary>Await the last persist operation to ensure WAL durability before save or exit.</summary>
    Task FlushAsync();
}
