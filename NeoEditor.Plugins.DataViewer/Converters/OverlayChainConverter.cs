using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.Converters;

public class OverlayPanelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Data.Model.Game.IEntity entity)
            return null;

        var svc = ConverterServiceHelper.DataTable;
        var chain = svc?.GetOverlayChain(entity) ?? new List<OverlayChainEntry>();
        if (chain.Count == 0)
        {
            var modName = svc?.GetEntityModName(entity) ?? "?";
            if (string.IsNullOrEmpty(modName)) modName = "?";
            chain = new List<OverlayChainEntry>
                { new(modName, 0, typeof(object), "", entity.Subject) };
        }

        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { FontWeight = FontWeight.SemiBold, Text = "Overlay Chain" });

        var activeModName = svc?.GetEntityModName(entity) ?? "";
        var activeIndex = chain.FindIndex(e => e.ModName == activeModName);
        if (activeIndex < 0) activeIndex = chain.Count - 1;

        for (var i = 0; i < chain.Count; i++)
        {
            var entry = chain[i];
            var isActive = i == activeIndex;
            var prefix = isActive ? "→ " : "  ";
            var captured = entry;

            var tb = new TextBlock
            {
                Text = prefix + captured.Display,
                Foreground = isActive ? Brushes.Teal : Brushes.Gray,
                FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal,
            };
            var border = new Border
            {
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = new Thickness(0, 1),
                Padding = new Thickness(4, 2),
                CornerRadius = new CornerRadius(2),
                Child = tb
            };

            if (captured.EntityType != typeof(object))
            {
                var navType = captured.EntityType;
                var navEntityId = captured.EntityId;
                var navId = captured.Id;
                border.PointerPressed += (_, _) =>
                {
                    if (!string.IsNullOrEmpty(navEntityId))
                        svc?.NavigateToByEntityId(navType, navEntityId);
                    else
                        svc?.NavigateTo(navType, navId);
                };
            }

            panel.Children.Add(border);
        }

        return panel;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
