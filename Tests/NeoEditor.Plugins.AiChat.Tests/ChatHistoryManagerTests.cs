using NeoEditor.Plugins.AiChat.Services;
using Xunit;

namespace NeoEditor.Plugins.AiChat.Tests;

public class ChatHistoryManagerTests
{
    [Fact]
    public void NewManager_HasNoMessages()
    {
        var mgr = new ChatHistoryManager();
        Assert.Empty(mgr.Messages);
    }

    [Fact]
    public void Add_AppendsMessage()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("user", "hello");

        Assert.Single(mgr.Messages);
        Assert.Equal("user", mgr.Messages[0].Role);
        Assert.Equal("hello", mgr.Messages[0].Content);
    }

    [Fact]
    public void Add_MultipleMessages_PreservesOrder()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("system", "You are helpful.");
        mgr.Add("user", "hi");
        mgr.Add("assistant", "Hello!");

        Assert.Equal(3, mgr.Messages.Count);
        Assert.Equal("system", mgr.Messages[0].Role);
        Assert.Equal("user", mgr.Messages[1].Role);
        Assert.Equal("assistant", mgr.Messages[2].Role);
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("user", "hello");
        mgr.Add("assistant", "hi");
        mgr.Clear();

        Assert.Empty(mgr.Messages);
    }

    [Fact]
    public void SetSystemPrompt_ReplacesExisting()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("system", "old prompt");
        mgr.Add("user", "hello");

        mgr.SetSystemPrompt("new prompt");

        Assert.Equal(2, mgr.Messages.Count);
        Assert.Equal("system", mgr.Messages[0].Role);
        Assert.Equal("new prompt", mgr.Messages[0].Content);
        Assert.Equal("user", mgr.Messages[1].Role);
    }

    [Fact]
    public void SetSystemPrompt_AddsWhenNoneExists()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("user", "hello");

        mgr.SetSystemPrompt("you are helpful");

        Assert.Equal(2, mgr.Messages.Count);
        Assert.Equal("system", mgr.Messages[0].Role);
        Assert.Equal("you are helpful", mgr.Messages[0].Content);
    }

    [Fact]
    public void SetSystemPrompt_RemovesMultipleSystemMessages()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("system", "prompt1");
        mgr.Add("system", "prompt2");
        mgr.Add("user", "hello");

        mgr.SetSystemPrompt("final prompt");

        Assert.Equal(2, mgr.Messages.Count); // system + user
        Assert.Equal("final prompt", mgr.Messages[0].Content);
    }

    [Fact]
    public void Add_TrimsWhenOverLimit_KeepsSystemMessage()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("system", "you are helpful");

        // Add 101 more messages to trigger trimming (max = 100)
        for (var i = 0; i < 101; i++)
            mgr.Add("user", $"message {i}");

        Assert.Equal(100, mgr.Messages.Count); // capped at 100
        // System message must be preserved at position 0
        Assert.Equal("system", mgr.Messages[0].Role);
        Assert.Equal("you are helpful", mgr.Messages[0].Content);
    }

    [Fact]
    public void Add_TrimsWhenOverLimit_NoSystemMessage()
    {
        var mgr = new ChatHistoryManager();

        // Add 105 messages (over the 100 limit)
        for (var i = 0; i < 105; i++)
            mgr.Add("user", $"message {i}");

        Assert.Equal(100, mgr.Messages.Count);
        // Without system message, oldest user messages are dropped
        Assert.Equal("user", mgr.Messages[0].Role);
    }

    [Fact]
    public void Clear_ThenAdd_Fresh()
    {
        var mgr = new ChatHistoryManager();
        mgr.Add("user", "old");
        mgr.Clear();
        mgr.Add("user", "new");

        Assert.Single(mgr.Messages);
        Assert.Equal("new", mgr.Messages[0].Content);
    }
}
