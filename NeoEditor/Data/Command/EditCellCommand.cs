using System;
using System.Reflection;
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
        GenericDataGridHelper.EditedCells.Add((_entity.EntityId, _columnName));
        _onChanged();
    }

    public void Undo()
    {
        _property.SetValue(_entity, _oldValue);
        _onChanged();
    }

    public string Serialize()
    {
        var obj = new JObject
        {
            ["entityId"] = _entity.EntityId,
            ["entityType"] = _entity.GetType().Name,
            ["propertyName"] = _property.Name,
            ["columnName"] = _columnName,
            ["oldValue"] = _oldValue != null ? JToken.FromObject(_oldValue) : JValue.CreateNull(),
            ["newValue"] = _newValue != null ? JToken.FromObject(_newValue) : JValue.CreateNull()
        };
        return obj.ToString(Formatting.None);
    }
}
