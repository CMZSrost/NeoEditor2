using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Views.UserControls.Editors;

public class TreasureTreePreviewEditor : ICustomTableEditor
{
    public Type EntityType => typeof(TreasureTable);
    public string EditorName => "Treasure Table Editor";
    private TabControl? _tabs;
    private TreasureTable? _table;
    private Dictionary<int, TreasureTable>? _allTables;
    private Dictionary<string, ItemType>? _itemTypes;

    public Control CreateEditor() { _tabs = EditorHelper.CreateEditorTabs(); return _tabs; }

    public void UpdateEntity(IEntity? entity)
    {
        if (_tabs is null) return; _tabs.Items.Clear();
        _table = entity as TreasureTable; if (_table is null) return;
        _allTables = ReferenceResolver.GetDedupedInt<TreasureTable>();
        _itemTypes = ReferenceResolver.GetDedupedComposite<ItemType>(it => $"{it.GroupId}.{it.SubgroupId}");

        _tabs.Items.Add(EditorHelper.BuildOverviewTab(_table));
        _tabs.Items.Add(EditorHelper.MakeTab("Treasure Tree", BuildTree()));
    }

    private ScrollViewer BuildTree()
    {
        var tree = new TreeView();
        if (_table is null || _allTables is null) return new ScrollViewer { Content = tree };
        tree.Items.Add(BuildTreeViewItem(_table, new HashSet<int>(), 0));
        return new ScrollViewer { Content = tree, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    private TreeViewItem BuildTreeViewItem(TreasureTable table, HashSet<int> visited, int depth)
    {
        var item = new TreeViewItem
        {
            IsExpanded = true,
            Header = new TextBlock { Text = $"[TT] {table.Name} (id={table.Id})", FontWeight = FontWeight.Bold, Foreground = Brushes.DodgerBlue, TextWrapping = TextWrapping.Wrap }
        };
        if (!visited.Add(table.Id)) return item;
        if (depth >= 5 || string.IsNullOrWhiteSpace(table.Treasures)) return item;

        foreach (var orSeg in table.Treasures.Split('|'))
        {
            var orItem = new TreeViewItem
            {
                IsExpanded = true,
                Header = new TextBlock { Text = "OR Group", FontWeight = FontWeight.SemiBold, Foreground = Brushes.CornflowerBlue, TextWrapping = TextWrapping.Wrap }
            };
            foreach (var seg in orSeg.Split(','))
            {
                var parts = seg.Trim().Split('x');
                if (parts.Length < 2) continue;
                var itemId = parts[0].Trim();
                var probStr = parts.Length > 1 ? parts[1].Trim() : "1";
                var qtyRange = parts.Length > 2 ? parts[2].Trim() : "1";
                var prob = double.TryParse(probStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 1.0;

                var itemName = itemId;
                TreasureTable? nestedTable = null;

                if (_itemTypes?.TryGetValue(itemId, out var matched) == true)
                {
                    itemName = matched.Name;
                    if (!string.IsNullOrWhiteSpace(matched.TreasureId) && int.TryParse(matched.TreasureId, out var nid) && _allTables!.TryGetValue(nid, out var nt) && !visited.Contains(nid))
                    { visited.Add(nid); nestedTable = nt; }
                }
                else if (int.TryParse(itemId, out var tid) && _allTables!.TryGetValue(tid, out var tt) && !visited.Contains(tid))
                { visited.Add(tid); nestedTable = tt; itemName = $"[TT] {tt.Name}"; }

                var headerText = $"{itemName} ({itemId})  [{prob:P0}, qty {qtyRange}]";
                var andItem = new TreeViewItem
                {
                    IsExpanded = true,
                    Header = new TextBlock { Text = headerText, Foreground = Brushes.DarkGreen, TextWrapping = TextWrapping.Wrap }
                };
                if (nestedTable is not null) andItem.Items.Add(BuildTreeViewItem(nestedTable, visited, depth + 1));
                orItem.Items.Add(andItem);
            }
            item.Items.Add(orItem);
        }
        return item;
    }
}
