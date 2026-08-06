using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Helper;
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
            if (serviceType == typeof(NeoEditor.Core.Abstractions.IHostService))
                return new StubHostService();
            if (serviceType == typeof(IEntityLookupService))
                return new StubEntityLookupService();
            if (serviceType == typeof(NeoEditor.Infra.Services.ILocalizationService))
                return new StubLocalizationService();
            if (serviceType == typeof(INotificationService))
                return new StubNotificationService();
            if (serviceType == typeof(NeoEditor.Core.Abstractions.IReferenceListSerializer))
                return new ReferenceListSerializer();
            if (serviceType == typeof(NeoEditor.Core.Abstractions.IXmlParser))
                return new StubXmlParser();
            if (serviceType == typeof(IConfigService))
                return new StubConfigService();
            throw new InvalidOperationException($"Unexpected service request: {serviceType}");
        }
    }
}
