using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;
using NeoEditor.Core.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Plugins.Mcp.Tools;

/// <summary>
/// Single source of truth for MCP tool metadata: enumerates the [McpServerTool] methods
/// on <see cref="EditorTools"/> and builds their names, descriptions and JSON schemas.
/// Shared by the stdio/TCP server (<see cref="Server.McpServerHost"/>) and the in-process
/// provider (<see cref="McpToolExecutor"/>) so the two reflection paths cannot drift apart.
/// </summary>
public static class EditorToolRegistry
{
    /// <summary>All [McpServerTool]-annotated methods on EditorTools, ordered by name.</summary>
    public static IReadOnlyList<MethodInfo> EnumerateToolMethods()
        => typeof(EditorTools)
            .GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0)
            .OrderBy(m => m.Name)
            .ToList();

    /// <summary>Tool description: the [Description] attribute or the method name.</summary>
    public static string GetDescription(MethodInfo method)
        => method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? method.Name;

    /// <summary>Build the in-process tool info (name, description, input JSON Schema).</summary>
    public static McpToolInfo BuildToolInfo(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var props = new JObject();
        var required = new JArray();
        foreach (var p in parameters)
        {
            var paramDesc = p.GetCustomAttributes(false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()?.Description ?? p.Name!;
            props[p.Name!] = new JObject
            {
                ["type"] = MapType(p.ParameterType),
                ["description"] = paramDesc
            };
            if (!p.IsOptional)
                required.Add(p.Name);
        }

        var schema = new JObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = required
        };

        return new McpToolInfo(method.Name, GetDescription(method), schema.ToString());
    }

    private static string MapType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long)) return "integer";
        if (type == typeof(double) || type == typeof(float)) return "number";
        if (type == typeof(bool)) return "boolean";
        return "string";
    }
}
