using System.Threading;
using System.Threading.Tasks;
using Avalonia;

namespace NeoEditor.Plugins.ImageTools.Services;

public interface IImageEditorProcessingService
{
    Task<ImageEditorProcessingResult?> CreatePreviewAsync(ImageEditorProcessingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a preview with optional pixel art post-processing (color quantization,
    /// edge enhancement, dithering, transparent background).
    /// </summary>
    Task<ImageEditorProcessingResult?> CreatePixelArtPreviewAsync(
        ImageEditorProcessingRequest request,
        PixelArtConversionOptions? pixelOptions,
        CancellationToken cancellationToken = default);

    Task<ImageEditorSaveResult?> SaveAsync(string normalOutputPath, string x2OutputPath, ImageEditorProcessingRequest request, CancellationToken cancellationToken = default);
}

public sealed record ImageEditorProcessingRequest(string SourcePath, int NormalWidth, int NormalHeight, PixelRect? CropRect);

public sealed record ImageEditorProcessingResult(Avalonia.Media.Imaging.Bitmap PreviewBitmap, int X2Width, int X2Height);

public sealed record ImageEditorSaveResult(int X2Width, int X2Height);
