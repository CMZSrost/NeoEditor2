using System;
using CommunityToolkit.Mvvm.ComponentModel;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;

namespace NeoEditor.Plugins.EntityEditor.ViewModels;

/// <summary>
/// Plugin-side base class for dock documents.
/// Mirrors NeoEditor.App's DocumentBase but uses DI-injected ILocalizationService
/// instead of ViewServices.Loc (service locator). Migrated during M10 Phase 5.
/// </summary>
public abstract partial class PluginDocumentBase : ObservableObject, IDocumentBase
{
    private string _title = string.Empty;
    private string? _localizedTitleKey;
    private object[] _localizedTitleArguments = [];

    /// <summary>DI-injected localization service. Exposed as a public property
    /// so XAML bindings ({Binding Loc[...]}) work with compiled bindings.</summary>
    public ILocalizationService Loc { get; }

    protected PluginDocumentBase(ILocalizationService loc)
    {
        Loc = loc;
        SetLocalizedTitle("Untitled");
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    [ObservableProperty] public partial bool CanClose { get; set; } = true;
    [ObservableProperty] public partial bool NeedNotifyWhenClose { get; set; }

    public void SetStaticTitle(string title)
    {
        _localizedTitleKey = null;
        _localizedTitleArguments = Array.Empty<object>();
        Title = title;
    }

    public void SetLocalizedTitle(string key, params object[] args)
    {
        _localizedTitleKey = key;
        _localizedTitleArguments = CloneArguments(args);
        Title = Loc[key, _localizedTitleArguments];
    }

    public virtual void RefreshLocalizedText()
    {
        if (!string.IsNullOrWhiteSpace(_localizedTitleKey))
        {
            Title = Loc[_localizedTitleKey, _localizedTitleArguments];
        }
    }

    private static object[] CloneArguments(object[] args)
    {
        return args.Length == 0 ? Array.Empty<object>() : (object[])args.Clone();
    }
}
