using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using NeoEditor.Core.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;

namespace NeoEditor.Plugins.ImageTools.Services;

public sealed class ImageFileService : IImageFileService
{
    private const string OutputExtension = ".png";
    private readonly ILocalizationService _loc;

    public ImageFileService(ILocalizationService loc)
    {
        _loc = loc;
    }

    public async Task<string[]> PickImagesAsync(bool allowMultiple)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return [];
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _loc["SelectImage"],
            AllowMultiple = allowMultiple,
            FileTypeFilter =
            [
                new FilePickerFileType(_loc["ImageFiles"])
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"]
                }
            ]
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
    }

    public async Task SaveAsync(Bitmap bitmap, string suggestedName)
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _loc["SaveImage"],
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("PNG") { Patterns = ["*.png"] }
            ],
            DefaultExtension = OutputExtension
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var selectedPath = Path.GetFullPath(file.TryGetLocalPath() ?? string.Empty);
            var directory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
            var normalFileName = NormalizeNormalOutputFileName(selectedPath);
            var normalPath = Path.Combine(directory, normalFileName);
            var x2Path = Path.Combine(directory, GetSuggestedX2FileName(normalFileName));

            await using (var fs = File.Create(normalPath))
            {
                SavePng(bitmap, fs);
            }

            await SaveX2VersionAsync(bitmap, x2Path);
        }
        catch
        {
            // Ignore save failures and leave the preview intact.
        }
    }

    public string GetSuggestedFileName(string? sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return $"pixelated{OutputExtension}";
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceName);
        return $"{fileNameWithoutExtension}{OutputExtension}";
    }

    public string GetSuggestedX2FileName(string normalFileName)
    {
        return $"x2_{NormalizeNormalOutputFileName(normalFileName)}";
    }

    public string NormalizeNormalOutputFileName(string fileName)
    {
        var normalizedFileName = Path.GetFileName(fileName);
        var fileNameWithoutPrefix = normalizedFileName.StartsWith("x2_", StringComparison.OrdinalIgnoreCase)
            ? normalizedFileName[3..]
            : normalizedFileName;

        return $"{Path.GetFileNameWithoutExtension(fileNameWithoutPrefix)}{OutputExtension}";
    }

    public Bitmap FromBytes(byte[] pngBytes)
    {
        // Decode via a temp file, matching FromFile's file-path path. Avalonia's
        // Bitmap(Stream) keeps a reference to the source stream; disposing it (the
        // using below) can leave the Skia backend rendering garbled pixels on some
        // platforms.
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllBytes(tempPath, pngBytes);
            return new Bitmap(tempPath);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }

    public Bitmap FromFile(string path)
    {
        return new Bitmap(path);
    }

    public Bitmap FromImageSharp(Image<Rgba32> image)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            image.SaveAsPng(tempPath);
            return new Bitmap(tempPath);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }

    public string StageAiCandidate(byte[] pngBytes, string name)
    {
        _ = name; // reserved for naming hints
        var directory = Path.Combine(Path.GetTempPath(), "NeoEditor", "AiStaging");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, pngBytes);
        return path;
    }

    public void CleanupStagedCandidates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NeoEditor", "AiStaging");
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(directory, "*.png"))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>Encode a bitmap as PNG. Avalonia's <c>Save(Stream, int?)</c> is obsolete in
    /// favor of BitmapEncoderOptions; default PNG quality is exactly what we want, so the
    /// deprecated overload is intentionally used.</summary>
    private static void SavePng(Bitmap bitmap, Stream stream)
    {
#pragma warning disable CS0618 // Bitmap.Save(Stream, int?) is obsolete; default PNG encoding is intended.
        bitmap.Save(stream);
#pragma warning restore CS0618
    }

    private static async Task SaveX2VersionAsync(Bitmap source, string x2Path)
    {
        try
        {
            using var ms = new MemoryStream();
            SavePng(source, ms);
            ms.Position = 0;
            using var img = Image.Load<Rgba32>(ms);
            var x2Width = img.Width * 2;
            var x2Height = img.Height * 2;
            using var x2 = img.Clone(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(x2Width, x2Height),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.NearestNeighbor,
                });
            });
            await using var fs = File.Create(x2Path);
            await x2.SaveAsPngAsync(fs);
        }
        catch
        {
            // The 2× version is optional — don't fail the whole save.
        }
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            })
        {
            return null;
        }

        return TopLevel.GetTopLevel(mainWindow)?.StorageProvider;
    }
}
