using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NeoEditor.Data.Model;

using NeoEditor.Core.Abstractions;

/// <summary>
/// A collection of reference entries that stores entity-to-entity references.
/// Both single-value and multi-value reference properties use this type.
/// Implements ICollection for editability and IReadOnlyList for indexed access.
/// </summary>
public class ReferenceList<T> : ICollection<T>, IReadOnlyList<T> where T : IReferenceEntry
{
    private readonly List<T> _items = [];

    /// <summary>
    /// The original raw XML text that produced this list.
    /// Updated by the serializer on both Deserialize and Serialize.
    /// Used for backward compatibility — consumers that need the raw string
    /// can use this directly instead of the implicit conversion.
    /// </summary>
    public string RawText { get; set; } = "";

    // ── ICollection<T> ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public void Add(T item) { _items.Add(item); InvalidateRawText(); }

    /// <inheritdoc/>
    public bool Remove(T item) { var r = _items.Remove(item); if (r) InvalidateRawText(); return r; }

    /// <inheritdoc/>
    public void Clear() { _items.Clear(); RawText = ""; }

    /// <inheritdoc/>
    public bool Contains(T item) => _items.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    // ── IReadOnlyList<T> ───────────────────────────────────────────────

    /// <inheritdoc/>
    public T this[int index]
    {
        get => _items[index];
        set { _items[index] = value; InvalidateRawText(); }
    }

    // ── IEnumerable ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── Backward-compat implicit conversion ────────────────────────────

    /// <summary>
    /// Implicit conversion to string for backward compatibility.
    /// Existing code that treats reference properties as strings will
    /// automatically get the raw XML text representation.
    /// New code should use the structured list directly.
    /// </summary>
    public static implicit operator string?(ReferenceList<T>? list)
        => list?.RawText;

    // ── Convenience ────────────────────────────────────────────────────

    /// <summary>Add multiple entries at once.</summary>
    public void AddRange(IEnumerable<T> items)
    {
        _items.AddRange(items);
        InvalidateRawText();
    }

    /// <summary>Insert an entry at a specific index.</summary>
    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        InvalidateRawText();
    }

    /// <summary>Remove the entry at a specific index.</summary>
    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        InvalidateRawText();
    }

    // ── Backward-compat string bridging ────────────────────────────────

    /// <summary>
    /// Backward-compat: delegates to <see cref="RawText"/>.Split(separator).
    /// New code should iterate the list directly instead.
    /// </summary>
    public string[] Split(params char[] separator) => RawText.Split(separator);

    /// <inheritdoc cref="Split(char[])"/>
    public string[] Split(string separator, StringSplitOptions options = StringSplitOptions.None)
        => RawText.Split(separator, options);

    /// <inheritdoc cref="Split(char[])"/>
    public string[] Split(char separator, StringSplitOptions options = StringSplitOptions.None)
        => RawText.Split(separator, options);

    /// <summary>
    /// Returns the raw XML text representation of this list.
    /// Equivalent to serializing via <see cref="Core.Abstractions.IReferenceListSerializer"/>.
    /// </summary>
    public string ToRawString(string? separator)
    {
        if (_items.Count == 0) return "";
        if (separator is null) return _items[0].ToRawString();
        return string.Join(separator, _items.Select(e => e.ToRawString()));
    }

    /// <inheritdoc/>
    public override string ToString()
        => $"[{string.Join(", ", _items.Select(e => e.DisplayText))}]";

    // ── Private helpers ─────────────────────────────────────────────────

    private void InvalidateRawText()
    {
        // R30 (M1): mark the raw text stale on mutation — consumers that read RawText
        // (Split / implicit string conversion) must NOT see the previous value after the
        // entries changed. Serialize() re-populates it on the next write.
        RawText = "";
    }
}
