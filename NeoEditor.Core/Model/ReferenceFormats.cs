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
    public string FormatTemplate => ValueFirst ? "{value}={entityRef}" : "{entityRef}={value}";
    public string ToRawString()
    {
        var v = Value.ToString("0.####", CultureInfo.InvariantCulture);
        return ValueFirst ? $"{v}={Entity.ToRawString()}" : $"{Entity.ToRawString()}={v}";
    }
    public string DisplayText
    {
        get
        {
            var v = Value.ToString("0.####", CultureInfo.InvariantCulture);
            return ValueFirst ? $"{v} = {Entity.DisplayText}" : $"{Entity.DisplayText} = {v}";
        }
    }
}

/// <summary>
/// Bracketed entity reference (BattleMove conditions).
/// Template: "[{entityRef}". Example: "[211".
/// </summary>
public sealed record BracketFormat : IReferenceFormat
{
    public EntityRef Entity { get; init; } = new();
    public string FormatTemplate => "[{entityRef}";
    public string ToRawString() => $"[{Entity.ToRawString()}";
    public string DisplayText => $"[{Entity.DisplayText}";
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
