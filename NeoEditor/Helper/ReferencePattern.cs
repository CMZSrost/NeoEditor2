using System;
using NeoEditor.Helper;

namespace NeoEditor.Helper;

/// <summary>
/// Strategy for parsing and formatting reference field patterns.
/// Adding a new pattern now only requires a new subclass — no changes to
/// ExtractRawId, FormatSegmentDisplay, or FormatExtraInfo scattered across files.
/// </summary>
public abstract class ReferencePattern
{
    public string Name { get; }

    protected ReferencePattern(string name) { Name = name; }
    /// <summary>Extract the raw ID from a reference segment (e.g. "NSEb:7x1" → "NSEb:7").</summary>
    public abstract string ExtractRawId(string segment);

    /// <summary>Format a resolved segment for DataGrid display. Default: "Subject (rawId)".</summary>
    public virtual string FormatDisplay(string segment, string? subject, string? modName)
    {
        if (string.IsNullOrEmpty(subject)) return segment;
        var modPrefix = !string.IsNullOrEmpty(modName) && modName != "0" ? modName + ":" : "";
        return $"{modPrefix}{subject} ({ExtractRawId(segment)})";
    }

    /// <summary>Format extra info for Overview tree display (e.g. "x50%", "= 100").</summary>
    public virtual string FormatExtraInfo(string segment) => "";

    // ── Pattern singletons ─────────────────────────────────────────────────

    public static readonly ReferencePattern Id = new IdPattern();
    public static readonly ReferencePattern IdXMult = new IdXMultPattern();
    public static readonly ReferencePattern MultXId = new MultXIdPattern();
    public static readonly ReferencePattern IdEqualsValue = new IdEqualsValuePattern();
    public static readonly ReferencePattern BracketId = new BracketIdPattern();

    /// <summary>Resolve a pattern from a pattern name string.</summary>
    public static ReferencePattern FromName(string? name) => name switch
    {
        "{id}x{mult}" => IdXMult,
        "{mult}x{id}" => MultXId,
        "{id}={value}" => IdEqualsValue,
        "[{id}" => BracketId,
        _ => Id
    };

    /// <summary>Resolve a pattern from a ReferenceFieldAttribute.</summary>
    public static ReferencePattern FromAttribute(ReferenceFieldAttribute attr) => FromName(attr.Pattern);

    // ── Default implementation helpers ─────────────────────────────────────

    protected static double? TryParseDouble(string s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    protected static string FmtPct(string s)
    {
        var d = TryParseDouble(s);
        return d is > 0 and < 1 ? $"{d * 100:F1}%" : s;
    }

    // ── Concrete patterns (private nested) ──────────────────────────────────

    private sealed class IdPattern : ReferencePattern
    {
        public IdPattern() : base("Id") { }
        public override string ExtractRawId(string segment) => segment.Trim();
    }

    private sealed class IdXMultPattern : ReferencePattern
    {
        public IdXMultPattern() : base("IdXMult") { }
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            var xIdx = trimmed.IndexOf('x');
            return xIdx > 0 ? trimmed[..xIdx].Trim() : trimmed;
        }
        public override string FormatDisplay(string segment, string? subject, string? modName)
        {
            if (string.IsNullOrEmpty(subject)) return segment;
            var isNeg = segment.TrimStart().StartsWith('-');
            var negPrefix = isNeg ? "~" : "";
            var modPrefix = !string.IsNullOrEmpty(modName) && modName != "0" ? modName + ":" : "";
            var xIdx = segment.LastIndexOf('x');
            var suffix = xIdx > 0 ? segment[xIdx..] : "";
            return $"{negPrefix}{modPrefix}{subject}{suffix}";
        }
        public override string FormatExtraInfo(string segment)
        {
            var xIdx = segment.IndexOf('x');
            if (xIdx <= 0) return "";
            return $"x{FmtPct(segment[(xIdx + 1)..].Trim())}";
        }
    }

    private sealed class MultXIdPattern : ReferencePattern
    {
        public MultXIdPattern() : base("MultXId") { }
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            var xIdx = trimmed.LastIndexOf('x');
            return xIdx > 0 ? trimmed[(xIdx + 1)..].Trim() : trimmed;
        }
        public override string FormatExtraInfo(string segment)
        {
            var xIdx = segment.LastIndexOf('x');
            return xIdx > 0 ? $"{segment[..xIdx].Trim()}x" : "";
        }
    }

    private sealed class IdEqualsValuePattern : ReferencePattern
    {
        public IdEqualsValuePattern() : base("IdEqualsValue") { }
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            var eqIdx = trimmed.IndexOf('=');
            return eqIdx > 0 ? trimmed[..eqIdx].Trim() : trimmed;
        }
        public override string FormatDisplay(string segment, string? subject, string? modName)
        {
            if (string.IsNullOrEmpty(subject)) return segment;
            var modPrefix = !string.IsNullOrEmpty(modName) && modName != "0" ? modName + ":" : "";
            var eqIdx = segment.LastIndexOf('=');
            var suffix = eqIdx > 0 ? segment[eqIdx..] : "";
            return $"{modPrefix}{subject}{suffix}";
        }
        public override string FormatExtraInfo(string segment)
        {
            var eqIdx = segment.IndexOf('=');
            if (eqIdx <= 0) return "";
            return $"= {FmtPct(segment[(eqIdx + 1)..].Trim())}";
        }
    }

    private sealed class BracketIdPattern : ReferencePattern
    {
        public BracketIdPattern() : base("BracketId") { }
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            var start = trimmed.StartsWith('[') ? 1 : 0;
            var commaIdx = trimmed.IndexOf(',', start);
            return commaIdx > start ? trimmed[start..commaIdx].Trim() : trimmed[start..].TrimEnd(']').Trim();
        }
    }
}
