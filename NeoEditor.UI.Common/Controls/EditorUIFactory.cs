using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NeoEditor.Helper;

/// <summary>
/// Pure UI factory methods for building editor tree views and tab controls.
/// Extracted from EditorHelper to separate UI construction from data access.
/// M10: moved from App to UI.Common (pure Avalonia, no App dependencies).
/// </summary>
public static class EditorUIFactory
{
    /// <summary>Create a TreeViewItem with text wrapping, text selection, and consistent styling.</summary>
    public static TreeViewItem NewNode(string text, IBrush? fg = null, bool bold = false)
    {
        var tb = new TextBox
        {
            Text = text,
            Foreground = fg ?? Brushes.Black,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Ibeam)
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

    /// <summary>
    /// Create a selectable, read-only TextBox that looks like a TextBlock.
    /// Use for detail view text that users should be able to copy (Ctrl+C).
    /// </summary>
    public static TextBox SelectableText(string text, double fontSize = 12,
        FontWeight? fontWeight = null, IBrush? foreground = null,
        TextAlignment textAlignment = TextAlignment.Left,
        TextWrapping textWrapping = TextWrapping.Wrap)
    {
        return new TextBox
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeight.Normal,
            Foreground = foreground ?? Brushes.Black,
            TextAlignment = textAlignment,
            TextWrapping = textWrapping,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Ibeam),
            AcceptsReturn = true
        };
    }
}
