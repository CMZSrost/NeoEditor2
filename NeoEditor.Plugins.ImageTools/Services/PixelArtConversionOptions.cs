namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Parameters controlling the pixel art conversion pipeline.
/// </summary>
public sealed record PixelArtConversionOptions(
    int TargetWidth,
    int TargetHeight,
    int ColorCount = 24,
    bool EdgeEnhancement = true,
    bool Dithering = false,
    bool TransparentBackground = true
)
{
    /// <summary>Minimum allowed color count.</summary>
    public const int MinColorCount = 4;

    /// <summary>Maximum allowed color count.</summary>
    public const int MaxColorCount = 64;

    /// <summary>Returns a color count clamped to the valid range.</summary>
    public int ClampedColorCount =>
        ColorCount < MinColorCount ? MinColorCount :
        ColorCount > MaxColorCount ? MaxColorCount : ColorCount;
}
