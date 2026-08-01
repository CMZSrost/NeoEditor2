using System.Collections.Generic;

namespace NeoEditor.Plugins.AiChat.Services;

/// <summary>
/// Lightweight conversation message with role and content.
/// </summary>
public sealed record ConversationMessage(string Role, string Content);

/// <summary>
/// Manages conversation history with a configurable context window size.
/// </summary>
public class ChatHistoryManager
{
    private readonly List<ConversationMessage> _messages = new();
    private const int MaxMessages = 100;

    public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();

    public void Add(string role, string content)
    {
        _messages.Add(new ConversationMessage(role, content));

        // Trim oldest messages if over limit, keeping the first system message if present
        while (_messages.Count > MaxMessages)
        {
            var toRemove = _messages[0].Role == "system" ? _messages[1] : _messages[0];
            _messages.Remove(toRemove);
        }
    }

    public void Clear()
    {
        _messages.Clear();
    }

    public void SetSystemPrompt(string prompt)
    {
        _messages.RemoveAll(m => m.Role == "system");
        _messages.Insert(0, new ConversationMessage("system", prompt));
    }
}
