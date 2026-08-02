using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Moq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Plugins.ImageTools.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.ViewModels;

/// <summary>
/// Image slot: bitmap ownership (dispose-on-replace + Dispose), source bytes,
/// title with dimensions, crop state (crop-enabled slot only), and the save
/// command wired to the parent document.
/// </summary>
public class ImageSlotViewModelTests
{
    static ImageSlotViewModelTests()
    {
        TestApp.EnsureAvaloniaInitialized();
    }

    private static ILocalizationService CreateLocMock()
    {
        var loc = new Mock<ILocalizationService>();
        loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        return loc.Object;
    }

    private static ImageSlotViewModel CreateSlot(bool isCropEnabled = false)
    {
        var loc = CreateLocMock();
        return new ImageSlotViewModel(loc, new ImageFileService(loc), isCropEnabled, "Slot", "Empty");
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var img = new Image<Rgba32>(width, height);
        img.Mutate(ctx => ctx.BackgroundColor(new Rgba32(255, 0, 0, 255)));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static string WriteTempPng(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"neoeditor-slot-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, CreatePngBytes(width, height));
        return path;
    }

    [Fact]
    public void LoadFile_SetsImageMetadataAndTitle()
    {
        var slot = CreateSlot();
        var path = WriteTempPng(16, 16);
        try
        {
            slot.LoadFile(path);

            Assert.True(slot.HasImage);
            Assert.False(slot.HasNoImage);
            Assert.Equal(Path.GetFullPath(path), slot.FilePath);
            Assert.Equal(Path.GetFileName(path), slot.ImageName);
            Assert.Null(slot.SourceBytes);
            Assert.Contains("×", slot.Title);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadBytes_KeepsSourceBytes_ForPixelationPipeline()
    {
        var slot = CreateSlot();
        slot.LoadBytes(CreatePngBytes(8, 8), "candidate.png");

        Assert.True(slot.HasImage);
        Assert.NotNull(slot.SourceBytes);
        Assert.Equal("candidate.png", slot.ImageName);
        Assert.Equal(string.Empty, slot.FilePath);
    }

    [Fact]
    public void LoadFile_MissingPath_ClearsSlot()
    {
        var slot = CreateSlot();
        slot.LoadFile(Path.Combine(Path.GetTempPath(), "does-not-exist.png"));

        Assert.False(slot.HasImage);
        Assert.Equal(string.Empty, slot.ImageName);
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        var slot = CreateSlot();
        slot.LoadBytes(CreatePngBytes(8, 8), "candidate.png");
        Assert.True(slot.HasImage);

        slot.Clear();

        Assert.False(slot.HasImage);
        Assert.Null(slot.SourceBytes);
        Assert.Equal(string.Empty, slot.ImageName);
    }

    [Fact]
    public void SetCropBounds_OnlyOnCropEnabledSlot()
    {
        // Crop-enabled slot: a real-size bitmap (headless decodes PNGs to 1×1, so use
        // a WriteableBitmap) accepts a crop.
        var enabled = CreateSlot(isCropEnabled: true);
        enabled.ShowBitmap(new WriteableBitmap(new PixelSize(64, 64), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque));
        enabled.SetCropBounds(4, 4, 20, 20);

        Assert.NotNull(enabled.CropRect);
        Assert.Equal(4, enabled.CropRect.Value.X);
        Assert.Equal(4, enabled.CropRect.Value.Y);
        Assert.Equal(16, enabled.CropRect.Value.Width);
        Assert.Equal(16, enabled.CropRect.Value.Height);
        Assert.True(enabled.HasSelection);

        // Crop-disabled slot ignores SetCropBounds.
        var disabled = CreateSlot(isCropEnabled: false);
        disabled.ShowBitmap(new WriteableBitmap(new PixelSize(64, 64), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque));
        disabled.SetCropBounds(4, 4, 20, 20);

        Assert.Null(disabled.CropRect);
        Assert.False(disabled.HasSelection);
    }

    [Fact]
    public void SetCropBounds_ClampsToImageBounds()
    {
        var slot = CreateSlot(isCropEnabled: true);
        slot.ShowBitmap(new WriteableBitmap(new PixelSize(32, 32), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque));

        slot.SetCropBounds(-10, 0, 100, 50);

        Assert.NotNull(slot.CropRect);
        Assert.Equal(0, slot.CropRect.Value.X);
        Assert.Equal(0, slot.CropRect.Value.Y);
        Assert.Equal(32, slot.CropRect.Value.Right);
        Assert.Equal(32, slot.CropRect.Value.Bottom);
    }

    [Fact]
    public async Task SaveCommand_CallsHandler_WithSuggestedName()
    {
        var slot = CreateSlot();
        slot.LoadBytes(CreatePngBytes(8, 8), "sword.png");

        (Avalonia.Media.Imaging.Bitmap? bitmap, string name) = (null, string.Empty);
        slot.SetSaveHandler((b, n) =>
        {
            bitmap = b;
            name = n;
            return Task.CompletedTask;
        });

        Assert.True(slot.SaveCommand.CanExecute(null));
        await slot.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(bitmap);
        Assert.Equal("sword.png", name); // GetSuggestedFileName from the slot's image name
    }

    [Fact]
    public void Dispose_ReleasesBitmap()
    {
        var slot = CreateSlot();
        slot.LoadBytes(CreatePngBytes(8, 8), "candidate.png");
        Assert.True(slot.HasImage);

        slot.Dispose();

        Assert.False(slot.HasImage);
        Assert.Null(slot.SourceBytes);
    }
}
