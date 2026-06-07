namespace NeoEditor.Data.Command;

/// <summary>
/// Command that can serialize itself for DB persistence.
/// Each command is responsible for its own JSON format — no reflection on private fields.
/// </summary>
public interface ISerializableCommand : IEditorCommand
{
    /// <summary>Stable identifier used in the command_log table.</summary>
    string CommandType { get; }

    /// <summary>Serialize this command's data to a JSON string for storage.</summary>
    string Serialize();
}
