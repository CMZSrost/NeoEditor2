using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NeoEditor.Services;

namespace NeoEditor.ViewModels;

public abstract class LocalizedObservableObject : ObservableObject
{
    private LocalizationService? _loc;

    public LocalizationService Loc
    {
        get => _loc ?? App.Localizor;
        set => _loc = value;
    }

    protected LocalizedObservableObject() { }

    protected LocalizedObservableObject(LocalizationService loc)
    {
        _loc = loc;
    }
}

public abstract class ViewModelBase : ObservableRecipient
{
    private LocalizationService? _loc;
    private INotificationService? _notification;
    private ILogger? _loggerInstance;

    public LocalizationService Loc
    {
        get => _loc ?? App.Localizor;
        set => _loc = value;
    }

    public INotificationService Notification
    {
        get => _notification ?? App.Notification;
        set => _notification = value;
    }

    protected ILogger Logger
    {
        get => _loggerInstance ?? App.Logger!;
        set => _loggerInstance = value;
    }

    /// <summary>Per-instance correlation ID for log tracing. First 8 chars of a Guid.</summary>
    protected Guid ViewId { get; } = Guid.NewGuid();
    protected string IdPrefix => $"[{ViewId.ToString("N")[..8]}]";

    protected ViewModelBase()
    {
        IsActive = true;
    }

    protected ViewModelBase(LocalizationService loc, INotificationService notification, ILogger? logger = null)
    {
        _loc = loc;
        _notification = notification;
        _loggerInstance = logger;
        IsActive = true;
    }
}