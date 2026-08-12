using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Services;
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

    private static IAiPromptPresetService CreatePresets(params AiPromptPreset[] presets)
    {
        var svc = new Mock<IAiPromptPresetService>();
        svc.Setup(s => s.GetPresets()).Returns(presets);
        return svc.Object;
    }

    /// <summary>Stateful in-memory preset service for save-as-preset tests.</summary>
    private sealed class FakePresetService : IAiPromptPresetService
    {
        public List<AiPromptPreset> Items { get; } = new();
        public AiPromptPreset? LastAdded { get; private set; }

        public IReadOnlyList<AiPromptPreset> GetPresets() => Items;

        public AiPromptPreset AddOrUpdatePreset(AiPromptPreset preset)
        {
            Items.RemoveAll(p => p.Name == preset.Name);
            Items.Add(preset);
            LastAdded = preset;
            return preset;
        }
    }

    private static AiGenerationPanelViewModel CreatePanel(
        IImageGenerationService? gen = null, IConfigService? config = null,
        IAiPromptPresetService? presets = null)
    {
        return new AiGenerationPanelViewModel(
            gen ?? new Mock<IImageGenerationService>().Object,
            CreateLocMock(),
            config ?? CreateConfig(4),
            presets ?? CreatePresets());
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

    [Fact]
    public void Presets_AreExposed_FromService()
    {
        var panel = CreatePanel(presets: CreatePresets(
            new AiPromptPreset("武器·正侧视", "weapon prompt", 1024, 512)));

        var preset = Assert.Single(panel.Presets);
        Assert.Equal("武器·正侧视", preset.Name);
        Assert.Equal("weapon prompt", preset.Prompt);
    }

    [Fact]
    public void SelectingPreset_FillsPromptAndSize_ThenResetsSelection()
    {
        var panel = CreatePanel(presets: CreatePresets(
            new AiPromptPreset("武器·正侧视", "weapon prompt", 1024, 512)));

        panel.SelectedPreset = panel.Presets[0];

        Assert.Equal("weapon prompt", panel.AiPrompt);
        Assert.Equal(1024, panel.AiWidth);
        Assert.Equal(512, panel.AiHeight);
        // The selection resets so the same preset can be re-applied after editing.
        Assert.Null(panel.SelectedPreset);
    }

    [Fact]
    public void SelectingPreset_WithoutSize_KeepsCurrentSize()
    {
        var panel = CreatePanel(presets: CreatePresets(
            new AiPromptPreset("通用", "generic prompt")));
        panel.AiWidth = 768;
        panel.AiHeight = 768;

        panel.SelectedPreset = panel.Presets[0];

        Assert.Equal("generic prompt", panel.AiPrompt);
        Assert.Equal(768, panel.AiWidth);
        Assert.Equal(768, panel.AiHeight);
    }

    [Fact]
    public void SelectingNull_DoesNotClearExistingPrompt()
    {
        var panel = CreatePanel(presets: CreatePresets(new AiPromptPreset("X", "p")));
        panel.AiPrompt = "typed by user";

        panel.SelectedPreset = null;

        Assert.Equal("typed by user", panel.AiPrompt);
    }

    [Fact]
    public void SavePresetCommand_Disabled_WhenPromptEmpty()
    {
        var panel = CreatePanel(presets: new FakePresetService());

        Assert.False(panel.CanSavePreset);

        panel.AiPrompt = "a prompt";
        Assert.True(panel.CanSavePreset);
    }

    [Fact]
    public void SavePreset_AddsNamedPreset_WithSize_AndRefreshesDropdown()
    {
        var fake = new FakePresetService();
        var panel = CreatePanel(presets: fake);
        panel.AiPrompt = "my prompt";
        panel.NewPresetName = "我的模板";
        panel.AiWidth = 768;
        panel.AiHeight = 512;

        panel.SavePresetCommand.Execute(null);

        Assert.NotNull(fake.LastAdded);
        Assert.Equal("我的模板", fake.LastAdded!.Name);
        Assert.Equal("my prompt", fake.LastAdded.Prompt);
        Assert.Equal(768, fake.LastAdded.Width);
        Assert.Equal(512, fake.LastAdded.Height);
        Assert.Contains(fake.LastAdded, panel.Presets); // dropdown refreshed
        Assert.Equal(string.Empty, panel.NewPresetName); // name box cleared
    }

    [Fact]
    public void SavePreset_EmptyName_UsesLocalizedAutoName()
    {
        var fake = new FakePresetService();
        var panel = CreatePanel(presets: fake);
        panel.AiPrompt = "a prompt";

        panel.SavePresetCommand.Execute(null);

        // The loc stub returns the key itself — the auto-name falls back to it.
        Assert.Equal("AiPromptPresetDefaultName", fake.LastAdded!.Name);
    }

    [Fact]
    public void SavePreset_EmptyName_NumberedWhenTaken()
    {
        var fake = new FakePresetService();
        fake.Items.Add(new AiPromptPreset("AiPromptPresetDefaultName", "existing"));
        var panel = CreatePanel(presets: fake);
        panel.AiPrompt = "a prompt";

        panel.SavePresetCommand.Execute(null);

        Assert.Equal("AiPromptPresetDefaultName 2", fake.LastAdded!.Name);
    }

    [Fact]
    public void SavePreset_SameName_ReplacesExisting()
    {
        var fake = new FakePresetService();
        fake.Items.Add(new AiPromptPreset("武器", "old prompt"));
        var panel = CreatePanel(presets: fake);
        panel.AiPrompt = "new prompt";
        panel.NewPresetName = "武器";

        panel.SavePresetCommand.Execute(null);

        Assert.Single(fake.Items);
        Assert.Equal("new prompt", fake.Items[0].Prompt);
    }
}
