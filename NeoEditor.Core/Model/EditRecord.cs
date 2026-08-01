using System.Reflection;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Command;

/// <summary>
/// Named struct replacing ValueTuple in BatchEditCommand.
/// Used by serialization — no reflection on compiler-generated Item1/Item2 names.
/// </summary>
public readonly record struct EditRecord(
    IEntity Entity,
    PropertyInfo Property,
    string ColumnName,
    object? OldValue,
    object? NewValue);
