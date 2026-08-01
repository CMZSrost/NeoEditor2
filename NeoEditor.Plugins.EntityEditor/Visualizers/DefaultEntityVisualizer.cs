using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Plugins.EntityEditor.Services;
namespace NeoEditor.Plugins.EntityEditor.Visualizers;

public class DefaultEntityVisualizer : IEntityVisualizer
{
    public Type EntityType { get; }
    private readonly VisHelperService _vis;

    public DefaultEntityVisualizer(Type type, VisHelperService vis)
    {
        EntityType = type;
        _vis = vis;
    }

    public Control BuildDetail(IEntity entity)
    {
        var tree = new TreeView();
        var root = _vis.Section(entity.Subject ?? $"[{entity.GetType().Name}]", Brushes.DodgerBlue);

        var props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>() != null
                        && p.DeclaringType != typeof(IEntity))
            .OrderBy(p => p.MetadataToken);

        foreach (var p in props)
        {
            var val = p.GetValue(entity);
            var colName = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>()?.Name ??
                          p.Name;
            var refAttr = p.GetCustomAttribute<ReferenceFieldAttribute>();
            var strVal = val is bool b ? (b ? "1" : "0") : val?.ToString() ?? "";

            if (refAttr is not null && !string.IsNullOrWhiteSpace(strVal))
            {
                var display = strVal.Length > 100 ? strVal[..100] + "..." : strVal;
                root.Items.Add(_vis.Leaf($"→ {colName}: {display}", Brushes.Teal));
            }
            else if (!string.IsNullOrWhiteSpace(strVal))
            {
                var display = strVal.Length > 100 ? strVal[..100] + "..." : strVal;
                root.Items.Add(_vis.Leaf($"{colName}: {display}"));
            }
        }

        tree.Items.Add(root);
        return _vis.Wrap(tree);
    }
}
