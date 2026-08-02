using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace NeoEditor.Plugins.AiChat.Converters;

/// <summary>
/// True → Right (user bubbles), False → Left (assistant/system bubbles).
/// Drives the ChatMimic bubble alignment in AiChatView.
/// </summary>
public sealed class BoolToHorizontalAlignmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// User bubbles get a translucent accent so they stay readable on both light and dark
/// themes; assistant bubbles get a faint neutral tint.
/// </summary>
public sealed class BoolToBubbleBrushConverter : IValueConverter
{
    private static readonly IBrush UserBrush = new SolidColorBrush(Color.FromArgb(0x46, 0x47, 0x8F, 0xD9));
    private static readonly IBrush AssistantBrush = new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? UserBrush : AssistantBrush;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}