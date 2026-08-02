using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Plugins.AiChat.Services;

namespace NeoEditor.Plugins.AiChat.ViewModels;

public partial class AiChatViewModel : ObservableObject
{
    /// <summary>
    /// Matches the streaming tool-execution marker ChatService yields between text chunks,
    /// e.g. <c>"\n[tool: executing SearchAllTypes]\n"</c>. The leading/trailing whitespace is
    /// part of the marker, so the check tolerates it.
    /// </summary>
    [GeneratedRegex(@"\[tool:\s*executing\s+([^\]]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex ToolMarkerRegex();

    [GeneratedRegex(@"\[tool:\s*result\s+([^\]]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex ToolResultMarkerRegex();

    [GeneratedRegex(@"\[system:\s*([^\]]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex SystemMarkerRegex();

    private readonly IChatService _chatService;
    private readonly IRagService? _ragService;

    public AiChatViewModel(IChatService chatService, IRagService? ragService = null)
    {
        _chatService = chatService;
        _ragService = ragService;
        _systemPrompt = _chatService.GetSystemPrompt();
        _isSystemPromptExpanded = false;
        _isAvailable = _chatService.IsAvailable;
        UpdateRagStatus();
    }

    [ObservableProperty] private string _inputText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(SendOrStopCommand))]
    [NotifyPropertyChangedFor(nameof(SendOrStopLabel))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isBusy;

    [ObservableProperty] private string _systemPrompt;

    [ObservableProperty] private bool _isSystemPromptExpanded;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanBuildIndex))]
    private bool _isBuildingIndex;

    [ObservableProperty] private string _ragStatus = "";

    /// <summary>
    /// Whether AI chat is usable. False when no API key is configured — the panel shows a
    /// "not configured" notice and disables sending / index building.
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSend))] [NotifyPropertyChangedFor(nameof(CanBuildIndex))]
    private bool _isAvailable;

    /// <summary>True when chat input is usable (configured AND not busy).</summary>
    public bool CanSend => IsAvailable && !IsBusy;

    /// <summary>True while a response is streaming — the Stop button can cancel it.</summary>
    public bool CanStop => IsBusy;

    /// <summary>The input action: Send when idle, Stop while a response is streaming (toggle).</summary>
    public ICommand SendOrStopCommand => IsBusy ? StopCommand : SendMessageCommand;

    public string SendOrStopLabel => IsBusy ? "Stop" : "Send";

    /// <summary>True when the RAG index can be built (configured AND not already building).</summary>
    public bool CanBuildIndex => IsAvailable && !IsBuildingIndex;

    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    [RelayCommand]
    private async Task SendMessageAsync(CancellationToken ct)
    {
        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!IsAvailable)
        {
            Messages.Add(new ChatMessageItem("system",
                "AI Chat is not configured. Set an API Key in Settings → AI & MCP, then restart the app."));
            return;
        }

        InputText = "";
        IsBusy = true;

        Messages.Add(new ChatMessageItem("user", text));

        // Streaming: assistant text, tool markers and post-tool text arrive interleaved.
        // Keep ONE live assistant message; each tool call becomes its own expandable Tool
        // block — header = tool name, content = the tool result streamed after the result
        // marker. Cancelling (Stop) aborts the loop without an error bubble.
        ChatMessageItem? assistantMsg = null;
        ChatMessageItem? pendingTool = null;

        try
        {
            await foreach (var chunk in _chatService.SendMessageStreamingAsync(text, ct)
                               .WithCancellation(ct))
            {
                var execMatch = ToolMarkerRegex().Match(chunk);
                var resultMatch = ToolResultMarkerRegex().Match(chunk);
                var systemMatch = SystemMarkerRegex().Match(chunk);

                if (systemMatch.Success)
                {
                    FinalizeAssistant();
                    Messages.Add(new ChatMessageItem("system", systemMatch.Groups[1].Value.Trim()));
                }
                else if (execMatch.Success)
                {
                    FinalizeAssistant();
                    pendingTool = new ChatMessageItem("tool", "")
                    {
                        ToolName = execMatch.Groups[1].Value.Trim()
                    };
                    Messages.Add(pendingTool);
                }
                else if (resultMatch.Success)
                {
                    var rest = chunk.Substring(resultMatch.Index + resultMatch.Length);
                    if (pendingTool is not null && !string.IsNullOrWhiteSpace(rest))
                        pendingTool.Content += rest.TrimStart('\n', ' ', '\r', '\t');
                }
                else if (assistantMsg is null)
                {
                    assistantMsg = new ChatMessageItem("assistant", "");
                    Messages.Add(assistantMsg);
                    assistantMsg.IsThinking = false;
                    assistantMsg.Content += chunk;
                }
                else
                {
                    assistantMsg.IsThinking = false;
                    assistantMsg.Content += chunk;
                }
            }

            FinalizeAssistant();
        }
        catch (OperationCanceledException)
        {
            FinalizeAssistant();
            if (pendingTool is not null)
                Messages.Remove(pendingTool);
            if (assistantMsg is not null)
                Messages.Remove(assistantMsg);
            Messages.Add(new ChatMessageItem("system", "Stopped."));
        }
        catch (Exception ex)
        {
            if (pendingTool is not null)
                Messages.Remove(pendingTool);
            if (assistantMsg is not null)
                Messages.Remove(assistantMsg);
            Messages.Add(new ChatMessageItem("system", $"Error: {ex.Message}"));
        }
        finally
        {
            IsBusy = false;
        }

        return;

        /// <summary>Pushes the pending assistant text as its own bubble; drops empty placeholders.</summary>
        void FinalizeAssistant()
        {
            if (assistantMsg is null)
                return;

            assistantMsg.IsThinking = false;
            if (string.IsNullOrEmpty(assistantMsg.Content))
                Messages.Remove(assistantMsg);
            assistantMsg = null;
        }
    }

    /// <summary>Cancels the in-flight streaming response (SendMessageCommand supports Cancel).</summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        SendMessageCommand.Cancel();
    }

    [RelayCommand]
    private void ClearMessages()
    {
        Messages.Clear();
    }

    [RelayCommand]
    private void ApplySystemPrompt()
    {
        var prompt = SystemPrompt.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;
        _chatService.SetSystemPrompt(prompt);
    }

    [RelayCommand]
    private void ResetSystemPrompt()
    {
        _chatService.ResetSystemPrompt();
        SystemPrompt = _chatService.GetSystemPrompt();
    }

    [RelayCommand]
    private void ToggleSystemPrompt()
    {
        IsSystemPromptExpanded = !IsSystemPromptExpanded;
        if (IsSystemPromptExpanded)
            SystemPrompt = _chatService.GetSystemPrompt();
    }

    [RelayCommand]
    private async Task BuildIndexAsync(CancellationToken ct)
    {
        if (_ragService is null || !_ragService.IsAvailable)
        {
            RagStatus = "RAG: unavailable (no API key)";
            return;
        }

        IsBuildingIndex = true;
        RagStatus = "Building index...";

        try
        {
            await _ragService.BuildIndexAsync(ct);
            UpdateRagStatus();
        }
        catch (OperationCanceledException)
        {
            RagStatus = "Index build cancelled.";
        }
        catch (Exception ex)
        {
            RagStatus = $"Index build failed: {ex.Message}";
        }
        finally
        {
            IsBuildingIndex = false;
        }
    }

    [RelayCommand]
    private void ClearRagIndex()
    {
        _ragService?.Clear();
        UpdateRagStatus();
    }

    private void UpdateRagStatus()
    {
        if (_ragService is null || !_ragService.IsAvailable)
        {
            RagStatus = "RAG: unavailable (no API key)";
        }
        else if (_ragService.HasIndex)
        {
            RagStatus = $"RAG: {_ragService.IndexedCount} entities indexed";
        }
        else
        {
            RagStatus = "RAG: no index built";
        }
    }
}