using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;

namespace NeoEditor.Helper;

/// <summary>
/// Serializes and deserializes reference field values.
/// Each segment is parsed into a concrete <see cref="IReferenceFormat"/> class
/// that corresponds directly to the pattern on <see cref="ReferenceFieldAttribute"/>.
///
/// Pattern → Format mapping:
///   {id}           plain       → EntityRef or PureRefFormat
///   {id}           negated "-" → NegatedRefFormat
///   {id}x{mult}                → IdXMultFormat
///   {mult}x{id}                → MultXIdFormat
///   {id}={value}               → AssignFormat (ValueFirst=false)
///   {value}={id}               → AssignFormat (ValueFirst=true)
///   [{id}                      → BracketFormat
///   {mult}x{id} + "+" sep      → MultiIngredientRecipeFormat (Recipe style)
///   composite TargetKey        → EntityRef { GroupId, SubgroupId }
/// </summary>
public class ReferenceListSerializer : IReferenceListSerializer
{
    /// <inheritdoc/>
    public ReferenceList<IReferenceEntry> Deserialize(string raw, ReferenceFieldAttribute metadata)
    {
        var result = new ReferenceList<IReferenceEntry> { RawText = raw ?? "" };

        if (string.IsNullOrWhiteSpace(raw))
            return result;

        var pattern = metadata.Pattern ?? "{id}";
        var keyInfo = ReferenceParser.ParseTargetKey(metadata.TargetKey);
        var separator = metadata.Separator;
        var orSeparator = metadata.OrSeparator;

        var parts = separator is not null
            ? raw.Split(separator)
            : [raw];

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;

            var entry = DeserializeTopLevelPart(trimmed, pattern, orSeparator, keyInfo);
            result.Add(entry);
        }

