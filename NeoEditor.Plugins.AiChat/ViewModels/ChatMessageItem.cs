using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Plugins.AiChat.ViewModels;

public partial class ChatMessageItem : ObservableObject
{
    [ObservableProperty]
    private string _role = "";

    [ObservableProperty]
    private string _content = "";

    [ObservableProperty]
    private bool _isUser;

    [ObservableProperty]
    private bool _isThinking;

    public ChatMessageItem() { }

    public ChatMessageItem(string role, string content)
    {
        _role = role;
        _content = content;
        _isUser = role == "user";
    }
}
