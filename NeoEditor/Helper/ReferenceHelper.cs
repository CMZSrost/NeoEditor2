using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NeoEditor.Helper;

public static class ReferenceHelper
{
    public record ParsedRef(string ModName, int Id, double Multiplier = 1.0);

    /// <summary>Describes how to decompose a raw ID value into lookup keys on the target entity.</summary>
    public record TargetKeyInfo(string[] KeyNames, string KeySeparator)
    {
        public bool IsComposite => KeyNames.Length > 1;
    }

    /// <summary>Parse a TargetKey pattern like "{GroupId}.{SubgroupId}" → TargetKeyInfo.</summary>
    public static TargetKeyInfo ParseTargetKey(string? targetKey)
    {
        if (string.IsNullOrEmpty(targetKey))
            return new TargetKeyInfo(["Id"], "");

        var keyNames = new List<string>();
        var sepChars = new List<char>();
        var current = new System.Text.StringBuilder();
        var inBrace = false;

        foreach (var ch in targetKey)
        {
            if (ch == '{') { inBrace = true; current.Clear(); }
            else if (ch == '}')
            {
                inBrace = false;
                keyNames.Add(current.ToString());
            }
            else if (inBrace) { current.Append(ch); }
            else { sepChars.Add(ch); }
        }

        return new TargetKeyInfo(keyNames.ToArray(), new string(sepChars.ToArray()));
    }

    /// <summary>Decompose a raw ID value using the target key info into property-name→value pairs.</summary>
    public static Dictionary<string, int> DecomposeId(string rawId, TargetKeyInfo keyInfo)
    {
        var result = new Dictionary<string, int>();

        // Strip namespace prefix: "NSE:86.6" → "86.6"
        var colonIdx = rawId.IndexOf(':');
        var idOnly = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;

        if (!keyInfo.IsComposite)
        {
            if (int.TryParse(idOnly, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                result[keyInfo.KeyNames[0]] = id;
            else
                result[keyInfo.KeyNames[0]] = 0;
            return result;
        }

        // If the value doesn't contain the separator (e.g. "418" for GroupId.SubgroupId key),
        // fall back to using "Id" as the lookup key (the entity's default primary key).
        if (!idOnly.Contains(keyInfo.KeySeparator))
        {
            if (int.TryParse(idOnly, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
                result["Id"] = val;
            return result;
        }

        var parts = idOnly.Split(new[] { keyInfo.KeySeparator }, StringSplitOptions.None);
        for (var i = 0; i < keyInfo.KeyNames.Length && i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var val))
                result[keyInfo.KeyNames[i]] = val;
            else
                result[keyInfo.KeyNames[i]] = 0;
        }
        return result;
    }

    /// <summary>Extract the raw ID string from a segment using the parse pattern.</summary>
    public static string ExtractRawId(string segment, string? pattern)
    {
        return ReferencePattern.FromName(pattern).ExtractRawId(segment);
    }

    /// <summary>Parse "NSE:42" → ("NSE", 42), "152" → ("", 152)</summary>
    public static (string ModName, int Id) ParseReference(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("", 0);

        var colonIndex = raw.IndexOf(':');
        if (colonIndex < 0)
        {
            return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? ("", id)
                : ("", 0);
        }

        var modName = raw[..colonIndex].Trim();
        var idPart = raw[(colonIndex + 1)..].Trim();
        return int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId)
            ? (modName, parsedId)
            : (modName, 0);
    }

    /// <summary>Parse comma-separated ID list: "10,11,NSE:12" → list of (ModName, Id)</summary>
    public static List<ParsedRef> ParseCommaList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split(',')
            .Select(ParseSingle)
            .Where(r => r.Id > 0 || !string.IsNullOrEmpty(r.ModName))
            .ToList();
    }

