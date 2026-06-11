using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace NeoEditor.Helper.Extensions;

public static class LoggingExtensions
{
    public static IServiceCollection AddSerilogLogging(this IServiceCollection services,
        IConfiguration configuration, string logFilePath = "logs/modeditor-.log")
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(logFilePath,
                rollingInterval: RollingInterval.Hour,
                retainedFileCountLimit: 72,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        // Read LogLevel overrides from appsettings.json
        var logLevelSection = configuration.GetSection("Logging:LogLevel");
        if (logLevelSection.Exists())
        {
            foreach (var entry in logLevelSection.GetChildren())
            {
                var ns = entry.Key;
                var level = entry.Value?.ToLowerInvariant();
                loggerConfig = level switch
                {
                    "verbose" or "trace" => loggerConfig.MinimumLevel.Override(ns, Serilog.Events.LogEventLevel.Verbose),
                    "debug" => loggerConfig.MinimumLevel.Override(ns, Serilog.Events.LogEventLevel.Debug),
                    "information" or "info" => loggerConfig.MinimumLevel.Override(ns, Serilog.Events.LogEventLevel.Information),
                    "warning" or "warn" => loggerConfig.MinimumLevel.Override(ns, Serilog.Events.LogEventLevel.Warning),
                    "error" => loggerConfig.MinimumLevel.Override(ns, Serilog.Events.LogEventLevel.Error),
                    "fatal" or "critical" => loggerConfig.MinimumLevel.Override(ns, Serilog.Events.LogEventLevel.Fatal),
                    _ => loggerConfig
                };
            }
        }

        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
            builder.AddFilter("Microsoft.Extensions.Localization", LogLevel.Warning);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        });

        return services;
    }
}
