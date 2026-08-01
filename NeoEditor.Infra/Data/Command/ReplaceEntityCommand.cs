using System.Collections.Generic;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Data.Command;

/// <summary>
/// Command to replace an entity's working-set version (R26 v2, <c>IEntityRepository{T}.UpdateAsync</c>).
/// Execute puts <c>_entity</c> into the HostService cache; Undo restores the previous
/// instance (or removes it if there was none). Pure data descriptor — no UI coupling.
/// The cache mutation is driven generically by <see cref="GetCacheDelta"/>/<see cref="GetUndoCacheDelta"/>,
/// so HostService needs no type-dispatch for this command.
/// </summary>
public class ReplaceEntityCommand : ISerializableCommand
{
    private readonly string _entityType;
    private readonly IEntity _entity;
    private readonly IEntity? _previous;

    public string Description => $"Replace {_entityType} {_entity.EntityId}";
    public string CommandType => "ReplaceEntity";

    /// <inheritdoc cref="IEditorCommand.GetAffectedEntityIds"/>
    public IReadOnlySet<string> GetAffectedEntityIds() => new HashSet<string> { _entity.EntityId };

    /// <inheritdoc cref="IEditorCommand.GetCacheDelta"/>
    public IReadOnlyDictionary<string, IEntity?> GetCacheDelta()
        => new Dictionary<string, IEntity?> { [_entity.EntityId] = _entity };

    /// <inheritdoc cref="IEditorCommand.GetUndoCacheDelta"/>
    public IReadOnlyDictionary<string, IEntity?> GetUndoCacheDelta()
        => new Dictionary<string, IEntity?> { [_entity.EntityId] = _previous };

    /// <summary>
    /// Create a command that swaps the working-set version of an entity.
    /// </summary>
    /// <param name="entityType">Entity type name.</param>
    /// <param name="entity">The new entity version.</param>
    /// <param name="previous">The previous cached/backend version (null = the entity is brand new).</param>
    public ReplaceEntityCommand(string entityType, IEntity entity, IEntity? previous)
    {
        _entityType = entityType;
        _entity = entity;
        _previous = previous;
    }

    /// <summary>No direct mutation — cache delta is applied by HostService.</summary>
    public void Execute()
    {
    }

    /// <summary>No direct mutation — undo cache delta is applied by HostService.</summary>
    public void Undo()
    {
    }

    public string Serialize()
    {
        var obj = new JObject
        {
            ["entityType"] = _entityType,
            ["entityId"] = _entity.EntityId
        };
        return obj.ToString(Formatting.None);
    }
}