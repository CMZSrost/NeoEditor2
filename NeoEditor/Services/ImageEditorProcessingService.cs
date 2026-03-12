using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NeoEditor.Services;

public sealed class ImageEditorProcessingService : IImageEditorProcessingService
{
    public async Task<ImageEditorProcessingResult?> CreatePreviewAsync(ImageEditorProcessingRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryValidateRequest(request))
        {
            return null;
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var normalImage = CreatePixelatedBaseImage(request);
            using var x2Image = CreateX2Image(normalImage);
            var previewBitmap = CreateBitmap(x2Image);
            return new ImageEditorProcessingResult(previewBitmap, x2Image.Width, x2Image.Height);
        }, cancellationToken);
    }

    public async Task<ImageEditorSaveResult?> SaveAsync(string normalOutputPath, string x2OutputPath, ImageEditorProcessingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(x2OutputPath);

        if (!TryValidateRequest(request))
        {
            return null;
        }

        using var normalImage = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CreatePixelatedBaseImage(request);
        }, cancellationToken);

        await using var normalStream = File.Create(normalOutputPath);
        await normalImage.SaveAsync(normalStream, CreatePngEncoder(), cancellationToken);

        using var x2Image = CreateX2Image(normalImage);
        await using var x2Stream = File.Create(x2OutputPath);
        await x2Image.SaveAsync(x2Stream, CreatePngEncoder(), cancellationToken);

        return new ImageEditorSaveResult(x2Image.Width, x2Image.Height);
    }

    private static bool TryValidateRequest(ImageEditorProcessingRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.SourcePath)
               && File.Exists(request.SourcePath)
               && request.NormalWidth > 0
               && request.NormalHeight > 0;
    }

    private static Image<Rgba32> CreatePixelatedBaseImage(ImageEditorProcessingRequest request)
    {
        using var source = Image.Load<Rgba32>(request.SourcePath);

        using var working = request.CropRect is { Width: > 0, Height: > 0 } cropRect
            ? source.Clone(context => context.Crop(new Rectangle(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height)))
            : source.Clone();

        return working.Clone(context => context.Resize(new ResizeOptions
        {
            Size = new Size(Math.Max(1, request.NormalWidth), Math.Max(1, request.NormalHeight)),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.NearestNeighbor,
        }));
    }

    private static Image<Rgba32> CreateX2Image(Image<Rgba32> normalImage)
    {
        return normalImage.Clone(context => context.Resize(new ResizeOptions
        {
            Size = new Size(Math.Max(1, normalImage.Width * 2), Math.Max(1, normalImage.Height * 2)),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.NearestNeighbor,
        }));
    }

    private static Bitmap CreateBitmap(Image<Rgba32> image)
    {
        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        memoryStream.Position = 0;
        return new Bitmap(memoryStream);
    }

    private static PngEncoder CreatePngEncoder()
    {
        return new PngEncoder();
    }
}


