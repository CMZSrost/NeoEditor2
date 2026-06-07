using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class ImagePreviewView : UserControl
{
    public ImagePreviewView()
    {
        InitializeComponent();
        ImageList.AddHandler(InputElement.DoubleTappedEvent, OnImageDoubleTapped,
            RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnImageDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ImageList.SelectedItem is not ImageEntry entry || !entry.IsFound) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = entry.FullPath!,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
