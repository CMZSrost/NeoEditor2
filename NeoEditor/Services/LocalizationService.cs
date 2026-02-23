using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;

namespace NeoEditor.Services;

public sealed class LocalizationService : ObservableObject
{
    private static readonly ResourceManager ResourceManager =
        new("NeoEditor.Assets.Resources", Assembly.GetExecutingAssembly());

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
        Console.WriteLine($"Set Culture to {culture.Name}");

        OnPropertyChanged(nameof(CurrentCulture));
        OnPropertyChanged("Item[]");
        OnPropertyChanged("Item");
    }
}