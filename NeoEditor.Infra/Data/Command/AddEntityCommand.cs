using System;
using System.Collections.Generic;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Data.Command;

/// <summary>
/// Command to add a new entity. Pure data descriptor — no UI coupling.
/// Optional callbacks allow the HostService (or WAL replay) to hook into collection management.
/// When callbacks are null (MCP/CLI), only the entity cache is updated by HostService.
/// </summary>
public class AddEntityCommand : ISerializableCommand
{
    private readonly string _entityType;
    private readonly IEntity _entity;
    private readonly Action<IEntity>? _addAction;
    private readonly Action<IEntity>? _removeAction;

    public string Description =>
        $"Add {_entityType} {_entity.EntityId}";

    public string CommandType => "AddEntity";

    /// <summary>The entity type name (e.g., "ItemType").</summary>
    public string EntityType => _entityType;

    /// <summary>The entity instance to add.</summary>
    public IEntity Entity => _entity;

    /// <inheritdoc cref="IEditorCommand.GetAffectedEntityIds"/>
    public IReadOnlySet<string> GetAffectedEntityIds() => new HashSet<string> { _entity.EntityId };

    /// <inheritdoc cref="IEditorCommand.GetCacheDelta"/>
    public IReadOnlyDictionary<string, IEntity?> GetCacheDelta()
        => new Dictionary<string, IEntity?> { [_entity.EntityId] = _entity };

    /// <inheritdoc cref="IEditorCommand.GetUndoCacheDelta"/>
    public IReadOnlyDictionary<string, IEntity?> GetUndoCacheDelta()
        => new Dictionary<string, IEntity?> { [_entity.EntityId] = null };

    /// <summary>
    /// Create a command that adds an entity.
    /// </summary>
    /// <param name="entityType">Entity type name.</param>
    /// <param name="entity">The entity to add.</param>
    /// <param name="addAction">Optional: callback to add the entity to a collection (e.g., ObservableCollection.Add).</param>
    /// <param name="removeAction">Optional: callback to remove the entity from a collection (for Undo).</param>
    public AddEntityCommand(string entityType, IEntity entity,
        Action<IEntity>? addAction = null, Action<IEntity>? removeAction = null)
    {
        _entityType = entityType;
        _entity = entity;
        _addAction = addAction;
        _removeAction = removeAction;
    }

    /// <summary>Adds the entity via the registered callback (if any).</summary>
    public void Execute() => _addAction?.Invoke(_entity);

    /// <summary>Removes the entity via the registered callback (if any).</summary>
    public void Undo() => _removeAction?.Invoke(_entity);

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
            else if (prop.GetCustomAttribute<ReferenceFieldAttribute>() is { } refAttr)
                // R30 (A2): reference values persist as raw text ("3,14") — see BatchEditCommand.
                entityData[prop.Name] = JToken.FromObject(ReferenceText.GetRawString(val, refAttr));
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