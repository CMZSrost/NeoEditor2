using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Data.Model;

// ═══════════════════════════════════════════════════════════════════════════
//  Leaf formats — each wraps exactly one EntityRef with typed parameters
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Pure entity reference — no decorations.
/// Template: "{entityRef}". Example: "211", "NSE:42", "86.6".
/// </summary>
public sealed record PureRefFormat : IReferenceFormat
{
    public EntityRef Entity { get; init; } = new();
    public string FormatTemplate => "{entityRef}";
    public string ToRawString() => Entity.ToRawString();
    public string DisplayText => Entity.DisplayText;
}

/// <summary>
/// Negated entity reference. Wraps any IReferenceEntry (EntityRef or another format).
/// Template: "-{entry}". Example: "-211", "-211x1.5" (negation of an IdXMultFormat).
/// </summary>
public sealed record NegatedRefFormat : IReferenceFormat
{
    public IReferenceEntry Inner { get; init; } = new EntityRef();
    public string FormatTemplate => "-{entry}";
    public string ToRawString() => $"-{Inner.ToRawString()}";
    public string DisplayText => $"-{Inner.DisplayText}";
}

/// <summary>
/// Entity reference with multiplier — suffix form: {id}x{mult}.
/// Template: "{entityRef}x{mul}". Example: "211x1.5".
/// </summary>
public sealed record IdXMultFormat : IReferenceFormat
{
    public EntityRef Entity { get; init; } = new();
    public double Multiplier { get; init; } = 1.0;
    public string FormatTemplate => "{entityRef}x{mul}";
    public string ToRawString()
    {
        var m = Multiplier.ToString("0.####", CultureInfo.InvariantCulture);
        return $"{Entity.ToRawString()}x{m}";
    }
    public string DisplayText => $"{Entity.DisplayText} x{Multiplier}";
}

/// <summary>
/// Entity reference with multiplier — prefix form: {mult}x{id}.
/// Template: "{mul}x{entityRef}". Example: "2x15".
/// </summary>
public sealed record MultXIdFormat : IReferenceFormat
{
    public EntityRef Entity { get; init; } = new();
    public double Multiplier { get; init; } = 1.0;
    public string FormatTemplate => "{mul}x{entityRef}";
    public string ToRawString()
    {
        var m = Multiplier.ToString("0.####", CultureInfo.InvariantCulture);
        return $"{m}x{Entity.ToRawString()}";
    }
    public string DisplayText => $"{Multiplier}x{Entity.DisplayText}";
}

/// <summary>
/// Entity reference with value assignment.
/// Template: "{entityRef}={value}" or "{value}={entityRef}".
/// Examples: "38=1", "1=38".
/// </summary>
public sealed record AssignFormat : IReferenceFormat
{
    public EntityRef Entity { get; init; } = new();
    public double Value { get; init; }
    /// <summary>True = {value}={entityRef}, False = {entityRef}={value}.</summary>
    public bool ValueFirst { get; init; }

    /// <summary>
    /// Original value text when it is not a plain number (e.g. aSwitchIDs state names
    /// "On"/"Off"/"Hood Off" are free text). When non-null, round-trips verbatim and
    /// <see cref="Value"/> is ignored.
    /// </summary>
    public string? RawValue { get; init; }

    public string FormatTemplate => ValueFirst ? "{value}={entityRef}" : "{entityRef}={value}";
    public string ToRawString()
    {
        var v = RawValue ?? Value.ToString("0.####", CultureInfo.InvariantCulture);
        return ValueFirst ? $"{v}={Entity.ToRawString()}" : $"{Entity.ToRawString()}={v}";
    }
    public string DisplayText
    {
        get
        {
            var v = RawValue ?? Value.ToString("0.####", CultureInfo.InvariantCulture);
            return ValueFirst ? $"{v} = {Entity.DisplayText}" : $"{Entity.DisplayText} = {v}";
        }
    }
}

/// <summary>
/// Bracketed entity reference (BattleMove conditions).
/// Template: "[{entityRef},{p1},{p2}]". Example: "[-137,0,0]".
/// The columns use the quirky "]," group separator whose boundary brackets are asymmetric
/// (first segment "[−137,0,0", last "146,0,0]"), so the original segment text is kept in
/// <see cref="RawSegment"/> and round-trips verbatim; <see cref="Entity"/>/<see cref="P1"/>/<see cref="P2"/>
/// are the parsed parts used for resolution/display.
/// </summary>
public sealed record BracketFormat : IReferenceFormat
{
    public EntityRef Entity { get; init; } = new();

