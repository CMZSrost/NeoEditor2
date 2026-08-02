using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Infra.Tests.Helper;

public class ReferenceListSerializerTests
{
    private readonly ReferenceListSerializer _serializer = new();

    // ═══════════════════════════════════════════════════════════
    // {id} — PureRefFormat / NegatedRefFormat
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Id_plain_returns_PureRefFormat()
    {
        var list = _serializer.Deserialize("42", new(typeof(Condition)));
        var f = Assert.IsType<PureRefFormat>(list[0]);
        Assert.Equal("42", f.Entity.Id);
        Assert.Equal("42", _serializer.Serialize(list, new(typeof(Condition))));
    }

    [Fact]
    public void Id_namespaced()
    {
        var list = _serializer.Deserialize("NSE:42", new(typeof(Condition)));
        var f = Assert.IsType<PureRefFormat>(list[0]);
        Assert.Equal("42", f.Entity.Id);
        Assert.Equal("NSE", f.Entity.Namespace);
        Assert.Equal("NSE:42", _serializer.Serialize(list, new(typeof(Condition))));
    }

    [Fact]
    public void Id_negated_returns_NegatedRefFormat()
    {
        var list = _serializer.Deserialize("-115", new(typeof(Condition)));
        var f = Assert.IsType<NegatedRefFormat>(list[0]);
        var inner = Assert.IsType<EntityRef>(f.Inner);
        Assert.Equal("115", inner.Id);
        Assert.Equal("-115", _serializer.Serialize(list, new(typeof(Condition))));
    }

    [Fact]
    public void Id_0namespace_preserved()
    {
        var list = _serializer.Deserialize("0:152", new(typeof(Condition)));
        var f = Assert.IsType<PureRefFormat>(list[0]);
        Assert.Equal("0", f.Entity.Namespace);
        Assert.Equal("152", f.Entity.Id);
        Assert.Equal("0:152", _serializer.Serialize(list, new(typeof(Condition))));
    }

