using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NeoEditor.Helper;

// ═══════════════════════════════════════════════════════════════════════════
//  Data Types
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Single parsed reference with optional multiplier.</summary>
public record ParsedRef(string ModName, int Id, double Multiplier = 1.0);

/// <summary>Describes how to decompose a raw ID value into lookup keys on the target entity.</summary>
public record TargetKeyInfo(string[] KeyNames, string KeySeparator)
{
    public bool IsComposite => KeyNames.Length > 1;
}

/// <summary>Parsed result for a single segment of a reference field.</summary>
public record ResolvedRefSegment
{
    /// <summary>Original text segment, e.g. "NSE:211x1.0"</summary>
    public required string RawText { get; init; }

    /// <summary>Pure ID stripped of format info (multiplier, assignment, brackets), e.g. "NSE:211"</summary>
    public required string ExtractedId { get; init; }

    /// <summary>Namespace prefix. "NSE", "" (default), "0" (treated as default)</summary>
    public string? Namespace { get; init; }

    /// <summary>Numeric ID without namespace prefix.</summary>
    public int NumericId { get; init; }

    /// <summary>Extra display info (multiplier, assigned value, etc.)</summary>
    public string? ExtraInfo { get; init; }

    /// <summary>Decomposed key-value pairs for matching. Simple ref → {"Id":211}, composite → {"GroupId":86, "SubgroupId":6}</summary>
    public Dictionary<string, int> KeyValues { get; init; } = new();
}

/// <summary>Full parsed result for a reference field value.</summary>
public record ParsedReferenceField
{
    /// <summary>Parsed segments. Single-value fields produce exactly one element.</summary>
    public required IReadOnlyList<ResolvedRefSegment> Segments { get; init; }

    /// <summary>The [ReferenceField] attribute metadata from the property.</summary>
    public required ReferenceFieldAttribute Metadata { get; init; }

