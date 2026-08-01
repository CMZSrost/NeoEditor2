using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Plugins.Mcp.Tools;

/// <summary>
/// Implements <see cref="IMcpToolProvider"/> for in-process tool calls (AI Chat, CLI).
/// Delegates to <see cref="EditorTools"/> which also serves the MCP stdio server.
/// Registered in DI as the bridge between plugins — R17 compliant.
/// </summary>
public class McpToolExecutor : IMcpToolProvider
{
    private readonly EditorTools _tools;

    public McpToolExecutor(EditorTools tools)
    {
        _tools = tools;
    }

    public IReadOnlyList<McpToolInfo> GetTools()
    {
        // Extract tool metadata from EditorTools via reflection on [McpServerTool] methods
        return typeof(EditorTools)
            .GetMethods()
            .Where(m => m.GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "McpServerToolAttribute"))
            .Select(m =>
            {
                var descAttr = m.GetCustomAttributes(false)
                    .FirstOrDefault(a => a.GetType().Name == "DescriptionAttribute");
                var description = descAttr is System.ComponentModel.DescriptionAttribute d
                    ? d.Description : m.Name;

                var parameters = m.GetParameters();
                var props = new JObject();
                var required = new JArray();
                foreach (var p in parameters)
                {
                    var paramDesc = p.GetCustomAttributes(false)
                        .OfType<System.ComponentModel.DescriptionAttribute>()
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

                return new McpToolInfo(m.Name, description, schema.ToString());
            })
            .ToList();
    }

    public async Task<string> ExecuteToolAsync(string toolName, string argumentsJson,
        CancellationToken ct = default)
    {
        var method = typeof(EditorTools).GetMethod(toolName);
        if (method is null)
            return JsonConvert.SerializeObject(new { error = $"Unknown tool: {toolName}" });

        var args = string.IsNullOrWhiteSpace(argumentsJson)
            ? new JObject()
            : JObject.Parse(argumentsJson);

        var parameters = method.GetParameters();
        var paramValues = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            var token = args[p.Name!];
            if (token is null && p.IsOptional)
            {
                paramValues[i] = p.DefaultValue;
            }
            else if (token is not null)
            {
                paramValues[i] = token.ToObject(p.ParameterType);
            }
            else
            {
                return JsonConvert.SerializeObject(new
                    { error = $"Missing required parameter: {p.Name}" });
            }
        }

        var task = (Task<string>?)method.Invoke(_tools, paramValues);
        if (task is null)
            return JsonConvert.SerializeObject(new { error = "Tool execution failed" });

        await task.ConfigureAwait(false);
        return task.Result;
    }

    private static string MapType(System.Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long)) return "integer";
        if (type == typeof(double) || type == typeof(float)) return "number";
        if (type == typeof(bool)) return "boolean";
        return "string";
    }
}
