using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NeoEditor.Data.Messages;

public class GameRootDirChangedMessage : ValueChangedMessage<string>
{
    public GameRootDirChangedMessage(string value) : base(value)
    {
    }
}