        // R30 (M1): Add() invalidates RawText — restore the source text so consumers
        // (Split / implicit string conversion) read the authoritative raw value.
        result.RawText = raw;
        return result;
    }

    /// <summary>
    /// Deserialize one top-level segment. When the column declares an OR separator and this
    /// segment contains it, split further into an <see cref="OrGroupFormat"/> of alternatives.
    /// </summary>
    private static IReferenceEntry DeserializeTopLevelPart(
        string part, string pattern, string? orSeparator, TargetKeyInfo keyInfo)
    {
        if (orSeparator is not null && part.Contains(orSeparator))
        {
            var leaves = part.Split(orSeparator)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .Select(l => DeserializeSegment(l, pattern, keyInfo))
                .ToList();

            if (leaves.Count > 1)
                return new OrGroupFormat { Alternatives = leaves };
            if (leaves.Count == 1)
                return leaves[0];
        }

        return DeserializeSegment(part, pattern, keyInfo);
    }

    /// <inheritdoc/>
    public string Serialize(ReferenceList<IReferenceEntry> list, ReferenceFieldAttribute metadata)
    {
        if (list.Count == 0)
        {
            list.RawText = "";
            return "";
        }

        var rawStrings = list.Select(e => e.ToRawString());
        var separator = metadata.Separator;
        var result = separator is not null
            ? string.Join(separator, rawStrings)
            : rawStrings.First();

        list.RawText = result;
        return result;
    }

    // ── Segment deserialization ──────────────────────────────────────────

    private static IReferenceEntry DeserializeSegment(string segment, string pattern, TargetKeyInfo keyInfo)
    {
        return pattern switch
        {
            "{id}x{mult}" => DeserializeIdXMult(segment, keyInfo),
            "{mult}x{id}" => DeserializeMultXId(segment, keyInfo),
            "{id}x{mult}x{qty}" => DeserializeIdXMultXQty(segment, keyInfo),
            "{id}={value}" => DeserializeAssign(segment, keyInfo, valueFirst: false),
            "{value}={id}" => DeserializeAssign(segment, keyInfo, valueFirst: true),
            "[{id}" => DeserializeBracket(segment, keyInfo),
            _ => DeserializeId(segment, keyInfo)
        };
    }

    // ── Pattern-specific deserializers ────────────────────────────────────

    private static IReferenceEntry DeserializeId(string segment, TargetKeyInfo keyInfo)
    {
        // Check for negation
        var trimmed = segment.Trim();
        if (trimmed.StartsWith('-'))
        {
            var innerSeg = trimmed[1..].Trim();
            return new NegatedRefFormat
            {
                Inner = BuildEntityRef(innerSeg, keyInfo)
            };
        }

        return new PureRefFormat
        {
            Entity = BuildEntityRef(segment, keyInfo)
        };
    }

    private static IReferenceEntry DeserializeIdXMult(string segment, TargetKeyInfo keyInfo)
    {
        var trimmed = segment.Trim();
        var isNeg = trimmed.StartsWith('-');
        var body = isNeg ? trimmed[1..].Trim() : trimmed;
        var xIdx = body.LastIndexOf('x');

        var idPart = xIdx > 0 ? body[..xIdx].Trim() : body;
        var multStr = xIdx > 0 && xIdx < body.Length - 1 ? body[(xIdx + 1)..].Trim() : "1.0";
        double.TryParse(multStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var mult);

        var entity = BuildEntityRef(idPart, keyInfo);
        // R30 (M2): keep the original multiplier text ("1.0" vs "1") for lossless round-trip.
        var fmt = new IdXMultFormat { Entity = entity, Multiplier = mult, RawMult = multStr };

        // If negated, wrap the whole IdXMultFormat in NegatedRefFormat
        return isNeg
            ? new NegatedRefFormat { Inner = fmt }
            : fmt;
    }

    private static IReferenceEntry DeserializeIdXMultXQty(string segment, TargetKeyInfo keyInfo)
    {
        var trimmed = segment.Trim();
        var isNeg = trimmed.StartsWith('-');
        var body = isNeg ? trimmed[1..].Trim() : trimmed;

        // id is everything before the FIRST 'x'; the remainder is "{prob}" or "{prob}x{qty}".
        var xIdx = body.IndexOf('x');
        var idPart = xIdx > 0 ? body[..xIdx].Trim() : body;
        var rest = xIdx > 0 && xIdx < body.Length - 1 ? body[(xIdx + 1)..].Trim() : "";

        string prob;
        string? qty;
        if (rest.Length == 0)
        {
            prob = "1.0";
            qty = null;
        }
        else
        {
            var qx = rest.IndexOf('x');
            if (qx > 0)
            {
                prob = rest[..qx].Trim();
                qty = rest[(qx + 1)..].Trim();
            }
            else
            {
                prob = rest;
                qty = null;
            }
        }

        var entity = BuildEntityRef(idPart, keyInfo);
        var fmt = new IdXMultXQtyFormat { Entity = entity, Prob = prob, Qty = qty };
        return isNeg ? new NegatedRefFormat { Inner = fmt } : fmt;
    }

    private static IReferenceEntry DeserializeMultXId(string segment, TargetKeyInfo keyInfo)
    {
        var trimmed = segment.Trim();
        var xIdx = trimmed.LastIndexOf('x');

        var multStr = xIdx > 0 ? trimmed[..xIdx].Trim() : "1.0";
        var idPart = xIdx > 0 ? trimmed[(xIdx + 1)..].Trim() : trimmed;
        double.TryParse(multStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var mult);

        // R30 (M2): keep the original multiplier text ("2.0" vs "2") for lossless round-trip.
        return new MultXIdFormat
        {
            Entity = BuildEntityRef(idPart, keyInfo),
            Multiplier = mult,
            RawMult = multStr
        };
    }

    private static IReferenceEntry DeserializeAssign(
        string segment, TargetKeyInfo keyInfo, bool valueFirst)
    {
        var trimmed = segment.Trim();
        var eqIdx = trimmed.IndexOf('=');
        if (eqIdx <= 0)
            return DeserializeId(segment, keyInfo);

        var left = trimmed[..eqIdx].Trim();
        var right = trimmed[(eqIdx + 1)..].Trim();

        var idPart = valueFirst ? right : left;
        var valPart = valueFirst ? left : right;

        // Non-numeric value (e.g. aSwitchIDs free-text state name "Hood Off") → keep verbatim.
        if (!double.TryParse(valPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            return new AssignFormat
            {
                Entity = BuildEntityRef(idPart, keyInfo),
                Value = 0,
                RawValue = valPart,
                ValueFirst = valueFirst
            };
        }

        return new AssignFormat
        {
            Entity = BuildEntityRef(idPart, keyInfo),
            Value = val,
            ValueFirst = valueFirst
        };
    }

    private static IReferenceEntry DeserializeBracket(string segment, TargetKeyInfo keyInfo)
    {
        var trimmed = segment.Trim();
        var start = trimmed.StartsWith('[') ? trimmed[1..] : trimmed;
        var body = start.EndsWith(']') ? start[..^1] : start;
        var parts = body.Split(',');

        var idStr = parts[0].Trim();
        string? p1 = parts.Length > 1 ? parts[1].Trim() : null;
        string? p2 = parts.Length > 2 ? parts[2].Trim() : null;

        return new BracketFormat
        {
            Entity = BuildEntityRef(idStr, keyInfo),
            P1 = p1,
            P2 = p2,
            RawSegment = trimmed
        };
    }

    // ── EntityRef builder ─────────────────────────────────────────────────

    private static EntityRef BuildEntityRef(string extractedId, TargetKeyInfo keyInfo)
    {
        var (modName, _) = ReferenceParser.ParseReference(extractedId);
        var hasNamespace = !string.IsNullOrEmpty(modName);
        var ns = hasNamespace ? modName : null;

        // Strip namespace prefix to get the bare key
        var bareKey = hasNamespace
            ? extractedId[(modName.Length + 1)..]
            : extractedId;

        // Composite key: TargetKey is composite and bareKey contains the separator
        if (keyInfo.IsComposite && bareKey.Contains(keyInfo.KeySeparator))
        {
            var keyValues = ReferenceParser.DecomposeId(bareKey, keyInfo);
            keyValues.TryGetValue(keyInfo.KeyNames[0], out var gid);
            keyValues.TryGetValue(keyInfo.KeyNames[1], out var sid);
            return new EntityRef { Namespace = ns, GroupId = gid, SubgroupId = sid };
        }

        return new EntityRef { Namespace = ns, Id = bareKey };
    }
}
