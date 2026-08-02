using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Infra.Services;
using Xunit;

namespace NeoEditor.Plugins.EntityEditor.Tests.Services;

public class EntityEditorDocumentFactoryTests
{
    [Fact]
    public void CreateDocument_ReturnsEntityEditorDocument()
    {
        var factory = new EntityEditor.Services.EntityEditorDocumentFactory(new StubServiceProvider());
        var entity = new StubEntity("test-entity", "Test Entity");

        var result = factory.CreateDocument(entity);

        Assert.NotNull(result);
        Assert.IsType<ViewModels.EntityEditorDocument>(result);
    }

    [Fact]
    public void CreateDocument_SetsCorrectEntity()
    {
        var factory = new EntityEditor.Services.EntityEditorDocumentFactory(new StubServiceProvider());
        var entity = new StubEntity("test-entity", "Test Entity");

        var doc = (ViewModels.EntityEditorDocument)factory.CreateDocument(entity);

        Assert.NotNull(doc.Entity);
        Assert.Equal("test-entity", doc.Entity.EntityId);
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(NeoEditor.Services.IWorkspaceSession))
                return new StubWorkspaceSession();
            if (serviceType == typeof(IDbContextFactory<GameDbContext>))
                return new StubDbContextFactory();
            if (serviceType == typeof(IEntityLookupService))
                return new StubEntityLookupService();
            if (serviceType == typeof(NeoEditor.Infra.Services.ILocalizationService))
                return new StubLocalizationService();
            if (serviceType == typeof(INotificationService))
                return new StubNotificationService();
            throw new InvalidOperationException($"Unexpected service request: {serviceType}");
        }
    }

    private sealed class StubDbContextFactory : IDbContextFactory<GameDbContext>
    {
        public GameDbContext CreateDbContext()
            => throw new NotSupportedException("DB context creation not supported in unit tests");
    }
}
