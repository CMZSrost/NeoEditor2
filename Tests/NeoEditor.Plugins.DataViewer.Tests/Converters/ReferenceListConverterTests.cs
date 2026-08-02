using System.Globalization;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.DataViewer.Converters;
using Xunit;

namespace NeoEditor.Plugins.DataViewer.Tests;

/// <summary>
/// R30 (P2): DataGrid edit controls bridge string ↔ ReferenceList through
/// ReferenceListConverter — reads raw text ("3,14", never "[3, 14]") and writes
/// back through the serializer so cell edits reach the entity.
/// </summary>
public class ReferenceListConverterTests
{
    private static readonly ReferenceFieldAttribute Attr =
        new(typeof(Encounter)) { Separator = "," };

    [Fact]
    public void Convert_ReferenceList_ReturnsRawText_NotBrokenBrackets()
    {
        var converter = new ReferenceListConverter();
        var list = new ReferenceList<IReferenceEntry>
        {
            new PureRefFormat { Entity = new EntityRef { Id = "3" } },
            new PureRefFormat { Entity = new EntityRef { Id = "14" } },
        };

        var result = converter.Convert(list, typeof(string), Attr, CultureInfo.InvariantCulture);

        Assert.Equal("3,14", result);
    }

    [Fact]
    public void ConvertBack_RawText_RestoresReferenceList()
    {
        var converter = new ReferenceListConverter();

        var result = converter.ConvertBack("3,14", typeof(ReferenceList<IReferenceEntry>), Attr,
            CultureInfo.InvariantCulture);

        var list = Assert.IsType<ReferenceList<IReferenceEntry>>(result);
        Assert.Equal(2, list.Count);
        Assert.Equal("3,14", list.ToRawString(","));
    }

    [Fact]
    public void ConvertBack_EmptyText_ReturnsEmptyList()
    {
        var converter = new ReferenceListConverter();

        var result = converter.ConvertBack("", typeof(ReferenceList<IReferenceEntry>), Attr,
            CultureInfo.InvariantCulture);

        var list = Assert.IsType<ReferenceList<IReferenceEntry>>(result);
        Assert.Empty(list);
    }
}
