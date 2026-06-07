namespace NeoEditor.Data.Command;

public interface IEditorCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}
