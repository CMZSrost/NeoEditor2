using System;
using Avalonia.Controls;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls.Editors;

public class IngredientEditor : ICustomTableEditor
{
    public Type EntityType => typeof(Ingredient);
    public string EditorName => "Ingredient Editor";
    private TabControl? _tabs;

    public Control CreateEditor() { _tabs = EditorHelper.CreateEditorTabs(); return _tabs; }

    public void UpdateEntity(IEntity? entity)
    {
        if (_tabs is null) return; _tabs.Items.Clear();
        if (entity is null) return;
        _tabs.Items.Add(EditorHelper.BuildOverviewTab(entity));
    }
}
