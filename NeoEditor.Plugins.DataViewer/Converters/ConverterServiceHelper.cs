using System;
using Avalonia;
using NeoEditor.Plugins.DataViewer.Services;

namespace NeoEditor.Plugins.DataViewer.Converters;

/// <summary>
/// Provides converters with access to DataTableService without using a static Instance property.
/// Resolves from Application.Current.Resources["Services"] (the root IServiceProvider set in App startup).
/// M9: replaces DataTableService.Instance static accessor.
/// </summary>
internal static class ConverterServiceHelper
{
    /// <summary>
    /// Resolve DataTableService from the application DI container.
    /// Returns null if the application is not yet initialized.
    /// </summary>
    public static DataTableService? DataTable =>
        (Application.Current?.Resources["Services"] as IServiceProvider)
            ?.GetService(typeof(DataTableService)) as DataTableService;
}
