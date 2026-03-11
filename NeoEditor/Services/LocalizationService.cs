using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NeoEditor.Assets;
using Semi.Avalonia;

namespace NeoEditor.Services;

public sealed class LocalizationService : ObservableObject
{
    public LocalizationService() : this(App.ServiceProvider.GetRequiredService<IStringLocalizer<Resources>>())
    {
    }

    public LocalizationService(IStringLocalizer<Resources> localizer)
    {
        _localizer = localizer;
    }

    private readonly IStringLocalizer<Resources> _localizer;

    public CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

    public string this[string key] => _localizer[key].Value;

    public string this[string key, params object[] arguments] => _localizer[key, arguments].Value;

    public void SetCulture(CultureInfo culture)
    {
        if (Equals(CurrentCulture, culture))
        {
            return;
        }

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;

        if (Application.Current is { } application)
        {
            SemiTheme.OverrideLocaleResources(application, culture);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(CurrentCulture)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item"));
    }
}