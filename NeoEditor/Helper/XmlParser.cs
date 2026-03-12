using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Helper;

public class XmlParser
{
    private const string DefaultDatabaseName = "neogame";

    private readonly ILogger<XmlParser> _logger;

    public XmlParser(ILogger<XmlParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     从 XML 文档中导入指定类型的所有实体
    /// </summary>
    /// <typeparam name="T">实体类型，必须有无参构造函数</typeparam>
    /// <param name="doc">XML 文档（pma_xml_export 格式）</param>
    /// <param name="modId">当前 Mod 的 ID</param>
    /// <param name="filePath">xml文件的路径</param>
    /// <returns>实体列表</returns>
    public IList<T> ImportEntities<T>(XDocument doc, int modId, string filePath) where T : IEntity, new()
    {
        var type = typeof(T);
        var tableName = ResolveTableName(type);
        var databaseNode = doc.Root?.XPathSelectElement("//database[1]");
        if (databaseNode == null) return new List<T>();

        var tableNodes = databaseNode.Elements("table")
            .Where(e => e.Attribute("name")?.Value == tableName);

        var result = new List<T>();

        var realKey = ResolveEntityKeyColumnName(type);
        // Console.WriteLine($"keyName: {realKey} for {typeof(T).Name}");

        foreach (var tableNode in tableNodes)
        {
            try
            {
                var entity = new T
                {
                    ModId = modId,
                    FilePath = filePath,
                    EntityId = Sha256Helper.CreateEntityId(tableName, modId,
                        tableNode.Elements("column")
                            .FirstOrDefault(c => c.Attribute("name")?.Value == realKey)?.Value ?? "")
                };

                // 遍历所有列节点
                foreach (var colNode in tableNode.Elements("column"))
                {
                    var colName = colNode.Attribute("name")?.Value;
                    if (colName == null) continue;

                    // 查找映射到该列名的属性
                    var prop = type.GetProperties()
                        .FirstOrDefault(p => p.GetCustomAttribute<ColumnAttribute>()?.Name == colName);
                    if (prop == null) continue;

                    var valueStr = colNode.Value;

                    try
                    {
                        var convertedValue = ConvertValue(valueStr, prop.PropertyType);
                        if (convertedValue is null) continue;
                        prop.SetValue(entity, convertedValue);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(
                            $"load failed\n{tableNode}\n column {colName} with value '{valueStr}' {e.Message}");
                        throw;
                    }
                }

                result.Add(entity);
            }
            catch (Exception e)
            {
                _logger.LogWarning($"parse table failed: {e.Message}");
            }
        }

        return result;
    }

    public XDocument Export(IEnumerable<IEntity> entities, string databaseName = DefaultDatabaseName)
    {
        var normalizedEntities = entities
            .OrderBy(entity => ResolveTableName(entity.GetType()), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entity => ResolveEntitySortKey(entity, entity.GetType()), StringComparer.Ordinal)
            .ToList();

        var databaseElement = new XElement("database",
            new XAttribute("name", string.IsNullOrWhiteSpace(databaseName) ? DefaultDatabaseName : databaseName));

        foreach (var entityGroup in normalizedEntities.GroupBy(entity => entity.GetType())
                     .OrderBy(group => ResolveTableName(group.Key), StringComparer.OrdinalIgnoreCase))
        {
            var tableName = ResolveTableName(entityGroup.Key);
            var columnProperties = GetColumnProperties(entityGroup.Key);

            foreach (var entity in entityGroup)
            {
                var tableElement = new XElement("table", new XAttribute("name", tableName));
                foreach (var property in columnProperties)
                {
                    var columnName = ResolveColumnName(property);
                    tableElement.Add(new XElement("column",
                        new XAttribute("name", columnName),
                        FormatValue(property.GetValue(entity), property.PropertyType)));
                }

                databaseElement.Add(tableElement);
            }
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("pma_xml_export",
                new XAttribute("version", "1.0"),
                databaseElement));
    }

    private object? ConvertValue(string str, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(str)) return null;
        try
        {
            if (targetType == typeof(string)) return str;
            if (targetType == typeof(int))
                return string.IsNullOrWhiteSpace(str) ? null : int.Parse(str);

            if (targetType == typeof(float))
                return string.IsNullOrWhiteSpace(str) ? null : float.Parse(str, CultureInfo.InvariantCulture);

            if (targetType == typeof(bool)) return str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (targetType.BaseType == typeof(Enum)) return Enum.Parse(targetType, str);
            // 可根据需要扩展其他类型
            return Convert.ChangeType(str, targetType);
        }
        catch (Exception e)
        {
            _logger.LogWarning($"Conversion error: cannot convert '{str}' to {targetType.Name}: {e.Message}");
            throw;
        }
    }

    private static string ResolveTableName(Type type)
    {
        return type.GetCustomAttribute<TableAttribute>()?.Name
               ?? throw new InvalidOperationException($"Type {type.Name} is missing XmlTableAttribute.");
    }

    private static IReadOnlyList<PropertyInfo> GetColumnProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.DeclaringType != typeof(IEntity) &&
                               property.GetCustomAttribute<ColumnAttribute>() != null)
            .OrderBy(property => property.MetadataToken)
            .ToArray();
    }

    private static string ResolveColumnName(PropertyInfo property)
    {
        return property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
    }

    private static string ResolveEntityKeyColumnName(Type type)
    {
        var keyProperty = ResolveEntityKeyProperty(type);
        return keyProperty is null ? string.Empty : ResolveColumnName(keyProperty);
    }

    private static PropertyInfo? ResolveEntityKeyProperty(Type type)
    {
        var indexAttribute = type.GetCustomAttributes<IndexAttribute>().FirstOrDefault();
        var keyPropertyName = indexAttribute?.PropertyNames.FirstOrDefault(name => name != nameof(IEntity.EntityId));

        if (string.IsNullOrWhiteSpace(keyPropertyName))
        {
            return GetColumnProperties(type).FirstOrDefault();
        }

        return type.GetProperty(keyPropertyName, BindingFlags.Instance | BindingFlags.Public);
    }

    private static string ResolveEntitySortKey(IEntity entity, Type entityType)
    {
        var keyProperty = ResolveEntityKeyProperty(entityType);
        var keyText = keyProperty is null
            ? string.Empty
            : FormatValue(keyProperty.GetValue(entity), keyProperty.PropertyType);
        return string.Concat(keyText, "|", entity.EntityId);
    }

    private static string FormatValue(object? value, Type propertyType)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var effectiveType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (effectiveType == typeof(bool))
        {
            return (bool)value ? "1" : "0";
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return value.ToString() ?? string.Empty;
    }
}