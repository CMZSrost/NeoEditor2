using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Plugins.AiChat.Services;

namespace NeoEditor.Plugins.AiChat.ViewModels;

public partial class AiChatViewModel : ObservableObject
{
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
    private bool _isBusy;

    [ObservableProperty] private string _systemPrompt;

    [ObservableProperty] private bool _isSystemPromptExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildIndex))]
    private bool _isBuildingIndex;

    [ObservableProperty] private string _ragStatus = "";

    /// <summary>
    /// Whether AI chat is usable. False when no API key is configured — the panel shows a
    /// "not configured" notice and disables sending / index building.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    [NotifyPropertyChangedFor(nameof(CanBuildIndex))]
    private bool _isAvailable;

    /// <summary>True when chat input is usable (configured AND not busy).</summary>
    public bool CanSend => IsAvailable && !IsBusy;

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

        // Placeholder for streaming assistant response
        var assistantMsg = new ChatMessageItem("assistant", "") { IsThinking = true };
        Messages.Add(assistantMsg);

        try
        {
            await foreach (var chunk in _chatService.SendMessageStreamingAsync(text, ct)
                               .WithCancellation(ct))
            {
                if (chunk.StartsWith("[tool:", StringComparison.Ordinal))
                {
                    // Tool execution marker — show briefly in the content
                    assistantMsg.Content += chunk;
                }
                else
                {
                    assistantMsg.IsThinking = false;
                    assistantMsg.Content += chunk;
                }
            }

            // Remove placeholder if nothing was produced
            if (string.IsNullOrEmpty(assistantMsg.Content))
            {
                Messages.Remove(assistantMsg);
            }
        }
        catch (Exception ex)
        {
            Messages.Remove(assistantMsg);
            Messages.Add(new ChatMessageItem("system", $"Error: {ex.Message}"));
        }
        finally
        {
            IsBusy = false;
        }
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