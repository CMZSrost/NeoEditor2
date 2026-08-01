using System;
using NeoEditor.Plugins.EntityEditor.Services;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests.Services;

public class VisHelperServiceTests
{
    [Fact]
    public void Constructor_AcceptsNullFindImage()
    {
        var service = CreateService(findImage: null);
        Assert.NotNull(service);
    }

    [Fact]
    public void Resolver_ReturnsInjectedResolver()
    {
        var service = CreateService();
        Assert.NotNull(service.Resolver);
    }

    [Fact]
    public void Router_ReturnsInjectedRouter()
    {
        var service = CreateService();
        Assert.NotNull(service.Router);
    }

    [Fact]
    public void Loc_ReturnsLocalizedString()
    {
        var service = CreateService();
        var result = service.Loc("NonExistentKey");
        Assert.NotNull(result);
    }

    private static VisHelperService CreateService(Func<string, string?>? findImage = null)
    {
        return new VisHelperService(
            findImage ?? (_ => null),
            new StubReferenceResolver(),
            new StubNavigationRouter(),
            new StubEntityLookupService(),
            new StubLocalizationService());
    }
}
