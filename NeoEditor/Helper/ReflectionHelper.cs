using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Helper;

public static class ReflectionHelper
{
    public static string GetPropertyTableName<TItem>() where TItem : IEntity
    {
        return GetPropertyTableName(typeof(TItem));
    }

    public static IEnumerable<string> GetPropertyColumnName<TItem>() where TItem : IEntity
    {
        return GetPropertyColumnName(typeof(TItem));
    }

    public static string GetPropertyTableName(Type item)
    {
        return item.GetCustomAttribute<TableAttribute>()!.Name;
    }

    public static IEnumerable<string> GetPropertyColumnName(Type item)
    {
        return item.GetProperties().Select(info => info.GetCustomAttribute<ColumnAttribute>()?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();
    }
}