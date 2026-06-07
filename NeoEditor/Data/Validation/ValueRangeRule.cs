using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Validation;

/// <summary>
/// Checks numeric fields for reasonable value ranges.
/// </summary>
public class ValueRangeRule : IValidationRule
{
    private static readonly Dictionary<string, (double Min, double Max)> ChanceFields = new()
    {
        ["fChance"] = (0.0, 1.0),
        ["fLootChance"] = (0.0, 1.0),
        ["fAccidentChance"] = (0.0, 1.0),
        ["fCreatureChance"] = (0.0, 1.0),
        ["fDetect"] = (0.0, 1.0),
    };

    public void Validate(IReadOnlyList<IEntity> entities, ValidationReport report)
    {
        foreach (var entity in entities)
        {
            var type = entity.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                if (colAttr is null) continue;

                if (ChanceFields.TryGetValue(colAttr.Name ?? "", out var range))
                {
                    if (prop.GetValue(entity) is double val && (val < range.Min || val > range.Max))
                    {
                        var colName = colAttr.Name ?? prop.Name;
                        report.Warning(entity.Subject, colName,
                            $"Value {val} is outside expected range [{range.Min}, {range.Max}]");
                    }
                }
            }
        }
    }
}
