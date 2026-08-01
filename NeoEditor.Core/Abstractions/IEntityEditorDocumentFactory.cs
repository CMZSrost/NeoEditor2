using NeoEditor.Data.Model.Game;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Factory for creating entity editor documents.
/// Allows the App shell to open entity documents without referencing
/// the EntityEditor plugin's ViewModel types directly (R07 / R18).
/// </summary>
public interface IEntityEditorDocumentFactory
{
    /// <summary>Create a new document for the given entity.</summary>
    object CreateDocument(IEntity entity);
}
