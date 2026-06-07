using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Data.Command;

public class DeleteEntityCommand : ISerializableCommand
{
    private readonly ObservableCollection<object> _collection;
    private readonly IEntity _entity;
    private readonly int _index;
    private readonly Action _onChanged;

    public string Description =>
        $"Delete {_entity.GetType().Name} id={(_entity as dynamic)?.Id ?? _entity.EntityId}";
    public string CommandType => "DeleteEntity";

    public DeleteEntityCommand(ObservableCollection<object> collection, IEntity entity, Action onChanged)
    {
        _collection = collection;
        _entity = entity;
        _onChanged = onChanged;
        _index = collection.IndexOf(entity);
        if (_index < 0) _index = collection.Count;
    }

    public void Execute()
    {
        _collection.Remove(_entity);
        _onChanged();
    }

    public void Undo()
    {
        var insertAt = Math.Min(_index, _collection.Count);
        _collection.Insert(insertAt, _entity);
        _onChanged();
    }

    public string Serialize()
    {
        var entityData = new JObject();
        foreach (var prop in _entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
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
            ["entityId"] = _entity.EntityId,
            ["entityType"] = _entity.GetType().Name,
            ["tabType"] = _entity.GetType().Name,
            ["index"] = _index,
            ["entityData"] = entityData
        };
        return obj.ToString(Formatting.None);
    }
}