    /// <summary>First bracket parameter as raw text (e.g. "0"). Null = absent.</summary>
    public string? P1 { get; init; }

    /// <summary>Second bracket parameter as raw text (e.g. "0" or "0.5"). Null = absent.</summary>
    public string? P2 { get; init; }

    /// <summary>Original segment text as split by the "]," separator — lossless round-trip.</summary>
    public string? RawSegment { get; init; }

    public string FormatTemplate => "[{entityRef},{p1},{p2}]";
    public string ToRawString() => RawSegment ?? Build();
    public string DisplayText => Build();

    private string Build()
    {
        var s = $"[{Entity.ToRawString()}";
        if (P1 is not null) s += $",{P1}";
        if (P2 is not null) s += $",{P2}";
        return s + "]";
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  Compound format — holds multiple sub-formats (AST-like nesting)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Multi-ingredient recipe reference. Nests <see cref="IReferenceFormat"/> ingredients
/// (either IdXMultFormat or MultXIdFormat depending on the pattern)
/// and an optional target with parameters.
/// Template: "{fmt1}+{fmt2}+...={target}X{p1}X{p2}X{p3}X{p4}"
/// Example: "1x2+1x3" (MultXIdFormat), "91.8x1+91.3x1=22x1x0x0x0" (IdXMultFormat)
/// </summary>
public sealed record MultiIngredientRecipeFormat : IReferenceFormat
{
    /// <summary>Ingredient sub-formats (type depends on pattern).</summary>
    public IReadOnlyList<IReferenceFormat> Ingredients { get; init; } = [];

    /// <summary>Target entity reference (the "="... part). Null if no target.</summary>
    public EntityRef? Target { get; init; }

    /// <summary>Parameters after the target (X values).</summary>
    public IReadOnlyList<double> TargetParams { get; init; } = [];

    public string FormatTemplate
        => Target is not null
            ? $"{string.Join("+", Enumerable.Repeat("{fmt}", Ingredients.Count))}={{target}}X{string.Join("X", Enumerable.Repeat("{p}", TargetParams.Count))}"
            : string.Join("+", Enumerable.Repeat("{fmt}", Ingredients.Count));

    public string ToRawString()
    {
        var ing = string.Join("+", Ingredients.Select(i => i.ToRawString()));
        if (Target is null) return ing;
        var tp = string.Join("x", TargetParams.Select(p =>
            p.ToString("0.####", CultureInfo.InvariantCulture)));
        return $"{ing}={Target.ToRawString()}x{tp}";
    }

    public string DisplayText => ToRawString();
}

/// <summary>
/// Treasure-table entry with probability and optional quantity — {id}x{prob}x{qty}.
/// Template: "{entityRef}x{prob}x{qty}". Example: "86.6x1.0x5-9", "36.6x0.01694915254" (qty omitted = 1).
/// prob/qty are stored as raw strings so "1.0" vs "1" and min-max ranges like "5-9" round-trip verbatim.
/// </summary>
public sealed record IdXMultXQtyFormat : IReferenceFormat
{
    public EntityRef Entity { get; init; } = new();

    /// <summary>Probability 0~1 as raw text (e.g. "1.0", "0.01694915254").</summary>
    public string Prob { get; init; } = "";

    /// <summary>Quantity as raw text — integer, min-max ("5-9"), or null (=1).</summary>
    public string? Qty { get; init; }

    public string FormatTemplate => "{entityRef}x{prob}x{qty}";
    public string ToRawString()
        => Qty is not null
            ? $"{Entity.ToRawString()}x{Prob}x{Qty}"
            : $"{Entity.ToRawString()}x{Prob}";
    public string DisplayText
        => Qty is not null
            ? $"{Entity.DisplayText} x{Prob} x{Qty}"
            : $"{Entity.DisplayText} x{Prob}";
}

/// <summary>
/// OR-group of alternative references — "X|Y|Z" (pick one).
/// Used by treasuretable.aTreasures where "|" binds tighter than "," (AND).
/// Template: "{alt1}|{alt2}|...". Example: "35.1x0.1x1-1|35.2x0.1x1-1".
/// </summary>
public sealed record OrGroupFormat : IReferenceFormat
{
    public IReadOnlyList<IReferenceEntry> Alternatives { get; init; } = [];

    public string FormatTemplate => string.Join("|", Enumerable.Repeat("{alt}", Alternatives.Count));
    public string ToRawString() => string.Join("|", Alternatives.Select(a => a.ToRawString()));
    public string DisplayText => string.Join(" | ", Alternatives.Select(a => a.DisplayText));
}
