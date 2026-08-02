using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using NeoEditor.Plugins.AiChat.ViewModels;

namespace NeoEditor.Plugins.AiChat.Views;

public partial class AiChatView : UserControl
{
    private AiChatViewModel? _vm;

    public AiChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.Messages.CollectionChanged -= OnMessagesChanged;

        _vm = DataContext as AiChatViewModel;
        if (_vm is not null)
            _vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    /// <summary>Keep the chat pinned to the newest message when one is appended.</summary>
    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _vm is { Messages.Count: > 0 })
            MessageScroll.ScrollToEnd();
    }
}