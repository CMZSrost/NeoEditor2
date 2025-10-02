using Microsoft.Extensions.Logging;

namespace NeoEditor.Data.Messages;

public record LogMessage
{
    public bool MsgBox = false;
    public LogLevel Level { get; set; } = LogLevel.Information;
    public required string Message { get; set; }
}

public class LoggingEvent : PubSubEvent<LogMessage>;