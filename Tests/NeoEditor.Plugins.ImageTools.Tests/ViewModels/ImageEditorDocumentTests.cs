using System.IO;
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
/// Image Editor workbench: the 4-slot model (original / pixel-art / AI-generated /
/// AI pixel-art) and the AI generate panel. The AI image must live in its own slot and
/// never leak into the original or processed slots. The pixelation pipeline reads straight
/// from the AI image's PNG bytes (ImageSharp is pure managed code), so these tests run
/// without a windowing platform — the headless Avalonia platform is only needed where a
/// <c>Bitmap</c> is actually decoded.
/// </summary>
public class ImageEditorDocumentTests
{
    static ImageEditorDocumentTests()
    {
        // Tests that decode Avalonia Bitmaps need the headless platform (Skia).
        TestApp.EnsureAvaloniaInitialized();
    }

    private static ImageEditorDocument CreateDoc(IImageGenerationService? imageGen = null)
    {
        return new ImageEditorDocument(
            new Mock<IImageEditorProcessingService>().Object,
            new PixelArtConversionService(),
            imageGen ?? new Mock<IImageGenerationService>().Object,
            new Mock<ILocalizationService>().Object);
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var img = new Image<Rgba32>(width, height);
        img.Mutate(ctx => ctx.BackgroundColor(Color.Red));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public void AiGenerateCommand_CanExecute_FollowsAvailabilityAndPrompt()
    {
        var available = new Mock<IImageGenerationService>();
        available.Setup(g => g.IsAvailable).Returns(true);
        var doc = CreateDoc(available.Object);

        // Empty prompt → cannot generate even when available.
        Assert.False(doc.AiGenerateCommand.CanExecute(null));

        doc.AiPrompt = "a pixel-art sword";
        Assert.True(doc.AiGenerateCommand.CanExecute(null));

        var unavailable = new Mock<IImageGenerationService>();
        unavailable.Setup(g => g.IsAvailable).Returns(false);
        var doc2 = CreateDoc(unavailable.Object);
        doc2.AiPrompt = "a pixel-art sword";
        Assert.False(doc2.AiGenerateCommand.CanExecute(null));
    }

    [Fact]
    public void LoadGeneratedImage_SetsAiSlotOnly_NotOriginalOrProcessed()
    {
        var doc = CreateDoc();

        doc.LoadGeneratedImage(CreatePngBytes(16, 16), "ai.png");

        Assert.NotNull(doc.AiImage);
        Assert.True(doc.HasAiImage);
        Assert.False(doc.HasNoAiImage);
        // The AI image must not leak into the original / processed slots.
        Assert.Null(doc.SelectedImage);
        Assert.True(doc.HasNoImage);
        Assert.Null(doc.ProcessedImage);
        Assert.True(doc.HasNoProcessedImage);
        Assert.False(doc.HasAiProcessedImage);
        Assert.True(doc.CanSaveAiImage);
        Assert.False(doc.CanSaveOriginalImage);
    }

    [Fact]
    public void SaveCommands_CanExecute_FollowTheirSlotState()
    {
        var doc = CreateDoc();

        // All empty initially.
        Assert.False(doc.SaveOriginalImageCommand.CanExecute(null));
        Assert.False(doc.SaveProcessedImageCommand.CanExecute(null));
        Assert.False(doc.SaveAiImageCommand.CanExecute(null));
        Assert.False(doc.SaveAiProcessedImageCommand.CanExecute(null));

        doc.LoadGeneratedImage(CreatePngBytes(16, 16), "ai.png");

        // Only the AI slot is populated — its save is enabled, the others stay disabled.
        Assert.True(doc.SaveAiImageCommand.CanExecute(null));
        Assert.False(doc.SaveOriginalImageCommand.CanExecute(null));
        Assert.False(doc.SaveProcessedImageCommand.CanExecute(null));
        Assert.False(doc.SaveAiProcessedImageCommand.CanExecute(null));
        Assert.True(doc.CanPixelateAiImage);
    }

    [Fact]
    public async Task PixelateAiImage_ProducesAiProcessedSlot()
    {
        var doc = CreateDoc();
        doc.LoadGeneratedImage(CreatePngBytes(16, 16), "ai.png");
        Assert.True(doc.CanPixelateAiImage);

        await doc.PixelateAiImageCommand.ExecuteAsync(null);

        // The full Avalonia↔ImageSharp pipeline must land in the AI-processed slot
        // without touching the original / processed slots.
        Assert.True(doc.HasAiProcessedImage);
        Assert.NotNull(doc.AiProcessedImage);
        Assert.True(doc.CanSaveAiProcessedImage);
        Assert.Null(doc.SelectedImage);
        Assert.True(doc.HasNoImage);
        Assert.Null(doc.ProcessedImage);
        Assert.True(doc.HasNoProcessedImage);
    }

    [Fact]
    public async Task PixelateAiImage_WithoutAiImage_IsNoOp()
    {
        var doc = CreateDoc();

        await doc.PixelateAiImageCommand.ExecuteAsync(null);

        Assert.False(doc.HasAiProcessedImage);
        Assert.False(doc.CanPixelateAiImage);
    }

    [Fact]
    public async Task AiGenerateAsync_PassesSelectedSize_AndTogglesLoading()
    {
        var imageGen = new Mock<IImageGenerationService>();
        imageGen.Setup(g => g.IsAvailable).Returns(true);
        imageGen.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageGenerationResult(CreatePngBytes(16, 16), "png", 16, 16, null));

        var doc = CreateDoc(imageGen.Object);
        doc.AiPrompt = "a sword";
        doc.AiWidth = 1024;
        doc.AiHeight = 1024;
        Assert.True(doc.CanGenerateAiImage);

        await doc.AiGenerateCommand.ExecuteAsync(null);

        // Loading toggled on during the call and off after.
        Assert.False(doc.IsGeneratingAi);
        // The free size was forwarded as an explicit RequestSize plus Width/Height, and the
        // workbench asks for the raw image (ApplyPixelArt:false) so realistic renders aren't
        // garbled into pixel-art noise automatically.
        imageGen.Verify(g => g.GenerateAsync("a sword",
            It.Is<ImageGenerationOptions>(o => o.Width == 1024 && o.Height == 1024
                && o.RequestSize == "1024x1024"
                && o.ApplyPixelArt == false),
            It.IsAny<CancellationToken>()), Times.Once);
        // Result landed in the AI slot.
        Assert.True(doc.HasAiImage);
        Assert.True(doc.CanSaveAiImage);
    }

