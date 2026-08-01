using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Messages;

/// <summary>Fired when the merge view dirty state changes.</summary>
public record MergeViewDirtyChangedMessage(bool IsDirty);

/// <summary>Request overlay chain display for an entity.</summary>
public record OverlayChainRequestedMessage(string EntityId, string Subject, string EntityType);

/// <summary>Request visual editor display for an entity type.</summary>
public record VisualEditorRequestedMessage(System.Type EntityType, IEntity? Entity);

/// <summary>Request navigation to a specific entity.</summary>
public record NavigateToEntityRequestedMessage(string EntityType, string EntityId);

/// <summary>Save granularity scope (R11).</summary>
public enum SaveScope { All, CurrentTab }

/// <summary>Request save operation.</summary>
public record SaveRequestedMessage(SaveScope Scope = SaveScope.All);

/// <summary>Fired after QuickSaveAsync completes successfully.
/// EntityEditorDocuments listen for this to MarkClean() their dirty state.</summary>
public record SaveCompletedMessage;

/// <summary>Fired when Center XML tab applies edits to an entity.
/// Carries field-level old→new transitions for command_history persistence.</summary>
public record EntityFieldEditsMessage(
    IEntity Entity,
    System.Collections.Generic.IReadOnlyList<Data.Command.EditRecord> Edits);

/// <summary>Fired after EntityEditorDocument.SaveDocument() persists a single entity to game.db.
/// ModGameDataTabsView listens to update WAL snapshot marker so the entity won't re-mark dirty on restart.</summary>
public record EntityDbSavedMessage(int ModId);

// Q10=A: OpenDataBrowserMessage, OpenModManagerMessage deleted (dead messages).
