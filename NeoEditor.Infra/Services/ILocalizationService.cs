using System.ComponentModel;
using System.Globalization;

namespace NeoEditor.Infra.Services;

/// <summary>Localization service interface — extracted from App to Infra per M9 plugin migration.</summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    CultureInfo CurrentCulture { get; }
    string this[string key] { get; }
    string this[string key, params object[] arguments] { get; }
    void SetCulture(CultureInfo culture);
}
