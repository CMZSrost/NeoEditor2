using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;
using NeoEditor.ViewModels;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.Views.UserControls;

public partial class HomePage : UserControl
{
    private HomePageViewModel? _vm;
    private DispatcherTimer? _configCheckTimer;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        _vm = DataContext as HomePageViewModel;
        UpdateSetupBanner();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateSetupBanner();
        // Periodically re-check config (it loads asynchronously after startup)
        _configCheckTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) =>
            {
                UpdateSetupBanner();
                if (!SetupBanner.IsVisible)
                    _configCheckTimer?.Stop(); // hide once config is found
            });
        _configCheckTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _configCheckTimer?.Stop();
    }

    private void UpdateSetupBanner()
    {
        try
        {
            var config = App.ServiceProvider!.GetRequiredService<IConfigService>().Config;
            if (config is null) { SetupBanner.IsVisible = true; return; }
            var hasGameRoot = !string.IsNullOrWhiteSpace(config.GameRootDir)
                && Directory.Exists(config.GameRootDir);
            SetupBanner.IsVisible = !hasGameRoot;
        }
        catch { SetupBanner.IsVisible = true; }
    }

    private void OnGoToSettingsClick(object? sender, RoutedEventArgs e)
    {
        var mainVm = App.ServiceProvider!.GetRequiredService<MainWindowViewModel>();
        mainVm.NavigateToSettings();
    }

    private void OnBrowseClick(object? sender, RoutedEventArgs e)
        => _vm?.BrowseGameDataCommand.Execute(null);

    private void OnNewModClick(object? sender, RoutedEventArgs e)
        => _vm?.NewModCommand.Execute(null);

    private void OnImportClick(object? sender, RoutedEventArgs e)
        => _vm?.ImportModCommand.Execute(null);

    private void OnRecentModClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int modId)
            foreach (var entry in _vm?.RecentMods ?? [])
                if (entry.ModId == modId)
                    _vm.OpenRecentModCommand.Execute(entry);
    }

    private void OnProfileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int profileId)
            foreach (var entry in _vm?.Profiles ?? [])
                if (entry.ProfileId == profileId)
                    _vm.OpenMergeFromProfileCommand.Execute(entry);
    }
}