    [Fact]
    public void SlotTitles_ShowDimensions_OnlyWhenPopulated()
    {
        var doc = CreateDoc();

        // Empty slots: title is the bare localised name (no dimension suffix).
        Assert.False(doc.HasImage);
        Assert.DoesNotContain("(", doc.OriginalTitle);
        Assert.DoesNotContain("×", doc.OriginalTitle);

        doc.LoadGeneratedImage(CreatePngBytes(16, 16), "ai.png");

        // Populated AI slot: title includes the image dimensions. (Under the headless test
        // platform Avalonia decodes PNGs to a 1×1 placeholder, so we assert the shape — "×" —
        // rather than a concrete pixel count.)
        Assert.True(doc.HasAiImage);
        Assert.Contains("×", doc.AiTitle);
        Assert.Contains("px", doc.AiTitle);
        // Other slots stay bare.
        Assert.DoesNotContain("×", doc.ProcessedTitle);
        Assert.DoesNotContain("×", doc.AiProcessedTitle);
    }

    [Fact]
    public void AiSize_Defaults_AndConstraints_AreCogViewCompatible()
    {
        var doc = CreateDoc();
        // Default free size.
        Assert.Equal(512, doc.AiWidth);
        Assert.Equal(512, doc.AiHeight);
        // CogView constraint: [512, 2880], step 16.
        Assert.Equal(512, doc.AiSizeMin);
        Assert.Equal(2880, doc.AiSizeMax);
        Assert.Equal(16, doc.AiSizeStep);
    }

    [Fact]
    public async Task AiGenerateAsync_OnFailure_SurfacesErrorMessage_AndClearsLoading()
    {
        var imageGen = new Mock<IImageGenerationService>();
        imageGen.Setup(g => g.IsAvailable).Returns(true);
        imageGen.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("size 400x400 is invalid for CogView"));

        var doc = CreateDoc(imageGen.Object);
        doc.AiPrompt = "a sword";
        doc.AiWidth = 400;
        doc.AiHeight = 400;

        await doc.AiGenerateCommand.ExecuteAsync(null);

        // The loading indicator is cleared and the failure is surfaced (not swallowed),
        // so the user isn't left with an empty AI slot and no explanation.
        Assert.False(doc.IsGeneratingAi);
        Assert.True(doc.HasAiGenerationError);
        Assert.Contains("400", doc.AiGenerationError);
        Assert.False(doc.HasAiImage);
        Assert.True(doc.HasNoAiImage);
    }
}
