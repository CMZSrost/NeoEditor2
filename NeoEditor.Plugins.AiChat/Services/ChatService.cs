using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using Newtonsoft.Json;
using OpenAI.Chat;

namespace NeoEditor.Plugins.AiChat.Services;

/// <summary>
/// Orchestrates LLM calls with MCP tool integration and optional RAG context injection.
/// Uses <see cref="ChatClient"/> (OpenAI-compatible, model-agnostic) with
/// a manual function-calling loop.
/// </summary>
public class ChatService : IChatService
{
    private readonly ChatClient? _chatClient;
    private readonly IMcpToolProvider? _toolProvider;
    private readonly ChatHistoryManager _history;
    private readonly SystemPromptBuilder _promptBuilder;
    private readonly IRagService? _ragService;
    private readonly IConfigService? _configService;

    /// <inheritdoc />
    public bool IsAvailable => _chatClient is not null;

    public ChatService(ChatClient? chatClient,
        IServiceProvider serviceProvider,
        ChatHistoryManager history,
        SystemPromptBuilder promptBuilder)
    {
        _chatClient = chatClient;
        _toolProvider = serviceProvider.GetService(typeof(IMcpToolProvider)) as IMcpToolProvider;
        _ragService = serviceProvider.GetService(typeof(IRagService)) as IRagService;
        _configService = serviceProvider.GetService(typeof(IConfigService)) as IConfigService;
        _history = history;
        _promptBuilder = promptBuilder;

        // Auto-inject default system prompt on construction
        if (!_history.Messages.Any(m => m.Role == "system"))
            _history.SetSystemPrompt(_promptBuilder.BuildDefaultPrompt());
    }

    /// <inheritdoc />
    public string DefaultSystemPrompt => _promptBuilder.BuildDefaultPrompt();

    /// <inheritdoc />
    public string GetSystemPrompt()
    {
        return _history.Messages.FirstOrDefault(m => m.Role == "system")?.Content ?? "";
    }

    /// <inheritdoc />
    public void SetSystemPrompt(string prompt)
    {
        _history.SetSystemPrompt(prompt);
    }

    /// <inheritdoc />
    public void ResetSystemPrompt()
    {
        _history.SetSystemPrompt(_promptBuilder.BuildDefaultPrompt());
    }

    /// <inheritdoc />
    public async Task<string> SendMessageAsync(string userMessage, CancellationToken ct = default)
    {
        // Collect all streaming chunks into a single string (full-response mode).
        // Markers carry a leading "\n", so trim before checking to filter them all out.
        var sb = new StringBuilder();
        await foreach (var chunk in SendMessageStreamingAsync(userMessage, ct).WithCancellation(ct))
        {
            if (!chunk.TrimStart().StartsWith("[tool:", StringComparison.Ordinal))
                sb.Append(chunk);
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> SendMessageStreamingAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_chatClient is null)
        {
            yield return "AI Chat is not configured. Set an API Key in Settings → AI & MCP, then restart the app.";
            yield break;
        }

        _history.Add("user", userMessage);

        var messages = BuildMessages();

        // Inject RAG context if available
        if (_ragService is { HasIndex: true })
        {
            var ragResults = await _ragService.SearchAsync(userMessage, topK: 3, ct);
            if (ragResults.Count > 0)
            {
                var ragText = BuildRagContextText(ragResults);
                var lastUserIdx = messages.FindLastIndex(m => m is UserChatMessage);
                if (lastUserIdx >= 0)
                    messages.Insert(lastUserIdx, new SystemChatMessage(ragText));
            }
        }

        var options = new ChatCompletionOptions();
        var tools = BuildToolDefinitions();
        foreach (var t in tools) options.Tools.Add(t);

        // Streaming function-calling loop — cap the NUMBER of tool calls per turn
        // (configurable, default 30), with the iteration count as a secondary bound.
        var maxToolCalls = Math.Max(1, _configService?.Config.MaxToolCallsPerConversation ?? 30);
        var executedToolCalls = 0;
        for (var i = 0; i < maxToolCalls; i++)
        {
            ct.ThrowIfCancellationRequested();

            var contentBuilder = new StringBuilder();
            var toolAccumulators = new Dictionary<int, ToolCallAccumulator>();

            // Stream this completion (ClientResult<StreamingChatCompletion> is IAsyncEnumerable)
            await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, options, ct))
            {
                // --- Content deltas (text chunks for typewriter effect) ---
                foreach (var part in update.ContentUpdate)
                {
                    var delta = part.Text?.ToString();
                    if (part.Kind == ChatMessageContentPartKind.Text && !string.IsNullOrEmpty(delta))
                    {
                        contentBuilder.Append(delta);
                        yield return delta;
                    }
                }

                // --- Tool call deltas (accumulate across streaming updates) ---
                foreach (var tc in update.ToolCallUpdates)
                {
                    if (!toolAccumulators.TryGetValue(tc.Index, out var acc))
                    {
                        acc = new ToolCallAccumulator();
                        toolAccumulators[tc.Index] = acc;
                    }

                    if (tc.ToolCallId is not null) acc.Id = tc.ToolCallId;
                    if (tc.FunctionName is not null) acc.Name += tc.FunctionName;
                    if (tc.FunctionArgumentsUpdate is not null)
                        acc.Arguments.Append(tc.FunctionArgumentsUpdate.ToString());
                }
            }