    // ═══════════════════════════════════════════════════════════
    // {id}x{mult} — IdXMultFormat
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void IdXMult()
    {
        var attr = new ReferenceFieldAttribute(typeof(TreasureTable)) { Pattern = "{id}x{mult}" };
        var list = _serializer.Deserialize("211x1.5", attr);
        var f = Assert.IsType<IdXMultFormat>(list[0]);
        Assert.Equal("211", f.Entity.Id);
        Assert.Equal(1.5, f.Multiplier);
        Assert.Equal("211x1.5", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void IdXMult_negated()
    {
        var attr = new ReferenceFieldAttribute(typeof(TreasureTable)) { Pattern = "{id}x{mult}" };
        var list = _serializer.Deserialize("-211x0.5", attr);
        // NegatedRefFormat wraps IdXMultFormat
        var neg = Assert.IsType<NegatedRefFormat>(list[0]);
        var inner = Assert.IsType<IdXMultFormat>(neg.Inner);
        Assert.Equal("211", inner.Entity.Id);
        Assert.Equal(0.5, inner.Multiplier);
        Assert.Equal("-211x0.5", _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // {mult}x{id} — MultXIdFormat
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void MultXId()
    {
        var attr = new ReferenceFieldAttribute(typeof(Ingredient)) { Pattern = "{mult}x{id}" };
        var list = _serializer.Deserialize("2x15", attr);
        var f = Assert.IsType<MultXIdFormat>(list[0]);
        Assert.Equal("15", f.Entity.Id);
        Assert.Equal(2, f.Multiplier);
        Assert.Equal("2x15", _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // {id}={value} / {value}={id} — AssignFormat
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Assign_id_equals_value()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Pattern = "{id}={value}" };
        var list = _serializer.Deserialize("38=1", attr);
        var f = Assert.IsType<AssignFormat>(list[0]);
        Assert.Equal("38", f.Entity.Id);
        Assert.Equal(1, f.Value);
        Assert.False(f.ValueFirst);
        Assert.Equal("38=1", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void Assign_value_equals_id()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Pattern = "{value}={id}" };
        var list = _serializer.Deserialize("1=38", attr);
        var f = Assert.IsType<AssignFormat>(list[0]);
        Assert.Equal("38", f.Entity.Id);
        Assert.Equal(1, f.Value);
        Assert.True(f.ValueFirst);
        Assert.Equal("1=38", _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // [{id} — BracketFormat
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Bracket()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Pattern = "[{id}" };
        var list = _serializer.Deserialize("[211", attr);
        var f = Assert.IsType<BracketFormat>(list[0]);
        Assert.Equal("211", f.Entity.Id);
        Assert.Equal("[211", _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // Composite key
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Composite()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType)) { TargetKey = "{GroupId}.{SubgroupId}" };
        var list = _serializer.Deserialize("86.6", attr);
        var f = Assert.IsType<PureRefFormat>(list[0]);
        Assert.True(f.Entity.IsComposite);
        Assert.Equal(86, f.Entity.GroupId);
        Assert.Equal(6, f.Entity.SubgroupId);
        Assert.Equal("86.6", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void Composite_with_namespace()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType)) { TargetKey = "{GroupId}.{SubgroupId}" };
        var list = _serializer.Deserialize("NSE:86.6", attr);
        var f = Assert.IsType<PureRefFormat>(list[0]);
        Assert.Equal("NSE", f.Entity.Namespace);
        Assert.True(f.Entity.IsComposite);
        Assert.Equal("NSE:86.6", _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // Multi-value
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Comma_separated_list()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "," };
        var list = _serializer.Deserialize("10,11,12", attr);
        Assert.Equal(3, list.Count);
        Assert.Equal("10,11,12", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void Comma_list_with_namespace()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "," };
        var list = _serializer.Deserialize("10,NSE:42,0:12", attr);
        Assert.Equal(3, list.Count);
        Assert.Equal("10,NSE:42,0:12", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void Plus_separator_recipe()
    {
        var attr = new ReferenceFieldAttribute(typeof(Ingredient)) { Separator = "+", Pattern = "{mult}x{id}" };
        var list = _serializer.Deserialize("1x2+1x3", attr);
        Assert.Equal(2, list.Count);
        Assert.IsType<MultXIdFormat>(list[0]);
        Assert.IsType<MultXIdFormat>(list[1]);
        Assert.Equal("1x2+1x3", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void Amp_separator()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemProp)) { Separator = "&" };
        var list = _serializer.Deserialize("prop1&prop2", attr);
        Assert.Equal(2, list.Count);
        Assert.Equal("prop1&prop2", _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // Edge cases
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Empty_input() { Assert.Equal(0, _serializer.Deserialize("", new(typeof(Condition))).Count); }
    [Fact]
    public void Whitespace_input() { Assert.Equal(0, _serializer.Deserialize("   ", new(typeof(Condition))).Count); }
    [Fact]
    public void Empty_list_serializes_empty() { Assert.Equal("", _serializer.Serialize(new(), new(typeof(Condition)) { Separator = "," })); }
    [Fact]
    public void RawText_preserved() { Assert.Equal("42", _serializer.Deserialize("42", new(typeof(Condition))).RawText); }

    [Fact]
    public void Implicit_conversion_to_string()
    {
        var list = _serializer.Deserialize("42", new(typeof(Condition)));
        string s = list!;
        Assert.Equal("42", s);
    }

    // ═══════════════════════════════════════════════════════════
    // {id}x{mult}x{qty} — IdXMultXQtyFormat (treasuretable.aTreasures)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void IdXMultXQty_three_segment_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType))
        {
            Pattern = "{id}x{mult}x{qty}", Separator = ",", TargetKey = "{GroupId}.{SubgroupId}"
        };
        var list = _serializer.Deserialize("86.6x1.0x5-9", attr);
        var f = Assert.IsType<IdXMultXQtyFormat>(list[0]);
        Assert.True(f.Entity.IsComposite);
        Assert.Equal(86, f.Entity.GroupId);
        Assert.Equal(6, f.Entity.SubgroupId);
        Assert.Equal("1.0", f.Prob);
        Assert.Equal("5-9", f.Qty);
        Assert.Equal("86.6x1.0x5-9", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void IdXMultXQty_qty_omitted_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType))
        {
            Pattern = "{id}x{mult}x{qty}", Separator = ",", TargetKey = "{GroupId}.{SubgroupId}"
        };
        var list = _serializer.Deserialize("36.6x0.01694915254", attr);
        var f = Assert.IsType<IdXMultXQtyFormat>(list[0]);
        Assert.Equal("0.01694915254", f.Prob);
        Assert.Null(f.Qty);
        Assert.Equal("36.6x0.01694915254", _serializer.Serialize(list, attr));
    }

    [Fact]
    public void aTreasures_or_group_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType))
        {
            Pattern = "{id}x{mult}x{qty}", Separator = ",", OrSeparator = "|",
            TargetKey = "{GroupId}.{SubgroupId}"
        };
        const string raw = "10.3x0.25x1-20,35.1x0.1x1-1|35.2x0.1x1-1,11.4x1x1-1";
        var list = _serializer.Deserialize(raw, attr);
        Assert.Equal(3, list.Count);
        Assert.IsType<IdXMultXQtyFormat>(list[0]);
        var group = Assert.IsType<OrGroupFormat>(list[1]);
        Assert.Equal(2, group.Alternatives.Count);
        Assert.IsType<IdXMultXQtyFormat>(group.Alternatives[0]);
        Assert.IsType<IdXMultXQtyFormat>(group.Alternatives[1]);
        Assert.IsType<IdXMultXQtyFormat>(list[2]);
        Assert.Equal(raw, _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // [{id},{p1},{p2}] — BracketFormat params preserved (BattleMove)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Bracket_params_preserved_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "],[", Pattern = "[{id}" };
        const string raw = "[-137,0,0],[146,0,0]";
        var list = _serializer.Deserialize(raw, attr);
        Assert.Equal(2, list.Count);
        var f0 = Assert.IsType<BracketFormat>(list[0]);
        Assert.Equal("-137", f0.Entity.Id);
        Assert.Equal("0", f0.P1);
        Assert.Equal("0", f0.P2);
        Assert.Equal(raw, _serializer.Serialize(list, attr));
    }

    [Fact]
    public void Bracket_decimal_param_preserved()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = "],[", Pattern = "[{id}" };
        var list = _serializer.Deserialize("[209,4,0.5]", attr);
        var f = Assert.IsType<BracketFormat>(list[0]);
        Assert.Equal("209", f.Entity.Id);
        Assert.Equal("4", f.P1);
        Assert.Equal("0.5", f.P2);
        Assert.Equal("[209,4,0.5]", _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // {value}={id} with free-text value (aSwitchIDs state names)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void SwitchId_freetext_value_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(ItemType))
        {
            Separator = ",", Pattern = "{value}={id}", TargetKey = "{GroupId}.{SubgroupId}"
        };
        const string raw = "Hood Off=8.7,On=8.3";
        var list = _serializer.Deserialize(raw, attr);
        Assert.Equal(2, list.Count);
        var f0 = Assert.IsType<AssignFormat>(list[0]);
        Assert.Equal("Hood Off", f0.RawValue);
        Assert.Equal("8.7", f0.Entity.ToRawString());
        Assert.True(f0.ValueFirst);
        Assert.Equal(raw, _serializer.Serialize(list, attr));
    }

