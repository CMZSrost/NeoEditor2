using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NeoEditor.Plugins.EntityEditor.Views;

public partial class ZoomableImageView : UserControl
{
    private double _zoom = 1.0;
    private bool _panning;
    private double _px, _py;
    private Point _panOrigin;

    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new(0, 0);
    private readonly TransformGroup _group = new();

    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<ZoomableImageView, IImage?>(nameof(Source));

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ZoomableImageView()
    {
        InitializeComponent();
        _group.Children.Add(_scale);
        _group.Children.Add(_translate);
        Image.RenderTransform = _group;
        Image.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

        SourceProperty.Changed.AddClassHandler<ZoomableImageView>((s, _) => s.OnSourceChanged());

        // Handle events on the ScrollViewer host for reliable capture
        Host.PointerWheelChanged += OnWheel;
        Host.PointerPressed += OnPress;
        Host.PointerMoved += OnMove;
        Host.PointerReleased += (_, _) => _panning = false;
    }

    private void OnSourceChanged()
    {
        Image.Source = Source;
        _zoom = 1.0;
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
        _translate.X = 0;
        _translate.Y = 0;
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (Source is null) return;
        var oldZ = _zoom;
        _zoom *= e.Delta.Y > 0 ? 1.2 : 0.833;
        _zoom = Math.Clamp(_zoom, 0.1, 50.0);

        var pos = e.GetPosition(Host);
        var ratio = _zoom / oldZ;
        _translate.X = pos.X - ratio * (pos.X - _translate.X);
        _translate.Y = pos.Y - ratio * (pos.Y - _translate.Y);

        _scale.ScaleX = _zoom;
        _scale.ScaleY = _zoom;
        e.Handled = true;
    }

    private void OnPress(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed ||
            (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && _zoom > 1.0))
        {
            _panning = true;
            _panOrigin = e.GetPosition(Host);
            _px = _translate.X;
            _py = _translate.Y;
            e.Handled = true;
        }
    }

    private void OnMove(object? sender, PointerEventArgs e)
    {
        if (!_panning) return;
        var pos = e.GetPosition(Host);
        _translate.X = _px + (pos.X - _panOrigin.X);
        _translate.Y = _py + (pos.Y - _panOrigin.Y);
    }
}
