using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using Xunit;

namespace NeoEditor.Core.Tests.Model;

/// <summary>
/// Regression: ReferenceList values must be read via <see cref="ReferenceText.GetRawString"/>
/// (raw XML text), never .ToString() which emits the damaged "[a, b]" format.
/// round28 fixed this in XmlParser/ReferenceIndex/ReferenceResolver; ReferenceText is the
/// shared helper so display paths can't reintroduce it.
/// </summary>
public class ReferenceTextTests
{
    private static ReferenceList<IReferenceEntry> List(params (string Id, string? Ns)[] items)
    {
        var rl = new ReferenceList<IReferenceEntry>();
        foreach (var (id, ns) in items)
            rl.Add(new PureRefFormat { Entity = new EntityRef { Id = id, Namespace = ns } });
        return rl;
    }

    [Fact]
    public void ReferenceList_UsesRawText_NotBrokenToString()
    {
        var rl = List(("3", null), ("14", null));
        var attr = new ReferenceFieldAttribute(typeof(Faction)) { Separator = "," };

        // Sanity: ReferenceList.ToString() is exactly the damaged format round28 fixed.
        Assert.Equal("[3, 14]", rl.ToString());
        Assert.Equal("3,14", ReferenceText.GetRawString(rl, attr));
    }

    [Fact]
    public void ReferenceList_PreservesNamespacePrefix()
        => Assert.Equal("NSE:42", ReferenceText.GetRawString(List(("42", "NSE")), null));

    [Fact]
    public void ReferenceList_CompositeKey_NoSeparator()
        => Assert.Equal("86.6", ReferenceText.GetRawString(
            new ReferenceList<IReferenceEntry>
            {
                new PureRefFormat { Entity = new EntityRef { GroupId = 86, SubgroupId = 6 } }
            }, null));

    [Fact]
    public void PlainString_ReturnsVerbatim()
        => Assert.Equal("hello", ReferenceText.GetRawString("hello", null));

    [Fact]
    public void Null_ReturnsEmpty()
        => Assert.Equal("", ReferenceText.GetRawString(null, null));
}