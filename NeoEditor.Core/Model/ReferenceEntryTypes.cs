using NeoEditor.Core.Abstractions;

namespace NeoEditor.Data.Model;

/// <summary>
/// Pure entity reference — the fundamental unit that identifies a target game entity.
/// Contains only the fields needed to locate the target: namespace, id, and optional
/// composite key components.
///
/// Format classes (in <see cref="ReferenceFormats"/>) wrap EntityRef with typed parameters:
///   PureRefFormat, NegatedRefFormat, IdXMultFormat, MultXIdFormat, AssignFormat,
///   BracketFormat, MultiIngredientRecipeFormat.
///
/// Examples:
///   "211"      → EntityRef { Id = "211" }
///   "NSE:42"   → EntityRef { Namespace = "NSE", Id = "42" }
///   "86.6"     → EntityRef { GroupId = 86, SubgroupId = 6 }
///   "NSE:86.6" → EntityRef { Namespace = "NSE", GroupId = 86, SubgroupId = 6 }
/// </summary>
public sealed record EntityRef : IReferenceEntry
{
    /// <summary>
    /// Mod namespace prefix.
    /// "" or null = no namespace (bare reference, resolves in source entity's context).
    /// "0" = game's default namespace (serialized as "0:152").
    /// "NSE" = a mod's namespace (serialized as "NSE:42").
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>Simple entity ID, e.g. "211". Used when <see cref="GroupId"/> is null.</summary>
    public string Id { get; init; } = "";

    /// <summary>First component of a composite lookup key (e.g. nGroupID on ItemType).</summary>
    public int? GroupId { get; init; }

    /// <summary>Second component of a composite lookup key (e.g. nSubgroupID on ItemType).</summary>
    public int? SubgroupId { get; init; }

    /// <summary>True when this is a composite-key reference.</summary>
    public bool IsComposite => GroupId.HasValue && SubgroupId.HasValue;

    /// <summary>True when there is a non-null namespace (including "0").</summary>
    public bool HasNamespace => !string.IsNullOrEmpty(Namespace);

    /// <inheritdoc/>
    public string ToRawString()
    {
        var key = IsComposite
            ? $"{GroupId}.{SubgroupId}"
            : Id;

        return HasNamespace
            ? $"{Namespace}:{key}"
            : key;
    }

    /// <inheritdoc/>
    public string DisplayText => ToRawString();
}
