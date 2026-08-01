using NeoEditor.Services;
using NeoEditor.Helper;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace NeoEditor.Views.Dialog;

public partial class ImageViewerWindow : Window
{
    private double _zoom = 1.0;
    public ILocalizationService Loc => ViewServices.Loc;
    private bool _isPanning;
    private double _panX, _panY;
    private ScaleTransform _scale = new(1.0, 1.0);
    private TranslateTransform _translate = new(0, 0);
    private TransformGroup _group = new();

    public ImageViewerWindow()
    {
        InitializeComponent();
        _group.Children.Add(_scale);
        _group.Children.Add(_translate);
        MainImage.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        MainImage.RenderTransform = _group;
        MainImage.PointerWheelChanged += OnWheel;
        MainImage.PointerPressed += OnPress;
        MainImage.PointerMoved += OnMove;
        MainImage.PointerReleased += (_, _) => _isPanning = false;
    }

    public ImageViewerWindow(string title, IImage source) : this()
    {
        Title = title;
        MainImage.Source = source;
        UpdateZoomLabel();
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        var oldZoom = _zoom;
        _zoom *= e.Delta.Y > 0 ? 1.2 : 0.833;
        _zoom = Math.Clamp(_zoom, 0.1, 20.0);

        var pos = e.GetPosition(MainImage);
        var scaleRatio = _zoom / oldZoom;
        _translate.X = pos.X - scaleRatio * (pos.X - _translate.X);
        _translate.Y = pos.Y - scaleRatio * (pos.Y - _translate.Y);

        _scale.ScaleX = _zoom;
        _scale.ScaleY = _zoom;
        UpdateZoomLabel();
    }

    private void OnPress(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _panX = e.GetPosition(this).X;
            _panY = e.GetPosition(this).Y;
        }
    }

    private void OnMove(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;
        var dx = e.GetPosition(this).X - _panX;
        var dy = e.GetPosition(this).Y - _panY;
        _panX = e.GetPosition(this).X;
        _panY = e.GetPosition(this).Y;
        _translate.X += dx;
        _translate.Y += dy;
    }

    private void UpdateZoomLabel() => ZoomLabel.Text = $"{(int)(_zoom * 100)}%";

    public static void Show(string title, IImage image, Window? owner = null)
    {
        var win = new ImageViewerWindow(title, image);
        win.Show();
    }
}
