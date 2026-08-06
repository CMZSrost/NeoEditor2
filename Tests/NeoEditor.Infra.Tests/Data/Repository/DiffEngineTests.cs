using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using Xunit;

namespace NeoEditor.Infra.Tests.Data.Repository;

/// <summary>
/// Round 31: DiffEngine compares [ReferenceField] properties as canonical raw text
/// ("cp1") — never via ReferenceList.ToString(), which emits the damaged "[a, b]"
/// format and misreports unchanged references as modified.
/// </summary>
public class DiffEngineTests
{
    private static AttackMode WithChargeProfile(string id)
        => new()
        {
            ChargeProfiles = new ReferenceList<IReferenceEntry>
                { new PureRefFormat { Entity = new EntityRef { Id = id } } }
        };

    [Fact]
    public void ComputeDiff_ReferenceFields_SerializeAsRawText()
    {
        var diffs = DiffEngine.ComputeDiff(WithChargeProfile("cp1"), WithChargeProfile("cp2"));

        var d = Assert.Single(diffs);
        Assert.Equal("ChargeProfiles", d.PropertyName);
        Assert.Equal("cp1", d.OldValue);
        Assert.Equal("cp2", d.NewValue);
        Assert.Equal(DiffKind.Modified, d.Kind);
        Assert.DoesNotContain("[", d.OldValue!);
        Assert.DoesNotContain("[", d.NewValue!);
    }

    [Fact]
    public void ComputeDiff_ReferenceFields_Unchanged_NoDiff()
    {
        // Two independent ReferenceList instances holding the same raw text must
        // NOT be reported as changed (reference-equality would false-positive).
        var diffs = DiffEngine.ComputeDiff(WithChargeProfile("cp1"), WithChargeProfile("cp1"));

        Assert.Empty(diffs);
    }

    [Fact]
    public void ComputeDiff_ReferenceFields_MultiValue_JoinWithSeparator()
    {
        var before = new AttackMode
        {
            ChargeProfiles = new ReferenceList<IReferenceEntry>
            {
                new PureRefFormat { Entity = new EntityRef { Id = "cp1" } },
                new PureRefFormat { Entity = new EntityRef { Id = "cp2" } },
            }
        };
        var after = new AttackMode
        {
            ChargeProfiles = new ReferenceList<IReferenceEntry>
            {
                new PureRefFormat { Entity = new EntityRef { Id = "cp1" } },
                new PureRefFormat { Entity = new EntityRef { Id = "cp3" } },
            }
        };

        var diffs = DiffEngine.ComputeDiff(before, after);

        var d = Assert.Single(diffs);
        // ChargeProfiles has Separator = "," → raw text "cp1,cp2" vs "cp1,cp3".
        Assert.Equal("cp1,cp2", d.OldValue);
        Assert.Equal("cp1,cp3", d.NewValue);
    }

    [Fact]
    public void ComputeChangedColumns_ReturnsXmlColumnNames()
    {
        // Docs/41 追修: legacy pending-export upgrade — maps diff entries to the XML column
        // keys ([Column]Name), which is what EditStore/EditRecord use (Name → strName).
        var before = new AttackMode { Id = 1, Name = "old", DamageCut = 1.0 };
        var after = new AttackMode { Id = 1, Name = "new", DamageCut = 1.0 };

        var columns = DiffEngine.ComputeChangedColumns(before, after);

        Assert.Equal(new[] { "strName" }, columns);
    }

    [Fact]
    public void ComputeChangedColumns_ReferenceFields_RawText()
    {
        var columns = DiffEngine.ComputeChangedColumns(WithChargeProfile("cp1"), WithChargeProfile("cp2"));

        Assert.Equal(new[] { "strChargeProfiles" }, columns);
    }
}
