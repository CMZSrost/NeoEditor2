using System;
using System.Collections.Generic;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Data.Command;

public class EditCellCommand : ISerializableCommand
{
    private readonly IEntity _entity;
    private readonly PropertyInfo _property;
    private readonly string _columnName;
    private readonly object? _oldValue;
    private readonly object? _newValue;
    private readonly Action _onChanged;

    public string Description => $"Edit {_columnName}: '{_oldValue}' → '{_newValue}'";
    public string CommandType => "EditCell";

    /// <summary>Source entity ModId. Used as persistence target fallback when the
    /// merge editor (ProfileId=-1) has no valid ModInfo binding.</summary>
    public int SourceModId => _entity.ModId;

    /// <inheritdoc cref="IEditorCommand.GetAffectedEntityIds"/>
    public IReadOnlySet<string> GetAffectedEntityIds() => new HashSet<string> { _entity.EntityId };

    /// <inheritdoc cref="IEditorCommand.GetCacheDelta"/>
    /// <remarks>R30 (追修 6): the edited entity mutates in place — upsert it into the
    /// HostService working-set cache or SaveAllAsync silently drops it (cache miss →
    /// empty save → WAL never cleared → replay on restart = dirty-on-open).</remarks>
    public IReadOnlyDictionary<string, IEntity?> GetCacheDelta() =>
        new Dictionary<string, IEntity?> { [_entity.EntityId] = _entity };

    /// <inheritdoc cref="IEditorCommand.GetUndoCacheDelta"/>
    public IReadOnlyDictionary<string, IEntity?> GetUndoCacheDelta() => GetCacheDelta();

    public EditCellCommand(IEntity entity, PropertyInfo property, string columnName,
        object? oldValue, object? newValue, Action onChanged)
    {
        _entity = entity;
        _property = property;
        _columnName = columnName;
        _oldValue = oldValue;
        _newValue = newValue;
        _onChanged = onChanged;
    }

    public void Execute()
    {
        _property.SetValue(_entity, _newValue);
        // TODO M8.4: Wire via IWorkspaceSession.MarkEntityDirty() or EditTrackingStore
        _onChanged();
    }

    public void Undo()
    {
        _property.SetValue(_entity, _oldValue);
        _onChanged();
    }

    public string Serialize()
    {
        // R30 (A2): reference values persist as raw text — see BatchEditCommand.Serialize.
        var refAttr = _property.GetCustomAttribute<ReferenceFieldAttribute>();
        var obj = new JObject
        {
            ["entityId"] = _entity.EntityId,
            ["entityType"] = _entity.GetType().Name,
            ["propertyName"] = _property.Name,
            ["columnName"] = _columnName,
            ["oldValue"] = _oldValue != null
                ? JToken.FromObject(ReferenceText.GetRawString(_oldValue, refAttr))
                : JValue.CreateNull(),
            ["newValue"] = _newValue != null
                ? JToken.FromObject(ReferenceText.GetRawString(_newValue, refAttr))
                : JValue.CreateNull()
        };
        return obj.ToString(Formatting.None);
    }
}
