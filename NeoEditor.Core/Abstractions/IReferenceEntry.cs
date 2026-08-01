namespace NeoEditor.Core.Abstractions;

/// <summary>
/// A single entry inside a reference list.
/// Each entry can serialize itself back to the raw XML text format (roundtrip-safe).
/// </summary>
public interface IReferenceEntry
{
    /// <summary>
    /// Serialize to the exact XML-storable text representation.
    /// Roundtrip: Deserialize(ToRawString()) must return an equal entry.
    /// </summary>
    string ToRawString();

    /// <summary>
    /// Human-readable display text for UI rendering (DataGrid cells, tooltips, etc.).
    /// Default implementation returns <see cref="ToRawString"/>.
    /// </summary>
    string DisplayText => ToRawString();
}
