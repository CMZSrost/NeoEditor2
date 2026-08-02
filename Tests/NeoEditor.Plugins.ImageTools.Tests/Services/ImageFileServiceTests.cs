using System;
using System.IO;
using Moq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Services;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

/// <summary>
/// File service: naming conventions (x2_ pairs), PNG pair helpers, and the staged
/// AI-candidate lifecycle (stage → exists → cleanup).
/// </summary>
public class ImageFileServiceTests
{
    private static ImageFileService CreateService()
    {
        var loc = new Mock<ILocalizationService>();
        loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        return new ImageFileService(loc.Object);
    }

    [Theory]
    [InlineData(null, "pixelated.png")]
    [InlineData("", "pixelated.png")]
    [InlineData("sword.png", "sword.png")]
    [InlineData("sword.jpg", "sword.png")]
    [InlineData("dir/sword.bmp", "sword.png")]
    public void GetSuggestedFileName_FromSourceName(string? sourceName, string expected)
    {
        Assert.Equal(expected, CreateService().GetSuggestedFileName(sourceName));
    }

    [Fact]
    public void GetSuggestedX2FileName_PrefixesNormalName()
    {
        var service = CreateService();
        Assert.Equal("x2_sword.png", service.GetSuggestedX2FileName("sword.png"));
        // An already-x2 name is not doubled.
        Assert.Equal("x2_sword.png", service.GetSuggestedX2FileName("x2_sword.png"));
    }

    [Theory]
    [InlineData("sword.png", "sword.png")]
    [InlineData("x2_sword.png", "sword.png")]
    [InlineData("X2_SWORD.PNG", "SWORD.png")]
    [InlineData("dir/x2_sword.png", "sword.png")]
    [InlineData("dir/sword.png", "sword.png")]
    public void NormalizeNormalOutputFileName_StripsPrefixAndNormalizes(string input, string expected)
    {
        Assert.Equal(expected, CreateService().NormalizeNormalOutputFileName(input));
    }

    [Fact]
    public void StageAiCandidate_WritesFile_AndCleanupRemovesIt()
    {
        var service = CreateService();
        var path = service.StageAiCandidate([1, 2, 3, 4], "ai_candidate_1.png");

        Assert.True(File.Exists(path));
        Assert.Equal([1, 2, 3, 4], File.ReadAllBytes(path));

        // A second candidate survives cleanup of the first? Cleanup removes everything.
        var path2 = service.StageAiCandidate([5, 6], "ai_candidate_2.png");
        Assert.True(File.Exists(path2));

        service.CleanupStagedCandidates();

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path2));
    }
}