            // --- After stream ends: check for tool calls ---
            if (toolAccumulators.Count > 0)
            {
                // Build assistant message with accumulated tool calls
                var toolCalls = toolAccumulators
                    .OrderBy(kv => kv.Key)
                    .Select(kv => ChatToolCall.CreateFunctionToolCall(
                        kv.Value.Id!,
                        kv.Value.Name,
                        BinaryData.FromString(kv.Value.Arguments.ToString())))
                    .ToList();

                messages.Add(new AssistantChatMessage(toolCalls));

                var limitReached = false;
                foreach (var toolCall in toolCalls)
                {
                    if (executedToolCalls >= maxToolCalls)
                    {
                        limitReached = true;
                        break;
                    }
                    executedToolCalls++;

                    var toolName = toolCall.FunctionName;
                    yield return $"\n[tool: executing {toolName}]\n";

                    var resultJson = await ExecuteMcpToolAsync(
                        toolName, toolCall.FunctionArguments.ToString(), ct);
                    messages.Add(new ToolChatMessage(toolCall.Id, resultJson));
                    // Surface the tool result so the UI tool block can expand to show it.
                    yield return $"\n[tool: result {toolName}]\n{resultJson}";
                }

                if (limitReached)
                {
                    yield return BuildLimitNotice(maxToolCalls);
                    yield break;
                }

                continue; // Next iteration of the function-calling loop
            }

            // --- Text response complete ---
            var text = contentBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                _history.Add("assistant", text);
            yield break;
        }

        // Loop exhausted because every iteration called tools without a final answer.
        yield return BuildLimitNotice(maxToolCalls);
    }

    private static string BuildLimitNotice(int maxToolCalls) =>
        $"\n[system: Tool-call limit reached ({maxToolCalls} this turn). " +
        "Narrow the search with entityType / modId filters or ask a more specific question.]\n";

    private static string BuildRagContextText(IReadOnlyList<RagResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Relevant game data for context (use this to inform your response):");
        foreach (var r in results)
        {
            sb.AppendLine($"  {r.Summary}");
        }

        sb.AppendLine("End of relevant context.");
        return sb.ToString();
    }

    private async Task<string> ExecuteMcpToolAsync(string toolName, string argsJson,
        CancellationToken ct)
    {
        if (_toolProvider is null)
            return JsonConvert.SerializeObject(new { error = "No tool provider available" });

        try
        {
            return await _toolProvider.ExecuteToolAsync(toolName, argsJson, ct);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { error = ex.Message });
        }
    }

    private List<ChatMessage> BuildMessages()
    {
        return _history.Messages.Select(m => m.Role switch
        {
            "system" => (ChatMessage)new SystemChatMessage(m.Content),
            "assistant" => new AssistantChatMessage(m.Content),
            _ => new UserChatMessage(m.Content)
        }).ToList();
    }

    private List<ChatTool> BuildToolDefinitions()
    {
        if (_toolProvider is null) return new List<ChatTool>();

        return _toolProvider.GetTools().Select(t =>
        {
            var schema = BinaryData.FromString(t.InputSchemaJson);
            return ChatTool.CreateFunctionTool(t.Name, t.Description, schema);
        }).ToList();
    }

    /// <summary>Accumulates streaming tool call deltas into a complete function call.</summary>
    private sealed class ToolCallAccumulator
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public StringBuilder Arguments { get; } = new();
    }
}