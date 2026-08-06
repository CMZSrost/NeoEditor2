using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;
using NeoEditor.Helper;
using Newtonsoft.Json;

namespace NeoEditor.ViewModels;

public abstract class LocalizedObservableObject : ObservableObject
{
    private ILocalizationService? _loc;

    [JsonIgnore]
    public ILocalizationService Loc
    {
        get => _loc ?? (ViewServices.Loc as ILocalizationService)!;
        set => _loc = value;
    }

    protected LocalizedObservableObject() { }

    protected LocalizedObservableObject(ILocalizationService loc)
    {
        _loc = loc;
    }
}

public abstract class ViewModelBase : ObservableRecipient
{
    private ILocalizationService? _loc;
    private INotificationService? _notification;
    private ILogger? _loggerInstance;

    [JsonIgnore]
    public ILocalizationService Loc
    {
        get => _loc ?? (ViewServices.Loc as ILocalizationService)!;
        set => _loc = value;
    }

    [JsonIgnore]
    public INotificationService Notification
    {
        get => _notification!;
        set => _notification = value;
    }

    [JsonIgnore]
    protected ILogger Logger
    {
        get => _loggerInstance!;
        set => _loggerInstance = value;
    }

    /// <summary>Per-instance correlation ID for log tracing. First 8 chars of a Guid.</summary>
    protected Guid ViewId { get; } = Guid.NewGuid();
    protected string IdPrefix => $"[{ViewId.ToString("N")[..8]}]";

    protected ViewModelBase()
    {
        IsActive = true;
    }

    protected ViewModelBase(ILocalizationService loc, INotificationService notification, ILogger? logger = null)
    {
        _loc = loc;
        _notification = notification;
        _loggerInstance = logger;
        IsActive = true;
    }
}