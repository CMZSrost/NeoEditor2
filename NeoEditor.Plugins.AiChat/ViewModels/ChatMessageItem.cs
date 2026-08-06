using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    /// <summary>Markdown source for the assistant bubble renderer (Docs/41: render AI
    /// output as Markdown by default). Kept in sync with <see cref="Content"/>.</summary>
    public LiveMarkdown.Avalonia.ObservableStringBuilder MarkdownBuilder { get; } = new();

    /// <summary>Docs/41: copy the message content to the clipboard.</summary>
    [RelayCommand]
    private async Task Copy()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (mainWindow.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(Content);
    }

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

    partial void OnContentChanged(string value)
    {
        // Docs/41: keep the MarkdownRenderer source in sync (streaming updates included).
        MarkdownBuilder.Clear();
        if (!string.IsNullOrEmpty(value))
            MarkdownBuilder.Append(value);
    }
}