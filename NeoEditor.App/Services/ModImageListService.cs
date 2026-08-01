using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Helper;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Views.Dialog;

namespace NeoEditor.Services;

/// <summary>
/// App-side implementation of IModImageListService.
/// Wraps PhpParser (PHP image list parsing) and RenameImagePairDialog (rename UI).
/// Registered in App Composition Root as the concrete implementation.
/// Created during M11 migration to bridge Plugin → App dependencies.
/// </summary>
public sealed class ModImageListService : IModImageListService
{
    private readonly PhpParser _phpParser;
    private readonly IServiceProvider _serviceProvider;

    public ModImageListService(PhpParser phpParser, IServiceProvider serviceProvider)
    {
        _phpParser = phpParser;
        _serviceProvider = serviceProvider;
    }

    public IReadOnlyList<(string NormalImage, string X2Image)> ParseImagePairs(string getImagesPath)
    {
        return _phpParser.ParseImagePairs(getImagesPath);
    }

    public string GenerateImagePhp(IReadOnlyList<(string NormalImage, string X2Image)> imagePairs)
    {
        return _phpParser.GenerateImagePhp(imagePairs);
    }

    public async Task<(string NormalFileName, string X2FileName)?> RequestRenameAsync(
        string imageDirectory, string currentNormalPath, string currentX2Path)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow })
        {
            return null;
        }

        var dialog = _serviceProvider.GetRequiredService<RenameImagePairDialog>();
        dialog.ViewModel.Initialize(imageDirectory, currentNormalPath, currentX2Path);
        await dialog.ShowDialog<object?>(mainWindow);

        if (!dialog.ViewModel.IsConfirmed)
        {
            return null;
        }

        return dialog.ViewModel.GetProposedNames();
    }
}
