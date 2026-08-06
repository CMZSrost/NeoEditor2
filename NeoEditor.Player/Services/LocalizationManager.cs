using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace NeoEditor.Player.Services;

/// <summary>
/// Lightweight localization for the standalone player (Docs/42 v2.28): resx-backed with a
/// property-changed notifier so XAML bindings ({Binding [key], Source={x:Static ...}})
/// refresh when the language switches. Covers the UI shell (menus, welcome page, overlays,
/// status texts); the wiki detail markdown stays Chinese (game data semantics).
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private readonly ResourceManager _resources = new(
        "NeoEditor.Player.Localization.Resources", typeof(LocalizationManager).Assembly);

    public string CurrentLanguage { get; private set; } = "zh";

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("zh");

    /// <summary>Resx lookup — unknown keys fall back to the key itself.</summary>
    public string this[string key] => _resources.GetString(key, CurrentCulture) ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(string code)
    {
        CurrentLanguage = code;
        CurrentCulture = code.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo("en")
            : CultureInfo.GetCultureInfo("zh");
        // Empty property name → every {Binding [key]} re-evaluates.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
