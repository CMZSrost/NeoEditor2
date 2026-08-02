using CommunityToolkit.Mvvm.Messaging;
using Moq;
using NeoEditor.Core.Model;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Plugins.ImageTools.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.ViewModels;

/// <summary>
/// Phase 9C: Image Orchestration (R27) — getimages.php declaration order,
/// file-existence markers, R27 3-way path resolution, save-write-back, reorder,
/// and auto-refresh on workspace lifecycle messages.
/// </summary>
public class ImageOrchestrationViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly Mock<IModImageListService> _imageList;
    private readonly Mock<INotificationService> _notification;

    public ImageOrchestrationViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"NeoEditorImgOrch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "img"));
        Directory.CreateDirectory(Path.Combine(_root, "Mods", "MyMod", "img"));

        // Base game getimages.php + images
        File.WriteAllText(Path.Combine(_root, "getimages.php"),
            "nRows=2&nCols=2&strImageURL0=a.png&strImageURL1=a@2x.png");
        File.WriteAllText(Path.Combine(_root, "img", "a.png"), "x");
        File.WriteAllText(Path.Combine(_root, "img", "a@2x.png"), "x");
        // Shared game image a mod may reference (R27 resolution fallback).
        File.WriteAllText(Path.Combine(_root, "img", "shared.png"), "x");

        // Mod getimages.php + image
        File.WriteAllText(Path.Combine(_root, "Mods", "MyMod", "getimages.php"),
            "nRows=1&nCols=2&strImageURL0=c.png&strImageURL1=c@2x.png");
        File.WriteAllText(Path.Combine(_root, "Mods", "MyMod", "img", "c.png"), "x");

        _imageList = new Mock<IModImageListService>();
        _imageList.Setup(s => s.ParseImagePairs(It.IsAny<string>()))
            .Returns((string path) => path.Contains("Mods", StringComparison.OrdinalIgnoreCase)
                ? new[] { ("c.png", "c@2x.png"), ("shared.png", "shared@2x.png") }
                : new[] { ("a.png", "a@2x.png"), ("missing.png", "missing@2x.png") });
        _imageList.Setup(s => s.GenerateImagePhp(It.IsAny<IReadOnlyList<(string, string)>>()))
            .Returns("GENERATED-PHP");

        _notification = new Mock<INotificationService>();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private (ImageOrchestrationViewModel Vm, IMessenger Messenger) CreateVm()
    {
        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new AppConfig { GameRootDir = _root });

        var messenger = new WeakReferenceMessenger();
        // Real provider: no profile → legacy Mods/ fallback scan (what the existing
        // tests exercise); a LoadProfileMessage switches it to profile-driven paths.
        var sourceProvider = new ProfileModSourceProvider(config.Object, messenger);
        var vm = new ImageOrchestrationViewModel(
            new Mock<ILocalizationService>().Object,
            config.Object,
            _imageList.Object,
            _notification.Object,
            sourceProvider,
            messenger);

        return (vm, messenger);
    }

    [Fact]
    public async Task Refresh_ReadsBaseGameAndModSources_InDeclarationOrder()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Sources.Count);

        var baseGame = vm.Sources[0];
        Assert.Equal("Base Game", baseGame.Name);
        Assert.True(baseGame.ReadOnly);
        Assert.True(baseGame.HasGetImagesFile);
        Assert.Equal(2, baseGame.Pairs.Count);
        Assert.Equal("a.png", baseGame.Pairs[0].NormalImage);
        Assert.Equal("missing.png", baseGame.Pairs[1].NormalImage);
        Assert.True(baseGame.Pairs[0].NormalExists && baseGame.Pairs[0].X2Exists);
        Assert.True(baseGame.Pairs[1].IsMissing);
        Assert.Equal(1, baseGame.MissingCount);

        var mod = vm.Sources[1];
        Assert.Equal("MyMod", mod.Name);
        Assert.False(mod.ReadOnly);
        Assert.Equal("c.png", mod.Pairs[0].NormalImage);
        Assert.Equal("shared.png", mod.Pairs[1].NormalImage);
        // shared.png only exists in game img/ — R27 3-way resolution must find it.
        Assert.True(mod.Pairs[1].NormalExists);
        Assert.False(mod.Pairs[1].X2Exists);
    }

    [Fact]
    public async Task Refresh_ModWithoutGetImagesFile_AppearsEmpty_AndIsWritable()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Mods", "EmptyMod"));

        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);

        var emptyMod = vm.Sources.First(s => s.Name == "EmptyMod");
        Assert.False(emptyMod.HasGetImagesFile);
        Assert.Empty(emptyMod.Pairs);
        Assert.False(emptyMod.ReadOnly);
        vm.SelectedSource = emptyMod;
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_WritesGetImagesPhp_ForModSource()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedSource = vm.Sources.First(s => s.Name == "MyMod");
        Assert.True(vm.SaveCommand.CanExecute(null));

        await vm.SaveCommand.ExecuteAsync(null);

        var modPhp = Path.Combine(_root, "Mods", "MyMod", "getimages.php");
        Assert.Equal("GENERATED-PHP", File.ReadAllText(modPhp));
        _notification.Verify(n => n.ShowSuccess(It.IsAny<string>(), "Image Orchestration"), Times.Once);
    }

    [Fact]
    public async Task Save_IsDisabled_ForReadOnlyBaseGame()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedSource = vm.Sources[0];

        Assert.True(vm.SelectedSource.ReadOnly);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task MoveDown_ReordersPairs()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedSource = vm.Sources.First(s => s.Name == "MyMod");

        // SelectedPair defaults to the first pair (index 0).
        Assert.False(vm.MoveUpCommand.CanExecute(null));
        Assert.True(vm.MoveDownCommand.CanExecute(null));

        vm.MoveDownCommand.Execute(null);

        Assert.Equal("shared.png", vm.SelectedSource.Pairs[0].NormalImage);
        Assert.Equal("c.png", vm.SelectedSource.Pairs[1].NormalImage);
        // The selection follows the moved item down (now at index 1).
        Assert.NotNull(vm.SelectedPair);
        Assert.Equal("c.png", vm.SelectedPair.NormalImage);
    }

    [Fact]
    public async Task DeletePair_RemovesFromDeclaration()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedSource = vm.Sources.First(s => s.Name == "MyMod");

        var initialCount = vm.SelectedSource.Pairs.Count;
        vm.DeletePairCommand.Execute(null);

        Assert.Equal(initialCount - 1, vm.SelectedSource.Pairs.Count);
        Assert.DoesNotContain(vm.SelectedSource.Pairs, p => p.NormalImage == "c.png");
    }

    [Fact]
    public async Task TreeModel_IsBuiltWithSourcesAsRoots()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);

        // SetRoots materialized the source roots into the hierarchical model.
        Assert.NotNull(vm.TreeModel.Root);
    }

    [Fact]
    public async Task Selection_OnSourceRow_SelectsSourceAndFirstPair()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);

        var mod = vm.Sources.First(s => s.Name == "MyMod");
        vm.SelectedRow = mod;

        Assert.Same(mod, vm.SelectedSource);
        Assert.Same(mod.Pairs[0], vm.SelectedPair);
    }

    [Fact]
    public async Task Selection_OnPairRow_ResolvesOwningSource()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);

        var mod = vm.Sources.First(s => s.Name == "MyMod");
        var pair = mod.Pairs[1];
        vm.SelectedRow = pair;

        Assert.Same(mod, vm.SelectedSource);
        Assert.Same(pair, vm.SelectedPair);
    }

    [Fact]
    public async Task RefreshModMessage_TriggersAutoRefresh()
    {
        var (vm, messenger) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.DoesNotContain(vm.Sources, s => s.Name == "NewMod");

        // Simulate a mod being created: new folder + getimages.php, then a nudge.
        Directory.CreateDirectory(Path.Combine(_root, "Mods", "NewMod"));
        File.WriteAllText(Path.Combine(_root, "Mods", "NewMod", "getimages.php"), "empty");
        messenger.Send(new RefreshModMessage());

        await WaitUntilAsync(() => vm.Sources.Any(s => s.Name == "NewMod"));
    }

    [Fact]
    public async Task Refresh_UsesProfileModLoadInfoPaths_NotModsConvention()
    {
        // A mod whose directory lives OUTSIDE gameRoot/Mods (e.g. imported from an
        // arbitrary folder) must be found via the profile's ModLoadInfo.Path.
        var externalDir = Path.Combine(_root, "External", "MyMod");
        Directory.CreateDirectory(Path.Combine(externalDir, "img"));
        File.WriteAllText(Path.Combine(externalDir, "getimages.php"),
            "nRows=1&nCols=2&strImageURL0=e.png&strImageURL1=e@2x.png");
        File.WriteAllText(Path.Combine(externalDir, "img", "e.png"), "x");

        var modInfo = new ModInfo { Name = "ExternalMod", ModId = 0, Path = "External/MyMod" };
        var profile = new ProfileInfo { Name = "TestProfile" };
        profile.ModLoadInfos.Add(new ModLoadInfo { Info = modInfo, Namespace = "Ext" });

        var (vm, messenger) = CreateVm();
        messenger.Send(new LoadProfileMessage(profile));

        await WaitUntilAsync(() => vm.Sources.Any(s => s.Name == "ExternalMod"));

        var source = vm.Sources.First(s => s.Name == "ExternalMod");
        Assert.False(source.ReadOnly);
        Assert.True(source.HasGetImagesFile);
        // ContentRoot must come from the profile path, not the legacy Mods/ scan.
        Assert.EndsWith("External/MyMod", source.ContentRoot.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
        // With a profile active, the Mods/ folder mod is no longer a source.
        Assert.DoesNotContain(vm.Sources, s => s.Name == "MyMod");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition not met within timeout");
            }

            await Task.Delay(25);
        }
    }
}