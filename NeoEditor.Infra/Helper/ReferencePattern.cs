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
    public static readonly ReferencePattern IdXMultXQty = new IdXMultXQtyPattern();
    public static readonly ReferencePattern IdEqualsValue = new IdEqualsValuePattern();
    public static readonly ReferencePattern ValueEqualsId = new ValueEqualsIdPattern();
    public static readonly ReferencePattern BracketId = new BracketIdPattern();

    /// <summary>Resolve a pattern from a pattern name string.</summary>
    public static ReferencePattern FromName(string? name) => name switch
    {
        "{id}x{mult}" => IdXMult,
        "{mult}x{id}" => MultXId,
        "{id}x{mult}x{qty}" => IdXMultXQty,
        "{id}={value}" => IdEqualsValue,
        "{value}={id}" => ValueEqualsId,
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
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            return trimmed.StartsWith('-') ? trimmed[1..].Trim() : trimmed;
        }
        public override string FormatDisplay(string segment, string? subject, string? modName)
        {
            if (string.IsNullOrEmpty(subject)) return segment;
            var isNeg = segment.TrimStart().StartsWith('-');
            var negPrefix = isNeg ? "~" : "";
            var modPrefix = !string.IsNullOrEmpty(modName) && modName != "0" ? modName + ":" : "";
            return $"{negPrefix}{modPrefix}{subject} ({ExtractRawId(segment)})";
        }
        public override string FormatExtraInfo(string segment)
        {
            return segment.TrimStart().StartsWith('-') ? "-" : "";
        }
    }

    private sealed class IdXMultPattern : ReferencePattern
    {
        public IdXMultPattern() : base("IdXMult") { }
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            // Strip leading '-' (negation modifier) if present
            var isNeg = trimmed.StartsWith('-');
            var body = isNeg ? trimmed[1..].Trim() : trimmed;
            var xIdx = body.IndexOf('x');
            return xIdx > 0 ? body[..xIdx].Trim() : body;
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
            var trimmed = segment.Trim();
            var isNeg = trimmed.StartsWith('-');
            var xIdx = trimmed.LastIndexOf('x');
            var result = "";
            if (isNeg) result += "-";
            if (xIdx > 0) result += $"x{FmtPct(trimmed[(xIdx + 1)..].Trim())}";
            return result;
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

    private sealed class IdXMultXQtyPattern : ReferencePattern
    {
        public IdXMultXQtyPattern() : base("IdXMultXQty") { }
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            var isNeg = trimmed.StartsWith('-');
            var body = isNeg ? trimmed[1..].Trim() : trimmed;
            var xIdx = body.IndexOf('x');
            return xIdx > 0 ? body[..xIdx].Trim() : body;
        }
        public override string FormatDisplay(string segment, string? subject, string? modName)
        {
            if (string.IsNullOrEmpty(subject)) return segment;
            var isNeg = segment.TrimStart().StartsWith('-');
            var negPrefix = isNeg ? "~" : "";
            var modPrefix = !string.IsNullOrEmpty(modName) && modName != "0" ? modName + ":" : "";
            var xIdx = segment.IndexOf('x');
            var suffix = xIdx > 0 ? segment[xIdx..] : "";
            return $"{negPrefix}{modPrefix}{subject}{suffix}";
        }
        public override string FormatExtraInfo(string segment)
        {
            var trimmed = segment.Trim();
            var xIdx = trimmed.IndexOf('x');
            return xIdx > 0 && xIdx < trimmed.Length - 1 ? trimmed[xIdx..] : "";
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

    private sealed class ValueEqualsIdPattern : ReferencePattern
    {
        public ValueEqualsIdPattern() : base("ValueEqualsId") { }
        public override string ExtractRawId(string segment)
        {
            var trimmed = segment.Trim();
            var eqIdx = trimmed.IndexOf('=');
            return eqIdx > 0 ? trimmed[(eqIdx + 1)..].Trim() : trimmed;
        }
        public override string FormatDisplay(string segment, string? subject, string? modName)
        {
            if (string.IsNullOrEmpty(subject)) return segment;
            var modPrefix = !string.IsNullOrEmpty(modName) && modName != "0" ? modName + ":" : "";
            var eqIdx = segment.IndexOf('=');
            var prefix = eqIdx > 0 ? segment[..(eqIdx + 1)] : "";
            return $"{prefix}{modPrefix}{subject}";
        }
        public override string FormatExtraInfo(string segment)
        {
            var eqIdx = segment.IndexOf('=');
            if (eqIdx <= 0) return "";
            return segment[..eqIdx].Trim();
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
        public override string FormatExtraInfo(string segment)
        {
            var trimmed = segment.Trim();
            var start = trimmed.StartsWith('[') ? 1 : 0;
            var commaIdx = trimmed.IndexOf(',', start);
            if (commaIdx <= start) return "";
            var tail = trimmed[(commaIdx + 1)..].TrimEnd(']').Trim();
            return tail.Length > 0 ? $",{tail}" : "";
        }
    }
}
