using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// File-system side of the image tools: picking images, saving a bitmap as a
/// PNG pair (normal + x2_ version), naming conventions, and bitmap encoding /
/// decoding. Keeps file IO and platform storage out of the view models.
/// </summary>
public interface IImageFileService
{
    /// <summary>Open the platform file picker for images. Returns full paths
    /// (empty when the user cancels).</summary>
    Task<string[]> PickImagesAsync(bool allowMultiple);

    /// <summary>
    /// Show the save dialog (always) and write the bitmap as PNG plus a 2×
    /// (x2_) version next to it. Save failures are swallowed — the preview stays.
    /// </summary>
    Task SaveAsync(Bitmap bitmap, string suggestedName);

    /// <summary>Suggested file name for the normal output derived from the
    /// source image name ("pixelated.png" when no source name).</summary>
    string GetSuggestedFileName(string? sourceName);

    /// <summary>x2_ prefixed file name for the 2× version of a normal output.</summary>
    string GetSuggestedX2FileName(string normalFileName);

    /// <summary>Strip an x2_ prefix and normalize the extension to .png.</summary>
    string NormalizeNormalOutputFileName(string fileName);

    /// <summary>Decode PNG bytes via a temp file (Avalonia Bitmap(Stream) keeps a
    /// reference to the stream; disposing it can garble Skia rendering).</summary>
    Bitmap FromBytes(byte[] pngBytes);

    /// <summary>Decode a bitmap from a file path.</summary>
    Bitmap FromFile(string path);

    /// <summary>Encode an ImageSharp image as PNG and decode it into an Avalonia
    /// Bitmap (temp-file roundtrip, see <see cref="FromBytes"/>).</summary>
    Bitmap FromImageSharp(Image<Rgba32> image);

    /// <summary>Stage AI-generated PNG bytes to the temp session directory and
    /// return the file path (used by the create-image flow).</summary>
    string StageAiCandidate(byte[] pngBytes, string name);

    /// <summary>Remove staged AI-candidate files left over from a previous session
    /// (called when the create-image document is built).</summary>
    void CleanupStagedCandidates();
}
