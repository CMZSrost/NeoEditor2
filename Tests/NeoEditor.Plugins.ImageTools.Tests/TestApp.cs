using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;

namespace NeoEditor.Plugins.ImageTools.Tests;

/// <summary>
/// Headless Avalonia platform (Skia backend) so tests that decode <see cref="Avalonia.Media.Imaging.Bitmap"/>
/// instances — e.g. <c>ImageEditorDocument.LoadGeneratedImage</c> — run without a real windowing
/// platform. Avalonia.Headless.XUnit is NOT used (it pulls xunit v3 and conflicts with the
/// project's xunit 2.9); the platform is initialized manually once per test run.
/// </summary>
public class TestApp : Application
{
    private static readonly object InitGate = new();
    private static bool _initialized;

    /// <summary>Initialize the headless platform once (idempotent, thread-safe).</summary>
    public static void EnsureAvaloniaInitialized()
    {
        lock (InitGate)
        {
            if (_initialized)
            {
                return;
            }

            AppBuilder.Configure<TestApp>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
            _initialized = true;
        }
    }
}
