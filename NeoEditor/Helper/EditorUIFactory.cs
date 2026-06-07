using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NeoEditor.Helper;

/// <summary>
/// Pure UI factory methods for building editor tree views and tab controls.
/// Extracted from EditorHelper to separate UI construction from data access.
/// </summary>
public static class EditorUIFactory
{
    /// <summary>Create a TreeViewItem with text wrapping and consistent styling.</summary>
    public static TreeViewItem NewNode(string text, IBrush? fg = null, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = fg ?? Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 2000
        };
        if (bold) tb.FontWeight = FontWeight.Bold;
        return new TreeViewItem { IsExpanded = true, Header = tb };
    }

    /// <summary>Wire Ctrl+Click navigation on a TreeViewItem.</summary>
    public static void NavOnCtrl(TreeViewItem item, Action nav)
    {
        item.Cursor = new Cursor(StandardCursorType.Hand);
        item.PointerPressed += (_, e) => { if ((e.KeyModifiers & KeyModifiers.Control) != 0) nav(); };
    }

    /// <summary>Create a TabItem with header and scrollable content.</summary>
    public static TabItem MakeTab(string header, Control content)
        => new() { Header = header, Content = content };

    /// <summary>Create a properly stretched TabControl for editor use.</summary>
    public static TabControl CreateEditorTabs()
        => new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
}