    /// <summary>Original unparsed field value.</summary>
    public required string RawValue { get; init; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  ReferenceParser — pure functions, zero external dependencies
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Pure-function reference field parser. No state, no DI, no store references.
/// All methods are deterministic string → structured-data transformations.
/// </summary>
public static class ReferenceParser
{
    // ── TargetKey parsing ───────────────────────────────────────────────────

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

    /// <summary>
    /// Decompose a raw ID value using the target key info into property-name→value pairs.
    /// Handles fallback: if value doesn't contain the composite separator, falls back to {"Id": value}.
    /// </summary>
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
        // fall back to using "Id" as the lookup key.
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

    // ── ID extraction ───────────────────────────────────────────────────────

    /// <summary>Extract the raw ID string from a segment using the parse pattern.</summary>
    public static string ExtractRawId(string segment, string? pattern)
    {
        return ReferencePattern.FromName(pattern).ExtractRawId(segment);
    }

    // ── Reference parsing ───────────────────────────────────────────────────

    /// <summary>Parse "NSE:42" → ("NSE", 42), "152" → ("", 152).</summary>
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

    // ── Single reference parsing ────────────────────────────────────────────

    /// <summary>Parse a single reference with optional multiplier suffix.</summary>
    public static ParsedRef ParseSingle(string raw)
    {
        var trimmed = raw.Trim();
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

    /// <summary>Parse "1x2" (multiplier before x, id after) — used by Recipe strTools/strConsumed.</summary>
    private static ParsedRef ParseMultiplierReversed(string raw)
    {
        var trimmed = raw.Trim();
        var lastX = trimmed.LastIndexOf('x');
        if (lastX <= 0) return ParseSingle(trimmed);
        var idPart = trimmed[(lastX + 1)..].Trim();
        var multPart = trimmed[..lastX].Trim();
        var (modName, id) = ParseReference(idPart);
        // R30: keep the multiplier ("2x15" → mult=2, id=15) — was silently dropped as 1.0.
        double.TryParse(multPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var mult);
        return new ParsedRef(modName, id, mult);
    }

    /// <summary>Parse a bracket segment "[155,0,0]" / "[-137,0,0]" / "NSE:[155,0,0]" — id + P1/P2 params.</summary>
    private static ParsedRef ParseBracket(string raw)
    {
        var trimmed = raw.Trim();
        // Extract the id (handles "NSE:[155,0,0]" → NSE:155).
        var idPart = ReferencePattern.FromName("[{id}").ExtractRawId(trimmed);
        var (modName, id) = ParseReference(idPart);
        return new ParsedRef(modName, id);
    }

    private static ParsedRef ParseAssignment(string raw)
    {
        var trimmed = raw.Trim();
        var eqIdx = trimmed.IndexOf('=');
        if (eqIdx <= 0) return ParseSingle(trimmed);
        var idPart = trimmed[..eqIdx];
        var valPart = trimmed[(eqIdx + 1)..];
        var (modName, id) = ParseReference(idPart);
        double.TryParse(valPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var val);
        return new ParsedRef(modName, id, val);
    }

    /// <summary>Parse a single reference segment using the given pattern.</summary>
    public static ParsedRef ParseWithPattern(string raw, string? pattern)
    {
        pattern ??= "{id}";
        return pattern switch
        {
            "{id}x{mult}" => ParseSingle(raw),
            "{mult}x{id}" => ParseMultiplierReversed(raw),
            "{id}={value}" => ParseAssignment(raw),
            "[{id}" => ParseBracket(raw),
            _ => ParseSingle(raw)
        };
    }

    // ── List parsing ────────────────────────────────────────────────────────

    /// <summary>Parse comma-separated ID list: "10,11,NSE:12" → list of ParsedRef.</summary>
    public static List<ParsedRef> ParseCommaList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return raw.Split(',')
            .Select(ParseSingle)
            .Where(r => r.Id > 0 || !string.IsNullOrEmpty(r.ModName))
            .ToList();
    }

    /// <summary>Parse multiplier format: "211x1.0,NSE:42x1" → list of ParsedRef.</summary>
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

    /// <summary>Parse assignment format: "38=1,50=0.5" → list of ParsedRef.</summary>
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

    // ── Namespace helpers ───────────────────────────────────────────────────

    /// <summary>Is this a default (game base) namespace?</summary>
    public static bool IsDefaultNamespace(string? modName) => modName is "" or "0" or null;

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

    /// <summary>Normalize namespace: "" and "0" both mean game base.</summary>
    public static string NormalizeNamespace(string? ns)
        => IsDefaultNamespace(ns) ? "" : (ns ?? "");

    /// <summary>Build the canonical lookup key: "{namespace}:{extractedId}", with default namespace stripped.
    /// Also strips "=value" suffix (e.g., "38=1" → "38") for IdEqualsValue patterns.</summary>
    public static string BuildLookupKey(string extractedId)
    {
        // Strip "=value" suffix first (handles IdEqualsValue pattern)
        var eqIdx = extractedId.IndexOf('=');
        if (eqIdx > 0)
            extractedId = extractedId[..eqIdx].Trim();

        var colonIdx = extractedId.IndexOf(':');
        if (colonIdx <= 0) return extractedId; // no namespace prefix

        var ns = extractedId[..colonIdx];
        if (IsDefaultNamespace(ns))
            return extractedId[(colonIdx + 1)..]; // strip "0:" or ":"
        return extractedId;
    }

    // ── High-level parse entry points ───────────────────────────────────────

    /// <summary>
    /// Split a field value into leaf segments, flattening OR groups.
    /// Top-level split by <see cref="ReferenceFieldAttribute.Separator"/>, then any segment
    /// containing <see cref="ReferenceFieldAttribute.OrSeparator"/> is split further.
    /// </summary>
    private static IEnumerable<string> SplitSegments(string value, ReferenceFieldAttribute attr)
    {
        var parts = attr.Separator is not null
            ? value.Split(attr.Separator)
            : [value];

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;

            if (attr.OrSeparator is not null && trimmed.Contains(attr.OrSeparator))
            {
                foreach (var leaf in trimmed.Split(attr.OrSeparator))
                {
                    var lt = leaf.Trim();
                    if (lt.Length > 0) yield return lt;
                }
            }
            else
            {
                yield return trimmed;
            }
        }
    }

    /// <summary>
    /// Parse a complete reference field value using its [ReferenceField] attribute metadata.
    /// Returns structured segments with extracted IDs, key values, and extra info.
    /// </summary>
    public static ParsedReferenceField Parse(string value, ReferenceFieldAttribute attr)
    {
        var segments = new List<ResolvedRefSegment>();
        if (string.IsNullOrWhiteSpace(value))
            return new ParsedReferenceField { Segments = segments, Metadata = attr, RawValue = value ?? "" };

        var pattern = attr.Pattern ?? "{id}";
        var refPattern = ReferencePattern.FromName(pattern);
        var keyInfo = ParseTargetKey(attr.TargetKey);

        foreach (var trimmed in SplitSegments(value, attr))
        {
            var extractedId = refPattern.ExtractRawId(trimmed);
            var (modName, numericId) = ParseReference(extractedId);
            var extraInfo = refPattern.FormatExtraInfo(trimmed);
            var keyValues = DecomposeId(extractedId, keyInfo);

            segments.Add(new ResolvedRefSegment
            {
                RawText = trimmed,
                ExtractedId = extractedId,
                Namespace = modName.Length > 0 ? modName : null,
                NumericId = numericId,
                ExtraInfo = extraInfo.Length > 0 ? extraInfo : null,
                KeyValues = keyValues
            });
        }

        return new ParsedReferenceField { Segments = segments, Metadata = attr, RawValue = value };
    }

    /// <summary>
    /// Fast-path: extract only (ExtractedId, KeyValues) pairs for index building.
    /// Skips display formatting overhead.
    /// </summary>
    public static List<(string ExtractedId, Dictionary<string, int> KeyValues)> ExtractIds(
        string value, ReferenceFieldAttribute attr)
    {
        var results = new List<(string, Dictionary<string, int>)>();
        if (string.IsNullOrWhiteSpace(value)) return results;

        var pattern = attr.Pattern ?? "{id}";
        var refPattern = ReferencePattern.FromName(pattern);
        var keyInfo = ParseTargetKey(attr.TargetKey);

        foreach (var trimmed in SplitSegments(value, attr))
        {
            var extractedId = refPattern.ExtractRawId(trimmed);
            var keyValues = DecomposeId(extractedId, keyInfo);
            results.Add((extractedId, keyValues));
        }

        return results;
    }
}
