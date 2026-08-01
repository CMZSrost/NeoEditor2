using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Microsoft.Extensions.Logging;
using NeoEditor.Infra.Services;

namespace NeoEditor.Services;

public class NotificationService : INotificationService
{
    private WindowNotificationManager? _notificationManager;
    private ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void SetNotificationManager(WindowNotificationManager manager)
    {
        _notificationManager = manager;
    }

    public void ShowSuccess(string message, string title = "成功")
    {
        _logger.LogInformation("<{Title}> {Message}", title, message);
        Show(new Notification(title, message, NotificationType.Success));
    }

    public void ShowError(string message, string title = "错误")
    {
        _logger.LogError("<{Title}> {Message}", title, message);
        Show(new Notification(title, message, NotificationType.Error));
    }

    public void ShowInfo(string message, string title = "提示")
    {
        _logger.LogInformation("<{Title}> {Message}", title, message);
        Show(new Notification(title, message, NotificationType.Information));
    }

    public void ShowWarning(string message, string title = "警告")
    {
        _logger.LogWarning("<{Title}> {Message}", title, message);
        Show(new Notification(title, message, NotificationType.Warning));
    }

    private void Show(INotification notification)
    {
        // 确保在 UI 线程上显示通知
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { _notificationManager?.Show(notification); });
    }
}