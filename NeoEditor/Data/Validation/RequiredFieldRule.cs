using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Validation;

/// <summary>
/// Checks that key name/property fields are not empty.
/// </summary>
public class RequiredFieldRule : IValidationRule
{
    private static readonly HashSet<string> RequiredPropNames = ["strName", "Name", "strPropertyName", "strDesc"];

    public void Validate(IReadOnlyList<IEntity> entities, ValidationReport report)
    {
        foreach (var entity in entities)
        {
            var type = entity.GetType();
            foreach (var propName in RequiredPropNames)
            {
                var prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
                if (prop is null) continue;
                var value = prop.GetValue(entity)?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) continue;

                var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                report.Warning(entity.Subject, colName, $"'{colName}' is empty — may cause issues in game");
            }
        }
    }
}
