using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Services;

public interface IFilterService
{
    ObservableCollection<object> ApplyFilters(
        ObservableCollection<object> source,
        System.Type entityType,
        bool isMergeView,
        bool showAll,
        HashSet<string> overriddenEntityIds,
        int? selectedModId,
        string? filterText);
}
