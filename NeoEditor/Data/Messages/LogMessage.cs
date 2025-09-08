using Microsoft.Extensions.Logging;

namespace NeoEditor.Data.Messages;

public record LogMessage
{
    public LogLevel Level { get; set; } = LogLevel.Information;
    public required string Message { get; set; }
    public bool MsgBox = false;
}