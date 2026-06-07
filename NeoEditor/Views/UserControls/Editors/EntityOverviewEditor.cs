using System;
using Avalonia.Controls;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls.Editors;

public class EntityOverviewEditor : ICustomTableEditor
{
    private readonly Type _entityType;
    public Type EntityType => _entityType;
    public string EditorName => $"{_entityType.Name} Overview";
    private TabControl? _tabs;

    public EntityOverviewEditor(Type entityType) { _entityType = entityType; }

    public Control CreateEditor() { _tabs = EditorHelper.CreateEditorTabs(); return _tabs; }

    public void UpdateEntity(IEntity? entity)
    {
        if (_tabs is null) return; _tabs.Items.Clear();
        if (entity is null) return;
        _tabs.Items.Add(EditorHelper.BuildOverviewTab(entity));
    }
}
