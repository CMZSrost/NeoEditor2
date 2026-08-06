using System;
using System.IO;
using System.Linq;
using Avalonia;
using Serilog;

namespace NeoEditor.Player;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // WebView2 user data (cache / EBWebView) must NOT pollute the app folder — the
        // distribution stays clean (v2.27). Must run before any WebView2 environment exists.
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NeoScavengerPlayer", "WebView2"));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        // Dragging a SWF onto the exe passes its path as a command-line argument.
        App.StartupSwfPath = args
            .FirstOrDefault(a => a.EndsWith(".swf", StringComparison.OrdinalIgnoreCase) && File.Exists(a));

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        Log.CloseAndFlush();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseWin32()      // v2.27: Windows-only build target (Avalonia.Win32 backend)
            .UseSkia()       // v2.27: rendering is a separate package since Avalonia 12
            .UseHarfBuzz()   // v2.27: text shaping (Inter font) is separate too
            .WithInterFont()
            .LogToTrace();
}
