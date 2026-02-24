using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.ViewModels;

public partial class SearchableDataGridViewModel : ViewModelBase
{
    public SearchableDataGridViewModel()
    {
        _logger = App.ServiceProvider!.GetRequiredService<ILogger<SearchableDataGridViewModel>>();
    }

    [ObservableProperty] public partial string FilterText { get; set; } = string.Empty;
    private readonly ILogger<SearchableDataGridViewModel> _logger;

    private bool FilterPredicate(object obj)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        var item = obj;
        return item?.GetType().GetProperties()
            .Any(prop => prop.GetValue(item)?.ToString()?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ==
                         true) ?? false;
    }
}