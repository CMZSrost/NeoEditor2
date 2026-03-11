using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NeoEditor.ViewModels;

public sealed class HelpMenuNode
{
    public string Header { get; init; } = string.Empty;
    public string? AbsolutePath { get; init; }
    public string? RelativePath { get; init; }
    public string? DocumentTitle { get; init; }
    public ICommand? Command { get; init; }
    public object? CommandParameter { get; init; }
    public ObservableCollection<HelpMenuNode> Children { get; } = new();
    public bool IsLeaf => !string.IsNullOrWhiteSpace(AbsolutePath);
    public bool HasChildren => Children.Count > 0;
    public IEnumerable<HelpMenuNode>? ChildItemsSource => HasChildren ? Children : null;
}