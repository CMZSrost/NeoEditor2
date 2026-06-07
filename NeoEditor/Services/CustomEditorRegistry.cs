using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public class CustomEditorRegistry
{
    private readonly Dictionary<Type, ICustomTableEditor> _editors = new();

    public void Register(ICustomTableEditor editor)
    {
        _editors[editor.EntityType] = editor;
    }

    public bool TryGet(Type entityType, out ICustomTableEditor? editor)
    {
        return _editors.TryGetValue(entityType, out editor);
    }

    public IEnumerable<Type> RegisteredTypes => _editors.Keys;
    public bool HasEditor(Type entityType) => _editors.ContainsKey(entityType);
}
