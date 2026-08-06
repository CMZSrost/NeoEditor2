using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
/// Tool metadata comes from the shared <see cref="EditorToolRegistry"/>.
/// </summary>
public class McpToolExecutor : IMcpToolProvider
{
    private readonly EditorTools _tools;

    public McpToolExecutor(EditorTools tools)
    {
        _tools = tools;
    }

    public IReadOnlyList<McpToolInfo> GetTools()
        => EditorToolRegistry.EnumerateToolMethods()
            .Select(EditorToolRegistry.BuildToolInfo)
            .ToList();

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
}
