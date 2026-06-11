using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NeoEditor.Data.Messages;

public class GameRootDirChangedMessage : ValueChangedMessage<string>
{
    public GameRootDirChangedMessage(string value) : base(value) { }
}

public record SwitchToSettingsMessage;

public record FontSizeChangedMessage { public int FontSize { get; init; } }
public record GridRowHeightChangedMessage { public int RowHeight { get; init; } }
public record ColumnVisibilityChangedMessage { public string TableName { get; init; } = ""; }