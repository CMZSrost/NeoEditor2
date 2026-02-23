using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Options;
using NeoEditor.Helper.Extensions;
using NeoEditor.ViewModels;
using NeoEditor.Views;
using NeoEditor.Services;

namespace NeoEditor;

public partial class App : Application
{
    public IHost _host;

    public static IServiceProvider? ServiceProvider { get; set; }

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

                // services
                services.AddSingleton<LocalizationService>();
                services.AddSingleton<INotificationService, NotificationService>();

                // window
                services.AddTransient<MainWindow>()
                    .AddSingleton<MainWindowViewModel>()
                    .AddScoped<ExplorerPaneViewModel>()
                    .AddSingleton<SearchPaneViewModel>()
                    .AddSingleton<SettingsPaneViewModel>();
                services.AddTransient<SearchableDataGridViewModel<GameVar>, GameVarDataGridViewModel>();
            })
            .Build();
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var notificationService = _host.Services.GetRequiredService<INotificationService>() as NotificationService;
            notificationService!.SetNotificationManager(mainWindow.NotificationManager);
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