using System;
using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.ViewModels;

/// <summary>
/// ViewModel for a single data tab in the data viewer.
/// Each tab represents one entity type (e.g. ItemType, Recipe, etc.) and holds
/// the source collection, the filtered/sorted ItemsSource, dirty state,
/// and its own per-tab editing and merge stores.
/// M9: Moved from App to DataViewer plugin.
/// </summary>
public sealed partial class GameDataTypeTabItem : ObservableObject
{
    private IEnumerable? _itemsSource;
    private string _baseHeader = "";
    private bool _dirtyWasSet;

    public required Type EntityType { get; init; }

    private bool _isEditorVisible = true;
    public bool IsEditorVisible
    {
        get => _isEditorVisible;
        set => SetProperty(ref _isEditorVisible, value);
    }

    public bool IsDirty => _dirtyWasSet;

    public string Header
    {
        get => _dirtyWasSet ? "● " + _baseHeader : _baseHeader;
        set
        {
            _baseHeader = value;
            OnPropertyChanged(nameof(Header));
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public void MarkDirty()
    {
        if (_dirtyWasSet) return;
        _dirtyWasSet = true;
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(IsDirty));
    }

    public void ClearDirty()
    {
        if (!_dirtyWasSet) return;
        _dirtyWasSet = false;
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Full, unfiltered source data. Mutations (add/remove) happen here.</summary>
    public required ObservableCollection<object> SourceCollection { get; set; }

    /// <summary>
    /// The collection bound to the DataGrid (DataGridCollectionView, sortable + filterable).
    /// </summary>
    public IEnumerable ItemsSource
    {
        get => _itemsSource ?? SourceCollection;
        set => SetProperty(ref _itemsSource, value);
    }

    /// <summary>Per-tab edit tracking store. Created on tab construction.</summary>
    public EditTrackingStore EditStore { get; } = new();

    /// <summary>Per-tab merge state store. Populated when in merge view.</summary>
    public EntityMergeStore? MergeStore { get; set; }
}
