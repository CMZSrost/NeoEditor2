using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Data.Command;
using NeoEditor.Data.Model.Game;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Services;

/// <summary>
/// Serializes/deserializes IEditorCommand to/from JSON for DB persistence.
/// Commands self-serialize via ISerializableCommand — no reflection on private fields.
/// </summary>
public static class CommandSerializer
{
    #region Serialization

    public static (string commandType, string serializedData) Serialize(IEditorCommand command)
    {
        if (command is not ISerializableCommand sc)
            throw new ArgumentException($"Command type {command.GetType().Name} does not implement ISerializableCommand");
        return (sc.CommandType, sc.Serialize());
    }

    #endregion

    #region Deserialization

    /// <summary>
    /// Deserialize a command from WAL storage. No longer requires ObservableCollection
    /// resolver — entities are resolved from IHostService's cache.
    /// </summary>
    public static IEditorCommand Deserialize(
        string commandType, string serializedData,
        Func<string, Type, IEntity?> entityResolver,
        Action onChanged)
    {
        return commandType switch
        {
            "EditCell" => DeserializeEditCell(serializedData, entityResolver, onChanged),
            "AddEntity" => DeserializeAddEntity(serializedData),
            "DeleteEntity" => DeserializeDeleteEntity(serializedData, entityResolver),
            "BatchEdit" => DeserializeBatchEdit(serializedData, entityResolver, onChanged),
            _ => throw new ArgumentException($"Unknown command type: {commandType}")
        };
    }

    private static IEditorCommand DeserializeEditCell(
        string data, Func<string, Type, IEntity?> entityResolver, Action onChanged)
    {
        var obj = JObject.Parse(data);
        var entityId = obj["entityId"]!.Value<string>()!;
        var entityTypeName = obj["entityType"]!.Value<string>()!;
        if (!Constants.GameTypes.TryGetValue(entityTypeName, out var entityType))
            throw new InvalidOperationException($"Unknown entity type '{entityTypeName}' in command_log");
        var propertyName = obj["propertyName"]!.Value<string>()!;
        var columnName = obj["columnName"]!.Value<string>()!;

        var entity = entityResolver(entityId, entityType)
            ?? throw new InvalidOperationException($"Entity {entityId} ({entityTypeName}) not found for replay");

        var property = entityType.GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found on {entityTypeName}");

        var oldValue = DeserializeValue(obj["oldValue"]!, property.PropertyType);
        var newValue = DeserializeValue(obj["newValue"]!, property.PropertyType);

        return new EditCellCommand(entity, property, columnName, oldValue, newValue, onChanged);
    }

    private static IEditorCommand DeserializeAddEntity(string data)
    {
        var obj = JObject.Parse(data);
        var entityTypeName = obj["entityType"]!.Value<string>()!;
        var entityData = (JObject)obj["entityData"]!;

        var entityType = Constants.GameTypes[entityTypeName];
        var entity = (IEntity)DeserializeEntity(entityType, entityData);

        return new AddEntityCommand(entityTypeName, entity);
    }

    private static IEditorCommand DeserializeDeleteEntity(
        string data,
        Func<string, Type, IEntity?> entityResolver)
    {
        var obj = JObject.Parse(data);
        var entityId = obj["entityId"]!.Value<string>()!;
        var entityTypeName = obj["entityType"]!.Value<string>()!;
        var entityType = Constants.GameTypes[entityTypeName];

        var entity = entityResolver(entityId, entityType)
            ?? throw new InvalidOperationException($"Entity {entityId} ({entityTypeName}) not found for replay");

        return new DeleteEntityCommand(entityTypeName, entity);
    }

    private static IEditorCommand DeserializeBatchEdit(
        string data, Func<string, Type, IEntity?> entityResolver, Action onChanged)
    {
        var obj = JObject.Parse(data);
        var editsArr = (JArray)obj["edits"]!;
        var edits = new List<EditRecord>();
        var skipped = 0;

        foreach (var editObj in editsArr.Cast<JObject>())
        {
            try
            {
                var entityId = editObj["entityId"]!.Value<string>()!;
                var entityTypeName = editObj["entityType"]!.Value<string>()!;
                if (!Constants.GameTypes.TryGetValue(entityTypeName, out var entityType))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DeserializeBatchEdit] unknown type '{entityTypeName}', skipping edit");
                    skipped++;
                    continue;
                }
                var propertyName = editObj["propertyName"]!.Value<string>()!;
                var columnName = editObj["columnName"]!.Value<string>()!;

                var entity = entityResolver(entityId, entityType);
                if (entity == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DeserializeBatchEdit] entity {entityId} ({entityTypeName}) not found, skipping edit");
                    skipped++;
                    continue;
                }

                var property = entityType.GetProperty(propertyName);
                if (property == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DeserializeBatchEdit] property '{propertyName}' not found on {entityTypeName}, skipping edit");
                    skipped++;
                    continue;
                }

                var oldValue = DeserializeValue(editObj["oldValue"]!, property.PropertyType);
                var newValue = DeserializeValue(editObj["newValue"]!, property.PropertyType);

                edits.Add(new EditRecord(entity, property, columnName, oldValue, newValue));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DeserializeBatchEdit] failed to deserialize single edit: {ex.Message}, skipping");
                skipped++;
            }
        }

        if (skipped > 0)
            System.Diagnostics.Debug.WriteLine(
                $"[DeserializeBatchEdit] {edits.Count}/{edits.Count + skipped} edits restored ({skipped} skipped)");

        return new BatchEditCommand(edits, onChanged);
    }

    #endregion

    #region Helpers

    private static object? DeserializeValue(JToken token, Type targetType)
    {
        if (token.Type == JTokenType.Null) return null;
        if (targetType == typeof(string)) return token.Value<string>();
        if (targetType == typeof(int)) return token.Value<int>();
        if (targetType == typeof(float)) return token.Value<float>();
        if (targetType == typeof(double)) return token.Value<double>();
        if (targetType == typeof(bool)) return token.Value<bool>();
        if (targetType == typeof(byte)) return token.Value<byte>();
        if (targetType.IsEnum) return Enum.ToObject(targetType, token.Value<int>());
        return token.ToObject(targetType);
    }

    private static object DeserializeEntity(Type entityType, JObject data)
    {
        var entity = Activator.CreateInstance(entityType)!;
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr == null) continue;
            var token = data[prop.Name];
            if (token == null || token.Type == JTokenType.Null) continue;
            var val = DeserializeValue(token, prop.PropertyType);
            prop.SetValue(entity, val);
        }
        return entity;
    }

    #endregion
}
