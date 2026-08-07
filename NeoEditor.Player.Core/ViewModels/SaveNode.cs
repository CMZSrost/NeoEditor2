using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NeoEditor.Player.Core.ViewModels;

/// <summary>标量值的可编辑类别（决定编辑控件与序列化方式）。</summary>
public enum SaveScalarKind
{
    String, Int, Double, Bool,
    Null, Undefined,     // 无编辑控件
    Date, Xml, Bytes,    // 只读（保留原始 JSON 原样回写）
}

/// <summary>容器节点的种类（决定子节点布局与序列化结构）。</summary>
public enum SaveListKind { Array, VecInt, VecUInt, VecDouble, VecObject, Dict }

/// <summary>保存树节点：存档 JSON（LsoExpand.toTree 结构）的树形视图。
/// 标量可编辑（string/int/double/bool），容器只读结构（保持 traits 完整，
/// 增删字段会把对象 names[]/values[] 错位——不提供）。</summary>
public abstract class SaveNode
{
    /// <summary>字段名 / 条目名 / 索引（"[i]"）/ assoc key。</summary>
    public string Name { get; set; } = "";

    /// <summary>object 的 dynamic 字段 / array 的 assoc 项标记（序列化分组用）。</summary>
    public bool IsAssoc { get; set; }

    /// <summary>容器子节点（叶子为空集合）。</summary>
    public ObservableCollection<SaveNode> Children { get; } = [];

    public bool IsContainer => this is not SaveScalarNode;

    public abstract string TypeLabel { get; }
}

/// <summary>标量叶子：string/int/double 用 ValueText 编辑，bool 用 BoolValue，其余只读。</summary>
public sealed class SaveScalarNode : SaveNode
{
    public SaveScalarKind Kind { get; init; }

    /// <summary>string/int/double 的编辑文本（double 用 InvariantCulture "R" 保留精度）。</summary>
    public string ValueText { get; set; } = "";

    public bool BoolValue { get; set; }

    /// <summary>只读类型（date/xml/bytes/null/undefined）保留的原始 JSON，序列化时原样回写。</summary>
    public JsonElement? RawJson { get; set; }

    public bool IsEditable => Kind is SaveScalarKind.String or SaveScalarKind.Int or SaveScalarKind.Double;
    public bool IsBool => Kind == SaveScalarKind.Bool;
    public bool IsReadOnly => !IsEditable && !IsBool;

    public override string TypeLabel => Kind switch
    {
        SaveScalarKind.Bytes => "bytes",
        _ => Kind.ToString().ToLowerInvariant(),
    };

    /// <summary>只读节点的显示文本（bytes 显示长度，date 显示毫秒，null/undefined 显示字面）。</summary>
    public string DisplayText => Kind switch
    {
        SaveScalarKind.Null => "null",
        SaveScalarKind.Undefined => "undefined",
        SaveScalarKind.Bytes when RawJson is { } r && r.ValueKind == JsonValueKind.Object
            && r.TryGetProperty("b", out var b) => $"bytes[{b.GetArrayLength()}]",
        _ => ValueText,
    };
}

/// <summary>AMF object：names[]（密封字段）↔ values[] + dynamic[]（动态字段）。</summary>
public sealed class SaveObjectNode : SaveNode
{
    public string ClassName { get; set; } = "";
    public bool IsDynamic { get; set; }

    /// <summary>密封字段（names/values 配对，顺序即序列化顺序）。</summary>
    public List<SaveNode> SealedValues { get; } = [];

    /// <summary>动态字段（序列化为 dynamic: [{name, value}]）。</summary>
    public List<SaveNode> DynamicValues { get; } = [];

    public override string TypeLabel => $"Object({(ClassName.Length == 0 ? "anonymous" : ClassName)})";
}

/// <summary>array / vec* / dict 容器。</summary>
public sealed class SaveListNode : SaveNode
{
    public SaveListKind Kind { get; init; }
    public bool Fixed { get; set; }
    public string? ItemClassName { get; set; }
    public bool Weak { get; set; }

    public override string TypeLabel => Kind switch
    {
        SaveListKind.Dict => $"dict[{Children.Count}]",
        SaveListKind.Array => $"array[{Children.Count}]",
        SaveListKind.VecObject => $"vecobject[{Children.Count}]{(ItemClassName is { Length: > 0 } c ? $"<{c}>" : "")}",
        _ => $"{Kind.ToString().ToLowerInvariant()}[{Children.Count}]",
    };
}

