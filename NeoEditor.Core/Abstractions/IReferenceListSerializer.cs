namespace NeoEditor.Core.Abstractions;

using NeoEditor.Data.Model;
using NeoEditor.Helper;

/// <summary>
/// Serializes and deserializes <see cref="ReferenceList{IReferenceEntry}"/> to/from
/// raw XML field text. Bidirectionally compatible with the existing string-based format.
/// Wraps internal ReferenceParser logic.
/// </summary>
public interface IReferenceListSerializer
{
    /// <summary>
    /// Deserialize a raw XML field value into a typed reference list.
    /// Uses the <see cref="ReferenceFieldAttribute"/> metadata to determine
    /// separator, pattern, and target key for parsing.
    /// </summary>
    ReferenceList<IReferenceEntry> Deserialize(string raw, ReferenceFieldAttribute metadata);

    /// <summary>
    /// Serialize a reference list back to the exact XML field value.
    /// Roundtrip: Serialize(Deserialize(raw, attr), attr) == raw.
    /// </summary>
    string Serialize(ReferenceList<IReferenceEntry> list, ReferenceFieldAttribute metadata);
}
