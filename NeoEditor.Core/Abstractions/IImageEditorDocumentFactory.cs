namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Factory for creating image-editor documents.
/// Lets the App shell open image-editor documents without referencing the
/// ImageTools plugin's document types directly (R07 / R18), mirroring
/// <see cref="IModImagesDocumentFactory"/>.
/// </summary>
public interface IImageEditorDocumentFactory
{
    /// <summary>Create a blank image-editor document.</summary>
    object CreateDocument();

    /// <summary>Create an image-editor document with the given image already loaded.</summary>
    object CreateDocument(string imagePath);
}
