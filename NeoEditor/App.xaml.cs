using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using HandyControl.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NeoEditor.Data.Context;
using NeoEditor.Data.Options;
using NeoEditor.Helpers;
using NeoEditor.Services;
using NeoEditor.ViewModels;
using NeoEditor.ViewModels.Controls;
using NeoEditor.ViewModels.Controls.Tabs;
using NeoEditor.Views;
using NeoEditor.Views.Controls;
using NeoEditor.Views.Controls.Tabs;
using Serilog;
using MessageBox = System.Windows.MessageBox;

namespace NeoEditor;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(builder =>
        {
            var basePath = Path.GetDirectoryName(AppContext.BaseDirectory);
            ArgumentException.ThrowIfNullOrEmpty(basePath);
            builder.SetBasePath(basePath)
                .AddInMemoryCollection().AddEnvironmentVariables()
                .AddJsonFile("appsettings.json", false, true)
                .AddJsonFile("appsettings.development.json", true, true);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Build())
                .Enrich.FromLogContext()
                .CreateLogger();
        })
        .UseSerilog()
        .ConfigureServices((
            collection, services) =>
        {
            services.AddHostedService<ApplicationHostService>();
            services.Configure<ProjectOption>(collection.Configuration.GetSection(nameof(ProjectOption)));
            services.AddDbContextFactory<NeoContext>(options =>
            {
                options.UseSqlite(collection.Configuration.GetConnectionString("DefaultConnection"));
            });
            Encoding.GetEncoding("utf-8");

            services.AddSingleton<XmlLoader>();

            services.AddSingleton<LoggerService>();

            services.AddSingleton<MainWindow>()
                .AddSingleton<MenuViewModel>()
                .AddSingleton<ProjectViewModel>()
                .AddSingleton<MainWindowViewModel>();

            services.AddTransient<TabItem, AttackMode>().AddTransient<AttackModeViewModel>();

            services.AddSingleton<EditTablePage>().AddSingleton<EditTableViewModel>();
        })
        .Build();

    public static IServiceProvider Services => Host.Services;

    private async void Onstartup(object sender, StartupEventArgs e)
    {
        await Host.StartAsync();
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        await Host.StopAsync();
        Host.Dispose();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.ToString());
    }
}