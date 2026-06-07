using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

/// <summary>Renders Map.StrDef hex grid data as a colored Bitmap.</summary>
public static class HexMapRenderer
{
    private static readonly Color[] DefaultPalette =
    [
        Color.Parse("#1a5276"), Color.Parse("#2e7d32"), Color.Parse("#8d6e63"),
        Color.Parse("#689f38"), Color.Parse("#ff8f00"), Color.Parse("#5d4037"),
        Color.Parse("#7b1fa2"), Color.Parse("#c2185b"), Color.Parse("#0097a7"),
        Color.Parse("#f9a825"), Color.Parse("#33691e"), Color.Parse("#b71c1c"),
        Color.Parse("#455a64"), Color.Parse("#827717"), Color.Parse("#e65100"),
        Color.Parse("#283593"), Color.Parse("#827717"), Color.Parse("#ad1457"),
        Color.Parse("#00838f"), Color.Parse("#4e342e"), Color.Parse("#bf360c"),
        Color.Parse("#311b92"), Color.Parse("#880e4f"), Color.Parse("#004d40"),
        Color.Parse("#558b2f"), Color.Parse("#1b5e20"), Color.Parse("#0d47a1"),
        Color.Parse("#1a237e"), Color.Parse("#b71c1c"), Color.Parse("#212121"),
        Color.Parse("#263238"), Color.Parse("#3e2723"), Color.Parse("#e65100"),
    ];

    /// <summary>Get a color for a HexType ID. Uses HexType name hash if lookup available.</summary>
    public static Color GetHexColor(int hexTypeId, IReadOnlyDictionary<int, HexType>? hexTypes = null)
    {
        if (hexTypes?.TryGetValue(hexTypeId, out var ht) == true)
        {
            int hash = 0;
            foreach (var c in ht.Name) hash = hash * 31 + c;
            return Color.FromUInt32((uint)(0xFF000000 | ((uint)(hash * 2654435761) & 0xFFFFFF)));
        }
        return DefaultPalette[Math.Abs(hexTypeId) % DefaultPalette.Length];
    }

    /// <summary>Parse Map.StrDef into a list of HexType IDs.</summary>
    public static List<int> ParseDefinition(string strDef)
    {
        if (string.IsNullOrWhiteSpace(strDef)) return [];
        return strDef.Split(',')
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
            .ToList();
    }

    /// <summary>Guess map dimensions from hex count. Common Neo Scavenger maps are 50x100.</summary>
    public static (int Width, int Height) GuessDimensions(int hexCount)
    {
        // Common sizes: 50x100=5000, 25x25=625, etc.
        var commonDims = new[] { (50, 100), (100, 50), (25, 25), (40, 80), (60, 60), (30, 70) };
        foreach (var (w, h) in commonDims)
            if (w * h == hexCount) return (w, h);
        // Estimate square-ish
        var side = (int)Math.Sqrt(hexCount);
        return (side, hexCount / side + (hexCount % side > 0 ? 1 : 0));
    }

    /// <summary>Render hex grid to a Bitmap at the given cell size.</summary>
    public static Bitmap Render(string strDef, IReadOnlyDictionary<int, HexType>? hexTypes = null, int cellSize = 6, int? mapWidth = null, int? mapHeight = null)
    {
        var hexes = ParseDefinition(strDef);
        if (hexes.Count == 0) return CreateEmptyBitmap(1, 1);

        var gw = mapWidth ?? 50;
        var gh = mapHeight ?? 100;

        var totalW = gw * cellSize;
        var totalH = gh * cellSize;
        if (totalW < 1 || totalH < 1 || totalW > 4096 || totalH > 4096)
        {
            cellSize = Math.Max(1, Math.Min(cellSize, 4096 / Math.Max(gw, 1)));
            totalW = gw * cellSize;
            totalH = gh * cellSize;
        }

        var bmp = new RenderTargetBitmap(new PixelSize(totalW, totalH));
        using var ctx = bmp.CreateDrawingContext();

        for (int i = 0; i < hexes.Count && i < gw * gh; i++)
        {
            var col = i % gw;
            var row = i / gw;
            if (row >= gh) break;

            var color = GetHexColor(hexes[i], hexTypes);
            var rect = new Rect(col * cellSize, row * cellSize, cellSize, cellSize);
            ctx.FillRectangle(new SolidColorBrush(color), rect);
        }

        return bmp;
    }

    /// <summary>Render at a specific zoom level (bitmap is cellSize * zoom pixels per cell).</summary>
    public static Bitmap RenderZoomed(string strDef, IReadOnlyDictionary<int, HexType>? hexTypes = null, int cellSize = 6, double zoom = 1.0, int? mapWidth = null, int? mapHeight = null)
    {
        return Render(strDef, hexTypes, Math.Max(1, (int)(cellSize * zoom)), mapWidth, mapHeight);
    }

    private static Bitmap CreateEmptyBitmap(int w, int h)
    {
        var bmp = new RenderTargetBitmap(new PixelSize(Math.Max(1, w), Math.Max(1, h)));
        using var ctx = bmp.CreateDrawingContext();
        ctx.FillRectangle(Brushes.Gray, new Rect(0, 0, w, h));
        return bmp;
    }
}
