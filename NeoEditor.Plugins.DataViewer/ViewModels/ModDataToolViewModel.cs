using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Data.Model;

namespace NeoEditor.Plugins.DataViewer.ViewModels;

/// <summary>Context for the bottom DataTable Tool. Wraps the active ProfileInfo
/// (merge profile or single-mod profile) so DataTableView can bind to it via a DataTemplate.
/// B4: single-mod view removed — the bottom tool is always profile-shaped.</summary>
public partial class ModDataToolViewModel : ObservableObject
{
    [ObservableProperty] public partial ProfileInfo? ProfileInfo { get; set; }

    public void SetProfile(ProfileInfo info)
    {
        ProfileInfo = info;
        OnPropertyChanged(nameof(ProfileInfo));
    }

    public void Clear()
    {
        ProfileInfo = null;
        OnPropertyChanged(nameof(ProfileInfo));
    }
}

/// <summary>Placeholder context when no session is active.</summary>
public class DataTablePlaceholder { }
