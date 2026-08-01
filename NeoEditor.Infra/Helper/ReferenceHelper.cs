using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NeoEditor.Helper;

/// <summary>
/// Legacy reference helper. All methods delegate to <see cref="ReferenceParser"/>.
/// New code should use ReferenceParser directly.
/// </summary>
public static class ReferenceHelper
{
    // Types have moved to ReferenceParser.cs. They remain accessible as ParsedRef / TargetKeyInfo
    // in the same namespace without qualification.

    /// <summary>Parse a TargetKey pattern like "{GroupId}.{SubgroupId}" → TargetKeyInfo.</summary>
    [Obsolete("Use ReferenceParser.ParseTargetKey instead.")]
    public static TargetKeyInfo ParseTargetKey(string? targetKey)
        => ReferenceParser.ParseTargetKey(targetKey);

    /// <summary>Decompose a raw ID value using the target key info into property-name→value pairs.</summary>
    [Obsolete("Use ReferenceParser.DecomposeId instead.")]
    public static Dictionary<string, int> DecomposeId(string rawId, TargetKeyInfo keyInfo)
        => ReferenceParser.DecomposeId(rawId, keyInfo);

    /// <summary>Extract the raw ID string from a segment using the parse pattern.</summary>
    [Obsolete("Use ReferenceParser.ExtractRawId instead.")]
    public static string ExtractRawId(string segment, string? pattern)
        => ReferenceParser.ExtractRawId(segment, pattern);

    /// <summary>Parse "NSE:42" → ("NSE", 42), "152" → ("", 152).</summary>
    [Obsolete("Use ReferenceParser.ParseReference instead.")]
    public static (string ModName, int Id) ParseReference(string raw)
        => ReferenceParser.ParseReference(raw);

    /// <summary>Parse comma-separated ID list: "10,11,NSE:12" → list of ParsedRef.</summary>
    [Obsolete("Use ReferenceParser.ParseCommaList instead.")]
    public static List<ParsedRef> ParseCommaList(string raw)
        => ReferenceParser.ParseCommaList(raw);

    /// <summary>Parse multiplier format: "211x1.0,NSE:42x1" → list of ParsedRef.</summary>
    [Obsolete("Use ReferenceParser.ParseMultiplierList instead.")]
    public static List<ParsedRef> ParseMultiplierList(string raw)
        => ReferenceParser.ParseMultiplierList(raw);

    /// <summary>Parse assignment format: "38=1,50=0.5" → list of ParsedRef.</summary>
    [Obsolete("Use ReferenceParser.ParseAssignmentList instead.")]
    public static List<ParsedRef> ParseAssignmentList(string raw)
        => ReferenceParser.ParseAssignmentList(raw);

    /// <summary>Parse a single reference with optional multiplier suffix.</summary>
    [Obsolete("Use ReferenceParser.ParseSingle instead.")]
    public static ParsedRef ParseSingle(string raw)
        => ReferenceParser.ParseSingle(raw);

    /// <summary>Parse multi-value field according to separator and pattern.</summary>
    [Obsolete("Use ReferenceParser.ParseMultiValue instead.")]
    public static List<ParsedRef> ParseMultiValue(string raw, string? separator, string? pattern)
        => ReferenceParser.ParseMultiValue(raw, separator, pattern);

    /// <summary>Parse a single reference segment using the given pattern.</summary>
    [Obsolete("Use ReferenceParser.ParseWithPattern instead.")]
    public static ParsedRef ParseWithPattern(string raw, string? pattern)
        => ReferenceParser.ParseWithPattern(raw, pattern);

    /// <summary>Is this a default (game base) namespace?</summary>
    [Obsolete("Use ReferenceParser.IsDefaultNamespace instead.")]
    public static bool IsDefaultNamespace(string modName)
        => ReferenceParser.IsDefaultNamespace(modName);

    /// <summary>Format for display: strip "0:" prefix, keep other namespaces.</summary>
    [Obsolete("Use ReferenceParser.FormatForDisplay instead.")]
    public static string FormatForDisplay(string raw)
        => ReferenceParser.FormatForDisplay(raw);

    /// <summary>Format a single parsed reference for display in context menu.</summary>
    [Obsolete("Use ReferenceParser.FormatParsed instead.")]
    public static string FormatParsed(ParsedRef r)
        => ReferenceParser.FormatParsed(r);
}
