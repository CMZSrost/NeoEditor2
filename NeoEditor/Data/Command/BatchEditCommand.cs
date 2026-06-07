using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Data.Command;

public class BatchEditCommand : ISerializableCommand
{
    private readonly List<EditRecord> _edits;
    private readonly Action _onChanged;

    public string Description =>
        _edits.Count == 1 ? $"Edit: '{_edits[0].OldValue}' → '{_edits[0].NewValue}'"
        : $"Batch edit {_edits.Count} cells";
    public string CommandType => "BatchEdit";

    public BatchEditCommand(List<EditRecord> edits, Action onChanged)
    {
        _edits = edits;
        _onChanged = onChanged;
    }

    public void Execute()
    {
        foreach (var edit in _edits)
            edit.Property.SetValue(edit.Entity, edit.NewValue);
        _onChanged();
    }

    public void Undo()
    {
        for (var i = _edits.Count - 1; i >= 0; i--)
        {
            var edit = _edits[i];
            edit.Property.SetValue(edit.Entity, edit.OldValue);
        }
        _onChanged();
    }

    public string Serialize()
    {
        var arr = new JArray();
        foreach (var edit in _edits)
        {
            arr.Add(new JObject
            {
                ["entityId"] = edit.Entity.EntityId,
                ["entityType"] = edit.Entity.GetType().Name,
                ["propertyName"] = edit.Property.Name,
                ["columnName"] = edit.ColumnName,
                ["oldValue"] = edit.OldValue != null ? JToken.FromObject(edit.OldValue) : JValue.CreateNull(),
                ["newValue"] = edit.NewValue != null ? JToken.FromObject(edit.NewValue) : JValue.CreateNull()
            });
        }
        var obj = new JObject { ["edits"] = arr };
        return obj.ToString(Formatting.None);
    }
}
