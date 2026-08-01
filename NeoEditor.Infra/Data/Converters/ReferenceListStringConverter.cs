using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Helper;

namespace NeoEditor.Data.Converters;

/// <summary>
/// EF Core <see cref="ValueConverter"/> that maps <see cref="ReferenceList{IReferenceEntry}"/>
/// to a plain string column (SQLite TEXT). Uses <see cref="ReferenceFieldAttribute"/> metadata
/// and <see cref="IReferenceListSerializer"/> for bidirectional conversion.
/// Roundtrip-safe: Serialize(Deserialize(str)) == str for all valid reference formats.
/// </summary>
public class ReferenceListStringConverter : ValueConverter<ReferenceList<IReferenceEntry>, string>
{
    public ReferenceListStringConverter(
        ReferenceFieldAttribute metadata,
        IReferenceListSerializer serializer)
        : base(
            list => serializer.Serialize(list, metadata),
            str => serializer.Deserialize(str ?? "", metadata))
    {
    }
}
