using System;
using Avalonia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IConfigService = NeoEditor.Core.Abstractions.IConfigService;
using NeoEditor.Data;
using IXmlParser = NeoEditor.Core.Abstractions.IXmlParser;
using NeoEditor.Data.Context;
using NeoEditor.Infra.Services;
using NeoEditor.Services;
using NeoEditor.UI.Common.Services;

namespace NeoEditor.Helper;

/// <summary>
/// Service locator for View code-behind. Resolves services from the IServiceProvider
/// registered in Application.Current.Resources["Services"] (set in App.OnFrameworkInitializationCompleted).
/// Replaces the V6 App.* static accessors with a single service locator entry point.
/// </summary>
public static class ViewServices
{
    /// <summary>Test hook: set before running tests that need DI without a full Avalonia app.</summary>
    public static IServiceProvider? TestServiceProvider { get; set; }

    private static IServiceProvider ServiceProvider =>
        TestServiceProvider
        ?? (IServiceProvider)(Application.Current?.Resources["Services"]
            ?? throw new InvalidOperationException("Services not initialized"));

    public static T Get<T>() where T : notnull =>
        ServiceProvider.GetRequiredService<T>();

    // ── Convenience accessors for commonly-used services ──

    public static INotificationService Notification => Get<INotificationService>();
    public static ILocalizationService Loc => Get<ILocalizationService>();
    public static IConfigService ConfigService => Get<IConfigService>();
    public static EntityVisualizerRegistry VisualizerRegistry => Get<EntityVisualizerRegistry>();
    public static Services.ISelectionService SelectionService => Get<Services.ISelectionService>();
    public static Services.IModManager ModManager => Get<Services.IModManager>();
    public static IBrowserIndexService BrowserIndex => Get<IBrowserIndexService>();
    public static PhpParser PhpParser => Get<PhpParser>();
    public static INavigationRouter NavigationRouter => Get<INavigationRouter>();
    public static IWorkspaceSession WorkspaceSession => Get<IWorkspaceSession>();
    public static IReferenceResolver ReferenceResolver => Get<IReferenceResolver>();
    public static IDbContextFactory<Data.Context.GameDbContext> GameDbFactory =>
        Get<IDbContextFactory<Data.Context.GameDbContext>>();
    public static IDbContextFactory<Data.Context.EditorDbContext> EditorDbFactory =>
        Get<IDbContextFactory<Data.Context.EditorDbContext>>();
    public static ILoggerFactory LoggerFactory => Get<ILoggerFactory>();
    public static IXmlParser XmlParser => Get<IXmlParser>();
    public static IWorkspacePersistenceService WorkspacePersistence => Get<IWorkspacePersistenceService>();
    public static IProfileManager ProfileManager => Get<IProfileManager>();
    public static IMergeService MergeService => Get<IMergeService>();
    public static NeoEditor.Core.Abstractions.IHostService HostService =>
        Get<NeoEditor.Core.Abstractions.IHostService>();
}
