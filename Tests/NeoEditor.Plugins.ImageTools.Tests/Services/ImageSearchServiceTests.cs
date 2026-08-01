using Xunit;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

public class ImageSearchServiceTests
{
    private readonly ImageSearchService _service = new();

    [Fact]
    public void GetImageSearchDirsForEntity_EmptyGameRoot_ReturnsEmpty()
    {
        var result = _service.GetImageSearchDirsForEntity(string.Empty, null);
        Assert.Empty(result);
    }

    [Fact]
    public void GetImageSearchDirsForEntity_NullGameRoot_ReturnsEmpty()
    {
        var result = _service.GetImageSearchDirsForEntity(null!, null);
        Assert.Empty(result);
    }

    [Fact]
    public void GetImageSearchDirsForEntity_ValidGameRoot_IncludesGameImgDir()
    {
        // The method always includes the img subdirectory path
        var result = _service.GetImageSearchDirsForEntity(@"C:\Game", null);
        Assert.Contains(@"C:\Game\img", result);
    }
}
