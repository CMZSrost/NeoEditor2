using Microsoft.Extensions.Logging;

namespace NeoEditor.Data.Messages;

public record LogMessage
{
    public LogLevel Level = LogLevel.Information;
    public required string Message;
    public bool MsgBox = false;
}