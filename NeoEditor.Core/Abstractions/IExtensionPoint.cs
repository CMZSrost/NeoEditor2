using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Extension point contract (R25 — interface design only, no invocation in this phase).
/// Feature Plugins implement this to hook into the editor lifecycle via
/// <see cref="IHostService.RegisterPreSaveHook"/> and related methods.
/// </summary>
/// <typeparam name="TContext">Context data passed to the hook at invocation time.</typeparam>
/// <remarks>
/// See <c>spec/R25-cross-plugin-extension-points.md</c> for the full specification.
/// Hook invocation is deferred to Phase 7+.
/// </remarks>
public interface IExtensionPoint<TContext>
{
    /// <summary>Human-readable name for diagnostics and logging.</summary>
    string Name { get; }

    /// <summary>
    /// Execution order within the same hook pipeline.
    /// Lower values execute earlier. Default: 0.
    /// </summary>
    int Order { get; }

    /// <summary>Execute the hook with the given context.</summary>
    Task ExecuteAsync(TContext context);
}
