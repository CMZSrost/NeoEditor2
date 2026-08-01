using NeoEditor.Data.Model;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Factory for creating mod images documents.
/// Lets the App shell open mod-images documents without referencing the
/// ImageTools plugin's document types directly (R07 / R18), mirroring
/// <see cref="IEntityEditorDocumentFactory"/>.
/// </summary>
public interface IModImagesDocumentFactory
{
    /// <summary>Create a new document for the given mod.</summary>
    object CreateDocument(ModInfo modInfo);
}
