using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Xaml.Interactions.DragAndDrop;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model;
using Dock.Model.Avalonia;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Serializer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Options;
using NeoEditor.Helper;
using NeoEditor.Helper.DragDropHandler;
using NeoEditor.Helper.Extensions;
using NeoEditor.ViewModels;
using NeoEditor.Views;
using NeoEditor.Services;
using NeoEditor.ViewModels.Dialog;
using NeoEditor.ViewModels.ExplorerPane;
using NeoEditor.ViewModels.MainContent;
using NeoEditor.Views.Dialog;
using NeoEditor.Views.UserControls;
using ConfigurationManager = Microsoft.Extensions.Configuration.ConfigurationManager;
using ModIndexViewModel = NeoEditor.ViewModels.ExplorerPane.ModIndexViewModel;

namespace NeoEditor;

public partial class App : Application
{
    public IHost _host;

    public static IServiceProvider ServiceProvider { get; set; } = null!;
    public static ILogger<App> Logger { get; set; } = null!;
    public static IConfigService ConfigService { get; set; } = null!;
    public static LocalizationService Localizor { get; set; } = null!;
    public static INotificationService Notification { get; set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder =>
                builder.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                    .Build()
            )
            .ConfigureServices((context, services) =>
            {
                // settings
                services.AddSerilogLogging(context.Configuration);
                services.AddLocalization();
                // options
                services.AddSingleton<IConfigService, ConfigService>();
                services.Configure<CultureSettings>(context.Configuration.GetSection(nameof(CultureSettings)));

                // database
                services.AddSingleton<ProjectDbContextFactory>();
                services.AddDbContextFactory<EditorDbContext>(options =>
                    options
                        .UseSqlite($"Data Source={Constants.EditorDatabasePath}")
                        .LogTo(Console.WriteLine, LogLevel.Warning)
                        .EnableDetailedErrors());
                services.AddDbContextFactory<GameDbContext>(options =>
                    options
                        .UseSqlite($"Data Source={Constants.GameDatabasePath}")
                        .LogTo(Console.WriteLine, LogLevel.Warning)
                        .EnableDetailedErrors());

                // DockServices
                services.AddSingleton<IDockState, DockState>();
                services.AddSingleton<Factory>();
                services.AddSingleton<IFactory>(static sp => sp.GetRequiredService<Factory>());

                services.AddSingleton<DockSerializer>();
                services.AddSingleton<IDockSerializer>(static sp => sp.GetRequiredService<DockSerializer>());

                // services
                services.AddSingleton<IMessenger, WeakReferenceMessenger>();
                services.AddSingleton<LocalizationService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IImageEditorProcessingService, ImageEditorProcessingService>();
                services.AddSingleton<PhpParser>();
                services.AddSingleton<IXmlParser, XmlParser>();
                services.AddSingleton<IModManager, ModManager>();
                services.AddSingleton<IProfileManager, ProfileManager>();
                services.AddSingleton<CsvImportExportService>();
                services.AddSingleton<DataExportService>();
                services.AddSingleton<CustomEditorRegistry>();
                services.AddSingleton<EntityVisualizerRegistry>();
                services.AddSingleton<IImageService, ImageService>();
                services.AddSingleton<IMergeService, MergeService>();
                services.AddSingleton<FilterService>();
                services.AddSingleton<IFilterService>(sp => sp.GetRequiredService<FilterService>());
                services.AddSingleton<SearchService>();
                services.AddSingleton<ISearchService>(sp => sp.GetRequiredService<SearchService>());
                services.AddSingleton<FieldDescriptionService>();
                services.AddSingleton<IWorkspacePersistenceService, WorkspacePersistenceService>();
                services.AddSingleton<Helper.INavigationRouter, Services.NavigationRouter>();

                // Custom table editors
                services.AddSingleton<Views.UserControls.Editors.RecipeFlowchartEditor>();
                services.AddSingleton<Views.UserControls.Editors.StoryTreeEditor>();
                services.AddSingleton<Views.UserControls.Editors.TreasureTreePreviewEditor>();
                services.AddSingleton<Views.UserControls.Editors.IngredientEditor>();
                services.AddSingleton<Views.UserControls.Editors.ItemPropEditor>();
                services.AddSingleton<Views.UserControls.Editors.ItemTypeEditor>();
                services.AddAutoMapper((expression => { }));

                // window
                services.AddTransient<MainWindow>()
                    .AddScoped<MainWindowViewModel>()
                    .AddScoped<MainWindowSideBarViewModel>()
                    .AddScoped<DocumentWorkspaceViewModel>()
                    // Panes
                    .AddScoped<ResourceManagerViewModel>()
                    .AddScoped<SearchPaneViewModel>()
                    .AddScoped<ModDatabaseViewModel>()
                    .AddScoped<SettingsPaneViewModel>()
                    .AddScoped<DataBrowserViewModel>()
                    // MainContents
                    .AddScoped<ModIndexViewModel>();
                services.AddTransient<SearchableDataGrid>();
                services.AddTransient<ModEntryDropHandler>();
                services.AddTransient<ModImagePairDropHandler>();
                // Dialog
                services.AddTransient<CreateModDialog>()
                    .AddTransient<CreateModDialogViewModel>()
                    .AddTransient<RenameImagePairDialog>()
                    .AddTransient<RenameImagePairDialogViewModel>();
            })
            .Build();
    }

    private static void InitDatabase<TContext>(IServiceProvider services) where TContext : DbContext
    {
        using var scope = services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TContext>>();
        using var dbContext = dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    /// <summary>Run lightweight migrations for existing editor.db files that were created before new tables existed.</summary>
    private static void RunEditorDbMigrations(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<Data.Context.EditorDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS command_log (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "TargetType TEXT NOT NULL, " +
            "TargetId INTEGER NOT NULL, " +
            "Sequence INTEGER NOT NULL, " +
            "CommandType TEXT NOT NULL, " +
            "SerializedData TEXT NOT NULL, " +
            "IsUnsaved INTEGER NOT NULL DEFAULT 1, " +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))");
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS IX_command_log_TargetType_TargetId ON command_log (TargetType, TargetId)");
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS workspace_snapshot (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "TargetType TEXT NOT NULL, " +
            "TargetId INTEGER NOT NULL, " +
            "LastCommandSequence INTEGER NOT NULL DEFAULT 0, " +
            "CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')))");
        db.Database.ExecuteSqlRaw(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_workspace_snapshot_TargetType_TargetId ON workspace_snapshot (TargetType, TargetId)");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 注册应用程序运行所需的所有服务

        // 从 collection 提供的 IServiceCollection 中创建包含服务的 ServiceProvider
        ServiceProvider = _host.Services;
        Logger = _host.Services.GetRequiredService<ILogger<App>>();
        ConfigService = _host.Services.GetRequiredService<IConfigService>();
        Resources["Loc"] = Localizor = _host.Services.GetRequiredService<LocalizationService>();
        Notification = _host.Services.GetRequiredService<INotificationService>();
        Dispatcher.UIThread.Invoke(async () =>
        {
            await ConfigService.LoadAsync();
            ApplyStartupSettings();
        });

        // Register custom editors
        var editorRegistry = _host.Services.GetRequiredService<CustomEditorRegistry>();
        editorRegistry.Register(_host.Services.GetRequiredService<Views.UserControls.Editors.RecipeFlowchartEditor>());
        editorRegistry.Register(_host.Services.GetRequiredService<Views.UserControls.Editors.StoryTreeEditor>());
        editorRegistry.Register(_host.Services.GetRequiredService<Views.UserControls.Editors.TreasureTreePreviewEditor>());
        editorRegistry.Register(_host.Services.GetRequiredService<Views.UserControls.Editors.IngredientEditor>());
        editorRegistry.Register(_host.Services.GetRequiredService<Views.UserControls.Editors.ItemPropEditor>());
        editorRegistry.Register(_host.Services.GetRequiredService<Views.UserControls.Editors.ItemTypeEditor>());

        // Register generic overview editors for remaining entity types
        foreach (var (_, type) in Data.Constants.GameTypes)
        {
            if (!editorRegistry.RegisteredTypes.Contains(type))
                editorRegistry.Register(new Views.UserControls.Editors.EntityOverviewEditor(type));
        }

        // Register entity visualizers (detail + overview per type)
        var visualizerRegistry = _host.Services.GetRequiredService<EntityVisualizerRegistry>();
        var defaultVis = new Views.UserControls.Editors.DefaultEntityVisualizer(typeof(IEntity));
        visualizerRegistry.SetDefault(defaultVis);
        // Core entities with custom visualizations
        visualizerRegistry.Register(new Views.UserControls.Editors.ItemTypeEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.RecipeEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.TreasureTableEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.EncounterEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.CreatureEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.ConditionEntityVisualizer());
        // Combat
        visualizerRegistry.Register(new Views.UserControls.Editors.AttackModeEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.BattleMoveEntityVisualizer());
        // World
        visualizerRegistry.Register(new Views.UserControls.Editors.HexTypeEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.FactionEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.CampTypeEntityVisualizer());
        // Crafting
        visualizerRegistry.Register(new Views.UserControls.Editors.IngredientEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.ItemPropEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.ChargeProfileEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.ContainerTypeEntityVisualizer());
        // Story / World spawn
        visualizerRegistry.Register(new Views.UserControls.Editors.EncounterTriggerEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.CreatureSourceEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.DmcPlaceEntityVisualizer());
        // Remaining tables
        visualizerRegistry.Register(new Views.UserControls.Editors.BarterHexEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.DataFileEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.GameVarEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.HeadlineEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.ForbiddenHexEntityVisualizer());
        visualizerRegistry.Register(new Views.UserControls.Editors.MapEntityVisualizer());

        InitDatabase<EditorDbContext>(ServiceProvider);
        InitDatabase<GameDbContext>(ServiceProvider);
        RunEditorDbMigrations(ServiceProvider);

        // Build the global browser reference index eagerly on startup.
        // This index persists as a static singleton (GDH.BrowserStore) for the entire session.
        // It is only rebuilt when profile changes or mod is saved (via InvalidateIndex).
        Helper.AsyncHelper.FireAndForget(ViewModels.MainContent.EntityBrowserDocument.EnsureIndexBuiltAsync());

        // Initialize field descriptions from .docx
        InitializeFieldDescriptions();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // DataAnnotation validation plugin removal skipped — BindingPlugins API changed in Avalonia 11.3

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            (Notification as NotificationService)!.SetNotificationManager(mainWindow.NotificationManager);
            mainWindow.Closing += (sender, args) => { Dispatcher.UIThread.InvokeAsync(ConfigService.SaveAsync); };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeFieldDescriptions()
    {
        var fieldDescService = _host.Services.GetRequiredService<FieldDescriptionService>();
        GenericDataGridHelper.FieldDescriptions = fieldDescService;
        var config = ConfigService.Config;

        // Try to load cached JSON first
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "field_descriptions.json");
        if (File.Exists(jsonPath))
        {
            fieldDescService.LoadFromJson(jsonPath);
            return;
        }

        // Try to extract from .docx in game directory
        var gameRoot = config?.GameRootDir;
        if (!string.IsNullOrWhiteSpace(gameRoot) && Directory.Exists(gameRoot))
        {
            var docxPath = Path.Combine(gameRoot, "游戏XML文本各项说明修正增强版.docx");
            if (File.Exists(docxPath))
            {
                fieldDescService.ExtractFromDocx(docxPath, jsonPath);
            }
            else
            {
                // Also search for the docx in Chinese or English variants
                var docxFiles = Directory.GetFiles(gameRoot, "*.docx");
                foreach (var f in docxFiles)
                {
                    var name = Path.GetFileName(f);
                    if (name.Contains("XML") || name.Contains("说明") || name.Contains("field"))
                    {
                        fieldDescService.ExtractFromDocx(f, jsonPath);
                        break;
                    }
                }
            }

            // Also extract mod guide to Help directory if present
            var modGuideDocx = Path.Combine(gameRoot, "NeoScavenger 模组制作指南中文翻译精修1.2（新）.docx");
            if (File.Exists(modGuideDocx))
            {
                ExtractModGuide(modGuideDocx);
            }
        }
    }

    private void ExtractModGuide(string docxPath)
    {
        try
        {
            var helpDir = Path.Combine(AppContext.BaseDirectory, "Help", "zh");
            var outputPath = Path.Combine(helpDir, "ModGuide.md");
            if (File.Exists(outputPath)) return; // already extracted

            var text = DocxTextExtractor.ExtractText(docxPath);
            if (string.IsNullOrWhiteSpace(text)) return;

            Directory.CreateDirectory(helpDir);

            // Clean up and save as markdown
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Neo Scavenger 模组制作指南");
            sb.AppendLine();
            sb.AppendLine("> 来源: 游戏根目录文档，自动提取");
            sb.AppendLine();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Preserve markdown-like markers if present
                if (trimmed.StartsWith("###") || trimmed.StartsWith("##") || trimmed.StartsWith("#"))
                    sb.AppendLine(trimmed);
                else if (trimmed.StartsWith("**"))
                    sb.AppendLine(trimmed);
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                    sb.AppendLine(trimmed);
                else if (trimmed.StartsWith("http"))
                    sb.AppendLine($"- [{trimmed}]({trimmed})");
                else
                    sb.AppendLine(trimmed);

                sb.AppendLine();
            }

            File.WriteAllText(outputPath, sb.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to extract mod guide from {Path}", docxPath);
        }
    }

    private void ApplyStartupSettings()
    {
        var config = ConfigService.Config;

        // Apply language
        try
        {
            var culture = new CultureInfo(config.Language);
            Thread.CurrentThread.CurrentUICulture = culture;
            Localizor.SetCulture(culture);
        }
        catch { /* keep default */ }

        // Apply theme
        RequestedThemeVariant = config.Theme switch
        {
            "Light" => Avalonia.Styling.ThemeVariant.Light,
            "Dark" => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };

        // Apply font size
        if (config.FontSize is > 0 and <= 24)
            Resources["AppFontSize"] = (double)config.FontSize;
    }

    public static void ApplyFontSize(int fontSize)
    {
        if (fontSize is > 0 and <= 24 && Current is App app)
            app.Resources["AppFontSize"] = (double)fontSize;
    }

}