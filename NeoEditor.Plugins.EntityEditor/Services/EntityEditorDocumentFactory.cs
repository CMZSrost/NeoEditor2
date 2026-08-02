using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.ViewModels;
// Use the Infra workspace session (not Core.Abstractions) to match Plugin convention.
using WorkspaceSession = NeoEditor.Services.IWorkspaceSession;

namespace NeoEditor.Plugins.EntityEditor.Services;

/// <summary>
/// Factory implementation for creating EntityEditorDocument instances.
/// Resolves DI services internally so callers don't need to know the constructor signature.
/// </summary>
public class EntityEditorDocumentFactory : IEntityEditorDocumentFactory
{
    private readonly IServiceProvider _services;

    public EntityEditorDocumentFactory(IServiceProvider services)
    {
        _services = services;
    }

    public object CreateDocument(IEntity entity)
    {
        return new EntityEditorDocument(
            entity,
            _services.GetRequiredService<WorkspaceSession>(),
            _services.GetRequiredService<IDbContextFactory<GameDbContext>>(),
            _services.GetRequiredService<IEntityLookupService>(),
            _services.GetRequiredService<ILocalizationService>(),
            _services.GetRequiredService<INotificationService>(),
            _services.GetRequiredService<IReferenceListSerializer>());
    }
}