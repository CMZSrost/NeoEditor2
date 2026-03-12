using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class ModImagesDocumentView : UserControl
{
    private DispatcherTimer? _highlightTimer;
    private ListBoxItem? _highlightedItem;
    private ModImagesDocument? _trackedDocument;

    public ModImagesDocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => ClearHighlight();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_trackedDocument is not null)
        {
            _trackedDocument.PropertyChanged -= OnDocumentPropertyChanged;
            _trackedDocument = null;
        }

        if (DataContext is ModImagesDocument document)
        {
            _trackedDocument = document;
            document.PropertyChanged += OnDocumentPropertyChanged;
        }
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ModImagesDocument document)
        {
            return;
        }

        if (e.PropertyName == nameof(ModImagesDocument.ScrollToSelectedPairRequestId))
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => BringSelectedPairIntoView(document));
        }

        if (e.PropertyName == nameof(ModImagesDocument.HighlightSelectedPairRequestId))
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => HighlightSelectedPair(document));
        }
    }

    private void BringSelectedPairIntoView(ModImagesDocument document)
    {
        var listBox = this.FindControl<ListBox>("ImagePairsList");
        if (listBox is null || document.SelectedPair is null)
        {
            return;
        }

        listBox.UpdateLayout();
        listBox.ScrollIntoView(document.SelectedPair);
        listBox.UpdateLayout();

        var container = FindItemContainer(listBox, document.SelectedPair);
        container?.BringIntoView();
    }

    private void HighlightSelectedPair(ModImagesDocument document)
    {
        var listBox = this.FindControl<ListBox>("ImagePairsList");
        if (listBox is null || document.SelectedPair is null)
        {
            return;
        }

        listBox.UpdateLayout();
        listBox.ScrollIntoView(document.SelectedPair);
        listBox.UpdateLayout();

        var container = FindItemContainer(listBox, document.SelectedPair);
        if (container is null)
        {
            return;
        }

        ClearHighlight();
        _highlightedItem = container;
        _highlightedItem.Classes.Add("rename-highlighted");

        _highlightTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _highlightTimer.Tick -= OnHighlightTimerTick;
        _highlightTimer.Tick += OnHighlightTimerTick;
        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private void OnHighlightTimerTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _highlightTimer?.Stop();
        ClearHighlight();
    }

    private void ClearHighlight()
    {
        if (_highlightedItem is not null)
        {
            _highlightedItem.Classes.Remove("rename-highlighted");
            _highlightedItem = null;
        }
    }

    private static ListBoxItem? FindItemContainer(ListBox listBox, object item)
    {
        return listBox.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .FirstOrDefault(container => ReferenceEquals(container.DataContext, item));
    }
}
