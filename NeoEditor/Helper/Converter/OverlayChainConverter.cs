using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NeoEditor.Helper.Converter;

public class OverlayPanelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Data.Model.Game.IEntity entity)
            return null;

        var chain = GenericDataGridHelper.GetOverlayChain(entity);
        if (chain.Count == 0)
        {
            var modName = GenericDataGridHelper.GetEntityModName(entity);
            if (string.IsNullOrEmpty(modName)) modName = "?";
            chain = new System.Collections.Generic.List<OverlayChainEntry>
                { new(modName, 0, typeof(object), "", entity.Subject) };
        }

        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { FontWeight = FontWeight.SemiBold, Text = "Overlay Chain" });

        // Determine which entry is active for this row
        // The entity's own source mod determines the arrow position
        var activeModName = GenericDataGridHelper.GetEntityModName(entity);
        var activeIndex = chain.FindIndex(e => e.ModName == activeModName);
        if (activeIndex < 0) activeIndex = chain.Count - 1; // fallback to winner

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
                Margin = new Avalonia.Thickness(0, 1),
                Padding = new Avalonia.Thickness(4, 2),
                CornerRadius = new Avalonia.CornerRadius(2),
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
                        GenericDataGridHelper.NavigateToByEntityId(navType, navEntityId);
                    else
                        GenericDataGridHelper.NavigateTo(navType, navId);
                };
            }

            panel.Children.Add(border);
        }

        return panel;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
