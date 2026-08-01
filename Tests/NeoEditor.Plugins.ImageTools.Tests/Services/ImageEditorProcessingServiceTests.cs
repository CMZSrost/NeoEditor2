using Xunit;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

public class ImageEditorProcessingServiceTests
{
    private readonly ImageEditorProcessingService _service;

    public ImageEditorProcessingServiceTests()
    {
        var pixelArtService = new PixelArtConversionService();
        _service = new ImageEditorProcessingService(pixelArtService);
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var pixelArtService = new PixelArtConversionService();
        var service = new ImageEditorProcessingService(pixelArtService);
        Assert.NotNull(service);
    }
}
