using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Plugins.AiChat.Services;
using NeoEditor.Plugins.AiChat.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.AiChat.Tests;

/// <summary>
/// Round 22 + 23: tool markers yielded by <see cref="IChatService.SendMessageStreamingAsync"/>
/// surface as their own expandable <see cref="ChatMessageKind.Tool"/> block — the executing
/// marker puts the tool name in <see cref="ChatMessageItem.ToolName"/>, the result marker's
/// JSON lands in <see cref="ChatMessageItem.Content"/> — and empty assistant placeholders
/// must not linger in the history.
/// </summary>
public class AiChatViewModelTests
{
    private sealed class FakeChatService(params string[] chunks) : IChatService
    {
        private readonly string[] _chunks = chunks;

        public bool IsAvailable => true;
        public string DefaultSystemPrompt => "";

        public Task<string> SendMessageAsync(string userMessage, CancellationToken ct = default)
            => Task.FromResult("");

        public async IAsyncEnumerable<string> SendMessageStreamingAsync(
            string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var chunk in _chunks)
                yield return chunk;
        }

        public string GetSystemPrompt() => "";

        public void SetSystemPrompt(string prompt)
        {
        }

        public void ResetSystemPrompt()
        {
        }
    }

    [Fact]
    public async Task SendMessage_InterleavesToolCall_AsExpandableToolBlock()
    {
        var vm = new AiChatViewModel(
            new FakeChatService(
                "Hello ",
                "\n[tool: executing SearchAllTypes]\n",
                "\n[tool: result SearchAllTypes]\n{\"found\":5}",
                "found 5 items"))
        {
            InputText = "search things"
        };

        await vm.SendMessageCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(4, vm.Messages.Count);
        Assert.Equal(ChatMessageKind.User, vm.Messages[0].Kind);
        Assert.Equal("search things", vm.Messages[0].Content);
        Assert.Equal(ChatMessageKind.Assistant, vm.Messages[1].Kind);
        Assert.Equal("Hello ", vm.Messages[1].Content);
        Assert.Equal(ChatMessageKind.Tool, vm.Messages[2].Kind);
        Assert.Equal("SearchAllTypes", vm.Messages[2].ToolName);
        Assert.Equal("{\"found\":5}", vm.Messages[2].Content);
        Assert.Equal(ChatMessageKind.Assistant, vm.Messages[3].Kind);
        Assert.Equal("found 5 items", vm.Messages[3].Content);
    }

    [Fact]
    public async Task SendMessage_ToolOnlyTurn_DropsEmptyAssistantPlaceholder()
    {
        var vm = new AiChatViewModel(
            new FakeChatService("\n[tool: executing GetModInfo]\n", "ok"))
        {
            InputText = "which mod"
        };

        await vm.SendMessageCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(3, vm.Messages.Count);
        Assert.Equal(ChatMessageKind.User, vm.Messages[0].Kind);
        Assert.Equal(ChatMessageKind.Tool, vm.Messages[1].Kind);
        Assert.Equal("GetModInfo", vm.Messages[1].ToolName);
        Assert.Equal(ChatMessageKind.Assistant, vm.Messages[2].Kind);
        Assert.Equal("ok", vm.Messages[2].Content);
    }

    [Fact]
    public void ChatMessageItem_MapsRoleToKind()
    {
        Assert.Equal(ChatMessageKind.User, new ChatMessageItem("user", "x").Kind);
        Assert.Equal(ChatMessageKind.Assistant, new ChatMessageItem("assistant", "x").Kind);
        Assert.Equal(ChatMessageKind.Tool, new ChatMessageItem("tool", "x").Kind);
        Assert.Equal(ChatMessageKind.System, new ChatMessageItem("system", "x").Kind);
    }
}