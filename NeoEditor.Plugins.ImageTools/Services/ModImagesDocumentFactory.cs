using System;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Creates <see cref="ModImagesDocument"/> instances via DI so the App shell
/// doesn't need to know the document constructor signature (R07 / R18).
/// Mirrors <c>EntityEditorDocumentFactory</c> / <c>IEntityEditorDocumentFactory</c>.
/// </summary>
public class ModImagesDocumentFactory : IModImagesDocumentFactory
{
    private readonly IServiceProvider _services;

    public ModImagesDocumentFactory(IServiceProvider services)
    {
        _services = services;
    }

    public object CreateDocument(ModInfo modInfo)
    {
        return new ModImagesDocument(
            modInfo,
            _services.GetRequiredService<IConfigService>(),
            _services.GetRequiredService<IModImageListService>(),
            _services.GetRequiredService<ModImagePairDropHandler>(),
            _services.GetRequiredService<INotificationService>(),
            _services.GetRequiredService<ILocalizationService>());
    }
}
