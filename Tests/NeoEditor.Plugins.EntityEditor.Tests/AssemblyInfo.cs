using Xunit;

// R31: these tests build Avalonia controls against a single shared headless
// Application instance (TestApp) and some write shared state
// (Application.Current.Resources["Services"]) that other UI tests resolve via
// GetServices. Under xUnit class-level parallelism that shared state races and
// produces intermittent failures (observed only in full-suite parallel runs).
// Running the assembly serially is cheap (~400ms) and makes the shared
// headless platform deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
