using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

/// <summary>
/// Plugin-side base class for observable objects that need localization.
/// Mirrors NeoEditor.App's LocalizedObservableObject but uses DI-injected
/// ILocalizationService instead of ViewServices.Loc.
/// Created during M11 migration.
/// </summary>
public abstract class ImageToolObservableObject : ObservableObject
{
    public ILocalizationService Loc { get; }

    protected ImageToolObservableObject(ILocalizationService loc)
    {
        Loc = loc;
    }
}
