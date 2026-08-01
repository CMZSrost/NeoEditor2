using System.Collections.Generic;

namespace NeoEditor.Core.Abstractions;

/// <summary>Context for pre-save hooks. Contains the entity IDs about to be persisted.</summary>
/// <param name="EntityIds">IDs of entities that will be saved.</param>
public record PreSaveContext(IReadOnlyList<string> EntityIds);

/// <summary>Context for post-load hooks. Contains the entity IDs that were just loaded.</summary>
/// <param name="EntityIds">IDs of entities that were loaded from the store.</param>
public record PostLoadContext(IReadOnlyList<string> EntityIds);

/// <summary>Context for pre-execute hooks. Contains the command about to be executed.</summary>
/// <param name="Command">The command that will be executed.</param>
public record PreExecuteContext(IEditorCommand Command);

/// <summary>Context for pre-export hooks. Contains the entity IDs about to be exported to XML.</summary>
/// <param name="EntityIds">IDs of entities that will be exported.</param>
public record PreExportContext(IReadOnlyList<string> EntityIds);
