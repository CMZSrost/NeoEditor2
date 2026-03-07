using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using Newtonsoft.Json;
using Serilog.Core;

namespace NeoEditor.Services;

public class XmlParser
{
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
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr == null)
            throw new InvalidOperationException($"Type {type.Name} is missing XmlTableAttribute.");

        var tableName = tableAttr.Name;
        var databaseNode = doc.Root?.XPathSelectElement("//database[1]");
        if (databaseNode == null) return new List<T>();

        var tableNodes = databaseNode.Elements("table")
            .Where(e => e.Attribute("name")?.Value == tableName);

        var result = new List<T>();

        var keyName = typeof(T).GetCustomAttribute<IndexAttribute>()
            .PropertyNames.First(s => s != nameof(IEntity.EntityId));
        var keyProp = type.GetProperties()
            .FirstOrDefault(p => p.Name == keyName);
        var realKey = keyProp?.GetCustomAttribute<ColumnAttribute>().Name ?? keyName;
        Console.WriteLine($"keyName: {realKey} for {typeof(T).Name}");

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
}