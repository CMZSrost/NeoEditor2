using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NeoEditor.Data.Messages;

public class GameRootDirChangedMessage : ValueChangedMessage<string>
{
    public GameRootDirChangedMessage(string value) : base(value) { }
}

// Q10=A: SwitchToSettingsMessage deleted (dead message).

public record ColumnVisibilityChangedMessage { public string TableName { get; init; } = ""; }
