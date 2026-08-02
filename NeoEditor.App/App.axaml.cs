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
using IXmlParser = NeoEditor.Core.Abstractions.IXmlParser;
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
using NeoEditor.Plugins.DataViewer;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Plugins.ImageTools;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;
using NeoEditor.Plugins.Mcp;
using NeoEditor.Plugins.Cli;
using NeoEditor.Plugins.AiChat;
using NeoEditor.UI.Common.Services;
using NeoEditor.UI.Common.Visualizers;
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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _host = CreateHost();
    }

    /// <summary>
    /// Build the composition root. Shared by the GUI (App.Initialize) and the headless
    /// --mcp server (Program) so both resolve the same services (R20).
    /// When <paramref name="mcpMode"/> is true, stdout is reserved for the MCP protocol
    /// and console logging is disabled.
    /// </summary>
    public static IHost CreateHost(bool mcpMode = false) => Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(builder =>
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .Build()
        )
        .ConfigureServices((context, services) =>
            {
                // settings
                services.AddSerilogLogging(context.Configuration, logToConsole: !mcpMode);
                services.AddLocalization();
                // options
                services.AddSingleton<IConfigService, ConfigService>();
                services.Configure<CultureSettings>(context.Configuration.GetSection(nameof(CultureSettings)));

                // database — use absolute paths anchored to the exe directory so the
                // same DB file is accessed regardless of process working directory.
                var baseDir = AppContext.BaseDirectory;
                services.AddSingleton<ProjectDbContextFactory>();
                services.AddDbContextFactory<EditorDbContext>(options =>
                {
                    options
                        .UseSqlite($"Data Source={System.IO.Path.Combine(baseDir, Constants.EditorDatabasePath)}")
                        .EnableDetailedErrors();
                    // In headless MCP mode stdout is the protocol channel — never write EF logs there.
                    if (!mcpMode) options.LogTo(Console.WriteLine, LogLevel.Warning);
                });
                services.AddDbContextFactory<GameDbContext>(options =>
                {
                    options
                        .UseSqlite($"Data Source={System.IO.Path.Combine(baseDir, Constants.GameDatabasePath)}")
                        .EnableDetailedErrors();
                    if (!mcpMode) options.LogTo(Console.WriteLine, LogLevel.Warning);
                });

                // DockServices
                services.AddSingleton<IDockState, DockState>();
                services.AddSingleton<Factory>();
                services.AddSingleton<IFactory>(static sp => sp.GetRequiredService<Factory>());

                services.AddSingleton<DockSerializer>();
                services.AddSingleton<IDockSerializer>(static sp => sp.GetRequiredService<DockSerializer>());

                // services
                services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
                services.AddSingleton<ILocalizationService, LocalizationService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<PhpParser>();
                services.AddSingleton<IXmlParser, XmlParser>();
                // B5: ModManager lives in Infra and is the HostService's internal collaborator.
                // IModManager resolves to the HostService so all mod writes flow through IHostService (R24).
                services.AddSingleton<ModManager>();
                services.AddSingleton<IProfileManager, ProfileManager>();
                services.AddSingleton<CsvImportExportService>();
                services.AddSingleton<DataExportService>();
                services.AddSingleton<IImageService, ImageService>();
                services.AddSingleton<IMergeService, MergeService>();
                services.AddSingleton<FilterService>();
                services.AddSingleton<IFilterService>(sp => sp.GetRequiredService<FilterService>());
                services.AddSingleton<SearchService>();
                services.AddSingleton<ISearchService>(sp => sp.GetRequiredService<SearchService>());
                services.AddSingleton<FieldDescriptionService>();
                services.AddSingleton<IWorkspacePersistenceService, WorkspacePersistenceService>();
                services.AddSingleton<IBrowserIndexService, BrowserIndexService>();
                services.AddSingleton<IWorkspaceSession, WorkspaceSession>();
                services.AddSingleton<NeoEditor.Core.Abstractions.IHostService, HostService>();
                // B5: all IModManager consumers (App VMs / views) route through the HostService instance.
                services.AddSingleton<IModManager>(sp =>
                    (IModManager)sp.GetRequiredService<NeoEditor.Core.Abstractions.IHostService>());
                services.AddSingleton<NeoEditor.Core.Abstractions.IReferenceListSerializer,
                    NeoEditor.Helper.ReferenceListSerializer>();
                services.AddSingleton<ReferenceResolver>(sp =>
                    new Helper.ReferenceResolver(
                        sp.GetRequiredService<IWorkspaceSession>(),
                        sp.GetRequiredService<IMessenger>()));
                services.AddSingleton<Helper.IReferenceResolver>(sp =>
                    sp.GetRequiredService<ReferenceResolver>());
                services.AddSingleton<ISelectionService, SelectionService>();
                services.AddSingleton<EntityVisualizerRegistry>();
                services.AddDataViewerPlugin();
                services.AddSingleton<IEntityLookupService>(
                    sp => sp.GetRequiredService<NeoEditor.Plugins.DataViewer.Services.DataTableService>());
                services.AddEntityEditorPlugin();
                services.AddImageToolsPlugin();
                services.AddMcpPlugin();
                services.AddCliPlugin();
                services.AddAiChatPlugin();
                // D02: App-level tool plugins (Profile Tool). Registered with the other
                // IToolPlugin instances so the dynamic dock build picks them up.
                services.AddSingleton<ViewModels.MainContent.ProfileToolViewModel>();
                services.AddSingleton<NeoEditor.Core.Abstractions.IToolPlugin, ProfileToolPlugin>();
                services.AddSingleton<IModImageListService, ModImageListService>();
                services.AddSingleton<NeoEditor.Plugins.EntityEditor.Services.VisHelperService>(sp =>
                    new NeoEditor.Plugins.EntityEditor.Services.VisHelperService(
                        sp.GetRequiredService<Services.IImageService>().FindImage,
                        sp.GetRequiredService<Helper.IReferenceResolver>(),
                        sp.GetRequiredService<Helper.INavigationRouter>(),
                        sp.GetRequiredService<IEntityLookupService>(),
                        sp.GetRequiredService<ILocalizationService>()));
                services.AddSingleton<NeoEditor.Plugins.EntityEditor.Services.RefNode>(
                    sp => new NeoEditor.Plugins.EntityEditor.Services.RefNode(
                        sp.GetRequiredService<Helper.IReferenceResolver>(),
                        sp.GetRequiredService<Helper.INavigationRouter>(),
                        sp.GetRequiredService<NeoEditor.Plugins.EntityEditor.Services.VisHelperService>()
                            .BuildRefTooltip));

                services.AddAutoMapper((expression => { }));

                // window
                services.AddTransient<MainWindow>()
                    .AddScoped<MainWindowViewModel>()
                    .AddScoped<MainWindowSideBarViewModel>()
                    .AddScoped<DocumentWorkspaceViewModel>()
                    .AddScoped<HomePageViewModel>()
                    .AddScoped<SettingsPageViewModel>()
                    // Panes
                    .AddScoped<ResourceManagerViewModel>()
                    .AddScoped<SearchPaneViewModel>()
                    .AddScoped<SettingsPaneViewModel>()
                    .AddTransient<WorkspaceHistoryViewModel>()
                    // MainContents
                    .AddScoped<ModIndexViewModel>();
                services.AddTransient<NeoEditor.Plugins.DataViewer.Views.SearchableDataGrid>();
                services.AddTransient<ModEntryDropHandler>();
                services.AddTransient<ModImagePairDropHandler>();
                // Dialog
                services.AddTransient<CreateModDialog>()
                    .AddTransient<CreateModDialogViewModel>()
                    .AddTransient<RenameImagePairDialog>()
                    .AddTransient<RenameImagePairDialogViewModel>();
            })
            .Build();

    /// <summary>
    /// Create both databases and apply lightweight migrations. Shared by the GUI startup
    /// and the headless --mcp server (Program) so MCP tools can access the same DB files.
    /// </summary>
    public static void EnsureDatabases(IServiceProvider services)
    {
        InitDatabase<EditorDbContext>(services);
        InitDatabase<GameDbContext>(services);
        RunEditorDbMigrations(services);
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

        // R26/B4: add IncludeGame + SingleModId columns to existing profile_info tables.
        // SQLite ALTER TABLE has no IF NOT EXISTS, so check PRAGMA table_info first.
        AddProfileInfoColumnIfMissing(db, "IncludeGame",
            "ALTER TABLE profile_info ADD COLUMN IncludeGame INTEGER NOT NULL DEFAULT 1");
        AddProfileInfoColumnIfMissing(db, "SingleModId",
            "ALTER TABLE profile_info ADD COLUMN SingleModId INTEGER NULL");
    }

    /// <summary>Add a column to the profile_info table if it does not already exist (SQLite).</summary>
    private static void AddProfileInfoColumnIfMissing(DbContext db, string column, string alterSql)
    {
        var columns = db.Database.SqlQueryRaw<string>(
                "SELECT name FROM pragma_table_info('profile_info')")
            .ToList();
        if (columns.Contains(column, StringComparer.OrdinalIgnoreCase)) return;
        db.Database.ExecuteSqlRaw(alterSql);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 注册应用程序运行所需的所有服务

        // Register IServiceProvider for View code-behind service resolution via ViewServices
        Resources["Services"] = _host.Services;
        Resources["Loc"] = _host.Services.GetRequiredService<ILocalizationService>();

        // Wire the reference serializer into EF Core model building
        // so ReferenceListStringConverter auto-discovery works in GameDbContext.OnModelCreating.
        Data.Context.GameDbContext.ReferenceSerializer =
            _host.Services.GetRequiredService<NeoEditor.Core.Abstractions.IReferenceListSerializer>();

        // Load config synchronously — must complete before any ViewModels are created.
        // Task.Run avoids the classic await deadlock: LoadAsync captures the UI
        // SynchronizationContext, but we block the UI thread with GetResult().
        try
        {
            Task.Run(async () => await _host.Services.GetRequiredService<IConfigService>().LoadAsync()).GetAwaiter().GetResult();
            ApplyStartupSettings();
        }
        catch (Exception ex)
        {
            _host.Services.GetRequiredService<ILogger<App>>().LogError(ex, "[Startup] Config load failed, using defaults");
        }

        // Set default visualizer and register all 25 from EntityEditor Plugin
        var visualizerRegistry = _host.Services.GetRequiredService<EntityVisualizerRegistry>();
        _host.Services.RegisterEntityEditorVisualizers();

        EnsureDatabases(_host.Services);

        var editorDbPath = System.IO.Path.Combine(AppContext.BaseDirectory, Constants.EditorDatabasePath);
        var startupLogger = _host.Services.GetRequiredService<ILogger<App>>();
        startupLogger.LogInformation("[DB] Editor db path: {Path}, exists: {Exists}, baseDir: {BaseDir}",
            editorDbPath, System.IO.File.Exists(editorDbPath), AppContext.BaseDirectory);

        // Ensure game base data is imported BEFORE building the browser index.
        // This is critical for first-time users: the game's XML files in data/ must be
        // imported into game.db before any browsing or editing can work.
        Helper.AsyncHelper.FireAndForget(ImportGameDataOnStartupAsync());

        // Build the global browser reference index eagerly on startup.
        Helper.AsyncHelper.FireAndForget(_host.Services.GetRequiredService<IBrowserIndexService>().EnsureBuiltAsync());

        // Initialize field descriptions from .docx
        InitializeFieldDescriptions();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // DataAnnotation validation plugin removal skipped — BindingPlugins API changed in Avalonia 11.3

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            var notificationService = _host.Services.GetRequiredService<INotificationService>();
            (notificationService as NotificationService)!.SetNotificationManager(mainWindow.NotificationManager);
            var configService = _host.Services.GetRequiredService<IConfigService>();
            mainWindow.Closing += (sender, args) => { Dispatcher.UIThread.InvokeAsync(configService.SaveAsync); };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeFieldDescriptions()
    {
        var fieldDescService = _host.Services.GetRequiredService<FieldDescriptionService>();
        // M9: FieldDescriptions moved from GenericDataGridHelper to ColumnTemplateFactory delegate.
        // R30: embedded authoritative descriptions (Docs/38, keyed by TableAttribute name + property)
        // take priority; the .docx cache only fills gaps (its extracted keys carry Chinese table
        // suffixes and don't match TableAttribute names).
        _host.Services.GetRequiredService<NeoEditor.Plugins.DataViewer.Services.ColumnTemplateFactory>()
            .FieldDescriptionProvider = (table, prop) =>
                NeoEditor.Data.Model.FieldDescriptions.GetDescription(table, prop)
                ?? fieldDescService.GetDescription(table, prop);
        var config = _host.Services.GetRequiredService<IConfigService>().Config;

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
            _host.Services.GetRequiredService<ILogger<App>>().LogWarning(ex, "Failed to extract mod guide from {Path}", docxPath);
        }
    }

    /// <summary>Import game base data (data/*.xml) if not already in the database.
    /// Critical for first-time users who haven't opened a profile yet.</summary>
    private async Task ImportGameDataOnStartupAsync()
    {
        var sp = _host.Services;
        var logger = sp.GetRequiredService<ILogger<App>>();
        var configService = sp.GetRequiredService<IConfigService>();
        var notification = sp.GetRequiredService<INotificationService>();
        try
        {
            var gameRoot = configService.Config?.GameRootDir;
            if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            {
                logger.LogInformation("[Startup] GameRootDir not configured, skipping game data import");
                return;
            }

            await using var edb = await sp
                .GetRequiredService<IDbContextFactory<Data.Context.EditorDbContext>>()
                .CreateDbContextAsync();

            var gameMod = await edb.ModInfos.FirstOrDefaultAsync(m => m.ModId == -1);
            if (gameMod is not null)
            {
                // Check if game data actually exists in game.db (not just the ModInfo record)
                await using var gdb = await sp
                    .GetRequiredService<IDbContextFactory<Data.Context.GameDbContext>>()
                    .CreateDbContextAsync();
                var itemCount = await gdb.ItemTypes.CountAsync();
                if (itemCount > 0)
                {
                    logger.LogInformation(
                        "[Startup] Game data already imported ({Count} ItemTypes), skipping", itemCount);
                    return;
                }
                // ModInfo exists but game.db is empty (e.g. DB was wiped).
                // Re-import using the existing ModInfo to avoid UNIQUE constraint.
                logger.LogInformation(
                    "[Startup] Game ModInfo exists but game.db is empty, re-importing");
                Dispatcher.UIThread.Post(() =>
                    notification.ShowInfo("正在重新导入游戏基础数据…", "启动"));

                var modManager = sp.GetRequiredService<IModManager>();
                await modManager.LoadModAsync(gameMod);
                gameMod.LastImport = DateTime.Now;
                await edb.SaveChangesAsync();
                logger.LogInformation("[Startup] Game data re-imported successfully");
                return;
            }

            Dispatcher.UIThread.Post(() =>
                notification.ShowInfo("正在导入游戏基础数据…", "首次启动"));

            var modManager2 = sp.GetRequiredService<IModManager>();
            var dataPath = Path.Combine(gameRoot, "data");
            if (!Directory.Exists(dataPath))
            {
                logger.LogWarning("[Startup] Game data/ directory not found: {Path}", dataPath);
                return;
            }

            var imported = await modManager2.ImportModAsync(dataPath, modId: -1);
            if (imported is not null)
            {
                // ImportModAsync sets IsBase=true when modId=-1,
                // but override Name to "Game" for clarity.
                imported.Name = "Game";
                await using var saveDb = await sp
                    .GetRequiredService<IDbContextFactory<Data.Context.EditorDbContext>>()
                    .CreateDbContextAsync();
                saveDb.ModInfos.Update(imported);
                await saveDb.SaveChangesAsync();

                logger.LogInformation(
                    "[Startup] Game data imported successfully: {Count} files", imported.XmlFilePaths.Count);
                Dispatcher.UIThread.Post(() =>
                    notification.ShowInfo("游戏数据导入完成！", "启动完成"));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Startup] Game data import failed (will retry on Browse)");
        }
    }

    private void ApplyStartupSettings()
    {
        var configService = _host.Services.GetRequiredService<IConfigService>();
        var localizor = _host.Services.GetRequiredService<ILocalizationService>();
        var config = configService.Config;

        // Apply language
        try
        {
            var culture = new CultureInfo(config.Language);
            Thread.CurrentThread.CurrentUICulture = culture;
            localizor.SetCulture(culture);
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

}