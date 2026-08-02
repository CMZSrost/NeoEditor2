using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;

namespace NeoEditor.Core.Tests.Model;

public class ReferenceEntryTypesTests
{
    // ═══════════════════════════════════════════════════════════
    // EntityRef
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void EntityRef_Simple() { Assert.Equal("211", new EntityRef { Id = "211" }.ToRawString()); }
    [Fact]
    public void EntityRef_WithNS() { Assert.Equal("NSE:42", new EntityRef { Namespace = "NSE", Id = "42" }.ToRawString()); }
    [Fact]
    public void EntityRef_DefaultNS() { Assert.Equal("0:152", new EntityRef { Namespace = "0", Id = "152" }.ToRawString()); }
    [Fact]
    public void EntityRef_NullNS() { Assert.Equal("211", new EntityRef { Namespace = null, Id = "211" }.ToRawString()); }
    [Fact]
    public void EntityRef_Composite() { Assert.Equal("86.6", new EntityRef { GroupId = 86, SubgroupId = 6 }.ToRawString()); }
    [Fact]
    public void EntityRef_Composite_NS() { Assert.Equal("NSE:86.6", new EntityRef { Namespace = "NSE", GroupId = 86, SubgroupId = 6 }.ToRawString()); }
    [Fact]
    public void EntityRef_IsComposite_True() { Assert.True(new EntityRef { GroupId = 86, SubgroupId = 6 }.IsComposite); }
    [Fact]
    public void EntityRef_IsComposite_False() { Assert.False(new EntityRef { GroupId = 86 }.IsComposite); }

    // ═══════════════════════════════════════════════════════════
    // Format classes
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void PureRefFormat() { Assert.Equal("211", new PureRefFormat { Entity = new() { Id = "211" } }.ToRawString()); }

    [Fact]
    public void NegatedRefFormat() { Assert.Equal("-211", new NegatedRefFormat { Inner = new EntityRef { Id = "211" } }.ToRawString()); }

    [Fact]
    public void NegatedRefFormat_WrapsAnyEntry()
    {
        // Negation can wrap any IReferenceEntry, not just EntityRef
        var inner = new IdXMultFormat { Entity = new() { Id = "211" }, Multiplier = 1.5 };
        var neg = new NegatedRefFormat { Inner = inner };
        Assert.Equal("-211x1.5", neg.ToRawString());
    }

    [Fact]
    public void IdXMultFormat() { Assert.Equal("211x1.5", new IdXMultFormat { Entity = new() { Id = "211" }, Multiplier = 1.5 }.ToRawString()); }

    [Fact]
    public void MultXIdFormat() { Assert.Equal("2x15", new MultXIdFormat { Entity = new() { Id = "15" }, Multiplier = 2 }.ToRawString()); }

    [Fact]
    public void AssignFormat_IdFirst() { Assert.Equal("38=1", new AssignFormat { Entity = new() { Id = "38" }, Value = 1, ValueFirst = false }.ToRawString()); }

    [Fact]
    public void AssignFormat_ValueFirst() { Assert.Equal("1=38", new AssignFormat { Entity = new() { Id = "38" }, Value = 1, ValueFirst = true }.ToRawString()); }

    [Fact]
    public void BracketFormat() { Assert.Equal("[211]", new BracketFormat { Entity = new() { Id = "211" } }.ToRawString()); }

    [Fact]
    public void MultiIngredientRecipeFormat_IngredientsOnly()
    {
        // Recipe style: {mult}x{id} pattern → MultXIdFormat
        var fmt = new MultiIngredientRecipeFormat
        {
            Ingredients = new List<IReferenceFormat>
            {
                new MultXIdFormat { Entity = new() { Id = "2" }, Multiplier = 1 },
                new MultXIdFormat { Entity = new() { Id = "3" }, Multiplier = 1 }
            }
        };
        Assert.Equal("1x2+1x3", fmt.ToRawString());
    }

    [Fact]
    public void MultiIngredientRecipeFormat_WithTarget()
    {
        // IdXMult style: {id}x{mult} pattern → IdXMultFormat
        var fmt = new MultiIngredientRecipeFormat
        {
            Ingredients = new List<IReferenceFormat>
            {
                new IdXMultFormat { Entity = new() { GroupId = 91, SubgroupId = 8 }, Multiplier = 1 },
                new IdXMultFormat { Entity = new() { GroupId = 91, SubgroupId = 3 }, Multiplier = 1 }
            },
            Target = new EntityRef { Id = "22" },
            TargetParams = new List<double> { 1, 0, 0, 0 }
        };
        Assert.Equal("91.8x1+91.3x1=22x1x0x0x0", fmt.ToRawString());
    }

    [Fact]
    public void MultiIngredientRecipeFormat_FormatTemplate()
    {
        var fmt = new MultiIngredientRecipeFormat
        {
            Ingredients = [new IdXMultFormat { Entity = new() { Id = "2" }, Multiplier = 1 }],
            Target = new EntityRef { Id = "22" },
            TargetParams = [1, 0, 0, 0]
        };
        Assert.Contains("{fmt}", fmt.FormatTemplate);
        Assert.Contains("{target}", fmt.FormatTemplate);
    }

    [Fact]
    public void AllFormats_ImplementIReferenceFormat()
    {
        Assert.IsAssignableFrom<IReferenceFormat>(new PureRefFormat());
        Assert.IsAssignableFrom<IReferenceFormat>(new NegatedRefFormat());
        Assert.IsAssignableFrom<IReferenceFormat>(new IdXMultFormat());
        Assert.IsAssignableFrom<IReferenceFormat>(new MultXIdFormat());
        Assert.IsAssignableFrom<IReferenceFormat>(new AssignFormat());
        Assert.IsAssignableFrom<IReferenceFormat>(new BracketFormat());
        Assert.IsAssignableFrom<IReferenceFormat>(new MultiIngredientRecipeFormat());
    }

    [Fact]
    public void AllFormats_AlsoImplementIReferenceEntry()
    {
        Assert.IsAssignableFrom<IReferenceEntry>(new PureRefFormat());
        Assert.IsAssignableFrom<IReferenceEntry>(new IdXMultFormat());
        Assert.IsAssignableFrom<IReferenceEntry>(new MultiIngredientRecipeFormat());
    }
}
