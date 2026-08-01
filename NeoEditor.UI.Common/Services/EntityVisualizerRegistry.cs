using System;
using System.Collections.Generic;
using NeoEditor.UI.Common.Visualizers;

namespace NeoEditor.UI.Common.Services;

public class EntityVisualizerRegistry
{
    private readonly Dictionary<Type, IEntityVisualizer> _visualizers = new();
    private IEntityVisualizer? _defaultVisualizer;

    public void Register(IEntityVisualizer visualizer)
    {
        _visualizers[visualizer.EntityType] = visualizer;
    }

    public void SetDefault(IEntityVisualizer visualizer)
    {
        _defaultVisualizer = visualizer;
    }

    public IEntityVisualizer? Get(Type entityType)
    {
        // Try exact match first, then base types (handles EF proxy types)
        if (_visualizers.TryGetValue(entityType, out var v)) return v;
        var t = entityType;
        while (t != null && t != typeof(object))
        {
            if (_visualizers.TryGetValue(t, out v)) return v;
            t = t.BaseType;
        }
        return _defaultVisualizer;
    }

    public bool HasVisualizer(Type entityType)
    {
        var t = entityType;
        while (t != null && t != typeof(object))
        {
            if (_visualizers.ContainsKey(t)) return true;
            t = t.BaseType;
        }
        return false;
    }
}
