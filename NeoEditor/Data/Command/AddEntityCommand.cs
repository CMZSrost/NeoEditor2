using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Data.Command;

public class AddEntityCommand : ISerializableCommand
{
    private readonly ObservableCollection<object> _collection;
    private readonly IEntity _entity;
    private readonly Action _onChanged;

    public string Description =>
        $"Add {_entity.GetType().Name} id={(_entity as dynamic)?.Id ?? EntityIdToStr()}";
    public string CommandType => "AddEntity";

    public AddEntityCommand(ObservableCollection<object> collection, IEntity entity, Action onChanged)
    {
        _collection = collection;
        _entity = entity;
        _onChanged = onChanged;
    }

    public void Execute()
    {
        _collection.Add(_entity);
        _onChanged();
    }

    public void Undo()
    {
        _collection.Remove(_entity);
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
            ["entityType"] = _entity.GetType().Name,
            ["tabType"] = _entity.GetType().Name,
            ["entityData"] = entityData
        };
        return obj.ToString(Formatting.None);
    }

    private string EntityIdToStr()
    {
        var keyProp = _entity.GetType().GetProperty("Id")
            ?? _entity.GetType().GetProperty("EntityId");
        return keyProp?.GetValue(_entity)?.ToString() ?? "?";
    }
}
