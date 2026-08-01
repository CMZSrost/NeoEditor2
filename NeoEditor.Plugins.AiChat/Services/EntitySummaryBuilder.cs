using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Plugins.AiChat.Services;

/// <summary>
/// Converts game entities to compact, searchable text summaries for RAG indexing.
/// Each summary is ~150-400 chars containing entity type, ID, subject, key properties,
/// and reference field values.
/// </summary>
public class EntitySummaryBuilder
{
    /// <summary>
    /// Build a compact text summary of an entity for embedding/search.
    /// </summary>
    public string BuildSummary(IEntity entity)
    {
        var type = entity.GetType();
        var sb = new StringBuilder();
        sb.Append($"Entity: {type.Name} / {entity.EntityId}");

        var subject = entity.Subject;
        if (!string.IsNullOrWhiteSpace(subject) && subject != entity.EntityId)
            sb.Append($" | Subject: {subject}");

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                && p.Name is not ("EntityId" or "Subject" or "ModId" or "IsDirty"));

        var keyValues = new List<string>();
        var refValues = new List<string>();

        foreach (var p in props)
        {
            var value = p.GetValue(entity);
            if (value is null) continue;

            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            if (refAttr is not null)
            {
                var valStr = value.ToString();
                if (!string.IsNullOrWhiteSpace(valStr))
                    refValues.Add($"{p.Name}: {valStr}");
            }
            else if (p.PropertyType == typeof(int) || p.PropertyType == typeof(double)
                || p.PropertyType == typeof(float) || p.PropertyType == typeof(long))
            {
                var numVal = value.ToString();
                if (numVal != "0" && numVal != "0.0")
                    keyValues.Add($"{p.Name}={numVal}");
            }
            else if (p.PropertyType == typeof(string))
            {
                var strVal = value.ToString();
                if (!string.IsNullOrWhiteSpace(strVal) && strVal.Length < 80)
                    keyValues.Add($"{p.Name}: {strVal}");
            }
            else if (p.PropertyType == typeof(bool))
            {
                if ((bool)value)
                    keyValues.Add(p.Name);
            }
        }

        if (keyValues.Count > 0)
            sb.Append($" | Props: {string.Join(", ", keyValues.Take(10))}");

        if (refValues.Count > 0)
            sb.Append($" | Refs: {string.Join(", ", refValues.Take(8))}");

        return sb.ToString();
    }
}