    /// <summary>Parse multiplier format: "211x1.0,NSE:42x1" → list of (ModName, Id, Multiplier)</summary>
    public static List<ParsedRef> ParseMultiplierList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var result = new List<ParsedRef>();
        foreach (var part in raw.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            var xIdx = trimmed.LastIndexOf('x');
            var idPart = xIdx > 0 ? trimmed[..xIdx] : trimmed;
            var multPart = xIdx > 0 && xIdx < trimmed.Length - 1 ? trimmed[(xIdx + 1)..] : "1.0";
            var (modName, id) = ParseReference(idPart);
            double.TryParse(multPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var mult);
            result.Add(new ParsedRef(modName, id, mult));
        }
        return result;
    }

    /// <summary>Parse assignment format: "38=1,50=0.5" → list of (ModName, Id, Value as Multiplier)</summary>
    public static List<ParsedRef> ParseAssignmentList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var result = new List<ParsedRef>();
        foreach (var part in raw.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            var eqIdx = trimmed.IndexOf('=');
            var idPart = eqIdx > 0 ? trimmed[..eqIdx] : trimmed;
            var valPart = eqIdx > 0 && eqIdx < trimmed.Length - 1 ? trimmed[(eqIdx + 1)..] : "1.0";
            var (modName, id) = ParseReference(idPart);
            double.TryParse(valPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var val);
            result.Add(new ParsedRef(modName, id, val));
        }
        return result;
    }

    /// <summary>Parse a single reference with optional multiplier suffix</summary>
    public static ParsedRef ParseSingle(string raw)
    {
        var trimmed = raw.Trim();
        // Use IndexOf (first 'x') so segments like "582x.01x1" correctly extract "582" as the ID
        var xIdx = trimmed.IndexOf('x');
        if (xIdx > 0 && xIdx < trimmed.Length - 1)
        {
            var idPart = trimmed[..xIdx];
            var multPart = trimmed[(xIdx + 1)..];
            var (modName, id) = ParseReference(idPart);
            double.TryParse(multPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var mult);
            return new ParsedRef(modName, id, mult);
        }
        else
        {
            var (modName, id) = ParseReference(trimmed);
            return new ParsedRef(modName, id);
        }
    }

    /// <summary>Parse multi-value field according to separator and pattern.</summary>
    public static List<ParsedRef> ParseMultiValue(string raw, string? separator, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        if (separator is null) return [];
        pattern ??= "{id}";

        return raw.Split(separator)
            .Select(s => ParseWithPattern(s.Trim(), pattern))
            .Where(r => r.Id != 0 || !string.IsNullOrEmpty(r.ModName))
            .ToList();
    }

    /// <summary>Parse a single reference segment using the given pattern.</summary>
    public static ParsedRef ParseWithPattern(string raw, string? pattern)
    {
        pattern ??= "{id}";

        return pattern switch
        {
            "{id}x{mult}" => ParseSingle(raw),    // handles "211x1.0" or "NSE:42x1.0"
            "{mult}x{id}" => ParseMultiplierReversed(raw), // handles "1x2" (qty x ingredientId)
            "{id}={value}" => ParseAssignment(raw),
            _ => ParseSingle(raw)                 // "{id}" or any unrecognized pattern
        };
    }

    private static ParsedRef ParseAssignment(string raw)
    {
        var trimmed = raw.Trim();
        var eqIdx = trimmed.IndexOf('=');
        if (eqIdx <= 0) return ParseSingle(trimmed);
        var idPart = trimmed[..eqIdx];
        var valPart = trimmed[(eqIdx + 1)..];
        var (modName, id) = ParseReference(idPart);
        double.TryParse(valPart, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var val);
        return new ParsedRef(modName, id, val);
    }

    /// <summary>Parse "1x2" (multiplier before x, id after) — used by Recipe strTools/strConsumed.</summary>
    private static ParsedRef ParseMultiplierReversed(string raw)
    {
        var trimmed = raw.Trim();
        var lastX = trimmed.LastIndexOf('x');
        if (lastX <= 0) return ParseSingle(trimmed);
        var idPart = trimmed[(lastX + 1)..].Trim();
        return ParseSingle(idPart);
    }

    /// <summary>Is this a default (game base) namespace?</summary>
    public static bool IsDefaultNamespace(string modName) => modName is "" or "0";

    /// <summary>
    /// Format for display: strip "0:" prefix, keep other namespaces.
    /// "0:152" → "152", "NSE:42" → "NSE:42"
    /// </summary>
    public static string FormatForDisplay(string raw)
    {
        var (modName, id) = ParseReference(raw);
        return IsDefaultNamespace(modName) ? id.ToString(CultureInfo.InvariantCulture) : raw;
    }

    /// <summary>Format a single parsed reference for display in context menu.</summary>
    public static string FormatParsed(ParsedRef r)
    {
        var idStr = IsDefaultNamespace(r.ModName) ? r.Id.ToString() : $"{r.ModName}:{r.Id}";
        return Math.Abs(r.Multiplier - 1.0) > 0.001 ? $"{idStr} (x{r.Multiplier})" : idStr;
    }
}
