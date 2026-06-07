using System;
using Avalonia.Controls;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls.Editors;

public class ItemPropEditor : ICustomTableEditor
{
    public Type EntityType => typeof(ItemProp);
    public string EditorName => "Item Property";
    private TabControl? _tabs;

    public Control CreateEditor() { _tabs = EditorHelper.CreateEditorTabs(); return _tabs; }

    public void UpdateEntity(IEntity? entity)
    {
        if (_tabs is null) return; _tabs.Items.Clear();
        if (entity is null) return;
        _tabs.Items.Add(EditorHelper.BuildOverviewTab(entity));
    }
}
