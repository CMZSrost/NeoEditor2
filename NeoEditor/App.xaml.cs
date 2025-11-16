﻿using System.Diagnostics;
using System.Text;
using System.Windows;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Options;
using NeoEditor.Helpers;
using NeoEditor.Services.QueueProcess;
using NeoEditor.Services.Worker;
using NeoEditor.ViewModels;
using NeoEditor.ViewModels.Controls;
using NeoEditor.Views;
using NeoEditor.Views.Controls;
using Serilog;

namespace NeoEditor;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IRegionManager, RegionManager>();
        containerRegistry.RegisterSingleton<IEventAggregator, EventAggregator>();
        containerRegistry.RegisterSingleton<IRegionNavigationService, RegionNavigationService>();

        containerRegistry.RegisterSingleton<TableConfig>();

        // containerRegistry.RegisterSingleton<MainWindowViewModel>();
        // containerRegistry.RegisterSingleton<EditTableViewModel>();
        containerRegistry.RegisterSingleton<ProjectViewModel>();
        containerRegistry.RegisterSingleton<FileSystemViewModel>();
        containerRegistry.RegisterSingleton<ReoGridControlViewModel>();
        containerRegistry.RegisterSingleton<LoggerViewModel>();
        // 改为 Transient，让每个 EditXmlPage 都有独立的 ViewModel 实例
        containerRegistry.Register<EditXmlViewModel>();

        containerRegistry.RegisterForNavigation<MainWindowView, MainWindowViewModel>();
        containerRegistry.RegisterForNavigation<EditTableReoPage, EditTableViewModel>();

        containerRegistry.Register<SerialQueueProcess>();
        containerRegistry.Register<LoadXmlQueueProcess>();
        containerRegistry.Register<ProjectLoadingWorker>();

        containerRegistry.RegisterInstance(Log.Logger);
    }


    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
            builder.AddSerilog(dispose: true)
        );

        // services.AddScoped<IAsyncInterceptor, ExceptionInterceptor>();
        // services.AddDefaultProxyGenerator()
        //     .AddTransientWithAsyncInterceptor<EquipmentControlViewModel, ExceptionInterceptorAsync>();
        // services.AddTransientWithAsyncInterceptor<AbsoluteLocationViewModel, ExceptionInterceptorAsync>();

        var configurationBuilder = new ConfigurationManager()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddInMemoryCollection().AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile("appsettings.development.json", true, true);
        var configuration = configurationBuilder.Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<ProjectOption>(configuration.GetSection(nameof(ProjectOption)));
        Encoding.GetEncoding("utf-8");

        services.AddAutoMapper(expression => { });

        // ViewModel
        // services.AddScoped<LogPageViewModel>();


        // Repositories
        // services.AddTransient<IIORepository, IORepository>();
        // services.AddTransient<IAlarmReository, AlarmRepository>();
        // services.AddTransient<IWarningRepository, WarningRepository>();
        // services.AddTransient<IEquiControlGroupRepository, EquiControlGroupRepository>();
        // services.AddTransient<IJogControlGroupRepository, JogControlRepository>();
        // services.AddTransient<IRealValGroupRepository, RealValGroupRepository>();
        // services.AddTransient<IDIntValGroupRepository, DIntValGroupRepository>();
    }

    #region 配置

    protected override void ConfigureViewModelLocator()
    {
        base.ConfigureViewModelLocator();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // moduleCatalog.AddModule<PubSubEventModule>();
    }

    protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
    {
        base.ConfigureRegionAdapterMappings(regionAdapterMappings);
    }

    protected override void ConfigureDefaultRegionBehaviors(IRegionBehaviorFactory regionBehaviors)
    {
        base.ConfigureDefaultRegionBehaviors(regionBehaviors);
    }

    #endregion

    #region 杂项

    public static Rules DefaultRules =>
        Rules.Default
            .WithConcreteTypeDynamicRegistrations(reuse: Reuse.Transient)
            .With(Made.Of(FactoryMethod.ConstructorWithResolvableArguments))
            .WithFuncAndLazyWithoutRegistration()
            .WithTrackingDisposableTransients()
            //.WithoutFastExpressionCompiler()
            .WithFactorySelector(Rules.SelectLastRegisteredFactory());

    protected override Rules CreateContainerRules()
    {
        return DefaultRules;
    }

    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindowView>();
    }

    protected override IContainerExtension CreateContainerExtension()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        var container = new Container(CreateContainerRules());
        container.WithDependencyInjectionAdapter(services);

        return new DryIocContainerExtension(container);
    }

    #endregion

    #region 入口与回调处理

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            // 单实例检查
            var ap = Process.GetCurrentProcess();
            if (Process.GetProcessesByName(ap.ProcessName).Length > 1)
            {
                MessageBox.Show("程序已运行.", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 全局异常处理
            ConfigureGlobalExceptionHandlers();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "应用程序错误");
            MessageBox.Show(ex.Message, "应用程序错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConfigureGlobalExceptionHandlers()
    {
        // 捕获UI线程未处理的异常
        DispatcherUnhandledException += (sender, ex) =>
        {
            Log.Logger.Error($"捕获UI线程未处理的异常 {ex.Exception.Message}: {ex.Exception.StackTrace}");
            ex.Handled = true;
        };

        // 捕获非UI线程未处理的异常
        AppDomain.CurrentDomain.UnhandledException += (sender, ex) =>
        {
            if (ex.ExceptionObject is Exception exception)
                Log.Logger.Error($"捕获非UI线程未处理的异常 {exception.Message}: {exception.StackTrace}");
        };

        // 捕获Task线程中未处理的异常
        TaskScheduler.UnobservedTaskException += (sender, ex) =>
        {
            Log.Logger.Error($"捕获Task线程中未处理的异常 {ex.Exception.Message}: {ex.Exception.StackTrace}");
        };
    }

    #endregion
}