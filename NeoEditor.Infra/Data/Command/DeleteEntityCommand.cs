using System;
using System.Collections.Generic;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Data.Command;

/// <summary>
/// Command to delete an entity. Pure data descriptor — no UI coupling.
/// Optional callbacks allow the HostService (or WAL replay) to hook into collection management.
/// When callbacks are null (MCP/CLI), only the entity cache is updated by HostService.
/// </summary>
public class DeleteEntityCommand : ISerializableCommand
{
    private readonly string _entityType;
    private readonly IEntity _entity;
    private readonly Action<IEntity>? _removeAction;
    private readonly Action<IEntity>? _addAction;

    public string Description =>
        $"Delete {_entityType} {_entity.EntityId}";

    public string CommandType => "DeleteEntity";

    /// <summary>The entity type name (e.g., "ItemType").</summary>
    public string EntityType => _entityType;

    /// <summary>The entity instance to delete.</summary>
    public IEntity Entity => _entity;

    /// <inheritdoc cref="IEditorCommand.GetAffectedEntityIds"/>
    public IReadOnlySet<string> GetAffectedEntityIds() => new HashSet<string> { _entity.EntityId };

    /// <inheritdoc cref="IEditorCommand.GetCacheDelta"/>
    public IReadOnlyDictionary<string, IEntity?> GetCacheDelta()
        => new Dictionary<string, IEntity?> { [_entity.EntityId] = null };

    /// <inheritdoc cref="IEditorCommand.GetUndoCacheDelta"/>
    public IReadOnlyDictionary<string, IEntity?> GetUndoCacheDelta()
        => new Dictionary<string, IEntity?> { [_entity.EntityId] = _entity };

    /// <summary>
    /// Create a command that deletes an entity.
    /// </summary>
    /// <param name="entityType">Entity type name.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="removeAction">Optional: callback to remove the entity from a collection (e.g., ObservableCollection.Remove).</param>
    /// <param name="addAction">Optional: callback to re-add the entity to a collection (for Undo).</param>
    public DeleteEntityCommand(string entityType, IEntity entity,
        Action<IEntity>? removeAction = null, Action<IEntity>? addAction = null)
    {
        _entityType = entityType;
        _entity = entity;
        _removeAction = removeAction;
        _addAction = addAction;
    }

    /// <summary>Removes the entity via the registered callback (if any).</summary>
    public void Execute() => _removeAction?.Invoke(_entity);

    /// <summary>Re-adds the entity via the registered callback (if any).</summary>
    public void Undo() => _addAction?.Invoke(_entity);

    public string Serialize()
    {
        var entityData = new JObject();
        foreach (var prop in _entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var colAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>();
            if (colAttr == null) continue;
            var val = prop.GetValue(_entity);
            if (val == null)
                entityData[prop.Name] = JValue.CreateNull();
            else if (prop.PropertyType.IsEnum)
                entityData[prop.Name] = Convert.ToInt32(val);
            else
                entityData[prop.Name] = JToken.FromObject(val);
        }

        var obj = new JObject
        {
            ["entityType"] = _entityType,
            ["entityId"] = _entity.EntityId,
            ["entityData"] = entityData
        };
        return obj.ToString(Formatting.None);
    }
}