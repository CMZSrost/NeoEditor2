using Moq;
using NeoEditor.Core.Model;
using NeoEditor.Data.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Plugins.ImageTools.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.ImageTools.Tests.Services;

public class ModImagesDocumentFactoryTests
{
    [Fact]
    public void CreateDocument_ReturnsModImagesDocument()
    {
        var factory = new ModImagesDocumentFactory(CreateServiceProvider());
        var modInfo = new ModInfo { ModId = 7, Name = "TestMod", Path = "Mods/TestMod" };

        var result = factory.CreateDocument(modInfo);

        Assert.NotNull(result);
        Assert.IsType<ModImagesDocument>(result);
    }

    [Fact]
    public void CreateDocument_SetsCorrectModInfo()
    {
        var factory = new ModImagesDocumentFactory(CreateServiceProvider());
        var modInfo = new ModInfo { ModId = 7, Name = "TestMod", Path = "Mods/TestMod" };

        var doc = (ModImagesDocument)factory.CreateDocument(modInfo);

        Assert.NotNull(doc.ModInfo);
        Assert.Equal(7, doc.ModInfo.ModId);
        Assert.Equal("TestMod", doc.ModInfo.Name);
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var config = new Mock<IConfigService>();
        config.SetupGet(c => c.Config).Returns(new AppConfig());

        var listService = new Mock<IModImageListService>();
        listService.Setup(s => s.ParseImagePairs(It.IsAny<string>()))
            .Returns(Array.Empty<(string NormalImage, string X2Image)>());

        var notification = new Mock<INotificationService>();
        var loc = new Mock<ILocalizationService>();

        return new StubServiceProvider(
            config.Object, listService.Object, notification.Object, loc.Object);
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly IConfigService _config;
        private readonly IModImageListService _listService;
        private readonly INotificationService _notification;
        private readonly ILocalizationService _loc;

        public StubServiceProvider(IConfigService config, IModImageListService listService,
            INotificationService notification, ILocalizationService loc)
        {
            _config = config;
            _listService = listService;
            _notification = notification;
            _loc = loc;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IConfigService)) return _config;
            if (serviceType == typeof(IModImageListService)) return _listService;
            if (serviceType == typeof(ModImagePairDropHandler)) return new ModImagePairDropHandler();
            if (serviceType == typeof(INotificationService)) return _notification;
            if (serviceType == typeof(ILocalizationService)) return _loc;
            throw new InvalidOperationException($"Unexpected service request: {serviceType}");
        }
    }
}
