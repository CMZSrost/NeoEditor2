using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Converts images to pixel art style using color quantization (MedianCut),
/// Sobel edge enhancement, Floyd-Steinberg dithering, and transparent background detection.
/// Zero constructor dependencies (matches <see cref="ImageEditorProcessingService"/> pattern).
/// </summary>
public sealed class PixelArtConversionService
{
    private const float EdgeThreshold = 0.15f;
    private const float EdgeDarkenFactor = 0.5f;

    // Floyd-Steinberg error diffusion weights
    private const float DitherRight = 7f / 16f;
    private const float DitherBottomLeft = 3f / 16f;
    private const float DitherBottom = 5f / 16f;
    private const float DitherBottomRight = 1f / 16f;

    // ── Public API ──

    /// <summary>
    /// Load an image from disk and convert it to pixel art style.
    /// </summary>
    public async Task<Image<Rgba32>> ConvertToPixelArtAsync(
        string sourcePath,
        PixelArtConversionOptions options,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var source = Image.Load<Rgba32>(sourcePath);
            return ConvertInternal(source, options);
        }, ct);
    }

    /// <summary>
    /// Convert an already-loaded image to pixel art style.
    /// The source image is not disposed by this method.
    /// </summary>
    public Task<Image<Rgba32>> ConvertToPixelArtAsync(
        Image<Rgba32> source,
        PixelArtConversionOptions options,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            return ConvertInternal(source, options);
        }, ct);
    }

    // ── Pipeline ──

    private static Image<Rgba32> ConvertInternal(Image<Rgba32> source, PixelArtConversionOptions options)
    {
        // Step 1: Downscale to target size (NearestNeighbor for hard edges)
        var downscaled = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(Math.Max(1, options.TargetWidth), Math.Max(1, options.TargetHeight)),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.NearestNeighbor,
        }));

        // Step 2: Color quantization (MedianCut)
        var palette = BuildPaletteMedianCut(downscaled, options.ClampedColorCount, options.TransparentBackground);

        if (options.Dithering)
        {
            ApplyFloydSteinbergDithering(downscaled, palette);
        }
        else
        {
            ApplyPaletteNoDithering(downscaled, palette);
        }

        // Step 3: Edge enhancement
        if (options.EdgeEnhancement)
        {
            ApplySobelEdgeEnhancement(downscaled);
        }

        // Step 4: Transparent background
        if (options.TransparentBackground)
        {
            ApplyTransparentBackground(downscaled);
        }

        return downscaled;
    }

    // ── MedianCut Color Quantization ──

    /// <summary>
    /// Represents an axis-aligned box in RGB color space.
    /// </summary>
    private readonly record struct ColorBox(int RMin, int RMax, int GMin, int GMax, int BMin, int BMax,
        int PixelCount, int[]? Indices)
    {
        public int RangeR => RMax - RMin;
        public int RangeG => GMax - GMin;
        public int RangeB => BMax - BMin;
    }

    private static Rgba32[] BuildPaletteMedianCut(
        Image<Rgba32> image, int colorCount, bool skipTransparent)
    {
        // Collect non-transparent pixels
        var pixels = new List<(byte R, byte G, byte B)>();

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (skipTransparent && pixel.A < 128) continue;
                    pixels.Add((pixel.R, pixel.G, pixel.B));
                }
            }
        });

        if (pixels.Count == 0)
        {
            // All transparent — return a single-color palette
            return [new Rgba32(0, 0, 0, 255)];
        }

        // Use a simple k-means approach instead of full MedianCut for simplicity and correctness.
        // Start with evenly distributed centroids, then iteratively refine.
        return BuildPaletteKMeans(pixels, Math.Min(colorCount, pixels.Count));
    }

    private static Rgba32[] BuildPaletteKMeans(List<(byte R, byte G, byte B)> pixels, int k)
    {
        if (k <= 1)
        {
            // Single color: average of all pixels
            var avgR = (byte)pixels.Average(p => (int)p.R);
            var avgG = (byte)pixels.Average(p => (int)p.G);
            var avgB = (byte)pixels.Average(p => (int)p.B);
            return [new Rgba32(avgR, avgG, avgB, 255)];
        }

        // Initialize centroids by sampling evenly across the sorted pixel list
        var sorted = pixels.OrderBy(p => (p.R * 256 + p.G) * 256 + p.B).ToList();
        var centroids = new (float R, float G, float B)[k];
        for (var i = 0; i < k; i++)
        {
            var idx = i * (sorted.Count - 1) / Math.Max(1, k - 1);
            centroids[i] = (sorted[idx].R, sorted[idx].G, sorted[idx].B);
        }

        // Lloyd's algorithm — iterate up to 20 times
        var assignments = new int[pixels.Count];
        for (var iter = 0; iter < 20; iter++)
        {
            var changed = false;

            // Assign each pixel to nearest centroid
            for (var i = 0; i < pixels.Count; i++)
            {
                var (r, g, b) = pixels[i];
                var bestDist = float.MaxValue;
                var bestIdx = 0;
                for (var j = 0; j < k; j++)
                {
                    var (cr, cg, cb) = centroids[j];
                    var dr = r - cr;
                    var dg = g - cg;
                    var db = b - cb;
                    var dist = dr * dr + dg * dg + db * db;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = j;
                    }
                }

                if (assignments[i] != bestIdx)
                {
                    assignments[i] = bestIdx;
                    changed = true;
                }
            }

            if (!changed) break;

            // Recompute centroids
            var sums = new (float R, float G, float B, int Count)[k];
            for (var i = 0; i < pixels.Count; i++)
            {
                var c = assignments[i];
                var (r, g, b) = pixels[i];
                sums[c].R += r;
                sums[c].G += g;
                sums[c].B += b;
                sums[c].Count++;
            }

            for (var j = 0; j < k; j++)
            {
                if (sums[j].Count > 0)
                {
                    centroids[j] = (
                        sums[j].R / sums[j].Count,
                        sums[j].G / sums[j].Count,
                        sums[j].B / sums[j].Count);
                }
            }
        }

        return centroids.Select(c => new Rgba32((byte)c.R, (byte)c.G, (byte)c.B, 255)).ToArray();
    }

    // ── Palette Application (no dithering) ──

    private static void ApplyPaletteNoDithering(Image<Rgba32> image, Rgba32[] palette)
    {
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (pixel.A < 128)
                    {
                        // Keep fully transparent pixels as-is
                        row[x] = new Rgba32(0, 0, 0, 0);
                        continue;
                    }

                    var nearest = FindNearestPaletteColor(pixel, palette);
                    row[x] = new Rgba32(nearest.R, nearest.G, nearest.B, pixel.A);
                }
            }
        });
    }

    // ── Floyd-Steinberg Dithering ──

    private static void ApplyFloydSteinbergDithering(Image<Rgba32> image, Rgba32[] palette)
    {
        var width = image.Width;
        var height = image.Height;

        // Work in float-space for error accumulation
        var buffer = new (float R, float G, float B, byte A)[height][];
        for (var y = 0; y < height; y++)
        {
            buffer[y] = new (float R, float G, float B, byte A)[width];
        }

        // Read pixels into float buffer
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    buffer[y][x] = (p.R, p.G, p.B, p.A);
                }
            }
        });

        // Apply Floyd-Steinberg error diffusion
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = buffer[y][x];

                if (a < 128)
                {
                    buffer[y][x] = (0, 0, 0, 0);
                    continue;
                }

                // Clamp to valid range
                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);

                var nearest = FindNearestPaletteColorF((r, g, b), palette);
                var errorR = r - nearest.R;
                var errorG = g - nearest.G;
                var errorB = b - nearest.B;

                buffer[y][x] = (nearest.R, nearest.G, nearest.B, a);

                // Distribute error to neighbors
                if (x + 1 < width)
                {
                    var (nr, ng, nb, na) = buffer[y][x + 1];
                    buffer[y][x + 1] = (nr + errorR * DitherRight, ng + errorG * DitherRight,
                        nb + errorB * DitherRight, na);
                }

                if (y + 1 < height)
                {
                    if (x > 0)
                    {
                        var (nr, ng, nb, na) = buffer[y + 1][x - 1];
                        buffer[y + 1][x - 1] = (nr + errorR * DitherBottomLeft, ng + errorG * DitherBottomLeft,
                            nb + errorB * DitherBottomLeft, na);
                    }

                    {
                        var (nr, ng, nb, na) = buffer[y + 1][x];
                        buffer[y + 1][x] = (nr + errorR * DitherBottom, ng + errorG * DitherBottom,
                            nb + errorB * DitherBottom, na);
                    }

                    if (x + 1 < width)
                    {
                        var (nr, ng, nb, na) = buffer[y + 1][x + 1];
                        buffer[y + 1][x + 1] = (nr + errorR * DitherBottomRight, ng + errorG * DitherBottomRight,
                            nb + errorB * DitherBottomRight, na);
                    }
                }
            }
        }

        // Write back
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var (r, g, b, a) = buffer[y][x];
                    row[x] = new Rgba32(
                        (byte)Math.Clamp((int)MathF.Round(r), 0, 255),
                        (byte)Math.Clamp((int)MathF.Round(g), 0, 255),
                        (byte)Math.Clamp((int)MathF.Round(b), 0, 255),
                        a);
                }
            }
        });
    }

    // ── Nearest Palette Color ──

    private static Rgba32 FindNearestPaletteColor(Rgba32 pixel, Rgba32[] palette)
    {
        return FindNearestPaletteColorF((pixel.R, pixel.G, pixel.B), palette);
    }

    private static Rgba32 FindNearestPaletteColorF((float R, float G, float B) pixel, Rgba32[] palette)
    {
        var bestDist = float.MaxValue;
        var bestIdx = 0;
        for (var i = 0; i < palette.Length; i++)
        {
            var dr = pixel.R - palette[i].R;
            var dg = pixel.G - palette[i].G;
            var db = pixel.B - palette[i].B;
            var dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
            }
        }

        return palette[bestIdx];
    }

    // ── Sobel Edge Enhancement ──

    private static void ApplySobelEdgeEnhancement(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;

        // Compute luminance for each pixel
        var luminance = new float[height, width];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    luminance[y, x] = 0.299f * p.R + 0.587f * p.G + 0.114f * p.B;
                }
            }
        });

        // Compute gradient magnitude using Sobel operators
        var maxGradient = 0f;
        var gradient = new float[height, width];
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var gx = -1f * luminance[y - 1, x - 1] + 1f * luminance[y - 1, x + 1]
                         - 2f * luminance[y, x - 1] + 2f * luminance[y, x + 1]
                         - 1f * luminance[y + 1, x - 1] + 1f * luminance[y + 1, x + 1];

                var gy = -1f * luminance[y - 1, x - 1] - 2f * luminance[y - 1, x] - 1f * luminance[y - 1, x + 1]
                         + 1f * luminance[y + 1, x - 1] + 2f * luminance[y + 1, x] + 1f * luminance[y + 1, x + 1];

                var mag = MathF.Sqrt(gx * gx + gy * gy);
                gradient[y, x] = mag;
                if (mag > maxGradient) maxGradient = mag;
            }
        }

        // Normalize and darken edge pixels
        if (maxGradient < 1f) return; // No meaningful edges

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 1; y < accessor.Height - 1; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 1; x < row.Length - 1; x++)
                {
                    var normalizedGradient = gradient[y, x] / maxGradient;
                    if (normalizedGradient > EdgeThreshold)
                    {
                        var p = row[x];
                        if (p.A < 128) continue; // Skip transparent

                        var strength = (normalizedGradient - EdgeThreshold) / (1f - EdgeThreshold);
                        var factor = 1f - strength * (1f - EdgeDarkenFactor);

                        row[x] = new Rgba32(
                            (byte)Math.Clamp((int)(p.R * factor), 0, 255),
                            (byte)Math.Clamp((int)(p.G * factor), 0, 255),
                            (byte)Math.Clamp((int)(p.B * factor), 0, 255),
                            p.A);
                    }
                }
            }
        });
    }

    // ── Transparent Background Detection ──

    private static void ApplyTransparentBackground(Image<Rgba32> image)
    {
        var width = image.Width;
        var height = image.Height;

        if (width < 3 || height < 3) return;

        // Sample the four corner pixels
        var corners = new List<Rgba32>(4);
        image.ProcessPixelRows(accessor =>
        {
            corners.Add(accessor.GetRowSpan(0)[0]);
            corners.Add(accessor.GetRowSpan(0)[width - 1]);
            corners.Add(accessor.GetRowSpan(height - 1)[0]);
            corners.Add(accessor.GetRowSpan(height - 1)[width - 1]);
        });

        // Check if ≥3 corners share a similar color (tolerance = 40 per channel)
        const int tolerance = 40;
        var bgColor = FindDominantCornerColor(corners, tolerance);
        if (bgColor is null) return;

        var (bgR, bgG, bgB) = bgColor.Value;

        // Make all pixels matching the background color transparent
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    if (p.A < 128) continue; // Already transparent

                    if (Math.Abs(p.R - bgR) <= tolerance &&
                        Math.Abs(p.G - bgG) <= tolerance &&
                        Math.Abs(p.B - bgB) <= tolerance)
                    {
                        row[x] = new Rgba32(0, 0, 0, 0);
                    }
                }
            }
        });
    }

    private static (byte R, byte G, byte B)? FindDominantCornerColor(
        List<Rgba32> corners, int tolerance)
    {
        // Count how many corners are "similar" to each corner
        var maxSimilar = 0;
        (byte R, byte G, byte B)? dominant = null;

        foreach (var a in corners)
        {
            if (a.A < 128) continue; // Skip already transparent corners
            var similar = corners.Count(b =>
                b.A >= 128 &&
                Math.Abs(a.R - b.R) <= tolerance &&
                Math.Abs(a.G - b.G) <= tolerance &&
                Math.Abs(a.B - b.B) <= tolerance);

            if (similar >= 3 && similar > maxSimilar)
            {
                maxSimilar = similar;
                dominant = (a.R, a.G, a.B);
            }
        }

        return dominant;
    }
}
