using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Messages;

/// <summary>User selected an entity in any workspace region.</summary>
public record EntitySelectedMessage(IEntity Entity, SelectSource Source);

public enum SelectSource
{
    BottomDataGrid,
    PeekPanel,
    ReferenceTree,
    NavigationBack,
}

/// <summary>Request to peek a resolved entity in the Peek panel (Ctrl+RMB). Single receiver: DocumentWorkspaceViewModel.</summary>
public record PeekEntityMessage(System.Type EntityType, string EntityId, IEntity? Entity);

/// <summary>Request to open entity in split view.</summary>
public record OpenInSplitViewMessage(IEntity Entity);

/// <summary>Emitted when the active entity being edited changes.
/// Left panel (KeyValueEditor, OverlayChain) follows this.</summary>
public record ActiveEntityChangedMessage(IEntity? Entity);

/// <summary>Request to refresh the active EntityEditorDocument (visual + XML).</summary>
public record RefreshEntityEditorMessage(IEntity Entity);

/// <summary>Emitted when session state changes (started/cleared). MainWindow uses this for page switching.</summary>
public record SessionStateChangedMessage(bool IsActive);

/// <summary>Request to resolve a reference and peek at the target entity.</summary>
public record PeekReferenceRequestMessage(IEntity SourceEntity, System.Type TargetType, string RawId, string PropertyName);

/// <summary>Emitted when data loading completes in ModGameDataTabsView. Carries type and entity counts.</summary>
public record DataLoadCompletedMessage(int TypeCount, int EntityCount);

/// <summary>Request to create a new entity in the current mod context.</summary>
public record CreateEntityRequestedMessage;

/// <summary>Request to copy the currently selected entity.</summary>
public record CopyEntityRequestedMessage;

/// <summary>Request to delete the currently selected entity.</summary>
public record DeleteEntityRequestedMessage;
