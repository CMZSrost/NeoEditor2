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
using Microsoft.EntityFrameworkCore;
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
using NeoEditor.ViewModels.ExplorerPane;
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
                services.AddSerilogLogging();
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

                // DockServices
                services.AddSingleton<IDockState, DockState>();
                services.AddSingleton<Factory>();
                services.AddSingleton<IFactory>(static sp => sp.GetRequiredService<Factory>());

                services.AddSingleton<DockSerializer>();
                services.AddSingleton<IDockSerializer>(static sp => sp.GetRequiredService<DockSerializer>());

                // services
                services.AddScoped<IMessenger, WeakReferenceMessenger>();
                services.AddSingleton<LocalizationService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<PhpParser>();
                services.AddAutoMapper((expression => { }));

                // window
                services.AddTransient<MainWindow>()
                    .AddScoped<MainWindowViewModel>()
                    // Panes
                    .AddScoped<ResourceManagerViewModel>()
                    .AddScoped<SearchPaneViewModel>()
                    .AddScoped<ModDatabaseViewModel>()
                    .AddScoped<SettingsPaneViewModel>()
                    // MainContents
                    .AddScoped<ModIndexViewModel>();
                services.AddTransient<SearchableDataGrid>();
                services.AddTransient<ModEntryDropHandler>();
                // DocumentView
                services.AddTransient<EditProfileViewModel>()
                    .AddSingleton<Func<ProfileInfo, EditProfileViewModel>>((info =>
                    {
                        var vm = ServiceProvider.GetRequiredService<EditProfileViewModel>();
                        vm.ProfileInfo = info;
                        vm.LoadEntries();
                        return vm;
                    }));
                // Dialog
                services.AddTransient<CreateModDialog>();
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
        Logger = _host.Services.GetRequiredService<ILogger<App>>();
        ConfigService = _host.Services.GetRequiredService<IConfigService>();
        Resources["Loc"] = Localizor = _host.Services.GetRequiredService<LocalizationService>();
        Notification = _host.Services.GetRequiredService<INotificationService>();
        Dispatcher.UIThread.InvokeAsync(ConfigService.LoadAsync);

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
            desktop.MainWindow = mainWindow;
            (Notification as NotificationService)!.SetNotificationManager(mainWindow.NotificationManager);
            mainWindow.Closing += (sender, args) => { Dispatcher.UIThread.InvokeAsync(ConfigService.SaveAsync); };
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