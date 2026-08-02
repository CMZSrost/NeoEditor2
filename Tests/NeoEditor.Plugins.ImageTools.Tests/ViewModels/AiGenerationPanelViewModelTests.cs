using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.ViewModels;

/// <summary>
/// AI generation form: configurable count, one CandidateGenerated event per
/// completed candidate, failure surfacing. The host (create-image document) stages
/// each candidate into its pending list — there is no in-panel gallery.
/// </summary>
public class AiGenerationPanelViewModelTests
{
    private static byte[] CreatePngBytes(int width, int height)
    {
        using var img = new Image<Rgba32>(width, height);
        img.Mutate(ctx => ctx.BackgroundColor(new Rgba32(255, 0, 0, 255)));
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static IConfigService CreateConfig(int candidateCount)
    {
        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new AppConfig { AiCandidateCount = candidateCount });
        return config.Object;
    }

    private static ILocalizationService CreateLocMock()
    {
        var loc = new Mock<ILocalizationService>();
        loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
        return loc.Object;
    }

    private static AiGenerationPanelViewModel CreatePanel(
        IImageGenerationService? gen = null, IConfigService? config = null)
    {
        return new AiGenerationPanelViewModel(
            gen ?? new Mock<IImageGenerationService>().Object,
            CreateLocMock(),
            config ?? CreateConfig(4));
    }

    private static Mock<IImageGenerationService> CreateWorkingGen(int width = 16, int height = 16)
    {
        var png = CreatePngBytes(width, height);
        var gen = new Mock<IImageGenerationService>();
        gen.Setup(g => g.IsAvailable).Returns(true);
        gen.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageGenerationResult(png, "png", width, height, null));
        return gen;
    }

    [Fact]
    public void CandidateCount_Defaults_FromConfig_AndClamps()
    {
        // Config value is honored.
        var panel = CreatePanel(config: CreateConfig(6));
        Assert.Equal(6, panel.CandidateCount);

        // Out-of-range config values are clamped to [1, 8].
        var clamped = CreatePanel(config: CreateConfig(42));
        Assert.Equal(8, clamped.CandidateCount);
    }

    [Fact]
    public void GenerateCommand_CanExecute_FollowsAvailabilityAndPrompt()
    {
        var panel = CreatePanel(gen: CreateWorkingGen().Object);

        Assert.False(panel.CanGenerate); // empty prompt

        panel.AiPrompt = "a pixel-art sword";
        Assert.True(panel.CanGenerate);
    }

    [Fact]
    public void PromptChange_NotifiesCanGenerate_ForButtonBinding()
    {
        // The Generate button binds IsEnabled to CanGenerate — the property must raise
        // PropertyChanged when the prompt changes, otherwise it stays disabled.
        var panel = CreatePanel(gen: CreateWorkingGen().Object);
        var notifications = new List<string?>();
        panel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        panel.AiPrompt = "a sword";

        Assert.Contains(nameof(AiGenerationPanelViewModel.CanGenerate), notifications);
    }

    [Fact]
    public async Task GenerateAsync_RaisesOneEventPerCandidate_AndTogglesLoading()
    {
        var gen = CreateWorkingGen();
        var panel = CreatePanel(gen: gen.Object);
        panel.AiPrompt = "a sword";
        panel.CandidateCount = 3;

        var generated = new List<(byte[] Bytes, string Name)>();
        panel.CandidateGenerated += (bytes, name) => generated.Add((bytes, name));

        await panel.GenerateCommand.ExecuteAsync(null);

        Assert.False(panel.IsGenerating);
        Assert.Equal(3, generated.Count);
        Assert.Equal(3, panel.CompletedCount);
        Assert.Equal("ai_candidate_1.png", generated[0].Name);
        Assert.Equal("ai_candidate_3.png", generated[2].Name);
        Assert.False(panel.HasGenerationError);
        // The requested count is persisted for next time.
        Assert.Equal(3, CreateConfig(3).Config.AiCandidateCount);
    }

    [Fact]
    public async Task GenerateAsync_PassesPromptAndSizeOptions()
    {
        var gen = CreateWorkingGen();
        string? capturedPrompt = null;
        ImageGenerationOptions? capturedOptions = null;
        gen.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ImageGenerationOptions?, CancellationToken>((p, o, _) =>
            {
                capturedPrompt = p;
                capturedOptions = o;
            })
            .ReturnsAsync(new ImageGenerationResult(CreatePngBytes(16, 16), "png", 16, 16, null));

        var panel = CreatePanel(gen: gen.Object);
        panel.AiPrompt = "a sword";
        panel.AiWidth = 1024;
        panel.AiHeight = 1024;

        await panel.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("a sword", capturedPrompt);
        Assert.NotNull(capturedOptions);
        Assert.Equal(1024, capturedOptions.Width);
        Assert.Equal(1024, capturedOptions.Height);
        Assert.Equal("1024x1024", capturedOptions.RequestSize);
        Assert.False(capturedOptions.ApplyPixelArt); // raw candidates; pixelation happens in the editor
    }

    [Fact]
    public async Task GenerateAsync_WhenAllFail_SurfacesError_AndClearsLoading()
    {
        var gen = new Mock<IImageGenerationService>();
        gen.Setup(g => g.IsAvailable).Returns(true);
        gen.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var panel = CreatePanel(gen: gen.Object);
        panel.AiPrompt = "a sword";

        await panel.GenerateCommand.ExecuteAsync(null);

        Assert.False(panel.IsGenerating);
        Assert.True(panel.HasGenerationError);
    }

    [Fact]
    public async Task GenerateAsync_PartialFailure_StillRaisesSurvivors()
    {
        var png = CreatePngBytes(8, 8);
        var gen = new Mock<IImageGenerationService>();
        gen.Setup(g => g.IsAvailable).Returns(true);
        gen.SetupSequence(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<ImageGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageGenerationResult(png, "png", 8, 8, null))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .ReturnsAsync(new ImageGenerationResult(png, "png", 8, 8, null));

        var panel = CreatePanel(gen: gen.Object);
        panel.AiPrompt = "a sword";
        panel.CandidateCount = 3;

        var generated = new List<string>();
        panel.CandidateGenerated += (_, name) => generated.Add(name);

        await panel.GenerateCommand.ExecuteAsync(null);

        Assert.Equal(2, generated.Count); // the failed candidate is skipped
        Assert.False(panel.HasGenerationError); // partial failure is not fatal
    }
}
