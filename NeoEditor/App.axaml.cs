using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Options;
using NeoEditor.Helper.Extensions;
using NeoEditor.ViewModels;
using NeoEditor.Views;
using NeoEditor.Services;
using NeoEditor.ViewModels.ExplorerPane;

namespace NeoEditor;

public partial class App : Application
{
    public IHost _host;

    public static IServiceProvider? ServiceProvider { get; set; }
    public static LocalizationService? Localizor { get; set; }
    public static INotificationService? Notification { get; set; }

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
                services.AddSerilogLogging();
                services.AddLocalization();
                // options
                services.Configure<CultureSettings>(context.Configuration.GetSection(nameof(CultureSettings)));

                // database
                services.AddSingleton<ProjectDbContextFactory>();
                services.AddDbContextFactory<EditorDbContext>(options =>
                    options
                        .UseSqlite($"Data Source={Constants.EditorDatabasePath}")
                        .LogTo(Console.WriteLine, LogLevel.Warning)
                        .EnableDetailedErrors());

                // services
                services.AddSingleton<LocalizationService>();
                services.AddSingleton<INotificationService, NotificationService>();

                // window
                services.AddTransient<MainWindow>()
                    .AddScoped<MainWindowViewModel>()
                    .AddScoped<ResourceManagerViewModel>()
                    .AddScoped<SearchPaneViewModel>()
                    .AddScoped<ModDatabaseViewModel>()
                    .AddScoped<SettingsPaneViewModel>();
                services.AddTransient<SearchableDataGridViewModel<GameVar>, GameVarDataGridViewModel>();
            })
            .Build();
    }

    private static void InitDatabase(IServiceProvider services)
    {
        // Create a scope to initialize the database
        using var scope = services.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EditorDbContext>>();
        using var dbContext = dbContextFactory.CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh"); // 设置为简体中文
        // 如果使用 CommunityToolkit，则需要用下面一行移除 Avalonia 数据验证。
        // 如果没有这一行，数据验证将会在 Avalonia 和 CommunityToolkit 中重复。
        BindingPlugins.DataValidators.RemoveAt(0);

        // 注册应用程序运行所需的所有服务

        // 从 collection 提供的 IServiceCollection 中创建包含服务的 ServiceProvider
        ServiceProvider = _host.Services;

        if (!File.Exists(Constants.EditorDatabasePath))
        {
            InitDatabase(ServiceProvider);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>() as NotificationService;
            notificationService!.SetNotificationManager(mainWindow.NotificationManager);
            Notification = notificationService;
            Localizor = _host.Services.GetRequiredService<LocalizationService>();

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}