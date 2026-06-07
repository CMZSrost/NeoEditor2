using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls.Editors;

public class RecipeFlowchartEditor : ICustomTableEditor
{
    public Type EntityType => typeof(Recipe);
    public string EditorName => "Recipe Editor";
    private TabControl? _tabs;
    private Recipe? _recipe;
    private Dictionary<int, Ingredient>? _ingredients;
    private Dictionary<int, TreasureTable>? _tt;
    private Dictionary<int, ItemProp>? _itemProps;
    private List<ItemType>? _itemTypes;

    public Control CreateEditor() { _tabs = EditorHelper.CreateEditorTabs(); return _tabs; }

    public void UpdateEntity(IEntity? entity)
    {
        if (_tabs is null) return; _tabs.Items.Clear();
        _recipe = entity as Recipe; if (_recipe is null) return;
        _ingredients = ReferenceResolver.GetDedupedInt<Ingredient>();
        _tt = ReferenceResolver.GetDedupedInt<TreasureTable>();
        _itemProps = ReferenceResolver.GetDedupedInt<ItemProp>();
        _itemTypes = ReferenceResolver.GetDedupedList<ItemType>();

        _tabs.Items.Add(EditorHelper.BuildOverviewTab(_recipe));
        _tabs.Items.Add(EditorHelper.MakeTab("Recipe Tree", BuildRecipeTree()));
    }

    private ScrollViewer BuildRecipeTree()
    {
        var tree = new TreeView();
        var r = _recipe!;
        var root = EditorHelper.NewNode(string.IsNullOrWhiteSpace(r.Name) ? $"Recipe (nID={r.Id})" : $"{r.Name} (nID={r.Id})", Brushes.DodgerBlue, true);

        if (!string.IsNullOrWhiteSpace(r.Tools))
            root.Items.Add(BuildIngredientGroup("Tools", r.Tools, "#FF8C00"));
        if (!string.IsNullOrWhiteSpace(r.Consumed))
            root.Items.Add(BuildIngredientGroup("Consumed", r.Consumed, "#DC143C"));
        if (!string.IsNullOrWhiteSpace(r.Destroyed))
            root.Items.Add(BuildIngredientGroup("Destroyed", r.Destroyed, "#8B0000"));
        root.Items.Add(BuildProductNode(r.TreasureId));
        if (!string.IsNullOrWhiteSpace(r.AlsoTry))
            root.Items.Add(BuildAlsoTry(r.AlsoTry));

        tree.Items.Add(root);
        return new ScrollViewer { Content = tree, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    private TreeViewItem BuildIngredientGroup(string label, string raw, string color)
    {
        var g = EditorHelper.NewNode(label, Brush.Parse(color), true);
        if (string.IsNullOrWhiteSpace(raw)) { g.Items.Add(EditorHelper.NewNode("(None)", Brushes.Gray)); return g; }
        foreach (var part in raw.Split('+'))
        {
            var parts = part.Trim().Split('x');
            var qty = parts.Length >= 2 ? parts[0] : "1";
            var idStr = parts.Length >= 2 ? parts[1] : parts[0];
            var id = int.TryParse(idStr, out var i) ? i : 0;
            Ingredient? ing = null;
            var name = _ingredients is not null && _ingredients.TryGetValue(id, out ing) ? ing.Name : $"#{idStr}";
            var node = EditorHelper.NewNode($"{name} x{qty}");
            if (ing is not null)
            {
                EditorHelper.NavOnCtrl(node, () => ReferenceResolver.NavigateToByKey<Ingredient>(id));
                AddProps(node, ing.RequiredProps, "Required", Brushes.DarkOrange, Brushes.Orange);
                AddProps(node, ing.ForbidProps, "Forbidden", Brushes.IndianRed, Brushes.Red);
            }
            g.Items.Add(node);
        }
        return g;
    }

    private void AddProps(TreeViewItem parent, string raw, string label, IBrush hdr, IBrush leaf)
    {
        if (string.IsNullOrWhiteSpace(raw) || _itemProps is null) return;
        var n = EditorHelper.NewNode($"{label} Properties", hdr, true);
        foreach (var s in raw.Split('&'))
        {
            if (int.TryParse(s.Trim(), out var pid) && _itemProps.TryGetValue(pid, out var p))
            {
                var child = EditorHelper.NewNode(p.PropertyName, leaf);
                EditorHelper.NavOnCtrl(child, () => ReferenceResolver.NavigateToByKey<ItemProp>(pid));
                n.Items.Add(child);
            }
        }
        if (n.Items.Count > 0) parent.Items.Add(n);
    }

    private TreeViewItem BuildProductNode(string ttId)
    {
        var n = EditorHelper.NewNode("Product", Brushes.DarkGreen, true);
        if (!int.TryParse(ttId, out var id) || _tt?.TryGetValue(id, out var t) != true)
        { n.Items.Add(EditorHelper.NewNode($"TT #{ttId}")); return n; }
        if (string.IsNullOrWhiteSpace(t.Treasures)) { n.Items.Add(EditorHelper.NewNode(t.Name)); return n; }
        foreach (var seg in t.Treasures.Split(',').Take(10))
        {
            var parts = seg.Trim().Split('x');
            if (parts.Length < 2) continue;
            var itemId = parts[0]; var qty = parts.Length > 2 ? parts[2] : "1";
            var it = _itemTypes?.FirstOrDefault(x => $"{x.GroupId}.{x.SubgroupId}" == itemId);
            var name = it?.Name ?? itemId;
            var child = EditorHelper.NewNode($"{name} ({itemId}) qty:{qty}");
            if (it is not null) EditorHelper.NavOnCtrl(child, () => ReferenceResolver.NavigateTo(typeof(ItemType), it.EntityId));
            n.Items.Add(child);
        }
        return n;
    }

    private TreeViewItem BuildAlsoTry(string raw)
    {
        var n = EditorHelper.NewNode("Also Try", Brushes.Purple, true);
        var lookup = ReferenceResolver.GetDedupedInt<Recipe>();
        foreach (var seg in raw.Split(','))
        {
            if (int.TryParse(seg.Trim(), out var id))
            {
                var name = lookup.TryGetValue(id, out var r) ? r.Name : $"Recipe #{id}";
                var child = EditorHelper.NewNode(name);
                EditorHelper.NavOnCtrl(child, () => ReferenceResolver.NavigateToByKey<Recipe>(id));
                n.Items.Add(child);
            }
        }
        return n;
    }
}