/// <summary>dict 的键值对条目（序列化为 entries: [[k, v], ...]）。</summary>
public sealed class SavePairNode : SaveNode
{
    public SaveNode Key { get; init; } = null!;
    public SaveNode Value { get; init; } = null!;
    public override string TypeLabel => "entry";
}

/// <summary>序列化/反序列化错误（标量值非法等）——保存中止并提示。</summary>
public sealed class SaveNodeException : Exception
{
    public SaveNodeException(string message) : base(message) { }
}

/// <summary>LsoExpand.toTree JSON ↔ SaveNode 树双向转换（无损：只读类型保留 RawJson）。</summary>
public static class SaveTree
{
    // ── JSON → 节点 ────────────────────────────────────────────────

    public static SaveNode Build(JsonElement value, string name = "")
    {
        var node = BuildCore(value);
        node.Name = name;
        return node;
    }

    private static SaveNode BuildCore(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.Object)
        {
            if (v.TryGetProperty("__amf", out var amf))
            {
                switch (amf.GetString())
                {
                    case "object": return BuildObject(v);
                    case "array": return BuildArray(v);
                    case "vecint": return BuildVec(v, SaveListKind.VecInt);
                    case "vecuint": return BuildVec(v, SaveListKind.VecUInt);
                    case "vecdouble": return BuildVec(v, SaveListKind.VecDouble);
                    case "vecobject": return BuildVec(v, SaveListKind.VecObject);
                    case "dict": return BuildDict(v);
                    case "date": return BuildReadOnly(v, SaveScalarKind.Date);
                    case "xml": return BuildReadOnly(v, SaveScalarKind.Xml);
                    case "bytes": return BuildReadOnly(v, SaveScalarKind.Bytes);
                }
            }
            if (v.TryGetProperty("__n", out var n))
            {
                // toTree 的 sanitizeTree 把 NaN/±Infinity 转成字符串标记（"NaN"/"Infinity"/"-Infinity"）
                var dv = n.ValueKind == JsonValueKind.Number
                    ? n.GetDouble()
                    : double.Parse(n.GetString() ?? "NaN", CultureInfo.InvariantCulture);
                return new SaveScalarNode { Kind = SaveScalarKind.Double, ValueText = dv.ToString("R", CultureInfo.InvariantCulture) };
            }
            if (v.TryGetProperty("__i", out var i))
                return new SaveScalarNode { Kind = SaveScalarKind.Int, ValueText = i.GetInt64().ToString(CultureInfo.InvariantCulture) };
            // 未知对象：当只读原始 JSON 兜底
            return BuildReadOnly(v, SaveScalarKind.Xml);
        }
        return v.ValueKind switch
        {
            JsonValueKind.String => new SaveScalarNode
            {
                Kind = v.GetString() == "undefined" ? SaveScalarKind.Undefined : SaveScalarKind.String,
                ValueText = v.GetString() ?? "",
            },
            JsonValueKind.Number => new SaveScalarNode
            {
                Kind = SaveScalarKind.Double,
                ValueText = v.GetDouble().ToString("R", CultureInfo.InvariantCulture),
            },
            JsonValueKind.True or JsonValueKind.False => new SaveScalarNode { Kind = SaveScalarKind.Bool, BoolValue = v.GetBoolean() },
            JsonValueKind.Null => new SaveScalarNode { Kind = SaveScalarKind.Null },
            _ => new SaveScalarNode { Kind = SaveScalarKind.String, ValueText = v.ToString() },
        };
    }

    private static SaveNode BuildObject(JsonElement v)
    {
        var obj = new SaveObjectNode
        {
            ClassName = v.TryGetProperty("className", out var c) ? c.GetString() ?? "" : "",
            IsDynamic = v.TryGetProperty("isDynamic", out var isDyn) && isDyn.GetBoolean(),
        };
        var names = v.TryGetProperty("names", out var ns) ? ns.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : [];
        var values = v.TryGetProperty("values", out var vs) ? vs.EnumerateArray().ToArray() : [];
        for (var i = 0; i < Math.Min(names.Length, values.Length); i++)
        {
            var child = BuildCore(values[i]);
            child.Name = names[i];
            obj.SealedValues.Add(child);
        }
        if (v.TryGetProperty("dynamic", out var dyn))
        {
            foreach (var d in dyn.EnumerateArray())
            {
                var name = d.TryGetProperty("name", out var dn) ? dn.GetString() ?? "" : "";
                var val = d.TryGetProperty("value", out var dv) ? BuildCore(dv) : new SaveScalarNode { Kind = SaveScalarKind.Null };
                val.Name = name;
                val.IsAssoc = true;
                obj.DynamicValues.Add(val);
            }
        }
        FillChildren(obj, obj.SealedValues.Concat(obj.DynamicValues));
        return obj;
    }

    private static SaveNode BuildArray(JsonElement v)
    {
        var list = new SaveListNode { Kind = SaveListKind.Array };
        var idx = 0;
        if (v.TryGetProperty("dense", out var dense))
        {
            foreach (var e in dense.EnumerateArray())
            {
                var child = BuildCore(e);
                child.Name = "[" + idx + "]";
                list.Children.Add(child);
                idx++;
            }
        }
        if (v.TryGetProperty("assoc", out var assoc) && assoc.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in assoc.EnumerateObject())
            {
                var child = BuildCore(p.Value);
                child.Name = p.Name;
                child.IsAssoc = true;
                list.Children.Add(child);
            }
        }
        return list;
    }

    private static SaveNode BuildVec(JsonElement v, SaveListKind kind)
    {
        var list = new SaveListNode
        {
            Kind = kind,
            Fixed = v.TryGetProperty("fixed", out var f) && f.GetBoolean(),
            ItemClassName = v.TryGetProperty("className", out var c) ? c.GetString() : null,
        };
        if (v.TryGetProperty("values", out var values))
        {
            var i = 0;
            foreach (var e in values.EnumerateArray())
            {
                var child = BuildCore(e);
                // vecint/vecuint 的裸数字元素 → Int 标量（显示/编辑友好；序列化时还原裸数字）
                if ((kind == SaveListKind.VecInt || kind == SaveListKind.VecUInt)
                    && child is SaveScalarNode { Kind: SaveScalarKind.Double } sd)
                {
                    child = new SaveScalarNode { Kind = SaveScalarKind.Int, ValueText = sd.ValueText };
                }
                child.Name = "[" + i + "]";
                list.Children.Add(child);
                i++;
            }
        }
        return list;
    }

    private static SaveNode BuildDict(JsonElement v)
    {
        var list = new SaveListNode
        {
            Kind = SaveListKind.Dict,
            Weak = v.TryGetProperty("weak", out var w) && w.GetBoolean(),
        };
        if (v.TryGetProperty("entries", out var entries))
        {
            var i = 0;
            foreach (var e in entries.EnumerateArray())
            {
                var pair = new SavePairNode
                {
                    Name = "[" + i + "]",
                    Key = e.GetArrayLength() > 0 ? BuildCore(e[0]) : new SaveScalarNode { Kind = SaveScalarKind.Null },
                    Value = e.GetArrayLength() > 1 ? BuildCore(e[1]) : new SaveScalarNode { Kind = SaveScalarKind.Null },
                };
                pair.Key.Name = "key";
                pair.Value.Name = "value";
                pair.Children.Add(pair.Key);
                pair.Children.Add(pair.Value);
                list.Children.Add(pair);
                i++;
            }
        }
        return list;
    }

    private static SaveNode BuildReadOnly(JsonElement v, SaveScalarKind kind)
    {
        var s = new SaveScalarNode { Kind = kind, RawJson = v.Clone() };
        s.ValueText = kind switch
        {
            SaveScalarKind.Date when v.TryGetProperty("ms", out var ms) => ms.GetDouble().ToString("R", CultureInfo.InvariantCulture),
            SaveScalarKind.Xml when v.TryGetProperty("s", out var x) => x.GetString() ?? "",
            _ => "",
        };
        return s;
    }

    private static void FillChildren(SaveNode container, IEnumerable<SaveNode> children)
    {
        foreach (var c in children) container.Children.Add(c);
    }

    // ── 节点 → JSON ────────────────────────────────────────────────

    /// <summary>整棵树序列化为 toTree 结构 JSON（body 数组由调用方组装）。</summary>
    public static JsonNode? SerializeValue(SaveNode node) => node switch
    {
        SaveScalarNode s => SerializeScalar(s),
        SaveObjectNode o => SerializeObject(o),
        SaveListNode l => SerializeList(l),
        SavePairNode p => new JsonArray(SerializeValue(p.Key), SerializeValue(p.Value)),
        _ => JsonValue.Create((string?)null),
    };

    private static JsonNode? SerializeScalar(SaveScalarNode s)
    {
        switch (s.Kind)
        {
            case SaveScalarKind.String: return JsonValue.Create(s.ValueText);
            case SaveScalarKind.Bool: return JsonValue.Create(s.BoolValue);
            case SaveScalarKind.Null: return JsonValue.Create((string?)null);
            case SaveScalarKind.Undefined: return JsonValue.Create("undefined");
            case SaveScalarKind.Int:
                if (!long.TryParse(s.ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    throw new SaveNodeException($"「{s.Name}」不是有效整数: {s.ValueText}");
                return new JsonObject { ["__i"] = l };
            case SaveScalarKind.Double:
                if (!double.TryParse(s.ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                    throw new SaveNodeException($"「{s.Name}」不是有效数字: {s.ValueText}");
                // JSON 不能表达 NaN/±Infinity——保持 toTree 的字符串标记（encValue 的 setFloat64 会还原）
                if (double.IsNaN(dv)) return new JsonObject { ["__n"] = "NaN" };
                if (double.IsPositiveInfinity(dv)) return new JsonObject { ["__n"] = "Infinity" };
                if (double.IsNegativeInfinity(dv)) return new JsonObject { ["__n"] = "-Infinity" };
                return new JsonObject { ["__n"] = dv };
            case SaveScalarKind.Date:
            case SaveScalarKind.Xml:
            case SaveScalarKind.Bytes:
                if (s.RawJson is { } raw) return JsonNode.Parse(raw.GetRawText());
                return JsonValue.Create((string?)null);
            default: return JsonValue.Create((string?)null);
        }
    }

    private static JsonObject SerializeObject(SaveObjectNode o)
    {
        return new JsonObject
        {
            ["__amf"] = "object",
            ["className"] = o.ClassName,
            ["names"] = new JsonArray(o.SealedValues.Select(n => (JsonNode?)JsonValue.Create(n.Name)).ToArray()),
            ["values"] = new JsonArray(o.SealedValues.Select(SerializeValue).ToArray()),
            ["isDynamic"] = o.IsDynamic,
            ["dynamic"] = new JsonArray(o.DynamicValues.Select(d => (JsonNode?)new JsonObject
            {
                ["name"] = d.Name,
                ["value"] = SerializeValue(d),
            }).ToArray()),
        };
    }

    private static JsonObject SerializeList(SaveListNode l)
    {
        var values = l.Kind == SaveListKind.Dict
            ? l.Children.Select(c => SerializeValue(c))           // SavePairNode → [k, v]
            : l.Children.Select(c => SerializeValue(c));
        var valuesArray = new JsonArray(values.ToArray());
        switch (l.Kind)
        {
            case SaveListKind.Array:
                var dense = new JsonArray(l.Children.Where(c => !c.IsAssoc).Select(SerializeValue).ToArray());
                var assoc = new JsonObject();
                foreach (var c in l.Children.Where(c => c.IsAssoc))
                    assoc[c.Name] = SerializeValue(c);
                return new JsonObject { ["__amf"] = "array", ["dense"] = dense, ["assoc"] = assoc };
            case SaveListKind.VecInt:
                return new JsonObject { ["__amf"] = "vecint", ["fixed"] = l.Fixed, ["values"] = SerializeVecNumbers(l) };
            case SaveListKind.VecUInt:
                return new JsonObject { ["__amf"] = "vecuint", ["fixed"] = l.Fixed, ["values"] = SerializeVecNumbers(l) };
            case SaveListKind.VecDouble:
                return new JsonObject { ["__amf"] = "vecdouble", ["fixed"] = l.Fixed, ["values"] = SerializeVecNumbers(l) };
            case SaveListKind.VecObject:
                return new JsonObject
                {
                    ["__amf"] = "vecobject", ["fixed"] = l.Fixed,
                    ["className"] = l.ItemClassName ?? "",
                    ["values"] = valuesArray,
                };
            case SaveListKind.Dict:
                return new JsonObject { ["__amf"] = "dict", ["weak"] = l.Weak, ["entries"] = valuesArray };
            default: throw new SaveNodeException("未知容器类型");
        }
    }

    /// <summary>vec* 的 values 在 toTree 里是**裸数字**（非 {"__n"} 标记）——原样还原。</summary>
    private static JsonArray SerializeVecNumbers(SaveListNode l)
    {
        var arr = new JsonArray();
        foreach (var c in l.Children)
        {
            if (c is SaveScalarNode { Kind: SaveScalarKind.Int } si)
                arr.Add(long.Parse(si.ValueText, NumberStyles.Integer, CultureInfo.InvariantCulture));
            else if (c is SaveScalarNode { Kind: SaveScalarKind.Double } sd)
                arr.Add(double.Parse(sd.ValueText, NumberStyles.Float, CultureInfo.InvariantCulture));
            else
                arr.Add(SerializeValue(c));
        }
        return arr;
    }
}
