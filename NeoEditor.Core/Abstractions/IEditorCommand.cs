using System.Collections.Generic;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Base command interface for all data modifications.
/// Executes against in-memory entities; persistence is handled by IHostService.
/// Concrete implementations (EditCell, AddEntity, ReplaceEntity, DeleteEntity) live in NeoEditor.Infra.
/// </summary>
public interface IEditorCommand
{
    void Execute();
    void Undo();
    string Description { get; }

    /// <summary>
    /// Returns the set of entity IDs affected by this command.
    /// Used by HostService for dirty tracking and event firing.
    /// </summary>
    IReadOnlySet<string> GetAffectedEntityIds() => new HashSet<string>();

    /// <summary>
    /// Working-set cache deltas applied by HostService after <see cref="Execute"/>:
    /// key = entity id, value = entity to upsert into the cache (null = remove).
    /// Replaces the previous type-dispatch (is AddEntityCommand / is DeleteEntityCommand)
    /// so HostService applies cache mutations generically (R26 v2).
    /// </summary>
    IReadOnlyDictionary<string, IEntity?> GetCacheDelta() => new Dictionary<string, IEntity?>();

    /// <summary>
    /// Working-set cache deltas applied by HostService after <see cref="Undo"/>:
    /// key = entity id, value = entity to upsert into the cache (null = remove).
    /// </summary>
    IReadOnlyDictionary<string, IEntity?> GetUndoCacheDelta() => new Dictionary<string, IEntity?>();
}