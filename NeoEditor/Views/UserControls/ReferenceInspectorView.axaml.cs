using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Helper;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class ReferenceInspectorView : UserControl
{
    private ReferenceInspectorContent? _vm;

    public ReferenceInspectorView() => InitializeComponent();

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as ReferenceInspectorContent;
        if (_vm is not null)
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ReferenceInspectorContent.IsPinned))
                    UpdatePinState();
            };
        UpdatePinState();
    }

    private void UpdatePinState()
    {
        var pinned = _vm?.IsPinned == true;
        PinButton.Content = pinned ? "Unpin" : "Pin";
        OuterBorder.BorderBrush = pinned
            ? Avalonia.Media.Brushes.DarkOrange
            : Avalonia.Media.Brushes.Transparent;
        // Use a subtle semi-transparent overlay; don't change background completely
        OuterBorder.Background = pinned
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(30, 255, 140, 0))
            : Avalonia.Media.Brushes.Transparent;
    }

    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        _vm?.TogglePin();
        UpdatePinState();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => _vm?.GoBack();

    private void OnForwardClick(object? sender, RoutedEventArgs e) => _vm?.GoForward();

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || !_vm.HasContent || string.IsNullOrEmpty(_vm.TargetEntityId)) return;
        if (!Data.Constants.GameTypes.TryGetValue(_vm.TargetType, out var entityType)) return;

        // Try navigation within currently loaded views first
        GenericDataGridHelper.NavigateToByEntityId(entityType, _vm.TargetEntityId);

        // If the entity belongs to a mod, also send a message to open that mod's data
        if (_vm.TargetModId > 0)
        {
            try
            {
                await using var edb = await App.ServiceProvider!
                    .GetRequiredService<IDbContextFactory<EditorDbContext>>()
                    .CreateDbContextAsync();
                var modInfo = await edb.ModInfos.FindAsync(_vm.TargetModId);
                if (modInfo is not null)
                {
                    var messenger = App.ServiceProvider!.GetRequiredService<IMessenger>();
                    messenger.Send(new OpenModGameDataDocumentMessage(modInfo, ReadOnly: false));
                }
            }
            catch { /* navigation already attempted; if it worked, this is just extra */ }
        }
    }
}
