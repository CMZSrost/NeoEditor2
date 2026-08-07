using System.Text.Json;
using NeoEditor.Player.Core.ViewModels;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class SaveTreeTests
{
    private const string ObjectJson =
        "{\"__amf\":\"object\",\"className\":\"Creature\",\"names\":[\"m_fHealth\",\"m_strName\"]," +
        "\"values\":[{\"__n\":0.9},\"Alice\"],\"dynamic\":[{\"name\":\"m_nXP\",\"value\":{\"__i\":100}}]," +
        "\"isDynamic\":true}";

    private const string ComplexJson =
        "{\"__amf\":\"object\",\"className\":\"\",\"names\":[],\"values\":[],\"isDynamic\":true," +
        "\"dynamic\":[{\"name\":\"vec\",\"value\":{\"__amf\":\"vecint\",\"fixed\":false,\"values\":[1,2,3]}}," +
        "{\"name\":\"arr\",\"value\":{\"__amf\":\"array\",\"dense\":[{\"__i\":1},\"two\",false]," +
        "\"assoc\":{\"custom\":{\"__n\":5.5}}}}," +
        "{\"name\":\"dict\",\"value\":{\"__amf\":\"dict\",\"weak\":false," +
        "\"entries\":[[\"k\",{\"__n\":1.0}],[{\"__i\":2},true]]}}," +
        "{\"name\":\"nan\",\"value\":{\"__n\":\"NaN\"}}]}";

    private static SaveNode Build(string json, string name = "root")
        => SaveTree.Build(JsonDocument.Parse(json).RootElement, name);

    private static string Serialize(SaveNode node)
        => SaveTree.SerializeValue(node)!.ToJsonString();

    [Fact]
    public void BuildsObjectWithSealedAndDynamicFields()
    {
        var node = Build(ObjectJson, "objSG");

        var obj = Assert.IsType<SaveObjectNode>(node);
        Assert.Equal("objSG", obj.Name);
        Assert.Equal("Creature", obj.ClassName);
        Assert.True(obj.IsDynamic);
        Assert.Equal(2, obj.SealedValues.Count);

        var health = Assert.IsType<SaveScalarNode>(obj.SealedValues[0]);
        Assert.Equal("m_fHealth", health.Name);
        Assert.Equal(SaveScalarKind.Double, health.Kind);
        Assert.Equal("0.9", health.ValueText);

        var name = Assert.IsType<SaveScalarNode>(obj.SealedValues[1]);
        Assert.Equal(SaveScalarKind.String, name.Kind);
        Assert.Equal("Alice", name.ValueText);

        var xp = Assert.IsType<SaveScalarNode>(obj.DynamicValues[0]);
        Assert.Equal("m_nXP", xp.Name);
        Assert.Equal(SaveScalarKind.Int, xp.Kind);
        Assert.Equal("100", xp.ValueText);
        Assert.True(xp.IsAssoc);
        Assert.Equal(3, obj.Children.Count);
    }

    [Fact]
    public void EditedScalarRoundTripsThroughJson()
    {
        var node = Build(ObjectJson);
        var obj = Assert.IsType<SaveObjectNode>(node);
        var health = Assert.IsType<SaveScalarNode>(obj.SealedValues[0]);

        health.ValueText = "0.5";   // 用户修改

        var json = Serialize(obj);
        Assert.Contains("\"m_fHealth\"", json);
        Assert.Contains("0.5", json);
        Assert.DoesNotContain("0.9", json);
    }

    [Fact]
    public void SerializeIsStructurallyEquivalentToSource()
    {
        var node = Build(ComplexJson);
        var json = Serialize(node);

        // 结构等价：数值 1.0 vs 1 在 JSON 文本上不同（JsonNode 序列化会省略 .0），
        // 用 JsonNode.DeepEquals 做语义比较（键顺序敏感）。
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(json),
            System.Text.Json.Nodes.JsonNode.Parse(ComplexJson)));
    }

    [Fact]
    public void BuildsVecArrayAndDictChildren()
    {
        var node = Build(ComplexJson);
        var obj = Assert.IsType<SaveObjectNode>(node);

        var vec = Assert.IsType<SaveListNode>(obj.DynamicValues[0]);
        Assert.Equal(SaveListKind.VecInt, vec.Kind);
        Assert.Equal(3, vec.Children.Count);
        Assert.Equal("[1]", vec.Children[1].Name);

        var arr = Assert.IsType<SaveListNode>(obj.DynamicValues[1]);
        Assert.Equal(SaveListKind.Array, arr.Kind);
        Assert.Equal(4, arr.Children.Count);   // 3 dense + 1 assoc
        Assert.True(arr.Children[3].IsAssoc);
        Assert.Equal("custom", arr.Children[3].Name);

        var dict = Assert.IsType<SaveListNode>(obj.DynamicValues[2]);
        Assert.Equal(SaveListKind.Dict, dict.Kind);
        Assert.Equal(2, dict.Children.Count);
        var pair = Assert.IsType<SavePairNode>(dict.Children[0]);
        Assert.Equal("k", Assert.IsType<SaveScalarNode>(pair.Key).ValueText);
        Assert.True(Assert.IsType<SaveScalarNode>(pair.Value).Kind == SaveScalarKind.Double);

        // NaN 字符串标记保留（encValue 的 setFloat64 会还原）
        var nan = Assert.IsType<SaveScalarNode>(obj.DynamicValues[3]);
        Assert.Equal(SaveScalarKind.Double, nan.Kind);
        Assert.Equal("NaN", nan.ValueText);
    }

    [Fact]
    public void InvalidNumberThrowsWithFieldName()
    {
        var node = Build(ObjectJson);
        var obj = Assert.IsType<SaveObjectNode>(node);
        var health = Assert.IsType<SaveScalarNode>(obj.SealedValues[0]);
        health.ValueText = "abc";

        var ex = Assert.Throws<SaveNodeException>(() => Serialize(obj));
        Assert.Contains("m_fHealth", ex.Message);
    }

    [Fact]
    public void BoolAndNullKindsSerializeCorrectly()
    {
        var node = Build("{\"__amf\":\"object\",\"className\":\"\",\"names\":[],\"values\":[]," +
                         "\"dynamic\":[{\"name\":\"b\",\"value\":true},{\"name\":\"n\",\"value\":null}]," +
                         "\"isDynamic\":true}", "obj");
        var obj = Assert.IsType<SaveObjectNode>(node);

        var b = Assert.IsType<SaveScalarNode>(obj.DynamicValues[0]);
        Assert.True(b.IsBool);
        b.BoolValue = false;   // 用户改 false

        var json = Serialize(obj);
        Assert.Contains("\"name\":\"b\",\"value\":false", json);
        Assert.Contains("\"name\":\"n\",\"value\":null", json);
    }
}
