using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls;
using NeoEditor.ViewModels;

namespace NeoEditor.Views.UserControls;

public partial class MainMenuBar : UserControl
{
    private MainWindowViewModel? _vm;

    public MainMenuBar()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            _vm.HelpMenuItems.CollectionChanged -= OnHelpItemsChanged;
        _vm = DataContext as MainWindowViewModel;
        if (_vm is not null)
        {
            _vm.HelpMenuItems.CollectionChanged += OnHelpItemsChanged;
            RebuildHelpMenu();
        }
    }

    private void OnHelpItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildHelpMenu();
    }

    private void RebuildHelpMenu()
    {
        // Keep the static "Import Tutorial" item (index 0), replace the rest
        while (HelpMenu.Items.Count > 1)
            HelpMenu.Items.RemoveAt(HelpMenu.Items.Count - 1);

        if (_vm is null) return;

        foreach (var node in _vm.HelpMenuItems)
            HelpMenu.Items.Add(CreateMenuItem(node));
    }

    private static MenuItem CreateMenuItem(HelpMenuNode node)
    {
        var item = new MenuItem { Header = node.Header };
        if (node.Command is not null)
        {
            item.Command = node.Command;
            item.CommandParameter = node; // pass the node as parameter like the original template did
        }
        if (node.HasChildren)
        {
            foreach (var child in node.Children)
                item.Items.Add(CreateMenuItem(child));
        }
        return item;
    }
}
