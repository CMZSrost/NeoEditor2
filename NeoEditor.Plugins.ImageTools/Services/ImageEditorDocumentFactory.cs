using System;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Creates <see cref="ImageEditorDocument"/> instances via DI so the App shell
/// doesn't need to know the document constructor signature (R07 / R18).
/// Mirrors <c>ModImagesDocumentFactory</c> / <c>IModImagesDocumentFactory</c>.
/// </summary>
public class ImageEditorDocumentFactory : IImageEditorDocumentFactory
{
    private readonly IServiceProvider _services;

    public ImageEditorDocumentFactory(IServiceProvider services)
    {
        _services = services;
    }

    public object CreateDocument()
    {
        return new ImageEditorDocument(
            _services.GetRequiredService<IImageEditorProcessingService>(),
            _services.GetRequiredService<IImageFileService>(),
            _services.GetRequiredService<ILocalizationService>());
    }

    public object CreateDocument(string imagePath)
    {
        var document = (ImageEditorDocument)CreateDocument();
        document.LoadImage(imagePath);
        return document;
    }
}
