using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools.Views;

/// <summary>
/// One image slot: title + image + (crop overlay when the slot is crop-enabled) +
/// save button. The crop pointer interaction (viewport mapping, handle dragging) is
/// self-contained here — the parent document view only places the slot.
/// </summary>
public partial class ImageSlotView : UserControl
{
    private const double EdgeHandleThickness = 12;
    private const double CornerHandleSize = 16;
    private const int MinimumCropSize = 2;

    private readonly CropSelectionInteraction _selectionInteraction = new(MinimumCropSize);
    private readonly ImageSelectionOverlayPresenter _overlayPresenter;
    private ImageCropSelection? _interactiveSelection;
    private ImageSlotViewModel? _viewModel;

    public ImageSlotView()
    {
        InitializeComponent();
        _overlayPresenter = new ImageSelectionOverlayPresenter(
            SelectionBorder,
            [SelectionMaskTop, SelectionMaskLeft, SelectionMaskRight, SelectionMaskBottom],
            [
                LeftHandle, RightHandle, TopHandle, BottomHandle, TopLeftHandle, TopRightHandle, BottomLeftHandle,
                BottomRightHandle
            ],
            EdgeHandleThickness,
            CornerHandleSize);
        DataContextChanged += OnDataContextChanged;
        ImageViewer.PropertyChanged += OnImageViewerPropertyChanged;
        SelectionOverlayCanvas.PropertyChanged += OnSelectionOverlayCanvasPropertyChanged;
        UpdateViewModel(DataContext as ImageSlotViewModel);
        UpdateSelectionOverlay();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        UpdateViewModel(DataContext as ImageSlotViewModel);
    }

    private void UpdateViewModel(ImageSlotViewModel? newViewModel)
    {
        if (ReferenceEquals(_viewModel, newViewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _selectionInteraction.End();
        _interactiveSelection = null;
        _viewModel = newViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateSelectionOverlay();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageSlotViewModel.Image) or nameof(ImageSlotViewModel.CropRect))
        {
            _interactiveSelection = null;
            UpdateSelectionOverlay();
        }
    }

    private void OnImageViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty
            || e.Property == Image.SourceProperty
            || e.Property == Image.StretchProperty)
        {
            RefreshAfterGeometryChanged();
        }
    }

    private void OnSelectionOverlayCanvasPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            RefreshAfterGeometryChanged();
        }
    }

    private void RefreshAfterGeometryChanged()
    {
        if (_selectionInteraction.IsActive)
        {
            _selectionInteraction.End();
            CommitInteractiveSelection();
        }

        UpdateSelectionOverlay();
    }

    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not { HasImage: true } || sender is not Control handle)
        {
            return;
        }

        var point = e.GetCurrentPoint(SelectionOverlayCanvas);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var cropHandle = GetCropHandle(handle.Tag as string);
        if (cropHandle == CropHandle.None)
        {
            return;
        }

        if (GetViewportGeometry() is not { } geometry || !TryGetDisplayedSelection(out var selection))
        {
            return;
        }

        if (!_selectionInteraction.TryBegin(cropHandle, point.Position, selection, geometry))
        {
            return;
        }

        _interactiveSelection = selection;
        e.Pointer.Capture(handle);
        e.Handled = true;
    }

    private void OnHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_selectionInteraction.IsActive || _viewModel is not { HasImage: true })
        {
            return;
        }

        if (GetViewportGeometry() is not { } geometry)
        {
            return;
        }

        var current = e.GetPosition(SelectionOverlayCanvas);
        _interactiveSelection = _selectionInteraction.Update(current, geometry);
        UpdateSelectionOverlay();
        e.Handled = true;
    }

    private void OnHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_selectionInteraction.IsActive)
        {
            return;
        }

        _selectionInteraction.End();
        e.Pointer.Capture(null);
        CommitInteractiveSelection();
        UpdateSelectionOverlay();
        e.Handled = true;
    }

    private void UpdateSelectionOverlay()
    {
        if (_viewModel is not { HasImage: true } || GetViewportGeometry() is not { } geometry ||
            !TryGetDisplayedSelection(out var selection))
        {
            _overlayPresenter.Hide();
            return;
        }

        var viewportRect = geometry.Project(selection.ToPixelRect());
        if (!ImageSelectionViewportMapper.IsValidRect(viewportRect))
        {
            _overlayPresenter.Hide();
            return;
        }

        _overlayPresenter.Show(viewportRect, SelectionOverlayCanvas.Bounds.Size);
    }

    private ImageSelectionViewportGeometry? GetViewportGeometry()
    {
        var sourceWidth = _viewModel?.Image?.PixelSize.Width ?? 0;
        var sourceHeight = _viewModel?.Image?.PixelSize.Height ?? 0;
        return ImageSelectionViewportMapper.TryCreateGeometryFromUniformStretch(
            SelectionOverlayCanvas.Bounds.Size,
            sourceWidth,
            sourceHeight);
    }

    private bool TryGetDisplayedSelection(out ImageCropSelection selection)
    {
        if (_interactiveSelection is { } interactiveSelection)
        {
            selection = interactiveSelection;
            return true;
        }

        if (_viewModel?.CropRect is { } cropRect)
        {
            selection = ImageCropSelection.FromPixelRect(cropRect);
            return true;
        }

        if (_viewModel?.Image is { } image)
        {
            selection = ImageCropSelection.FullImage(image.PixelSize.Width, image.PixelSize.Height);
            return true;
        }

        selection = default;
        return false;
    }

    private void CommitInteractiveSelection()
    {
        if (_interactiveSelection is { } interactiveSelection && _viewModel is { HasImage: true } viewModel)
        {
            viewModel.SetCropBounds(interactiveSelection.Left, interactiveSelection.Top, interactiveSelection.Right,
                interactiveSelection.Bottom);
        }

        _interactiveSelection = null;
    }

    private static CropHandle GetCropHandle(string? tag)
    {
        return tag switch
        {
            "Move" => CropHandle.Move,
            "Left" => CropHandle.Left,
            "Top" => CropHandle.Top,
            "Right" => CropHandle.Right,
            "Bottom" => CropHandle.Bottom,
            "TopLeft" => CropHandle.TopLeft,
            "TopRight" => CropHandle.TopRight,
            "BottomLeft" => CropHandle.BottomLeft,
            "BottomRight" => CropHandle.BottomRight,
            _ => CropHandle.None,
        };
    }
}
