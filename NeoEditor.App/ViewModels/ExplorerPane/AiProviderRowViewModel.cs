using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Helper;

namespace NeoEditor.ViewModels.ExplorerPane;

/// <summary>
/// Observable row wrapper around a single <see cref="AiProviderConfig"/> for the Settings
/// provider list editor and the per-model provider dropdowns. Each editable field persists to
/// config.json on change (mirrors the ColumnOption save-on-change pattern).
/// </summary>
public class AiProviderRowViewModel : ObservableObject
{
    private readonly IConfigService _config;

    public AiProviderConfig Provider { get; }

    public AiProviderRowViewModel(AiProviderConfig provider, IConfigService config,
        Action<AiProviderRowViewModel>? removeRequested = null, string? removeToolTip = null)
    {
        Provider = provider;
        _config = config;
        RemoveToolTip = removeToolTip;
        RemoveCommand = new RelayCommand(() => removeRequested?.Invoke(this));
    }

    /// <summary>Stable provider id referenced by the per-model ProviderId fields.</summary>
    public string Id => Provider.Id;

    /// <summary>ComboBox display text: name, or "id (endpoint)" when unnamed.</summary>
    public string DisplayLabel =>
        !string.IsNullOrWhiteSpace(Provider.Name)
            ? Provider.Name
            : $"{Provider.Id} ({Provider.Endpoint})";

    /// <summary>Remove button command (wired to the parent provider list by the settings VM).</summary>
    public IRelayCommand RemoveCommand { get; }

    /// <summary>Localized tooltip for the remove button.</summary>
    public string? RemoveToolTip { get; }

    public string Name
    {
        get => Provider.Name;
        set
        {
            if (Provider.Name == value) return;
            Provider.Name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayLabel));
            AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public string Endpoint
    {
        get => Provider.Endpoint;
        set
        {
            if (Provider.Endpoint == value) return;
            Provider.Endpoint = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayLabel));
            AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    /// <summary>Plaintext key (decrypted in memory by ConfigService.LoadAsync).</summary>
    public string ApiKey
    {
        get => Provider.ApiKey;
        set
        {
            if (Provider.ApiKey == value) return;
            Provider.ApiKey = value;
            OnPropertyChanged();
            AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }
}
