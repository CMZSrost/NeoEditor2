namespace NeoEditor.Messaging;

/// <summary>
/// Optional base type for all messages. Messages may extend this or be plain records.
/// Using the base type enables reflection-based discovery and consistent metadata.
/// </summary>
public abstract record MessageBase;
