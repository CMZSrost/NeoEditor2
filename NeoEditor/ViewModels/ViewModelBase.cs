using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Services;

namespace NeoEditor.ViewModels;

public abstract class LocalizedObservableObject : ObservableObject
{
    public LocalizationService Loc => App.Localizor;

    protected LocalizedObservableObject()
    {
    }
}

public abstract class ViewModelBase : ObservableRecipient
{
    public LocalizationService Loc => App.Localizor;

    protected ViewModelBase()
    {
        IsActive = true;
    }
}