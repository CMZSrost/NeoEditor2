using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// Headless Avalonia platform (Skia backend) so VMs that touch the UI thread dispatcher
/// (e.g. <see cref="ViewModels.KeyValueEditorViewModel.ApplyChanges"/>) run in tests.
/// Avalonia.Headless.XUnit is NOT used (it pulls xunit v3 and conflicts with xunit 2.9);
/// the platform is initialized manually once per test run (same pattern as ImageTools.Tests).
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
