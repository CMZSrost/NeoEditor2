using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace NeoEditor.ViewModels;

public abstract class ViewModelBase : ObservableRecipient
{
    protected ViewModelBase()
    {
        IsActive = true;
    }
}