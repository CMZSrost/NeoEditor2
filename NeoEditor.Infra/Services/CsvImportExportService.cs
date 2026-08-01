using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Services;

public class CsvImportExportService
{
    public List<object> ParseCsvToEntities(string csvFilePath, Type entityType, int modId, string filePath)
    {
        var lines = File.ReadAllLines(csvFilePath);
        if (lines.Length < 2) return [];

        var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
        var colProps = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity)
                && p.GetCustomAttribute<ColumnAttribute>() != null
                && p.CanWrite)
            .ToList();

        var mappings = new List<(int CsvIdx, PropertyInfo Prop)>();
        foreach (var prop in colProps)
        {
            var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
            var csvIdx = Array.IndexOf(headers, colName);
            if (csvIdx >= 0) mappings.Add((csvIdx, prop));
        }

        var result = new List<object>();
        for (var li = 1; li < lines.Length; li++)
        {
            var fields = ParseCsvLine(lines[li]);
            var entity = Activator.CreateInstance(entityType) as IEntity;
            if (entity is null) continue;

            entity.ModId = modId;
            entity.FilePath = filePath;
            entity.EntityId = $"import_{Guid.NewGuid():N}";

            foreach (var (csvIdx, prop) in mappings)
            {
                var raw = csvIdx < fields.Length ? fields[csvIdx] : "";
                var converted = ConvertValue(raw, prop.PropertyType);
                if (converted is not null)
                    prop.SetValue(entity, converted);
            }

            result.Add(entity);
        }

        return result;
    }

    public void ExportEntitiesToCsv(IEnumerable<IEntity> entities, Type entityType, string outputPath)
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return;

        var colProps = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity)
                && p.GetCustomAttribute<ColumnAttribute>() != null)
            .OrderBy(p => p.MetadataToken)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", colProps.Select(p =>
        {
            var name = p.GetCustomAttribute<ColumnAttribute>()?.Name ?? p.Name;
            return name.Contains(',') ? $"\"{name}\"" : name;
        })));

        foreach (var entity in entityList)
        {
            sb.AppendLine(string.Join(",", colProps.Select(p =>
            {
                var val = p.GetValue(entity)?.ToString() ?? "";
                return val.Contains(',') || val.Contains('"') || val.Contains('\n')
                    ? $"\"{val.Replace("\"", "\"\"")}\""
                    : val;
            })));
        }

        File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
    }

    public static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();
        foreach (var ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; continue; }
            if (ch == ',' && !inQuotes) { result.Add(current.ToString().Trim()); current.Clear(); continue; }
            current.Append(ch);
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    public static object? ConvertValue(string str, Type targetType)
    {
        if (targetType == typeof(string)) return str;
        if (targetType == typeof(int)) return int.TryParse(str, out var i) ? i : null;
        if (targetType == typeof(float) || targetType == typeof(double))
            return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (targetType == typeof(bool)) return str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (targetType.IsEnum) return Enum.TryParse(targetType, str, out var ev) ? ev : null;
        try { return Convert.ChangeType(str, targetType); }
        catch { return null; }
    }

    public List<CsvDiffRow> CompareEntities(List<object> imported, List<object> existing, Type entityType)
    {
        var result = new List<CsvDiffRow>();
        var keyProp = ResolveEntityKeyProperty(entityType);
        var colProps = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity)
                && p.GetCustomAttribute<ColumnAttribute>() != null)
            .ToList();

        var existingByKey = new Dictionary<object, object>();
        foreach (var entity in existing)
        {
            var key = keyProp?.GetValue(entity);
            if (key is not null) existingByKey[key] = entity;
        }

        foreach (var importedEntity in imported)
        {
            var key = keyProp?.GetValue(importedEntity);
            if (key is not null && existingByKey.TryGetValue(key, out var existingEntity))
            {
                foreach (var prop in colProps)
                {
                    var oldVal = prop.GetValue(existingEntity)?.ToString() ?? "";
                    var newVal = prop.GetValue(importedEntity)?.ToString() ?? "";
                    if (oldVal != newVal)
                    {
                        result.Add(new CsvDiffRow
                        {
                            Key = key.ToString() ?? "?",
                            Field = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name,
                            OldValue = oldVal,
                            NewValue = newVal,
                            Status = DiffStatus.Modified
                        });
                    }
                }
                existingByKey.Remove(key);
            }
            else
            {
                var keyStr = key?.ToString() ?? "?";
                foreach (var prop in colProps)
                {
                    var val = prop.GetValue(importedEntity)?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(val))
                    {
                        result.Add(new CsvDiffRow
                        {
                            Key = keyStr,
                            Field = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name,
                            OldValue = "",
                            NewValue = val,
                            Status = DiffStatus.Added
                        });
                    }
                }
            }
        }

        return result;
    }

    private static PropertyInfo? ResolveEntityKeyProperty(Type entityType)
    {
        return entityType.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == "id"
                              || p.GetCustomAttribute<ColumnAttribute>()?.Name == "nID");
    }
}

public class CsvDiffRow
{
    public string Key { get; set; } = "";
    public string Field { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    public DiffStatus Status { get; set; }
}

public enum DiffStatus
{
    Added,
    Modified,
    Unchanged
}
