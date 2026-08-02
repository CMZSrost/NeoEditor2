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
/// Phase 9C: Image Browser (R27) — file-system-only tree (never parses getimages.php),
/// @2x pairing, search filter, and auto-refresh on workspace lifecycle messages.
/// </summary>
public class ImageAssetManagerViewModelTests : IDisposable
{
    private readonly string _root;

    public ImageAssetManagerViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"NeoEditorImgBrowser_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "img"));
        Directory.CreateDirectory(Path.Combine(_root, "Mods", "MyMod", "img"));

        // Base game images (a.png paired with @2x; orphan.png has no x2).
        File.WriteAllText(Path.Combine(_root, "img", "a.png"), "x");
        File.WriteAllText(Path.Combine(_root, "img", "a@2x.png"), "x");
        File.WriteAllText(Path.Combine(_root, "img", "orphan.png"), "x");
        // Base game getimages.php exists — Browser must IGNORE it (R27).
        File.WriteAllText(Path.Combine(_root, "getimages.php"), "nRows=1&strImageURL0=not_in_dir.png");

        // Mod image.
        File.WriteAllText(Path.Combine(_root, "Mods", "MyMod", "img", "b.png"), "x");
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

    private (ImageAssetManagerViewModel Vm, IMessenger Messenger) CreateVm()
    {
        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new AppConfig { GameRootDir = _root });

        var messenger = new WeakReferenceMessenger();
        // Real provider: no profile → legacy Mods/ fallback scan (what the existing
        // tests exercise); a LoadProfileMessage switches it to profile-driven paths.
        var sourceProvider = new ProfileModSourceProvider(config.Object, messenger);
        var vm = new ImageAssetManagerViewModel(
            new Mock<ILocalizationService>().Object,
            config.Object,
            sourceProvider,
            messenger);

        return (vm, messenger);
    }

    [Fact]
    public async Task Refresh_BuildsFileSystemTree_IgnoringGetImagesPhp()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.ModNodes.Count);

        var baseNode = vm.ModNodes.First(n => n.Name == "Base Game");
        // Only files under img/ — the getimages.php-declared "not_in_dir.png" must NOT appear.
        Assert.Equal(2, baseNode.Children.Count);
        var aNode = baseNode.Children.First(c => c.Name == "a.png");
        Assert.True(aNode.IsImage);
        Assert.NotNull(aNode.X2ImagePath);
        Assert.Null(baseNode.Children.First(c => c.Name == "orphan.png").X2ImagePath);

        var modNode = vm.ModNodes.First(n => n.Name == "MyMod");
        Assert.Single(modNode.Children);
        Assert.Equal("b.png", modNode.Children[0].Name);
    }

    [Fact]
    public async Task Search_ShowsMatchingModFully_AndFiltersChildren()
    {
        var (vm, _) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.SearchText = "MyMod";
        Assert.Single(vm.ModNodes);
        Assert.Equal("MyMod", vm.ModNodes[0].Name);

        vm.SearchText = "b.png";
        var filtered = vm.ModNodes.FirstOrDefault(n => n.Name == "MyMod");
        Assert.NotNull(filtered);
        Assert.Single(filtered.Children);
        Assert.Equal("b.png", filtered.Children[0].Name);
    }

    [Fact]
    public async Task GameRootDirChangedMessage_TriggersAutoRefresh()
    {
        var (vm, messenger) = CreateVm();
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.DoesNotContain(vm.ModNodes, n => n.Name == "NewMod");

        // Simulate a new mod being added, then a game-root nudge.
        Directory.CreateDirectory(Path.Combine(_root, "Mods", "NewMod", "img"));
        File.WriteAllText(Path.Combine(_root, "Mods", "NewMod", "img", "c.png"), "x");
        messenger.Send(new GameRootDirChangedMessage(_root));

        await WaitUntilAsync(() => vm.ModNodes.Any(n => n.Name == "NewMod"));
    }

    [Fact]
    public async Task Refresh_FindsImgUnderProfileModLoadInfoPaths()
    {
        // A mod dir outside gameRoot/Mods must be scanned at the profile's path.
        var externalDir = Path.Combine(_root, "External", "MyMod");
        Directory.CreateDirectory(Path.Combine(externalDir, "img"));
        File.WriteAllText(Path.Combine(externalDir, "img", "ext.png"), "x");

        var modInfo = new ModInfo { Name = "ExternalMod", ModId = 0, Path = "External/MyMod" };
        var profile = new ProfileInfo { Name = "TestProfile" };
        profile.ModLoadInfos.Add(new ModLoadInfo { Info = modInfo, Namespace = "Ext" });

        var (vm, messenger) = CreateVm();
        messenger.Send(new LoadProfileMessage(profile));

        await WaitUntilAsync(() => vm.ModNodes.Any(n => n.Name == "ExternalMod"));

        var node = vm.ModNodes.First(n => n.Name == "ExternalMod");
        Assert.Single(node.Children);
        Assert.Equal("ext.png", node.Children[0].Name);
        // With a profile active, the Mods/ folder mod is no longer a source.
        Assert.DoesNotContain(vm.ModNodes, n => n.Name == "MyMod");
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