using Avalonia.Controls;
using Avalonia.Input;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools.Views;

public partial class ImageCreateDocumentView : UserControl
{
    public ImageCreateDocumentView()
    {
        InitializeComponent();
    }

    /// <summary>Single-click any part of a row (text or checkbox) → select it, which
    /// drives the right-side preview and enables Open in Editor. Setting SelectedItem
    /// manually avoids depending on the ListBox selection model (Avalonia 12 removed
    /// SelectionMode), which kept Open in Editor permanently disabled.</summary>
    private void OnPendingListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ImageCreateDocument document
            || e.Source is not Control source
            || source.DataContext is not PendingImageItem item)
        {
            return;
        }

        document.SelectedItem = item;
    }
}
