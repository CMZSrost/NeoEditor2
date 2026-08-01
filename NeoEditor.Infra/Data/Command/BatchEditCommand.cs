using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
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

    /// <summary>Source entity ModId from the first edit record. Used as persistence target
    /// fallback when the merge editor (ProfileId=-1) has no valid ModInfo binding.</summary>
    public int SourceModId => _edits.Count > 0 ? _edits[0].Entity.ModId : -1;

    /// <inheritdoc cref="IEditorCommand.GetAffectedEntityIds"/>
    public IReadOnlySet<string> GetAffectedEntityIds() =>
        new HashSet<string>(_edits.Select(e => e.Entity.EntityId));

    public BatchEditCommand(List<EditRecord> edits, Action onChanged)
    {
        _edits = edits;
        _onChanged = onChanged;
    }

    public void Execute()
    {
        foreach (var edit in _edits)
        {
            edit.Property.SetValue(edit.Entity, edit.NewValue);
            // TODO M8.4: Wire via IWorkspaceSession.MarkEntityDirty() or EditTrackingStore
        }
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
