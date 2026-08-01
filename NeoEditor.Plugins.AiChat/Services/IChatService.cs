using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeoEditor.Plugins.AiChat.Services;

/// <summary>
/// Chat service that orchestrates LLM calls with tool use.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Whether AI chat is usable. False when no API key is configured — the UI should
    /// disable the chat panel and tell the user to configure Settings → AI &amp; MCP.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Send a user message and get the assistant's response.
    /// Handles the function-calling loop internally.
    /// </summary>
    Task<string> SendMessageAsync(string userMessage, CancellationToken ct = default);

    /// <summary>
    /// Send a user message and stream the assistant's response token-by-token.
    /// Yields text deltas for the typewriter effect, and tool-execution status
    /// markers (prefixed with "[tool:") when MCP tools are invoked.
    /// Handles the function-calling loop internally — tool turns are transparent.
    /// </summary>
    IAsyncEnumerable<string> SendMessageStreamingAsync(string userMessage, CancellationToken ct = default);

    /// <summary>
    /// Get the current system prompt text.
    /// </summary>
    string GetSystemPrompt();

    /// <summary>
    /// Set a custom system prompt, replacing the current one.
    /// </summary>
    void SetSystemPrompt(string prompt);

    /// <summary>
    /// Reset the system prompt to the default (with auto-generated entity schema).
    /// </summary>
    void ResetSystemPrompt();

    /// <summary>
    /// Get the default system prompt text (read-only reference).
    /// </summary>
    string DefaultSystemPrompt { get; }
}