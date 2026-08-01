using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Infra.Tests.Helper;

/// <summary>
/// Characterization tests for ReferenceParser + ReferencePattern.
/// Pins down exact behavior BEFORE Phase 2 changes.
/// </summary>
public class ReferenceParserTests
{
    // ═══════════════════════════════════════════════════════════════
    // ReferencePattern — ExtractRawId
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("{id}", "211", "211")]
    [InlineData("{id}", "NSE:42", "NSE:42")]
    [InlineData("{id}", "-115", "115")]
    [InlineData("{id}", "0:152", "0:152")]
    [InlineData("{id}", "", "")]
    public void ExtractRawId_IdPattern(string pattern, string input, string expected)
    {
        var result = ReferenceParser.ExtractRawId(input, pattern);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{id}x{mult}", "211x1.0", "211")]
    [InlineData("{id}x{mult}", "NSE:42x2", "NSE:42")]
    [InlineData("{id}x{mult}", "-211x0.5", "211")]
    [InlineData("{id}x{mult}", "1", "1")]
    public void ExtractRawId_IdXMultPattern(string pattern, string input, string expected)
    {
        var result = ReferenceParser.ExtractRawId(input, pattern);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{mult}x{id}", "1x211", "211")]
    [InlineData("{mult}x{id}", "2xNSE:42", "NSE:42")]
    [InlineData("{mult}x{id}", "3.5x15", "15")]
    public void ExtractRawId_MultXIdPattern(string pattern, string input, string expected)
    {
        var result = ReferenceParser.ExtractRawId(input, pattern);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{id}={value}", "38=1", "38")]
    [InlineData("{id}={value}", "NSE:38=1.5", "NSE:38")]
    [InlineData("{id}={value}", "50=0.5", "50")]
    public void ExtractRawId_IdEqualsValuePattern(string pattern, string input, string expected)
    {
        var result = ReferenceParser.ExtractRawId(input, pattern);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("{value}={id}", "1=38", "38")]
    [InlineData("{value}={id}", "1.5=NSE:38", "NSE:38")]
    public void ExtractRawId_ValueEqualsIdPattern(string pattern, string input, string expected)
    {
        var result = ReferenceParser.ExtractRawId(input, pattern);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("[{id}", "[211", "211")]
    [InlineData("[{id}", "[211,SomeData]", "211")]
    [InlineData("[{id}", "211", "211")]
    public void ExtractRawId_BracketIdPattern(string pattern, string input, string expected)
    {
        var result = ReferenceParser.ExtractRawId(input, pattern);
        Assert.Equal(expected, result);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferencePattern.FromName
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FromName_UnknownPattern_ReturnsIdPattern()
    {
        var pattern = ReferencePattern.FromName("garbage");
        Assert.Equal("Id", pattern.Name);
    }

    [Fact]
    public void FromName_Null_ReturnsIdPattern()
    {
        var pattern = ReferencePattern.FromName(null);
        Assert.Equal("Id", pattern.Name);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceParser.ParseReference
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NSE:42", "NSE", 42)]
    [InlineData("152", "", 152)]
    [InlineData("0:152", "0", 152)]
    [InlineData("", "", 0)]
    [InlineData("   ", "", 0)]
    [InlineData("NSE:", "NSE", 0)]
    public void ParseReference_ReturnsCorrectModAndId(string raw, string expectedMod, int expectedId)
    {
        var (modName, id) = ReferenceParser.ParseReference(raw);
        Assert.Equal(expectedMod, modName);
        Assert.Equal(expectedId, id);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceParser.ParseSingle
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("211", "", 211, 1.0)]
    [InlineData("NSE:42", "NSE", 42, 1.0)]
    [InlineData("211x1.5", "", 211, 1.5)]
    [InlineData("NSE:42x2", "NSE", 42, 2.0)]
    public void ParseSingle_CorrectOutput(string raw, string expectedMod, int expectedId, double expectedMult)
    {
        var result = ReferenceParser.ParseSingle(raw);
        Assert.Equal(expectedMod, result.ModName);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(expectedMult, result.Multiplier);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceParser.Parse — high-level entry point
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmptySegments()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        var result = ReferenceParser.Parse("", attr);
        Assert.Empty(result.Segments);

        result = ReferenceParser.Parse("   ", attr);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public void Parse_SingleValue_IdPattern()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        var result = ReferenceParser.Parse("42", attr);

        Assert.Single(result.Segments);
        Assert.Equal("42", result.Segments[0].ExtractedId);
        Assert.Equal(42, result.Segments[0].NumericId);
        Assert.Null(result.Segments[0].Namespace);
    }

    [Fact]
    public void Parse_SingleValue_WithNamespace()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        var result = ReferenceParser.Parse("NSE:42", attr);

        Assert.Single(result.Segments);
        Assert.Equal("NSE:42", result.Segments[0].ExtractedId);
        Assert.Equal(42, result.Segments[0].NumericId);
        Assert.Equal("NSE", result.Segments[0].Namespace);
    }

    [Fact]
    public void Parse_SingleValue_WithNegation()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        var result = ReferenceParser.Parse("-115", attr);

        Assert.Single(result.Segments);
        Assert.Equal("115", result.Segments[0].ExtractedId);
        Assert.Equal(115, result.Segments[0].NumericId);
        Assert.Equal("-", result.Segments[0].ExtraInfo);
    }

    [Fact]
    public void Parse_CommaSeparatedList()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "," };
        var result = ReferenceParser.Parse("10,11,NSE:12", attr);

        Assert.Equal(3, result.Segments.Count);
        Assert.Equal("10", result.Segments[0].ExtractedId);
        Assert.Equal("11", result.Segments[1].ExtractedId);
        Assert.Equal("NSE:12", result.Segments[2].ExtractedId);
        Assert.Equal("NSE", result.Segments[2].Namespace);
    }

    [Fact]
    public void Parse_IdXMultPattern()
    {
        var attr = new ReferenceFieldAttribute(typeof(TreasureTable)) { Pattern = "{id}x{mult}" };
        var result = ReferenceParser.Parse("211x1.0", attr);

        Assert.Single(result.Segments);
        Assert.Equal("211", result.Segments[0].ExtractedId);
        Assert.Equal(211, result.Segments[0].NumericId);
        Assert.Contains("x1.0", result.Segments[0].ExtraInfo);
    }

    [Fact]
    public void Parse_IdXMultPattern_WithNegation()
    {
        var attr = new ReferenceFieldAttribute(typeof(TreasureTable)) { Pattern = "{id}x{mult}" };
        var result = ReferenceParser.Parse("-211x0.5", attr);

        Assert.Single(result.Segments);
        Assert.Equal("211", result.Segments[0].ExtractedId);
        Assert.Contains("-", result.Segments[0].ExtraInfo!);
        Assert.Contains("x50.0%", result.Segments[0].ExtraInfo!); // FmtPct converts 0.5 → 50.0%
    }

    [Fact]
    public void Parse_MultXIdPattern()
    {
        var attr = new ReferenceFieldAttribute(typeof(Ingredient)) { Pattern = "{mult}x{id}" };
        var result = ReferenceParser.Parse("2x15", attr);

        Assert.Single(result.Segments);
        Assert.Equal("15", result.Segments[0].ExtractedId);
        Assert.Equal(15, result.Segments[0].NumericId);
        Assert.Equal("2x", result.Segments[0].ExtraInfo);
    }

    [Fact]
    public void Parse_IdEqualsValuePattern()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Pattern = "{id}={value}" };
        var result = ReferenceParser.Parse("38=1", attr);

        Assert.Single(result.Segments);
        Assert.Equal("38", result.Segments[0].ExtractedId);
        Assert.Equal(38, result.Segments[0].NumericId);
        Assert.Contains("= 1", result.Segments[0].ExtraInfo);
    }

    [Fact]
    public void Parse_ValueEqualsIdPattern()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Pattern = "{value}={id}" };
        var result = ReferenceParser.Parse("1=38", attr);

        Assert.Single(result.Segments);
        Assert.Equal("38", result.Segments[0].ExtractedId);
        Assert.Equal(38, result.Segments[0].NumericId);
        Assert.Equal("1", result.Segments[0].ExtraInfo);
    }

    [Fact]
    public void Parse_BracketIdPattern()
    {
        var attr = new ReferenceFieldAttribute(typeof(BattleMove)) { Pattern = "[{id}" };
        var result = ReferenceParser.Parse("[211,SomeData]", attr);

        Assert.Single(result.Segments);
        Assert.Equal("211", result.Segments[0].ExtractedId);
        Assert.Equal(211, result.Segments[0].NumericId);
    }

    [Fact]
    public void Parse_CompositeTargetKey()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType))
        {
            TargetKey = "{GroupId}.{SubgroupId}"
        };
        var result = ReferenceParser.Parse("86.6", attr);

        Assert.Single(result.Segments);
        Assert.Equal("86.6", result.Segments[0].ExtractedId);
        Assert.Equal(2, result.Segments[0].KeyValues.Count);
        Assert.Equal(86, result.Segments[0].KeyValues["GroupId"]);
        Assert.Equal(6, result.Segments[0].KeyValues["SubgroupId"]);
    }

    [Fact]
    public void Parse_CompositeTargetKey_WithNamespace()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType))
        {
            TargetKey = "{GroupId}.{SubgroupId}"
        };
        var result = ReferenceParser.Parse("NSE:86.6", attr);

        Assert.Single(result.Segments);
        Assert.Equal("NSE:86.6", result.Segments[0].ExtractedId);
        Assert.Equal("NSE", result.Segments[0].Namespace);
        Assert.Equal(86, result.Segments[0].KeyValues["GroupId"]);
        Assert.Equal(6, result.Segments[0].KeyValues["SubgroupId"]);
    }

    [Fact]
    public void Parse_CompositeTargetKey_WithMultiValue()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType))
        {
            Separator = ",",
            TargetKey = "{GroupId}.{SubgroupId}"
        };
        var result = ReferenceParser.Parse("86.6,87.3", attr);

        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(86, result.Segments[0].KeyValues["GroupId"]);
        Assert.Equal(6, result.Segments[0].KeyValues["SubgroupId"]);
        Assert.Equal(87, result.Segments[1].KeyValues["GroupId"]);
        Assert.Equal(3, result.Segments[1].KeyValues["SubgroupId"]);
    }

    [Fact]
    public void Parse_DefaultNamespace_Stripped()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        var result = ReferenceParser.Parse("0:152", attr);

        Assert.Single(result.Segments);
        Assert.Equal("0:152", result.Segments[0].ExtractedId);
        Assert.Equal(152, result.Segments[0].NumericId);
        // Namespace "0" is kept as-is by Parse (NormalizeNamespace exists separately)
        Assert.Equal("0", result.Segments[0].Namespace);
    }

    [Fact]
    public void Parse_PlusSeparator_RecipeStyle()
    {
        var attr = new ReferenceFieldAttribute(typeof(Ingredient))
        {
            Separator = "+",
            Pattern = "{mult}x{id}"
        };
        var result = ReferenceParser.Parse("1x2+1x3", attr);

        Assert.Equal(2, result.Segments.Count);
        Assert.Equal("2", result.Segments[0].ExtractedId);
        Assert.Equal("3", result.Segments[1].ExtractedId);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceParser.ParseTargetKey
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseTargetKey_Null_ReturnsIdKey()
    {
        var result = ReferenceParser.ParseTargetKey(null);
        Assert.Equal(["Id"], result.KeyNames);
        Assert.Equal("", result.KeySeparator);
    }

    [Fact]
    public void ParseTargetKey_Empty_ReturnsIdKey()
    {
        var result = ReferenceParser.ParseTargetKey("");
        Assert.Equal(["Id"], result.KeyNames);
    }

    [Fact]
    public void ParseTargetKey_Composite()
    {
        var result = ReferenceParser.ParseTargetKey("{GroupId}.{SubgroupId}");
        Assert.Equal(["GroupId", "SubgroupId"], result.KeyNames);
        Assert.Equal(".", result.KeySeparator);
        Assert.True(result.IsComposite);
    }

    [Fact]
    public void ParseTargetKey_Simple()
    {
        var result = ReferenceParser.ParseTargetKey("{Id}");
        Assert.Equal(["Id"], result.KeyNames);
        Assert.False(result.IsComposite);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceParser.DecomposeId
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void DecomposeId_SimpleKey_ParsesInt()
    {
        var keyInfo = new TargetKeyInfo(["Id"], "");
        var result = ReferenceParser.DecomposeId("42", keyInfo);
        Assert.Equal(42, result["Id"]);
    }

    [Fact]
    public void DecomposeId_WithNamespace_StripsNamespace()
    {
        var keyInfo = new TargetKeyInfo(["Id"], "");
        var result = ReferenceParser.DecomposeId("NSE:42", keyInfo);
        Assert.Equal(42, result["Id"]);
    }

    [Fact]
    public void DecomposeId_CompositeKey()
    {
        var keyInfo = new TargetKeyInfo(["GroupId", "SubgroupId"], ".");
        var result = ReferenceParser.DecomposeId("86.6", keyInfo);
        Assert.Equal(86, result["GroupId"]);
        Assert.Equal(6, result["SubgroupId"]);
    }

    [Fact]
    public void DecomposeId_CompositeKey_FallbackToId()
    {
        // When raw ID doesn't contain separator, fall back to "Id" key
        var keyInfo = new TargetKeyInfo(["GroupId", "SubgroupId"], ".");
        var result = ReferenceParser.DecomposeId("418", keyInfo);
        Assert.False(result.ContainsKey("GroupId"));
        Assert.Equal(418, result["Id"]);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceParser.ExtractIds — fast path
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractIds_CommaSeparated()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "," };
        var results = ReferenceParser.ExtractIds("10,11,12", attr);

        Assert.Equal(3, results.Count);
        Assert.Equal("10", results[0].ExtractedId);
        Assert.Equal("11", results[1].ExtractedId);
        Assert.Equal("12", results[2].ExtractedId);
    }

    [Fact]
    public void ExtractIds_Empty_ReturnsEmpty()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        var results = ReferenceParser.ExtractIds("", attr);
        Assert.Empty(results);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceParser namespace helpers
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData(null, true)]
    [InlineData("NSE", false)]
    public void IsDefaultNamespace(string? ns, bool expected)
    {
        Assert.Equal(expected, ReferenceParser.IsDefaultNamespace(ns));
    }

    [Theory]
    [InlineData("0:152", "152")]
    [InlineData("NSE:42", "NSE:42")]
    [InlineData("152", "152")]
    public void FormatForDisplay(string raw, string expected)
    {
        Assert.Equal(expected, ReferenceParser.FormatForDisplay(raw));
    }

    [Theory]
    [InlineData("NSE:42", "NSE:42")]        // non-default namespace kept
    [InlineData("0:152", "152")]            // "0:" stripped (default ns)
    [InlineData("NSE:38=1", "NSE:38")]      // strips =value suffix, keeps NSE:
    [InlineData("38=1", "38")]              // strips =value suffix only
    public void BuildLookupKey_StripsDefaultNs(string extractedId, string expected)
    {
        var result = ReferenceParser.BuildLookupKey(extractedId);
        Assert.Equal(expected, result);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferencePattern.FormatDisplay — display formatting
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IdPattern_FormatDisplay_NoSubject_ReturnsSegment()
    {
        var pattern = ReferencePattern.Id;
        var result = pattern.FormatDisplay("211", null, null);
        Assert.Equal("211", result);
    }

    [Fact]
    public void IdPattern_FormatDisplay_WithSubject()
    {
        var pattern = ReferencePattern.Id;
        var result = pattern.FormatDisplay("211", "Water Bottle", "");
        Assert.Equal("Water Bottle (211)", result);
    }

    [Fact]
    public void IdPattern_FormatDisplay_WithModName()
    {
        var pattern = ReferencePattern.Id;
        var result = pattern.FormatDisplay("NSE:211", "Custom Item", "NSE");
        Assert.Equal("NSE:Custom Item (NSE:211)", result);
    }

    [Fact]
    public void IdPattern_FormatDisplay_WithNegation()
    {
        var pattern = ReferencePattern.Id;
        var result = pattern.FormatDisplay("-211", "Water Bottle", "");
        Assert.Equal("~Water Bottle (211)", result);
    }

    [Fact]
    public void IdXMultPattern_FormatDisplay()
    {
        var pattern = ReferencePattern.IdXMult;
        var result = pattern.FormatDisplay("211x1.5", "Water Bottle", "");
        Assert.Equal("Water Bottlex1.5", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferencePattern.FormatExtraInfo
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IdPattern_FormatExtraInfo_Negation()
    {
        var pattern = ReferencePattern.Id;
        var result = pattern.FormatExtraInfo("-211");
        Assert.Equal("-", result);
    }

    [Fact]
    public void IdXMultPattern_FormatExtraInfo_Multiplier()
    {
        var pattern = ReferencePattern.IdXMult;
        var result = pattern.FormatExtraInfo("211x1.5");
        Assert.Equal("x1.5", result);
    }

    [Fact]
    public void IdEqualsValuePattern_FormatExtraInfo()
    {
        var pattern = ReferencePattern.IdEqualsValue;
        var result = pattern.FormatExtraInfo("38=1");
        Assert.Equal("= 1", result);
    }

    [Fact]
    public void ValueEqualsIdPattern_FormatExtraInfo()
    {
        var pattern = ReferencePattern.ValueEqualsId;
        var result = pattern.FormatExtraInfo("1=38");
        Assert.Equal("1", result);
    }

    // ═══════════════════════════════════════════════════════════════
    // ReferenceFieldAttribute — properties
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ReferenceFieldAttribute_IsMultiValue_FalseByDefault()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        Assert.False(attr.IsMultiValue);
    }

    [Fact]
    public void ReferenceFieldAttribute_IsMultiValue_TrueWithSeparator()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "," };
        Assert.True(attr.IsMultiValue);
    }

    [Fact]
    public void ReferenceFieldAttribute_StoresTargetType()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType));
        Assert.Equal(typeof(ItemType), attr.TargetEntityType);
    }

    // ═══════════════════════════════════════════════════════════════
    // Roundtrip: Parse → re-serialize via segments
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Roundtrip_SingleValue()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition));
        var parsed = ReferenceParser.Parse("42", attr);
        var rawText = parsed.Segments[0].RawText;
        Assert.Equal("42", rawText);
    }

    [Fact]
    public void Roundtrip_CommaList()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "," };
        var parsed = ReferenceParser.Parse("10,11,12", attr);
        var recombined = string.Join(",", parsed.Segments.Select(s => s.RawText));
        Assert.Equal("10,11,12", recombined);
    }
}
