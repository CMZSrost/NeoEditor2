using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Plugins.AiChat.ViewModels;

/// <summary>Kinds of chat message shown in the AI Chat history.</summary>
public enum ChatMessageKind
{
    User,
    Assistant,
    Tool,
    System
}

/// <summary>
/// One chat message rendered in the AI Chat history. <see cref="Kind"/> drives which
/// bubble/block template is used (user right bubble, assistant left bubble, a distinct
/// tool-invocation block, or a centered system strip).
/// </summary>
public partial class ChatMessageItem : ObservableObject
{
    [ObservableProperty] private string _role = "";

    [ObservableProperty] private string _content = "";

    /// <summary>Tool name for Tool-kind messages (the expander header).</summary>
    [ObservableProperty] private string _toolName = "";

    [ObservableProperty] private bool _isThinking;

    /// <summary>Stable per message — set from the role at construction.</summary>
    public ChatMessageKind Kind { get; }

    public bool IsUser => Kind == ChatMessageKind.User;
    public bool IsAssistant => Kind == ChatMessageKind.Assistant;
    public bool IsTool => Kind == ChatMessageKind.Tool;
    public bool IsSystem => Kind == ChatMessageKind.System;

    public ChatMessageItem() : this("assistant", "")
    {
    }

    public ChatMessageItem(string role, string content)
    {
        _role = role;
        _content = content;
        Kind = role switch
        {
            "user" => ChatMessageKind.User,
            "tool" => ChatMessageKind.Tool,
            "system" => ChatMessageKind.System,
            _ => ChatMessageKind.Assistant
        };
    }
}