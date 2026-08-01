using System.Threading;
using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Provides context actions for entities in the EntityEditor.
/// Implementations register contextual actions like "Generate Image" that appear
/// in entity context menus. R17-compliant cross-plugin extension point.
/// </summary>
public interface IEntityContextActionProvider
{
    /// <summary>Human-readable action label (e.g. "Generate Image").</summary>
    string ActionLabel { get; }

    /// <summary>Returns true if this action is applicable to the given entity type.</summary>
    bool CanHandle(string entityType);

    /// <summary>Execute the action for the given entity. Returns a user-facing result message.</summary>
    Task<string> ExecuteAsync(string entityType, string entityId, CancellationToken ct = default);
}
