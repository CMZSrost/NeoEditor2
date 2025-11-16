﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Helpers;

namespace NeoEditor.ViewModels.Controls;

public partial class EditXmlViewModel : ObservableObject
{
    [ObservableProperty] public partial ObservableCollection<XmlNodeItem> Nodes { get; set; } = [];
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial ObservableCollection<XmlNodeItem> FilteredNodes { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<DtoTabItem> DtoTabs { get; set; } = [];

    private string? _currentFilePath;
    private bool _isLoading;

    partial void OnSearchTextChanged(string value)
    {
        FilterNodes();
    }

    public async Task LoadXmlAsync(string xmlFilePath)
    {
        Debug.WriteLine($"[EditXmlViewModel {GetHashCode()}] LoadXmlAsync called for: {xmlFilePath}");
        
        // 防止重复加载同一文件
        if (_isLoading)
        {
            Debug.WriteLine($"[EditXmlViewModel {GetHashCode()}] Already loading, skipping: {xmlFilePath}");
            return;
        }

        if (_currentFilePath == xmlFilePath && DtoTabs.Count > 0)
        {
            Debug.WriteLine($"[EditXmlViewModel {GetHashCode()}] File already loaded: {xmlFilePath}");
            return;
        }

        _isLoading = true;
        try
        {
            Debug.WriteLine($"[EditXmlViewModel {GetHashCode()}] Loading XML from file: {xmlFilePath}");
            var xDoc = await GameXmlLoader.LoadXmlToDom(xmlFilePath);
            Nodes.Clear();
            FilteredNodes.Clear();
            DtoTabs.Clear();

            if (xDoc.Root is null) return;

            ParseElement(xDoc.Root, null, 0);
            FilterNodes();
            ParseDtos(xDoc.Root);

            _currentFilePath = xmlFilePath;

            // 调试输出
            Debug.WriteLine($"[EditXmlViewModel {GetHashCode()}] DtoTabs Count: {DtoTabs.Count}");
            foreach (var tab in DtoTabs) Debug.WriteLine($"[EditXmlViewModel {GetHashCode()}] Tab: {tab.Name}, Items: {tab.Items.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EditXmlViewModel {GetHashCode()}] Error loading XML: {ex.Message}");
            throw;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ParseDtos(XElement root)
    {
        // 调试输出
        Debug.WriteLine($"Root element: {root.Name.LocalName}");

        // 尝试查找 database 元素（可能在根元素或子元素中）
        // 排除 structure_schemas 下的 database 元素（schema 定义）
        XElement? database;
        if (root.Name.LocalName == "database")
        {
            database = root;
        }
        else
        {
            // 查找所有 database 元素，但排除在 structure_schemas 或任何带命名空间的父元素下的
            database = root.Descendants()
                .Where(e => e.Name.LocalName == "database")
                .FirstOrDefault(e => 
                    // 确保不在 structure_schemas 下
                    !e.Ancestors().Any(a => a.Name.LocalName == "structure_schemas")
                );
        }

        if (database == null)
        {
            Debug.WriteLine("Database element not found!");
            return;
        }

        Debug.WriteLine($"Database element found: {database.Name.LocalName}, parent: {database.Parent?.Name.LocalName ?? "none"}");

        var groupedTables = database.Elements()
            .Where(e => e.Name.LocalName == "table")
            .GroupBy(t => t.Attribute("name")?.Value ?? "Unknown");

        Debug.WriteLine($"Table groups count: {groupedTables.Count()}");

        foreach (var group in groupedTables)
        {
            var tableName = group.Key;
            Debug.WriteLine($"Processing table: {tableName}, count: {group.Count()}");

            var dtoType = ResolveDtoType(tableName);
            if (dtoType == null)
            {
                Debug.WriteLine($"  DTO type not found for: {tableName}");
                continue;
            }

            Debug.WriteLine($"  Found DTO type: {dtoType.Name}");

            var instances = new List<object>();

            foreach (var table in group)
            {
                var dtoInstance = Activator.CreateInstance(dtoType);
                if (dtoInstance == null) continue;

                foreach (var column in table.Elements().Where(e => e.Name.LocalName == "column"))
                {
                    var propName = column.Attribute("name")?.Value;
                    if (string.IsNullOrWhiteSpace(propName)) continue;

                    var rawValue = column.Value;
                    SetProperty(dtoInstance, dtoType, propName, rawValue);
                }

                instances.Add(dtoInstance);
            }

            if (instances.Count > 0)
            {
                Debug.WriteLine($"  Adding tab with {instances.Count} instances");
                DtoTabs.Add(new DtoTabItem
                {
                    Name = dtoType.Name,
                    Items = new ObservableCollection<object>(instances),
                    DtoType = dtoType
                });
            }
        }
    }

    private static Type? ResolveDtoType(string tableName)
    {
        var asm = Assembly.GetExecutingAssembly();

        // 尝试多种命名变体
        var candidates = new[]
        {
            tableName.ToLower(), // attackmodes -> attackmodes
            tableName.ToLower().TrimEnd('s'), // attackmodes -> attackmode
            tableName, // 保持原样
            tableName.TrimEnd('s') // 移除末尾 s
        };

        var type = asm.GetTypes()
            .FirstOrDefault(t => (t.Namespace == "NeoEditor.Data.Models.Dto" || t.Namespace == "NeoEditor.Dto") &&
                                 candidates.Contains(t.Name, StringComparer.OrdinalIgnoreCase));
        return type;
    }


    private static void SetProperty(object instance, Type type, string propName, string rawValue)
    {
        var prop = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase) &&
                                 p.CanWrite);
        if (prop == null) return;

        try
        {
            var converted = ConvertTo(rawValue, prop.PropertyType);
            prop.SetValue(instance, converted);
        }
        catch
        {
            // 忽略转换错误
        }
    }

    private static object? ConvertTo(string value, Type targetType)
    {
        if (targetType == typeof(string)) return value;
        if (string.IsNullOrWhiteSpace(value))
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        try
        {
            if (targetType == typeof(bool))
                return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);

            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, true);

            return Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType);
        }
        catch
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }
    }

    private void ParseElement(XElement? element, XmlNodeItem? parent, int level)
    {
        if (element == null) return;
        var item = new XmlNodeItem
        {
            Name = element.Name.LocalName,
            Value = element.HasElements ? string.Empty : element.Value,
            Level = level,
            Attributes = string.Join(", ", element.Attributes().Select(a => $"{a.Name}={a.Value}"))
        };
        Nodes.Add(item);
        foreach (var child in element.Elements())
            ParseElement(child, item, level + 1);
    }

    private void FilterNodes()
    {
        FilteredNodes.Clear();
        var query = Nodes.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(n =>
                n.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                n.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                n.Attributes.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        foreach (var node in query)
            FilteredNodes.Add(node);
    }
}

public class XmlNodeItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Attributes { get; set; } = string.Empty;
    public int Level { get; set; }
}

public class DtoTabItem
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<object> Items { get; set; } = [];
    public Type? DtoType { get; set; }
}