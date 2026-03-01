using System;
using System.Globalization;
using System.Net.Mime;
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
    public LocalizationService() : this(App.ServiceProvider!.GetRequiredService<IStringLocalizer<Resources>>())
    {
    }

    public LocalizationService(IStringLocalizer<Resources> localizer)
    {
        ResourceManager = localizer;
    }


    private readonly IStringLocalizer<Resources> ResourceManager;

    public CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

    public string this[string key] => ResourceManager.GetString(key, CurrentCulture) ?? key;

    public void SetCulture(CultureInfo culture)
    {
        if (Equals(CurrentCulture, culture))
        {
            Console.WriteLine($"Set Culture Equals {CurrentCulture.Name} {culture.Name}");
            return;
        }

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        SemiTheme.OverrideLocaleResources(Application.Current, culture);
        Console.WriteLine($"Set Culture to {culture.Name}");

        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged("Item[]");
        OnPropertyChanged("Item");
    }
}