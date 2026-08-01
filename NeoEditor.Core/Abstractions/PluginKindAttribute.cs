using System;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Marks a plugin class with its classification kind.
/// Every <see cref="IPlugin"/> implementation must carry exactly one <c>[PluginKind]</c> attribute.
/// Single-use, non-inherited — each plugin class declares its own kind.
/// See <c>spec/R23-plugin-classification.md</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PluginKindAttribute : Attribute
{
    public PluginKind Kind { get; }

    public PluginKindAttribute(PluginKind kind)
    {
        Kind = kind;
    }
}
