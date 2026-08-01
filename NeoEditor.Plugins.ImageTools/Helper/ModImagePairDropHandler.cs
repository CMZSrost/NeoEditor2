using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools.Helper;

public class ModImagePairDropHandler : DropHandlerBase
{
    private ListBoxItem? _dropIndicatorItem;
    private bool _dropIndicatorAfter;

    public override void Over(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        if (sender is ListBox listBox)
        {
            UpdateDropIndicator(listBox, e, sourceContext);
        }

        base.Over(sender, e, sourceContext, targetContext);
    }

    public override void Leave(object? sender, RoutedEventArgs e)
    {
        ClearDropIndicator();
        base.Leave(sender, e);
    }

    public override void Drop(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
    {
        try
        {
            base.Drop(sender, e, sourceContext, targetContext);
        }
        finally
        {
            ClearDropIndicator();
        }
    }

    public override void Cancel(object? sender, RoutedEventArgs e)
    {
        ClearDropIndicator();
        base.Cancel(sender, e);
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        return TryHandleDrop(sender, e, sourceContext, execute: false);
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext,
        object? state)
    {
        return TryHandleDrop(sender, e, sourceContext, execute: true);
    }

    private static bool TryHandleDrop(object? sender, DragEventArgs e, object? sourceContext, bool execute)
    {
        if (sender is not ListBox listBox ||
            sourceContext is not ModImagePairItem sourceItem ||
            listBox.ItemsSource is not ObservableCollection<ModImagePairItem> items ||
            items.Count <= 1)
        {
            return false;
        }

        var sourceIndex = items.IndexOf(sourceItem);
        if (sourceIndex < 0)
        {
            return false;
        }

        var dropInfo = GetDropInfo(listBox, e);
        var targetIndex = CalculateTargetIndex(items, sourceIndex, dropInfo.TargetItem, dropInfo.InsertAfter);
        if (targetIndex < 0 || targetIndex > items.Count - 1)
        {
            return false;
        }

        e.DragEffects = DragDropEffects.Move;
        if (!execute)
        {
            return true;
        }

        if (sourceIndex == targetIndex)
        {
            return false;
        }

        if (listBox.DataContext is ModImagesDocument document)
        {
            return document.MoveImagePair(sourceItem, targetIndex);
        }

        items.Move(sourceIndex, targetIndex);
        return true;
    }

    private static int CalculateTargetIndex(ObservableCollection<ModImagePairItem> items, int sourceIndex,
        ModImagePairItem? targetItem, bool insertAfter)
    {
        if (targetItem is null)
        {
            return items.Count - 1;
        }

        var targetIndex = items.IndexOf(targetItem);
        if (targetIndex < 0)
        {
            return -1;
        }

        var insertIndex = targetIndex + (insertAfter ? 1 : 0);
        if (sourceIndex < insertIndex)
        {
            insertIndex--;
        }

        return Math.Clamp(insertIndex, 0, items.Count - 1);
    }

    private static (ModImagePairItem? TargetItem, bool InsertAfter) GetDropInfo(ListBox listBox, DragEventArgs e)
    {
        var targetContainer = FindTargetContainer(listBox, e);
        if (targetContainer?.DataContext is not ModImagePairItem targetItem)
        {
            return (null, true);
        }

        var targetPoint = e.GetPosition(targetContainer);
        var insertAfter = targetPoint.Y >= targetContainer.Bounds.Height / 2d;
        return (targetItem, insertAfter);
    }

    private void UpdateDropIndicator(ListBox listBox, DragEventArgs e, object? sourceContext)
    {
        if (sourceContext is not ModImagePairItem ||
            listBox.ItemsSource is not ObservableCollection<ModImagePairItem> items ||
            items.Count <= 1)
        {
            ClearDropIndicator();
            return;
        }

        var targetContainer = FindTargetContainer(listBox, e);
        var insertAfter = true;

        if (targetContainer is not null)
        {
            var targetPoint = e.GetPosition(targetContainer);
            insertAfter = targetPoint.Y >= targetContainer.Bounds.Height / 2d;
        }
        else
        {
            targetContainer = listBox.GetVisualDescendants().OfType<ListBoxItem>().LastOrDefault();
        }

        ApplyDropIndicator(targetContainer, insertAfter);
    }

    private void ApplyDropIndicator(ListBoxItem? targetContainer, bool insertAfter)
    {
        if (ReferenceEquals(_dropIndicatorItem, targetContainer) && _dropIndicatorAfter == insertAfter)
        {
            return;
        }

        ClearDropIndicator();
        if (targetContainer is null)
        {
            return;
        }

        _dropIndicatorItem = targetContainer;
        _dropIndicatorAfter = insertAfter;
        _dropIndicatorItem.Classes.Add(insertAfter ? "drop-after" : "drop-before");
    }

    private void ClearDropIndicator()
    {
        if (_dropIndicatorItem is null)
        {
            return;
        }

        _dropIndicatorItem.Classes.Remove("drop-before");
        _dropIndicatorItem.Classes.Remove("drop-after");
        _dropIndicatorItem = null;
    }

    private static ListBoxItem? FindTargetContainer(ListBox listBox, DragEventArgs e)
    {
        var hit = listBox.InputHitTest(e.GetPosition(listBox)) as StyledElement;
        while (hit != null)
        {
            if (hit is ListBoxItem listBoxItem)
            {
                return listBoxItem;
            }

            hit = hit.Parent;
        }

        return null;
    }
}
