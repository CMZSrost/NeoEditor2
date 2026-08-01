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
}
