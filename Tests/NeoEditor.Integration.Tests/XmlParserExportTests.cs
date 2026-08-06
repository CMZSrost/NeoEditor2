using Microsoft.Extensions.Logging.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using Xunit;

namespace NeoEditor.Integration.Tests;

/// <summary>
/// Regression for Doc 37 偏差5: XmlParser.Export must serialize reference columns as their raw
/// text (via the serializer), never ToString() which emits "[16, 46]".
/// </summary>
public class XmlParserExportTests
{
    [Fact]
    public void Export_writes_reference_raw_text_not_tostring()
    {
        var parser = new XmlParser(NullLogger<XmlParser>.Instance, new ReferenceListSerializer());
        var item = new ItemType { Id = 5, EntityId = "it-5", ModId = 0 };
        item.Properties.Add(new PureRefFormat { Entity = new EntityRef { Id = "16" } });
        item.Properties.Add(new PureRefFormat { Entity = new EntityRef { Id = "46" } });

        var doc = parser.Export(new IEntity[] { item });
        var xml = doc.ToString();

        Assert.Contains(">16,46<", xml);
        Assert.DoesNotContain("[16, 46]", xml);
    }
}