    [Fact]
    public void Thresholds_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(Condition)) { Separator = ";", Pattern = "{value}={id}" };
        const string raw = "1=795;2=794;3=763";
        var list = _serializer.Deserialize(raw, attr);
        Assert.Equal(3, list.Count);
        var f0 = Assert.IsType<AssignFormat>(list[0]);
        Assert.Equal(1, f0.Value);
        Assert.Equal("795", f0.Entity.Id);
        Assert.Equal(raw, _serializer.Serialize(list, attr));
    }

    // ═══════════════════════════════════════════════════════════
    // ImageAsset — [Namespace:]FileName, non-numeric Id
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Image_file_ref_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(ImageAsset)) { TargetKey = "{FileName}" };
        const string raw = "0:AModeSpearSharp.png";
        var list = _serializer.Deserialize(raw, attr);
        var f = Assert.IsType<PureRefFormat>(list[0]);
        Assert.Equal("0", f.Entity.Namespace);
        Assert.Equal("AModeSpearSharp.png", f.Entity.Id);
        Assert.Equal(raw, _serializer.Serialize(list, attr));
    }

    [Fact]
    public void SpriteList_roundtrip()
    {
        var attr = new ReferenceFieldAttribute(typeof(ImageAsset))
        {
            Separator = ",", Pattern = "{value}={id}", TargetKey = "{FileName}"
        };
        const string raw = "20=CreItmBagPlasticL.png,13=NSEf:CreItmBagDuffelBack.png";
        var list = _serializer.Deserialize(raw, attr);
        Assert.Equal(2, list.Count);
        var f0 = Assert.IsType<AssignFormat>(list[0]);
        Assert.Equal(20, f0.Value);
        Assert.Equal("CreItmBagPlasticL.png", f0.Entity.Id);
        var f1 = Assert.IsType<AssignFormat>(list[1]);
        Assert.Equal(13, f1.Value);
        Assert.Equal("NSEf", f1.Entity.Namespace);
        Assert.Equal(raw, _serializer.Serialize(list, attr));
    }
}
